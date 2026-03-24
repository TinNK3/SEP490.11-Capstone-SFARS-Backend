using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SFARS.API.Payloads;
using SFARS.Application.HealthChecks;
using System.Net;

namespace SFARS.API.Controller;

[ApiController]
public class HealthCheckController : ControllerBase
{
    private readonly AggregatedHealthCheckService _aggregatedHealthCheckService;

    public HealthCheckController(AggregatedHealthCheckService aggregatedHealthCheckService)
    {
        _aggregatedHealthCheckService = aggregatedHealthCheckService;
    }

    /// <summary>
    /// Handles a health check request and returns an HTTP 200 OK response.
    /// </summary>
    /// <returns>An IActionResult indicating a successful health check.</returns>
    //[Authorize]
    [HttpGet(APIRoute.HealthCheck.BaseUrl)]
    public async Task<IActionResult> ForCheckAsync()
    {
        // Mark as complete task
        await Task.CompletedTask;
        return Ok();
    }

    /// <summary>
    /// Retrieves the aggregated health status of system components.
    /// </summary>
    /// <returns>An HTTP 200 response with health status details if all components are healthy; otherwise, an HTTP 503 response
    /// with the same details.</returns>
    //[Authorize]
    [HttpGet(APIRoute.HealthCheck.Check)]
    public async Task<IActionResult> GetHealthStatusAsync()
    {
        var healthStatus = await _aggregatedHealthCheckService.GetHealthStatusAsync();

        var response = healthStatus.Select(entry => new
        {
            Name = entry.Key,
            Status = entry.Value.Status.ToString(),
            Description = string.IsNullOrWhiteSpace(entry.Value.Description)
                ? (entry.Value.Status == HealthStatus.Healthy
                    ? $"{entry.Key} is healthy."
                    : $"{entry.Key} health check did not return a description.")
                : entry.Value.Description
        });

        bool isHealthy = healthStatus.All(entry => entry.Value.Status == HealthStatus.Healthy);

        return isHealthy ? Ok(response) : StatusCode((int)HttpStatusCode.ServiceUnavailable, response);
    }
}