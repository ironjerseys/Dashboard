using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dashboard.Business;

public sealed record AnalysisRunResult(
    int Analyzed,
    int Failed,
    int ApplicationsCreated,
    int NeedsReview,
    int Remaining,
    string? StoppedReason);

public sealed record AiSpendSummary(
    bool Configured,
    string Model,
    decimal MonthCostUsd,
    decimal MonthlyBudgetUsd,
    int CallsToday,
    int MaxCallsPerDay);

public interface IEmailAnalysisService
{
    /// <summary>
    /// Analyse les mails en attente, du plus ancien au plus recent, dans la limite des plafonds,
    /// et les rattache aux candidatures.
    /// </summary>
    Task<AnalysisRunResult> AnalyzePendingAsync(CancellationToken cancellationToken = default);

    Task<AiSpendSummary> GetSpendAsync(CancellationToken cancellationToken = default);
}

public sealed class EmailAnalysisService : IEmailAnalysisService
{
    private const int MaxConsecutiveFailures = 3;
    private const string Purpose = "EmailClassification";

    private readonly IDbContextFactory<BlogContext> _dbContextFactory;
    private readonly IEmailClassifier _classifier;
    private readonly AnthropicOptions _options;

    public EmailAnalysisService(
        IDbContextFactory<BlogContext> dbContextFactory,
        IEmailClassifier classifier,
        IOptions<AnthropicOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _classifier = classifier;
        _options = options.Value;
    }

    public async Task<AnalysisRunResult> AnalyzePendingAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await RepairJobBoardEmailsAsync(dbContext, cancellationToken);

        if (!_options.IsConfigured)
        {
            int waiting = await CountPendingAsync(dbContext, cancellationToken);
            return new AnalysisRunResult(0, 0, 0, 0, waiting, "Cle Anthropic absente (variable Anthropic__Key) : analyse desactivee.");
        }

        // Chronologique : la confirmation d'une candidature doit passer avant le refus qui la suit.
        List<int> ids = await dbContext.EmailMessages
            .Where(m => m.AnalysisState == EmailAnalysisState.Pending)
            .OrderBy(m => m.SentUtc).ThenBy(m => m.Id)
            .Select(m => m.Id)
            .Take(_options.MaxAnalysesPerRun)
            .ToListAsync(cancellationToken);

        int analyzed = 0, failed = 0, created = 0, review = 0, consecutiveFailures = 0;
        string? stoppedReason = null;

        foreach (int id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            stoppedReason = await CheckLimitsAsync(dbContext, cancellationToken);
            if (stoppedReason is not null)
            {
                break;
            }

            EmailMessage email = await dbContext.EmailMessages.FirstAsync(m => m.Id == id, cancellationToken);
            email.AnalysisAttempts++;

            try
            {
                var (classification, usage) = await _classifier.ClassifyAsync(
                    new EmailToClassify(email.FromAddress, email.FromName, email.Subject, email.SentUtc, EmailText.BodyOf(email)),
                    cancellationToken);

                RecordUsage(dbContext, usage, email.Id);

                classification = JobBoardEmailParser.Refine(email, classification);

                MatchDecision decision = await ApplyClassificationAsync(dbContext, email, classification, cancellationToken);
                created += decision == MatchDecision.Create ? 1 : 0;
                review += decision == MatchDecision.Review ? 1 : 0;

                email.AnalysisState = EmailAnalysisState.Analyzed;
                email.AnalyzedUtc = DateTime.UtcNow;
                email.AnalysisError = null;
                email.LabelSyncPending = true;

                analyzed++;
                consecutiveFailures = 0;
            }
            catch (EmailClassificationException ex)
            {
                if (ex.Usage is not null)
                {
                    RecordUsage(dbContext, ex.Usage, email.Id);
                }

                email.AnalysisError = Truncate(ex.Message, 1024);

                if (email.AnalysisAttempts >= _options.MaxAttemptsPerEmail)
                {
                    email.AnalysisState = EmailAnalysisState.Failed;
                }

                failed++;
                consecutiveFailures++;
            }
            catch (AiServiceStoppedException ex)
            {
                // Le mail n'y est pour rien : la tentative ne compte pas.
                email.AnalysisAttempts--;
                stoppedReason = ex.Message;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (stoppedReason is not null)
            {
                break;
            }

            if (consecutiveFailures >= MaxConsecutiveFailures)
            {
                // Un probleme systematique (format, modele, reseau) ferait echouer tous les mails suivants.
                stoppedReason = $"{MaxConsecutiveFailures} echecs d'affilee : analyse suspendue jusqu'au prochain passage.";
                break;
            }
        }

        int remaining = await CountPendingAsync(dbContext, cancellationToken);

        var result = new AnalysisRunResult(analyzed, failed, created, review, remaining, stoppedReason);

        dbContext.Logs.Add(new Log
        {
            Level = stoppedReason is null && failed == 0 ? "Info" : "Warning",
            Source = nameof(EmailAnalysisService),
            Event = "Analyze",
            Message = $"Analyzed={analyzed}; Failed={failed}; Created={created}; Review={review}; " +
                      $"Remaining={remaining}; Stopped={stoppedReason}",
            TimestampUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<AiSpendSummary> GetSpendAsync(CancellationToken cancellationToken = default)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var (monthStart, dayStart) = Periods();

        decimal monthCost = await dbContext.AiUsageRecords
            .Where(u => u.TimestampUtc >= monthStart)
            .SumAsync(u => (decimal?)u.EstimatedCostUsd, cancellationToken) ?? 0m;

        int callsToday = await dbContext.AiUsageRecords
            .CountAsync(u => u.TimestampUtc >= dayStart, cancellationToken);

        return new AiSpendSummary(_options.IsConfigured, _options.Model, monthCost, _options.MonthlyBudgetUsd, callsToday, _options.MaxCallsPerDay);
    }

    private async Task<string?> CheckLimitsAsync(BlogContext dbContext, CancellationToken cancellationToken)
    {
        var (monthStart, dayStart) = Periods();

        decimal monthCost = await dbContext.AiUsageRecords
            .Where(u => u.TimestampUtc >= monthStart)
            .SumAsync(u => (decimal?)u.EstimatedCostUsd, cancellationToken) ?? 0m;

        if (monthCost >= _options.MonthlyBudgetUsd)
        {
            return $"Budget du mois atteint ({monthCost:0.00} $ / {_options.MonthlyBudgetUsd:0.00} $).";
        }

        int callsToday = await dbContext.AiUsageRecords
            .CountAsync(u => u.TimestampUtc >= dayStart, cancellationToken);

        return callsToday >= _options.MaxCallsPerDay
            ? $"Plafond du jour atteint ({callsToday} appels)."
            : null;
    }

    /// <summary>
    /// Mails Indeed analyses avant <see cref="JobBoardEmailParser"/> : l'entreprise manquait ou etait "Indeed".
    /// On la relit dans le corps, sans appel a Claude, et on corrige le rattachement. Idempotent.
    /// </summary>
    private static async Task RepairJobBoardEmailsAsync(BlogContext dbContext, CancellationToken cancellationToken)
    {
        List<EmailMessage> emails = await dbContext.EmailMessages
            .Where(m => m.AnalysisState == EmailAnalysisState.Analyzed && m.FromAddress.EndsWith("indeed.com"))
            .OrderBy(m => m.SentUtc).ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        int repaired = 0;

        foreach (EmailMessage email in emails)
        {
            if (JobBoardEmailParser.TryParse(email) is not { } facts)
            {
                continue;
            }

            string? wrongCompany = email.ExtractedCompany;

            bool companyWrong = wrongCompany is null || !JobApplicationMatcher.SameCompany(wrongCompany, facts.Company);

            // Le lieu d'une confirmation Indeed n'est que dans le corps : quand Claude ne l'a pas lu,
            // le parser le donne gratuitement. Reservee aux mails deja rattaches, cette reparation ne
            // touche que des champs vides ; relancer le rattachement rouvrirait des decisions tranchees.
            bool locationMissing = facts.Location is not null
                                   && email.ExtractedLocation is null
                                   && email.JobApplicationId is not null;

            if (!companyWrong && !locationMissing)
            {
                continue;
            }

            if (email.JobApplicationId is { } applicationId)
            {
                // Rattache (souvent a la main, avec "Indeed Apply" comme entreprise) : on renomme la candidature.
                JobApplication application = await dbContext.JobApplications.FirstAsync(a => a.Id == applicationId, cancellationToken);

                if (companyWrong)
                {
                    if (application.Company.Contains("indeed", StringComparison.OrdinalIgnoreCase)
                        || (wrongCompany is not null && JobApplicationMatcher.SameCompany(application.Company, wrongCompany)))
                    {
                        application.Company = Truncate(facts.Company, 256)!;
                    }

                    email.ExtractedCompany = Truncate(facts.Company, 256);
                }

                email.ExtractedLocation = Truncate(facts.Location, 256) ?? email.ExtractedLocation;
                application.Location ??= email.ExtractedLocation;
            }
            else
            {
                var classification = JobBoardEmailParser.Refine(email, new EmailClassification(
                    email.EventType ?? EmailEventType.ApplicationReceived,
                    email.ExtractedCompany,
                    email.ExtractedPosition,
                    email.ExtractedLocation,
                    email.AnalysisSummary ?? ""));

                await ApplyClassificationAsync(dbContext, email, classification, cancellationToken);
            }

            // Chaque correction est enregistree de suite : le rattachement suivant doit voir la candidature creee.
            await dbContext.SaveChangesAsync(cancellationToken);
            repaired++;
        }

        if (repaired > 0)
        {
            dbContext.Logs.Add(new Log
            {
                Level = "Info",
                Source = nameof(EmailAnalysisService),
                Event = "RepairJobBoard",
                Message = $"Repaired={repaired}",
                TimestampUtc = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<MatchDecision> ApplyClassificationAsync(
        BlogContext dbContext,
        EmailMessage email,
        EmailClassification classification,
        CancellationToken cancellationToken)
    {
        email.EventType = classification.EventType;
        email.ExtractedCompany = Truncate(classification.Company, 256);
        email.ExtractedPosition = Truncate(classification.Position, 256);
        email.ExtractedLocation = Truncate(classification.Location, 256);
        email.AnalysisSummary = Truncate(classification.Summary, 512);
        email.NeedsReview = false;

        // Deja rattache a la main (avant l'analyse ou avant un "Reessayer") : le choix manuel prime.
        if (email.JobApplicationId is { } manualId)
        {
            JobApplication manual = await dbContext.JobApplications.FirstAsync(a => a.Id == manualId, cancellationToken);
            JobApplicationMatcher.ApplyEmail(manual, email);
            return MatchDecision.Link;
        }

        List<ApplicationCandidate> candidates = await dbContext.JobApplications
            .AsNoTracking()
            .Select(a => new ApplicationCandidate(a.Id, a.Company, a.Position, a.Status, a.LastEventUtc))
            .ToListAsync(cancellationToken);

        MatchResult match = JobApplicationMatcher.Match(classification, email.SentUtc, candidates);

        switch (match.Decision)
        {
            case MatchDecision.Link:
                JobApplication existing = await dbContext.JobApplications.FirstAsync(a => a.Id == match.ApplicationId, cancellationToken);
                email.JobApplicationId = existing.Id;
                JobApplicationMatcher.ApplyEmail(existing, email);
                break;

            case MatchDecision.Create:
                var application = new JobApplication
                {
                    Company = email.ExtractedCompany!,
                    LastEventUtc = email.SentUtc
                };
                JobApplicationMatcher.ApplyEmail(application, email);
                application.Status = JobApplicationMatcher.StatusFor(classification.EventType) ?? JobApplicationStatus.Applied;
                dbContext.JobApplications.Add(application);
                email.JobApplication = application;
                break;

            case MatchDecision.Review:
                email.NeedsReview = true;
                break;
        }

        return match.Decision;
    }

    private void RecordUsage(BlogContext dbContext, AiCallUsage usage, int emailId)
    {
        decimal inputPrice = _options.InputPricePerMTok / 1_000_000m;
        decimal outputPrice = _options.OutputPricePerMTok / 1_000_000m;

        // Ecriture en cache facturee 1,25x l'entree, lecture 0,1x.
        decimal cost = usage.InputTokens * inputPrice
                       + usage.CacheCreationInputTokens * inputPrice * 1.25m
                       + usage.CacheReadInputTokens * inputPrice * 0.1m
                       + usage.OutputTokens * outputPrice;

        dbContext.AiUsageRecords.Add(new AiUsageRecord
        {
            Model = _options.Model,
            Purpose = Purpose,
            EmailMessageId = emailId,
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            CacheCreationInputTokens = usage.CacheCreationInputTokens,
            CacheReadInputTokens = usage.CacheReadInputTokens,
            EstimatedCostUsd = cost
        });
    }

    private static Task<int> CountPendingAsync(BlogContext dbContext, CancellationToken cancellationToken) =>
        dbContext.EmailMessages.CountAsync(m => m.AnalysisState == EmailAnalysisState.Pending, cancellationToken);

    private static (DateTime MonthStart, DateTime DayStart) Periods()
    {
        DateTime now = DateTime.UtcNow;
        return (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), now.Date);
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
