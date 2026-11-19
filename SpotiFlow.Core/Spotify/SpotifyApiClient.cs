using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpotiFlow.Spotify;

public class SpotifyApiClient
{
    private const string BaseUrl = "https://api.spotify.com/v1/";

    private readonly HttpClient _http;
    private SpotifyAuthService? _auth;

    public SpotifyApiClient(HttpClient http)
    {
        _http = http;
    }

    public SpotifyToken? Token { get; private set; }

    public bool IsLoggedIn => Token != null;

    public void SetSession(SpotifyAuthService auth, SpotifyToken token)
    {
        _auth = auth;
        Token = token;
    }

    public async Task<PlaybackState?> GetPlaybackStateAsync(CancellationToken ct)
    {
        // additional_types=episode, aby dostawac dane na temat podcastow by je pomijac
        using var response = await SendAsync(HttpMethod.Get, "me/player?additional_types=episode", ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        string json = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PlaybackState>(json);
    }

    public async Task AddToQueueAsync(string trackUri, CancellationToken ct)
    {
        using var _ = await SendAsync(HttpMethod.Post, "me/player/queue?uri=" + Uri.EscapeDataString(trackUri), ct);
    }

    public async Task<QueueState?> GetQueueAsync(CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, "me/player/queue", ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        string json = await response.Content.ReadAsStringAsync(ct);
        return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<QueueState>(json);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, CancellationToken ct)
    {
        if (Token == null)
            throw new SpotifyApiException(HttpStatusCode.Unauthorized, "Nie zalogowano do Spotify.");

        var response = await SendOnceAsync(method, path, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && Token.RefreshToken != null && _auth != null)
        {
            response.Dispose();
            try
            {
                Token = await _auth.RefreshAsync(Token.RefreshToken, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new SpotifyApiException(HttpStatusCode.Unauthorized, "Token wygasł i nie udało się go odświeżyć.");
            }
            response = await SendOnceAsync(method, path, ct);
        }

        if (IsServerError(response.StatusCode))
        {
            response.Dispose();
            await Task.Delay(ServerErrorRetryDelay, ct);
            response = await SendOnceAsync(method, path, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
            var status = response.StatusCode;
            response.Dispose();

            string endpoint = $"{method} {path.Split('?')[0]}";
            throw new SpotifyApiException(status, $"Spotify API ({endpoint}) zwróciło {(int)status}: {body}", retryAfter);
        }

        return response;
    }

    public TimeSpan ServerErrorRetryDelay { get; set; } = TimeSpan.FromSeconds(1); // Przy błędach 5xx (Problem po stronie Spotify), ponawiam próbę po 1 sekundzie

    public static bool IsServerError(HttpStatusCode status) => (int)status >= 500 && (int)status <= 599;

    private Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        // Każde żądanie do API musi mieć nagłówek: Authorization: Bearer [token]
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token!.AccessToken);
        if (method == HttpMethod.Post)
            request.Content = new ByteArrayContent(Array.Empty<byte>()); // Dodanie pustego content-length, poniewaz spotify inaczej odrzuca
        return _http.SendAsync(request, ct);
    }
}
