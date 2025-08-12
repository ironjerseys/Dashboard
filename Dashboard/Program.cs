using Dashboard.Data;
using Dashboard.Models;
using Dashboard.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ✅ Chemin DB: /home/site/data en Azure, App_Data en local
string dbDir;
var home = Environment.GetEnvironmentVariable("HOME"); // Azure: D:\home ou /home
if (!string.IsNullOrEmpty(home))
    dbDir = Path.Combine(home, "site", "data");
else
    dbDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dbDir);
var dbPath = Path.Combine(dbDir, "blog.db");

// 1) DbContext SQLite + Identity
builder.Services.AddDbContext<BlogContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}")); // ✅ on n’utilise plus "blog.db" à la racine

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options => { })
    .AddEntityFrameworkStores<BlogContext>()
    .AddDefaultTokenProviders();

// 2) Cookie auth
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Identity/Account/Login";
    opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 3) Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageAll", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CanManageOwn", policy => policy.RequireAssertion(ctx =>
    {
        var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var article = (Article)ctx.Resource!;
        return ctx.User.IsInRole("Admin") || article.AuthorId == userId;
    }));
});

// Services
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IQuizService, QuizService>();

// 4) MVC + Session
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

// 7) Migrations + seed (roles/admin)
// ✅ On migre d’abord la base au chemin Azure, puis on seed.
// ✅ Optionnel: copie d’une DB “seed” packagée si la DB n’existe pas encore.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var db = scope.ServiceProvider.GetRequiredService<BlogContext>();

        // (Optionnel) copier une DB packagée au premier démarrage
        // Place un fichier 'App_Data/seed-blog.db' dans le projet (Build Action: Content, Copy Always)
        var packagedSeed = Path.Combine(env.ContentRootPath, "App_Data", "seed-blog.db");
        if (!File.Exists(dbPath) && File.Exists(packagedSeed))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            File.Copy(packagedSeed, dbPath);
        }

        await db.Database.MigrateAsync(); // ✅ crée/maj schema dans /home/site/data/blog.db

        // Seed rôles/admin
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

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
        app.Logger.LogError(ex, "Erreur au démarrage (migration/seed)");
        // en prod on log; en dev tu peux rethrow si tu veux
    }
}