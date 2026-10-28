using ExifMetadata.Core.Models;

namespace ExifMetadata.Core.Services;

public sealed class MediaAnalyzer
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".avi", ".m2t", ".m2ts", ".m2v", ".m4v", ".mkv", ".mov",
        ".mp4", ".mpe", ".mpeg", ".mpg", ".mts", ".mxf", ".ts", ".vob", ".webm", ".wmv"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif", ".bmp", ".dng", ".gif", ".heic", ".heif", ".jpeg", ".jpg",
        ".nef", ".orf", ".pef", ".png", ".raf", ".raw", ".rw2", ".tif", ".tiff",
        ".webp", ".arw", ".cr2", ".cr3"
    };

    private readonly ExifToolService _exifTool;
    private readonly FFprobeService _ffprobe;

    public MediaAnalyzer(
        ExifToolService? exifTool = null,
        FFprobeService? ffprobe = null)
    {
        _exifTool = exifTool ?? new ExifToolService();
        _ffprobe = ffprobe ?? new FFprobeService();
    }

    public async Task<MediaAnalysis> AnalyzeAsync(
        string mediaPath,
        string exifToolPath,
        string ffprobePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(mediaPath);
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
            throw new FileNotFoundException("找不到要读取的媒体文件。", fullPath);

        var exifTask = _exifTool.ExtractAsync(exifToolPath, fullPath, cancellationToken: cancellationToken);
        var extension = Path.GetExtension(fullPath);
        var shouldProbeVideo = VideoExtensions.Contains(extension)
            || !ImageExtensions.Contains(extension);
        var ffprobeTask = shouldProbeVideo
            ? _ffprobe.ProbeAsync(ffprobePath, fullPath, cancellationToken: cancellationToken)
            : Task.FromResult(new ParserResult
            {
                ParserName = FFprobeService.ParserName,
                Status = ParserStatus.NotRun,
                Message = "该文件扩展名属于常见图片类型，本次未启动视频探测。"
            });

        await Task.WhenAll(exifTask, ffprobeTask).ConfigureAwait(false);
        var exifResult = await exifTask.ConfigureAwait(false);
        var ffprobeResult = await ffprobeTask.ConfigureAwait(false);

        return new MediaAnalysis
        {
            FilePath = fullPath,
            FileSize = fileInfo.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
            AnalyzedAtUtc = DateTime.UtcNow,
            ExifTool = exifResult,
            FFprobe = ffprobeResult,
            AllFields = exifResult.Fields.Concat(ffprobeResult.Fields).ToArray()
        };
    }

    public static bool IsKnownMediaFile(string path)
    {
        var extension = Path.GetExtension(path);
        return ImageExtensions.Contains(extension) || VideoExtensions.Contains(extension);
    }

    public static bool IsVideoFile(string path) => VideoExtensions.Contains(Path.GetExtension(path));
}
