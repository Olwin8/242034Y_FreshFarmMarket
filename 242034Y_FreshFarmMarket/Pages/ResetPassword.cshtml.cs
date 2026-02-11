using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPasswordStrengthService _passwordStrengthService;
        private readonly IPasswordHistoryService _passwordHistoryService;
        private readonly IAuditLogService _auditLogService;
        private readonly IConfiguration _configuration;

        public ResetPasswordModel(
            UserManager<ApplicationUser> userManager,
            IPasswordStrengthService passwordStrengthService,
            IPasswordHistoryService passwordHistoryService,
            IAuditLogService auditLogService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _passwordStrengthService = passwordStrengthService;
            _passwordHistoryService = passwordHistoryService;
            _auditLogService = auditLogService;
            _configuration = configuration;
        }

        [BindProperty, Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [BindProperty, Required]
        public string Token { get; set; } = string.Empty;

        [BindProperty, Required, DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty, Required, DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Confirm password must match the new password.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public IActionResult OnGet(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToPage("/Login");

            Email = email;
            Token = token;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Email.Trim());
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid reset request.");
                return Page();
            }

            // ✅ MIN password age enforcement
            var minMinutes = _configuration.GetValue<int>("PasswordAgePolicy:MinAgeMinutes", 1);
            if (user.LastPasswordChangeDate.HasValue)
            {
                var sinceLast = DateTime.Now - user.LastPasswordChangeDate.Value;
                if (sinceLast.TotalMinutes < minMinutes)
                {
                    ModelState.AddModelError(string.Empty, $"You can only reset your password after {minMinutes} minute(s) from the last change.");
                    return Page();
                }
            }

            var strength = _passwordStrengthService.CheckPasswordStrength(NewPassword);
            if (!strength.IsValid)
            {
                ModelState.AddModelError(string.Empty, $"Password is not strong enough ({strength.Strength}).");
                foreach (var s in strength.Suggestions.Where(x => x.TrimStart().StartsWith("✗")))
                    ModelState.AddModelError(string.Empty, s);

                return Page();
            }

            // ✅ password history enforcement
            var reused = await _passwordHistoryService.IsPasswordReusedAsync(user, NewPassword);
            if (reused)
            {
                ModelState.AddModelError(string.Empty, "You cannot reuse your current password or your last 2 previous passwords.");
                return Page();
            }

            var oldHash = user.PasswordHash ?? string.Empty;

            var result = await _userManager.ResetPasswordAsync(user, Token, NewPassword);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                return Page();
            }

            await _passwordHistoryService.AddPasswordToHistoryAsync(user.Id, oldHash);

            user.LastPasswordChangeDate = DateTime.Now;
            await _userManager.UpdateAsync(user);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var ua = HttpContext.Request.Headers["User-Agent"].ToString();

            await _auditLogService.LogSecurityEventAsync(
                user.Id,
                user.Email ?? "",
                "ResetPassword",
                "Password reset successfully",
                ip,
                ua);

            TempData["Success"] = "Password reset successfully. Please login again.";
            return RedirectToPage("/Login");
        }
    }
}
