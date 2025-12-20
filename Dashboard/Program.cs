using System.Security.Claims;
using Dashboard.Data;
using Dashboard.Entities;
using Dashboard.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);

// ======================
// Database (SQL Server)
// ======================
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
          ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

builder.Services.AddDbContext<BlogContext>(opt => opt.UseSqlServer(cs));
builder.Services.AddDbContextFactory<BlogContext>(opt => opt.UseSqlServer(cs), ServiceLifetime.Scoped);

// ======================
// Identity (Cookies)
// ======================
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options => { })
    .AddEntityFrameworkStores<BlogContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Account/Login";
    opts.AccessDeniedPath = "/Account/AccessDenied";
});

// ======================
// Authorization policies
// ======================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageAll", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CanManageOwn", policy =>
        policy.RequireAssertion(ctx =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return false;
            if (ctx.User.IsInRole("Admin")) return true;
            if (ctx.Resource is Article article) return article.AuthorId == userId;
            return false;
        }));
});

// ======================
// MVC + Session
// ======================
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// ======================
// Blazor (Razor components)
// ======================
// CORRECTION : On ne garde qu'une seule déclaration ici
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// ======================
// App services
// ======================
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IDbQuizService, DbQuizService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IAIChessLogService, AIChessLogService>();

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddHostedService<GoalReminderService>();

// SUPPRIMÉ : Le doublon de AddRazorComponents a été retiré ici

var app = builder.Build();

// ======================
// Middleware
// ======================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ======================
// Endpoints
// ======================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// CORRECTION : On ne garde qu'un seul MapRazorComponents ici
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// ======================
// Migrations + Seed (Code inchangé)
// ======================
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = sp.GetRequiredService<BlogContext>();
        await db.Database.MigrateAsync();
        // ... reste de votre code de seed ...
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur au démarrage");
    }
}

app.Run();