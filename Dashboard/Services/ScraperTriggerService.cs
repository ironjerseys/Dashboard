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

/// Lance le scraper jobspy a la demande, en plus du run planifie du dimanche.
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

    public async Task<ScraperStatus?> GetStatusAsync(CancellationToken ct = default)
    {
        try
        {
            return await _http.GetFromJsonAsync<ScraperStatus>("status", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<ScraperStartResult> StartAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync("run", null, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
            return ScraperStartResult.AlreadyRunning;

        response.EnsureSuccessStatusCode();
        return ScraperStartResult.Started;
    }
}
