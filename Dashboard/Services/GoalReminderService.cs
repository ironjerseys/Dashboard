using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public class GoalReminderService : BackgroundService
{
    private readonly IServiceProvider _sp;

    public GoalReminderService(IServiceProvider sp)
    {
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LogAsync("Info", "ServiceStart", "GoalReminderService démarré");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wake up every minute to check schedules
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                await ProcessSchedules(stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await LogAsync("Info", "ServiceStopping", "GoalReminderService arrêt demandé");
            }
            catch (Exception ex)
            {
                await LogAsync("Error", "LoopError", ex.ToString());
            }
        }
        await LogAsync("Info", "ServiceStopped", "GoalReminderService arrêté");
    }

    private static DateTime GetLocalNow() => DateTime.Now;

    private async Task ProcessSchedules(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
        var mail = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var todoSvc = scope.ServiceProvider.GetRequiredService<ITodoService>();

        var now = GetLocalNow();
        var today = DateOnly.FromDateTime(now);
        var settings = await db.EmailSettings.Where(s => s.Enabled).ToListAsync(ct);
        foreach (var s in settings)
        {
            if (!ShouldRun(s, now)) continue;

            var (periodStart, periodEnd) = ComputePeriodWindow(s, now);

            var openGoals = s.IncludeGoals
                ? await db.Goals.Where(g => g.Debut <= today && g.Fin >= today && !g.IsDone).OrderBy(g => g.Debut).ToListAsync(ct)
                : new List<Goal>();

            var newArticles = s.IncludeArticles
                ? await db.Articles.Where(a => a.DateCreation >= periodStart && a.DateCreation <= periodEnd)
                    .OrderByDescending(a => a.DateCreation)
                    .Select(a => new ArticleInfo { Id = a.Id, Titre = a.Titre, DateCreation = a.DateCreation }).ToListAsync(ct)
                : new List<ArticleInfo>();

            var openTodos = s.IncludeTodos ? await todoSvc.GetOpenAsync() : new List<Todo>();
            var doneTodos = s.IncludeTodos ? await todoSvc.GetDoneInPeriodAsync(periodStart, periodEnd) : new List<Todo>();

            var body = BuildEmailBody(today, periodStart, periodEnd, openGoals, newArticles, openTodos, doneTodos);
            var subject = s.Frequency switch
            {
                EmailFrequency.Daily => $"Rappel quotidien - {today:yyyy-MM-dd}",
                EmailFrequency.Weekly => $"Rappel hebdomadaire - Semaine du {periodStart:yyyy-MM-dd}",
                EmailFrequency.Monthly => $"Rappel mensuel - {periodStart:yyyy-MM}",
                _ => $"Rappel - {today:yyyy-MM-dd}"
            };

            try
            {
                await mail.SendAsync(s.RecipientEmail, subject, body);
                s.LastSentUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await LogAsync("Info", "EmailSent", $"To={s.RecipientEmail}; Freq={s.Frequency}; Window={periodStart:o}-{periodEnd:o}; Goals={openGoals.Count}; Articles={newArticles.Count}; OpenTodos={openTodos.Count}; DoneTodos={doneTodos.Count}");
            }
            catch (Exception ex)
            {
                await LogAsync("Error", "EmailFailed", ex.ToString());
            }
        }
    }

    private static bool ShouldRun(EmailSettings s, DateTime now)
    {
        // Run at specified hour/minute. Prevent duplicates: if LastSentUtc within last 55 minutes, skip.
        if (now.Hour != s.Hour || now.Minute != s.Minute) return false;
        if (s.LastSentUtc.HasValue && (DateTime.UtcNow - s.LastSentUtc.Value) < TimeSpan.FromMinutes(55)) return false;
        return s.Frequency switch
        {
            EmailFrequency.Daily => true,
            EmailFrequency.Weekly => s.DayOfWeek.HasValue && now.DayOfWeek == s.DayOfWeek.Value,
            EmailFrequency.Monthly => s.DayOfMonth.HasValue && now.Day == s.DayOfMonth.Value,
            _ => false
        };
    }

    private static (DateTime start, DateTime end) ComputePeriodWindow(EmailSettings s, DateTime now)
    {
        return s.Frequency switch
        {
            EmailFrequency.Daily => (new DateTime(now.Year, now.Month, now.Day, 0, 0, 0), new DateTime(now.Year, now.Month, now.Day, 23, 59, 59)),
            EmailFrequency.Weekly =>
                (
                    now.Date.AddDays(-(int)now.DayOfWeek).Date,
                    now.Date.AddDays(6 - (int)now.DayOfWeek).Date.AddHours(23).AddMinutes(59).AddSeconds(59)
                ),
            EmailFrequency.Monthly =>
                (
                    new DateTime(now.Year, now.Month, 1),
                    new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59)
                ),
            _ => (now, now)
        };
    }

    private static string BuildEmailBody(DateOnly today, DateTime periodStart, DateTime periodEnd, List<Goal> openGoals, List<ArticleInfo> newArticles, List<Todo> openTodos, List<Todo> doneTodos)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"<p>Date: {today:yyyy-MM-dd}</p>");
        sb.Append($"<p>Période: {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}</p>");

        if (openGoals.Count > 0)
        {
            sb.Append("<h3>Objectifs ouverts</h3><ul>");
            foreach (var g in openGoals)
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(g.Titre)} ({g.Debut:yyyy-MM-dd} → {g.Fin:yyyy-MM-dd})</li>");
            sb.Append("</ul>");
        }

        if (newArticles.Count > 0)
        {
            sb.Append("<h3>Articles créés durant la période</h3><ul>");
            foreach (var a in newArticles)
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(a.Titre)} ({a.DateCreation:yyyy-MM-dd})</li>");
            sb.Append("</ul>");
        }

        if (openTodos.Count > 0)
        {
            sb.Append("<h3>Todos ouvertes</h3><ul>");
            foreach (var t in openTodos)
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(t.Description)}</li>");
            sb.Append("</ul>");
        }

        if (doneTodos.Count > 0)
        {
            sb.Append("<h3>Todos terminées dans la période</h3><ul>");
            foreach (var t in doneTodos)
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(t.Description)} (fait le {(t.DoneAt ?? DateTime.UtcNow):yyyy-MM-dd})</li>");
            sb.Append("</ul>");
        }

        if (sb.Length == 0)
            sb.Append("<p>Aucune donnée sélectionnée pour l'envoi.</p>");

        return sb.ToString();
    }

    private async Task LogAsync(string level, string evt, string? message)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new LogEntry
            {
                Level = level,
                Source = nameof(GoalReminderService),
                Event = evt,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch { }
    }
}

public class ArticleInfo
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; }
}
