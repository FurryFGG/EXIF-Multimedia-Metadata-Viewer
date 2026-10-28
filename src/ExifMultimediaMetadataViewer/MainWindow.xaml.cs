using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var commandLineFile = Environment.GetCommandLineArgs()
            .Skip(1)
            .FirstOrDefault(File.Exists);
        if (commandLineFile is not null)
            await OpenFileWithNeighborsAsync(commandLineFile);
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
            var files = await Task.Run(() => Directory.EnumerateFiles(dialog.FolderName)
                .Where(ExifMetadata.Core.Services.MediaAnalyzer.IsKnownMediaFile)
                .ToArray());
            await _viewModel.OpenAsync(files);
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
    private void Rotate_Click(object sender, RoutedEventArgs e) => _viewModel.RotatePreview();
    private void PreviewScale_Click(object sender, RoutedEventArgs e) => _viewModel.TogglePreviewSize();
    private void FitPreview_Click(object sender, RoutedEventArgs e) => _viewModel.ResetPreviewSize();

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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsDarkTheme))
            ApplyTheme(_viewModel.IsDarkTheme);
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
