namespace ExifMultimediaMetadataViewer.Services;

internal static class ViewerData
{
    // A private profile lets release QA run without changing a real user's history or splitter settings.
    internal static string Root => Environment.GetEnvironmentVariable("METALENS_PROFILE_DIR") is { Length: > 0 } path && Path.IsPathFullyQualified(path)
        ? path : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EXIF Multimedia Metadata Viewer");
}
