using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Account
{
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(SignInManager<User> signInManager, ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        public void OnGet()
        {
            // Mostra solo la pagina di logout
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Tentativo di logout");

            await _signInManager.SignOutAsync();

            // Per sicurezza, eseguiamo logout anche con autenticazione cookie standard
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            _logger.LogInformation("Utente disconnesso con successo");

            return RedirectToPage("/Account/Login");
        }
    }
}
