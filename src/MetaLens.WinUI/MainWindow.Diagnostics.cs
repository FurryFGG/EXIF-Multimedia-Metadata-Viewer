using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    partial void StartDiagnostics()
    {
        switch (Environment.GetEnvironmentVariable("METALENS_QA_MODE"))
        {
            case "browser":
                BrowserTest();
                break;
            case "media":
                MediaTest();
                break;
            case "formats":
                FormatTest();
                break;
            case "workspace":
                WorkspaceTest();
                break;
            case "restore":
            case "external":
                OtherTests();
                break;
        }
    }

    // Use the real preview decoder, presenter and raster scale, not a metadata-only probe.
    async void FormatTest()
    {
        string output = Environment.GetEnvironmentVariable("METALENS_QA_OUTPUT")!;
        var folder = Environment.GetEnvironmentVariable("METALENS_QA_SAMPLES")!;
        var results = new List<object>();
        try
        {
            foreach (var extension in new[] { ".png", ".tiff", ".dng", ".nef" })
            {
                var file = Directory.EnumerateFiles(folder).Single(x => Path.GetExtension(x).Equals(extension, StringComparison.OrdinalIgnoreCase));
                var timer = System.Diagnostics.Stopwatch.StartNew();
                await viewer.Show(file, lifetime.Token);
                var bitmap = viewer.Image ?? throw new InvalidDataException($"{extension}: {viewer.Error}");
                var (w,h) = (bitmap.PixelWidth, bitmap.PixelHeight);
                if (w <= 0 || h <= 0 || (extension == ".png" && (w,h) != (360,640)) || (extension == ".tiff" && (w,h) != (1024,512)))
                    throw new InvalidDataException($"Unexpected decoded dimensions {w} x {h} for {extension}");
                var decodeMs = timer.ElapsedMilliseconds;
                foreach (double scale in new[]{ .5,1,2,4 }) {
                    viewer.SetZoom(scale);
                    if (Math.Abs(viewer.RasterWidth - w * scale) > 1) throw new Exception("Physical scale: " + scale);
                }
                viewer.Rotate();
                results.Add(new { extension,w,h,viewer.Kind,decodeMs,viewer.Turn });
            }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {passed=true, results}));
        }
        catch(Exception ex) { await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed=false, results, error=ex.ToString() })); }
        finally { Close(); }
    }

    async void OtherTests()
    {
        var output = Environment.GetEnvironmentVariable("METALENS_QA_OUTPUT")!;
        var dir = Path.GetDirectoryName(output)!;
        try
        {
            if (Environment.GetEnvironmentVariable("METALENS_QA_MODE") == "restore")
            {
                var expected = JsonSerializer.Deserialize<ExifMultimediaMetadataViewer.Services.WorkspaceLayout>(await File.ReadAllTextAsync(Path.Combine(dir, "expected-layout.json")));
                if (layout != expected)
                    throw new Exception("布局或面板尺寸未恢复");
            }
            else
            {
                var helper = Environment.GetEnvironmentVariable("METALENS_QA_HELPER")!;
                var jpg = Path.Combine(dir, "中文 文件 " + Guid.NewGuid() + ".jpg");
                var receipt = Path.Combine(dir, Guid.NewGuid() + ".argv");
                File.Copy(Path.Combine(Environment.GetEnvironmentVariable("METALENS_QA_SAMPLES")!, "sample.jpg"), jpg);
                try
                {
                    Environment.SetEnvironmentVariable("METALENS_ARGV_RECEIPT", receipt);
                    OpenExternal(helper, jpg, false);
                    await Until(() => File.Exists(receipt) && new FileInfo(receipt).Length > 0);
                    if (!(await File.ReadAllLinesAsync(receipt)).SequenceEqual(new[] { jpg }) || sessionApplication != null)
                        throw new Exception("单次打开参数或会话状态错误");
                }
                finally
                {
                    Environment.SetEnvironmentVariable("METALENS_ARGV_RECEIPT", null);
                    File.Delete(jpg);
                }
            }

            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed = true }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { error = ex.Message }));
        }
        finally
        {
            Close();
        }
    }

    async void MediaTest()
    {
        var target = Environment.GetEnvironmentVariable("METALENS_QA_OUTPUT")!;
        try
        {
            var results = new List<object>();
            var folder = Environment.GetEnvironmentVariable("METALENS_QA_SAMPLES")!;
            foreach (var f in new[]
            {
                "sample.jpg",
                "sample.ARW",
                "sample.mp4"
            }

            )
            {
                var file = Path.Combine(folder, f);
                await viewer.Show(file, lifetime.Token);
                viewer.SetZoom(1);
                double width = viewer.RasterWidth;
                viewer.SetZoom(2);
                var twice = viewer.RasterWidth;
                viewer.Rotate();
                ToggleFull();
                ToggleFull();
                var player = viewer.Player;
                double played = 0, seek = 0;
                bool paused = false;
                if (player != null)
                {
                    viewer.ToggleVideo();
                    for (int i = 0; i < 60 && player.PlaybackSession.Position.TotalSeconds < .2; i++)
                        await Task.Delay(100);
                    played = player.PlaybackSession.Position.TotalSeconds;
                    player.Pause();
                    await Task.Delay(200);
                    paused = player.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused;
                    player.PlaybackSession.Position = TimeSpan.FromSeconds(1);
                    await Task.Delay(300);
                    seek = player.PlaybackSession.Position.TotalSeconds;
                }

                results.Add(new { f, viewer.Kind, w = viewer.Image?.PixelWidth, h = viewer.Image?.PixelHeight, width, twice, viewer.Turn, played, paused, seek, viewer.Error });
            }

            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(results));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new { error = ex.ToString() }));
        }
        finally
        {
            Close();
        }
    }

    async void BrowserTest()
    {
        var target = Environment.GetEnvironmentVariable("METALENS_QA_OUTPUT");
        if (string.IsNullOrEmpty(target))
            return;
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            await Until(() => !browser.Model.Loading);
            long enumeratedMs = timer.ElapsedMilliseconds;
            browser.SetView("缩略图");
            await Task.Delay(600);
            var grid = (GridView)browser.FindName("Tiles");
            var count = browser.Model.Items.Count;
            var realized = new List<int>();
            for (int i = 0; i <= 12; i++)
            {
                int index = (count - 1) * i / 12;
                grid.ScrollIntoView(grid.Items[index]);
                await Until(() => grid.ContainerFromIndex(index) != null);
                realized.Add(Enumerable.Range(0, count).Count(j => grid.ContainerFromIndex(j) != null));
            }

            var result = new
            {
                count,
                realized,
                enumeratedMs,
                folder = browser.Model.Folder,
                passed = count >= 1000 && realized.All(n => n > 0 && n < 100),
                scans = browser.Model.ScanCount
            };
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(result));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(target, JsonSerializer.Serialize(new { error = ex.ToString() }));
        }
        finally
        {
            Close();
        }
    }
}
