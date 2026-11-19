using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SpotiFlow.Logic;
using SpotiFlow.ReccoBeats;
using SpotiFlow.Spotify;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace SpotiFlow.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) }; // jeden klient Http bo atak zalecy microsoft

    private const int MaxHistoryItems = 60;

    // Kolory
    private static readonly Brush GreenBrush = Solid(0x1D, 0xB9, 0x54);
    private static readonly Brush OrangeBrush = Solid(0xFF, 0x9F, 0x43);
    private static readonly Brush YellowBrush = Solid(0xF5, 0xC4, 0x51);
    private static readonly Brush BlueBrush = Solid(0x64, 0xAA, 0xFF);
    private static readonly Brush RedBrush = Solid(0xF1, 0x5E, 0x6C);
    private static readonly Brush GrayBrush = Solid(0xA0, 0xA0, 0xA0);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SpotifyApiClient _spotify = new(Http);
    private readonly SpotifyCoverService _covers = new(Http);
    private readonly PlaybackMonitor _monitor;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();

    private readonly Dictionary<string, HistoryItem> _playedItems = new();
    private HistoryItem? _nowPlayingItem;

    private CancellationTokenSource? _monitorCts;

    public MainViewModel()
    {
        var history = new PlayHistory();
        var engine = new RecommendationEngine(new ReccoBeatsClient(Http), history);
        _monitor = new PlaybackMonitor(_spotify, engine, history);

        _monitor.Log += message => RunOnUi(() => AddMessage(message));
        _monitor.StatusChanged += status => RunOnUi(() => Status = status);
        _monitor.NowPlaying += (track, profile) => RunOnUi(() => ShowNowPlaying(track, profile));
        _monitor.TrackProgress += (track, progressMs) => RunOnUi(() => ShowProgress(track, progressMs));
        _monitor.TrackJudged += (track, w, decision) => RunOnUi(() => ShowVerdict(track, w, decision));
        _monitor.TrackQueued += recommendation => RunOnUi(() => AddQueuedItem(recommendation));
        _monitor.FatalError += message => RunOnUi(() => ShowFatalError(message));

        // Wartości początkowe
        ClientId = _settings.ClientId;
        LoginStatus = "Niezalogowano";
        LoginStatusBrush = GrayBrush;
        NowTitle = "Nic nie gra";
        NowArtist = "-";
        NowRatioText = "W = -";
        NowAttributes = AttributeView.From(null);
        Status = "Zaloguj się, oraz kliknij Start.";
        ErrorMessage = "";
    }

    public string RedirectUri => SpotifyAuthService.RedirectUri;

    [ObservableProperty]
    public partial string CopyButtonText { get; set; } = "Kopiuj";

    public ObservableCollection<HistoryItem> History { get; } = new();

    [ObservableProperty]
    public partial string ClientId { get; set; }

    [ObservableProperty]
    public partial string LoginStatus { get; set; }

    [ObservableProperty]
    public partial Brush LoginStatusBrush { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string NowTitle { get; set; }

    [ObservableProperty]
    public partial string NowArtist { get; set; }

    [ObservableProperty]
    public partial ImageSource? NowCover { get; set; }

    [ObservableProperty]
    public partial double NowRatio { get; set; }

    [ObservableProperty]
    public partial string NowRatioText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<AttributeView> NowAttributes { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [RelayCommand]
    private async Task LoginAsync()
    {
        string clientId = ClientId.Trim();
        if (clientId.Length == 0)
        {
            SetLoginStatus("Wklej Client ID z developer.spotify.com", OrangeBrush);
            return;
        }

        _settings.ClientId = clientId;
        _settings.Save();

        SetLoginStatus("Dokończ logowanie w przeglądarce…", YellowBrush);
        try
        {
            var auth = new SpotifyAuthService(Http, clientId);
            SpotifyToken token = await auth.LoginAsync(CancellationToken.None);
            _spotify.SetSession(auth, token);

            SetLoginStatus("Zalogowano", GreenBrush);
            IsLoggedIn = true;
            AddMessage("Zalogowano do Spotify.");
        }
        catch (OperationCanceledException)
        {
            SetLoginStatus("Timeout logowania - spróbuj ponownie.", RedBrush);
            AddMessage($"! Nie dokończono logowania w ciągu {SpotifyAuthService.LoginTimeout.TotalSeconds:0} s.");
        }
        catch (Exception ex)
        {
            SetLoginStatus("Błąd logowania", RedBrush);
            AddMessage("! " + ex.Message);
        }
    }

    private bool CanStart() => IsLoggedIn && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsErrorOpen = false;
        _monitorCts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            await _monitor.RunAsync(_monitorCts.Token); // pętla działa do Stop
        }
        finally
        {
            _monitorCts.Dispose();
            _monitorCts = null;
            IsRunning = false;
        }
    }

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _monitorCts?.Cancel();
        Status = "Monitorowanie zatrzymane.";
    }

    public IAsyncRelayCommand CopyRedirectUriCommand => _copyRedirectUriCommand ??= new AsyncRelayCommand(CopyRedirectUriAsync);
    private IAsyncRelayCommand? _copyRedirectUriCommand;

    private async Task CopyRedirectUriAsync()
    {
        var package = new DataPackage();
        package.SetText(RedirectUri);
        Clipboard.SetContent(package);

        CopyButtonText = "Skopiowano"; // zmiana po kliknieciu na 2sek
        await Task.Delay(2000);
        CopyButtonText = "Kopiuj";
    }

    [RelayCommand]
    private void ClearHistory()
    {
        History.Clear();
        _playedItems.Clear();
        _nowPlayingItem = null;
    }

    public void Shutdown() => _monitorCts?.Cancel();


    private void ShowNowPlaying(SpotifyTrack track, AudioProfile? profile)
    {
        NowTitle = track.Name;
        NowArtist = track.ArtistNames;
        NowAttributes = AttributeView.From(profile);
        NowCover = null;

        // Poprzedni utwór bez werdytktu np. przez brak wartości badgeujemy jak oodtworzony
        if (_nowPlayingItem != null && _nowPlayingItem.TrackId != track.Id && _nowPlayingItem.Badge.StartsWith("TERAZ"))
        {
            _nowPlayingItem.Badge = $"ODTWORZONY  -  {DateTime.Now:HH:mm}";
            _nowPlayingItem.BadgeBrush = GrayBrush;
        }

        string badge = $"TERAZ GRA  -  {DateTime.Now:HH:mm}";
        HistoryItem? item = History.FirstOrDefault(h => h.IsTrack && h.TrackId == track.Id);
        if (item != null)
        {
            item.UpdateProfile(profile);
            item.Badge = badge;
            item.BadgeBrush = GreenBrush;
            History.Remove(item);
            AddToHistory(item);
        }
        else
        {
            item = HistoryItem.ForTrack(track.Id, track.Name, track.ArtistNames, profile, badge, GreenBrush);
            AddToHistory(item);
        }
        _playedItems[track.Id] = item;
        _nowPlayingItem = item;

        HistoryItem playing = item;
        if (playing.Cover != null)
            NowCover = playing.Cover;
        else
            _ = LoadCoverAsync(track.Id, cover =>
            {
                playing.Cover = cover;
                if (NowTitle == track.Name)
                    NowCover = cover;
            });
    }

    private void ShowProgress(SpotifyTrack track, int progressMs)
    {
        NowRatio = ListeningAnalyzer.CalculateListenRatio(progressMs, track.DurationMs);
        NowRatioText = $"W = {NowRatio:0.00}";
    }

    private void ShowVerdict(SpotifyTrack track, double w, ListeningDecision decision)
    {
        if (!_playedItems.TryGetValue(track.Id, out HistoryItem? item))
            return;

        (item.Badge, item.BadgeBrush) = decision == ListeningDecision.Skipped
            ? ($"POMINIĘTY  -  W = {w:0.00}  ->  szukam przeciwieństwa", OrangeBrush)
            : ($"WYSŁUCHANY  -  W = {w:0.00}  ->  szukam podobnego", GreenBrush);
    }

    private void AddQueuedItem(Recommendation recommendation)
    {
        foreach (HistoryItem old in History.Where(h => h.IsTrack && h.TrackId == recommendation.SpotifyId).ToList())
            History.Remove(old);

        var item = HistoryItem.ForQueued(recommendation, $"DODANO DO KOLEJKI  -  {DateTime.Now:HH:mm}", BlueBrush);
        AddToHistory(item);
        _ = LoadCoverAsync(recommendation.SpotifyId, cover => item.Cover = cover);
    }

    private void ShowFatalError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
        Status = message.Split('\n')[0];
        SetLoginStatus("Wymagane ponowne logowanie", RedBrush);
        IsLoggedIn = false;
        AddMessage("! " + message.Split('\n')[0]);
    }

    private void AddMessage(string message)
    {
        bool isError = message.StartsWith('!');
        AddToHistory(HistoryItem.ForMessage($"{DateTime.Now:HH:mm:ss}    {message.TrimStart('!', ' ')}",
                                            isError ? RedBrush : GrayBrush));
    }

    private void AddToHistory(HistoryItem item)
    {
        History.Insert(0, item);
        while (History.Count > MaxHistoryItems)
            History.RemoveAt(History.Count - 1);
    }

    private async Task LoadCoverAsync(string trackId, Action<ImageSource> apply)
    {
        string? url = await _covers.GetCoverUrlAsync(trackId);
        if (url != null)
            apply(new BitmapImage(new Uri(url)));
    }

    private void SetLoginStatus(string text, Brush brush)
    {
        LoginStatus = text;
        LoginStatusBrush = brush;
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.HasThreadAccess) // akcje z innych wątków dodaje do sipatchera
            action();
        else
            _dispatcher.TryEnqueue(() => action());
    }

    private static SolidColorBrush Solid(byte r, byte g, byte b) => new(Color.FromArgb(255, r, g, b));
}
