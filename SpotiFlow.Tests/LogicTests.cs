using SpotiFlow.Logic;

namespace SpotiFlow.Tests;

public class LogicTests
{
    [Fact]
    public void CalculateListenRatio_IsListenedDividedByDuration()
    {
        // W = t / T = 10 000 / 200 000 = 0,05
        Assert.Equal(0.05, ListeningAnalyzer.CalculateListenRatio(10_000, 200_000), precision: 6);

        // Brak długości utworu -> 0 (bez dzielenia przez zero).
        Assert.Equal(0.0, ListeningAnalyzer.CalculateListenRatio(5_000, 0));
    }

    [Fact]
    public void Decide_ThresholdIs25Percent()
    {
        Assert.Equal(ListeningDecision.Skipped, ListeningAnalyzer.Decide(0.24));
        Assert.Equal(ListeningDecision.Engaged, ListeningAnalyzer.Decide(0.25)); // granica = wysłuchany
    }

    [Fact]
    public void Inverted_IsOneMinusA()
    {
        // A = (0,80 · 0,90 · 0,70 · 0,10)  ->  1 − A = (0,20 · 0,10 · 0,30 · 0,90)
        var inverted = new AudioProfile(0.8, 0.9, 0.7, 0.1).Inverted();

        Assert.Equal(0.2, inverted.Danceability, precision: 6);
        Assert.Equal(0.1, inverted.Energy, precision: 6);
        Assert.Equal(0.3, inverted.Valence, precision: 6);
        Assert.Equal(0.9, inverted.Acousticness, precision: 6);
    }

    [Fact]
    public void DistanceTo_SumsAbsoluteDifferences()
    {
        var target = new AudioProfile(0.2, 0.1, 0.3, 0.9);

        // C = 0,10 + 0,10 + 0 + 0,10 = 0,30;  D = 0,55 + 0,75 + 0,35 + 0,75 = 2,40
        Assert.Equal(0.30, new AudioProfile(0.3, 0.2, 0.3, 0.8).DistanceTo(target), precision: 6);
        Assert.Equal(2.40, new AudioProfile(0.75, 0.85, 0.65, 0.15).DistanceTo(target), precision: 6);
    }

    [Fact]
    public void PlayHistory_ForgetsTracksAfterOneHour()
    {
        DateTime now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var history = new PlayHistory(clock: () => now); // sztuczny zegar

        history.Remember("abc");
        Assert.True(history.WasRecentlyPlayed("abc"));

        now = now.AddMinutes(61);
        Assert.False(history.WasRecentlyPlayed("abc"));
    }
}
