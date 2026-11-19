using System.Text.Json.Serialization;

namespace SpotiFlow.Spotify;

public class PlaybackState
{
    [JsonPropertyName("is_playing")]
    public bool IsPlaying { get; set; }

    /// <summary>Aktualna pozycja w utworze w milisekundach.</summary>
    [JsonPropertyName("progress_ms")]
    public int? ProgressMs { get; set; }

    /// <summary>"track" (piosenka), "episode" (podcast), "ad" (reklama) lub "unknown".</summary>
    [JsonPropertyName("currently_playing_type")]
    public string? CurrentlyPlayingType { get; set; }

    /// <summary>Aktualnie odtwarzany element (null np. w trakcie reklamy).</summary>
    [JsonPropertyName("item")]
    public SpotifyTrack? Item { get; set; }
}

/// <summary>Utwór (lub odcinek podcastu - mają wspólne pola id/name/duration_ms).</summary>
public class SpotifyTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>Identyfikator w formacie "spotify:track:ID" - potrzebny do dodania do kolejki.</summary>
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = "";

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; set; } = new();

    /// <summary>Wykonawcy połączeni przecinkiem, np. "Daft Punk, Pharrell Williams".</summary>
    [JsonIgnore]
    public string ArtistNames => string.Join(", ", Artists.Select(a => a.Name));
}

public class SpotifyArtist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>Odpowiedź z GET /v1/me/player/queue: co gra teraz i co czeka w kolejce.</summary>
public class QueueState
{
    [JsonPropertyName("currently_playing")]
    public SpotifyTrack? CurrentlyPlaying { get; set; }

    [JsonPropertyName("queue")]
    public List<SpotifyTrack> Queue { get; set; } = new();
}
