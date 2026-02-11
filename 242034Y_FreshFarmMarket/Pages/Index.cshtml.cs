using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace _242034Y_FreshFarmMarket.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ISessionService _sessionService;
        private readonly IAuditLogService _auditLogService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<IndexModel> _logger;

        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserGender { get; set; } = "Not specified";
        public string UserMobileNo { get; set; } = string.Empty;
        public string UserAddress { get; set; } = string.Empty;
        public string UserAboutMe { get; set; } = string.Empty;
        public string UserInitial { get; set; } = string.Empty;
        public string LastLoginTime { get; set; } = "Recently";
        public string MemberSince { get; set; } = string.Empty;
        public int ProfileCompletion { get; set; } = 0;
        public int SecurityScore { get; set; } = 85;
        public int LoginCount { get; set; } = 0;
        public int ActiveSessions { get; set; } = 1;
        public int FailedLoginAttempts { get; set; } = 0;
        public bool IsLockedOut { get; set; } = false;
        public DateTime? LockoutEndTime { get; set; }
        public string SessionId { get; set; } = string.Empty;

        public string? UserPhotoPath { get; set; }
        public string? MaskedCreditCard { get; set; }

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            ISessionService sessionService,
            IAuditLogService auditLogService,
            IEncryptionService encryptionService,
            ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _sessionService = sessionService;
            _auditLogService = auditLogService;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.Now;
            LockoutEndTime = user.LockoutEnd;

            if (user.LastLoginDate == null || (DateTime.Now - user.LastLoginDate.Value).TotalMinutes > 1)
            {
                user.LastLoginDate = DateTime.Now;
                await _userManager.UpdateAsync(user);
            }

            UserFullName = user.FullName;
            UserEmail = user.Email ?? string.Empty;
            UserGender = user.Gender ?? "Not specified";

            // ✅ FULL mobile number display (no masking)
            // If you store only 8 digits, UI will show 8 digits. (No partial.)
            UserMobileNo = user.MobileNo ?? string.Empty;

            UserAddress = user.DeliveryAddress;

            // ✅ DB stores encoded, UI shows original text
            UserAboutMe = string.IsNullOrEmpty(user.AboutMe)
                ? string.Empty
                : WebUtility.HtmlDecode(user.AboutMe);

            UserInitial = UserFullName.Length > 0 ? UserFullName[0].ToString().ToUpper() : "U";
            UserPhotoPath = user.PhotoPath;

            // Mask credit card (decrypt + last 4 only)
            MaskedCreditCard = null;
            if (!string.IsNullOrEmpty(user.CreditCardNoEncrypted))
            {
                try
                {
                    var ccPlain = _encryptionService.Decrypt(user.CreditCardNoEncrypted);
                    if (!string.IsNullOrWhiteSpace(ccPlain) && ccPlain.Length >= 4)
                    {
                        var last4 = ccPlain.Substring(ccPlain.Length - 4);
                        MaskedCreditCard = $"**** **** **** {last4}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to decrypt/mask credit card for dashboard display.");
                }
            }

            MemberSince = user.CreatedDate?.ToString("MMMM yyyy") ?? "Recently";

            var lastLogin = await _auditLogService.GetLastLoginAsync(user.Id);
            if (lastLogin.HasValue)
            {
                LastLoginTime = lastLogin.Value.ToString("dd MMM, HH:mm");
            }
            else
            {
                LastLoginTime = user.LastLoginDate?.ToString("dd MMM, HH:mm") ?? "Recently";
            }

            ProfileCompletion = CalculateProfileCompletion(user);

            LoginCount = await _auditLogService.GetUserLoginCountAsync(user.Id, 30);
            ActiveSessions = await _sessionService.GetActiveSessionCountAsync(user.Id);
            FailedLoginAttempts = await _auditLogService.GetFailedLoginCountAsync(user.Id, 30);

            SessionId = HttpContext.Session.Id;

            SecurityScore = CalculateSecurityScore(user, ActiveSessions, FailedLoginAttempts);

            return Page();
        }

        private int CalculateProfileCompletion(ApplicationUser user)
        {
            int score = 0;
            int totalFields = 6;

            if (!string.IsNullOrEmpty(user.FullName?.Trim())) score++;
            if (!string.IsNullOrEmpty(user.Email?.Trim())) score++;
            if (!string.IsNullOrEmpty(user.Gender?.Trim())) score++;
            if (!string.IsNullOrEmpty(user.MobileNo?.Trim())) score++;
            if (!string.IsNullOrEmpty(user.DeliveryAddress?.Trim())) score++;
            if (!string.IsNullOrEmpty(user.AboutMe?.Trim())) score++;

            return (int)((score / (double)totalFields) * 100);
        }

        private int CalculateSecurityScore(ApplicationUser user, int activeSessions, int failedLoginAttempts)
        {
            int score = 100;

            if (failedLoginAttempts > 0)
                score -= Math.Min(failedLoginAttempts * 2, 20);

            if (user.FailedLoginAttempts > 0)
                score -= Math.Min(user.FailedLoginAttempts * 3, 15);

            if (activeSessions > 1)
                score -= (activeSessions - 1) * 10;

            if (user.CreatedDate.HasValue && (DateTime.Now - user.CreatedDate.Value).TotalDays > 90)
                score -= 15;

            if (IsLockedOut)
                score -= 30;

            var profileCompletion = CalculateProfileCompletion(user);
            if (profileCompletion >= 80)
                score += 5;

            if (user.LastPasswordChangeDate.HasValue &&
                (DateTime.Now - user.LastPasswordChangeDate.Value).TotalDays < 30)
                score += 10;

            return Math.Max(Math.Min(score, 100), 0);
        }
    }
}
