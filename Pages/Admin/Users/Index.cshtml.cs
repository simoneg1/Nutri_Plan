using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Admin.Users
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public List<User> Users { get; set; }

        public async Task OnGetAsync()
        {
            // Ottieni tutti gli utenti
            Users = await _context.Users
                .OrderByDescending(u => u.CreatedDate)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("ID utente non valido");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("Utente non trovato");
            }

            // Non permettere di eliminare l'admin corrente
            if (User.Identity.Name == user.Email && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ErrorMessage"] = "Non puoi eliminare il tuo account amministratore.";
                return RedirectToPage();
            }

            // Elimina l'utente
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Utente {user.Email} eliminato dall'amministratore {User.Identity.Name}");
                TempData["SuccessMessage"] = $"L'utente {user.Email} è stato eliminato con successo";
            }
            else
            {
                TempData["ErrorMessage"] = $"Errore durante l'eliminazione dell'utente: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToPage();
        }
    }
}