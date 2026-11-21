using ExifMetadata.Core.Services;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    readonly MetadataModel metaModel = new();
    readonly MetadataView meta = new();
    CancellationTokenSource? analysisJob;
    partial void InitializeMetadata()
    {
        metadataArea.Children.Add(meta);
        viewer.Changed += () =>
        {
            if (selected == viewer.Current)
                meta.PreviewStatus(viewer.Error ?? viewer.Kind);
        };
        CancelRequested += () => analysisJob?.Cancel();
        void Cmd(string text, Action act)
        {
            var b = new AppBarButton
            {
                Label = text
            };
            b.Click += (_, _) => act();
            Commands.SecondaryCommands.Add(b);
        }

        Cmd("复制字段", () => Copy(meta.Selection()));
        Cmd("复制全部", () => Copy(meta.All()));
        Cmd("导出 JSON", () => Export(true));
        Cmd("导出 CSV", () => Export(false));
    }

    partial void MetadataChosen(string path) => _ = ReadMetadata(path);
    async Task ReadMetadata(string path)
    {
        analysisJob?.Cancel();
        using var c = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        analysisJob = c;
        meta.Set(null, "");
        try
        {
            var a = await metaModel.Read(path, c.Token);
            c.Token.ThrowIfCancellationRequested();
            meta.Set(a, viewer.Error ?? viewer.Kind);
            Notice($"{Path.GetFileName(path)} · {a.AllFields.Count:N0} 个字段 · ExifTool {a.ExifTool.Status} · FFprobe {a.FFprobe.Status}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
        finally
        {
            if (analysisJob == c)
                analysisJob = null;
        }
    }

    void Copy(string text)
    {
        if (text.Length == 0)
            return;
        try
        {
            var d = new DataPackage();
            d.SetText(text);
            Clipboard.SetContent(d);
            Notice("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }

    async void Export(bool json)
    {
        var a = meta.Data;
        if (a == null)
        {
            Notice("请先选择已解析的文件");
            return;
        }

        try
        {
            var ext = json ? ".json" : ".csv";
            var p = InitPicker(new FileSavePicker { SuggestedFileName = Path.GetFileName(a.FilePath) + ".metadata" });
            p.FileTypeChoices.Add(json ? "JSON" : "CSV", new[] { ext });
            if (await p.PickSaveFileAsync()is not { } dest)
                return;
            await Task.Run(() => json ? MetadataExportService.ExportJsonAsync(a, dest.Path, lifetime.Token) : MetadataExportService.ExportCsvAsync(a, dest.Path, lifetime.Token));
            Notice("已导出：" + dest.Path);
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }
}
