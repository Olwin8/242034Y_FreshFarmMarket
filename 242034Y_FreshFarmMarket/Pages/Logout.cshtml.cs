using _242034Y_FreshFarmMarket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(bool timeout = false)
        {
            await FullLogoutAsync();

            if (timeout)
            {
                return RedirectToPage("/Login", new { timeout = true });
            }

            return RedirectToPage("/Login");
        }

        // ✅ IMPORTANT: your forms POST to /Logout (no handler), so this MUST be OnPostAsync
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            await FullLogoutAsync();
            return RedirectToPage("/Login");
        }

        private async Task FullLogoutAsync()
        {
            try
            {
                // Helps prevent browser caching “logged in” UI
                Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // ✅ Clear Session (includes your AppSessionId)
                HttpContext.Session.Remove("AppSessionId");
                HttpContext.Session.Clear();

                // ✅ Clear single-session DB flag so middleware doesn't treat old session as valid
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _userManager.FindByIdAsync(userId);
                    if (user != null)
                    {
                        user.CurrentSessionId = null;
                        user.LastSessionActivity = null;
                        await _userManager.UpdateAsync(user);
                    }
                }

                // ✅ Proper Identity sign out (clears auth cookie)
                await _signInManager.SignOutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
            }
        }
    }
}
