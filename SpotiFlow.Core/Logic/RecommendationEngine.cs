using SpotiFlow.ReccoBeats;

namespace SpotiFlow.Logic;

public record Recommendation(string SpotifyId, string Title, string Artists, AudioProfile? Profile)
{
    public string Uri => "spotify:track:" + SpotifyId;
}

public class RecommendationEngine
{
    private const int CandidateCount = 20;

    private readonly ReccoBeatsClient _recco;
    private readonly PlayHistory _history;

    // Pamięć atrybutów, aby niepotrzebnie nie pytac ReccoBeats
    private readonly Dictionary<string, AudioProfile> _profiles = new();

    public event Action<string>? Log;

    public RecommendationEngine(ReccoBeatsClient recco, PlayHistory history)
    {
        _recco = recco;
        _history = history;
    }

    public async Task<AudioProfile?> GetProfileAsync(string trackId, CancellationToken ct)
    {
        await LoadProfilesAsync(new[] { trackId }, ct);
        return _profiles.GetValueOrDefault(trackId);
    }

    public async Task<Recommendation?> FindNextTrackAsync(string sourceId, string sourceName,
                                                          ListeningDecision decision, CancellationToken ct)
    {
        AudioProfile? old = await GetProfileAsync(sourceId, ct);
        if (old == null)
        {
            Log?.Invoke($"ReccoBeats nie zna atrybutów „{sourceName}” - brak propozycji.");
            return null;
        }

        AudioProfile target = decision == ListeningDecision.Skipped
            ? old.Inverted()
            : old;
        Log?.Invoke($"Szukam {(decision == ListeningDecision.Skipped ? "przeciwieństwa" : "podobnego")}: {target}");

        List<RecommendedTrack> candidates = await _recco.GetRecommendationsAsync(sourceId, target, CandidateCount, ct);

        candidates = candidates
            .Where(c => c.SpotifyId != sourceId && !_history.WasRecentlyPlayed(c.SpotifyId)) // excluduje utwory z historii i ten co właśnie grał
            .ToList();
        if (candidates.Count == 0)
        {
            Log?.Invoke("ReccoBeats nie dało żadnego nowego kandydata.");
            return null;
        }

        await LoadProfilesAsync(candidates.Select(c => c.SpotifyId), ct);
        RecommendedTrack best = ChooseClosest(candidates, _profiles, target);
        AudioProfile? bestProfile = _profiles.GetValueOrDefault(best.SpotifyId);

        if (bestProfile != null)
            Log?.Invoke($"Najbliższym utworem jest „{best.Title}” (odległość {bestProfile.DistanceTo(target):0.00}).");

        return new Recommendation(best.SpotifyId, best.Title, best.Artists, bestProfile);
    }
    // utwory bez atarybutów dostają wartość infinity, więc będą wybrane tylko kiedy nie będzie innych
    public static RecommendedTrack ChooseClosest(IReadOnlyList<RecommendedTrack> candidates,
                                                 IReadOnlyDictionary<string, AudioProfile> profiles,
                                                 AudioProfile target)
    {
        return candidates
            .OrderBy(c => profiles.TryGetValue(c.SpotifyId, out AudioProfile? p) ? p.DistanceTo(target) : double.MaxValue)
            .First();
    }
    // Pobieranie atrybutów audio których jeszcze nie ma w cache'u
    private async Task LoadProfilesAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var missing = ids.Where(id => !_profiles.ContainsKey(id)).Distinct().ToList();
        if (missing.Count == 0)
            return;
        try
        {
            foreach (var (id, profile) in await _recco.GetAudioFeaturesAsync(missing, ct))
                _profiles[id] = profile;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.Invoke($"! ReccoBeats niedostępne ({ex.Message}).");
        }
    }
}
