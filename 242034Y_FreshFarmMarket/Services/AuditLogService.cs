using _242034Y_FreshFarmMarket.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IAuditLogService
    {
        Task LogLoginAttemptAsync(string email, bool success, string ipAddress, string userAgent, string? userId = null, string? additionalInfo = null);
        Task LogLogoutAsync(string userId, string email, string ipAddress, string userAgent);
        Task LogRegistrationAsync(string userId, string email, string ipAddress, string userAgent);
        Task LogProfileUpdateAsync(string userId, string email, string ipAddress, string userAgent, string changes);
        Task<List<AuditLog>> GetUserAuditLogsAsync(string userId, int days = 30);
        Task<DateTime?> GetLastLoginAsync(string userId);
        Task<int> GetUserLoginCountAsync(string userId, int days = 30);
        Task<int> GetFailedLoginCountAsync(string userId, int days = 30);
        Task LogSecurityEventAsync(string userId, string email, string action, string description, string ipAddress, string userAgent);

        // ✅ NEW: Admin view all logs
        Task<List<AuditLog>> GetAllAuditLogsAsync(int days = 30, string? email = null, string? action = null);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly AuthDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            AuthDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogLoginAttemptAsync(string email, bool success, string ipAddress, string userAgent, string? userId = null, string? additionalInfo = null)
        {
            try
            {
                var log = new AuditLog
                {
                    // ✅ FIX: null instead of empty string (prevents FK violation)
                    UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                    Email = email,
                    Action = success ? "LoginSuccess" : "LoginFailed",
                    Description = success ? "Successful login" : "Failed login attempt" + (additionalInfo != null ? $": {additionalInfo}" : ""),
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.Now,
                    Success = success,
                    AdditionalInfo = additionalInfo
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging login attempt");
            }
        }

        public async Task LogLogoutAsync(string userId, string email, string ipAddress, string userAgent)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = "Logout",
                    Description = "User logged out",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.Now,
                    Success = true
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging logout");
            }
        }

        public async Task LogRegistrationAsync(string userId, string email, string ipAddress, string userAgent)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = "Registration",
                    Description = "New user registration",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.Now,
                    Success = true
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging registration");
            }
        }

        public async Task LogProfileUpdateAsync(string userId, string email, string ipAddress, string userAgent, string changes)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = "ProfileUpdate",
                    Description = $"Profile updated: {changes}",
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.Now,
                    Success = true
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging profile update");
            }
        }

        public async Task LogSecurityEventAsync(string userId, string email, string action, string description, string ipAddress, string userAgent)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId,
                    Email = email,
                    Action = action,
                    Description = description,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Timestamp = DateTime.Now,
                    Success = true,
                    AdditionalInfo = "Security Event"
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging security event");
            }
        }

        public async Task<List<AuditLog>> GetUserAuditLogsAsync(string userId, int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);
                return await _context.AuditLogs
                    .Where(l => l.UserId == userId && l.Timestamp >= cutoffDate)
                    .OrderByDescending(l => l.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs");
                return new List<AuditLog>();
            }
        }

        public async Task<DateTime?> GetLastLoginAsync(string userId)
        {
            try
            {
                var lastLogin = await _context.AuditLogs
                    .Where(l => l.UserId == userId &&
                                l.Action == "LoginSuccess" &&
                                l.Success == true)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => l.Timestamp)
                    .FirstOrDefaultAsync();

                return lastLogin;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last login time");
                return null;
            }
        }

        public async Task<int> GetUserLoginCountAsync(string userId, int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);
                return await _context.AuditLogs
                    .CountAsync(l => l.UserId == userId &&
                                   l.Action == "LoginSuccess" &&
                                   l.Success == true &&
                                   l.Timestamp >= cutoffDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting login count");
                return 0;
            }
        }

        public async Task<int> GetFailedLoginCountAsync(string userId, int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);

                var userEmail = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userEmail))
                {
                    return await _context.AuditLogs
                        .CountAsync(l => l.UserId == userId &&
                                       l.Action == "LoginFailed" &&
                                       l.Timestamp >= cutoffDate);
                }
                else
                {
                    return await _context.AuditLogs
                        .CountAsync(l => (l.UserId == userId || l.Email == userEmail) &&
                                       l.Action == "LoginFailed" &&
                                       l.Timestamp >= cutoffDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting failed login count");
                return 0;
            }
        }

        // ✅ NEW: Admin view all logs
        public async Task<List<AuditLog>> GetAllAuditLogsAsync(int days = 30, string? email = null, string? action = null)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);

                var q = _context.AuditLogs.AsQueryable();
                q = q.Where(l => l.Timestamp >= cutoffDate);

                if (!string.IsNullOrWhiteSpace(email))
                {
                    var e = email.Trim();
                    q = q.Where(l => l.Email.Contains(e));
                }

                if (!string.IsNullOrWhiteSpace(action))
                {
                    var a = action.Trim();
                    q = q.Where(l => l.Action.Contains(a));
                }

                return await q
                    .OrderByDescending(l => l.Timestamp)
                    .Take(1000) // safety
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all audit logs");
                return new List<AuditLog>();
            }
        }
    }
}
