using Dashboard.Data;
using Dashboard.Entities;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Dashboard.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public class SmtpEmailSender(IOptions<SmtpOptions> options, IServiceProvider sp) : IEmailSender
{
    private readonly SmtpOptions _opts = options.Value;
    private readonly IServiceProvider _sp = sp;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_opts.User, _opts.Password)
        };
        using var msg = new MailMessage(_opts.FromEmail, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        try
        {
            await LogAsync("Info", nameof(SmtpEmailSender), "BeforeSend", $"Host={_opts.Host}; Port={_opts.Port}; User={Mask(_opts.User)}; From={_opts.FromEmail}");
            await client.SendMailAsync(msg);
            await LogAsync("Info", nameof(SmtpEmailSender), "EmailSent", $"To={toEmail}; Subject={subject}");
        }
        catch (Exception ex)
        {
            await LogAsync("Error", nameof(SmtpEmailSender), "EmailFailed", $"To={toEmail}; Subject={subject}; Host={_opts.Host}; Port={_opts.Port}; User={Mask(_opts.User)}", ex);
            throw;
        }
    }

    private static string Mask(string? s)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= 2 ? "**" : s[..2] + new string('*', Math.Max(0, s.Length - 4)) + s[^2..]);

    private async Task LogAsync(string level, string source, string evt, string? message, Exception? ex = null)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new LogEntry
            {
                Level = level,
                Source = source,
                Event = evt,
                Message = message,
                Exception = ex?.ToString(),
                TimestampUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch { /* no throw - avoid masking original errors */ }
    }
}

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
}
