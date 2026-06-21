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
        // Couleurs reprises du site (wwwroot/css/site.css).
        const string Primary = "#ff7a00";
        const string PrimaryAlt = "#ff9e44";
        const string Text = "#111827";
        const string Muted = "#6b7280";
        const string Border = "#e5e7eb";
        const string PageBg = "#f3f4f6";

        var url = System.Net.WebUtility.HtmlEncode(practiceUrl);
        var total = questionsDue + codingDue + sqlDue;

        var rows = new System.Text.StringBuilder();
        void Row(string icon, int count, string label)
        {
            if (count <= 0) return;
            rows.Append($@"
                <tr>
                    <td style=""padding:10px 14px;border:1px solid {Border};border-radius:12px;background:#fff;"">
                        <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
                            <tr>
                                <td style=""font-size:22px;width:34px;vertical-align:middle;"">{icon}</td>
                                <td style=""vertical-align:middle;font-family:Segoe UI,Arial,sans-serif;color:{Text};font-size:15px;"">{label}</td>
                                <td align=""right"" style=""vertical-align:middle;font-family:Segoe UI,Arial,sans-serif;color:{Primary};font-size:20px;font-weight:700;"">{count}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
                <tr><td style=""height:10px;line-height:10px;font-size:0;"">&nbsp;</td></tr>");
        }

        Row("📝", questionsDue, "Technical questions");
        Row("💻", codingDue, "Coding challenges");
        Row("🗄️", sqlDue, "SQL challenges");

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""></head>
<body style=""margin:0;padding:0;background:{PageBg};"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:{PageBg};padding:24px 12px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;background:#fff;border:1px solid {Border};border-radius:18px;overflow:hidden;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background:linear-gradient(135deg,{Primary},{PrimaryAlt});padding:26px 28px;"">
                            <div style=""font-family:Segoe UI,Arial,sans-serif;color:rgba(255,255,255,.85);font-size:13px;font-weight:600;letter-spacing:.5px;text-transform:uppercase;"">Joris Reynes</div>
                            <div style=""font-family:Segoe UI,Arial,sans-serif;color:#fff;font-size:24px;font-weight:700;margin-top:4px;"">⚡ Time to practice</div>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:28px;"">
                            <p style=""font-family:Segoe UI,Arial,sans-serif;color:{Text};font-size:16px;margin:0 0 20px;"">
                                You have <strong style=""color:{Primary};"">{total}</strong> item{(total == 1 ? "" : "s")} due for review today. Keep your streak going!
                            </p>

                            <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:separate;"">
                                {rows}
                            </table>

                            <!-- CTA -->
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:14px auto 4px;"">
                                <tr>
                                    <td align=""center"" style=""border-radius:12px;background:{Primary};"">
                                        <a href=""{url}"" style=""display:inline-block;padding:13px 30px;font-family:Segoe UI,Arial,sans-serif;font-size:16px;font-weight:600;color:#fff;text-decoration:none;border-radius:12px;"">Start reviewing →</a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding:18px 28px;border-top:1px solid {Border};"">
                            <p style=""font-family:Segoe UI,Arial,sans-serif;color:{Muted};font-size:12px;margin:0;text-align:center;"">
                                You receive this daily reminder because you have spaced-repetition reviews scheduled.
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
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
