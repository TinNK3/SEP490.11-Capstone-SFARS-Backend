using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SFARS.Domain.Interfaces;
using SFARS.Infrastructure.Data.Context;

namespace SFARS.Infrastructure.Data
{
    //	Summary:
    //		This class is to initialize database and seeding default data for the application
    public class DatabaseInitializer : IDatabaseInitializer
    {
        
        private readonly SFARSDbContext _context;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(ILogger<DatabaseInitializer> logger,
            SFARSDbContext context)
        {
            _context = context;
            _logger = logger;
        }

        //  Summary:
        //      Try to initialize database and its table if not exist
        public async Task InitializeAsync()
        {
            try
            {
                if (!await _context.Database.CanConnectAsync())
                {
                    _logger.LogWarning("Database cannot be connected to.");
                    return;
                }

                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("Database initialized successfully (migrations applied).");
                }
                else
                {
                    _logger.LogInformation("Database is up to date. No pending migrations.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing the database.");
            }
        }

        public async Task SeedAsync()
        {
            try
            {
                await TrySeedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        public Task TrySeedAsync()
        {
            throw new NotImplementedException();
        }
    }
}