using Microsoft.Extensions.Logging;
using SFARS.Application.Common;
using SFARS.Application.Interfaces.Services;
using SFARS.Domain.Entities;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;

namespace SFARS.Application.Services;

public class EmailJobService : IEmailJobService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailJobService> _logger;

    public EmailJobService(IEmailService emailService, ILogger<EmailJobService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendPasswordChangedEmailAsync(string email, DateTime changedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Skip password change email job because email is empty.");
            return;
        }

        var emailMessageDto = new EmailMessageDto
        {
            To = email,
            Subject = "Password Changed Successfully",
            Body = $"Your password was changed at {changedAtUtc:O}. If you didn't make this change, please reset your password immediately."
        };

        var isSent = await _emailService.SendEmailAsync(emailMessageDto, isBodyHtml: false);
        if (!isSent)
        {
            throw new InvalidOperationException($"Failed to send password change email to {email}.");
        }

        _logger.LogInformation("Password change email sent to {Email}", email);
    }

    public async Task SendRescuerRoleAssignedEmailAsync(string email, string? firstName, string? lastName, string password)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Skip rescuer role assignment email job because email is empty.");
            return;
        }

        var user = new User
        {
            Email = email,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty
        };

        var emailMessage = EmailTemplateFactory.BuildRescuerRoleAssignedEmail(user, password);
        var isSent = await _emailService.SendEmailAsync(emailMessage, isBodyHtml: true);
        if (!isSent)
        {
            throw new InvalidOperationException($"Failed to send rescuer role assignment email to {email}.");
        }

        _logger.LogInformation("Rescuer role assignment email sent to {Email}", email);
    }
}
