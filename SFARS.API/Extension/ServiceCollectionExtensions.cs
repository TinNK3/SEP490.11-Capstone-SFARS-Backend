using Microsoft.Data.SqlClient;
using Serilog;
using SFARS.Application.Configurations;
using SFARS.Application.HealthChecks;
using SFARS.Domain.Common.Constants;
using SFARS.Infrastructure.Configurations;
using StackExchange.Redis;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using SFARS.API.Converters;

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
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
                    
                // Global Timezone Formatter - convert DateTime to Vietnam Time +07:00
                options.JsonSerializerOptions.Converters.Add(new VietnamTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new VietnamNullableTimeConverter());
                options.JsonSerializerOptions.Converters.Add(new VietnamTimeOffsetConverter());
                options.JsonSerializerOptions.Converters.Add(new VietnamNullableTimeOffsetConverter());
            });

            // Increase form-data upload limit
            services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 209715200; // 200MB
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
            services.Configure<ClassificationModelOptions>(builder.Configuration.GetSection(ClassificationModelOptions.SectionName));
            services.Configure<MlopsOptions>(builder.Configuration.GetSection("Mlops"));

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

        public static IServiceCollection ConfigureRedisIntegration(this IServiceCollection services,
            IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis")
                ?? "localhost:6379,abortConnect=false";

            // Redis multiplexer is shared across app services.
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisConnectionString));

            // SignalR uses Redis backplane for multi-instance support.
            services.AddSignalR()
                .AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix =
                        RedisChannel.Literal(LocationConstants.SignalRRedisChannelPrefix);
                });

            return services;
        }
    }
}