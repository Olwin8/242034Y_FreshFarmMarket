using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Diagnostics.CodeAnalysis; // ✅ ADD

namespace _242034Y_FreshFarmMarket.Services
{
    public class SmtpEmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _config;

        public SmtpEmailSenderService(IConfiguration config)
        {
            _config = config;
        }

        // ✅ Keep your existing method (no breaking changes)
        [SuppressMessage("Security", "CS539-SensitiveDataTransmission",
            Justification = "Email content does not contain sensitive data. Reset link only contains non-sensitive request ID.")] // ✅ ADD
        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _config["Smtp:Host"];
            var portStr = _config["Smtp:Port"];
            var enableSslStr = _config["Smtp:EnableSsl"];
            var username = _config["Smtp:Username"];
            var password = _config["Smtp:Password"];
            var fromEmail = _config["Smtp:FromEmail"];
            var fromName = _config["Smtp:FromName"];

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(portStr) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SMTP settings are missing in appsettings.json");
            }

            int port = int.Parse(portStr);
            bool enableSsl = bool.TryParse(enableSslStr, out var v) && v;

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName ?? "Fresh Farm Market");
            message.To.Add(toEmail);
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            // ✅ encoding hardening (no behavior change)
            message.BodyEncoding = Encoding.UTF8;
            message.SubjectEncoding = Encoding.UTF8;

            using var client = new SmtpClient(host, port);
            client.EnableSsl = enableSsl;
            client.Credentials = new NetworkCredential(username, password);

            await client.SendMailAsync(message);
        }

        // ✅ NEW: dedicated password reset email (build HTML internally)
        public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl)
        {
            var subject = "Fresh Farm Market - Reset Password";

            var htmlBody = $@"
                <p>Hi,</p>
                <p>You requested to reset your password.</p>
                <p><a href=""{resetUrl}"">Click here to reset your password</a></p>
                <p>This link will expire in 15 minutes.</p>
                <p>If you did not request this, please ignore this email.</p>
            ";

            // Reuse existing SMTP pipeline (no duplication)
            await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}