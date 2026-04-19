using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WindroseServerManager.Core.Models;

namespace WindroseServerManager.Core.Services;

public sealed class NexusClient : INexusClient
{
    private const string BaseUrl = "https://api.nexusmods.com";
    private const string UserAgent = "WindroseServerManager/1.0";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<NexusClient> _logger;

    public NexusClient(IHttpClientFactory httpFactory, IAppSettingsService settings, ILogger<NexusClient> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.Current.NexusApiKey);

    public async Task<NexusModInfo?> GetModAsync(int modId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogDebug("Nexus API-Key fehlt — GetModAsync wird übersprungen.");
            return null;
        }

        var domain = _settings.Current.NexusGameDomain;
        var apiKey = _settings.Current.NexusApiKey;
        var url = $"{BaseUrl}/v1/games/{domain}/mods/{modId}.json";

        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("apikey", apiKey);
        req.Headers.Add("User-Agent", UserAgent);
        req.Headers.Add("Accept", "application/json");

        try
        {
            using var response = await http.SendAsync(req, ct).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Nexus-Mod {ModId} nicht gefunden (404).", modId);
                return null;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Nexus-API-Key ist ungültig oder abgelaufen.");
            }
            if ((int)response.StatusCode == 429)
            {
                throw new InvalidOperationException("Nexus-API-Limit erreicht. Bitte später erneut versuchen.");
            }

            response.EnsureSuccessStatusCode();

            var dto = await response.Content.ReadFromJsonAsync<NexusModDto>(JsonOpts, ct).ConfigureAwait(false);
            if (dto is null)
            {
                _logger.LogWarning("Nexus-Antwort für Mod {ModId} war leer.", modId);
                return null;
            }

            return new NexusModInfo(
                ModId: dto.ModId,
                Name: dto.Name ?? $"Mod #{modId}",
                Version: dto.Version ?? "",
                Summary: dto.Summary ?? "",
                PictureUrl: dto.PictureUrl,
                DomainName: dto.DomainName ?? domain,
                Available: dto.Available);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Nexus-API-Request für Mod {ModId} fehlgeschlagen.", modId);
            throw new InvalidOperationException($"Nexus-Server nicht erreichbar: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Nexus-Request hat zu lange gedauert (Timeout).");
        }
    }

    /// <summary>Internes DTO-Mapping passend zur /v1/games/.../mods/{id}.json Antwort (snake_case).</summary>
    private sealed class NexusModDto
    {
        public int ModId { get; set; }
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Summary { get; set; }
        public string? PictureUrl { get; set; }
        public string? DomainName { get; set; }
        public bool Available { get; set; }
    }
}
