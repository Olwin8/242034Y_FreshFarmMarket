namespace _242034Y_FreshFarmMarket.Services
{
    public interface IEmailSenderService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }
}
