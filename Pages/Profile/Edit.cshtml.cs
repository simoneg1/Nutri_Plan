using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;
using System.Collections.Generic;

namespace Nutri_Plan.Pages.Profile
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EditModel> _logger;

        public EditModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            ILogger<EditModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public List<SelectListItem> LivelloAttivitaOpzioni { get; set; }
        public List<SelectListItem> ObbiettivoPesoOpzioni { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "L'età è obbligatoria")]
            [Range(16, 100, ErrorMessage = "L'età deve essere tra 16 e 100 anni")]
            [Display(Name = "Età")]
            public int Eta { get; set; }

            [Required(ErrorMessage = "Il peso è obbligatorio")]
            [Range(30, 300, ErrorMessage = "Il peso deve essere tra 30 e 300 kg")]
            [Display(Name = "Peso (kg)")]
            [DisplayFormat(DataFormatString = "{0:F1}")]
            public double Peso { get; set; }

            [Required(ErrorMessage = "L'altezza è obbligatoria")]
            [Range(100, 250, ErrorMessage = "L'altezza deve essere tra 100 e 250 cm")]
            [Display(Name = "Altezza (cm)")]
            public double Altezza { get; set; }

            [Required(ErrorMessage = "Il livello di attività fisica è obbligatorio")]
            [Display(Name = "Livello di attività fisica")]
            public string LivelloAttivita { get; set; }

            [Required(ErrorMessage = "L'obiettivo di peso è obbligatorio")]
            [Display(Name = "Obiettivo di peso")]
            public string ObbiettivoPeso { get; set; }

            [Display(Name = "Cibo preferito (facoltativo)")]
            [StringLength(100, ErrorMessage = "Il cibo preferito non può superare i 100 caratteri")]
            public string CiboPreferito { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Non è stato possibile caricare l'utente con ID '{_userManager.GetUserId(User)}'.");
            }

            InitializeSelectLists();

            // Cerca un profilo esistente per l'utente
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (userProfile != null)
            {
                // Se il profilo esiste, carica i dati nel form
                Input = new InputModel
                {
                    Eta = userProfile.Eta,
                    Peso = userProfile.Peso,
                    Altezza = userProfile.Altezza,
                    LivelloAttivita = userProfile.LivelloAttivita,
                    ObbiettivoPeso = userProfile.ObbiettivoPeso,
                    CiboPreferito = userProfile.CiboPreferito
                };
            }
            else
            {
                // Altrimenti, inizializza con valori predefiniti
                Input = new InputModel
                {
                    Eta = 30,
                    Peso = 70,
                    Altezza = 170,
                    LivelloAttivita = "Moderato",
                    ObbiettivoPeso = "Mantenere",
                    CiboPreferito = ""
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                InitializeSelectLists();
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Non è stato possibile caricare l'utente con ID '{_userManager.GetUserId(User)}'.");
            }

            // Cerca un profilo esistente per l'utente
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (userProfile == null)
            {
                // Se il profilo non esiste, creane uno nuovo
                userProfile = new UserProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = user.Id
                };
                _context.UserProfiles.Add(userProfile);
            }

            // Aggiorna i dati del profilo
            userProfile.Eta = Input.Eta;
            userProfile.Peso = Input.Peso;
            userProfile.Altezza = Input.Altezza;
            userProfile.LivelloAttivita = Input.LivelloAttivita;
            userProfile.ObbiettivoPeso = Input.ObbiettivoPeso;
            userProfile.CiboPreferito = Input.CiboPreferito;

            // Salva i cambiamenti
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Profilo utente aggiornato con successo");
                SuccessMessage = "Il tuo profilo è stato aggiornato con successo!";

                // Reindirizza alla Home - corretto il percorso di reindirizzamento
                return RedirectToPage("/Home");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Errore durante l'aggiornamento del profilo");
                ErrorMessage = "Si è verificato un errore durante il salvataggio del profilo. Riprova più tardi.";
                InitializeSelectLists();
                return Page();
            }
        }

        private void InitializeSelectLists()
        {
            // Opzioni per il livello di attività fisica
            LivelloAttivitaOpzioni = new List<SelectListItem>
            {
                new SelectListItem { Value = "Sedentario", Text = "Sedentario (poco o nessun esercizio)" },
                new SelectListItem { Value = "Moderato", Text = "Moderato (esercizio leggero 1-3 giorni/settimana)" },
                new SelectListItem { Value = "Attivo", Text = "Attivo (esercizio moderato 3-5 giorni/settimana)" },
                new SelectListItem { Value = "Molto Attivo", Text = "Molto Attivo (esercizio intenso 6-7 giorni/settimana)" }
            };

            // Opzioni per l'obiettivo di peso
            ObbiettivoPesoOpzioni = new List<SelectListItem>
            {
                new SelectListItem { Value = "Dimagrire", Text = "Dimagrire" },
                new SelectListItem { Value = "Mantenere", Text = "Mantenere il peso attuale" },
                new SelectListItem { Value = "Ingrassare", Text = "Aumentare di peso" }
            };
        }
    }
}
