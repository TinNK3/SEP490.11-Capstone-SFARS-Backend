using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SFARS.Application.Configurations;
using SFARS.Domain.Interfaces.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace SFARS.API.Extension
{
    public static class AuthFeatureExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
        {
            var jwt = config.GetSection("WebTokenSettings").Get<WebTokenSettings>()
                      ?? throw new InvalidOperationException("WebTokenSettings is missing in configuration.");

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwt.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.IssuerSigningKey)),

                ValidateIssuer = jwt.ValidateIssuer,
                ValidIssuer = jwt.ValidIssuer,

                ValidateAudience = jwt.ValidateAudience,
                ValidAudience = jwt.ValidAudience,

                RequireExpirationTime = jwt.RequireExpirationTime,
                ValidateLifetime = jwt.ValidateLifetime,
                ClockSkew = TimeSpan.Zero
            };

            services.AddSingleton(tokenValidationParameters);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;

                    options.TokenValidationParameters = tokenValidationParameters;

                    options.Events = new JwtBearerEvents
                    {
                        // SignalR: read JWT from query string (?access_token=...)
                        // because WebSocket cannot send custom headers
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            // Only apply to SignalR hub endpoints
                            if (!string.IsNullOrEmpty(accessToken)
                                && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },

                        OnTokenValidated = context =>
                        {
                            var blacklist = context.HttpContext.RequestServices
                                .GetRequiredService<ITokenBlacklistService>();
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("SFARS.API.JwtAuth");

                            var jti = context.Principal
                                ?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                            var userId = context.Principal
                                ?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                            logger.LogDebug(
                                "[JWT] Token validated — UserId: {UserId}, JTI: {Jti}, Path: {Path}",
                                userId, jti, context.HttpContext.Request.Path);

                            if (jti != null && blacklist.IsRevoked(jti))
                            {
                                logger.LogDebug(
                                    "[JWT] ACCESS DENIED — JTI {Jti} is blacklisted (user already signed out).",
                                    jti);
                                context.Fail("Token has been revoked.");
                            }

                            return Task.CompletedTask;
                        },

                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("SFARS.API.JwtAuth");
                            logger.LogDebug(
                                "[JWT] Authentication failed — Path: {Path}, Reason: {Reason}",
                                context.HttpContext.Request.Path,
                                context.Exception.Message);
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }
    }
}