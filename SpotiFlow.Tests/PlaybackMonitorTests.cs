using System.Net;
using System.Text;
using SpotiFlow.Logic;
using SpotiFlow.ReccoBeats;
using SpotiFlow.Spotify;

namespace SpotiFlow.Tests;

public class PlaybackMonitorTests
{
    private const string TrackA = "AAAAAAAAAAAAAAAAAAAAAA";
    private const string TrackB = "BBBBBBBBBBBBBBBBBBBBBB";
    private const string TrackC = "CCCCCCCCCCCCCCCCCCCCCC";
    private const string TrackD = "DDDDDDDDDDDDDDDDDDDDDD";

    [Fact]
    public async Task QuickSkip_QueuesOppositeTrack()
    {
        var handler = new FakeHttpHandler(TrackC, TrackD);
        handler.Player.Enqueue(PlayerJson(TrackA, progressMs: 10_000)); // A: 10 s z 200 s
        handler.Player.Enqueue(PlayerJson(TrackB, progressMs: 1_000));  // gra B -> A pominięty (W = 0,05)

        var monitor = CreateMonitor(handler);
        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        // Cel = 1 − A: energia 0.100, taneczność 0.200.
        Assert.Contains(handler.Requests, u => u.Contains("energy=0.100") && u.Contains("danceability=0.200"));
        // Start: D (podobny do A). Po pominięciu: C (przeciwieństwo A).
        Assert.Equal(new[] { TrackD, TrackC }, handler.QueuedIds);
    }

    [Fact]
    public async Task Listened_QueuesSimilarTrack_AndSkipsRecentlyPlayed()
    {
        var handler = new FakeHttpHandler(TrackC, TrackD, TrackB);
        handler.Player.Enqueue(PlayerJson(TrackA, progressMs: 190_000)); // A: 190 s z 200 s
        handler.Player.Enqueue(PlayerJson(TrackC, progressMs: 1_000));  // gra C -> A wysłuchany (W = 0,95)

        var monitor = CreateMonitor(handler);
        await monitor.PollOnceAsync(CancellationToken.None);
        await monitor.PollOnceAsync(CancellationToken.None);

        // Cel = A (nieodwrócony): energia 0.900.
        Assert.Contains(handler.Requests, u => u.Contains("energy=0.900"));
        // Start: D (najbliższy A). Potem D i C już były (RB-03), więc zostaje B.
        Assert.Equal(new[] { TrackD, TrackB }, handler.QueuedIds);
    }

    [Fact]
    public async Task Forbidden403_StopsLoopWithFatalError()
    {
        var handler = new FakeHttpHandler();
        handler.Player.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden)); // brak Premium

        var monitor = CreateMonitor(handler);
        string? fatal = null;
        monitor.FatalError += message => fatal = message;
        await monitor.RunAsync(CancellationToken.None); // pętla musi zakończyć się sama

        Assert.Contains("403", fatal);
    }


    private static PlaybackMonitor CreateMonitor(FakeHttpHandler handler)
    {
        var http = new HttpClient(handler);
        var spotify = new SpotifyApiClient(http);
        spotify.SetSession(new SpotifyAuthService(http, "test"), new SpotifyToken { AccessToken = "fake" });

        var history = new PlayHistory();
        var engine = new RecommendationEngine(new ReccoBeatsClient(http), history);
        return new PlaybackMonitor(spotify, engine, history);
    }

    private static HttpResponseMessage PlayerJson(string id, int progressMs) => Json($$"""
        { "is_playing": true, "progress_ms": {{progressMs}}, "currently_playing_type": "track",
          "item": { "id": "{{id}}", "name": "Utwór {{id[..1]}}", "duration_ms": 200000, "artists": [] } }
        """);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Udawany internet: Spotify /me/player z kolejki odpowiedzi, ReccoBeats ze stałych danych.</summary>
    private class FakeHttpHandler : HttpMessageHandler
    {
        private static readonly Dictionary<string, (double D, double E, double V, double A)> Tracks = new()
        {
            [TrackA] = (0.8, 0.9, 0.7, 0.1),
            [TrackB] = (0.5, 0.5, 0.5, 0.5),
            [TrackC] = (0.3, 0.2, 0.3, 0.8),
            [TrackD] = (0.75, 0.85, 0.65, 0.15),
        };

        private readonly string[] _recommendations;

        public FakeHttpHandler(params string[] recommendations) => _recommendations = recommendations;

        /// <summary>Kolejne odpowiedzi GET /me/player.</summary>
        public Queue<HttpResponseMessage> Player { get; } = new();

        /// <summary>Wszystkie wysłane adresy.</summary>
        public List<string> Requests { get; } = new();

        /// <summary>ID utworów dodanych do kolejki (POST /me/player/queue?uri=...).</summary>
        public List<string> QueuedIds => Requests
            .Where(u => u.Contains("me/player/queue?uri="))
            .Select(u => Uri.UnescapeDataString(u).Split("spotify:track:")[1])
            .ToList();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string url = request.RequestUri!.ToString();
            Requests.Add(url);

            if (url.Contains("me/player/queue") && request.Method == HttpMethod.Get)  // kolejka zawsze pusta
                return Task.FromResult(Json("""{ "queue": [] }"""));

            if (url.Contains("me/player/queue"))                                       // dodanie do kolejki
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

            if (url.Contains("me/player"))                                             // co gra
                return Task.FromResult(Player.Count > 0 ? Player.Dequeue() : new HttpResponseMessage(HttpStatusCode.NoContent));

            if (url.Contains("audio-features"))                                        // atrybuty
            {
                var items = url.Split("ids=")[1].Split(',').Where(Tracks.ContainsKey).Select(id =>
                    FormattableString.Invariant(
                        $"{{ \"href\": \"https://open.spotify.com/track/{id}\", \"danceability\": {Tracks[id].D}, \"energy\": {Tracks[id].E}, \"valence\": {Tracks[id].V}, \"acousticness\": {Tracks[id].A} }}"));
                return Task.FromResult(Json("{ \"content\": [" + string.Join(",", items) + "] }"));
            }

            if (url.Contains("track/recommendation"))                                  // kandydaci
            {
                var items = _recommendations.Select(id =>
                    $"{{ \"href\": \"https://open.spotify.com/track/{id}\", \"trackTitle\": \"Utwór {id[..1]}\", \"artists\": [] }}");
                return Task.FromResult(Json("{ \"content\": [" + string.Join(",", items) + "] }"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
