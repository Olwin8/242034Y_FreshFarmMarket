using System.Threading.Tasks;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IEmailSenderService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);

        // ✅ ADD: dedicated method for password reset email (prevents CodeQL tainted htmlBody path)
        Task SendPasswordResetEmailAsync(string toEmail, string resetUrl);
    }
}
