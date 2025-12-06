using Dashboard.Models;
using Dashboard.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Data;

public class BlogContext : IdentityDbContext<IdentityUser>
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<AIChessLogs> AIChessLogs => Set<AIChessLogs>();
    public DbSet<Label> Labels => Set<Label>();

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
            e.HasIndex(g => new { g.OwnerId, g.Debut, g.Fin });
        });

        builder.Entity<LogEntry>(e =>
        {
            e.HasIndex(l => l.TimestampUtc);
            e.HasIndex(l => l.Level);
            e.HasIndex(l => l.Source);
        });

        builder.Entity<AIChessLogs>(e =>
        {
            e.HasIndex(x => x.TimestampUtc);
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.BestMoveUci).HasMaxLength(16);
        });

        builder.Entity<Label>(e =>
        {
            e.HasIndex(l => l.Name).IsUnique();
            e.Property(l => l.Name).HasMaxLength(64).IsRequired();
        });

        // Many-to-many Article-Label
        builder.Entity<Article>()
               .HasMany(a => a.Labels)
               .WithMany();
    }
}