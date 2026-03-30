using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SFARS.Application.HealthChecks
{
    public class ApiHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly IServer _server;

        public ApiHealthCheck(HttpClient httpClient, IServer server)
        {
            _httpClient = httpClient;
            _server = server;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Dynamically resolve the server's own listening address
                var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
                var baseAddress = addresses?.FirstOrDefault() ?? "http://localhost:5000";

                // Normalize: replace 0.0.0.0, +, or [::] with localhost
                baseAddress = baseAddress
                    .Replace("://0.0.0.0", "://localhost")
                    .Replace("://[::]", "://localhost")
                    .Replace("://+", "://localhost");

                var url = $"{baseAddress.TrimEnd('/')}/api";

                var response = await _httpClient.GetAsync(url, cancellationToken);
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