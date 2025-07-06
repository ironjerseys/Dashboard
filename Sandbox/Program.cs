using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sandbox.Data;
using Sandbox.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddDbContext<BlogContext>(opt =>
    opt.UseSqlite("Data Source=blog.db"));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<BlogContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts => {
    opts.LoginPath = "/Account/Login";
    opts.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddAuthorization(options => {
    options.AddPolicy("CanManageAll", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("CanManageOwn", policy =>
        policy.RequireAssertion(ctx => {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var article = (Article)ctx.Resource!;
            return ctx.User.IsInRole("Admin")
                   || article.AuthorId == userId;
        }));
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();



using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    string[] roles = { "Admin", "User" };
    foreach (var r in roles)
        if (!await roleMgr.RoleExistsAsync(r))
            await roleMgr.CreateAsync(new IdentityRole(r));

    // Crée un admin par défaut si nécessaire
    var admin = await userMgr.FindByEmailAsync("admin@example.com");
    if (admin == null)
    {
        admin = new IdentityUser("admin@example.com") { Email = "admin@example.com" };
        await userMgr.CreateAsync(admin, "MotDePasse1!");
        await userMgr.AddToRoleAsync(admin, "Admin");
    }
}


app.Run();