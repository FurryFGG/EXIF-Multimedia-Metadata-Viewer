using ExifMetadata.Core.Services;
using ExifMultimediaMetadataViewer.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MetaLens.WinUI;
internal static class MediaImages
{
    internal static string Tool(string n) => Path.Combine(AppContext.BaseDirectory, "tools", n == "exiftool" ? n : "ffmpeg", n + ".exe");
    static readonly SemaphoreSlim workers = new(3), preview = new(1);
    static readonly BoundedLruCache<string, byte[]> thumbs = new(192);
    static long nextTrim;
    static readonly string dir = Path.Combine(ViewerData.Root, "thumbnail-cache", "winui-v2");
    // At most two smaller decoded previews (about 32 MiB combined at 32 bpp).
    // Full-resolution camera photos remain eligible for viewing, not for caching.
    static readonly BoundedLruCache<string, (BitmapImage Image, string Kind)> previews = new(2, StringComparer.OrdinalIgnoreCase);
    internal static async Task<(Stream Data, string Kind)> Input(string file, bool thumb, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (MediaAnalyzer.IsVideoFile(file))
        {
            var bytes = await Binary("ffmpeg", ["-hide_banner", "-loglevel", "error", "-ss", "0.2", "-i", file, "-frames:v", "1", "-vf", thumb ? "scale=320:-2" : "scale=1600:-2", "-f", "image2pipe", "-vcodec", "mjpeg", "pipe:1"], ct);
            return (new MemoryStream(bytes), "视频首帧");
        }

        var data = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            await BitmapDecoder.CreateAsync(data.AsRandomAccessStream()).AsTask(ct);
            data.Position = 0;
            return (data, Path.GetExtension(file).ToLowerInvariant()is ".arw" or ".dng" or ".nef" or ".cr2" or ".cr3" or ".orf" or ".rw2" or ".raf" or ".pef" or ".raw" ? "RAW 系统解码（可能使用内嵌预览）" : "原图");
        }
        catch (OperationCanceledException)
        {
            data.Dispose();
            throw;
        }
        catch
        {
            data.Dispose();
        }

        foreach (var tag in new[]
        {
            "PreviewImage",
            "PreviewImage2",
            "JpgFromRaw",
            "ThumbnailImage"
        }

        )
        {
            var b = await Binary("exiftool", ["-b", "-" + tag, "--", file], ct);
            if (b.Length > 0)
                return (new MemoryStream(b), "内嵌预览 · " + tag);
        }

        throw new InvalidDataException("不支持此图像解码，亦无可读内嵌预览");
    }

    static async Task<byte[]> Binary(string tool, string[] args, CancellationToken ct)
    {
        var exe = Tool(tool);
        var r = await ProcessRunner.RunBinaryAsync(exe, args, Path.GetDirectoryName(exe)!, TimeSpan.FromSeconds(18), ct);
        return r.StandardOutput;
    }

    internal static async Task<(BitmapImage Image, string Kind)> ReadPreview(string file, CancellationToken ct)
    {
        await preview.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            var key = $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            if (previews.TryGet(key, out var hit))
                return hit;
            var result = await Task.Run(() => Input(file, false, ct), ct);
            using var stream = result.Data;
            var dims = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream()).AsTask(ct);
            stream.Position = 0;
            var image = new BitmapImage
            {
                DecodePixelWidth = checked((int)dims.OrientedPixelWidth)
            };
            await image.SetSourceAsync(stream.AsRandomAccessStream()).AsTask(ct);
            ct.ThrowIfCancellationRequested();
            if ((long)image.PixelWidth * image.PixelHeight <= 4194304)
                previews.Set(key, (image, result.Kind));
            return (image, result.Kind);
        }
        finally
        {
            preview.Release();
        }
    }

    internal static async Task<byte[]> Thumb(string file, CancellationToken ct)
    {
        var inf = new FileInfo(file);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{inf.FullName}|{inf.Length}|{inf.LastWriteTimeUtc.Ticks}")));
        if (thumbs.TryGet(key, out var hit))
            return hit;
        await workers.WaitAsync(ct);
        try
        {
            if (thumbs.TryGet(key, out hit))
                return hit;
            var dest = Path.Combine(dir, key + ".png");
            if (File.Exists(dest))
            {
                try
                {
                    hit = await File.ReadAllBytesAsync(dest, ct);
                    if (hit.Length > 0)
                    {
                        thumbs.Set(key, hit);
                        return hit;
                    }
                }
                catch (IOException)
                {
                }
            }

            var input = await Input(file, true, ct);
            using var source = input.Data;
            var decoder = await BitmapDecoder.CreateAsync(source.AsRandomAccessStream()).AsTask(ct);
            var scale = Math.Min(1, 320d / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, decoder.PixelWidth * scale),
                ScaledHeight = (uint)Math.Max(1, decoder.PixelHeight * scale)
            };
            using var bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb).AsTask(ct);
            using var ms = new MemoryStream();
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms.AsRandomAccessStream()).AsTask(ct);
            enc.SetSoftwareBitmap(bmp);
            await enc.FlushAsync().AsTask(ct);
            hit = ms.ToArray();
            thumbs.Set(key, hit);
            try
            {
                Directory.CreateDirectory(dir);
                var tmp = dest + Guid.NewGuid() + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(tmp, hit, ct);
                    File.Move(tmp, dest, true);
                }
                finally
                {
                    File.Delete(tmp);
                }

                Trim();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return hit;
        }
        finally
        {
            workers.Release();
        }
    }

    static void Trim()
    {
        var now = Environment.TickCount64;
        var old = Interlocked.Read(ref nextTrim);
        if (now < old || Interlocked.CompareExchange(ref nextTrim, now + 60000, old) != old)
            return;
        long total = 0;
        foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc))
        {
            if (f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30) || total + f.Length > 512L * 1024 * 1024)
            {
                try
                {
                    f.Delete();
                }
                catch (IOException)
                {
                }
            }
            else
                total += f.Length;
        }
    }
}
