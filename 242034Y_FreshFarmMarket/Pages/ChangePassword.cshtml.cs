using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using _242034Y_FreshFarmMarket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace _242034Y_FreshFarmMarket.Pages
{
    [Authorize]
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IPasswordStrengthService _passwordStrengthService;
        private readonly IPasswordHistoryService _passwordHistoryService;
        private readonly IAuditLogService _auditLogService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChangePasswordModel> _logger;

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IPasswordStrengthService passwordStrengthService,
            IPasswordHistoryService passwordHistoryService,
            IAuditLogService auditLogService,
            IConfiguration configuration,
            ILogger<ChangePasswordModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _passwordStrengthService = passwordStrengthService;
            _passwordHistoryService = passwordHistoryService;
            _auditLogService = auditLogService;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public ChangePasswordViewModel CModel { get; set; } = new();

        public List<string> PasswordSuggestions { get; set; } = new();

        public bool ShouldRedirectToHome { get; set; } = false;
        public int RedirectDelaySeconds { get; set; } = 2;

        public void OnGet()
        {
            PasswordSuggestions = _passwordStrengthService
                .CheckPasswordStrength(string.Empty)
                .Suggestions;

            if (TempData.Peek("Success") != null)
            {
                ShouldRedirectToHome = true;
                RedirectDelaySeconds = 2;
            }

            if (Request.Query.ContainsKey("expired"))
            {
                ModelState.AddModelError(string.Empty, "Your password has expired. Please change your password.");
            }
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                PasswordSuggestions = _passwordStrengthService
                    .CheckPasswordStrength(CModel.NewPassword ?? string.Empty)
                    .Suggestions;

                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToPage("/Login");

            // ✅ MIN password age enforcement
            var minMinutes = _configuration.GetValue<int>("PasswordAgePolicy:MinAgeMinutes", 1);
            if (user.LastPasswordChangeDate.HasValue)
            {
                var sinceLast = DateTime.Now - user.LastPasswordChangeDate.Value;
                if (sinceLast.TotalMinutes < minMinutes)
                {
                    ModelState.AddModelError(string.Empty, $"You can only change your password after {minMinutes} minute(s) from the last change.");
                    PasswordSuggestions = _passwordStrengthService.CheckPasswordStrength(CModel.NewPassword ?? string.Empty).Suggestions;
                    return Page();
                }
            }

            var strengthResult = _passwordStrengthService.CheckPasswordStrength(CModel.NewPassword);

            if (!strengthResult.IsValid)
            {
                ModelState.AddModelError(string.Empty, $"Password is not strong enough ({strengthResult.Strength}).");

                if (strengthResult.Suggestions != null)
                {
                    foreach (var s in strengthResult.Suggestions)
                    {
                        if (!string.IsNullOrWhiteSpace(s) && s.TrimStart().StartsWith("✗"))
                            ModelState.AddModelError(string.Empty, s);
                    }
                }

                PasswordSuggestions = strengthResult.Suggestions ?? new List<string>();
                return Page();
            }

            var reused = await _passwordHistoryService.IsPasswordReusedAsync(user, CModel.NewPassword);
            if (reused)
            {
                ModelState.AddModelError(string.Empty, "You cannot reuse your current password or your last 2 previous passwords.");
                PasswordSuggestions = strengthResult.Suggestions ?? new List<string>();
                return Page();
            }

            var oldHash = user.PasswordHash ?? string.Empty;

            var result = await _userManager.ChangePasswordAsync(user, CModel.CurrentPassword, CModel.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                PasswordSuggestions = strengthResult.Suggestions ?? new List<string>();
                return Page();
            }

            await _passwordHistoryService.AddPasswordToHistoryAsync(user.Id, oldHash);

            // ✅ Update password change date
            user.LastPasswordChangeDate = DateTime.Now;
            await _userManager.UpdateAsync(user);

            await _signInManager.RefreshSignInAsync(user);

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await _auditLogService.LogSecurityEventAsync(
                user.Id,
                user.Email ?? "",
                "ChangePassword",
                "User changed password successfully",
                ipAddress,
                userAgent);

            TempData["Success"] = "Password updated successfully.";
            return RedirectToPage("/ChangePassword");
        }
    }
}
