using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using _242034Y_FreshFarmMarket.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEncryptionService _encryptionService;
        private readonly IAuditLogService _auditLogService;
        private readonly IRecaptchaService _recaptchaService;
        private readonly IPasswordStrengthService _passwordStrengthService;
        private readonly ISessionService _sessionService; // ✅ NEW (minimal add)
        private readonly IConfiguration _configuration;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEncryptionService encryptionService,
            IAuditLogService auditLogService,
            IRecaptchaService recaptchaService,
            IPasswordStrengthService passwordStrengthService,
            ISessionService sessionService, // ✅ NEW (minimal add)
            IConfiguration configuration,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _encryptionService = encryptionService;
            _auditLogService = auditLogService;
            _recaptchaService = recaptchaService;
            _passwordStrengthService = passwordStrengthService;
            _sessionService = sessionService; // ✅ NEW
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public RegisterViewModel RModel { get; set; } = new();

        public List<string> PasswordSuggestions { get; set; } = new();

        [BindProperty]
        public string RecaptchaToken { get; set; } = string.Empty;

        public string RecaptchaSiteKey { get; set; } = string.Empty;

        public void OnGet()
        {
            RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"] ?? string.Empty;

            PasswordSuggestions = _passwordStrengthService
                .CheckPasswordStrength(string.Empty)
                .Suggestions;
        }

        public IActionResult OnGetCheckPassword(string password)
        {
            var result = _passwordStrengthService.CheckPasswordStrength(password ?? string.Empty);

            return new JsonResult(new
            {
                score = result.Score,
                strength = result.Strength,
                isValid = result.IsValid,
                suggestions = result.Suggestions
            });
        }

        public async Task<IActionResult> OnGetCheckEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return new JsonResult(new { exists = false });
            }

            var existing = await _userManager.FindByEmailAsync(email.Trim());
            return new JsonResult(new { exists = existing != null });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            RecaptchaSiteKey = _configuration["Recaptcha:SiteKey"] ?? string.Empty;

            if (!ModelState.IsValid)
            {
                PasswordSuggestions = _passwordStrengthService
                    .CheckPasswordStrength(RModel.Password ?? string.Empty)
                    .Suggestions;

                return Page();
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var (ok, score, err) = await _recaptchaService.VerifyAsync(
                RecaptchaToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                expectedAction: "register");

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Anti-bot verification failed. Please try again.");
                _logger.LogWarning("reCAPTCHA failed during registration (score {Score}): {Err}", score, err);

                PasswordSuggestions = _passwordStrengthService
                    .CheckPasswordStrength(RModel.Password ?? string.Empty)
                    .Suggestions;

                return Page();
            }

            var existing = await _userManager.FindByEmailAsync(RModel.Email);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "Email is already registered.");

                PasswordSuggestions = _passwordStrengthService
                    .CheckPasswordStrength(RModel.Password ?? string.Empty)
                    .Suggestions;

                return Page();
            }

            var aboutMeEncodedForDb = string.IsNullOrEmpty(RModel.AboutMe)
                ? null
                : WebUtility.HtmlEncode(RModel.AboutMe);

            var user = new ApplicationUser
            {
                UserName = RModel.Email,
                Email = RModel.Email,
                FullName = RModel.FullName,
                Gender = RModel.Gender,
                MobileNo = RModel.MobileNo,
                DeliveryAddress = RModel.DeliveryAddress,
                AboutMe = aboutMeEncodedForDb,
                CreatedDate = DateTime.Now,

                // ✅ NEW: password age policies
                LastPasswordChangeDate = DateTime.Now
            };

            user.CreditCardNoEncrypted = _encryptionService.Encrypt(RModel.CreditCardNo);

            if (RModel.Photo != null && RModel.Photo.Length > 0)
            {
                var ext = Path.GetExtension(RModel.Photo.FileName).ToLower();
                if (ext != ".jpg" && ext != ".jpeg")
                {
                    ModelState.AddModelError(string.Empty, "Photo must be JPG only.");

                    PasswordSuggestions = _passwordStrengthService
                        .CheckPasswordStrength(RModel.Password ?? string.Empty)
                        .Suggestions;

                    return Page();
                }

                if (RModel.Photo.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(string.Empty, "Photo must be 5MB or less.");

                    PasswordSuggestions = _passwordStrengthService
                        .CheckPasswordStrength(RModel.Password ?? string.Empty)
                        .Suggestions;

                    return Page();
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await RModel.Photo.CopyToAsync(stream);
                }

                user.PhotoPath = Path.Combine("uploads", fileName).Replace("\\", "/");
            }

            var result = await _userManager.CreateAsync(user, RModel.Password);

            if (result.Succeeded)
            {
                await _auditLogService.LogRegistrationAsync(
                    user.Id,
                    user.Email ?? RModel.Email,
                    ipAddress,
                    userAgent);

                await _signInManager.SignInAsync(user, isPersistent: false);

                // ✅ FIX: create single-session id + store into ASP.NET Session (same as Login)
                var newSessionId = await _sessionService.CreateSingleSessionAsync(user.Id);
                HttpContext.Session.SetString("AppSessionId", newSessionId);

                return RedirectToPage("/Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            PasswordSuggestions = _passwordStrengthService
                .CheckPasswordStrength(RModel.Password ?? string.Empty)
                .Suggestions;

            return Page();
        }
    }
}
