using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderService _emailSender;
        private readonly IAuditLogService _auditLogService;
        private readonly AuthDbContext _db; // ✅ ADD
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSenderService emailSender,
            IAuditLogService auditLogService,
            AuthDbContext db, // ✅ ADD
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _auditLogService = auditLogService;
            _db = db; // ✅ ADD
            _logger = logger;
        }

        [BindProperty]
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var email = Email.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            // ✅ avoid account enumeration
            TempData["Info"] = "If the email exists, a reset link has been sent.";

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var ua = HttpContext.Request.Headers["User-Agent"].ToString();

            if (user == null)
            {
                await _auditLogService.LogSecurityEventAsync(
                    null,
                    email,
                    "ForgotPassword",
                    "Password reset requested (email not found)",
                    ip,
                    ua);

                return RedirectToPage("/ForgotPassword");
            }

            user.LastPasswordResetRequest = DateTime.Now;
            await _userManager.UpdateAsync(user);

            // ✅ Generate token (DO NOT send token in email)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // ✅ Create safe request id to send via email
            var rid = Guid.NewGuid().ToString("N");

            // ✅ Store token server-side with expiry
            var req = new PasswordResetRequest
            {
                RequestId = rid,
                UserId = user.Id,
                Email = user.Email ?? email,
                Token = token,
                CreatedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddMinutes(15),
                Used = false
            };

            _db.PasswordResetRequests.Add(req);
            await _db.SaveChangesAsync();

            // ✅ Email link only contains rid (safe)
            var callbackUrl = Url.Page(
                "/ResetPassword",
                pageHandler: null,
                values: new { rid },
                protocol: "https");

            var safeUrl = HtmlEncoder.Default.Encode(callbackUrl ?? "");

            var subject = "Fresh Farm Market - Reset Password";

            try
            {
                await _emailSender.SendPasswordResetEmailAsync(user.Email!, safeUrl);

                await _auditLogService.LogSecurityEventAsync(
                    user.Id,
                    user.Email ?? email,
                    "ForgotPassword",
                    "Password reset link sent",
                    ip,
                    ua);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reset email.");

                await _auditLogService.LogSecurityEventAsync(
                    user.Id,
                    user.Email ?? email,
                    "ForgotPassword",
                    "Failed to send password reset email",
                    ip,
                    ua);
            }

            return RedirectToPage("/ForgotPassword");
        }
    }
}
