using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using _242034Y_FreshFarmMarket.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class LoginModel : PageModel
    {
        private const int MaxAttempts = 3;

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLogService;
        private readonly IRecaptchaService _recaptchaService;
        private readonly ISessionService _sessionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLogService,
            IRecaptchaService recaptchaService,
            ISessionService sessionService,
            IConfiguration configuration,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditLogService = auditLogService;
            _recaptchaService = recaptchaService;
            _sessionService = sessionService;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public LoginViewModel LModel { get; set; } = new();

        [BindProperty]
        public string RecaptchaToken { get; set; } = string.Empty;

        public string RecaptchaSiteKey { get; set; } = string.Empty;

        public int FailedAttemptsUsed { get; set; } = 0;
        public int AttemptsLeft { get; set; } = MaxAttempts;
        public bool IsLockedOut { get; set; } = false;
        public int LockoutSecondsRemaining { get; set; } = 0;

        public void OnGet()
        {
            RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"] ?? string.Empty;

            FailedAttemptsUsed = 0;
            AttemptsLeft = MaxAttempts;
            IsLockedOut = false;
            LockoutSecondsRemaining = 0;

            if (Request.Query.ContainsKey("forced"))
                ModelState.AddModelError(string.Empty, "You were logged out because your account was signed in elsewhere.");

            if (Request.Query.ContainsKey("timeout"))
                ModelState.AddModelError(string.Empty, "Your session has expired due to inactivity. Please login again.");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"] ?? string.Empty;

            if (!ModelState.IsValid)
                return Page();

            var email = (LModel.Email ?? string.Empty).Trim();
            var password = LModel.Password ?? string.Empty;

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var (ok, score, err) = await _recaptchaService.VerifyAsync(
                RecaptchaToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                expectedAction: "login");

            if (!ok)
            {
                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: null, additionalInfo: "reCAPTCHA failed");
                ModelState.AddModelError(string.Empty, "Anti-bot verification failed. Please try again.");
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: null, additionalInfo: "UserNotFound");
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                IsLockedOut = true;

                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                if (lockoutEnd.HasValue)
                {
                    var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
                    LockoutSecondsRemaining = (int)Math.Max(0, remaining.TotalSeconds);
                }

                var failed = await _userManager.GetAccessFailedCountAsync(user);
                FailedAttemptsUsed = Math.Min(failed, MaxAttempts);
                AttemptsLeft = Math.Max(0, MaxAttempts - FailedAttemptsUsed);

                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: user.Id, additionalInfo: "LockedOut");
                ModelState.AddModelError(string.Empty, "Account is locked. Please try again later.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                password,
                isPersistent: false,
                lockoutOnFailure: true);

            _logger.LogWarning(
                "Login result: Succeeded={Succeeded}, RequiresTwoFactor={RequiresTwoFactor}, IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}",
                result.Succeeded, result.RequiresTwoFactor, result.IsLockedOut, result.IsNotAllowed);

            if (result.RequiresTwoFactor)
            {
                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: user.Id, additionalInfo: "2FA Required");
                return RedirectToPage("/LoginWith2fa", new { returnUrl = "/Index" });
            }

            if (result.IsNotAllowed)
            {
                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: user.Id, additionalInfo: "NotAllowed");
                ModelState.AddModelError(string.Empty, "Login is not allowed for this account right now.");
                return Page();
            }

            if (result.Succeeded)
            {
                await _userManager.ResetAccessFailedCountAsync(user);

                // ✅ SINGLE SESSION ONLY AFTER FULL LOGIN (non-2FA path)
                var newSessionId = await _sessionService.CreateSingleSessionAsync(user.Id);
                HttpContext.Session.SetString("AppSessionId", newSessionId);

                await _auditLogService.LogLoginAttemptAsync(email, success: true, ipAddress, userAgent, userId: user.Id);

                return RedirectToPage("/Index");
            }

            if (result.IsLockedOut)
            {
                IsLockedOut = true;

                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                if (lockoutEnd.HasValue)
                {
                    var remaining = lockoutEnd.Value - DateTimeOffset.UtcNow;
                    LockoutSecondsRemaining = (int)Math.Max(0, remaining.TotalSeconds);
                }

                var failed = await _userManager.GetAccessFailedCountAsync(user);
                FailedAttemptsUsed = Math.Min(failed, MaxAttempts);
                AttemptsLeft = Math.Max(0, MaxAttempts - FailedAttemptsUsed);

                await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: user.Id, additionalInfo: "LockedOutTriggered");
                ModelState.AddModelError(string.Empty, "Account is locked. Please try again later.");
                return Page();
            }

            var failedCount = await _userManager.GetAccessFailedCountAsync(user);
            FailedAttemptsUsed = Math.Min(failedCount, MaxAttempts);
            AttemptsLeft = Math.Max(0, MaxAttempts - FailedAttemptsUsed);

            await _auditLogService.LogLoginAttemptAsync(email, success: false, ipAddress, userAgent, userId: user.Id, additionalInfo: "WrongPassword");
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}
