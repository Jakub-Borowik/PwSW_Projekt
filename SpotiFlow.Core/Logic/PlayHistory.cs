namespace SpotiFlow.Logic;

public class PlayHistory
{
    private readonly Dictionary<string, DateTime> _lastSeen = new();
    private readonly TimeSpan _window;
    private readonly Func<DateTime> _clock;

    /// <param name="window">Jak długo pamięta utwór (1 godzina)</param>
    /// <param name="clock">Źródło czasu</param>
    public PlayHistory(TimeSpan? window = null, Func<DateTime>? clock = null)
    {
        _window = window ?? TimeSpan.FromHours(1);
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public void Remember(string trackId)
    {
        if (!string.IsNullOrEmpty(trackId))
            _lastSeen[trackId] = _clock();
    }

    public bool WasRecentlyPlayed(string trackId)
    {
        RemoveExpired();
        return _lastSeen.ContainsKey(trackId);
    }

    public int Count
    {
        get { RemoveExpired(); return _lastSeen.Count; }
    }

    private void RemoveExpired()
    {
        DateTime limit = _clock() - _window;
        foreach (string id in _lastSeen.Where(kv => kv.Value < limit).Select(kv => kv.Key).ToList())
            _lastSeen.Remove(id);
    }
}
