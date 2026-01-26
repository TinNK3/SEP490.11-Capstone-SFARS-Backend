using SFARS.Domain.Interfaces;

namespace SFARS.API.Extensions
{
    public static class InitializeExtensions
    {
        // Summary:
        //      Progress initialize database (Migrate, Seed Data)
        public static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            // Create IServiceScope to resolve scoped services
            using (var scope = app.Services.CreateScope())
            {
                // Resolve IDatabaseInitializer
                var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                // Resolve ILogger
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("DatabaseInitializer");

                try
                {
                    logger.LogInformation("Starting database initialization...");
                    
                    // Initialize database (if not exist)
                    await initializer.InitializeAsync();
                    logger.LogInformation("Database initialized successfully.");

                    // Seeding default data 
                    await initializer.SeedAsync();
                    logger.LogInformation("Database seeding completed.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database initialization failed: {Message}", ex.Message);
                    throw; // Re-throw to prevent app from starting with invalid state
                }
            }
        }
    }
}
