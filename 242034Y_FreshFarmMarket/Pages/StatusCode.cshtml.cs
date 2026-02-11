using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace _242034Y_FreshFarmMarket.Pages
{
    public class StatusCodeModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int StatusCode { get; set; }

        public void OnGet(int code)
        {
            StatusCode = code;
        }
    }
}
