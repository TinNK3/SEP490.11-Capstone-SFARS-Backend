using Hangfire;

namespace SFARS.Application.Interfaces.Services;

public interface IEmailJobService
{
    [Queue("dispatch")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task SendPasswordChangedEmailAsync(string email, DateTime changedAtUtc);

    [Queue("dispatch")]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    Task SendRescuerRoleAssignedEmailAsync(string email, string? firstName, string? lastName, string password);
}
