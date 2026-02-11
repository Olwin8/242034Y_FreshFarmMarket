using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class LoginWith2faModel : PageModel
    {
        private const string TwoFactorProvider = "EmailOTP";

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSenderService _emailSender;
        private readonly IAuditLogService _auditLogService;
        private readonly ISessionService _sessionService;

        public LoginWith2faModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IEmailSenderService emailSender,
            IAuditLogService auditLogService,
            ISessionService sessionService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _emailSender = emailSender;
            _auditLogService = auditLogService;
            _sessionService = sessionService;
        }

        [BindProperty, Required]
        public string Code { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMachine { get; set; } = false;

        [BindProperty]
        public string ReturnUrl { get; set; } = "/Index";

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RedirectToPage("/Login");

            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/Index" : returnUrl;

            var token = await _userManager.GenerateTwoFactorTokenAsync(user, TwoFactorProvider);

            var subject = "Fresh Farm Market - 2FA Code";
            var body = $@"
                <p>Your login verification code is:</p>
                <h2 style=""letter-spacing:2px;"">{token}</h2>
                <p>If you did not attempt to log in, please change your password.</p>
            ";

            await _emailSender.SendEmailAsync(user.Email!, subject, body);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null) return RedirectToPage("/Login");

            var code = (Code ?? "").Replace(" ", "").Replace("-", "");

            var result = await _signInManager.TwoFactorSignInAsync(
                TwoFactorProvider,
                code,
                isPersistent: false,
                rememberClient: RememberMachine);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var ua = HttpContext.Request.Headers["User-Agent"].ToString();

            if (result.Succeeded)
            {
                var newSessionId = await _sessionService.CreateSingleSessionAsync(user.Id);
                HttpContext.Session.SetString("AppSessionId", newSessionId);

                await _auditLogService.LogLoginAttemptAsync(user.Email ?? "", success: true, ip, ua, user.Id, additionalInfo: "2FA Success");

                return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/Index" : ReturnUrl);
            }

            await _auditLogService.LogLoginAttemptAsync(user.Email ?? "", success: false, ip, ua, user.Id, additionalInfo: "2FA Failed");

            ModelState.AddModelError(string.Empty, "Invalid verification code.");
            return Page();
        }
    }
}
