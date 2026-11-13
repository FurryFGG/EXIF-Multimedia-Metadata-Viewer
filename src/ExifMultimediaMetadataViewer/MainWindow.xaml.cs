using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using ExifMultimediaMetadataViewer.Controls;
using ExifMultimediaMetadataViewer.Services;
using ExifMultimediaMetadataViewer.ViewModels;
using Microsoft.Win32;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ExifMultimediaMetadataViewer;

public partial class MainWindow : Window
{
    private static readonly string MediaFilter =
        "媒体文件|*.jpg;*.jpeg;*.tif;*.tiff;*.png;*.webp;*.heic;*.heif;*.avif;*.dng;*.arw;*.cr2;*.cr3;*.nef;*.raf;*.rw2;*.orf;*.pef;*.mp4;*.mov;*.m4v;*.mkv;*.avi;*.mts;*.m2ts;*.mpg;*.mpeg;*.3gp;*.mxf;*.ts;*.webm|所有文件|*.*";

    private readonly ViewerViewModel _viewModel = new();
    private readonly ImageViewport _viewport = new();
    private HwndSource? _windowSource;
    private bool _isFullscreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private ResizeMode _savedResizeMode;
    private Thickness _savedRootMargin;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewport.SetBinding(ImageViewport.SourceProperty, new System.Windows.Data.Binding(nameof(ViewerViewModel.PreviewImage)));
        _viewport.SetBinding(ImageViewport.ZoomProperty, new System.Windows.Data.Binding(nameof(ViewerViewModel.Zoom))
        {
            Mode = System.Windows.Data.BindingMode.TwoWay
        });
        _viewport.SetBinding(ImageViewport.RotationProperty, new System.Windows.Data.Binding(nameof(ViewerViewModel.PreviewRotation))
        {
            Mode = System.Windows.Data.BindingMode.TwoWay
        });
        NormalViewportHost.Content = _viewport;
        PreviewColumn.Width = new GridLength(_viewModel.SplitRatio, GridUnitType.Star);
        MetadataColumn.Width = new GridLength(1 - _viewModel.SplitRatio, GridUnitType.Star);
        FolderTreeColumn.Width = new GridLength(_viewModel.BrowserTreeWidth);
        ApplyBrowserVisibility();
        ApplyTheme(_viewModel.IsDarkTheme);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowSource = (HwndSource?)PresentationSource.FromVisual(this);
        _windowSource?.AddHook(WindowMessageHook);
        var commandLineFile = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(File.Exists);
        if (commandLineFile is not null)
            await OpenFileWithNeighborsAsync(commandLineFile);
        else
        {
            var initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Directory.Exists(initialDirectory))
                await _viewModel.NavigateDirectoryAsync(initialDirectory);
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        if (_viewModel.IsBrowserExpanded)
            _viewModel.SetLayoutSizes(FolderTreeColumn.ActualWidth, BrowserRow.ActualHeight);
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开照片或视频",
            Filter = MediaFilter,
            Multiselect = false,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            await OpenFileWithNeighborsAsync(dialog.FileName);
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含照片或视频的文件夹",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _viewModel.IsBrowserExpanded = true;
            await _viewModel.NavigateDirectoryAsync(dialog.FolderName);
            var firstMedia = _viewModel.BrowserEntries.FirstOrDefault(entry =>
                !entry.IsDirectory && ExifMetadata.Core.Services.MediaAnalyzer.IsKnownMediaFile(entry.FullPath));
            if (firstMedia is not null)
                await _viewModel.OpenBrowserEntryAsync(firstMedia);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private void Recent_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = sender as UIElement,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };

        if (_viewModel.RecentFiles.Count == 0)
        {
            menu.Items.Add(new System.Windows.Controls.MenuItem { Header = "暂无最近文件", IsEnabled = false });
        }
        else
        {
            foreach (var path in _viewModel.RecentFiles)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = Path.GetFileName(path),
                    ToolTip = path,
                    Tag = path
                };
                item.Click += async (_, _) =>
                {
                    try { await _viewModel.OpenRecentAsync((string)item.Tag); }
                    catch (Exception exception) { ShowError(exception.Message); }
                };
                menu.Items.Add(item);
            }
        }

        menu.IsOpen = true;
    }

    private async Task OpenFileWithNeighborsAsync(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            var files = directory is null
                ? [path]
                : await Task.Run(() => Directory.EnumerateFiles(directory)
                    .Where(ExifMetadata.Core.Services.MediaAnalyzer.IsKnownMediaFile)
                    .ToArray());
            await _viewModel.OpenAsync(files, path);
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async void Previous_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.GoPreviousAsync();

    private async void Next_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.GoNextAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => _viewModel.Cancel();
    private void Rotate_Click(object sender, RoutedEventArgs e) => _viewport.RotateClockwise();
    private void PreviewScale_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePreviewSize();
    private void FitPreview_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetPreviewSize();
        _viewport.FitToViewport();
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e) => _viewport.SetActualPixelScale(1.0);
    private void DoubleSize_Click(object sender, RoutedEventArgs e) => _viewport.SetActualPixelScale(2.0);

    private void Theme_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsDarkTheme = !_viewModel.IsDarkTheme;

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var field = _viewModel.SelectedField;
        if (field is null)
        {
            ShowError("请先在“全部元数据”中选择一个字段。");
            return;
        }
        Clipboard.SetText($"{field.Source}\t{field.FullPath}\t{field.TagId}\t{field.RawValue}\t{field.FormattedValue}");
        _viewModel.SetStatus($"已复制：{field.FullPath}");
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var text = _viewModel.CopyAllFields();
        if (string.IsNullOrEmpty(text))
        {
            ShowError("请先打开媒体文件。");
            return;
        }
        Clipboard.SetText(text);
        _viewModel.SetStatus($"已复制全部 {_viewModel.Analysis!.AllFields.Count:N0} 个字段。");
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Analysis is null)
        {
            ShowError("请先打开媒体文件。");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "导出完整元数据 JSON",
            Filter = "JSON 文件|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = Path.GetFileNameWithoutExtension(_viewModel.Analysis.FilePath) + ".metadata.json"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try { await _viewModel.ExportJsonAsync(dialog.FileName); }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Analysis is null)
        {
            ShowError("请先打开媒体文件。");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Title = "导出全部元数据 CSV",
            Filter = "CSV 文件|*.csv",
            DefaultExt = ".csv",
            AddExtension = true,
            FileName = Path.GetFileNameWithoutExtension(_viewModel.Analysis.FilePath) + ".metadata.csv"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try { await _viewModel.ExportCsvAsync(dialog.FileName); }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private void Window_PreviewDragOver(object sender, WpfDragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, WpfDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        var paths = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (paths is null || paths.Length == 0)
            return;
        var first = paths.FirstOrDefault(File.Exists);
        if (first is not null)
            await OpenFileWithNeighborsAsync(first);
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            OpenFile_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            _ = _viewModel.GoPreviousAsync();
        }
        else if (e.Key == Key.Right)
        {
            _ = _viewModel.GoNextAsync();
        }
    }

    private async void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None || IsTextEntryFocused())
            return;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Up:
                if (_viewModel.CanGoPrevious) await _viewModel.GoPreviousAsync();
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                if (_viewModel.CanGoNext) await _viewModel.GoNextAsync();
                e.Handled = true;
                break;
        }
    }

    private bool IsTextEntryFocused()
    {
        var focused = Keyboard.FocusedElement as DependencyObject;
        return focused is TextBoxBase or ComboBox or DataGrid;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmMouseHWheel = 0x020E;
        if (message != wmMouseHWheel || !_viewport.IsVisible || _viewport.Source is null)
            return IntPtr.Zero;

        var screenPoint = new Point(unchecked((short)(long)lParam), unchecked((short)((long)lParam >> 16)));
        var localPoint = _viewport.PointFromScreen(screenPoint);
        if (localPoint.X < 0 || localPoint.Y < 0 || localPoint.X >= _viewport.ActualWidth || localPoint.Y >= _viewport.ActualHeight)
            return IntPtr.Zero;

        var delta = unchecked((short)(((long)wParam >> 16) & 0xffff));
        _viewport.PanHorizontally(delta * 0.55);
        handled = true;
        return IntPtr.Zero;
    }

    private void ToggleBrowser_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsBrowserExpanded = !_viewModel.IsBrowserExpanded;

    private async void BackFolder_Click(object sender, RoutedEventArgs e) => await _viewModel.GoBackDirectoryAsync();
    private async void ForwardFolder_Click(object sender, RoutedEventArgs e) => await _viewModel.GoForwardDirectoryAsync();
    private async void ParentFolder_Click(object sender, RoutedEventArgs e) => await _viewModel.GoParentDirectoryAsync();
    private async void RefreshFolder_Click(object sender, RoutedEventArgs e) => await _viewModel.RefreshBrowserAsync();
    private async void BrowserPathBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        await _viewModel.NavigateDirectoryAsync(BrowserPathBox.Text);
        e.Handled = true;
    }

    private void ThumbnailView_Click(object sender, RoutedEventArgs e) => _viewModel.BrowserViewMode = "缩略图";
    private void ListView_Click(object sender, RoutedEventArgs e) => _viewModel.BrowserViewMode = "列表";
    private void DetailsView_Click(object sender, RoutedEventArgs e) => _viewModel.BrowserViewMode = "详细信息";

    private async void FolderTreeItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: DirectoryTreeNode node })
            await _viewModel.LoadDirectoryChildrenAsync(node);
    }

    private async void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        await _viewModel.SelectDirectoryNodeAsync(e.NewValue as DirectoryTreeNode);

    private async void BrowserList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is BrowserEntry { IsDirectory: false } listEntry &&
            !string.Equals(listEntry.FullPath, _viewModel.FilePath, StringComparison.OrdinalIgnoreCase))
            await _viewModel.OpenBrowserEntryAsync(listEntry);
        else if (sender is DataGrid grid && grid.SelectedItem is BrowserEntry { IsDirectory: false } gridEntry &&
                 !string.Equals(gridEntry.FullPath, _viewModel.FilePath, StringComparison.OrdinalIgnoreCase))
            await _viewModel.OpenBrowserEntryAsync(gridEntry);
    }

    private async void BrowserList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list)
            await _viewModel.OpenBrowserEntryAsync(list.SelectedItem as BrowserEntry);
        else if (sender is DataGrid grid)
            await _viewModel.OpenBrowserEntryAsync(grid.SelectedItem as BrowserEntry);
    }

    private async void BrowserList_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        var entry = sender switch
        {
            ListBox list => list.SelectedItem as BrowserEntry,
            DataGrid grid => grid.SelectedItem as BrowserEntry,
            _ => null
        };
        await _viewModel.OpenBrowserEntryAsync(entry);
        e.Handled = true;
    }

    private async void Thumbnail_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is BrowserEntry entry && IsElementVisibleIn(image, ThumbnailList))
            await _viewModel.LoadThumbnailAsync(entry);
    }

    private async void ThumbnailScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        await LoadVisibleThumbnailsAsync();
    }

    private async Task LoadVisibleThumbnailsAsync()
    {
        if (!ThumbnailList.IsVisible)
            return;
        foreach (var item in ThumbnailList.Items)
        {
            if (ThumbnailList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container ||
                !IsElementVisibleIn(container, ThumbnailList) || item is not BrowserEntry entry)
                continue;
            await _viewModel.LoadThumbnailAsync(entry);
        }
    }

    private static bool IsElementVisibleIn(FrameworkElement element, FrameworkElement viewport)
    {
        if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return false;
        try
        {
            var bounds = element.TransformToAncestor(viewport)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return bounds.IntersectsWith(new Rect(0, 0, viewport.ActualWidth, viewport.ActualHeight));
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        var available = PreviewColumn.ActualWidth + MetadataColumn.ActualWidth;
        if (available > 0)
            _viewModel.SetSplitRatio(PreviewColumn.ActualWidth / available);
    }

    private void BrowserTreeSplitter_DragCompleted(object sender, DragCompletedEventArgs e) =>
        _viewModel.SetLayoutSizes(FolderTreeColumn.ActualWidth, BrowserRow.ActualHeight);

    private void BrowserHeightSplitter_DragCompleted(object sender, DragCompletedEventArgs e) =>
        _viewModel.SetLayoutSizes(FolderTreeColumn.ActualWidth, BrowserRow.ActualHeight);

    private void ToggleFullScreen_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();
    private void ToggleFullScreen()
    {
        if (!_isFullscreen)
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedResizeMode = ResizeMode;
            _savedRootMargin = RootGrid.Margin;
            NormalViewportHost.Content = null;
            FullscreenViewportHost.Content = _viewport;
            MainLayout.Visibility = Visibility.Collapsed;
            FullscreenOverlay.Visibility = Visibility.Visible;
            RootGrid.Margin = new Thickness(0);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            _isFullscreen = true;
            _viewport.Focus();
        }
        else
        {
            FullscreenViewportHost.Content = null;
            NormalViewportHost.Content = _viewport;
            FullscreenOverlay.Visibility = Visibility.Collapsed;
            MainLayout.Visibility = Visibility.Visible;
            WindowState = _savedWindowState;
            WindowStyle = _savedWindowStyle;
            ResizeMode = _savedResizeMode;
            RootGrid.Margin = _savedRootMargin;
            _isFullscreen = false;
            _viewport.Focus();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsDarkTheme))
            ApplyTheme(_viewModel.IsDarkTheme);
        else if (e.PropertyName == nameof(ViewerViewModel.IsBrowserExpanded))
            ApplyBrowserVisibility();
        else if (e.PropertyName == nameof(ViewerViewModel.BrowserViewMode) && _viewModel.IsThumbnailView)
        {
            Dispatcher.BeginInvoke(async () => await LoadVisibleThumbnailsAsync());
        }
    }

    private void ApplyBrowserVisibility()
    {
        if (_viewModel.IsBrowserExpanded)
        {
            BrowserRow.MinHeight = 190;
            BrowserRow.Height = new GridLength(Math.Clamp(_viewModel.BrowserHeight, 190, 620));
            BrowserSplitterRow.Height = new GridLength(7);
        }
        else
        {
            if (BrowserRow.ActualHeight >= 190)
                _viewModel.SetLayoutSizes(FolderTreeColumn.ActualWidth, BrowserRow.ActualHeight);
            BrowserRow.MinHeight = 0;
            BrowserRow.Height = new GridLength(0);
            BrowserSplitterRow.Height = new GridLength(0);
        }
    }

    private void ApplyTheme(bool dark)
    {
        var colors = dark
            ? new Dictionary<string, string>
            {
                ["WindowBrush"] = "#151A21", ["PanelBrush"] = "#202730", ["PanelAltBrush"] = "#29333F",
                ["BorderBrush"] = "#3B4755", ["TextBrush"] = "#ECF1F7", ["MutedTextBrush"] = "#A3B0C0",
                ["AccentBrush"] = "#5D9CEC", ["AccentTextBrush"] = "#FFFFFF", ["HighlightBrush"] = "#FFD43B",
                ["PreviewBrush"] = "#0C1015", ["InputBrush"] = "#29333F"
            }
            : new Dictionary<string, string>
            {
                ["WindowBrush"] = "#F3F6FB", ["PanelBrush"] = "#FFFFFF", ["PanelAltBrush"] = "#EAF0F7",
                ["BorderBrush"] = "#D4DCE8", ["TextBrush"] = "#17243A", ["MutedTextBrush"] = "#65738A",
                ["AccentBrush"] = "#1769D2", ["AccentTextBrush"] = "#FFFFFF", ["HighlightBrush"] = "#FFD43B",
                ["PreviewBrush"] = "#111820", ["InputBrush"] = "#FFFFFF"
            };

        foreach (var (key, value) in colors)
            System.Windows.Application.Current.Resources[key] = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "EXIF 全媒体元数据查看器", MessageBoxButton.OK, MessageBoxImage.Information);
}
