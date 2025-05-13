using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;
using Nutri_Plan.Services;

namespace Nutri_Plan.Pages
{
    [Authorize]
    public class FoodsModel : PageModel
    {
        private readonly FoodNutritionService _foodService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FoodsModel> _logger;

        public FoodsModel(
            FoodNutritionService foodService,
            ApplicationDbContext context,
            ILogger<FoodsModel> logger)
        {
            _foodService = foodService;
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public string FoodQuery { get; set; }

        public Food SearchedFood { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasSearched { get; set; }
        public bool IsSearching { get; set; }

        // Aggiungiamo una lista di alimenti popolari precalcolati
        public List<Food> PopularFoods { get; set; }

        public async Task OnGetAsync()
        {
            HasSearched = false;
            IsSearching = false;

            // Carica alimenti popolari dal database (solo primi 8)
            PopularFoods = await _context.Foods
                .OrderBy(f => f.Nome)
                .Take(8)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(FoodQuery))
            {
                ModelState.AddModelError("FoodQuery", "Inserisci il nome di un alimento da cercare.");
                HasSearched = false;
                return Page();
            }

            try
            {
                HasSearched = true;
                IsSearching = true;

                // Controlla prima se l'alimento esiste già nel database
                var existingFood = await _context.Foods
                    .FirstOrDefaultAsync(f => EF.Functions.Like(f.Nome, $"%{FoodQuery}%"));

                if (existingFood != null)
                {
                    // Usa l'alimento dal database
                    SearchedFood = existingFood;
                    IsSearching = false;
                }
                else
                {
                    // Chiama il servizio per ottenere informazioni nutrizionali
                    var result = await _foodService.GetFoodNutritionInfoAsync(FoodQuery);

                    if (result.Food != null)
                    {
                        SearchedFood = result.Food;

                        // Salva il nuovo alimento nel database
                        _context.Foods.Add(SearchedFood);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        ErrorMessage = result.ErrorMessage;
                    }
                }

                // Carica alimenti popolari per i suggerimenti
                PopularFoods = await _context.Foods
                    .OrderBy(f => f.Nome)
                    .Take(8)
                    .ToListAsync();

                IsSearching = false;
                return Page();
            }
            catch (Exception ex)
            {
                IsSearching = false;
                ErrorMessage = "Si è verificato un errore durante la ricerca: " + ex.Message;
                _logger.LogError(ex, "Errore nella ricerca dell'alimento {FoodQuery}", FoodQuery);

                // Carica alimenti popolari per i suggerimenti
                PopularFoods = await _context.Foods
                    .OrderBy(f => f.Nome)
                    .Take(8)
                    .ToListAsync();

                return Page();
            }
        }
    }
}