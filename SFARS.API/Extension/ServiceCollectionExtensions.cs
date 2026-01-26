using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SFARS.Application.Configurations;
using SFARS.Application.HealthChecks;
using System.Data.Common;
using System.Text;

namespace SFARS.API.Extension
{
    //  Summary:
    //      This class is to configure services for presentation layer 
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Add controllers
            services.AddControllers();
            // Configures ApiExplorer
            services.AddEndpointsApiExplorer();
            // Add swagger
            services.AddSwaggerGen();

            return services;
        }

        public static IServiceCollection ConfigureSerilog(this IServiceCollection services, WebApplicationBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Debug()
                .WriteTo.Console()
                .Enrich.WithProperty("Environment", builder.Environment)
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            builder.Host.UseSerilog();

            return services;
        }

        public static IServiceCollection ConfigureHealthCheckServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddSingleton<AggregatedHealthCheckService>();
            services.AddScoped<DbConnection>(sp =>
                new SqlConnection(configuration.GetConnectionString("DefaultConnectionStr")));

            return services;
        }

        public static IServiceCollection ConfigureAppSettings(this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            // Configure AppSettings
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
            
            // Configure WebTokenSettings for JWT
            services.Configure<WebTokenSettings>(configuration.GetSection("WebTokenSettings"));

            #region Development stage
            if (env.IsDevelopment()) // Is Development env
            {

            }
            #endregion
            #region Production stage
            else if (env.IsProduction()) // Is Production env
            {

            }
            #endregion
            #region Staging 
            else if (env.IsStaging()) // Is Staging env
            {

            }
            #endregion

            return services;
        }

        public static IServiceCollection ConfigureJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var webTokenSettings = configuration.GetSection("WebTokenSettings").Get<WebTokenSettings>();
            
            if (webTokenSettings == null)
            {
                throw new InvalidOperationException("WebTokenSettings is not configured properly in appsettings.json");
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = webTokenSettings.ValidateIssuerSigningKey,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(webTokenSettings.IssuerSigningKey)),
                    ValidateIssuer = webTokenSettings.ValidateIssuer,
                    ValidIssuer = webTokenSettings.ValidIssuer,
                    ValidateAudience = webTokenSettings.ValidateAudience,
                    ValidAudience = webTokenSettings.ValidAudience,
                    ValidateLifetime = webTokenSettings.ValidateLifetime,
                    RequireExpirationTime = webTokenSettings.RequireExpirationTime,
                    ClockSkew = TimeSpan.Zero
                };
            });

            return services;
        }
    }
}
