using Dashboard.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Data;

public class BlogContext : IdentityDbContext<IdentityUser>
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Goal>(e =>
        {
            e.Property(g => g.OwnerId).HasMaxLength(450);
            e.HasOne(g => g.Article)
             .WithMany()
             .HasForeignKey(g => g.ArticleId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(g => new { g.OwnerId, g.WeekStart });
        });
    }
}