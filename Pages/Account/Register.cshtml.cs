using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<RegisterModel> logger)
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
            [Required(ErrorMessage = "Il nome è obbligatorio")]
            [StringLength(50, ErrorMessage = "Il {0} deve essere di almeno {2} e al massimo {1} caratteri.", MinimumLength = 2)]
            public string Nome { get; set; }

            [Required(ErrorMessage = "Il cognome è obbligatorio")]
            [StringLength(50, ErrorMessage = "Il {0} deve essere di almeno {2} e al massimo {1} caratteri.", MinimumLength = 2)]
            public string Cognome { get; set; }

            [Required(ErrorMessage = "L'email è obbligatoria")]
            [EmailAddress(ErrorMessage = "Formato email non valido")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "La password è obbligatoria")]
            [StringLength(100, ErrorMessage = "La {0} deve essere di almeno {2} e al massimo {1} caratteri.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Conferma password")]
            [Compare("Password", ErrorMessage = "La password e la conferma password non corrispondono.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            // Pulisci il ModelState al caricamento della pagina
            ModelState.Clear();

            ReturnUrl = returnUrl ?? Url.Content("~/Home");

            // Mostra eventuali errori precedenti
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
                ShowErrors = true;
            }
        }

        public async Task<IActionResult> OnPostAsync([FromQuery] string returnUrl = null)
        {
            // FIX: Rimuovi errori ModelState relativi a returnUrl
            ModelState.Remove("returnUrl");

            // Imposta il flag per mostrare errori nel caso di fallimento
            ShowErrors = true;

            returnUrl ??= Url.Content("~/Home");

            if (ModelState.IsValid)
            {
                // Verifica se l'email è già in uso
                var existingUser = await _userManager.FindByEmailAsync(Input.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "Email già registrata. Prova con un'altra email o accedi.");
                    return Page();
                }

                // Crea l'utente
                var user = new User
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Nome = Input.Nome,
                    Cognome = Input.Cognome,
                    EmailConfirmed = true // Conferma l'email automaticamente per semplificare il processo
                };

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"Utente {Input.Email} creato con successo.");

                    // Imposta un messaggio di successo da mostrare nella pagina di login
                    SuccessMessage = $"Registrazione completata con successo! Ora puoi accedere con le tue credenziali.";

                    // Reindirizza alla pagina di login invece che alla home
                    return RedirectToPage("./Login");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        // Traduciamo alcuni messaggi di errore comuni
                        if (error.Code == "PasswordRequiresDigit")
                        {
                            ModelState.AddModelError(string.Empty, "La password deve contenere almeno un numero.");
                        }
                        else if (error.Code == "PasswordRequiresUpper")
                        {
                            ModelState.AddModelError(string.Empty, "La password deve contenere almeno una lettera maiuscola.");
                        }
                        else if (error.Code == "PasswordRequiresLower")
                        {
                            ModelState.AddModelError(string.Empty, "La password deve contenere almeno una lettera minuscola.");
                        }
                        else if (error.Code == "PasswordTooShort")
                        {
                            ModelState.AddModelError(string.Empty, "La password deve essere di almeno 6 caratteri.");
                        }
                        else
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }
                }
            }

            // Se arriviamo qui, qualcosa è fallito, rivisualizza il form
            return Page();
        }
    }
}