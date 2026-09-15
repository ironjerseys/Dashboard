using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Business;

public sealed record ImportResult(int Rows, int Created, int Merged, int AlreadyImported, IReadOnlyList<string> Warnings);

public interface IApplicationImportService
{
    /// <summary>
    /// Importe un classeur .xlsx de candidatures. Une ligne qui correspond a une candidature deja
    /// creee par l'analyse des mails la complete au lieu de la dupliquer ; reimporter le meme
    /// fichier ne cree rien de plus.
    /// </summary>
    Task<ImportResult> ImportXlsxAsync(Stream xlsx, CancellationToken cancellationToken = default);
}

public sealed class ApplicationImportService : IApplicationImportService
{
    private const double MinPositionSimilarity = 0.5;

    /// <summary>Meme lien et poste quasi identique : c'est la meme ligne importee une seconde fois.</summary>
    private const double SamePostingSimilarity = 0.9;
    private static readonly TimeSpan SameDayTolerance = TimeSpan.FromDays(1.5);

    private readonly IDbContextFactory<BlogContext> _dbContextFactory;

    public ApplicationImportService(IDbContextFactory<BlogContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ImportResult> ImportXlsxAsync(Stream xlsx, CancellationToken cancellationToken = default)
    {
        var (rows, warnings) = XlsxApplicationReader.Read(xlsx);

        await using BlogContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<JobApplication> applications = await dbContext.JobApplications.ToListAsync(cancellationToken);

        // Une candidature ne recoit qu'une ligne : deux candidatures au meme poste dans le fichier
        // (villes ou dates differentes) restent deux candidatures.
        var claimed = new HashSet<JobApplication>(ReferenceEqualityComparer.Instance);
        int created = 0, merged = 0, alreadyImported = 0;

        var handled = new HashSet<SpreadsheetApplicationRow>(ReferenceEqualityComparer.Instance);

        // 1. Ligne deja importee lors d'un precedent import. Le lien seul ne suffit pas : certains
        //    portails (SuccessFactors, Taleo) donnent la meme adresse pour plusieurs offres.
        foreach (SpreadsheetApplicationRow row in rows.Where(r => r.JobUrl is not null))
        {
            JobApplication? sameUrl = applications.FirstOrDefault(a =>
                !claimed.Contains(a)
                && a.JobUrl == row.JobUrl
                && JobApplicationMatcher.SameCompany(a.Company, row.Company)
                && JobApplicationMatcher.PositionSimilarity(a.Position, row.Position) >= SamePostingSimilarity);

            if (sameUrl is not null)
            {
                Merge(sameUrl, row);
                claimed.Add(sameUrl);
                handled.Add(row);
                alreadyImported++;
            }
        }

        // 2. Candidatures creees par les mails. On compare toutes les paires avant de choisir :
        //    traiter les lignes dans l'ordre du fichier donnerait un mail du 12/09 a une ligne du
        //    16/08 au meme intitule, simplement parce qu'elle vient avant.
        var pairs = rows
            .Where(r => !handled.Contains(r))
            .SelectMany(r => applications
                .Where(a => a.JobUrl is null && !claimed.Contains(a))
                .Where(a => JobApplicationMatcher.SameCompany(a.Company, r.Company))
                .Select(a => (Row: r, Application: a, Score: Score(a, r), Gap: DateGap(a, r))))
            .Where(p => p.Score is not null)
            .OrderByDescending(p => p.Gap <= SameDayTolerance)
            .ThenByDescending(p => p.Score)
            .ThenBy(p => p.Gap)
            .ToList();

        foreach (var pair in pairs)
        {
            if (handled.Contains(pair.Row) || claimed.Contains(pair.Application))
            {
                continue;
            }

            Merge(pair.Application, pair.Row);
            claimed.Add(pair.Application);
            handled.Add(pair.Row);
            merged++;
        }

        // 3. Le reste devient de nouvelles candidatures.
        foreach (SpreadsheetApplicationRow row in rows.Where(r => !handled.Contains(r)))
        {
            var application = new JobApplication
            {
                Company = row.Company,
                Position = row.Position,
                Location = row.Location,
                JobUrl = row.JobUrl,
                Status = row.Status,
                AppliedUtc = row.AppliedUtc,
                LastEventUtc = row.AppliedUtc ?? DateTime.UtcNow,
                Notes = row.Notes
            };

            dbContext.JobApplications.Add(application);
            applications.Add(application);
            claimed.Add(application);
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Logs.Add(new Log
        {
            Level = warnings.Count > 0 ? "Warning" : "Info",
            Source = nameof(ApplicationImportService),
            Event = "ImportXlsx",
            Message = $"Rows={rows.Count}; Created={created}; Merged={merged}; AlreadyImported={alreadyImported}; " +
                      $"Warnings={string.Join(" | ", warnings)}",
            TimestampUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportResult(rows.Count, created, merged, alreadyImported, warnings);
    }

    /// <summary>
    /// Proximite d'une candidature issue des mails avec une ligne : poste assez proche, ou poste
    /// inconnu mais meme date. Null si elles ne peuvent pas etre la meme candidature.
    /// </summary>
    private static double? Score(JobApplication application, SpreadsheetApplicationRow row)
    {
        if (application.Position is not null && row.Position is not null)
        {
            double similarity = JobApplicationMatcher.PositionSimilarity(application.Position, row.Position);
            return similarity >= MinPositionSimilarity ? similarity : null;
        }

        // Mail sans intitule de poste : seule une date identique permet de relier la ligne.
        return DateGap(application, row) <= SameDayTolerance ? MinPositionSimilarity : null;
    }

    private static TimeSpan DateGap(JobApplication application, SpreadsheetApplicationRow row)
    {
        DateTime? applicationDate = application.AppliedUtc ?? application.LastEventUtc;
        return applicationDate is null || row.AppliedUtc is null
            ? TimeSpan.MaxValue
            : (applicationDate.Value - row.AppliedUtc.Value).Duration();
    }

    /// <summary>
    /// Complete sans ecraser : les mails sont plus recents que le classeur, sauf quand le classeur
    /// connait une issue (refus, entretien...) que les mails ne montrent pas.
    /// </summary>
    private static void Merge(JobApplication application, SpreadsheetApplicationRow row)
    {
        application.Position ??= row.Position;
        application.Location ??= row.Location;
        application.JobUrl ??= row.JobUrl;
        application.AppliedUtc ??= row.AppliedUtc;

        if (application.Status == JobApplicationStatus.Applied && row.Status != JobApplicationStatus.Applied)
        {
            application.Status = row.Status;
        }

        if (row.Notes is not null && (application.Notes is null || !application.Notes.Contains(row.Notes)))
        {
            string notes = application.Notes is null ? row.Notes : application.Notes + "\n" + row.Notes;
            application.Notes = notes.Length <= 2000 ? notes : notes[..2000];
        }
    }
}
