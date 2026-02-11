using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace _242034Y_FreshFarmMarket.Pages
{
    [Authorize(Roles = "Admin")]
    public class AuditLogModel : PageModel
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogModel(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public List<AuditLog> Logs { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int Days { get; set; } = 30;

        [BindProperty(SupportsGet = true)]
        public string? Email { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Action { get; set; }

        public async Task OnGetAsync()
        {
            if (Days <= 0) Days = 30;
            if (Days > 365) Days = 365;

            Logs = await _auditLogService.GetAllAuditLogsAsync(Days, Email, Action);
        }
    }
}
