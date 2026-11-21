using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.ViewModels;
public sealed class BrowserEntry(string fullPath, bool isDirectory, long size, DateTime modifiedTime) : ObservableObject
{
    public string FullPath { get; } = fullPath;
    public bool IsDirectory { get; } = isDirectory;
    public long Size { get; } = size;
    public DateTime ModifiedTime { get; } = modifiedTime;
    private string? previewStatus;
    public string PreviewStatus => previewStatus ?? (IsDirectory ? "文件夹" : MediaAnalyzer.IsKnownMediaFile(FullPath) ? "等待预览" : "不支持预览");
    internal void ReportPreview(string status) => SetProperty(ref previewStatus, status, nameof(PreviewStatus));
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar));
    public string Extension => Path.GetExtension(FullPath);
    public string TypeGlyph => IsDirectory ? "文件夹" : MediaAnalyzer.IsVideoFile(FullPath) ? "视频" : MediaAnalyzer.IsImageFile(FullPath) ? "照片" : "不支持";
    public string SizeText => IsDirectory ? "—" : Size < 1024 ? $"{Size} B" : Size < 1048576 ? $"{Size / 1024d:0.#} KB" : $"{Size / 1048576d:0.##} MB";
    public string ModifiedText => ModifiedTime.ToString("yyyy-MM-dd HH:mm");
    public string Detail => $"{TypeGlyph}  {Extension}  ·  {SizeText}  ·  {ModifiedText}";
    public string Hint => $"{FullPath}\n{Detail}";
}
