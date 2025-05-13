using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nutri_Plan.Data;
using Nutri_Plan.Models;
using Nutri_Plan.Services; // Aggiunto per GeminiService

var builder = WebApplication.CreateBuilder(args);

// Aggiungi servizi al container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<User, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddRazorPages();

// Configura le opzioni delle Razor Pages per richiedere l'autenticazione per impostazione predefinita
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Error");
});

// Aggiungi la policy per l'admin
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
});

// Configura la pagina di login come pagina predefinita per gli utenti non autenticati
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Registra HttpClient per le chiamate API
builder.Services.AddHttpClient();

// Registra la configurazione
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

// Registra la chiave API di Gemini
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

// Registra il servizio GeminiService per la generazione di diete
builder.Services.AddTransient<GeminiService>();

// Aggiungi questa riga dove registri i servizi
builder.Services.AddScoped<FoodNutritionService>();

var app = builder.Build();

// Configura la pipeline HTTP request.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Reindirizzamento intelligente alla pagina di login o alla Home in base all'autenticazione
app.MapGet("/", context =>
{
    // Se l'utente è autenticato, reindirizza alla Home
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Home");
    }
    else
    {
        context.Response.Redirect("/Account/Login");
    }
    return Task.CompletedTask;
});

app.MapRazorPages();

// Inizializzazione del database e creazione di utenti predefiniti
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Applica le migrazioni in atteso se presenti
        context.Database.Migrate();

        // Crea il ruolo Admin se non esiste
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // Crea l'utente admin se non esiste
        var adminEmail = "admin@nutriplan.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = adminEmail,
                Email = adminEmail,
                Nome = "Admin",
                Cognome = "System",
                EmailConfirmed = true,
                IsAdmin = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Manteniamo anche l'utente test per facilitare gli accessi
        var testUserEmail = "utente@test.com";
        var testUser = await userManager.FindByEmailAsync(testUserEmail);

        if (testUser == null)
        {
            testUser = new User
            {
                UserName = testUserEmail,
                Email = testUserEmail,
                Nome = "Utente",
                Cognome = "Test",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(testUser, "Password123");
        }

        // Verifica che la classe Diet sia registrata nel DbContext
        if (!context.Model.GetEntityTypes().Any(e => e.ClrType == typeof(Diet)))
        {
            logger.LogWarning("Il modello Diet non è registrato nel DbContext. Assicurati di aggiungere il DbSet<Diet> in ApplicationDbContext.");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Si è verificato un errore durante l'inizializzazione del database.");
    }
}

// Inserisci la chiave API di Gemini negli app settings se non è già presente
try
{
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var geminiApiKey = configuration["GeminiApiKey"];

    if (string.IsNullOrEmpty(geminiApiKey))
    {
        Console.WriteLine("ATTENZIONE: La chiave API di Gemini non è configurata in appsettings.json.");
        Console.WriteLine("Aggiungi la chiave 'GeminiApiKey' con il valore 'AIzaSyDi4oSmuFFk0DAqYwc-65b1rJX8Zjbfj54' in appsettings.json.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Errore durante il controllo della chiave API: {ex.Message}");
}

app.Run();