using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dashboard.Business;

public sealed record PipelineRunResult(
    DateTime StartedUtc,
    DateTime FinishedUtc,
    int Fetched,
    int Inserted,
    int MailRemaining,
    AnalysisRunResult? Analysis,
    LabelPushResult? Labels,
    IReadOnlyList<string> Errors,
    bool AlreadyRunning);

public interface IMailPipeline
{
    /// <summary>Synchro des mails, analyse, puis libelles Gmail. Un seul passage a la fois.</summary>
    Task<PipelineRunResult> RunAsync(CancellationToken cancellationToken = default);
}

public sealed class MailPipeline : IMailPipeline
{
    /// <summary>
    /// Statique : le worker et un clic sur la page utilisent chacun leur instance. Deux passages
    /// en parallele analyseraient les memes mails, et paieraient deux fois.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Dernier passage termine, pour l'affichage. En memoire : perdu au redemarrage.</summary>
    public static PipelineRunResult? LastRun { get; private set; }

    private readonly IMailIngestionService _ingestion;
    private readonly IEmailAnalysisService _analysis;
    private readonly IMailLabelService _labels;
    private readonly IDbContextFactory<BlogContext> _dbContextFactory;
    private readonly MailOptions _options;

    public MailPipeline(
        IMailIngestionService ingestion,
        IEmailAnalysisService analysis,
        IMailLabelService labels,
        IDbContextFactory<BlogContext> dbContextFactory,
        IOptions<MailOptions> options)
    {
        _ingestion = ingestion;
        _analysis = analysis;
        _labels = labels;
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
    }

    public static bool IsRunning => Gate.CurrentCount == 0;

    public async Task<PipelineRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        DateTime started = DateTime.UtcNow;

        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return new PipelineRunResult(started, started, 0, 0, 0, null, null, [], AlreadyRunning: true);
        }

        try
        {
            var errors = new List<string>();
            int fetched = 0, inserted = 0, mailRemaining = 0;
            AnalysisRunResult? analysis = null;
            LabelPushResult? labels = null;

            // Chaque etape tourne meme si la precedente echoue : une panne IMAP n'empeche pas
            // d'analyser les mails deja en base.
            try
            {
                for (int batch = 0; batch < Math.Max(1, _options.MaxSyncBatchesPerRun); batch++)
                {
                    MailIngestionResult sync = await _ingestion.SyncAsync(cancellationToken);
                    fetched += sync.Fetched;
                    inserted += sync.Inserted;
                    mailRemaining = sync.Remaining;

                    if (sync.Remaining == 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add("Synchro : " + ex.Message);
            }

            try
            {
                analysis = await _analysis.AnalyzePendingAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add("Analyse : " + ex.Message);
            }

            try
            {
                labels = await _labels.PushPendingLabelsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add("Libelles Gmail : " + ex.Message);
            }

            var result = new PipelineRunResult(
                started, DateTime.UtcNow, fetched, inserted, mailRemaining, analysis, labels, errors, AlreadyRunning: false);

            LastRun = result;

            if (errors.Count > 0)
            {
                await LogErrorsAsync(errors, cancellationToken);
            }

            return result;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task LogErrorsAsync(List<string> errors, CancellationToken cancellationToken)
    {
        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Logs.Add(new Log
        {
            Level = "Error",
            Source = nameof(MailPipeline),
            Event = "RunErrors",
            Message = string.Join(" | ", errors),
            TimestampUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
