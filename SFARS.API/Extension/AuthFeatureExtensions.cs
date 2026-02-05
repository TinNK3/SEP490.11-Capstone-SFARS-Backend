using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SFARS.Application.Configurations;
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
                });

            return services;
        }
    }
}