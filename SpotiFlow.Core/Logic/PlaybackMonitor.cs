using System.Net;
using SpotiFlow.Spotify;

namespace SpotiFlow.Logic;

public class PlaybackMonitor
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1.5);

    private readonly SpotifyApiClient _spotify;
    private readonly RecommendationEngine _engine;
    private readonly PlayHistory _history;

    // Śledzony utwór i postęp odsłuchu, przewiniecie w tył nie zmniejsza
    private SpotifyTrack? _currentTrack;
    private int _maxProgressMs;

    // Ostatni utwór dodany przez SpotiFlow do kolejki
    private string? _lastQueuedId;

    public event Action<string>? Log;
    public event Action<string>? StatusChanged;
    public event Action<SpotifyTrack, int>? TrackProgress;
    public event Action<SpotifyTrack, AudioProfile?>? NowPlaying;
    public event Action<SpotifyTrack, double, ListeningDecision>? TrackJudged;
    public event Action<Recommendation>? TrackQueued;
    public event Action<string>? FatalError;

    public PlaybackMonitor(SpotifyApiClient spotify, RecommendationEngine engine, PlayHistory history)
    {
        _spotify = spotify;
        _engine = engine;
        _history = history;
        _engine.Log += message => Log?.Invoke(message);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Log?.Invoke("Start monitorowania.");
        _currentTrack = null;

        while (!ct.IsCancellationRequested)
        {
            TimeSpan delay = Interval;
            try
            {
                await PollOnceAsync(ct);
            }
            catch (SpotifyApiException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                delay = ex.RetryAfter ?? TimeSpan.FromSeconds(30);
                Log?.Invoke($"! Za dużo zapytań (429). Pauza na {delay.TotalSeconds:0} s.");
            }
            catch (SpotifyApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                FatalError?.Invoke("Brak uprawnień (403). Wymagane jest Spotify Premium.\n\n" + ex.Message);
                return;
            }
            catch (SpotifyApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                FatalError?.Invoke("Sesja wygasła (401). Zaloguj się ponownie.\n\n" + ex.Message);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"! Błąd: {ex.Message}");
            }

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log?.Invoke("Monitorowanie zatrzymane.");
    }

    public async Task PollOnceAsync(CancellationToken ct)
    {
        PlaybackState? state = await _spotify.GetPlaybackStateAsync(ct);
        if (state == null)
        {
            StatusChanged?.Invoke("Brak aktywnej sesji Spotify - uruchom Spotify.");
            return;
        }
        if (state.CurrentlyPlayingType != "track" || state.Item == null)
        {
            StatusChanged?.Invoke("Podcasty nie są oceniane - odpal muzykę.");
            return;
        }

        SpotifyTrack track = state.Item;
        int progress = state.ProgressMs ?? 0;
        StatusChanged?.Invoke(state.IsPlaying ? "Odtwarzanie - monitoruję." : "Wstrzymano odtwarzanie.");

        // Przy pierwszym utworze odrazu dodawany jest kawałek do kolejki z podobnymi wartościami
        if (_currentTrack == null)
        {
            await StartTrackingAsync(track, progress, ct);
            await QueueNextAsync(track.Id, track.Name, ListeningDecision.Engaged, ct);
            return;
        }

        if (track.Id == _currentTrack.Id)
        {
            _maxProgressMs = Math.Max(_maxProgressMs, progress);
            TrackProgress?.Invoke(track, _maxProgressMs);
            return;
        }

        SpotifyTrack finished = _currentTrack;
        double w = ListeningAnalyzer.CalculateListenRatio(_maxProgressMs, finished.DurationMs); // W = t / T
        ListeningDecision decision = ListeningAnalyzer.Decide(w);
        TrackJudged?.Invoke(finished, w, decision);

        await StartTrackingAsync(track, progress, ct);

        await QueueNextAsync(finished.Id, finished.Name, decision, ct);
    }

    private async Task StartTrackingAsync(SpotifyTrack track, int progress, CancellationToken ct)
    {
        _currentTrack = track;
        _maxProgressMs = progress;
        _history.Remember(track.Id);

        AudioProfile? profile = await _engine.GetProfileAsync(track.Id, ct); // atrybuty do Teraz gra
        NowPlaying?.Invoke(track, profile);
        TrackProgress?.Invoke(track, progress);
    }

    private async Task QueueNextAsync(string sourceId, string sourceName, ListeningDecision decision, CancellationToken ct)
    {
        Recommendation? next = await _engine.FindNextTrackAsync(sourceId, sourceName, decision, ct);
        if (next == null)
            return;

        if (await OurTrackIsWaitingAsync(ct))
            return;

        await _spotify.AddToQueueAsync(next.Uri, ct);
        _history.Remember(next.SpotifyId);
        _lastQueuedId = next.SpotifyId;
        TrackQueued?.Invoke(next);
    }

    private async Task<bool> OurTrackIsWaitingAsync(CancellationToken ct)
    {
        if (_lastQueuedId == null)
            return false;

        QueueState? queue = await _spotify.GetQueueAsync(ct);
        SpotifyTrack? waiting = queue?.Queue.FirstOrDefault(t => t.Id == _lastQueuedId);
        if (waiting != null)
            Log?.Invoke($"W kolejce czeka już „{waiting.Name}” od SpotiFlow");
        return waiting != null;
    }
}
