using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace Dashboard.Services;


public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}


public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _opts = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = _opts.EnableSsl,
            Credentials = new NetworkCredential(_opts.User, _opts.Password)
        };
        using var msg = new MailMessage(_opts.FromEmail, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        await client.SendMailAsync(msg);
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
