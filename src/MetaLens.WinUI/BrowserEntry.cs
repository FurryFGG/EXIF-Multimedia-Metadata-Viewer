using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.ViewModels;
public sealed record BrowserEntry(string FullPath, bool IsDirectory, long Size, DateTime ModifiedTime)
{
    public string Name => Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar));
    public string Extension => Path.GetExtension(FullPath);
    public string TypeGlyph => IsDirectory ? "文件夹" : MediaAnalyzer.IsVideoFile(FullPath) ? "视频" : MediaAnalyzer.IsImageFile(FullPath) ? "照片" : "文件";
    public string SizeText => IsDirectory ? "—" : Size < 1024 ? $"{Size} B" : Size < 1048576 ? $"{Size / 1024d:0.#} KB" : $"{Size / 1048576d:0.##} MB";
    public string ModifiedText => ModifiedTime.ToString("yyyy-MM-dd HH:mm");
    public string Detail => $"{TypeGlyph}  {Extension}  ·  {SizeText}  ·  {ModifiedText}";
    public string Hint => $"{FullPath}\n{Detail}";
}
