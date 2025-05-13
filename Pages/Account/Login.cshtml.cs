using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<LoginModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        // Flag per indicare se mostrare gli errori
        [TempData]
        public bool ShowErrors { get; set; } = false;

        public class InputModel
        {
            [Required(ErrorMessage = "L'email è obbligatoria")]
            [EmailAddress(ErrorMessage = "Formato email non valido")]
            public string Email { get; set; }

            [Required(ErrorMessage = "La password è obbligatoria")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Ricordami")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            // Pulisci il ModelState al caricamento della pagina
            ModelState.Clear();

            // Ma aggiungi eventuali messaggi di errore specifici
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
                ShowErrors = true;
            }

            // Pulisci il cookie esterno esistente
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ReturnUrl = returnUrl ?? Url.Content("~/Home");
        }

        public async Task<IActionResult> OnPostAsync([FromQuery] string returnUrl = null)
        {
            ModelState.Remove("returnUrl");
            ShowErrors = true;

            // Se returnUrl è vuoto, impostiamo un valore predefinito che sostituiremo in base al ruolo
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Email o password non validi.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Utente {Input.Email} ha effettuato l'accesso con successo.");

                    // Aggiorna la data dell'ultimo accesso
                    user.LastLoginDate = DateTime.Now;
                    await _userManager.UpdateAsync(user);

                    // Reindirizza gli amministratori alla dashboard, gli altri utenti alla Home
                    if (user.IsAdmin || await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return LocalRedirect("/Admin/Dashboard");
                    }

                    // Utenti normali vanno alla Home
                    return LocalRedirect("/Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Email o password non validi.");
                    return Page();
                }
            }

            return Page();
        }
    }
}