using Microsoft.OpenApi.Models;

namespace SFARS.API.Extension
{

    /// <summary>
    /// This static class contains extension methods for configuring Swagger in the service collection and application pipeline.
    /// </summary>
    public static class SwaggerFeatureExtensions
    {
        public static IServiceCollection AddSwaggerFeature(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "SFARS.API",
                    Version = "v1"
                });

                // Enforce camelCase query parameters in Swagger UI
                c.OperationFilter<CamelCaseQueryParameterFilter>();

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter: Bearer {your JWT token}"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }

        public static WebApplication WithSwagger(this WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json",
                    "SFARS API V1");
            });

            return app;
        }
    }
}