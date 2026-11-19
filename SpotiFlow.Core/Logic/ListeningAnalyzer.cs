namespace SpotiFlow.Logic;

public enum ListeningDecision
{
    Skipped,
    Engaged,
}

public static class ListeningAnalyzer
{
    public const double SkipThreshold = 0.25;

    public static double CalculateListenRatio(int listenedMs, int durationMs)
    {
        if (durationMs <= 0)
            return 0;
        return Math.Clamp((double)listenedMs / durationMs, 0, 1);
    }

    public static ListeningDecision Decide(double listenRatio)
    {
        return listenRatio < SkipThreshold ? ListeningDecision.Skipped : ListeningDecision.Engaged;
    }
}
