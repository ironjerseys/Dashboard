using Dashboard.Persistance.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Label = Dashboard.Persistance.Entities.Label;

namespace Dashboard.Persistance.DbContext;

public class BlogContext : IdentityDbContext<IdentityUser>
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Log> Logs => Set<Log>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<QuestionTechnique> QuizQuestions => Set<QuestionTechnique>();
    public DbSet<LeitnerCard> LeitnerCards => Set<LeitnerCard>();
    public DbSet<LeitnerReview> LeitnerReviews => Set<LeitnerReview>();
    public DbSet<CodeChallengeCard> CodeChallengeCards => Set<CodeChallengeCard>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Log>(e =>
        {
            e.HasIndex(l => l.TimestampUtc);
            e.HasIndex(l => l.Level);
            e.HasIndex(l => l.Source);
        });

        builder.Entity<Label>(e =>
        {
            e.HasIndex(l => l.Name).IsUnique();
            e.Property(l => l.Name).HasMaxLength(64).IsRequired();
        });

        builder.Entity<Article>()
            .HasMany(a => a.Labels)
            .WithMany();

        builder.Entity<Article>()
            .HasIndex(a => a.Slug)
            .IsUnique();

        builder.Entity<Article>()
            .HasOne(a => a.CoverMedia)
            .WithMany()
            .HasForeignKey(a => a.CoverMediaId)
            .OnDelete(DeleteBehavior.SetNull);


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

        builder.Entity<CodeChallengeCard>()
            .HasIndex(card => new { card.OwnerId, card.ChallengeKey })
            .IsUnique();

        builder.Entity<MediaAsset>().HasIndex(x => x.CreatedUtc);

        builder.Entity<JobPosting>(e =>
        {
            e.HasIndex(j => j.JobUrl).IsUnique();
            e.HasIndex(j => j.ScrapedAt);
            e.HasIndex(j => j.SearchRole);
            e.HasIndex(j => j.SearchCity);
            e.Property(j => j.Site).HasMaxLength(64);
            e.Property(j => j.JobUrl).HasMaxLength(2048);
            e.Property(j => j.JobUrlDirect).HasMaxLength(2048);
            e.Property(j => j.Title).HasMaxLength(512);
            e.Property(j => j.Company).HasMaxLength(256);
            e.Property(j => j.Location).HasMaxLength(256);
            e.Property(j => j.JobType).HasMaxLength(64);
            e.Property(j => j.Interval).HasMaxLength(32);
            e.Property(j => j.Currency).HasMaxLength(16);
            e.Property(j => j.JobLevel).HasMaxLength(128);
            e.Property(j => j.SearchRole).HasMaxLength(128);
            e.Property(j => j.SearchCity).HasMaxLength(128);
            e.Property(j => j.MinAmount).HasColumnType("decimal(18,2)");
            e.Property(j => j.MaxAmount).HasColumnType("decimal(18,2)");
        });
    }
}