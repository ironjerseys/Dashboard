using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dashboard.Services;

public class ReminderService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private DateOnly _lastRunDate = DateOnly.MinValue;
    private readonly HashSet<string> _sentToday = new();

    public ReminderService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LogAsync("Info", "ServiceStart", "ReminderService démarré");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);

                if (today != _lastRunDate)
                {
                    _lastRunDate = today;
                    _sentToday.Clear();
                }

                if (now.Hour == 18 && now.Minute == 0)
                {
                    await ProcessRemindersAsync(stoppingToken);
                }
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await LogAsync("Info", "ServiceStopping", "ReminderService arrêt demandé");
            }
            catch (Exception ex)
            {
                await LogAsync("Error", "LoopError", ex.ToString());
            }
        }
        await LogAsync("Info", "ServiceStopped", "ReminderService arrêté");
    }

    private async Task ProcessRemindersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var leitnerService = scope.ServiceProvider.GetRequiredService<ILeitnerService>();
        var mail = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var baseUrl = configuration["App:BaseUrl"];
        var reviewUrl = BuildAbsoluteUrl(baseUrl, "/review");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var users = userManager.Users.ToList();
        foreach (var user in users)
        {
            if (_sentToday.Contains(user.Id)) continue;
            if (string.IsNullOrWhiteSpace(user.Email)) continue;

            var dueCount = await leitnerService.GetDueCountAsync(user.Id, today, ct);
            if (dueCount == 0) continue;

            var subject = $"Questions techniques du jour — {today:yyyy-MM-dd}";
            var body = BuildEmailBody(dueCount, reviewUrl);

            try
            {
                await mail.SendAsync(user.Email, subject, body);
                _sentToday.Add(user.Id);
                await LogAsync("Info", "EmailSent", $"To={user.Email}; DueQuestions={dueCount}");
            }
            catch (Exception ex)
            {
                await LogAsync("Error", "EmailFailed", ex.ToString());
            }
        }
    }

    private static string BuildAbsoluteUrl(string? baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return path;
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static string BuildEmailBody(int dueCount, string reviewUrl)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"<p>Vous avez <strong>{dueCount}</strong> question(s) technique(s) à réviser aujourd'hui.</p>");
        sb.Append($"<p><a href=\"{System.Net.WebUtility.HtmlEncode(reviewUrl)}\">Cliquez ici pour répondre aux questions</a></p>");
        return sb.ToString();
    }

    private async Task LogAsync(string level, string evt, string? message)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new Log
            {
                Level = level,
                Source = nameof(ReminderService),
                Event = evt,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch { }
    }
}
