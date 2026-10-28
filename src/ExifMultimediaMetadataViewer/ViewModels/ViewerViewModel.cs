using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;
using ExifMultimediaMetadataViewer.Services;

namespace ExifMultimediaMetadataViewer.ViewModels;

public sealed class ViewerViewModel : ObservableObject
{
    private readonly MediaAnalyzer _analyzer = new();
    private readonly MediaPreviewService _previewService = new();
    private readonly RecentFilesStore _recentFilesStore = new();
    private readonly FileBrowserService _fileBrowserService = new();
    private readonly ViewerSettingsStore _settingsStore = new();
    private readonly ICollectionView _filteredFields;
    private CancellationTokenSource? _loadCancellation;
    private IReadOnlyList<string> _folderFiles = [];
    private int _folderIndex = -1;
    private MediaAnalysis? _analysis;
    private MetadataField? _selectedField;
    private BitmapSource? _previewImage;
    private string _filePath = "";
    private string _statusText = "打开照片或视频以查看其解析结果。";
    private string _searchText = "";
    private string _previewMessage = "在此预览照片或视频首帧";
    private string _basicMessage = "尚未读取文件";
    private bool _isBusy;
    private bool _isDarkTheme;
    private bool _isFitPreview = true;
    private double _zoom = 1;
    private double _previewRotation;
    private string _previewScale = "适应窗口";
    private long _loadGeneration;
    private string _currentDirectory = "";
    private string _browserStatus = "选择文件夹以浏览媒体";
    private bool _isBrowserExpanded;
    private bool _isThumbnailView = true;
    private string _browserSortMode = "名称";
    private BrowserEntry? _selectedBrowserEntry;
    private double _splitRatio = 0.49;
    private long _browserGeneration;

    public ViewerViewModel()
    {
        Fields = new BulkObservableCollection<MetadataField>();
        SummaryFields = new BulkObservableCollection<MetadataField>();
        RecentFiles = new ObservableCollection<string>(_recentFilesStore.Load());
        _filteredFields = CollectionViewSource.GetDefaultView(Fields);
        _filteredFields.Filter = FilterField;
        var settings = _settingsStore.Load();
        _splitRatio = settings.SplitRatio;
        _browserSortMode = settings.BrowserSortMode;
        _isThumbnailView = settings.IsThumbnailView;
        _isBrowserExpanded = settings.IsBrowserExpanded;
        BrowserEntries = new ObservableCollection<BrowserEntry>();
        SummarySections = [];
        BrowserSortModes = ["名称", "类型", "修改时间", "大小"];
    }

    public BulkObservableCollection<MetadataField> Fields { get; }
    public BulkObservableCollection<MetadataField> SummaryFields { get; }
    public ObservableCollection<string> RecentFiles { get; }
    public ObservableCollection<BrowserEntry> BrowserEntries { get; }
    public IReadOnlyList<MetadataSummarySection> SummarySections { get; private set; }
    public IReadOnlyList<string> BrowserSortModes { get; }
    public string CurrentDirectory { get => _currentDirectory; set => SetProperty(ref _currentDirectory, value); }
    public string BrowserStatus { get => _browserStatus; private set => SetProperty(ref _browserStatus, value); }
    public bool IsBrowserExpanded { get => _isBrowserExpanded; set { if (SetProperty(ref _isBrowserExpanded, value)) { OnPropertyChanged(nameof(BrowserButtonText)); SaveSettings(); } } }
    public bool IsThumbnailView { get => _isThumbnailView; set { if (SetProperty(ref _isThumbnailView, value)) SaveSettings(); } }
    public string BrowserSortMode { get => _browserSortMode; set { if (SetProperty(ref _browserSortMode, value)) { SaveSettings(); _ = RefreshBrowserAsync(); } } }
    public BrowserEntry? SelectedBrowserEntry { get => _selectedBrowserEntry; set => SetProperty(ref _selectedBrowserEntry, value); }
    public double SplitRatio => _splitRatio;
    public string BrowserButtonText => IsBrowserExpanded ? "隐藏浏览器" : "文件浏览器";
    public string FileCountText => $"{BrowserEntries.Count:N0} 项";
    public bool CanGoParent => !string.IsNullOrWhiteSpace(CurrentDirectory) && Directory.GetParent(CurrentDirectory) is not null;
    public ICollectionView FilteredFields => _filteredFields;
    public MediaAnalysis? Analysis { get => _analysis; private set => SetProperty(ref _analysis, value); }
    public MetadataField? SelectedField { get => _selectedField; set => SetProperty(ref _selectedField, value); }
    public BitmapSource? PreviewImage { get => _previewImage; private set => SetProperty(ref _previewImage, value); }
    public string FilePath { get => _filePath; private set => SetProperty(ref _filePath, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                _filteredFields.Refresh();
        }
    }
    public string PreviewMessage { get => _previewMessage; private set => SetProperty(ref _previewMessage, value); }
    public string BasicMessage { get => _basicMessage; private set => SetProperty(ref _basicMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
                OnPropertyChanged(nameof(ThemeButtonText));
        }
    }
    public bool IsFitPreview { get => _isFitPreview; private set => SetProperty(ref _isFitPreview, value); }
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, Math.Clamp(value, 0.005, 8.0)); }
    public double PreviewRotation { get => _previewRotation; set => SetProperty(ref _previewRotation, (value % 360 + 360) % 360); }
    public string PreviewScale { get => _previewScale; private set => SetProperty(ref _previewScale, value); }
    public string ThemeButtonText => IsDarkTheme ? "浅色主题" : "深色主题";
    public string FileName => string.IsNullOrWhiteSpace(FilePath) ? "未打开文件" : Path.GetFileName(FilePath);
    public string FileSizeText => Analysis is null ? "" : FormatBytes(Analysis.FileSize);
    public string FileTypeText => Analysis is null ? "" : Path.GetExtension(Analysis.FilePath).TrimStart('.').ToUpperInvariant();
    public string FieldCountText => Analysis is null ? "" : $"{Analysis.AllFields.Count:N0} 个字段";
    public string ParserSummary => Analysis is null
        ? ""
        : $"ExifTool：{StatusLabel(Analysis.ExifTool)}    FFprobe：{StatusLabel(Analysis.FFprobe)}";
    public string RawOutput
    {
        get
        {
            if (Analysis is null)
                return "尚无解析器输出。";
            var builder = new StringBuilder();
            builder.AppendLine("===== ExifTool JSON (原始输出) =====");
            builder.AppendLine(Analysis.ExifTool.RawJson);
            builder.AppendLine();
            builder.AppendLine("===== ExifTool stderr =====");
            builder.AppendLine(Analysis.ExifTool.StandardError);
            builder.AppendLine();
            builder.AppendLine("===== FFprobe JSON (原始输出) =====");
            builder.AppendLine(Analysis.FFprobe.RawJson);
            builder.AppendLine();
            builder.AppendLine("===== FFprobe stderr =====");
            builder.AppendLine(Analysis.FFprobe.StandardError);
            return builder.ToString();
        }
    }

    public bool CanGoPrevious => _folderIndex > 0 && _folderFiles.Count > 0 && !IsBusy;
    public bool CanGoNext => _folderIndex >= 0 && _folderIndex < _folderFiles.Count - 1 && !IsBusy;

    public async Task OpenAsync(IEnumerable<string> paths, string? preferredPath = null)
    {
        _folderFiles = paths
            .Where(path => File.Exists(path) && MediaAnalyzer.IsKnownMediaFile(path))
            .Select(Path.GetFullPath)
            .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var target = preferredPath is null
            ? _folderFiles.FirstOrDefault()
            : _folderFiles.FirstOrDefault(path => string.Equals(path, Path.GetFullPath(preferredPath), StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            StatusText = "所选位置中没有可读取的照片或视频。";
            return;
        }
        _folderIndex = Array.IndexOf(_folderFiles.ToArray(), target);
        RefreshNavigation();
        await NavigateDirectoryAsync(Path.GetDirectoryName(target)!).ConfigureAwait(true);
        await LoadFileAsync(target).ConfigureAwait(true);
        SelectedBrowserEntry = BrowserEntries.FirstOrDefault(entry =>
            string.Equals(entry.FullPath, target, StringComparison.OrdinalIgnoreCase));
    }

    public async Task NavigateDirectoryAsync(string directory, string? selectPath = null)
    {
        var generation = Interlocked.Increment(ref _browserGeneration);
        try
        {
            CurrentDirectory = Path.GetFullPath(directory);
            var entries = await _fileBrowserService.EnumerateAsync(CurrentDirectory, BrowserSortMode).ConfigureAwait(true);
            if (generation != _browserGeneration)
                return;
            BrowserEntries.Clear();
            foreach (var entry in entries)
                BrowserEntries.Add(entry);
            SelectedBrowserEntry = selectPath is null ? null : BrowserEntries.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, Path.GetFullPath(selectPath), StringComparison.OrdinalIgnoreCase));
            BrowserStatus = $"{CurrentDirectory} · {BrowserEntries.Count:N0} 项";
            OnPropertyChanged(nameof(FileCountText));
            OnPropertyChanged(nameof(CanGoParent));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            BrowserStatus = $"无法读取文件夹：{exception.Message}";
        }
    }

    public async Task GoParentDirectoryAsync()
    {
        var parent = string.IsNullOrWhiteSpace(CurrentDirectory) ? null : Directory.GetParent(CurrentDirectory);
        if (parent is not null)
            await NavigateDirectoryAsync(parent.FullName).ConfigureAwait(true);
    }

    public Task RefreshBrowserAsync() => string.IsNullOrWhiteSpace(CurrentDirectory)
        ? Task.CompletedTask
        : NavigateDirectoryAsync(CurrentDirectory, FilePath);

    public void SetSplitRatio(double ratio)
    {
        _splitRatio = Math.Clamp(ratio, 0.3, 0.7);
        SaveSettings();
    }

    public void SetPreviewStatus(string message)
    {
        PreviewMessage = message;
        if (Analysis is not null)
        {
            SummarySections = MetadataSummaryBuilder.Build(Analysis, message);
            OnPropertyChanged(nameof(SummarySections));
        }
    }

    public async Task LoadThumbnailAsync(BrowserEntry entry)
    {
        if (!entry.TryBeginThumbnailLoad())
            return;
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        var result = await _previewService.LoadThumbnailAsync(entry.FullPath, ffmpeg, CancellationToken.None)
            .ConfigureAwait(true);
        entry.CompleteThumbnailLoad(result.Image, result.Width, result.Height);
    }

    public async Task OpenBrowserEntryAsync(BrowserEntry? entry)
    {
        if (entry is null)
            return;
        if (entry.IsDirectory)
        {
            await NavigateDirectoryAsync(entry.FullPath).ConfigureAwait(true);
            return;
        }
        if (!MediaAnalyzer.IsKnownMediaFile(entry.FullPath))
        {
            BrowserStatus = $"不支持的文件类型：{entry.Extension}";
            return;
        }
        if (string.Equals(entry.FullPath, FilePath, StringComparison.OrdinalIgnoreCase))
            return;
        IsBrowserExpanded = true;
        _folderFiles = BrowserEntries.Where(item => !item.IsDirectory && MediaAnalyzer.IsKnownMediaFile(item.FullPath))
            .Select(item => item.FullPath)
            .ToArray();
        _folderIndex = Array.FindIndex(_folderFiles.ToArray(), path =>
            string.Equals(path, entry.FullPath, StringComparison.OrdinalIgnoreCase));
        RefreshNavigation();
        await LoadFileAsync(entry.FullPath).ConfigureAwait(true);
        SelectedBrowserEntry = entry;
    }

    public async Task GoPreviousAsync()
    {
        if (CanGoPrevious)
        {
            _folderIndex--;
            await LoadFileAsync(_folderFiles[_folderIndex]).ConfigureAwait(true);
        }
        RefreshNavigation();
    }

    public async Task GoNextAsync()
    {
        if (CanGoNext)
        {
            _folderIndex++;
            await LoadFileAsync(_folderFiles[_folderIndex]).ConfigureAwait(true);
        }
        RefreshNavigation();
    }

    public async Task OpenRecentAsync(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !File.Exists(path))
        {
            RecentFiles.Remove(path);
            _recentFilesStore.Save(RecentFiles);
            StatusText = "最近文件已不存在，已从列表移除。";
            return;
        }

        var files = await Task.Run(() => Directory.EnumerateFiles(directory)
            .Where(MediaAnalyzer.IsKnownMediaFile)
            .ToArray());
        await OpenAsync(files, path).ConfigureAwait(true);
    }

    public void Cancel()
    {
        if (IsBusy)
        {
            StatusText = "正在取消解析…";
            _loadCancellation?.Cancel();
        }
    }

    public void SetStatus(string message) => StatusText = message;

    public void RotatePreview() => PreviewRotation = (PreviewRotation + 90) % 360;

    public void TogglePreviewSize()
    {
        IsFitPreview = !IsFitPreview;
        Zoom = IsFitPreview ? 1 : 2;
        PreviewScale = IsFitPreview ? "适应窗口" : "放大预览";
    }

    public void ResetPreviewSize()
    {
        IsFitPreview = true;
        Zoom = 1;
        PreviewScale = "适应窗口";
    }

    public async Task ExportJsonAsync(string destinationPath)
    {
        if (Analysis is null)
            throw new InvalidOperationException("请先打开媒体文件。");
        await MetadataExportService.ExportJsonAsync(Analysis, destinationPath).ConfigureAwait(true);
        StatusText = $"已导出完整 JSON：{destinationPath}";
    }

    public async Task ExportCsvAsync(string destinationPath)
    {
        if (Analysis is null)
            throw new InvalidOperationException("请先打开媒体文件。");
        await MetadataExportService.ExportCsvAsync(Analysis, destinationPath).ConfigureAwait(true);
        StatusText = $"已导出全部 {Analysis.AllFields.Count:N0} 个字段：{destinationPath}";
    }

    public string CopyAllFields()
    {
        if (Analysis is null)
            return "";
        return string.Join(Environment.NewLine, Analysis.AllFields.Select(field =>
            $"{field.Source}\t{field.FullPath}\t{field.TagId}\t{field.RawValue}\t{field.FormattedValue}"));
    }

    private async Task LoadFileAsync(string path)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;
        FilePath = Path.GetFullPath(path);
        RememberRecentFile(FilePath);
        OnPropertyChanged(nameof(FileName));
        PreviewImage = null;
        PreviewRotation = 0;
        PreviewMessage = "正在读取预览…";
        StatusText = $"正在读取 {FileName}…";
        IsBusy = true;
        Fields.Clear();
        SummaryFields.Clear();
        Analysis = null;
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(FileTypeText));
        OnPropertyChanged(nameof(FieldCountText));
        OnPropertyChanged(nameof(ParserSummary));
        OnPropertyChanged(nameof(RawOutput));
        _filteredFields.Refresh();

        try
        {
            var exifTool = Path.Combine(AppContext.BaseDirectory, "tools", "exiftool", "exiftool.exe");
            var ffprobe = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffprobe.exe");
            var ffmpeg = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");

            var analysis = await _analyzer.AnalyzeAsync(FilePath, exifTool, ffprobe, token).ConfigureAwait(true);
            if (token.IsCancellationRequested || generation != _loadGeneration)
                return;
            Analysis = analysis;
            Fields.ReplaceAll(analysis.AllFields);
            SummaryFields.ReplaceAll(GetSummaryFields(analysis));

            OnPropertyChanged(nameof(FileSizeText));
            OnPropertyChanged(nameof(FileTypeText));
            OnPropertyChanged(nameof(FieldCountText));
            OnPropertyChanged(nameof(ParserSummary));
            OnPropertyChanged(nameof(RawOutput));
            _filteredFields.Refresh();
            BasicMessage = SummaryFields.Count == 0
                ? "解析器没有返回下列概览字段；请在“全部元数据”中搜索完整结果。"
                : $"展示 {SummaryFields.Count:N0} 个易读概览字段；完整解析结果仍保存在“全部元数据”和原始 JSON 中。";
            SummarySections = MetadataSummaryBuilder.Build(analysis, "正在准备预览");
            OnPropertyChanged(nameof(SummarySections));
            StatusText = $"{FileName} · {analysis.AllFields.Count:N0} 个字段 · {analysis.ExifTool.Message} {analysis.FFprobe.Message}";
            RefreshNavigation();

            try
            {
                var image = await _previewService.LoadAsync(analysis, exifTool, ffmpeg, token).ConfigureAwait(true);
                if (token.IsCancellationRequested || generation != _loadGeneration)
                    return;
                PreviewImage = image;
                PreviewMessage = image is null
                    ? MediaAnalyzer.IsVideoFile(FilePath)
                        ? "视频缩略图不可用；视频元数据仍可查看。"
                        : "不支持预览或图像解码失败；元数据读取结果仍可查看。"
                    : IsVideo(FilePath)
                        ? "视频首帧预览 · 本地 FFmpeg 解码"
                        : "照片预览";
                SummarySections = MetadataSummaryBuilder.Build(analysis, PreviewMessage);
                OnPropertyChanged(nameof(SummarySections));
            }
            catch (OperationCanceledException)
            {
                if (token.IsCancellationRequested)
                    throw;
                PreviewMessage = "预览提取已停止；元数据解析结果仍可查看。";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "解析已取消。";
            PreviewMessage = "预览读取已取消。";
        }
        catch (Exception exception)
        {
            StatusText = $"打开失败：{exception.Message}";
            PreviewMessage = "预览不可用；请查看解析器状态与错误信息。";
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsBusy = false;
                RefreshNavigation();
            }
        }
    }

    private static IEnumerable<MetadataField> GetSummaryFields(MediaAnalysis analysis)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FileName", "FileType", "FileSize", "ImageWidth", "ImageHeight", "Make", "Model",
            "LensModel", "ExposureTime", "FNumber", "ISO", "FocalLength", "DateTimeOriginal",
            "CreateDate", "ModifyDate", "GPSLatitude", "GPSLongitude", "GPSAltitude",
            "Duration", "codec_name", "codec_long_name", "width", "height", "r_frame_rate",
            "avg_frame_rate", "bit_rate", "sample_rate", "channels", "channel_layout"
        };
        return analysis.AllFields.Where(field => names.Contains(field.TagName));
    }

    private bool FilterField(object item)
    {
        if (item is not MetadataField field)
            return false;
        if (string.IsNullOrWhiteSpace(SearchText))
            return true;
        var search = SearchText.Trim();
        return field.TagName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || field.FullPath.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || field.Group.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || (field.TagId?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || field.RawValue.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || field.FormattedValue.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || field.Source.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshNavigation()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(new ViewerSettings
            {
                SplitRatio = _splitRatio,
                BrowserSortMode = BrowserSortMode,
                IsThumbnailView = IsThumbnailView,
                IsBrowserExpanded = IsBrowserExpanded
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"界面设置无法保存：{exception.Message}";
        }
    }

    private void RememberRecentFile(string path)
    {
        var existing = RecentFiles.FirstOrDefault(item =>
            string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            RecentFiles.Remove(existing);
        RecentFiles.Insert(0, path);
        while (RecentFiles.Count > 10)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        try
        {
            _recentFilesStore.Save(RecentFiles);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText = $"最近文件列表无法保存：{exception.Message}";
        }
    }

    private static bool IsVideo(string path) => MediaAnalyzer.IsVideoFile(path);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    private static string StatusLabel(ParserResult result)
    {
        if (result.Status == ParserStatus.NotRun)
            return "未运行";
        var elapsed = result.Elapsed is null ? "" : $" · {result.Elapsed.Value.TotalSeconds:0.0} 秒";
        return $"{result.Status} · {result.Fields.Count:N0} 字段{elapsed}";
    }
}
