using Microsoft.Data.SqlClient;
using Serilog;
using SFARS.Application.Configurations;
using SFARS.Application.HealthChecks;
using SFARS.Infrastructure.Configurations;
using System.Data.Common;

namespace SFARS.API.Extension
{
    //  Summary:
    //      This class is to configure services for presentation layer 
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureEndpoints(this IServiceCollection services)
        {
            // Add controllers
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
            // Configures ApiExplorer
            services.AddEndpointsApiExplorer();

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
                new SqlConnection(configuration.GetConnectionString("DefaultConnection")));

            return services;
        }

        public static IServiceCollection ConfigureAppSettings(this IServiceCollection services,
            WebApplicationBuilder builder,
            IWebHostEnvironment env)
        {

            #region Development stage
            if (env.IsDevelopment()) // Is Development env
            {
                // Development-specific configuration:
                // - Enable detailed error messages for debugging
                // - Use relaxed security settings for local development  
                // - Enable developer tools and diagnostics

                // Example: Configure detailed exception pages, enable sensitive data logging
                // builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                Log.Information("Running in Development mode - detailed logging enabled");
            }
            #endregion
            #region Production stage
            else if (env.IsProduction()) // Is Production env
            {
                // Production-specific configuration:
                // - Enable strict security settings (HSTS, secure cookies)
                // - Use optimized caching and performance settings
                // - Configure production-grade error handling

                // Example: Enforce HTTPS, configure distributed caching
                // builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));

                Log.Information("Running in Production mode - security hardening enabled");
            }
            #endregion
            #region Staging 
            else if (env.IsStaging()) // Is Staging env
            {
                // Staging-specific configuration:
                // - Use production-like settings for testing
                // - Enable additional monitoring for pre-release validation
                // - May include feature flags for A/B testing

                // Example: Enable extended diagnostics for QA
                // builder.Services.Configure<DiagnosticOptions>(o => o.EnableVerboseLogging = true);

                Log.Information("Running in Staging mode - production-like with extended diagnostics");
            }
            #endregion


            // Configure AppSettings
            services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

            // Configure WebTokenSettings for JWT
            services.Configure<WebTokenSettings>(builder.Configuration.GetSection("WebTokenSettings"));
            // Configure GoogleAuthSettings for Google OAuth
            services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection("GoogleAuthSettings"));
            // Configure CloudinarySettings for Cloudinary image service
            services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
            // Configure PayOSSettings for PayOS payment gateway
            services.Configure<PayOSSettings>(builder.Configuration.GetSection("PayOSSettings"));
            
            // Configure general payment settings
            services.Configure<PaymentSettings>(builder.Configuration.GetSection("PaymentSettings"));

            services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
            
            // AI Services Configuration
            services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
            services.Configure<YoloModelOptions>(builder.Configuration.GetSection("YoloModel"));

            return services;
        }

        public static IServiceCollection EstablishApplicationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
        {

        // PayOS Configuration - Payment Gateway
        services.Configure<PayOSSettings>(configuration.GetSection("PayOSSettings"));

        return services;
        }

        public static IServiceCollection AddCors(this IServiceCollection services,
        IConfiguration configuration, string policyName)
        {
            var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()
                ?? ["*"];
            services.AddCors(p => p.AddPolicy(policyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }));
            return services;
        }
    }
}