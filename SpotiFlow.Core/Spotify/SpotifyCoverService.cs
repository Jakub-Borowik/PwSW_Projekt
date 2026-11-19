using System.Text.Json;

namespace SpotiFlow.Spotify;

public class SpotifyCoverService
{
    private readonly HttpClient _http;
    private readonly Dictionary<string, string> _cache = new();

    public SpotifyCoverService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>Adres okładki albo null, jeśli się nie udało (brak okładki nie jest błędem).</summary>
    public async Task<string?> GetCoverUrlAsync(string trackId)
    {
        if (_cache.TryGetValue(trackId, out string? cached))
            return cached;

        try
        {
            string json = await _http.GetStringAsync(
                "https://open.spotify.com/oembed?url=" + Uri.EscapeDataString("https://open.spotify.com/track/" + trackId));

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("thumbnail_url", out JsonElement url) && url.GetString() is string address)
            {
                _cache[trackId] = address;
                return address;
            }
        }
        catch
        {
            // brak internetu / nieznany utwór -> karta pokaże zastępczą ikonę
        }
        return null;
    }
}
