using System.Text.Json;

namespace ExifMultimediaMetadataViewer.Services;

public sealed class RecentFilesStore
{
    private const int MaximumEntries = 10;
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EXIF Multimedia Metadata Viewer",
        "recent-files.json");

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return [];

            var paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_settingsPath)) ?? [];
            return paths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumEntries)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<string> paths)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        var entries = paths.Take(MaximumEntries).ToArray();
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }
}
