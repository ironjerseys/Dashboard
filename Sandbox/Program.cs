using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sandbox.Data;
using Sandbox.Models;
using Sandbox.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) DbContext SQLite + Identity avec UI par défaut
builder.Services.AddDbContext<BlogContext>(opt =>
    opt.UseSqlite("Data Source=blog.db"));

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
    })
    .AddEntityFrameworkStores<BlogContext>()
    .AddDefaultTokenProviders();


// 2) Cookie auth pour redirections login/access denied
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Identity/Account/Login";
    opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 3) Authorization policies (CanManageAll / CanManageOwn)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanManageAll", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("CanManageOwn", policy =>
        policy.RequireAssertion(ctx =>
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var article = (Article)ctx.Resource!;
            return ctx.User.IsInRole("Admin")
                || article.AuthorId == userId;
        }));
});

builder.Services.AddScoped<IArticleService, ArticleService>();

// 4) MVC + Razor Pages
builder.Services.AddControllersWithViews();

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

app.UseAuthentication();
app.UseAuthorization();

// 6) Endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// 7) Seed roles et admin
using (var scope = app.Services.CreateScope())
{
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

app.Run();
