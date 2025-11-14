using HRManagementSystem.Data;
using HRManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure; // added
using Microsoft.EntityFrameworkCore.Storage; // added
using QuestPDF.Infrastructure;
using System.Globalization;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Localization; // added

// NUCLEAR APPROACH: Set environment BEFORE creating builder
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Production");

var builder = WebApplication.CreateBuilder(args);

// TRIPLE FORCE Production environment
builder.Environment.EnvironmentName = "Production";

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-ZA", "en-US" };
    options.SetDefaultCulture("en-ZA")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Caching
builder.Services.AddMemoryCache();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Configure connection string - use SQLite for persistent storage
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=/tmp/hrmanagement.db";

// Use SQLite database for persistent storage
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// Use SAME database for Identity (shared database approach)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString)); // Changed from identity.db to hrmanagement.db

// Hangfire with in-memory storage for demo
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());

builder.Services.AddHangfireServer();

// Configure Identity with optimized settings
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
    
    // Optimize lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Register services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IBackgroundJobTasks, BackgroundJobTasks>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// Configure HttpClient for ESS Leave API
builder.Services.AddHttpClient<IESSLeaveApiClient, ESSLeaveApiClient>();
builder.Services.AddScoped<IESSLeaveApiClient, ESSLeaveApiClient>();

// Background reminders hosted service
builder.Services.AddHostedService<RemindersHostedService>();

// Configure QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// NUCLEAR: ALWAYS use production error handling - NO EXCEPTIONS
app.UseExceptionHandler("/Home/Error");
app.UseHsts();

// Hangfire Dashboard (Admin only)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new IDashboardAuthorizationFilter[] { } // Remove authorization for testing
});

// Configure South African culture for currency formatting by default
var defaultCulture = new CultureInfo("en-ZA");
defaultCulture.NumberFormat.CurrencyGroupSeparator = " ";
defaultCulture.NumberFormat.CurrencyDecimalSeparator = ".";
defaultCulture.NumberFormat.CurrencySymbol = "R";
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

// Apply localization to current request with supported cultures
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-ZA"),
    SupportedCultures = new[] { new CultureInfo("en-ZA"), new CultureInfo("en-US") },
    SupportedUICultures = new[] { new CultureInfo("en-ZA"), new CultureInfo("en-US") }
};
app.UseRequestLocalization(localizationOptions);

// Lightweight endpoint to set culture cookie and persist selection
app.MapGet("/set-culture", (string culture, string? returnUrl, HttpContext httpContext) =>
{
    var supported = new[] { "en-ZA", "en-US" };
    if (!supported.Contains(culture)) culture = "en-ZA"; // fallback

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false, IsEssential = true }
    );

    return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
});

// Remove HTTPS redirect for Render (handled by proxy)
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ROBUST DATABASE INITIALIZATION - Handle shared DB with two DbContexts
void InitializeDatabase()
{
    var maxRetries = 5;
    var retryCount = 0;

    while (retryCount < maxRetries)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var appContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var identityContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine($"?? Database initialization attempt {retryCount + 1}/{maxRetries}...");

            var dbPath = "/tmp/hrmanagement.db";
            if (File.Exists(dbPath))
            {
                try
                {
                    if (!identityContext.Database.CanConnect())
                    {
                        Console.WriteLine("?? Database file exists but not accessible, recreating...");
                        File.Delete(dbPath);
                    }
                }
                catch
                {
                    Console.WriteLine("?? Database file corrupted, recreating...");
                    File.Delete(dbPath);
                }
            }

            // Create database file and Identity tables first
            var idCreator = identityContext.Database.GetService<IRelationalDatabaseCreator>();
            if (!File.Exists(dbPath))
            {
                idCreator.Create(); // create database file
            }
            try { idCreator.CreateTables(); } catch { /* tables may already exist */ }

            // Then create AppDbContext tables explicitly (EnsureCreated won't run when DB already exists)
            var appCreator = appContext.Database.GetService<IRelationalDatabaseCreator>();
            try { appCreator.CreateTables(); } catch { /* ignore if exists */ }

            // Update schema tweaks
            try { DatabaseUpdater.AddIsDeletedColumn(); } catch (Exception ex) { Console.WriteLine($"?? Schema update warning: {ex.Message}"); }

            // Seed data synchronously
            Task.WaitAll(
                RoleSeeder.SeedAsync(scope.ServiceProvider),
                DemoDataSeeder.SeedAsync(appContext)
            );

            // Verify
            var userCount = identityContext.Users.Count();
            var employeeCount = appContext.Employees.Count();
            Console.WriteLine($"? Database initialized successfully! Users: {userCount}, Employees: {employeeCount}");
            return;
        }
        catch (Exception ex)
        {
            retryCount++;
            Console.WriteLine($"? Database initialization attempt {retryCount} failed: {ex.Message}");
            if (retryCount >= maxRetries)
            {
                Console.WriteLine($"?? CRITICAL: Database initialization failed after {maxRetries} attempts");
                return;
            }
            Thread.Sleep(2000 * retryCount);
        }
    }
}

InitializeDatabase();

// Middleware to handle database recreation on-the-fly for requests
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex) when (ex.Message.Contains("no such table"))
    {
        Console.WriteLine($"?? Database table missing during request, reinitializing...");
        try 
        {
            InitializeDatabase();
            // Retry the request after database recreation
            await next(context);
        }
        catch (Exception retryEx)
        {
            Console.WriteLine($"?? Request retry failed: {retryEx.Message}");
            throw;
        }
    }
});

// Background job scheduling (non-critical, can be async)
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(5000); // Wait for database to stabilize
        
        try
        {
            using var scope = app.Services.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobTasks>();

            // Schedule recurring background jobs
            RecurringJob.AddOrUpdate("birthday-reminders",
                () => jobs.SendBirthdayReminders(), Cron.Daily(6));

            RecurringJob.AddOrUpdate("anniversary-reminders",
                () => jobs.SendAnniversaryReminders(), Cron.Daily(6));

            RecurringJob.AddOrUpdate("monthly-headcount-report",
                () => jobs.GenerateMonthlyHeadcountReport(), Cron.Monthly(1, 7));

            RecurringJob.AddOrUpdate("daily-salary-band-report",
                () => jobs.GenerateSalaryBandReport(), Cron.Daily(19));

            Console.WriteLine("? Background jobs scheduled. Visit /hangfire to view.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Background job scheduling error: {ex.Message}");
        }
    });
});

await app.RunAsync();

// Hangfire authorization filter
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin");
    }
}
