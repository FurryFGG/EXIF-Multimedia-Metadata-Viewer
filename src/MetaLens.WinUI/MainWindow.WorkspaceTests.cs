using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using ExifMultimediaMetadataViewer.Services;
using ExifMetadata.Core.Services;
using ExifMetadata.Core.Models;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    async Task Shot(string file)
    {
        await Task.Delay(180);
        var shot = new RenderTargetBitmap();
        await shot.RenderAsync(Root);
        var bytes = (await shot.GetPixelsAsync()).ToArray();
        using var stream = File.Create(file);
        var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
        enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)shot.PixelWidth, (uint)shot.PixelHeight, 96, 96, bytes);
        await enc.FlushAsync();
    }

    static async Task Until(Func<bool> predicate, int ms = 30000)
    {
        var t = System.Diagnostics.Stopwatch.StartNew();
        while (!predicate())
        {
            if (t.ElapsedMilliseconds > ms)
                throw new TimeoutException("未在时限内取得实际结果");
            await Task.Delay(80);
        }
    }

    async void WorkspaceTest()
    {
        var output = Environment.GetEnvironmentVariable("METALENS_QA_OUTPUT")!;
        var dir = Path.GetDirectoryName(output)!;
        var samples = Environment.GetEnvironmentVariable("METALENS_QA_SAMPLES")!;
        var reference = Environment.GetEnvironmentVariable("METALENS_QA_BASELINE")!;
        var steps = new List<object>();
        try
        {
            await Until(() => !browser.Model.Loading);
            foreach (var name in new[]
            {
                "sample.jpg",
                "sample.ARW",
                "sample.mp4"
            }

            )
            {
                var file = Path.Combine(samples, name);
                Request(file);
                await Until(() => meta.Data?.FilePath == file && viewer.Current == file && viewer.Image != null);
                var actual = meta.Data!;
                var expected = await new MediaAnalyzer().AnalyzeAsync(file, Path.Combine(reference, "exiftool", "exiftool.exe"), Path.Combine(reference, "ffmpeg", "ffprobe.exe"), lifetime.Token);
                var diffs = expected.AllFields.Zip(actual.AllFields).Where(p => p.First != p.Second).Select(p => p.First.FullPath).ToArray();
                bool equal = actual.AllFields.Count == expected.AllFields.Count && diffs.All(p => p.EndsWith("FileAccessDate", StringComparison.OrdinalIgnoreCase)) && actual.ExifTool.Status == expected.ExifTool.Status && actual.FFprobe.Status == expected.FFprobe.Status;
                steps.Add(new { name, fields = actual.AllFields.Count, expected = expected.AllFields.Count, diffs = diffs.Take(25), equal });
                if (!equal)
                    throw new InvalidDataException("字段差异: " + name);
            }

            var jpg = Path.Combine(samples, "sample.jpg");
            Request(jpg);
            await Until(() => viewer.Current == jpg && viewer.Image != null && meta.Data?.FilePath == jpg);
            var scans = browser.Model.ScanCount;
            var parses = metaModel.Parses;
            var identity = meta.Data;
            var image = viewer.Image;
            var previousTheme = settings.IsDarkTheme;
            if (previousTheme)
                ThemeClick(this, new());
            foreach (var mode in Enum.GetValues<LayoutMode>())
            {
                SwitchLayout(mode);
                await Shot(Path.Combine(dir, "layout-" + mode + ".png"));
                for (int i = 0; i < 3; i++)
                {
                    var host = i == 0 ? Workspace : i == 1 ? mainArea : browser.TreeLayout;
                    var span = Horizontal(i) ? host.ActualWidth : host.ActualHeight;
                    Adjust(i, span * .40, span);
                }

                if (!ReferenceEquals(identity, meta.Data) || !ReferenceEquals(image, viewer.Image) || scans != browser.Model.ScanCount || parses != metaModel.Parses)
                    throw new Exception("布局切换替换了数据");
                steps.Add(new { mode = mode.ToString(), layout.Active, scans, parses });
            }

            ThemeClick(this, new());
            await Shot(Path.Combine(dir, "dark-stacked.png"));
            if (!previousTheme)
                ThemeClick(this, new());
            var tabs = (ComboBox)meta.FindName("Tabs");
            tabs.SelectedIndex = 1;
            var q = (TextBox)meta.FindName("Search");
            q.Text = "ImageWidth";
            var fields = (ListView)meta.FindName("Fields");
            await Until(() => fields.Items.Count > 0 && fields.Items.Count < 100);
            fields.SelectedIndex = 0;
            var copy = meta.Selection();
            if (!copy.Contains("Width", StringComparison.OrdinalIgnoreCase))
                throw new Exception("字段搜索或选择失败");
            tabs.SelectedIndex = 2;
            if (meta.All().Length < meta.Data!.ExifTool.RawJson.Length)
                throw new Exception("不完整的原始 JSON");
            tabs.SelectedIndex = 0;
            var entry = browser.Model.Entries.First(x => x.FullPath == jpg);
            var menu = FileMenu(entry);
            if (!menu.Items.OfType<MenuFlyoutSubItem>().Any())
                throw new Exception("缺少打开方式");
            try
            {
                ExternalOpenService.StartInfo(Path.Combine(dir, "missing.exe"), jpg);
                throw new Exception("没有拒绝缺失程序");
            }
            catch (FileNotFoundException)
            {
            }

            for (int i = 0; i < 30; i++)
                SwitchLayout((LayoutMode)(i % 3));
            for (int i = 0; i < 12; i++)
                Request(Path.Combine(samples, i % 2 == 0 ? "sample.ARW" : "sample.mp4"));
            Request(jpg);
            await Until(() => viewer.Current == jpg && viewer.Image != null && meta.Data?.FilePath == jpg);
            steps.Add(new { layoutSwitches = 30, rapidSelections = 13, final = Path.GetFileName(meta.Data!.FilePath), menuItems = menu.Items.Count });
            await File.WriteAllTextAsync(Path.Combine(dir, "expected-layout.json"), JsonSerializer.Serialize(layout));
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed = true, steps }));
        }
        catch (Exception ex)
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed = false, steps, error = ex.ToString() }));
        }
        finally
        {
            Close();
        }
    }
}
