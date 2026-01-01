using Dashboard.Data;
using Dashboard.Entities;
using Dashboard.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Dashboard.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public class SmtpEmailSender(IOptions<SmtpOptions> options, IServiceProvider ServiceProvider) : IEmailSender
{
    private readonly SmtpOptions _smtpOptions = options.Value;
    private readonly IServiceProvider _serviceProvider = ServiceProvider;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_smtpOptions.Host, _smtpOptions.Port)
        {
            EnableSsl = _smtpOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpOptions.User, _smtpOptions.Password)
        };
        using var msg = new MailMessage(_smtpOptions.FromEmail, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        try
        {
            await LogAsync("Info", nameof(SmtpEmailSender), "BeforeSend", $"Host={_smtpOptions.Host}; Port={_smtpOptions.Port}; User={Mask(_smtpOptions.User)}; From={_smtpOptions.FromEmail}");
            await client.SendMailAsync(msg);
            await LogAsync("Info", nameof(SmtpEmailSender), "EmailSent", $"To={toEmail}; Subject={subject}");
        }
        catch (Exception ex)
        {
            await LogAsync("Error", nameof(SmtpEmailSender), "EmailFailed", $"To={toEmail}; Subject={subject}; Host={_smtpOptions.Host}; Port={_smtpOptions.Port}; User={Mask(_smtpOptions.User)}", ex);
            throw;
        }
    }

    private static string Mask(string? s)
    {
        return string.IsNullOrEmpty(s) ? "" : (s.Length <= 2 ? "**" : s[..2] + new string('*', Math.Max(0, s.Length - 4)) + s[^2..]);
    }

    private async Task LogAsync(string level, string source, string evt, string? message, Exception? ex = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new Log
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


