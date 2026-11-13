using System.Diagnostics;
using System.Collections.Concurrent;
using System.Windows.Media.Imaging;
using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.Services;

public sealed record PreviewLoadResult(BitmapSource? Image, string Description, bool IsFullResolution);

public sealed class MediaPreviewService
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> ThumbnailCache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<(BitmapSource? Image, int? Width, int? Height)> LoadThumbnailAsync(
        string path,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        var cacheKey = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        if (ThumbnailCache.TryGetValue(cacheKey, out var reference) && reference.TryGetTarget(out var cached))
            return (cached, cached.PixelWidth, cached.PixelHeight);

        try
        {
            BitmapSource? image;
            if (MediaAnalyzer.IsVideoFile(path))
            {
                var frame = await ExtractVideoFrameAsync(ffmpegPath, path, cancellationToken).ConfigureAwait(false);
                image = Decode(frame, 280);
            }
            else
            {
                image = await Task.Run(() => DecodeFile(path, 280), cancellationToken).ConfigureAwait(false);
            }
            if (image is not null)
                ThumbnailCache[cacheKey] = new WeakReference<BitmapSource>(image);
            return (image, image?.PixelWidth, image?.PixelHeight);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException or System.ComponentModel.Win32Exception)
        {
            return (null, null, null);
        }
    }

    public async Task<PreviewLoadResult> LoadAsync(
        MediaAnalysis analysis,
        string exifToolPath,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        if (MediaAnalyzer.IsVideoFile(analysis.FilePath))
        {
            if (!File.Exists(ffmpegPath))
                return new PreviewLoadResult(null, "视频预览不可用：未找到 FFmpeg。", false);
            var frame = await ExtractVideoFrameAsync(ffmpegPath, analysis.FilePath, cancellationToken)
                .ConfigureAwait(false);
            var videoImage = Decode(frame, null);
            return new PreviewLoadResult(videoImage,
                videoImage is null ? "视频首帧解码失败；元数据仍可查看。" : "视频首帧预览 · FFmpeg 缩放至最长边约 1600 像素",
                false);
        }

        try
        {
            var fullImage = await Task.Run(() => DecodeFile(analysis.FilePath, null), cancellationToken)
                .ConfigureAwait(false);
            return new PreviewLoadResult(fullImage,
                fullImage is null ? "图像解码失败；元数据仍可查看。" : $"完整图像解码 · {fullImage.PixelWidth:N0} × {fullImage.PixelHeight:N0} 像素",
                fullImage is not null);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException)
        {
            if (!File.Exists(exifToolPath))
                return new PreviewLoadResult(null, "不支持原图解码，且未找到 ExifTool 嵌入预览提取器。", false);
            foreach (var tag in new[] { "PreviewImage", "PreviewImage2", "JpgFromRaw", "ThumbnailImage" })
            {
                var bytes = await ExtractExifToolBinaryAsync(
                    exifToolPath, analysis.FilePath, tag, cancellationToken).ConfigureAwait(false);
                var image = Decode(bytes, null);
                if (image is not null)
                    return new PreviewLoadResult(image,
                        $"RAW 嵌入预览（{tag}）· {image.PixelWidth:N0} × {image.PixelHeight:N0} 像素；不是完整 RAW 渲染",
                        false);
            }
            return new PreviewLoadResult(null, "不支持完整图像解码，且文件中没有可用的嵌入预览。", false);
        }
    }

    private static BitmapSource? DecodeFile(string path, int? decodePixelWidth)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (decodePixelWidth is > 0)
            bitmap.DecodePixelWidth = decodePixelWidth.Value;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource? Decode(byte[]? bytes, int? decodePixelWidth)
    {
        if (bytes is null || bytes.Length == 0)
            return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth is > 0)
                bitmap.DecodePixelWidth = decodePixelWidth.Value;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException)
        {
            return null;
        }
    }

    private static async Task<byte[]?> ExtractExifToolBinaryAsync(
        string executable,
        string path,
        string tag,
        CancellationToken cancellationToken)
    {
        var start = CreateStartInfo(executable);
        start.ArgumentList.Add("-b");
        start.ArgumentList.Add($"-{tag}");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(Path.GetFullPath(path));
        return await RunBinaryAsync(start, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ExtractVideoFrameAsync(
        string executable,
        string path,
        CancellationToken cancellationToken)
    {
        var start = CreateStartInfo(executable);
        foreach (var argument in new[]
        {
            "-hide_banner", "-loglevel", "error", "-ss", "0.2", "-i", Path.GetFullPath(path),
            "-frames:v", "1", "-vf", "scale=1600:-2", "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1"
        })
            start.ArgumentList.Add(argument);
        return await RunBinaryAsync(start, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(string executable) => new()
    {
        FileName = executable,
        WorkingDirectory = Path.GetDirectoryName(executable)!,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WindowStyle = ProcessWindowStyle.Hidden
    };

    private static async Task<byte[]?> RunBinaryAsync(
        ProcessStartInfo start,
        CancellationToken cancellationToken)
    {
        start.Environment["LC_ALL"] = "C";
        using var process = new Process { StartInfo = start };
        try
        {
            if (!process.Start())
                return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var buffer = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(buffer);
        var errorTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            return process.ExitCode == 0 ? buffer.ToArray() : null;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await WaitForExitIgnoringErrorsAsync(process).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                throw;
            return null;
        }
        catch (IOException)
        {
            TryKill(process);
            await WaitForExitIgnoringErrorsAsync(process).ConfigureAwait(false);
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static async Task WaitForExitIgnoringErrorsAsync(Process process)
    {
        try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (InvalidOperationException) { }
    }
}
