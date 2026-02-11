using _242034Y_FreshFarmMarket.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface ISessionService
    {
        Task<int> GetActiveSessionCountAsync(string userId);
        Task<bool> ValidateSessionAsync(string sessionId, string userId);
        Task<string> CreateSessionAsync(string userId);
        Task UpdateLastActivityAsync(string sessionId);

        Task<List<UserSession>> GetUserSessionsAsync(string userId);
        Task<bool> TerminateSessionAsync(string sessionId);
        Task<bool> TerminateAllUserSessionsAsync(string userId);
        Task EndSessionAsync(string userId, string sessionId);

        // ✅ NEW: single-session enforcement helper
        Task<string> CreateSingleSessionAsync(string userId);
    }

    public class SessionService : ISessionService
    {
        private readonly AuthDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SessionService> _logger;

        // ✅ Requirement: 2 minutes inactivity
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(2);

        public SessionService(
            AuthDbContext context,
            IMemoryCache cache,
            ILogger<SessionService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<UserSession>> GetUserSessionsAsync(string userId)
        {
            return await _context.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .OrderByDescending(s => s.LastActivity)
                .ToListAsync();
        }

        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            try
            {
                var session = await _context.UserSessions.FindAsync(sessionId);
                if (session != null)
                {
                    session.IsActive = false;
                    session.TerminatedAt = DateTime.Now;
                    session.TerminationReason = "Manual Termination";

                    var user = await _context.Users.FindAsync(session.UserId);
                    if (user != null && user.CurrentSessionId == sessionId)
                    {
                        user.CurrentSessionId = null;
                    }

                    await _context.SaveChangesAsync();
                    _cache.Remove($"active_sessions_{session.UserId}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> TerminateAllUserSessionsAsync(string userId)
        {
            try
            {
                var activeSessions = await _context.UserSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .ToListAsync();

                foreach (var session in activeSessions)
                {
                    session.IsActive = false;
                    session.TerminatedAt = DateTime.Now;
                    session.TerminationReason = "All Sessions Terminated";
                }

                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    user.CurrentSessionId = null;
                }

                await _context.SaveChangesAsync();
                _cache.Remove($"active_sessions_{userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating all sessions for user {UserId}", userId);
                return false;
            }
        }

        public async Task EndSessionAsync(string userId, string sessionId)
        {
            await TerminateSessionAsync(sessionId);
        }

        public async Task UpdateLastActivityAsync(string sessionId)
        {
            var session = await _context.UserSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.LastActivity = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetActiveSessionCountAsync(string userId)
        {
            var cacheKey = $"active_sessions_{userId}";

            if (!_cache.TryGetValue(cacheKey, out int activeSessions))
            {
                // ✅ FIX: compute cutoff OUTSIDE the LINQ query (EF can translate this)
                var cutoff = DateTime.Now.Subtract(InactivityTimeout);

                activeSessions = await _context.UserSessions
                    .CountAsync(s => s.UserId == userId
                                  && s.IsActive
                                  && s.LastActivity >= cutoff);

                _cache.Set(cacheKey, activeSessions, TimeSpan.FromMinutes(1));
            }

            return activeSessions;
        }

        public async Task<bool> ValidateSessionAsync(string sessionId, string userId)
        {
            var session = await _context.UserSessions.FindAsync(sessionId);

            if (session != null
                && session.IsActive
                && session.UserId == userId
                && session.LastActivity >= DateTime.Now.Subtract(InactivityTimeout))
            {
                session.LastActivity = DateTime.Now;
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        public async Task<string> CreateSessionAsync(string userId)
        {
            var sessionId = Guid.NewGuid().ToString();

            var newSession = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                IsActive = true,
                CreatedAt = DateTime.Now,
                LastActivity = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(InactivityTimeout) // ✅ 2 minutes
            };

            _context.UserSessions.Add(newSession);

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.CurrentSessionId = sessionId;
            }

            await _context.SaveChangesAsync();
            return sessionId;
        }

        // ✅ NEW: Enforce 1 session only (terminate all, then create)
        public async Task<string> CreateSingleSessionAsync(string userId)
        {
            await TerminateAllUserSessionsAsync(userId);
            return await CreateSessionAsync(userId);
        }
    }
}
