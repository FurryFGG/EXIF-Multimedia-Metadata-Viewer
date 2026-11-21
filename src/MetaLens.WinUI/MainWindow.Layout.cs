using ExifMultimediaMetadataViewer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text.Json;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    WorkspaceLayout layout = WorkspaceLayout.Default;
    Dictionary<LayoutMode, double> tileByMode = [];
    static string SizesFile => Path.Combine(ViewerData.Root, "winui-tile-sizes.json");

    partial void InitializeLayouts()
    {
        layout = (settings.Layout ?? WorkspaceLayout.Default.WithActive(new PanelSizes(settings.BrowserHeight, settings.SplitRatio, settings.BrowserTreeWidth, settings.IsBrowserExpanded))).Checked();
        try
        {
            if (File.Exists(SizesFile))
                tileByMode = JsonSerializer.Deserialize<Dictionary<LayoutMode, double>>(File.ReadAllText(SizesFile)) ?? [];
        }
        catch (Exception ex)when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        RestoreTile();
        var menu = new MenuFlyout();
        foreach (var pair in new[]
        {
            (LayoutMode.Original, "原版布局"),
            (LayoutMode.Columns, "三栏布局"),
            (LayoutMode.Stacked, "双层布局")
        }

        )
        {
            var item = new MenuFlyoutItem
            {
                Text = pair.Item2
            };
            item.Click += (_, _) => SwitchLayout(pair.Item1);
            menu.Items.Add(item);
        }

        var reset = new MenuFlyoutItem
        {
            Text = "恢复三个布局的默认尺寸"
        };
        reset.Click += (_, _) =>
        {
            layout = WorkspaceLayout.Default with
            {
                Mode = layout.Mode
            };
            tileByMode.Clear();
            browser.TileSize = 150;
            ApplyLayout();
            PersistLayout();
        };
        menu.Items.Add(reset);
        LayoutButton.Flyout = menu;
        BrowserButton.Click += (_, _) =>
        {
            layout = layout.WithActive(layout.Active with { Expanded = !layout.Active.Expanded });
            ApplyLayout();
            PersistLayout();
        };
        BindGrip(outerGrip, Workspace, 0);
        BindGrip(innerGrip, mainArea, 1);
        BindGrip(browser.Handle, browser.TreeLayout, 2);
        Workspace.SizeChanged += (_, _) =>
        {
            if (!fullscreen)
                ApplyLayout();
        };
        browser.OptionsChanged += () => tileByMode[layout.Mode] = browser.TileSize;
        Closed += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(ViewerData.Root);
                tileByMode[layout.Mode] = browser.TileSize;
                File.WriteAllText(SizesFile + ".tmp", JsonSerializer.Serialize(tileByMode));
                File.Move(SizesFile + ".tmp", SizesFile, true);
            }
            catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
            {
            }
        };
        ApplyLayout();
    }

    void PersistLayout()
    {
        settings = settings with
        {
            Layout = layout,
            ThumbnailSize = browser.TileSize
        };
        store.Save(settings);
    }

    void RestoreTile()
    {
        browser.TileSize = tileByMode.TryGetValue(layout.Mode, out var size) && double.IsFinite(size) ? Math.Clamp(size, 96, 260) : settings.ThumbnailSize;
        browser.FitTiles();
    }

    void SwitchLayout(LayoutMode mode)
    {
        tileByMode[layout.Mode] = browser.TileSize;
        layout = layout with
        {
            Mode = mode
        };
        RestoreTile();
        ApplyLayout();
        PersistLayout();
    }

    bool Horizontal(int i) => i switch
    {
        0 => layout.Mode != LayoutMode.Original,
        1 => layout.Mode != LayoutMode.Stacked,
        _ => layout.Mode == LayoutMode.Original
    };
    static GridLength Star(double f) => new(f, GridUnitType.Star);
    void ApplyLayout()
    {
        if (fullscreen)
            return;
        var p = layout.Active;
        var original = layout.Mode == LayoutMode.Original;
        double h = Math.Max(240, Workspace.ActualHeight), w = Math.Max(220, Workspace.ActualWidth);
        browser.Visibility = outerGrip.Visibility = p.Expanded ? Visibility.Visible : Visibility.Collapsed;
        Split(Workspace, Horizontal(0), browser, outerGrip, mainArea, !p.Expanded ? new GridLength(0) : original ? new GridLength(Math.Min(p.Browser, h * .75)) : Star(p.Browser), !p.Expanded || original ? 1 : 1 - p.Browser);
        if (!p.Expanded)
        {
            if (Horizontal(0))
                Workspace.ColumnDefinitions[1].Width = new GridLength(0);
            else
                Workspace.RowDefinitions[1].Height = new GridLength(0);
        }

        Split(mainArea, Horizontal(1), previewArea, innerGrip, metadataArea, Star(p.Preview), 1 - p.Preview);
        Split(browser.TreeLayout, Horizontal(2), browser.FolderTree, browser.Handle, browser.FilePane, original ? new GridLength(Math.Min(p.Tree, w * .63)) : Star(p.Tree), original ? 1 : 1 - p.Tree);
        browser.FitTiles();
    }

    void BindGrip(FrameworkElement grip, Grid host, int i)
    {
        bool held = false;
        grip.PointerPressed += (_, e) =>
        {
            held = e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed && grip.CapturePointer(e.Pointer);
            e.Handled = held;
        };
        grip.PointerMoved += (_, e) =>
        {
            if (!held)
                return;
            var pos = e.GetCurrentPoint(host).Position;
            Adjust(i, Horizontal(i) ? pos.X : pos.Y, Horizontal(i) ? host.ActualWidth : host.ActualHeight);
            e.Handled = true;
        };
        grip.PointerReleased += (_, e) =>
        {
            held = false;
            grip.ReleasePointerCapture(e.Pointer);
        };
        grip.PointerCaptureLost += (_, _) => held = false;
    }

    void Adjust(int i, double coord, double span)
    {
        if (span <= 12)
            return;
        var p = layout.Active;
        var max = span - 5;
        var min = Math.Min(i == 2 ? 70 : 105, max * .22);
        var n = Math.Clamp(coord, min, max - min);
        p = i switch
        {
            0 => p with
            {
                Browser = layout.Mode == LayoutMode.Original ? n : n / max
            },
            1 => p with
            {
                Preview = n / max
            },
            _ => p with
            {
                Tree = layout.Mode == LayoutMode.Original ? n : n / max
            }
        };
        layout = layout.WithActive(p.Checked(layout.Mode));
        ApplyLayout();
        PersistLayout();
    }
}
