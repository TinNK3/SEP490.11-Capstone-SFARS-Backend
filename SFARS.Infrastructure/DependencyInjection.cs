using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
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

            // Register Infrastructure services
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            
            services.AddScoped<IExternalAuthService, ExternalAuthService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IFileStorageService, CloudinaryStorageService>();
            
            // AI Services
            services.AddSingleton<IYoloInferenceService, YoloInferenceService>();
            services.AddHttpClient<IGeminiAiService, GeminiAiService>();
            
            // Register repositories
            services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Location services
            services.AddSingleton<ILocationCacheService, RedisLocationCacheService>();
            services.AddScoped<ILocationBroadcastService, LocationBroadcastService>();

            // SOS-specific services
            services.AddSingleton<ISosSpamGuardService, SosSpamGuardService>();

            return services;
        }
}