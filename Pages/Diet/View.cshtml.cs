using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;

namespace Nutri_Plan.Pages.Diet
{
    [Authorize]
    public class ViewModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ViewModel> _logger;

        public ViewModel(
            UserManager<User> userManager,
            ApplicationDbContext context,
            ILogger<ViewModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            DayContents = new Dictionary<string, List<MealViewModel>>();
        }

        public Models.Diet Diet { get; set; }
        public string FormattedDietPlan { get; set; }
        public Dictionary<string, List<MealViewModel>> DayContents { get; set; }
        public List<Models.Diet> UserDiets { get; set; }

        [TempData]
        public string SuccessMessage { get; set; }

        // Array standard dei giorni della settimana
        private readonly string[] StandardDays = new[]
        {
            "Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato", "Domenica"
        };

        public async Task<IActionResult> OnGetAsync(string id = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Utente non trovato.");
            }

            // Carica tutti i piani alimentari dell'utente, ordinati per data di creazione (più recenti prima)
            UserDiets = await _context.Diets
                .Where(d => d.UserId == user.Id)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            if (string.IsNullOrEmpty(id))
            {
                // Se l'ID non è fornito, ottieni l'ultima dieta dell'utente
                Diet = UserDiets.FirstOrDefault();

                if (Diet == null)
                {
                    // Nessuna dieta trovata, reindirizza alla pagina di creazione
                    return RedirectToPage("/Diet/Create");
                }
            }
            else
            {
                // Ottieni la dieta specifica
                Diet = await _context.Diets
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (Diet == null)
                {
                    return NotFound("Piano alimentare non trovato.");
                }

                // Verifica che l'utente sia autorizzato a visualizzare questa dieta
                if (Diet.UserId != user.Id && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }
            }

            // Formatta il piano alimentare per la visualizzazione
            try
            {
                if (!string.IsNullOrEmpty(Diet.DietPlanJson))
                {
                    // Deserializza il JSON
                    var dietPlanObj = JsonSerializer.Deserialize<JsonElement>(Diet.DietPlanJson);

                    if (dietPlanObj.TryGetProperty("fullText", out JsonElement fullText))
                    {
                        string dietText = fullText.GetString();

                        // Analizza il testo per estrarre i dati per giorni
                        ParseDietPlanByDays(dietText);

                        // Assicura contenuti per tutti i giorni
                        EnsureAllDaysHaveContent(dietText);

                        // Formatta il piano completo
                        FormattedDietPlan = FormatDietPlanText(dietText);
                    }
                    else
                    {
                        FormattedDietPlan = "<div class='alert alert-warning'>Dettagli del piano non disponibili.</div>";
                    }
                }
                else
                {
                    FormattedDietPlan = "<div class='alert alert-warning'>Dettagli del piano non disponibili.</div>";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la formattazione del piano alimentare");
                FormattedDietPlan = "<div class='alert alert-danger'>Si è verificato un errore durante il caricamento del piano alimentare.</div>";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("ID del piano alimentare non valido.");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("Utente non trovato.");
            }

            var diet = await _context.Diets.FirstOrDefaultAsync(d => d.Id == id);
            if (diet == null)
            {
                return NotFound("Piano alimentare non trovato.");
            }

            // Verifica che l'utente sia il proprietario del piano o un amministratore
            if (diet.UserId != user.Id && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Elimina il piano alimentare
            _context.Diets.Remove(diet);
            await _context.SaveChangesAsync();

            // Imposta messaggio di successo
            TempData["SuccessMessage"] = $"Il piano alimentare \"{diet.Title}\" è stato eliminato con successo.";

            // Reindirizza alla vista principale
            return RedirectToPage("/Diet/View");
        }

        private string FormatDietPlanText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<div class='alert alert-info'>Nessun contenuto disponibile per questo piano alimentare.</div>";
            }

            var fullPlanBuilder = new System.Text.StringBuilder();
            fullPlanBuilder.AppendLine("<div class='complete-diet-plan'>");

            // Aggiunge una intestazione generale
            fullPlanBuilder.AppendLine("<div class='diet-plan-header'>");
            fullPlanBuilder.AppendLine("<h1>Piano Alimentare Settimanale</h1>");
            fullPlanBuilder.AppendLine("<p class='diet-plan-description'>Piano personalizzato basato sui tuoi dati nutrizionali e obiettivi.</p>");
            fullPlanBuilder.AppendLine("</div>");

            // Aggiunge i giorni della settimana
            foreach (var day in StandardDays)
            {
                fullPlanBuilder.AppendLine($"<div class='diet-day-section' id='day-{day.ToLower()}'>");
                fullPlanBuilder.AppendLine($"<div class='day-header'>");
                fullPlanBuilder.AppendLine($"<h2>{day}</h2>");

                if (DayContents.ContainsKey(day) && DayContents[day].Any())
                {
                    // Calcola valori nutrizionali totali giornalieri
                    int totalCalories = DayContents[day].Sum(m => m.Calories);
                    double totalProtein = DayContents[day].Sum(m => m.Protein);
                    double totalCarbs = DayContents[day].Sum(m => m.Carbs);
                    double totalFat = DayContents[day].Sum(m => m.Fat);

                    // Mostra riepilogo nutrizionale giornaliero se disponibile
                    if (totalCalories > 0 || totalProtein > 0 || totalCarbs > 0 || totalFat > 0)
                    {
                        fullPlanBuilder.AppendLine("<div class='day-macros'>");
                        fullPlanBuilder.AppendLine("<div class='macro-pills'>");

                        if (totalCalories > 0)
                            fullPlanBuilder.AppendLine($"<span class='macro-pill calories'><i class='fas fa-fire'></i> {totalCalories} kcal</span>");

                        if (totalProtein > 0)
                            fullPlanBuilder.AppendLine($"<span class='macro-pill protein'><i class='fas fa-drumstick-bite'></i> {Math.Round(totalProtein)} g proteine</span>");

                        if (totalCarbs > 0)
                            fullPlanBuilder.AppendLine($"<span class='macro-pill carbs'><i class='fas fa-bread-slice'></i> {Math.Round(totalCarbs)} g carboidrati</span>");

                        if (totalFat > 0)
                            fullPlanBuilder.AppendLine($"<span class='macro-pill fat'><i class='fas fa-cheese'></i> {Math.Round(totalFat)} g grassi</span>");

                        fullPlanBuilder.AppendLine("</div>");
                        fullPlanBuilder.AppendLine("</div>");
                    }
                }

                fullPlanBuilder.AppendLine("</div>"); // Fine day-header

                fullPlanBuilder.AppendLine("<div class='day-meals'>");

                if (DayContents.ContainsKey(day) && DayContents[day].Any())
                {
                    // Ordina i pasti nell'ordine standard: Colazione, Spuntino mattutino, Pranzo, Spuntino pomeridiano, Cena
                    var sortedMeals = SortMealsByTimeOfDay(DayContents[day]);

                    foreach (var meal in sortedMeals)
                    {
                        fullPlanBuilder.AppendLine("<div class='meal-card'>");
                        fullPlanBuilder.AppendLine("<div class='meal-header'>");
                        fullPlanBuilder.AppendLine($"<h3>{meal.MealType}</h3>");

                        if (meal.Calories > 0)
                        {
                            fullPlanBuilder.AppendLine($"<span class='meal-calories'>{meal.Calories} kcal</span>");
                        }

                        fullPlanBuilder.AppendLine("</div>"); // Fine meal-header

                        fullPlanBuilder.AppendLine("<div class='meal-content'>");
                        fullPlanBuilder.AppendLine("<div class='meal-foods'>");
                        fullPlanBuilder.AppendLine(meal.FormattedFoods);
                        fullPlanBuilder.AppendLine("</div>"); // Fine meal-foods

                        if (meal.HasNutrients)
                        {
                            fullPlanBuilder.AppendLine("<div class='meal-macros'>");

                            if (meal.Protein > 0)
                                fullPlanBuilder.AppendLine($"<div class='macro-item'><i class='fas fa-drumstick-bite text-primary'></i> <span>Proteine: <strong>{meal.Protein} g</strong></span></div>");

                            if (meal.Carbs > 0)
                                fullPlanBuilder.AppendLine($"<div class='macro-item'><i class='fas fa-bread-slice text-warning'></i> <span>Carboidrati: <strong>{meal.Carbs} g</strong></span></div>");

                            if (meal.Fat > 0)
                                fullPlanBuilder.AppendLine($"<div class='macro-item'><i class='fas fa-cheese text-success'></i> <span>Grassi: <strong>{meal.Fat} g</strong></span></div>");

                            fullPlanBuilder.AppendLine("</div>"); // Fine meal-macros
                        }

                        fullPlanBuilder.AppendLine("</div>"); // Fine meal-content
                        fullPlanBuilder.AppendLine("</div>"); // Fine meal-card
                    }

                    // Riepilogo del giorno (totali)
                    if (sortedMeals.Any(m => m.HasNutrients))
                    {
                        int dayCalories = sortedMeals.Sum(m => m.Calories);
                        double dayProtein = sortedMeals.Sum(m => m.Protein);
                        double dayCarbs = sortedMeals.Sum(m => m.Carbs);
                        double dayFat = sortedMeals.Sum(m => m.Fat);

                        fullPlanBuilder.AppendLine("<div class='day-totals'>");
                        fullPlanBuilder.AppendLine("<h4>Totale Giornaliero</h4>");
                        fullPlanBuilder.AppendLine("<div class='total-macros'>");

                        fullPlanBuilder.AppendLine("<div class='total-macro-item calories'>");
                        fullPlanBuilder.AppendLine($"<div class='total-macro-value'>{dayCalories}</div>");
                        fullPlanBuilder.AppendLine("<div class='total-macro-label'>kcal</div>");
                        fullPlanBuilder.AppendLine("<i class='fas fa-fire'></i>");
                        fullPlanBuilder.AppendLine("</div>");

                        fullPlanBuilder.AppendLine("<div class='total-macro-item protein'>");
                        fullPlanBuilder.AppendLine($"<div class='total-macro-value'>{Math.Round(dayProtein)}</div>");
                        fullPlanBuilder.AppendLine("<div class='total-macro-label'>g proteine</div>");
                        fullPlanBuilder.AppendLine("<i class='fas fa-drumstick-bite'></i>");
                        fullPlanBuilder.AppendLine("</div>");

                        fullPlanBuilder.AppendLine("<div class='total-macro-item carbs'>");
                        fullPlanBuilder.AppendLine($"<div class='total-macro-value'>{Math.Round(dayCarbs)}</div>");
                        fullPlanBuilder.AppendLine("<div class='total-macro-label'>g carboidrati</div>");
                        fullPlanBuilder.AppendLine("<i class='fas fa-bread-slice'></i>");
                        fullPlanBuilder.AppendLine("</div>");

                        fullPlanBuilder.AppendLine("<div class='total-macro-item fat'>");
                        fullPlanBuilder.AppendLine($"<div class='total-macro-value'>{Math.Round(dayFat)}</div>");
                        fullPlanBuilder.AppendLine("<div class='total-macro-label'>g grassi</div>");
                        fullPlanBuilder.AppendLine("<i class='fas fa-cheese'></i>");
                        fullPlanBuilder.AppendLine("</div>");

                        fullPlanBuilder.AppendLine("</div>"); // Fine total-macros
                        fullPlanBuilder.AppendLine("</div>"); // Fine day-totals
                    }
                }
                else
                {
                    // Messaggio per giorni senza dati
                    fullPlanBuilder.AppendLine("<div class='no-meals-message'>");
                    fullPlanBuilder.AppendLine("<i class='fas fa-exclamation-circle'></i>");
                    fullPlanBuilder.AppendLine("<p>Nessun pasto specificato per questo giorno.</p>");
                    fullPlanBuilder.AppendLine("</div>");
                }

                fullPlanBuilder.AppendLine("</div>"); // Fine day-meals
                fullPlanBuilder.AppendLine("</div>"); // Fine diet-day-section
            }

            fullPlanBuilder.AppendLine("</div>"); // Fine complete-diet-plan

            return fullPlanBuilder.ToString();
        }

        // Metodo per ordinare i pasti in base all'ora del giorno
        private List<MealViewModel> SortMealsByTimeOfDay(List<MealViewModel> meals)
        {
            var mealOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
                {"Colazione", 1},
                {"Breakfast", 1},
                {"Spuntino mattutino", 2},
                {"Morning Snack", 2},
                {"Spuntino del mattino", 2},
                {"Pranzo", 3},
                {"Lunch", 3},
                {"Spuntino pomeridiano", 4},
                {"Afternoon Snack", 4},
                {"Spuntino del pomeriggio", 4},
                {"Merenda", 5},
                {"Snack", 6},
                {"Cena", 7},
                {"Dinner", 7},
                {"Spuntino serale", 8},
                {"Evening Snack", 8}
            };

            return meals.OrderBy(m => {
                // Cerca prima una corrispondenza esatta
                string normalizedMealType = m.MealType?.Trim().ToLower();
                foreach (var mealType in mealOrder.Keys)
                {
                    if (normalizedMealType.Equals(mealType.ToLower()))
                    {
                        return mealOrder[mealType];
                    }
                }

                // Se non trova una corrispondenza esatta, cerca una corrispondenza parziale
                foreach (var mealType in mealOrder.Keys)
                {
                    if (normalizedMealType.Contains(mealType.ToLower()))
                    {
                        return mealOrder[mealType];
                    }
                }

                // Se non trova nessuna corrispondenza, mette in fondo
                return 100;
            }).ToList();
        }

        private void ParseDietPlanByDays(string dietText)
        {
            try
            {
                // Inizializza il dizionario dei giorni
                DayContents.Clear();

                // Trova sezioni per ogni giorno della settimana
                foreach (var day in StandardDays)
                {
                    // Costruisce vari pattern di ricerca per trovare le sezioni dei giorni
                    var dayPatterns = new List<string> {
                        $@"(?:##?\s*{day}\b|(?:GIORNO|DAY)\s*\d*\s*[-:]\s*{day}\b|{day}\s*:)",
                        $@"(?:##?\s*{day}|{day}:)" // Pattern più semplice come fallback
                    };

                    foreach (var pattern in dayPatterns)
                    {
                        var match = Regex.Match(dietText, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            int startIndex = match.Index;
                            int endIndex;

                            // Trova la fine della sezione (prossimo giorno o fine testo)
                            var nextDayMatch = FindNextDayMatch(dietText, startIndex + 1);
                            endIndex = nextDayMatch.Success ? nextDayMatch.Index : dietText.Length;

                            // Estrae il contenuto del giorno
                            string dayContent = dietText.Substring(startIndex, endIndex - startIndex);

                            // Processa i pasti per questo giorno
                            var meals = ExtractMealsFromDayContent(dayContent);

                            // Se ha trovato pasti, aggiunge al dizionario
                            if (meals.Any())
                            {
                                DayContents[day] = meals;
                                break; // Passa al prossimo giorno
                            }
                        }
                    }
                }

                // Assicurati che tutti i giorni abbiano contenuti anche quando non sono esplicitamente menzionati
                foreach (var day in StandardDays)
                {
                    if (!DayContents.ContainsKey(day))
                    {
                        // Per i giorni che non sono stati trovati esplicitamente, prova a inferire
                        InferMealsForMissingDay(day, dietText);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'analisi del piano alimentare");
            }
        }

        // Trova il prossimo match di un giorno della settimana partendo da una posizione
        private Match FindNextDayMatch(string text, int startIndex)
        {
            string dayPattern = string.Join("|", StandardDays.Select(d => Regex.Escape(d)));
            string pattern = $@"(?:##?\s*(?:{dayPattern})\b|(?:GIORNO|DAY)\s*\d*\s*[-:]\s*(?:{dayPattern})\b|(?:{dayPattern})\s*:)";

            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return regex.Match(text, startIndex);
        }

        // Estrae i pasti dal contenuto di un giorno
        private List<MealViewModel> ExtractMealsFromDayContent(string dayContent)
        {
            var meals = new List<MealViewModel>();

            try
            {
                // Pattern per vari tipi di pasti
                var mealPatterns = new Dictionary<string, List<string>> {
                    { "Colazione", new List<string> { @"###?\s*Colazione\b", @"(?<![\w])Colazione\s*:" } },
                    { "Spuntino mattutino", new List<string> { @"###?\s*Spuntino\s*(?:del)?\s*mattin[oa]\b", @"(?<![\w])Spuntino\s*(?:del)?\s*mattin[oa]\s*:" } },
                    { "Pranzo", new List<string> { @"###?\s*Pranzo\b", @"(?<![\w])Pranzo\s*:" } },
                    { "Spuntino pomeridiano", new List<string> { @"###?\s*Spuntino\s*(?:del)?\s*pomeriggi[oa]\b", @"(?<![\w])Spuntino\s*(?:del)?\s*pomeriggi[oa]\s*:" } },
                    { "Merenda", new List<string> { @"###?\s*Merenda\b", @"(?<![\w])Merenda\s*:" } },
                    { "Cena", new List<string> { @"###?\s*Cena\b", @"(?<![\w])Cena\s*:" } },
                    { "Spuntino", new List<string> { @"###?\s*Spuntino(?!\s*(?:del)?\s*(?:mattin|pomeriggi))\b", @"(?<![\w])Spuntino(?!\s*(?:del)?\s*(?:mattin|pomeriggi))\s*:" } }
                };

                // Lista di tutti i match trovati
                var allMealMatches = new List<(string MealType, int Position, int PatternIndex)>();

                // Cerca tutti i pattern di pasti nel contenuto del giorno
                foreach (var mealType in mealPatterns)
                {
                    for (int i = 0; i < mealType.Value.Count; i++)
                    {
                        var matches = Regex.Matches(dayContent, mealType.Value[i], RegexOptions.IgnoreCase);
                        foreach (Match match in matches)
                        {
                            allMealMatches.Add((mealType.Key, match.Index, i));
                        }
                    }
                }

                // Ordina per posizione
                allMealMatches = allMealMatches.OrderBy(m => m.Position).ToList();

                // Estrai contenuti per ogni pasto trovato
                for (int i = 0; i < allMealMatches.Count; i++)
                {
                    var (mealType, startPos, patternIndex) = allMealMatches[i];

                    // Calcola fine del segmento
                    int endPos = (i < allMealMatches.Count - 1)
                        ? allMealMatches[i + 1].Position
                        : dayContent.Length;

                    string mealContent = dayContent.Substring(startPos, endPos - startPos);
                    var meal = ProcessMealContent(mealType, mealContent);

                    if (meal != null)
                    {
                        meals.Add(meal);
                    }
                }

                // Se non ha trovato pasti ma c'è un contenuto significativo, crea un pasto generico
                if (!meals.Any() && !string.IsNullOrWhiteSpace(dayContent) && dayContent.Length > 50)
                {
                    var genericMeal = new MealViewModel
                    {
                        MealType = "Piano Alimentare",
                        FormattedFoods = FormatGenericContent(dayContent),
                        RawContent = dayContent
                    };

                    // Cerca di estrarre valori nutrizionali
                    ExtractNutrientsFromContent(dayContent, genericMeal);

                    meals.Add(genericMeal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'estrazione dei pasti dal contenuto di un giorno");
            }

            return meals;
        }

        // Processa il contenuto di un pasto per estrarre valori nutrizionali e formattare gli alimenti
        private MealViewModel ProcessMealContent(string mealType, string content)
        {
            try
            {
                // Rimuovi l'intestazione del pasto
                string cleanContent = Regex.Replace(content, $@"###?\s*{mealType}\b.*?\n", "", RegexOptions.IgnoreCase);
                cleanContent = Regex.Replace(cleanContent, $@"(?<![\w]){mealType}\s*:.*?\n", "", RegexOptions.IgnoreCase);

                var meal = new MealViewModel
                {
                    MealType = mealType,
                    RawContent = content
                };

                // Estrai i valori nutrizionali
                ExtractNutrientsFromContent(content, meal);

                // Formatta gli alimenti
                meal.FormattedFoods = FormatFoodItems(cleanContent);

                return meal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel processare il contenuto di un pasto");
                return null;
            }
        }

        // Estrai valori nutrizionali dal testo
        private void ExtractNutrientsFromContent(string content, MealViewModel meal)
        {
            try
            {
                // Pattern migliorati per estrarre i valori nutrizionali
                var calorieMatches = new List<Match> {
            Regex.Match(content, @"(?:circa\s*)?(\d+(?:[.,]\d+)?)\s*(?:k[cC]al|[cC]alorie)", RegexOptions.IgnoreCase),
            Regex.Match(content, @"[cC]alorie:?\s*(\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase),
            Regex.Match(content, @"(\d+(?:[.,]\d+)?)\s*calorie", RegexOptions.IgnoreCase)
        };

                var proteinMatches = new List<Match> {
            Regex.Match(content, @"[pP]roteine:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase),
            Regex.Match(content, @"(\d+(?:[.,]\d+)?)\s*g\s+(?:di\s+)?[pP]roteine", RegexOptions.IgnoreCase),
            Regex.Match(content, @"[pP]:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase)
        };

                var carbsMatches = new List<Match> {
            Regex.Match(content, @"[cC]arboidrati:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase),
            Regex.Match(content, @"(\d+(?:[.,]\d+)?)\s*g\s+(?:di\s+)?[cC]arboidrati", RegexOptions.IgnoreCase),
            Regex.Match(content, @"[cC]:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase)
        };

                var fatMatches = new List<Match> {
            Regex.Match(content, @"[gG]rassi:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase),
            Regex.Match(content, @"(\d+(?:[.,]\d+)?)\s*g\s+(?:di\s+)?[gG]rassi", RegexOptions.IgnoreCase),
            Regex.Match(content, @"[gG]:?\s*(\d+(?:[.,]\d+)?)\s*g", RegexOptions.IgnoreCase)
        };

                // Trova il primo match valido per ogni nutriente
                var calorieMatch = calorieMatches.FirstOrDefault(m => m.Success);
                var proteinMatch = proteinMatches.FirstOrDefault(m => m.Success);
                var carbsMatch = carbsMatches.FirstOrDefault(m => m.Success);
                var fatMatch = fatMatches.FirstOrDefault(m => m.Success);

                // Converti i valori
                if (calorieMatch?.Success == true)
                {
                    int.TryParse(calorieMatch.Groups[1].Value.Replace(',', '.').Split('.')[0], out int calories);
                    meal.Calories = calories;
                }

                if (proteinMatch?.Success == true)
                {
                    double.TryParse(proteinMatch.Groups[1].Value.Replace(',', '.'), out double protein);
                    meal.Protein = protein;
                }

                if (carbsMatch?.Success == true)
                {
                    double.TryParse(carbsMatch.Groups[1].Value.Replace(',', '.'), out double carbs);
                    meal.Carbs = carbs;
                }

                if (fatMatch?.Success == true)
                {
                    double.TryParse(fatMatch.Groups[1].Value.Replace(',', '.'), out double fat);
                    meal.Fat = fat;
                }

                // Verifica la coerenza dei dati calcolando le calorie dai macronutrienti
                // se i macronutrienti sono presenti ma le calorie no
                if (meal.Calories == 0 && (meal.Protein > 0 || meal.Carbs > 0 || meal.Fat > 0))
                {
                    // Formula: 4 cal per g di proteine, 4 cal per g di carb, 9 cal per g di grassi
                    meal.Calories = (int)Math.Round((meal.Protein * 4) + (meal.Carbs * 4) + (meal.Fat * 9));
                }

                // Se ci sono calorie ma nessun macronutriente, stima i macronutrienti
                // in base a una distribuzione generica 25% P, 50% C, 25% F
                if (meal.Calories > 0 && meal.Protein == 0 && meal.Carbs == 0 && meal.Fat == 0)
                {
                    // Questo è un'approssimazione, quindi arrotondiamo i valori
                    meal.Protein = Math.Round((meal.Calories * 0.25) / 4);
                    meal.Carbs = Math.Round((meal.Calories * 0.5) / 4);
                    meal.Fat = Math.Round((meal.Calories * 0.25) / 9);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'estrazione dei valori nutrizionali");
            }
        }

        // Formatta gli alimenti in una lista HTML
        private string FormatFoodItems(string content)
        {
            var formattedContent = new System.Text.StringBuilder();
            formattedContent.AppendLine("<ul class='food-list'>");

            // Cerca elementi di lista o righe significative
            var foodItems = new List<string>();

            // Prima cerca elementi di lista (con - o • come punto elenco)
            var bulletItems = Regex.Matches(content, @"[-•]\s+([^\n]+)")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToList();

            if (bulletItems.Any())
            {
                foodItems.AddRange(bulletItems.Where(item => !string.IsNullOrWhiteSpace(item)));
            }
            else
            {
                // Se non ci sono bullet points, usa le righe non vuote
                foodItems = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .Where(line => !Regex.IsMatch(line, @"^(?:Calorie|Proteine|Carboidrati|Grassi)", RegexOptions.IgnoreCase))
                    .ToList();
            }

            // Aggiungi gli elementi alla lista
            foreach (var item in foodItems)
            {
                // Evita di includere righe che sembrano essere intestazioni di valori nutrizionali
                if (!Regex.IsMatch(item, @"^(?:Calorie|Proteine|Carboidrati|Grassi|Nutrienti)", RegexOptions.IgnoreCase))
                {
                    formattedContent.AppendLine($"<li><i class='fas fa-utensils text-success'></i> {item}</li>");
                }
            }

            // Se non ci sono elementi, aggiungi un messaggio generico
            if (foodItems.Count == 0 || !formattedContent.ToString().Contains("<li>"))
            {
                formattedContent.AppendLine("<li><i class='fas fa-info-circle text-info'></i> Vedi il piano completo per i dettagli specifici.</li>");
            }

            formattedContent.AppendLine("</ul>");
            return formattedContent.ToString();
        }

        // Formatta un contenuto generico
        private string FormatGenericContent(string content)
        {
            var formattedContent = new System.Text.StringBuilder();
            formattedContent.AppendLine("<div class='generic-content'>");

            // Rimuovi intestazioni markdown
            content = Regex.Replace(content, @"#{1,3}\s*", "");

            // Dividi in sezioni
            var sections = content
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line))
                .ToList();

            if (sections.Any())
            {
                formattedContent.AppendLine("<ul class='content-sections'>");
                foreach (var section in sections)
                {
                    // Formatta elementi che sembrano alimenti
                    string formattedSection = section;
                    if (section.StartsWith("-") || section.StartsWith("•"))
                    {
                        formattedSection = section.Substring(1).Trim();
                    }

                    // Evita di includere righe con solo intestazioni di valori nutrizionali
                    if (!Regex.IsMatch(formattedSection, @"^(?:Calorie|Proteine|Carboidrati|Grassi):", RegexOptions.IgnoreCase))
                    {
                        formattedContent.AppendLine($"<li><i class='fas fa-utensils text-success'></i> {formattedSection}</li>");
                    }
                }
                formattedContent.AppendLine("</ul>");
            }
            else
            {
                formattedContent.AppendLine("<p class='text-muted'>Nessun dettaglio disponibile.</p>");
            }

            formattedContent.AppendLine("</div>");
            return formattedContent.ToString();
        }

        // Infer pasti per giorni mancanti
        private void InferMealsForMissingDay(string day, string fullText)
        {
            try
            {
                // Per prima cosa cerca se ci sono menzioni esplicite del giorno in altre parti del testo
                var dayPattern = new Regex($@"\b{day}\b", RegexOptions.IgnoreCase);
                var match = dayPattern.Match(fullText);

                if (match.Success)
                {
                    // Estrai una porzione di testo intorno al match
                    int startPos = Math.Max(0, match.Index - 50);
                    int endPos = Math.Min(fullText.Length, match.Index + 1000);
                    string daySection = fullText.Substring(startPos, endPos - startPos);

                    // Prova a estrarre pasti da questa sezione
                    var meals = ExtractMealsFromDayContent(daySection);

                    if (meals.Any())
                    {
                        DayContents[day] = meals;
                        return;
                    }
                }

                // Se non ha trovato menzioni esplicite, usa la strategia di duplicazione
                // basata su una rotazione settimanale comune: Lun=Gio, Mar=Ven, Mer=Sab/Dom
                string sourceDay = null;

                if (day == "Giovedì" && DayContents.ContainsKey("Lunedì"))
                    sourceDay = "Lunedì";
                else if (day == "Venerdì" && DayContents.ContainsKey("Martedì"))
                    sourceDay = "Martedì";
                else if (day == "Sabato" && DayContents.ContainsKey("Mercoledì"))
                    sourceDay = "Mercoledì";
                else if (day == "Domenica" && (DayContents.ContainsKey("Sabato") || DayContents.ContainsKey("Mercoledì")))
                    sourceDay = DayContents.ContainsKey("Sabato") ? "Sabato" : "Mercoledì";

                if (sourceDay != null && DayContents.ContainsKey(sourceDay))
                {
                    // Clona i pasti dal giorno di origine con una etichetta che indica che sono stati duplicati
                    var sourceMeals = DayContents[sourceDay];
                    var clonedMeals = sourceMeals.Select(m => new MealViewModel
                    {
                        MealType = m.MealType,
                        FormattedFoods = m.FormattedFoods.Replace("</ul>", "<li class='text-muted small'><i class='fas fa-info-circle'></i> Piano duplicato da " + sourceDay + "</li></ul>"),
                        RawContent = m.RawContent,
                        Calories = m.Calories,
                        Protein = m.Protein,
                        Carbs = m.Carbs,
                        Fat = m.Fat
                    }).ToList();

                    DayContents[day] = clonedMeals;
                }
                else
                {
                    // Per i giorni senza corrispondenza, crea un messaggio generico
                    DayContents[day] = new List<MealViewModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'inferire pasti per il giorno {Day}", day);
                DayContents[day] = new List<MealViewModel>();
            }
        }

        // Assicurati che tutti i giorni abbiano contenuto
        private void EnsureAllDaysHaveContent(string fullText)
        {
            foreach (var day in StandardDays)
            {
                if (!DayContents.ContainsKey(day) || !DayContents[day].Any())
                {
                    InferMealsForMissingDay(day, fullText);
                }
            }
        }
    }

    public class MealViewModel
    {
        public string MealType { get; set; }
        public string FormattedFoods { get; set; }
        public string RawContent { get; set; }
        public int Calories { get; set; }
        public double Protein { get; set; }
        public double Carbs { get; set; }
        public double Fat { get; set; }

        public bool HasNutrients => Protein > 0 || Carbs > 0 || Fat > 0 || Calories > 0;
    }
}