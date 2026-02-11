using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace _242034Y_FreshFarmMarket.Pages
{
    [Authorize]
    public class Enable2faModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLogService;

        public Enable2faModel(UserManager<ApplicationUser> userManager, IAuditLogService auditLogService)
        {
            _userManager = userManager;
            _auditLogService = auditLogService;
        }

        [BindProperty]
        public bool Enable { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                Enable = false;
                return;
            }

            Enable = await _userManager.GetTwoFactorEnabledAsync(user);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            var result = await _userManager.SetTwoFactorEnabledAsync(user, Enable);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                Enable = await _userManager.GetTwoFactorEnabledAsync(user);
                return Page();
            }

            user.TwoFactorMethod = Enable ? "EmailOTP" : null;
            await _userManager.UpdateAsync(user);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var ua = HttpContext.Request.Headers["User-Agent"].ToString();

            await _auditLogService.LogSecurityEventAsync(
                user.Id,
                user.Email ?? "",
                "2FA",
                Enable ? "2FA enabled (Email OTP)" : "2FA disabled",
                ip,
                ua);

            TempData["Success"] = Enable ? "2FA enabled." : "2FA disabled.";
            return RedirectToPage("/Enable2fa");
        }
    }
}
