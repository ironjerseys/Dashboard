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
        var reviewService = scope.ServiceProvider.GetRequiredService<ICodeChallengeReviewService>();
        var mail = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var baseUrl = configuration["App:BaseUrl"];
        var practiceUrl = BuildAbsoluteUrl(baseUrl, "/quiz/all");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var users = userManager.Users.ToList();
        foreach (var user in users)
        {
            if (_sentToday.Contains(user.Id)) continue;
            if (string.IsNullOrWhiteSpace(user.Email)) continue;

            var questionsDue = await leitnerService.GetDueCountAsync(user.Id, today, ct);
            var (codingDue, sqlDue) = await reviewService.GetDueCountsAsync(user.Id, today, ct);
            if (questionsDue + codingDue + sqlDue == 0) continue;

            var subject = $"Daily practice reminder — {today:yyyy-MM-dd}";
            var body = BuildEmailBody(questionsDue, codingDue, sqlDue, practiceUrl);

            try
            {
                await mail.SendAsync(user.Email, subject, body);
                _sentToday.Add(user.Id);
                await LogAsync("Info", "EmailSent", $"To={user.Email}; Questions={questionsDue}; Coding={codingDue}; Sql={sqlDue}");
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

    private static string BuildEmailBody(int questionsDue, int codingDue, int sqlDue, string practiceUrl)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<p>You have items to practice today:</p>");
        sb.Append("<ul>");
        if (questionsDue > 0)
            sb.Append($"<li><strong>{questionsDue}</strong> technical question(s)</li>");
        if (codingDue > 0)
            sb.Append($"<li><strong>{codingDue}</strong> coding challenge(s)</li>");
        if (sqlDue > 0)
            sb.Append($"<li><strong>{sqlDue}</strong> SQL challenge(s)</li>");
        sb.Append("</ul>");
        sb.Append($"<p><a href=\"{System.Net.WebUtility.HtmlEncode(practiceUrl)}\">Open your practice page</a></p>");
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
