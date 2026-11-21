using System.Text.Json;

namespace ExifMultimediaMetadataViewer.Services;

public sealed record ViewerSettings
{
    public int SettingsVersion { get; init; } = 3;
    public WorkspaceLayout? Layout { get; init; }
    public double SplitRatio { get; init; } = 0.49;
    public double BrowserTreeWidth { get; init; } = 260;
    public double BrowserHeight { get; init; } = 300;
    public double ThumbnailSize { get; init; } = 150;
    public string BrowserSortMode { get; init; } = "名称";
    public string BrowserViewMode { get; init; } = "缩略图";
    public bool IsBrowserExpanded { get; init; } = true;
    public bool IsDarkTheme { get; init; }
}

public sealed class ViewerSettingsStore : IDisposable
{
    private readonly object _saveLock = new();
    private readonly Timer _saveTimer;
    private ViewerSettings? _pendingSettings;
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EXIF Multimedia Metadata Viewer",
        "viewer-settings.json");

    public ViewerSettingsStore()
    {
        _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public ViewerSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new ViewerSettings();
            var settings = JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(_settingsPath));
            return settings is null || settings.SettingsVersion < 2 || !double.IsFinite(settings.SplitRatio)
                ? new ViewerSettings()
                : settings with
                {
                    SplitRatio = Math.Clamp(settings.SplitRatio, 0.3, 0.7),
                    BrowserTreeWidth = Math.Clamp(settings.BrowserTreeWidth, 170, 520),
                    BrowserHeight = Math.Clamp(settings.BrowserHeight, 190, 620),
                    ThumbnailSize = Math.Clamp(settings.ThumbnailSize, 96, 260),
                    BrowserViewMode = settings.BrowserViewMode is "缩略图" or "列表" or "详细信息"
                        ? settings.BrowserViewMode
                        : "缩略图"
                };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ViewerSettings();
        }
    }

    public void Save(ViewerSettings settings)
    {
        lock (_saveLock)
        {
            _pendingSettings = settings;
            _saveTimer.Change(TimeSpan.FromMilliseconds(600), Timeout.InfiniteTimeSpan);
        }
    }

    public void Flush()
    {
        ViewerSettings? settings;
        lock (_saveLock)
        {
            settings = _pendingSettings;
            _pendingSettings = null;
        }
        if (settings is null)
            return;
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Settings persistence must never take down the application from a timer callback.
        }
    }

    public void Dispose()
    {
        _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Flush();
        _saveTimer.Dispose();
    }
}
