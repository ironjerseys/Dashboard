using Dashboard.Data;
using Dashboard.Entities;
using Dashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ========= DB PATH (Azure vs Local) =========
string dataDir;
var home = Environment.GetEnvironmentVariable("HOME"); // Azure: D:\home ou /home
if (!string.IsNullOrEmpty(home))
    dataDir = Path.Combine(home, "site", "data");       // Emplacement écrivable sur App Service
else
    dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data"); // Local
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "blog.db");
// ============================================

// 1) 
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BlogContext>(opt => opt.UseSqlServer(cs));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options => { })
    .AddEntityFrameworkStores<BlogContext>()
    .AddDefaultTokenProviders();

// 2) Cookie auth
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Account/Login";
    opts.AccessDeniedPath = "/Account/AccessDenied";
});

// 3) Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageAll", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("CanManageOwn", policy =>
        policy.RequireAssertion(ctx =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var article = (Article)ctx.Resource!;
            return ctx.User.IsInRole("Admin") || article.AuthorId == userId;
        }));
});

// Services
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IDbQuizService, DbQuizService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IAIChessLogService, AIChessLogService>();

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddHostedService<GoalReminderService>();

// 4) MVC + Session + API controllers
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

var app = builder.Build();

// 5) Middleware
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

// 6) Endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

// 7) Migrations + seed rôles/admin
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = sp.GetRequiredService<BlogContext>();
        await db.Database.MigrateAsync();

        // Seed rôles & admin
        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };
        foreach (var r in roles)
            if (!await roleMgr.RoleExistsAsync(r))
                await roleMgr.CreateAsync(new IdentityRole(r));

        var admin = await userMgr.FindByEmailAsync("admin@example.com");
        if (admin == null)
        {
            admin = new IdentityUser("admin@example.com") { Email = "admin@example.com" };
            await userMgr.CreateAsync(admin, "MotDePasse1!");
            await userMgr.AddToRoleAsync(admin, "Admin");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erreur au démarrage (migration/seed)");
    }
}

app.Run();
