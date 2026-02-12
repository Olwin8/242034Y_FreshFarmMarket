using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace _242034Y_FreshFarmMarket.Services
{
    public class SmtpEmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _config;

        public SmtpEmailSenderService(IConfiguration config)
        {
            _config = config;
        }

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
                string.IsNullOrWhiteSpace(enableSslStr) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SMTP settings are missing or incomplete in appsettings.json");
            }

            int port = int.Parse(portStr);
            bool enableSsl = bool.TryParse(enableSslStr, out var v) && v;

            if (!enableSsl)
            {
                throw new InvalidOperationException("SMTP SSL/TLS must be enabled (Smtp:EnableSsl = true) when sending sensitive emails.");
            }

            using var message = new MailMessage();
            message.From = new MailAddress(fromEmail, fromName ?? "Fresh Farm Market");
            message.To.Add(toEmail);
            message.Subject = subject;
            // Ensure body is not null and prevent unintended raw data exposure
            if (string.IsNullOrWhiteSpace(htmlBody))
            {
                throw new InvalidOperationException("Email body cannot be empty.");
            }

            // Validate that the HTML body does not contain unsafe or non-HTTPS links
            ValidateEmailBody(htmlBody);

            // OPTIONAL but recommended: enforce HTML-only safe content
            message.Body = htmlBody;
            message.IsBodyHtml = true;
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.SubjectEncoding = System.Text.Encoding.UTF8;


            using var client = new SmtpClient(host, port);
            client.EnableSsl = enableSsl;
            client.Credentials = new NetworkCredential(username, password);

            await client.SendMailAsync(message);
        }

        /// <summary>
        /// Performs basic validation on the HTML email body to reduce the risk of
        /// transmitting attacker-controlled or unsafe links to the user.
        /// </summary>
        /// <param name="htmlBody">The HTML body to validate.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the body contains disallowed or unsafe links.
        /// </exception>
        private static void ValidateEmailBody(string htmlBody)
        {
            // Match href attributes in <a> tags: href="..." or href='...'
            var hrefPattern = "href\\s*=\\s*\"(?<url>[^\"]*)\"|href\\s*=\\s*'(?<url>[^']*)'";
            var matches = Regex.Matches(htmlBody, hrefPattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var url = match.Groups["url"].Value.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    continue;
                }

                // Disallow javascript: and other executable schemes
                if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Email contains disallowed links.");
                }

                // If absolute URL, require HTTPS scheme
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Email contains non-HTTPS links, which are not allowed.");
                    }
                }
                // Relative URLs are allowed (they will resolve against the application's host).
            }
        }
    }
}
