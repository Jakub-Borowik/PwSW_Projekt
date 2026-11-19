using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SpotiFlow.Logic;

namespace SpotiFlow.ViewModels;

public record AttributeView(string Name, double Value, string ValueText)
{
    public static IReadOnlyList<AttributeView> From(AudioProfile? p) => new[]
    {
        Make("Taneczność", p?.Danceability),
        Make("Energia", p?.Energy),
        Make("Nastrój", p?.Valence),
        Make("Akustyczność", p?.Acousticness),
    };

    private static AttributeView Make(string name, double? value) =>
        new(name, value ?? 0, value?.ToString("0.00") ?? "-");
}

public partial class HistoryItem : ObservableObject
{
    private HistoryItem() { }

    public string TrackId { get; private init; } = "";
    public bool IsTrack { get; private init; }
    public bool IsMessage => !IsTrack;

    public string Title { get; private init; } = "";
    public string Subtitle { get; private init; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<AttributeView> Attributes { get; set; }

    public string MessageText { get; private init; } = "";
    public Brush? MessageBrush { get; private init; }

    [ObservableProperty]
    public partial string Badge { get; set; }

    [ObservableProperty]
    public partial Brush? BadgeBrush { get; set; }

    [ObservableProperty]
    public partial ImageSource? Cover { get; set; }

    public static HistoryItem ForTrack(string trackId, string title, string artists, AudioProfile? profile,
                                       string badge, Brush badgeBrush) => new()
    {
        TrackId = trackId,
        IsTrack = true,
        Title = title,
        Subtitle = artists,
        Attributes = AttributeView.From(profile),
        Badge = badge,
        BadgeBrush = badgeBrush,
    };

    public static HistoryItem ForQueued(Recommendation queued, string badge, Brush badgeBrush) =>
        ForTrack(queued.SpotifyId, queued.Title, queued.Artists, queued.Profile, badge, badgeBrush);

    public static HistoryItem ForMessage(string text, Brush brush) => new()
    {
        MessageText = text,
        MessageBrush = brush,
        Badge = "",
        Attributes = Array.Empty<AttributeView>(),
    };

    public void UpdateProfile(AudioProfile? profile)
    {
        if (profile != null)
            Attributes = AttributeView.From(profile);
    }
}
