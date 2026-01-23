using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SFARS.Infrastructure.Data.Context;

/// <summary>
/// Design-time factory for creating SFARSDbContext instances.
/// This is used by EF Core tools (e.g., dotnet ef migrations) to instantiate the DbContext
/// without requiring the full application startup with all dependencies.
/// </summary>
public class SFARSDbContextFactory : IDesignTimeDbContextFactory<SFARSDbContext>
{
    public SFARSDbContext CreateDbContext(string[] args)
    {
        // Determine the base path for finding appsettings.json
        // When running 'dotnet ef' from different directories, we need to find SFARS.API
        var currentDirectory = Directory.GetCurrentDirectory();
        string basePath;

        // Check if we're in SFARS.Infrastructure directory
        if (currentDirectory.EndsWith("SFARS.Infrastructure"))
        {
            basePath = Path.Combine(currentDirectory, "..", "SFARS.API");
        }
        // Check if SFARS.API exists as subdirectory (running from solution root)
        else if (Directory.Exists(Path.Combine(currentDirectory, "SFARS.API")))
        {
            basePath = Path.Combine(currentDirectory, "SFARS.API");
        }
        // Otherwise try to navigate up and find SFARS.API
        else
        {
            basePath = Path.Combine(currentDirectory, "..", "SFARS.API");
        }

        basePath = Path.GetFullPath(basePath);

        // Validate that appsettings.json exists
        var appSettingsPath = Path.Combine(basePath, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            throw new InvalidOperationException(
                $"appsettings.json not found at: {appSettingsPath}. " +
                $"Current directory: {currentDirectory}");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string 'DefaultConnectionStr' not found in configuration. " +
                $"BasePath: {basePath}");
        }

        // Create DbContextOptions
        var optionsBuilder = new DbContextOptionsBuilder<SFARSDbContext>();
        optionsBuilder.UseSqlServer(connectionString, 
            sqlServerOptions => 
            {
                sqlServerOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                sqlServerOptions.UseNetTopologySuite();
            });

        return new SFARSDbContext(optionsBuilder.Options);
    }
}