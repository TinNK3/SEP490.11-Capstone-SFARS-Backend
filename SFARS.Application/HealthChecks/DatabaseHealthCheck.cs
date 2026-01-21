using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SFARS.Infrastructure.Data.Context;

namespace SFARS.Application.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly SFARSDbContext _dbContext;

    public DatabaseHealthCheck(SFARSDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await connection.CloseAsync();
            return HealthCheckResult.Healthy("Database connection is healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection is unhealthy.", ex);
        }
    }
}