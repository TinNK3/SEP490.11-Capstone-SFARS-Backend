using SFARS.Domain.Models;

namespace SFARS.Domain.Interfaces.Infrastructure
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(EmailMessageDto message, bool isBodyHtml = true);
    }
}