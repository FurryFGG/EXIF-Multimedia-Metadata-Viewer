using System.Diagnostics;
using System.Windows.Media.Imaging;
using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;

namespace ExifMultimediaMetadataViewer.Services;

public sealed class MediaPreviewService
{
    public async Task<BitmapSource?> LoadAsync(
        MediaAnalysis analysis,
        string exifToolPath,
        string ffmpegPath,
        CancellationToken cancellationToken)
    {
        if (MediaAnalyzer.IsVideoFile(analysis.FilePath))
        {
            if (!File.Exists(ffmpegPath))
                return null;
            var frame = await ExtractVideoFrameAsync(ffmpegPath, analysis.FilePath, cancellationToken)
                .ConfigureAwait(false);
            return Decode(frame);
        }

        try
        {
            return await Task.Run(() => DecodeFile(analysis.FilePath), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or FileFormatException)
        {
            if (!File.Exists(exifToolPath))
                return null;
            foreach (var tag in new[] { "PreviewImage", "PreviewImage2", "JpgFromRaw", "ThumbnailImage" })
            {
                var bytes = await ExtractExifToolBinaryAsync(
                    exifToolPath, analysis.FilePath, tag, cancellationToken).ConfigureAwait(false);
                var image = Decode(bytes);
                if (image is not null)
                    return image;
            }
            return null;
        }
    }

    private static BitmapSource? DecodeFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.DecodePixelWidth = 1800;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource? Decode(byte[]? bytes)
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
            bitmap.DecodePixelWidth = 1800;
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
