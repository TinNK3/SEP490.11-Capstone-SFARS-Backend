using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SFARS.Domain.Interfaces.Infrastructure;
using SFARS.Domain.Models;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace SFARS.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(EmailMessageDto message, bool isBodyHtml = true)
        {
            try
            {
                var smtpHost = _configuration["EmailSettings:SmtpHost"];
                var smtpPortValue = _configuration["EmailSettings:SmtpPort"];
                var username = _configuration["EmailSettings:SmtpCredential:UserName"];
                var from = _configuration["EmailSettings:From"];

                if (string.IsNullOrWhiteSpace(smtpHost)
                    || string.IsNullOrWhiteSpace(smtpPortValue)
                    || string.IsNullOrWhiteSpace(username)
                    || string.IsNullOrWhiteSpace(from))
                {
                    _logger.LogWarning(
                        "EmailSettings missing. Host: {Host}, Port: {Port}, Username set: {HasUser}, From set: {HasFrom}",
                        smtpHost ?? "(null)",
                        smtpPortValue ?? "(null)",
                        !string.IsNullOrWhiteSpace(username),
                        !string.IsNullOrWhiteSpace(from));
                    return false;
                }

                var emailMessage = BuildMessage(message, isBodyHtml);

                _logger.LogInformation(
                    "Sending email to {Recipient} via {Host}:{Port} (User: {User})",
                    message.To,
                    smtpHost,
                    smtpPortValue,
                    username);

                return await SendAsync(emailMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}", message.To);
                return false;
            }
        }

        private MimeMessage BuildMessage(EmailMessageDto message, bool isBodyHtml)
        {
            var emailMessage = new MimeMessage();
            var from = _configuration["EmailSettings:From"] ?? string.Empty;
            var fromName = _configuration["EmailSettings:FromName"] ?? "SFARS";

            emailMessage.From.Add(new MailboxAddress(fromName, from));
            emailMessage.To.Add(MailboxAddress.Parse(message.To));
            emailMessage.Subject = message.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = isBodyHtml ? message.Body : null,
                TextBody = isBodyHtml ? null : message.Body
            };

            emailMessage.Body = builder.ToMessageBody();
            return emailMessage;
        }

        private async Task<bool> SendAsync(MimeMessage mailMessage)
        {
            var smtpHost = _configuration["EmailSettings:SmtpHost"];
            var smtpPortValue = _configuration["EmailSettings:SmtpPort"];
            var smtpPort = int.TryParse(smtpPortValue, out var port) ? port : 587;
            var username = _configuration["EmailSettings:SmtpCredential:UserName"];
            var password = _configuration["EmailSettings:SmtpCredential:Password"];

            using var client = new SmtpClient();

            try
            {
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.Auto);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(mailMessage);

                _logger.LogInformation("Email sent successfully to {Recipients}.",
                    string.Join(", ", mailMessage.To.Select(r => r.ToString())));

                return true;
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError("SMTP command error at {Host}:{Port} (User: {User}). Code: {ErrorCode}, Message: {Message}",
                    smtpHost, smtpPort, username, ex.StatusCode, ex.Message);
                return false;
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError("SMTP protocol error at {Host}:{Port} (User: {User}). Message: {Message}",
                    smtpHost, smtpPort, username, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email via {Host}:{Port} (User: {User}).", smtpHost, smtpPort, username);
                return false;
            }
            finally
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true);
                }
            }
        }
    }
}