using System.Windows.Media.Imaging;
using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.ViewModels;

public sealed class BrowserEntry : ObservableObject
{
    private BitmapSource? _thumbnail;
    private bool _isThumbnailLoading;
    private bool _thumbnailLoaded;
    private int? _imageWidth;
    private int? _imageHeight;

    public BrowserEntry(string fullPath, bool isDirectory, long size, DateTime modifiedTime)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Size = size;
        ModifiedTime = modifiedTime;
    }

    public string FullPath { get; }
    public string Name => IsDirectory ? Path.GetFileName(FullPath.TrimEnd(Path.DirectorySeparatorChar)) : Path.GetFileName(FullPath);
    public bool IsDirectory { get; }
    public long Size { get; }
    public DateTime ModifiedTime { get; }
    public string Extension => IsDirectory ? "" : Path.GetExtension(FullPath);
    public string SizeText => IsDirectory ? "" : FormatSize(Size);
    public string ModifiedText => ModifiedTime.ToString("yyyy-MM-dd HH:mm");
    public string SizeAndDateText => IsDirectory ? "文件夹" : $"{SizeText} · {ModifiedText}";
    public string TypeLabel => IsDirectory
        ? "文件夹 / Folder"
        : MediaAnalyzer.IsVideoFile(FullPath)
            ? "视频 / Video"
            : MediaAnalyzer.IsImageFile(FullPath)
                ? "照片 / Image"
                : "不支持 / Unsupported";
    public string TypeGlyph => IsDirectory ? "文件夹" : MediaAnalyzer.IsVideoFile(FullPath) ? "视频" : MediaAnalyzer.IsImageFile(FullPath) ? "照片" : "文件";
    public string PreviewStatusText => IsDirectory
        ? "文件夹"
        : MediaAnalyzer.IsKnownMediaFile(FullPath)
            ? "可尝试预览"
            : "不支持处理";
    public string PreviewSizeText => _imageWidth is > 0 && _imageHeight is > 0
        ? $"{_imageWidth} × {_imageHeight}"
        : "";
    public BitmapSource? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }
    public bool IsThumbnailLoading { get => _isThumbnailLoading; private set => SetProperty(ref _isThumbnailLoading, value); }

    public bool TryBeginThumbnailLoad()
    {
        if (IsDirectory || _thumbnailLoaded || IsThumbnailLoading || !MediaAnalyzer.IsKnownMediaFile(FullPath))
            return false;
        IsThumbnailLoading = true;
        return true;
    }

    public void CompleteThumbnailLoad(BitmapSource? thumbnail, int? imageWidth, int? imageHeight)
    {
        Thumbnail = thumbnail;
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
        _thumbnailLoaded = true;
        IsThumbnailLoading = false;
        OnPropertyChanged(nameof(PreviewSizeText));
    }

    public void CancelThumbnailLoad() => IsThumbnailLoading = false;

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var index = 0;
        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return $"{size:0.##} {units[index]}";
    }
}
