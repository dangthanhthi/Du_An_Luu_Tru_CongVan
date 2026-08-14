using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Net.Mail;
using System.Threading.Tasks;

namespace NotificationService.Services
{
    public interface IEmailService
    {
        Task<(bool IsSuccess, string? ErrorMessage)> SendEmailAsync(string toEmail, string subject, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<(bool IsSuccess, string? ErrorMessage)> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Thêm chốt chặn null
                var username = _config["Smtp:Username"] ?? string.Empty;
                var password = _config["Smtp:Password"] ?? string.Empty;
                var host = _config["Smtp:Host"] ?? string.Empty;

                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse(username));
                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = body };
                email.Body = builder.ToMessageBody();

                using var smtp = new MailKit.Net.Smtp.SmtpClient();

                var port = int.Parse(_config["Smtp:Port"] ?? "587");
                var useSsl = bool.Parse(_config["Smtp:UseSSL"] ?? "false");
                var options = useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

                await smtp.ConnectAsync(host, port, options);
                await smtp.AuthenticateAsync(username, password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi email tới {email}", toEmail);
                return (false, ex.Message);
            }
        }
    }
}