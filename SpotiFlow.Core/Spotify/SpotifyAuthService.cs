using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace SpotiFlow.Spotify;

public class SpotifyToken
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class SpotifyAuthService
{
    public const int CallbackPort = 5543;
    public static readonly string RedirectUri = $"http://127.0.0.1:{CallbackPort}/callback";

    public static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(15);

    private const string Scopes = "user-read-playback-state user-modify-playback-state user-read-currently-playing";

    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";

    private readonly HttpClient _http;
    private readonly string _clientId;

    public SpotifyAuthService(HttpClient http, string clientId)
    {
        _http = http;
        _clientId = clientId;
    }

    public async Task<SpotifyToken> LoginAsync(CancellationToken cancellationToken)
    {
        string codeVerifier = CreateCodeVerifier();
        string codeChallenge = CreateCodeChallenge(codeVerifier);
        string state = Guid.NewGuid().ToString("N");

        string url = AuthorizeUrl +
            "?client_id=" + Uri.EscapeDataString(_clientId) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(RedirectUri) +
            "&code_challenge_method=S256" +
            "&code_challenge=" + codeChallenge +
            "&scope=" + Uri.EscapeDataString(Scopes) +
            "&state=" + state;

        var listener = new TcpListener(IPAddress.Loopback, CallbackPort);
        listener.Start();
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(LoginTimeout);

            var query = await WaitForCallbackAsync(listener, timeout.Token);

            if (query["error"] is string error)
                throw new InvalidOperationException($"Spotify odrzuciło logowanie: {error}");
            if (query["state"] != state)
                throw new InvalidOperationException("Niepoprawny parametr 'state' - logowanie przerwane.");
            string code = query["code"] ?? throw new InvalidOperationException("Brak kodu autoryzacji w odpowiedzi.");

            return await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["client_id"] = _clientId,
                ["code_verifier"] = codeVerifier,
            }, cancellationToken);
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task<SpotifyToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _clientId,
        }, cancellationToken);

        token.RefreshToken ??= refreshToken;
        return token;
    }

    private async Task<SpotifyToken> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), ct);
        string json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nie udało się pobrać tokenu ({(int)response.StatusCode}): {json}");

        var dto = JsonSerializer.Deserialize<TokenResponse>(json)
                  ?? throw new InvalidOperationException("Pusta odpowiedź serwera tokenów.");

        return new SpotifyToken
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(dto.ExpiresIn),
        };
    }
    // czeka na zadanie get logowania
    private static async Task<System.Collections.Specialized.NameValueCollection> WaitForCallbackAsync(
        TcpListener listener, CancellationToken ct)
    {
        while (true)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync(ct);
            using NetworkStream stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            // Pierwsza linia żądania wygląda tak: "GET /callback?code=abc&state=xyz HTTP/1.1"
            string requestLine = await reader.ReadLineAsync(ct) ?? "";
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(ct))) { } // pomijamy nagłówki

            string[] parts = requestLine.Split(' ');
            string target = parts.Length > 1 ? parts[1] : "";
            bool isCallback = target.StartsWith("/callback", StringComparison.Ordinal);

            string body = isCallback
                ? "<html><body style='font-family:sans-serif;text-align:center;margin-top:80px'>" +
                  "<h2>SpotiFlow: logowanie zakończone</h2><p>Możesz zamknąć tę kartę i wrócić do aplikacji.</p></body></html>"
                : "";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header = $"HTTP/1.1 {(isCallback ? "200 OK" : "404 Not Found")}\r\n" +
                            "Content-Type: text/html; charset=utf-8\r\n" +
                            $"Content-Length: {bodyBytes.Length}\r\n" +
                            "Connection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header), ct);
            await stream.WriteAsync(bodyBytes, ct);

            if (isCallback)
                return HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + target).Query);
        }
    }

    public static string CreateCodeVerifier()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        return RandomNumberGenerator.GetString(chars, 64);
    }

    public static string CreateCodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }
}
