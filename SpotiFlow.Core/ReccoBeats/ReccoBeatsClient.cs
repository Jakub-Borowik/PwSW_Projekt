using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpotiFlow.Logic;

namespace SpotiFlow.ReccoBeats;

public record RecommendedTrack(string SpotifyId, string Title, string Artists);

public class ReccoBeatsClient
{
    private const string BaseUrl = "https://api.reccobeats.com/v1/";
    private readonly HttpClient _http;

    public ReccoBeatsClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<Dictionary<string, AudioProfile>> GetAudioFeaturesAsync(IEnumerable<string> spotifyIds, CancellationToken ct)
    {
        var result = new Dictionary<string, AudioProfile>();
        string ids = string.Join(",", spotifyIds.Take(40));
        if (ids.Length == 0)
            return result;

        using var response = await _http.GetAsync(BaseUrl + "audio-features?ids=" + ids, ct);
        if (!response.IsSuccessStatusCode)
            return result;

        var page = JsonSerializer.Deserialize<ContentPage<FeaturesDto>>(await response.Content.ReadAsStringAsync(ct));
        foreach (var f in page?.Content ?? new())
        {
            string? spotifyId = ExtractSpotifyId(f.Href);
            if (spotifyId != null)
                result[spotifyId] = new AudioProfile(f.Danceability, f.Energy, f.Valence, f.Acousticness);
        }
        return result;
    }

    public async Task<List<RecommendedTrack>> GetRecommendationsAsync(string seedSpotifyId, AudioProfile target, int size, CancellationToken ct)
    {

        string F(double v) => Math.Clamp(v, 0, 1).ToString("0.000", CultureInfo.InvariantCulture);

        string url = BaseUrl + "track/recommendation" +
                     $"?size={size}&seeds={seedSpotifyId}" +
                     $"&danceability={F(target.Danceability)}" +
                     $"&energy={F(target.Energy)}" +
                     $"&valence={F(target.Valence)}" +
                     $"&acousticness={F(target.Acousticness)}";

        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return new List<RecommendedTrack>();

        var page = JsonSerializer.Deserialize<ContentPage<TrackDto>>(await response.Content.ReadAsStringAsync(ct));
        var tracks = new List<RecommendedTrack>();
        foreach (var t in page?.Content ?? new())
        {
            string? spotifyId = ExtractSpotifyId(t.Href);
            if (spotifyId != null)
                tracks.Add(new RecommendedTrack(spotifyId, t.TrackTitle, string.Join(", ", t.Artists.Select(a => a.Name))));
        }
        return tracks;
    }

    public static string? ExtractSpotifyId(string? href)
    {
        const string marker = "/track/";
        if (string.IsNullOrEmpty(href))
            return null;
        int index = href.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return null;
        string id = href[(index + marker.Length)..];
        int queryStart = id.IndexOf('?');
        return queryStart >= 0 ? id[..queryStart] : id;
    }

    // Klasy dla JSONA z ReccoBreats

    private class ContentPage<T>
    {
        [JsonPropertyName("content")] public List<T> Content { get; set; } = new();
    }

    private class FeaturesDto
    {
        [JsonPropertyName("href")] public string? Href { get; set; }
        [JsonPropertyName("danceability")] public double Danceability { get; set; }
        [JsonPropertyName("energy")] public double Energy { get; set; }
        [JsonPropertyName("valence")] public double Valence { get; set; }
        [JsonPropertyName("acousticness")] public double Acousticness { get; set; }
    }

    private class TrackDto
    {
        [JsonPropertyName("href")] public string? Href { get; set; }
        [JsonPropertyName("trackTitle")] public string TrackTitle { get; set; } = "";
        [JsonPropertyName("artists")] public List<ArtistDto> Artists { get; set; } = new();
    }

    private class ArtistDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
    }
}
