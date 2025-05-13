using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Models;

namespace Nutri_Plan.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;
        private static readonly SemaphoreSlim _apiSemaphore = new SemaphoreSlim(1, 1);

        public GeminiService(IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(3); // Aumenta il timeout a 3 minuti
            _apiKey = configuration["GeminiApiKey"];
            _logger = logger;

            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("Chiave API Gemini non trovata nella configurazione");
                throw new InvalidOperationException("La chiave API per Gemini non è configurata.");
            }
        }

        public async Task<string> GenerateDietPlanAsync(UserProfile userProfile, User user)
        {
            _logger.LogInformation("Inizio generazione piano alimentare per utente {UserId}", user.Id);

            // Costruisci il prompt
            string prompt = BuildDietPrompt(userProfile, user);

            // Usa un semaforo per assicurarci che facciamo solo una richiesta alla volta
            await _apiSemaphore.WaitAsync();

            try
            {
                // Lista di modelli verificati e disponibili
                string[] modelsToTry = new[] {
                    "gemini-1.5-flash", // Modello più leggero con meno limiti di quota
                    "gemini-1.5-pro-latest"  // Modello più avanzato
                };

                // Prova ogni modello fino a quando uno funziona
                foreach (var model in modelsToTry)
                {
                    try
                    {
                        _logger.LogInformation("Tentativo con modello {Model}", model);

                        // Prepara la richiesta con un prompt semplificato
                        var requestData = new
                        {
                            contents = new[]
                            {
                                new { role = "user", parts = new[] { new { text = prompt } } }
                            },
                            generationConfig = new
                            {
                                temperature = 0.7,
                                topK = 32,
                                topP = 1,
                                maxOutputTokens = 4096
                            }
                        };

                        // Url corretto con i modelli più recenti
                        var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={_apiKey}";

                        var content = new StringContent(
                            JsonSerializer.Serialize(requestData),
                            Encoding.UTF8,
                            "application/json");

                        // Aggiungi un ritardo significativo tra i tentativi (10 secondi)
                        if (model != modelsToTry[0])
                        {
                            _logger.LogInformation("Attesa di 10 secondi prima di provare il prossimo modello");
                            await Task.Delay(10000);
                        }

                        // Esegui la chiamata API
                        _logger.LogInformation("Invio richiesta a {Model}", model);
                        var response = await _httpClient.PostAsync(apiUrl, content);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        // Log dettagliato della risposta
                        _logger.LogInformation("Risposta da {Model}: Status {Status}",
                            model, response.StatusCode);

                        // Se abbiamo successo, elabora la risposta
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Risposta ricevuta con successo dal modello {Model}", model);
                            string extractedText = ExtractDietPlanFromResponse(responseBody);

                            if (!string.IsNullOrEmpty(extractedText) && extractedText.Length > 100)
                            {
                                return extractedText;
                            }
                        }
                        else if ((int)response.StatusCode == 429) // Too Many Requests
                        {
                            _logger.LogWarning("Limite di quota raggiunto per {Model}. Risposta: {Response}",
                                model, responseBody);

                            // Estrai il tempo di attesa consigliato dalla risposta
                            int retryDelay = 30; // Default 30 secondi
                            try
                            {
                                using var jsonDoc = JsonDocument.Parse(responseBody);
                                if (jsonDoc.RootElement.TryGetProperty("error", out var error) &&
                                    error.TryGetProperty("details", out var details))
                                {
                                    foreach (var detail in details.EnumerateArray())
                                    {
                                        if (detail.TryGetProperty("@type", out var type) &&
                                            type.GetString() == "type.googleapis.com/google.rpc.RetryInfo" &&
                                            detail.TryGetProperty("retryDelay", out var delay))
                                        {
                                            string delayStr = delay.GetString();
                                            if (delayStr.EndsWith("s"))
                                            {
                                                retryDelay = int.Parse(delayStr.TrimEnd('s'));
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Errore nel parsing della risposta d'errore");
                            }

                            _logger.LogInformation("Attesa di {Delay} secondi prima di provare il prossimo modello", retryDelay);
                            await Task.Delay(retryDelay * 1000);
                        }
                        else
                        {
                            _logger.LogWarning("Errore dal modello {Model}: {Status}. Risposta: {Response}",
                                model, response.StatusCode, responseBody);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Errore durante la chiamata al modello {Model}", model);
                    }
                }

                // Prova una seconda volta con un prompt più corto
                _logger.LogInformation("Tutti i modelli hanno fallito. Provo con prompt semplificato");
                return await TryWithShorterPrompt(userProfile, user);
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        // Aggiungi questo metodo alla classe GeminiService esistente

        public async Task<string> GetGeminiResponseAsync(string prompt, string preferredModel = "gemini-1.5-flash", int maxTokens = 2048)
        {
            _logger.LogInformation("Richiesta a Gemini: {PromptPreview}", prompt.Substring(0, Math.Min(50, prompt.Length)) + "...");

            await _apiSemaphore.WaitAsync();

            try
            {
                // Lista di modelli da tentare in ordine
                string[] modelsToTry = new[] { preferredModel, "gemini-1.5-pro-latest" };
                string responseText = null;

                foreach (var model in modelsToTry)
                {
                    try
                    {
                        _logger.LogInformation("Tentativo con modello {Model}", model);

                        var requestData = new
                        {
                            contents = new[]
                            {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                            generationConfig = new
                            {
                                temperature = 0.2,
                                topK = 32,
                                topP = 1,
                                maxOutputTokens = maxTokens
                            }
                        };

                        var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={_apiKey}";

                        var content = new StringContent(
                            JsonSerializer.Serialize(requestData),
                            Encoding.UTF8,
                            "application/json");

                        if (model != modelsToTry[0])
                        {
                            await Task.Delay(2000); // Breve attesa tra tentativi
                        }

                        var response = await _httpClient.PostAsync(apiUrl, content);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            responseText = ExtractTextFromResponse(responseBody);

                            if (!string.IsNullOrEmpty(responseText) && responseText.Length > 20)
                            {
                                break; // Abbiamo ottenuto una risposta valida
                            }
                        }
                        else if ((int)response.StatusCode == 429)
                        {
                            _logger.LogWarning("Limite di quota raggiunto per {Model}", model);
                            await Task.Delay(2000); // Attesa prima di provare il prossimo modello
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Errore durante la chiamata al modello {Model}", model);
                    }
                }

                return responseText;
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }

        private string ExtractTextFromResponse(string responseJson)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(responseJson);

                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> TryWithShorterPrompt(UserProfile userProfile, User user)
        {
            try
            {
                // Calcola una stima delle calorie più semplice
                double bmr = 10 * userProfile.Peso + 6.25 * userProfile.Altezza - 5 * userProfile.Eta + 5;
                double activityFactor = 1.375; // Default moderato
                double tdee = bmr * activityFactor;

                var obiettivoPeso = userProfile.ObbiettivoPeso?.ToLower() ?? "mantenere";
                if (obiettivoPeso.Contains("dimagrire")) tdee -= 500;
                else if (obiettivoPeso.Contains("aumentare")) tdee += 500;

                int calorieTotali = Convert.ToInt32(Math.Round(tdee));

                // Prompt molto più breve che usa meno token
                string shortPrompt = $@"Crea un piano settimanale alimentare di {calorieTotali} calorie. Età: {userProfile.Eta}, Peso: {userProfile.Peso}kg, Obiettivo: {userProfile.ObbiettivoPeso}";

                _logger.LogInformation("Tentativo con prompt semplificato");

                // Attesa aggiuntiva per rispettare i limiti di quota
                await Task.Delay(15000);

                var requestData = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = shortPrompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 32,
                        topP = 1,
                        maxOutputTokens = 2048 // Ridotto per consumare meno token
                    }
                };

                // Usa gemini-1.5-flash che ha meno limiti di quota
                var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key={_apiKey}";

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                _logger.LogInformation("Invio richiesta semplificata a gemini-1.5-flash");
                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Risposta ricevuta con successo al prompt semplificato");
                    return ExtractDietPlanFromResponse(responseBody);
                }
                else
                {
                    _logger.LogWarning("Anche il prompt semplificato ha fallito: {Status}", response.StatusCode);
                    return GetFallbackDietPlan(userProfile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il tentativo con prompt semplificato");
                return GetFallbackDietPlan(userProfile);
            }
        }



        private string BuildDietPrompt(UserProfile userProfile, User user)
        {
            double bmr = 10 * userProfile.Peso + 6.25 * userProfile.Altezza - 5 * userProfile.Eta + 5;
            double activityFactor = 1.2; // Default: sedentario

            // Mappa il livello di attività 
            string livelloAttivita = userProfile.LivelloAttivita?.ToLower() ?? "sedentario";
            if (livelloAttivita.Contains("sedentario")) activityFactor = 1.2;
            else if (livelloAttivita.Contains("leggero") || livelloAttivita.Contains("moderato")) activityFactor = 1.375;
            else if (livelloAttivita.Contains("attivo") && !livelloAttivita.Contains("molto")) activityFactor = 1.55;
            else if (livelloAttivita.Contains("molto attivo") || livelloAttivita.Contains("intenso")) activityFactor = 1.725;
            else if (livelloAttivita.Contains("atleta") || livelloAttivita.Contains("estremo")) activityFactor = 1.9;

            double tdee = bmr * activityFactor;

            // Adatta il TDEE
            string obiettivoPeso = userProfile.ObbiettivoPeso?.ToLower() ?? "mantenere";
            if (obiettivoPeso.Contains("dimagrire") || obiettivoPeso.Contains("perdere")) tdee -= 500;
            else if (obiettivoPeso.Contains("ingrassare") || obiettivoPeso.Contains("aumentare")) tdee += 500;

            _logger.LogInformation("Calcolo TDEE: {TDEE} kcal per utente {UserId}", tdee, user.Id);

            // Prompt completo ma ottimizzato
            return $@"Come nutrizionista, crea un piano alimentare settimanale per:
- Età: {userProfile.Eta} anni
- Peso: {userProfile.Peso} kg
- Altezza: {userProfile.Altezza} cm
- Attività: {userProfile.LivelloAttivita}
- Obiettivo: {userProfile.ObbiettivoPeso}
- Cibo preferito: {userProfile.CiboPreferito ?? "Nessuna preferenza"}

Fabbisogno: {Math.Round(tdee)} calorie.

Include:
- 7 giorni (Lunedì-Domenica)
- Per giorno: colazione, spuntini, pranzo, cena
- Per pasto: alimenti con grammature
- Valori nutrizionali per pasto
- Totali giornalieri

Usa ingredienti comuni italiani.";
        }

        private string ExtractDietPlanFromResponse(string responseJson)
        {
            try
            {
                // Stesso codice di prima
                using var jsonDoc = JsonDocument.Parse(responseJson);

                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textElement))
                {
                    string text = textElement.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }

                _logger.LogWarning("Struttura di risposta non standard da Gemini API");
                return "Il formato della risposta dall'API non è quello atteso. Riprova più tardi.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'estrazione del piano alimentare dalla risposta");
                return "Si è verificato un errore nell'elaborazione della risposta. Riprova più tardi.";
            }
        }

        private string GetFallbackDietPlan(UserProfile userProfile)
        {
            // ... stesso codice per il piano fallback ...
            double bmr = 10 * userProfile.Peso + 6.25 * userProfile.Altezza - 5 * userProfile.Eta + 5;
            double activityFactor = 1.375;

            string livelloAttivita = userProfile.LivelloAttivita?.ToLower() ?? "sedentario";
            if (livelloAttivita.Contains("sedentario")) activityFactor = 1.2;
            else if (livelloAttivita.Contains("leggero")) activityFactor = 1.375;
            else if (livelloAttivita.Contains("attivo")) activityFactor = 1.55;
            else if (livelloAttivita.Contains("molto")) activityFactor = 1.725;

            double tdee = bmr * activityFactor;

            string obiettivoPeso = userProfile.ObbiettivoPeso?.ToLower() ?? "mantenere";
            if (obiettivoPeso.Contains("dimagrire")) tdee -= 500;
            else if (obiettivoPeso.Contains("ingrassare")) tdee += 500;

            int calorieTotali = (int)Math.Round(tdee);

            return $@"# Piano Alimentare Personalizzato (Temporaneo)
*Nota: Questo è un piano temporaneo generato dal sistema perché l'API è momentaneamente non disponibile.*

## Informazioni Nutrizionali
- **Calorie giornaliere**: {calorieTotali} kcal
- **Proteine**: {Math.Round(calorieTotali * 0.25 / 4)}g 
- **Carboidrati**: {Math.Round(calorieTotali * 0.5 / 4)}g
- **Grassi**: {Math.Round(calorieTotali * 0.25 / 9)}g

## Piano settimanale
[...]

*Ti consigliamo di riprovare più tardi quando il servizio sarà disponibile.*";
        }
    }
}