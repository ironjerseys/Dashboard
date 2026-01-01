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
        var todoService = scope.ServiceProvider.GetRequiredService<ITodoService>();

        var leitnerService = scope.ServiceProvider.GetRequiredService<ILeitnerService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var now = GetLocalNow();
        var today = DateOnly.FromDateTime(now);

        var baseUrl = configuration["App:BaseUrl"];
        var reviewUrl = BuildAbsoluteUrl(baseUrl, "/review");

        var settings = await db.EmailSettings.Where(s => s.Enabled).ToListAsync(ct);

        foreach (var emailSettings in settings)
        {
            if (!ShouldRun(emailSettings, now))
            {
                continue;
            }

            var (periodStart, periodEnd) = ComputePeriodWindow(emailSettings, now);

            var openGoals = emailSettings.IncludeGoals
                ? await db.Goals
                    .Where(goal => goal.Debut <= today && goal.Fin >= today && !goal.IsDone && goal.OwnerId == emailSettings.UserId)
                    .OrderBy(goal => goal.Debut)
                    .ToListAsync(ct)
                : new List<Goal>();

            var newArticles = emailSettings.IncludeArticles
                ? await db.Articles
                    .Where(article => article.DateCreation >= periodStart && article.DateCreation <= periodEnd)
                    .OrderByDescending(article => article.DateCreation)
                    .Select(article => new ArticleInfo { Id = article.Id, Titre = article.Titre, DateCreation = article.DateCreation })
                    .ToListAsync(ct)
                : new List<ArticleInfo>();

            var openTodos = emailSettings.IncludeTodos ? await todoService.GetOpenAsync() : new List<Todo>();
            var doneTodos = emailSettings.IncludeTodos ? await todoService.GetDoneInPeriodAsync(periodStart, periodEnd) : new List<Todo>();

            // --- Leitner : on ne met PAS les questions dans l'email, juste un lien + compteur ---
            var utcNow = DateOnly.FromDateTime(DateTime.UtcNow);
            var dueCount = await leitnerService.GetDueCountAsync(emailSettings.UserId, utcNow, ct);

            var body = BuildEmailBody(
                today,
                periodStart,
                periodEnd,
                openGoals,
                newArticles,
                openTodos,
                doneTodos,
                dueCount,
                reviewUrl
            );

            var subject = emailSettings.Frequency switch
            {
                EmailFrequency.Daily => $"Rappel quotidien - {today:yyyy-MM-dd}",
                EmailFrequency.Weekly => $"Rappel hebdomadaire - Semaine du {periodStart:yyyy-MM-dd}",
                EmailFrequency.Monthly => $"Rappel mensuel - {periodStart:yyyy-MM}",
                _ => $"Rappel - {today:yyyy-MM-dd}"
            };

            try
            {
                await mail.SendAsync(emailSettings.RecipientEmail, subject, body);
                emailSettings.LastSentUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                await LogAsync("Info", "EmailSent",
                    $"To={emailSettings.RecipientEmail}; DueQuestions={dueCount}; Window={periodStart:o}-{periodEnd:o}");
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

    private static string BuildAbsoluteUrl(string? baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static string BuildEmailBody(
       DateOnly today,
       DateTime periodStart,
       DateTime periodEnd,
       List<Goal> openGoals,
       List<ArticleInfo> newArticles,
       List<Todo> openTodos,
       List<Todo> doneTodos,
       int leitnerDueCount,
       string leitnerReviewUrl)
    {
        var sb = new System.Text.StringBuilder();

        sb.Append($"<p>Date: {today:yyyy-MM-dd}</p>");
        sb.Append($"<p>Période: {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}</p>");

        sb.Append("<h3>Révision du jour</h3>");
        if (leitnerDueCount > 0)
        {
            sb.Append($"<p><strong>{leitnerDueCount}</strong> question(s) à revoir. ");
            sb.Append($"<a href=\"{System.Net.WebUtility.HtmlEncode(leitnerReviewUrl)}\">Ouvrir la session</a></p>");
        }
        else
        {
            sb.Append("<p>Aucune question à revoir aujourd’hui.</p>");
        }

        if (openGoals.Count > 0)
        {
            sb.Append("<h3>Objectifs ouverts</h3><ul>");
            foreach (var goal in openGoals)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(goal.Titre)} ({goal.Debut:yyyy-MM-dd} → {goal.Fin:yyyy-MM-dd})</li>");
            }
            sb.Append("</ul>");
        }

        if (newArticles.Count > 0)
        {
            sb.Append("<h3>Articles créés durant la période</h3><ul>");
            foreach (var article in newArticles)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(article.Titre)} ({article.DateCreation:yyyy-MM-dd})</li>");
            }
            sb.Append("</ul>");
        }

        if (openTodos.Count > 0)
        {
            sb.Append("<h3>Todos ouvertes</h3><ul>");
            foreach (var todo in openTodos)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(todo.Description)}</li>");
            }
            sb.Append("</ul>");
        }

        if (doneTodos.Count > 0)
        {
            sb.Append("<h3>Todos terminées dans la période</h3><ul>");
            foreach (var todo in doneTodos)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(todo.Description)} (fait le {(todo.DoneAt ?? DateTime.UtcNow):yyyy-MM-dd})</li>");
            }
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    private async Task LogAsync(string level, string evt, string? message)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new Log
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
