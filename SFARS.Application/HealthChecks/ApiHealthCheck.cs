using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SFARS.Application.HealthChecks
{
    public class ApiHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiHealthCheck(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["HealthChecks:ApiBaseUrl"] ?? "http://localhost:5000/api";
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(_baseUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy("API is reachable.");
                }
                else
                {
                    return HealthCheckResult.Unhealthy($"API returned status code {response.StatusCode}.");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("API is unreachable.", ex);
            }
        }
    }
}