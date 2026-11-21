using System.Diagnostics;
using ExifMetadata.Core.Services;
using ExifMultimediaMetadataViewer.Services;
using ExifMultimediaMetadataViewer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    private string? sessionApplication;
    partial void InitializeFileActions() => browser.Context += (anchor, entry) => FileMenu(entry).ShowAt(anchor);
    internal MenuFlyout FileMenu(BrowserEntry entry)
    {
        var menu = new MenuFlyout();
        var file = entry.FullPath;
        MenuFlyoutItem Command(string label, Func<Task> action)
        {
            var item = new MenuFlyoutItem
            {
                Text = label
            };
            item.Click += async (_, _) =>
            {
                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    Notice(ex.Message);
                }
            };
            return item;
        }

        MenuFlyoutItem Sync(string label, Action action) => Command(label, () =>
        {
            action();
            return Task.CompletedTask;
        });
        menu.Items.Add(Sync(entry.IsDirectory ? "打开文件夹" : "打开", () => Request(file)));
        if (!entry.IsDirectory && MediaAnalyzer.IsKnownMediaFile(file))
        {
            var other = new MenuFlyoutSubItem
            {
                Text = "打开方式"
            };
            if (sessionApplication is string program)
                other.Items.Add(Sync(Path.GetFileNameWithoutExtension(program), () => OpenExternal(program, file, false)));
            async Task Choose(bool remember)
            {
                var picker = InitPicker(new FileOpenPicker());
                picker.FileTypeFilter.Add(".exe");
                if (await picker.PickSingleFileAsync()is { } app)
                    OpenExternal(app.Path, file, remember);
            }

            other.Items.Add(Command("选择其他应用…", () => Choose(false)));
            other.Items.Add(Command("选择并仅在本次会话记住…", () => Choose(true)));
            menu.Items.Add(other);
        }

        menu.Items.Add(Sync("复制文件路径", () => Copy(file)));
        menu.Items.Add(Sync("复制文件名", () => Copy(entry.Name)));
        menu.Items.Add(Sync("打开所在文件夹", () =>
        {
            var folder = Path.GetDirectoryName(file);
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException("所在文件夹不存在。");
            using var opened = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }));
        menu.Items.Add(Command("刷新", () => browser.Navigate(browser.Model.Folder, false)));
        return menu;
    }

    private void OpenExternal(string program, string file, bool remember)
    {
        ExternalOpenService.Open(program, file);
        if (remember)
            sessionApplication = program;
        Notice("已交给 " + Path.GetFileName(program));
    }
}
