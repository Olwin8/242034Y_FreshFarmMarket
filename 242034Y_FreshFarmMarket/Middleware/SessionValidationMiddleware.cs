using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace _242034Y_FreshFarmMarket.Middleware
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SessionValidationMiddleware> _logger;

        public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ISessionService sessionService, UserManager<ApplicationUser> userManager, IAuditLogService auditLogService)
        {
            var path = context.Request.Path.Value?.ToLower();
            var skipPaths = new[] { "/login", "/register", "/logout", "/error", "/testencryption", "/accessdenied", "/api/" };

            if (skipPaths.Any(p => path?.StartsWith(p) == true))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = userManager.GetUserId(context.User);
                var sessionId = context.Request.Cookies["FreshFarmMarket.SessionId"];

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sessionId))
                {
                    // Order: sessionId, userId to match the Service interface
                    var isValid = await sessionService.ValidateSessionAsync(sessionId, userId);

                    if (!isValid)
                    {
                        _logger.LogWarning($"Invalid session for user {userId}. Redirecting to login.");
                        context.Response.Cookies.Delete("FreshFarmMarket.SessionId");
                        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                        context.Session.Clear();

                        var user = await userManager.FindByIdAsync(userId);
                        if (user != null)
                        {
                            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                            var userAgent = context.Request.Headers["User-Agent"].ToString();
                            await auditLogService.LogLoginAttemptAsync(user.Email, false, ipAddress, userAgent, userId, "Session expired");
                        }

                        context.Response.Redirect($"/Login?timeout=true");
                        return;
                    }

                    if (!IsStaticFileRequest(context.Request.Path))
                    {
                        // This method is now defined in ISessionService
                        await sessionService.UpdateLastActivityAsync(sessionId);
                    }
                }
                else
                {
                    _logger.LogWarning($"Missing session data for authenticated user. Redirecting to login.");
                    context.Response.Cookies.Delete("FreshFarmMarket.SessionId");
                    await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                    context.Session.Clear();
                    context.Response.Redirect("/Login?session=missing");
                    return;
                }
            }
            else if (path == "/")
            {
                context.Response.Redirect("/Login");
                return;
            }

            await _next(context);
        }

        private bool IsStaticFileRequest(PathString path)
        {
            var staticFileExtensions = new[] { ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot" };
            return staticFileExtensions.Any(ext => path.Value?.EndsWith(ext, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
}