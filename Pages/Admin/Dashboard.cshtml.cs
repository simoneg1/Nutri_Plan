using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Nutri_Plan.Data;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DashboardModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardModel(
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public User AdminUser { get; set; }
        public List<User> Users { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Ottieni i dettagli dell'amministratore corrente
            AdminUser = await _userManager.GetUserAsync(User);

            // Ottieni tutti gli utenti (escluso l'admin corrente) con paginazione
            Users = await _context.Users
                .Where(u => u.Id != AdminUser.Id)
                .OrderByDescending(u => u.CreatedDate)
                .Take(10)
                .ToListAsync();

            // Statistiche della dashboard
            TotalUsers = await _context.Users.CountAsync();
            ActiveUsers = await _context.Users.CountAsync(u => u.LastLoginDate > System.DateTime.Now.AddDays(-30));

            return Page();
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
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return BadRequest("Non è possibile eliminare un amministratore");
            }

            // Elimina l'utente
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"L'utente {user.Email} è stato eliminato con successo";
                return RedirectToPage();
            }
            else
            {
                TempData["ErrorMessage"] = $"Errore durante l'eliminazione dell'utente: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                return RedirectToPage();
            }
        }
    }
}