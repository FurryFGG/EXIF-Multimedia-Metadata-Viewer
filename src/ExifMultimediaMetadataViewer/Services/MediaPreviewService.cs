using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;
using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.Services;

public sealed record PreviewLoadResult(BitmapSource? Image, string Description, bool IsFullResolution);

public sealed class MediaPreviewService
{
    private const int ThumbnailDecodeWidth = 320;
    private const long DiskCacheLimitBytes = 512L * 1024 * 1024;
    private static readonly SemaphoreSlim ThumbnailWorkers = new(3, 3);
    private static readonly BoundedLruCache<string, BitmapSource> ThumbnailCache =
        new(192, StringComparer.OrdinalIgnoreCase);
    private static readonly string ThumbnailCacheDirectory = Path.Combine(
        ViewerData.Root, "thumbnail-cache", "v1");
    private static int _cleanupStarted;

    public async Task<(BitmapSource? Image, int? Width, int? Height)> LoadThumbnailAsync(
        string path,
        string exifToolPath,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        var cacheKey = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{ThumbnailDecodeWidth}";
        if (ThumbnailCache.TryGet(cacheKey, out var cached))
            return (cached, cached.PixelWidth, cached.PixelHeight);

        StartDiskCacheCleanup();
        await ThumbnailWorkers.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ThumbnailCache.TryGet(cacheKey, out cached))
                return (cached, cached.PixelWidth, cached.PixelHeight);
            var diskPath = GetDiskCachePath(cacheKey);
            var diskImage = File.Exists(diskPath)
                ? await Task.Run(() => TryDecodeFile(diskPath, ThumbnailDecodeWidth), cancellationToken).ConfigureAwait(false)
                : null;
            if (diskImage is not null)
            {
                TryTouch(diskPath);
                ThumbnailCache.Set(cacheKey, diskImage);
                return (diskImage, diskImage.PixelWidth, diskImage.PixelHeight);
            }

            BitmapSource? image;
            if (MediaAnalyzer.IsVideoFile(path))
            {
                var frame = await ExtractVideoFrameAsync(ffmpegPath, path, cancellationToken).ConfigureAwait(false);
                image = Decode(frame, ThumbnailDecodeWidth);
            }
            else
            {
                image = await Task.Run(() => TryDecodeFile(path, ThumbnailDecodeWidth), cancellationToken).ConfigureAwait(false);
                if (image is null && File.Exists(exifToolPath))
                {
                    foreach (var tag in new[] { "PreviewImage", "PreviewImage2", "JpgFromRaw", "ThumbnailImage" })
                    {
                        var bytes = await ExtractExifToolBinaryAsync(exifToolPath, path, tag, cancellationToken).ConfigureAwait(false);
                        image = Decode(bytes, ThumbnailDecodeWidth);
                        if (image is not null)
                            break;
                    }
                }
            }
            if (image is not null)
            {
                ThumbnailCache.Set(cacheKey, image);
                await Task.Run(() => SaveDiskThumbnail(diskPath, image), CancellationToken.None).ConfigureAwait(false);
            }
            return (image, image?.PixelWidth, image?.PixelHeight);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException or System.ComponentModel.Win32Exception)
        {
            return (null, null, null);
        }
        finally
        {
            ThumbnailWorkers.Release();
        }
    }

    private static string GetDiskCachePath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(ThumbnailCacheDirectory, hash + ".png");
    }

    private static void SaveDiskThumbnail(string destination, BitmapSource image)
    {
        try
        {
            Directory.CreateDirectory(ThumbnailCacheDirectory);
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                encoder.Save(stream);
            File.Move(temporary, destination, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private static void StartDiskCacheCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
            return;
        _ = Task.Run(CleanupDiskCache);
    }

    private static void CleanupDiskCache()
    {
        try
        {
            if (!Directory.Exists(ThumbnailCacheDirectory))
                return;
            var expiry = DateTime.UtcNow.AddDays(-30);
            var files = new DirectoryInfo(ThumbnailCacheDirectory).EnumerateFiles("*.png")
                .OrderByDescending(file => file.LastAccessTimeUtc).ToList();
            long retained = 0;
            foreach (var file in files)
            {
                if (file.LastAccessTimeUtc < expiry || retained + file.Length > DiskCacheLimitBytes)
                {
                    try { file.Delete(); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                }
                else
                    retained += file.Length;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryTouch(string path)
    {
        try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
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

    private static BitmapSource? TryDecodeFile(string path, int? decodePixelWidth)
    {
        try { return DecodeFile(path, decodePixelWidth); }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException or UnauthorizedAccessException)
        {
            return null;
        }
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
