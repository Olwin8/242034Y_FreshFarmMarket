using Microsoft.AspNetCore.Identity;
using _242034Y_FreshFarmMarket.Models;

namespace _242034Y_FreshFarmMarket.Services
{
    // ✅ Custom 2FA provider that works as long as the user has an email.
    // This avoids "provider invalid" issues that can happen when EmailConfirmed is false.
    public class EmailOtpTokenProvider<TUser> : TotpSecurityStampBasedTokenProvider<TUser>
        where TUser : ApplicationUser
    {
        public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
        {
            // Only require email to exist (no email confirmation requirement)
            var ok = !string.IsNullOrWhiteSpace(user.Email);
            return Task.FromResult(ok);
        }
    }
}
