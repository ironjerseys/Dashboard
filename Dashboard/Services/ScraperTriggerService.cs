using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Dashboard.Services;

public class ScraperOptions
{
    /// URL du mini serveur HTTP du container jobspy (reseau Docker interne).
    public string TriggerUrl { get; set; } = "http://jobspy:8000";

    /// Secret partage avec le container (SCRAPER_TRIGGER_TOKEN). Vide = pas de controle.
    public string TriggerToken { get; set; } = "";
}

public record ScraperStatus(bool Running, DateTime? StartedAt, DateTime? FinishedAt, int? ExitCode);

public enum ScraperStartResult { Started, AlreadyRunning }

/// Scripts que le container jobspy sait lancer (cf. SCRIPTS dans trigger_server.py).
public static class ScraperJobs
{
    /// Offres tech (page Job Data), planifie le dimanche.
    public const string Jobs = "jobs";

    /// Apprenti mecanicien au Quebec, planifie le mercredi.
    public const string Mechanics = "mechanics";
}

/// Lance les scrapers jobspy a la demande, en plus de leurs runs planifies.
public sealed class ScraperTriggerService
{
    private readonly HttpClient _http;

    public ScraperTriggerService(HttpClient http, IOptions<ScraperOptions> options)
    {
        _http = http;
        _http.BaseAddress = new Uri(options.Value.TriggerUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(10);
        if (!string.IsNullOrEmpty(options.Value.TriggerToken))
            _http.DefaultRequestHeaders.Add("X-Trigger-Token", options.Value.TriggerToken);
    }

    public Task<ScraperStatus?> GetStatusAsync(CancellationToken ct = default) => GetStatusAsync(ScraperJobs.Jobs, ct);

    public Task<ScraperStartResult> StartAsync(CancellationToken ct = default) => StartAsync(ScraperJobs.Jobs, ct);

    public async Task<ScraperStatus?> GetStatusAsync(string job, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<ScraperStatus>($"status?job={Uri.EscapeDataString(job)}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<ScraperStartResult> StartAsync(string job, CancellationToken ct = default)
    {
        using var response = await _http.PostAsync($"run?job={Uri.EscapeDataString(job)}", null, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
            return ScraperStartResult.AlreadyRunning;

        response.EnsureSuccessStatusCode();
        return ScraperStartResult.Started;
    }
}
