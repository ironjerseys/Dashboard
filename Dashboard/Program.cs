using Dashboard.Components;
using Dashboard.Data;
using Dashboard.Entities;
using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
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
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();


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
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur au démarrage");
    }
}

app.Run();