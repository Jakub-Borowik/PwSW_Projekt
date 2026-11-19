using Microsoft.UI.Xaml;

namespace SpotiFlow;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent(); // wczytanie zasobów z Xamla
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
