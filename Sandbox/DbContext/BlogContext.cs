using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sandbox.Models;

namespace Sandbox.Data;

public class BlogContext : IdentityDbContext<IdentityUser>
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Article> Articles => Set<Article>();
}