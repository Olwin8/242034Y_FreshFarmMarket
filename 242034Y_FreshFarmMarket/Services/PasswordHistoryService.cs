using _242034Y_FreshFarmMarket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IPasswordHistoryService
    {
        Task<bool> IsPasswordReusedAsync(ApplicationUser user, string newPassword);
        Task AddPasswordToHistoryAsync(string userId, string oldPasswordHash);
    }

    public class PasswordHistoryService : IPasswordHistoryService
    {
        private const int MaxHistory = 2;

        private readonly AuthDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
        private readonly ILogger<PasswordHistoryService> _logger;

        public PasswordHistoryService(
            AuthDbContext context,
            IPasswordHasher<ApplicationUser> passwordHasher,
            ILogger<PasswordHistoryService> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<bool> IsPasswordReusedAsync(ApplicationUser user, string newPassword)
        {
            try
            {
                // 1) Compare with current password hash
                if (!string.IsNullOrEmpty(user.PasswordHash))
                {
                    var currentMatch = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);
                    if (currentMatch == PasswordVerificationResult.Success ||
                        currentMatch == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        return true;
                    }
                }

                // 2) Compare with last 2 password histories
                var lastTwo = await _context.PasswordHistories
                    .Where(p => p.UserId == user.Id)
                    .OrderByDescending(p => p.ChangedAt)
                    .Take(MaxHistory)
                    .Select(p => p.PasswordHash)
                    .ToListAsync();

                foreach (var oldHash in lastTwo)
                {
                    var match = _passwordHasher.VerifyHashedPassword(user, oldHash, newPassword);
                    if (match == PasswordVerificationResult.Success ||
                        match == PasswordVerificationResult.SuccessRehashNeeded)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking password reuse for user {UserId}", user.Id);

                // Fail-safe: block password change if check fails
                return true;
            }
        }

        public async Task AddPasswordToHistoryAsync(string userId, string oldPasswordHash)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(oldPasswordHash))
                    return;

                _context.PasswordHistories.Add(new PasswordHistory
                {
                    UserId = userId,
                    PasswordHash = oldPasswordHash,
                    ChangedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                // Keep only latest 2 records per user
                var toDelete = await _context.PasswordHistories
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.ChangedAt)
                    .Skip(MaxHistory)
                    .ToListAsync();

                if (toDelete.Count > 0)
                {
                    _context.PasswordHistories.RemoveRange(toDelete);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding password history for user {UserId}", userId);
            }
        }
    }
}
