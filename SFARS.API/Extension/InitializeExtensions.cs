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
                // Resolve Serilog.ILogger
                var logger = scope.ServiceProvider.GetRequiredService<Serilog.ILogger>();

                try
                {
                    // Initialize database (if not exist)
                    await initializer.InitializeAsync();

                    // Seeding default data 
                    await initializer.SeedAsync();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                }
            }
        }
    }
}
