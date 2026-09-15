using Dashboard.Business;
using Dashboard.Persistance.DbContext;
using Dashboard.Persistance.Entities;
using Microsoft.Extensions.Options;

namespace Dashboard.Services;

/// <summary>
/// Passage automatique : synchro des mails, analyse, libelles Gmail. Les plafonds de cout sont
/// dans l'analyse elle-meme ; ce worker ne fait que declencher a intervalle regulier.
/// </summary>
public class MailPipelineWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MailOptions _options;

    public MailPipelineWorker(IServiceProvider serviceProvider, IOptions<MailOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.PipelineIntervalMinutes <= 0)
        {
            await LogAsync("Info", "Disabled", "Mail:PipelineIntervalMinutes <= 0 : passage automatique desactive.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.UserName) || string.IsNullOrWhiteSpace(_options.Password))
        {
            await LogAsync("Warning", "Disabled", "Mail:UserName ou Mail:Password absent : passage automatique desactive.");
            return;
        }

        var interval = TimeSpan.FromMinutes(_options.PipelineIntervalMinutes);

        try
        {
            // Laisse le demarrage (migrations, seed) se terminer avant le premier passage.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var pipeline = scope.ServiceProvider.GetRequiredService<IMailPipeline>();
                    await pipeline.RunAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await LogAsync("Error", "LoopError", ex.ToString());
                }

                await Task.Delay(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task LogAsync(string level, string evt, string message)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BlogContext>();
            db.Logs.Add(new Log
            {
                Level = level,
                Source = nameof(MailPipelineWorker),
                Event = evt,
                Message = message,
                TimestampUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch { }
    }
}
