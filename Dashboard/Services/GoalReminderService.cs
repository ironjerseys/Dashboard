using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Dashboard.Services;

public class ReminderOptions
{
    public string RecipientEmail { get; set; } = "";
    public int Hour { get; set; } = 21;
    public int Minute { get; set; } = 43;
    public string TimeZoneId { get; set; } = "Europe/Paris";
}

public class GoalReminderService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IOptions<ReminderOptions> _opts;

    public GoalReminderService(IServiceProvider sp, IOptions<ReminderOptions> opts)
    {
        _sp = sp;
        _opts = opts;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = _opts.Value;
        await LogAsync("Info", "ServiceStart", $"GoalReminderService démarré (Hour={o.Hour}, Minute={o.Minute}, TZ={o.TimeZoneId})");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = LocalNow();
                var next = NextOccurrence(now, o.Hour, o.Minute);
                var delay = next - now;
                await LogAsync("Debug", "ComputeNext", $"NowLocal={now:o}; NextLocal={next:o}; Delay={delay}");
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
                await Task.Delay(delay, stoppingToken);

                await SendDailyReminder(stoppingToken);
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

    private static DateTime NextOccurrence(DateTime fromLocal, int hour, int minute)
    {
        var next = new DateTime(fromLocal.Year, fromLocal.Month, fromLocal.Day, hour, minute, 0, fromLocal.Kind);
        if (next <= fromLocal) next = next.AddDays(1);
        return next;
    }

    private DateTime LocalNow()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_opts.Value.TimeZoneId);
            return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private async Task SendDailyReminder(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
        var mail = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var opts = _opts.Value;

        var today = DateOnly.FromDateTime(LocalNow());
        var openGoals = await db.Goals
            .Where(g => g.Debut <= today && g.Fin >= today && !g.IsDone)
            .OrderBy(g => g.Debut)
            .ToListAsync(ct);

        await LogAsync("Info", "DailyQuery", $"Date={today:yyyy-MM-dd}; OpenGoals={openGoals.Count}");

        if (openGoals.Count == 0)
        {
            await LogAsync("Info", "NoGoals", "Aucun objectif ouvert - pas d'envoi");
            return;
        }

        var lines = openGoals.Select(g => $"- {g.Titre} ({g.Debut:yyyy-MM-dd} → {g.Fin:yyyy-MM-dd})");
        var body = $"<p>Objectifs ouverts pour le {today:yyyy-MM-dd} :</p><ul>" + string.Join("", lines.Select(l => $"<li>{System.Net.WebUtility.HtmlEncode(l)}</li>")) + "</ul>";
        try
        {
            //await LogAsync("Info", "SendingEmail", $"To={opts.RecipientEmail}; Count={openGoals.Count}");


            // EN PAUSE LE TEMPS DE DEFINIR LA LOGIQUE D ENVOI DE MAIL DATE ETC

            //await mail.SendAsync(opts.RecipientEmail, $"Rappel objectifs - {today:yyyy-MM-dd}", body);
            //await LogAsync("Info", "EmailSent", "Rappel envoyé avec succès");
        }
        catch (Exception ex)
        {
            await LogAsync("Error", "EmailFailed", ex.ToString());
        }
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
