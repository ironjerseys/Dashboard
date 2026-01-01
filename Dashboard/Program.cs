using Dashboard.Components;
using Dashboard.Data;
using Dashboard.Entities;
using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ======================
// Database (SQL Server)
// ======================
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
          ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

builder.Services.AddDbContext<BlogContext>(opt => opt.UseSqlServer(cs));
builder.Services.AddDbContextFactory<BlogContext>(opt => opt.UseSqlServer(cs), ServiceLifetime.Scoped);

// Session needs a cache
builder.Services.AddDistributedMemoryCache();

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
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

// ======================
// App services
// ======================
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IDbQuizService, QuestionTechniqueService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IAIChessLogService, AIChessLogService>();
builder.Services.AddScoped<IEmailSettingsService, EmailSettingsService>();
builder.Services.AddScoped<ILeitnerService, LeitnerService>();


builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddHostedService<GoalReminderService>();

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

// IMPORTANT : nécessaire pour les formulaires Blazor
app.UseAntiforgery();

// ======================
// Helper (anti open-redirect)
// ======================
static string SafeReturnUrl(string? returnUrl, string fallback = "/dashboard")
{
    if (string.IsNullOrWhiteSpace(returnUrl)) return fallback;
    if (!returnUrl.StartsWith('/')) return fallback;
    if (returnUrl.StartsWith("//", StringComparison.Ordinal)) return fallback;
    if (returnUrl.Contains("://", StringComparison.Ordinal)) return fallback;
    return returnUrl;
}

// ======================
// Auth endpoints (Blazor forms -> minimal API)
// IMPORTANT : si tu as encore un AccountController MVC, supprime-le,
// sinon tu auras 2 endpoints /Account/Login en concurrence.
// ======================
var account = app.MapGroup("/Account");

account.MapPost("/Login", async (
        [FromForm] string Email,
        [FromForm] string Password,
        [FromForm] bool? RememberMe,
        [FromForm] string? ReturnUrl,
        SignInManager<IdentityUser> signInManager) =>
{
    if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
    {
        return Results.Redirect($"/Account/Login?error=required&returnUrl={Uri.EscapeDataString(ReturnUrl ?? "/dashboard")}");
    }

    var remember = RememberMe ?? false;

    var result = await signInManager.PasswordSignInAsync(
        Email, Password, remember, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        return Results.Redirect(SafeReturnUrl(ReturnUrl));
    }

    return Results.Redirect($"/Account/Login?error=invalid&returnUrl={Uri.EscapeDataString(ReturnUrl ?? "/dashboard")}&email={Uri.EscapeDataString(Email)}");
})
    .AllowAnonymous()
    .DisableAntiforgery();

account.MapPost("/Register", async (
        [FromForm] string Email,
        [FromForm] string Password,
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager) =>
{
    if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
    {
        return Results.Redirect("/Account/Register?error=required");
    }

    var user = new IdentityUser(Email) { Email = Email };
    var create = await userManager.CreateAsync(user, Password);

    if (!create.Succeeded)
    {
        return Results.Redirect("/Account/Register?error=failed");
    }

    await signInManager.SignInAsync(user, isPersistent: false);
    return Results.Redirect("/dashboard");
})
    .AllowAnonymous()
    .DisableAntiforgery();

account.MapPost("/Logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
})
    .RequireAuthorization()
    .DisableAntiforgery();

// ======================
// Endpoints
// ======================
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// ======================
// API: AIChessLogs ingest
// ======================
var aiChessLogsApi = app.MapGroup("/api/aichesslogs").AllowAnonymous();

aiChessLogsApi.MapPost("", async (
        AIChessLogCreateRequest request,
        IAIChessLogService logService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("AIChessLogsApi");

    if (request is null)
    {
        return Results.BadRequest();
    }

    var entity = new AIChessLogs
    {
        TimestampUtc = DateTime.UtcNow,
        Type = string.IsNullOrWhiteSpace(request.Type) ? "information" : request.Type.Trim(),

        SearchDepth = request.SearchDepth,
        DurationMs = request.DurationMs,
        LegalMovesCount = request.LegalMovesCount,
        EvaluatedMovesCount = request.EvaluatedMovesCount,

        BestMoveUci = request.BestMoveUci,
        BestScoreCp = request.BestScoreCp,

        GeneratedMovesTotal = request.GeneratedMovesTotal,
        NodesVisited = request.NodesVisited,
        LeafEvaluations = request.LeafEvaluations,

        EvaluatedMovesJson = request.EvaluatedMovesJson
    };

    try
    {
        int id = await logService.AddAsync(entity, cancellationToken);

        logger.LogInformation(
            "Stored AIChessLog Id={Id} Depth={Depth} DurMs={DurMs} Legal={Legal} Eval={Eval} Nodes={Nodes} Leaf={Leaf} Best={Best} Score={Score}",
            id, entity.SearchDepth, entity.DurationMs, entity.LegalMovesCount, entity.EvaluatedMovesCount,
            entity.NodesVisited, entity.LeafEvaluations, entity.BestMoveUci, entity.BestScoreCp);

        return Results.Created($"/api/aichesslogs/{id}", new { id });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "StoreFailed");
        return Results.Problem("Erreur interne", statusCode: 500);
    }
})
    .Accepts<AIChessLogCreateRequest>("application/json")
    .Produces(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);

// Home -> CV
app.MapGet("/", () => Results.Redirect("/cv"));

// ======================
// Migrations + Seed (optionnel)
// ======================
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = sp.GetRequiredService<BlogContext>();
        await db.Database.MigrateAsync();

        // Seed rôles
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };
        foreach (var roleName in roles)
        {
            if (!await roleMgr.RoleExistsAsync(roleName))
            {
                await roleMgr.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed admin depuis la config (User Secrets / env vars), PAS en clair dans le code
        // Config attendue :
        // SeedAdmin:Enabled = true/false
        // SeedAdmin:Email
        // SeedAdmin:Password
        var seedEnabled = app.Configuration.GetValue<bool>("SeedAdmin:Enabled");
        if (seedEnabled)
        {
            var adminEmail = app.Configuration["SeedAdmin:Email"];
            var adminPassword = app.Configuration["SeedAdmin:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("SeedAdmin enabled but Email/Password not configured. Skipping admin seed.");
            }
            else
            {
                var admin = await userMgr.FindByEmailAsync(adminEmail);
                if (admin == null)
                {
                    admin = new IdentityUser(adminEmail) { Email = adminEmail };
                    var createResult = await userMgr.CreateAsync(admin, adminPassword);

                    if (!createResult.Succeeded)
                    {
                        logger.LogError("Seed admin failed: {Errors}",
                            string.Join(" | ", createResult.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        await userMgr.AddToRoleAsync(admin, "Admin");
                        logger.LogInformation("Seed admin created: {Email}", adminEmail);
                    }
                }
                else
                {
                    // Si le user existe déjà, on s'assure juste qu'il est Admin
                    if (!await userMgr.IsInRoleAsync(admin, "Admin"))
                    {
                        await userMgr.AddToRoleAsync(admin, "Admin");
                        logger.LogInformation("Seed admin role added to existing user: {Email}", adminEmail);
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur au démarrage");
    }
}

app.Run();
