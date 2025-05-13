using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Nutri_Plan.Data;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages
{
    [Authorize]
    public class HomeModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public HomeModel(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public string UserFullName { get; set; }
        public bool HasDiet { get; set; }
        public DateTime CurrentDietDate { get; set; }
        public int CurrentDietCalories { get; set; }
        public double CurrentDietProtein { get; set; }
        public double CurrentDietCarbs { get; set; }
        public double CurrentDietFat { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Impossibile caricare i dati dell'utente.");
            }

            UserFullName = $"{user.Nome} {user.Cognome}";

            // Controlla se l'utente ha già una dieta creata
            var latestDiet = await _context.Diets
                .Where(d => d.UserId == user.Id)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestDiet != null)
            {
                HasDiet = true;
                CurrentDietDate = latestDiet.CreatedAt; // Cambiato da CreationDate a CreatedAt
                CurrentDietCalories = latestDiet.TotalCalories; // Cambiato da CalorieGiornaliere a TotalCalories
                CurrentDietProtein = latestDiet.TotalProtein; // Cambiato da Proteine a TotalProtein
                CurrentDietCarbs = latestDiet.TotalCarbs; // Cambiato da Carboidrati a TotalCarbs
                CurrentDietFat = latestDiet.TotalFat; // Cambiato da Grassi a TotalFat
            }
            else
            {
                HasDiet = false;
            }

            // Verifica se TempData contiene un messaggio di successo dalla pagina di modifica profilo
            if (TempData != null && TempData.ContainsKey("SuccessMessage"))
            {
                // Il messaggio verrà mostrato automaticamente nella Home.cshtml tramite il codice che abbiamo aggiunto prima
            }

            return Page();
        }
    }
}