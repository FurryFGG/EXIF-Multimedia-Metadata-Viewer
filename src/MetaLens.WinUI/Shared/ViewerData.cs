namespace ExifMultimediaMetadataViewer.Services;

internal static class ViewerData
{
    // A private profile lets release QA run without changing a real user's history or splitter settings.
    internal static string Root { get; } = Resolve();
    private static string Resolve()
    {
        if (Environment.GetEnvironmentVariable("METALENS_PROFILE_DIR") is { Length: > 0 } qa && Path.IsPathFullyQualified(qa)) return qa;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(local, "EXIF查看器", "WinUI-2.0.1");
        try
        {
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
                foreach (var name in new[] { "viewer-settings.json", "recent-files.json", "winui-tile-sizes.json" })
                {
                    var old = Path.Combine(local, "EXIF Multimedia Metadata Viewer",name);
                    if (File.Exists(old)) File.Copy(old, Path.Combine(root,name), false);
                }
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return root;
    }
}
