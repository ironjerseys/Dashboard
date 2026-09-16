using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public sealed record ApplicationRow(
    int Id,
    string Company,
    string? Position,
    string? Location,
    JobApplicationStatus Status,
    DateTime? AppliedUtc,
    DateTime LastEventUtc,
    int EmailCount,
    string? JobUrl,
    string? Notes)
{
    public JobRegion Region { get; } = JobLocation.Resolve(Location);
}

public interface IJobApplicationService
{
    /// <summary>
    /// Toutes les candidatures : quelques centaines au plus, la page filtre, trie et pagine en memoire
    /// pour que les statistiques par region suivent les filtres sans requete de plus.
    /// </summary>
    Task<List<ApplicationRow>> GetApplicationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Mails rattaches, du plus recent au plus ancien, sans les corps.</summary>
    Task<List<EmailMessage>> GetEmailsAsync(int applicationId, CancellationToken cancellationToken = default);

    /// <summary>Mails dont le rattachement automatique a hesite.</summary>
    Task<List<EmailMessage>> GetReviewQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Mails rattaches a aucune candidature et absents de la file de verification : hors candidature,
    /// mises a jour orphelines, en attente ou en echec d'analyse. C'est la que se cache un refus mal classe.
    /// </summary>
    Task<List<EmailMessage>> GetUntrackedAsync(CancellationToken cancellationToken = default);

    Task<List<ApplicationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default);

    Task LinkEmailAsync(int emailId, int applicationId, CancellationToken cancellationToken = default);

    Task CreateFromEmailAsync(int emailId, CancellationToken cancellationToken = default);

    /// <summary>Le mail ne concerne aucune candidature : on le retire de la file sans le rattacher.</summary>
    Task DismissReviewAsync(int emailId, CancellationToken cancellationToken = default);

    /// <summary>Correction manuelle quand l'analyse s'est trompee.</summary>
    Task SetStatusAsync(int applicationId, JobApplicationStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Doublon (ex. "AutoTrader.ca" et "Trader Corporation") : les mails et les informations de
    /// <paramref name="sourceId"/> passent dans <paramref name="targetId"/>, puis la source est supprimee.
    /// </summary>
    Task MergeAsync(int sourceId, int targetId, CancellationToken cancellationToken = default);
}

public sealed class JobApplicationService : IJobApplicationService
{
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public JobApplicationService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<ApplicationRow>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.JobApplications
            .AsNoTracking()
            .OrderByDescending(a => a.LastEventUtc)
            .Select(a => new ApplicationRow(
                a.Id, a.Company, a.Position, a.Location, a.Status, a.AppliedUtc, a.LastEventUtc, a.Emails.Count, a.JobUrl, a.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EmailMessage>> GetEmailsAsync(int applicationId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await WithoutBodies(dbContext.EmailMessages.Where(m => m.JobApplicationId == applicationId))
            .OrderByDescending(m => m.SentUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EmailMessage>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await WithoutBodies(dbContext.EmailMessages.Where(m => m.NeedsReview))
            .OrderBy(m => m.SentUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EmailMessage>> GetUntrackedAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await WithoutBodies(dbContext.EmailMessages.Where(m => m.JobApplicationId == null && !m.NeedsReview))
            .OrderByDescending(m => m.SentUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ApplicationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.JobApplications
            .AsNoTracking()
            .OrderBy(a => a.Company).ThenBy(a => a.Position)
            .Select(a => new ApplicationCandidate(a.Id, a.Company, a.Position, a.Status, a.LastEventUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task LinkEmailAsync(int emailId, int applicationId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmailMessage email = await dbContext.EmailMessages.FirstAsync(m => m.Id == emailId, cancellationToken);
        JobApplication application = await dbContext.JobApplications.FirstAsync(a => a.Id == applicationId, cancellationToken);

        MarkAsApplicationEmail(email);
        email.JobApplicationId = application.Id;
        email.NeedsReview = false;
        JobApplicationMatcher.ApplyEmail(application, email);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateFromEmailAsync(int emailId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        EmailMessage email = await dbContext.EmailMessages.FirstAsync(m => m.Id == emailId, cancellationToken);
        MarkAsApplicationEmail(email);

        var application = new JobApplication
        {
            Company = email.ExtractedCompany ?? email.FromName ?? email.FromAddress,
            LastEventUtc = email.SentUtc
        };

        JobApplicationMatcher.ApplyEmail(application, email);

        if (email.EventType is { } eventType && JobApplicationMatcher.StatusFor(eventType) is { } status)
        {
            application.Status = status;
        }

        dbContext.JobApplications.Add(application);
        email.JobApplication = application;
        email.NeedsReview = false;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissReviewAsync(int emailId, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.EmailMessages
            .Where(m => m.Id == emailId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.NeedsReview, false), cancellationToken);
    }

    public async Task SetStatusAsync(int applicationId, JobApplicationStatus status, CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.JobApplications
            .Where(a => a.Id == applicationId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status), cancellationToken);
    }

    public async Task MergeAsync(int sourceId, int targetId, CancellationToken cancellationToken = default)
    {
        if (sourceId == targetId)
        {
            return;
        }

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        JobApplication source = await dbContext.JobApplications
            .Include(a => a.Emails)
            .FirstAsync(a => a.Id == sourceId, cancellationToken);
        JobApplication target = await dbContext.JobApplications.FirstAsync(a => a.Id == targetId, cancellationToken);

        foreach (EmailMessage email in source.Emails)
        {
            email.JobApplicationId = target.Id;
        }

        target.Position ??= source.Position;
        target.Location ??= source.Location;
        target.JobUrl ??= source.JobUrl;

        if (source.AppliedUtc is not null && (target.AppliedUtc is null || source.AppliedUtc < target.AppliedUtc))
        {
            target.AppliedUtc = source.AppliedUtc;
        }

        // Une issue connue (refus, entretien...) l'emporte sur "envoyee" ; entre deux issues, la plus recente.
        bool sourceKnowsMore = target.Status == JobApplicationStatus.Applied && source.Status != JobApplicationStatus.Applied;
        bool sourceIsNewer = source.Status != JobApplicationStatus.Applied && source.LastEventUtc > target.LastEventUtc;
        if (sourceKnowsMore || sourceIsNewer)
        {
            target.Status = source.Status;
        }

        if (source.LastEventUtc > target.LastEventUtc)
        {
            target.LastEventUtc = source.LastEventUtc;
        }

        if (source.Notes is not null)
        {
            string notes = target.Notes is null ? source.Notes : target.Notes + "\n" + source.Notes;
            target.Notes = notes.Length <= 2000 ? notes : notes[..2000];
        }

        dbContext.JobApplications.Remove(source);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Rattacher a la main un mail classe hors candidature, c'est dire que l'analyse s'est trompee :
    /// il devient une mise a jour, pour ne plus s'afficher comme hors candidature.
    /// </summary>
    private static void MarkAsApplicationEmail(EmailMessage email)
    {
        if (email.EventType is null or EmailEventType.NotApplication)
        {
            email.EventType = EmailEventType.OtherUpdate;
        }
    }

    private static IQueryable<EmailMessage> WithoutBodies(IQueryable<EmailMessage> query) =>
        query.AsNoTracking().Select(m => new EmailMessage
        {
            Id = m.Id,
            MessageId = m.MessageId,
            FromAddress = m.FromAddress,
            FromName = m.FromName,
            Subject = m.Subject,
            SentUtc = m.SentUtc,
            AnalysisState = m.AnalysisState,
            AnalysisAttempts = m.AnalysisAttempts,
            AnalysisError = m.AnalysisError,
            EventType = m.EventType,
            ExtractedCompany = m.ExtractedCompany,
            ExtractedPosition = m.ExtractedPosition,
            AnalysisSummary = m.AnalysisSummary,
            NeedsReview = m.NeedsReview,
            JobApplicationId = m.JobApplicationId
        });
}
