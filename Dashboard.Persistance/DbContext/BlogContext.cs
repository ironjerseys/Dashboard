using Dashboard.Persistance.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Label = Dashboard.Persistance.Entities.Label;

namespace Dashboard.Persistance.DbContext;

public class BlogContext : IdentityDbContext<IdentityUser>
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Log> Logs => Set<Log>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<QuestionTechnique> QuizQuestions => Set<QuestionTechnique>();
    public DbSet<LeitnerCard> LeitnerCards => Set<LeitnerCard>();
    public DbSet<LeitnerReview> LeitnerReviews => Set<LeitnerReview>();
    public DbSet<CodeChallengeCard> CodeChallengeCards => Set<CodeChallengeCard>();
    public DbSet<SqlChallenge> SqlChallenges => Set<SqlChallenge>();
    public DbSet<InactiveChallenge> InactiveChallenges => Set<InactiveChallenge>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<MechanicJobPosting> MechanicJobPostings => Set<MechanicJobPosting>();
    public DbSet<ScraperRole> ScraperRoles => Set<ScraperRole>();
    public DbSet<HiddenJobChart> HiddenJobCharts => Set<HiddenJobChart>();
    public DbSet<ReminderSetting> ReminderSettings => Set<ReminderSetting>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<MailboxSyncState> MailboxSyncStates => Set<MailboxSyncState>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();


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

        // Un label (theme) regroupe plusieurs questions, une question peut avoir plusieurs labels.
        builder.Entity<QuestionTechnique>()
            .HasMany(q => q.Labels)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "QuestionLabels",
                right => right.HasOne<Label>().WithMany().HasForeignKey("LabelId").OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne<QuestionTechnique>().WithMany().HasForeignKey("QuestionId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("QuestionId", "LabelId"));

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

        // Supprimer un défi SQL supprime les cartes de révision et l'état "désactivé" qui le concernent.
        builder.Entity<CodeChallengeCard>()
            .HasOne<SqlChallenge>()
            .WithMany()
            .HasForeignKey(card => card.SqlChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<InactiveChallenge>()
            .HasOne<SqlChallenge>()
            .WithMany()
            .HasForeignKey(i => i.SqlChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MediaAsset>().HasIndex(x => x.CreatedUtc);

        builder.Entity<ScraperRole>().HasIndex(r => r.SearchTerm).IsUnique();

        builder.Entity<ReminderSetting>()
            .HasIndex(r => r.OwnerId)
            .IsUnique();

        builder.Entity<EmailMessage>(e =>
        {
            // Cle de deduplication : un meme mail ne doit jamais entrer deux fois.
            e.HasIndex(m => m.MessageId).IsUnique();
            e.HasIndex(m => m.SentUtc);
            e.HasIndex(m => m.FromAddress);

            // Enums en texte plutot qu'en entier : la table reste lisible en SQL et l'ordre
            // des enums peut changer sans corrompre les lignes.
            e.Property(m => m.AnalysisState).HasConversion<string>().HasMaxLength(16);
            e.Property(m => m.EventType).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(m => m.AnalysisState);
            e.HasIndex(m => m.NeedsReview);
            e.HasIndex(m => m.LabelSyncPending);

            // Supprimer une candidature ne doit pas effacer les mails : ils redeviennent orphelins.
            e.HasOne(m => m.JobApplication)
                .WithMany(a => a.Emails)
                .HasForeignKey(m => m.JobApplicationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<JobApplication>(e =>
        {
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(a => a.Company);
            e.HasIndex(a => a.Status);
        });

        builder.Entity<AiUsageRecord>(e =>
        {
            e.Property(u => u.EstimatedCostUsd).HasPrecision(18, 6);
            e.HasIndex(u => u.TimestampUtc);
        });

        builder.Entity<MailboxSyncState>()
            .HasIndex(s => new { s.Account, s.Folder })
            .IsUnique();

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

        builder.Entity<MechanicJobPosting>(e =>
        {
            // 800 caracteres : reste sous la limite de 1700 octets d'une cle d'index SQL Server.
            e.HasIndex(j => j.Url).IsUnique();
            e.HasIndex(j => j.LastSeenAt);
            e.Property(j => j.Source).HasMaxLength(32);
            e.Property(j => j.ExternalId).HasMaxLength(64);
            e.Property(j => j.Url).HasMaxLength(800);
            e.Property(j => j.Title).HasMaxLength(512);
            e.Property(j => j.Company).HasMaxLength(256);
            e.Property(j => j.City).HasMaxLength(128);
            e.Property(j => j.Salary).HasMaxLength(128);
            e.Property(j => j.SearchTerm).HasMaxLength(128);
        });
    }
}