using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFARS.Domain.Interfaces;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Interfaces.Repositories.Base;
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

            // Register Infrastructure services
            services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();
            
            services.AddScoped<IExternalAuthService, ExternalAuthService>();
            services.AddScoped<IEmailService, EmailService>();
            
            // Register repositories
            services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
}