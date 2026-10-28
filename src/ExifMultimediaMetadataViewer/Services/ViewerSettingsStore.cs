using System.Text.Json;

namespace ExifMultimediaMetadataViewer.Services;

public sealed record ViewerSettings
{
    public double SplitRatio { get; init; } = 0.49;
    public string BrowserSortMode { get; init; } = "名称";
    public bool IsThumbnailView { get; init; } = true;
    public bool IsBrowserExpanded { get; init; }
}

public sealed class ViewerSettingsStore
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EXIF Multimedia Metadata Viewer",
        "viewer-settings.json");

    public ViewerSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new ViewerSettings();
            var settings = JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(_settingsPath));
            return settings is null || !double.IsFinite(settings.SplitRatio)
                ? new ViewerSettings()
                : settings with { SplitRatio = Math.Clamp(settings.SplitRatio, 0.3, 0.7) };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ViewerSettings();
        }
    }

    public void Save(ViewerSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
