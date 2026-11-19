using System.Text.Json;

namespace SpotiFlow;

public class AppSettings
{
    /// <summary>Client ID aplikacji z developer.spotify.com to jedyne co jest zapisywane, token przy kazdym odpaleniu na nowo</summary>
    public string ClientId { get; set; } = "";

    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpotiFlow");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {

        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(FolderPath);
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
