using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Net.payOS;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
using SFARS.Domain.Interfaces.Services;
using SFARS.Infrastructure.Configurations;
using SFARS.Infrastructure.Data;
using SFARS.Infrastructure.Data.Context;
using SFARS.Infrastructure.Repositories;
using SFARS.Infrastructure.Services;

namespace SFARS.Infrastructure;

public static class DependencyInjection
{
        //	Summary:
        //		This class is to configure services for infrastructure layer
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,
             IConfiguration configuration)
        {
            // Retrieve connectionStr from application configuration
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Add application DbContext 
            services.AddDbContext<SFARSDbContext>(options => options.UseSqlServer(connectionString, x => x.UseNetTopologySuite()));

            // Configure Cloudinary
            services.Configure<CloudinarySettings>(
                configuration.GetSection(CloudinarySettings.SectionName));

            // Configure Storage options
            services.Configure<StorageOptions>(
                configuration.GetSection(StorageOptions.SectionName));

            // Configure Classification options
            services.Configure<ClassificationModelOptions>(
                configuration.GetSection(ClassificationModelOptions.SectionName));

            // Configure YOLO Detection options
            services.Configure<YoloDetectionOptions>(
                configuration.GetSection(YoloDetectionOptions.SectionName));

            // Configure PayOS
            services.Configure<PayOSSettings>(
                configuration.GetSection(PayOSSettings.SectionName));

            // Register PayOS SDK instance as singleton
            services.AddSingleton(sp =>
            {
                // Reuse validated options from DI instead of parsing configuration section again.
                var payOsSettings = sp.GetRequiredService<IOptions<PayOSSettings>>().Value;
                
                if (payOsSettings == null ||
                    string.IsNullOrWhiteSpace(payOsSettings.ClientId) ||
                    string.IsNullOrWhiteSpace(payOsSettings.ApiKey) ||
                    string.IsNullOrWhiteSpace(payOsSettings.ChecksumKey))
                {
                    throw new InvalidOperationException("PayOS configuration is missing or incomplete.");
                }
                
                return new PayOS(payOsSettings.ClientId, payOsSettings.ApiKey, payOsSettings.ChecksumKey);
            });

            // Register Infrastructure services
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            
            services.AddScoped<IExternalAuthService, ExternalAuthService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IFileStorageService, CloudinaryStorageService>();
            services.AddScoped<IPayOSService, PayOSService>();
            
            // AI Services — 2-Stage Pipeline
            services.AddSingleton<ISnakeDetectionService, YoloSnakeDetectionService>();
            services.AddSingleton<ISpeciesClassificationService, EfficientNetClassificationService>();
            services.AddHttpClient<IGeminiAiService, GeminiAiService>();
            services.AddScoped<ISpeechToTextService, GeminiSpeechToTextService>();
            
            // Register repositories
            services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Location services
            services.AddSingleton<ILocationCacheService, RedisLocationCacheService>();
            services.AddScoped<ILocationBroadcastService, LocationBroadcastService>();

            // SOS-specific services
            services.AddSingleton<ISosSpamGuardService, SosSpamGuardService>();
            services.AddScoped<IFcmPushService, FcmPushService>();

            // Hangfire — background job processing for tiered dispatch
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    // Schema name to avoid cluttering main app tables
                    SchemaName = "HangFire",
                    CommandBatchMaxTimeout        = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout    = TimeSpan.FromMinutes(5),
                    QueuePollInterval             = TimeSpan.Zero,
                    UseRecommendedIsolationLevel  = true,
                    DisableGlobalLocks            = true
                }));

            services.AddHangfireServer(opt =>
            {
                opt.WorkerCount = 2;   // Lightweight — only dispatch jobs
                opt.Queues      = new[] { "dispatch", "default" };
            });

            return services;
        }
}