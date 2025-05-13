using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;
using Nutri_Plan.Services;

namespace Nutri_Plan.Pages.Diet
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _geminiService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            GeminiService geminiService,
            ILogger<CreateModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _geminiService = geminiService;
            _logger = logger;
        }

        public bool HasUserProfile { get; private set; }
        public UserProfile UserProfile { get; private set; }
        public bool IsGenerating { get; private set; }

        [BindProperty]
        public Models.Diet Diet { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Utente non trovato.");
            }

            // Verifica se l'utente ha un profilo
            UserProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            HasUserProfile = UserProfile != null;

            // Se l'utente non ha un profilo, reindirizza alla pagina di modifica del profilo
            if (!HasUserProfile)
            {
                return RedirectToPage("/Profile/Edit");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostGenerateDietAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound("Utente non trovato.");
                }

                // Ottieni il profilo utente
                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (userProfile == null)
                {
                    return RedirectToPage("/Profile/Edit");
                }

                // Imposta lo stato di generazione
                IsGenerating = true;

                // Log dell'inizio della generazione
                _logger.LogInformation("Inizio generazione piano alimentare per utente {UserId}", user.Id);

                // Genera il piano alimentare
                string dietPlanText = await _geminiService.GenerateDietPlanAsync(userProfile, user);

                // Verifica se c'è stato un errore nella generazione
                if (dietPlanText.Contains("Si è verificato un errore") || dietPlanText.Contains("Non è stato possibile"))
                {
                    ErrorMessage = "Si è verificato un errore durante la generazione del piano alimentare. Riprova più tardi.";
                    _logger.LogWarning("Errore nella generazione del piano alimentare per l'utente {UserId}", user.Id);
                    return RedirectToPage("Create");
                }

                // Analizza il testo per estrarre i valori nutrizionali
                int estimatedCalories = EstimateCaloriesFromText(dietPlanText, userProfile);

                // Crea un nuovo oggetto Diet con ID generato
                var diet = new Models.Diet
                {
                    Id = Guid.NewGuid().ToString(), // Assicurati che sia generato un ID
                    UserId = user.Id,
                    Title = $"Piano alimentare {DateTime.Now.ToString("dd/MM/yyyy")}",
                    Description = $"Piano alimentare personalizzato in base al tuo profilo - {userProfile.ObbiettivoPeso}",
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    TotalCalories = estimatedCalories,
                    TotalProtein = estimatedCalories * 0.25 / 4, // 25% proteine (4 cal per grammo)
                    TotalCarbs = estimatedCalories * 0.5 / 4,    // 50% carb (4 cal per grammo) 
                    TotalFat = estimatedCalories * 0.25 / 9,     // 25% grassi (9 cal per grammo)
                    DietPlanJson = JsonSerializer.Serialize(new { fullText = dietPlanText })
                };

                // Salva la dieta nel database
                _context.Diets.Add(diet);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Piano alimentare generato con successo per l'utente {UserId}, Diet ID: {DietId}",
                    user.Id, diet.Id);

                // Reindirizza alla pagina di visualizzazione con ID esplicito
                return RedirectToPage("/Diet/View", new { id = diet.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la generazione della dieta");
                ErrorMessage = "Si è verificato un errore durante la generazione del piano alimentare. Riprova più tardi.";
                return RedirectToPage("Create");
            }
        }

        private int EstimateCaloriesFromText(string dietPlanText, UserProfile userProfile)
        {
            // Calcolo base del fabbisogno calorico utilizzando formula TDEE
            double bmr = 10 * userProfile.Peso + 6.25 * userProfile.Altezza - 5 * userProfile.Eta + 5;

            double activityFactor = 1.375; // Valore predefinito moderato

            // Mappa il livello di attività con i valori appropriati
            string livelloAttivita = userProfile.LivelloAttivita?.ToLower() ?? "sedentario";

            if (livelloAttivita.Contains("sedentario"))
                activityFactor = 1.2;
            else if (livelloAttivita.Contains("leggero") || livelloAttivita.Contains("moderato"))
                activityFactor = 1.375;
            else if (livelloAttivita.Contains("attivo") && !livelloAttivita.Contains("molto"))
                activityFactor = 1.55;
            else if (livelloAttivita.Contains("molto attivo") || livelloAttivita.Contains("intenso"))
                activityFactor = 1.725;

            double tdee = bmr * activityFactor;

            // Adatta il TDEE in base all'obiettivo di peso
            string obiettivoPeso = userProfile.ObbiettivoPeso?.ToLower() ?? "mantenere";

            if (obiettivoPeso.Contains("dimagrire") || obiettivoPeso.Contains("perdere"))
            {
                tdee -= 500; // Deficit calorico
            }
            else if (obiettivoPeso.Contains("ingrassare") || obiettivoPeso.Contains("aumentare"))
            {
                tdee += 500; // Surplus calorico
            }

            // Arrotonda a un intero
            return Convert.ToInt32(Math.Round(tdee));
        }
    }
}