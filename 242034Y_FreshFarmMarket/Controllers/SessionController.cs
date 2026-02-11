using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace _242034Y_FreshFarmMarket.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SessionController : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SessionController> _logger;

        public SessionController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<SessionController> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        [HttpPost("extend")]
        public IActionResult ExtendSession()
        {
            try
            {
                // Reset the session timeout
                _httpContextAccessor.HttpContext?.Session.SetString("LastActivity", DateTime.Now.ToString());

                // You can also update the cookie expiration if using cookie authentication
                var options = new CookieOptions
                {
                    Expires = DateTime.Now.AddMinutes(30),
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict
                };

                Response.Cookies.Append(".AspNetCore.Identity.Application",
                    Request.Cookies[".AspNetCore.Identity.Application"] ?? string.Empty,
                    options);

                return Ok(new { success = true, message = "Session extended" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending session");
                return StatusCode(500, new { success = false, message = "Failed to extend session" });
            }
        }

        [HttpGet("status")]
        public IActionResult GetSessionStatus()
        {
            var sessionId = HttpContext.Session.Id;
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var userName = User.Identity?.Name;
            var lastActivity = HttpContext.Session.GetString("LastActivity");

            return Ok(new
            {
                sessionId,
                isAuthenticated,
                userName,
                lastActivity,
                serverTime = DateTime.Now
            });
        }
    }
}