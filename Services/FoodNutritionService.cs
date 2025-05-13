using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Models;

namespace Nutri_Plan.Services
{
    public class FoodNutritionService
    {
        private readonly GeminiService _geminiService;
        private readonly ILogger<FoodNutritionService> _logger;

        public FoodNutritionService(GeminiService geminiService, ILogger<FoodNutritionService> logger)
        {
            _geminiService = geminiService;
            _logger = logger;
        }

        public async Task<(Food Food, string ErrorMessage)> GetFoodNutritionInfoAsync(string foodName)
        {
            _logger.LogInformation("Ricerca informazioni nutrizionali per: {FoodName}", foodName);

            try
            {
                // Costruisci il prompt specifico per il recupero di informazioni sugli alimenti
                string prompt = BuildFoodNutritionPrompt(foodName);

                // Utilizza il servizio Gemini per ottenere la risposta
                var response = await _geminiService.GetGeminiResponseAsync(prompt, "gemini-1.5-flash", 1024);

                if (string.IsNullOrEmpty(response))
                {
                    return (null, "Non è stato possibile ottenere informazioni nutrizionali. Riprova più tardi.");
                }

                // Elabora la risposta per creare un oggetto Food
                return ParseFoodFromText(response, foodName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle informazioni nutrizionali per {FoodName}", foodName);
                return (null, "Si è verificato un errore durante la ricerca delle informazioni nutrizionali.");
            }
        }

        private string BuildFoodNutritionPrompt(string foodName)
        {
            return $@"Fornisci informazioni nutrizionali per ""{foodName}"" con i seguenti valori specifici per 100 grammi di alimento:
1. Nome esatto dell'alimento in italiano
2. Calorie(kcal)
3. Proteine(grammi)
4. Carboidrati(grammi)
5. Grassi(grammi)
6. Fibre(grammi)

Rispondi SOLO con i valori numerici precisi nel seguente formato:
Nome: [nome dell'alimento]
Calorie: [valore numerico] kcal
Proteine: [valore numerico] g
Carboidrati: [valore numerico] g
Grassi: [valore numerico] g
Fibre: [valore numerico] g";
        }

        private (Food Food, string ErrorMessage) ParseFoodFromText(string text, string searchedFood)
        {
            try
            {
                var food = new Food();

                // Estrai nome alimento
                var nameMatch = Regex.Match(text, @"Nome:?\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase);
                food.Nome = nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : searchedFood;

                // Estrai calorie
                var calorieMatch = Regex.Match(text, @"Calorie:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (calorieMatch.Success)
                {
                    if (double.TryParse(calorieMatch.Groups[1].Value.Replace(',', '.'), out double calories))
                    {
                        food.Calorie = calories;
                    }
                }

                // Estrai proteine
                var proteinMatch = Regex.Match(text, @"Proteine:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (proteinMatch.Success)
                {
                    if (double.TryParse(proteinMatch.Groups[1].Value.Replace(',', '.'), out double proteins))
                    {
                        food.Proteine = proteins;
                    }
                }

                // Estrai carboidrati
                var carbsMatch = Regex.Match(text, @"Carboidrati:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (carbsMatch.Success)
                {
                    if (double.TryParse(carbsMatch.Groups[1].Value.Replace(',', '.'), out double carbs))
                    {
                        food.Carboidrati = carbs;
                    }
                }

                // Estrai grassi
                var fatMatch = Regex.Match(text, @"Grassi:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (fatMatch.Success)
                {
                    if (double.TryParse(fatMatch.Groups[1].Value.Replace(',', '.'), out double fats))
                    {
                        food.Grassi = fats;
                    }
                }

                // Estrai fibre
                var fiberMatch = Regex.Match(text, @"Fibre:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase);
                if (fiberMatch.Success)
                {
                    if (double.TryParse(fiberMatch.Groups[1].Value.Replace(',', '.'), out double fibers))
                    {
                        food.Fibre = fibers;
                    }
                }

                // Verifica che almeno alcune proprietà siano state popolate
                if (food.Calorie == 0 && food.Proteine == 0 && food.Carboidrati == 0 && food.Grassi == 0)
                {
                    return (null, "Non è stato possibile estrarre i valori nutrizionali dalla risposta.");
                }

                return (food, null);
            }
            catch (Exception ex)
            {
                return (null, "Si è verificato un errore nell'analisi delle informazioni nutrizionali: " + ex.Message);
            }
        }
    }
}
