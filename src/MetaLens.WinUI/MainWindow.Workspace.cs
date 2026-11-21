using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ExifMultimediaMetadataViewer.Services;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    readonly ExplorerView browser = new();
    readonly Grid mainArea = new(), previewArea = new(), metadataArea = new();
    readonly Border outerGrip = new(), innerGrip = new();
    int openEpoch;
    string? selected;
    partial void InitializeWorkspace()
    {
        Workspace.Children.Add(browser);
        Workspace.Children.Add(outerGrip);
        Workspace.Children.Add(mainArea);
        mainArea.Children.Add(previewArea);
        mainArea.Children.Add(innerGrip);
        mainArea.Children.Add(metadataArea);
        browser.Setup(settings, lifetime.Token);
        browser.Message += Notice;
        browser.Open += Request;
        browser.OptionsChanged += () =>
        {
            settings = settings with
            {
                BrowserViewMode = browser.View,
                BrowserSortMode = browser.Model.Sort,
                ThumbnailSize = browser.TileSize
            };
            store.Save(settings);
        };
        PathRequested += OpenPath;
        CancelRequested += browser.Model.Cancel;
        Split(Workspace, false, browser, outerGrip, mainArea, new GridLength(300));
        Split(mainArea, true, previewArea, innerGrip, metadataArea, new GridLength(1, GridUnitType.Star));
        Split(browser.TreeLayout, true, browser.FolderTree, browser.Handle, browser.FilePane, new GridLength(200));
        InitializePreview();
        InitializeMetadata();
        InitializeLayouts();
        InitializeFileActions();
    }

    partial void InitializePreview();
    partial void InitializeMetadata();
    partial void InitializeLayouts();
    partial void InitializeFileActions();
    partial void MediaChosen(string path);
    partial void MetadataChosen(string path);
    static void Split(Grid parent, bool horizontal, FrameworkElement first, FrameworkElement grip, FrameworkElement second, GridLength length, double tail = 1)
    {
        parent.RowDefinitions.Clear();
        parent.ColumnDefinitions.Clear();
        var sizes = new[]
        {
            length,
            new GridLength(5),
            new GridLength(tail, GridUnitType.Star)
        };
        foreach (var s in sizes)
        {
            if (horizontal)
                parent.ColumnDefinitions.Add(new ColumnDefinition { Width = s });
            else
                parent.RowDefinitions.Add(new RowDefinition { Height = s });
        }

        int i = 0;
        foreach (var f in new[]
        {
            first,
            grip,
            second
        }

        )
        {
            Grid.SetColumn(f, horizontal ? i : 0);
            Grid.SetRow(f, horizontal ? 0 : i);
            i++;
        }

        grip.Margin = new Thickness(horizontal ? 1 : 0, horizontal ? 0 : 1, horizontal ? 1 : 0, horizontal ? 0 : 1);
        if (grip is Border b)
            b.Background = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"];
    }

    partial void OpenInitialDirectory()
    {
        var p = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (Directory.Exists(p))
            _ = browser.Navigate(p);
    }

    async void OpenPath(string path)
    {
        var version = ++openEpoch;
        try
        {
            path = Path.GetFullPath(path.Trim().Trim('"'));
            if (Directory.Exists(path))
            {
                await browser.Navigate(path);
                return;
            }

            if (!File.Exists(path))
            {
                Notice("文件已移动、删除或不可访问：" + path);
                return;
            }

            var folder = Path.GetDirectoryName(path)!;
            if (!StringComparer.OrdinalIgnoreCase.Equals(folder, browser.Model.Folder))
            {
                if (!await browser.Navigate(folder) || version != openEpoch)
                    return;
            }

            selected = path;
            browser.SelectPath(path);
            Track(path);
            Notice(path);
            MediaChosen(path);
            MetadataChosen(path);
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }
}
