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
    public DbSet<Log> Logs => Set<Log>();
    public DbSet<AIChessLogs> AIChessLogs => Set<AIChessLogs>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<QuestionTechnique> QuizQuestions => Set<QuestionTechnique>();
    public DbSet<LeitnerCard> LeitnerCards => Set<LeitnerCard>();
    public DbSet<LeitnerReview> LeitnerReviews => Set<LeitnerReview>();

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

        builder.Entity<Log>(e =>
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

        builder.Entity<Article>()
               .HasMany(a => a.Labels)
               .WithMany();

        builder.Entity<EmailSettings>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.RecipientEmail).HasMaxLength(256);
        });

        builder.Entity<QuestionTechnique>(e =>
        {
            e.HasOne(q => q.Article)
             .WithMany()
             .HasForeignKey(q => q.ArticleId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<LeitnerCard>()
            .HasIndex(card => new { card.OwnerId, card.QuizQuestionId })
            .IsUnique();

        builder.Entity<LeitnerCard>()
            .HasOne(card => card.Question)
            .WithMany()
            .HasForeignKey(card => card.QuizQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}