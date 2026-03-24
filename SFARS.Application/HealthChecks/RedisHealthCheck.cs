using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SFARS.Application.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redisConnection;

        public RedisHealthCheck(IConnectionMultiplexer redisConnection)
        {
            _redisConnection = redisConnection;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_redisConnection.IsConnected)
                {
                    return HealthCheckResult.Unhealthy("Redis is unreachable.");
                }

                // Execute a lightweight round-trip command to verify connectivity.
                await _redisConnection.GetDatabase().PingAsync().ConfigureAwait(false);
                return HealthCheckResult.Healthy("Redis is reachable.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
            }
        }
    }
}
