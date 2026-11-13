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
    private CancellationTokenSource? _browserCancellation;
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
    private string _addressText = "";
    private string _browserStatus = "选择文件夹以浏览媒体";
    private bool _isBrowserExpanded;
    private string _browserViewMode = "缩略图";
    private string _browserSortMode = "名称";
    private BrowserEntry? _selectedBrowserEntry;
    private DirectoryTreeNode? _selectedDirectoryNode;
    private double _splitRatio = 0.49;
    private double _browserTreeWidth = 260;
    private double _browserHeight = 300;
    private double _thumbnailSize = 150;
    private long _browserGeneration;
    private readonly List<string> _directoryHistory = [];
    private int _directoryHistoryIndex = -1;
    private bool _syncingTreeSelection;

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
        _browserViewMode = settings.BrowserViewMode;
        _isBrowserExpanded = settings.IsBrowserExpanded;
        _browserTreeWidth = settings.BrowserTreeWidth;
        _browserHeight = settings.BrowserHeight;
        _thumbnailSize = settings.ThumbnailSize;
        _isDarkTheme = settings.IsDarkTheme;
        BrowserEntries = new ObservableCollection<BrowserEntry>();
        DirectoryRoots = new ObservableCollection<DirectoryTreeNode>(_fileBrowserService.GetDriveRoots());
        SummarySections = [];
        BrowserSortModes = ["名称", "类型", "修改时间", "大小"];
        BrowserViewModes = ["缩略图", "列表", "详细信息"];
    }

    public BulkObservableCollection<MetadataField> Fields { get; }
    public BulkObservableCollection<MetadataField> SummaryFields { get; }
    public ObservableCollection<string> RecentFiles { get; }
    public ObservableCollection<BrowserEntry> BrowserEntries { get; }
    public ObservableCollection<DirectoryTreeNode> DirectoryRoots { get; }
    public IReadOnlyList<MetadataSummarySection> SummarySections { get; private set; }
    public IReadOnlyList<string> BrowserSortModes { get; }
    public IReadOnlyList<string> BrowserViewModes { get; }
    public string CurrentDirectory { get => _currentDirectory; private set => SetProperty(ref _currentDirectory, value); }
    public string AddressText { get => _addressText; set => SetProperty(ref _addressText, value); }
    public string BrowserStatus { get => _browserStatus; private set => SetProperty(ref _browserStatus, value); }
    public bool IsBrowserExpanded { get => _isBrowserExpanded; set { if (SetProperty(ref _isBrowserExpanded, value)) { OnPropertyChanged(nameof(BrowserButtonText)); SaveSettings(); } } }
    public string BrowserViewMode
    {
        get => _browserViewMode;
        set
        {
            var normalized = value is "缩略图" or "列表" or "详细信息" ? value : "缩略图";
            if (!SetProperty(ref _browserViewMode, normalized)) return;
            OnPropertyChanged(nameof(IsThumbnailView));
            OnPropertyChanged(nameof(IsListView));
            OnPropertyChanged(nameof(IsDetailsView));
            SaveSettings();
        }
    }
    public bool IsThumbnailView => BrowserViewMode == "缩略图";
    public bool IsListView => BrowserViewMode == "列表";
    public bool IsDetailsView => BrowserViewMode == "详细信息";
    public string BrowserSortMode { get => _browserSortMode; set { if (SetProperty(ref _browserSortMode, value)) { SaveSettings(); _ = RefreshBrowserAsync(); } } }
    public BrowserEntry? SelectedBrowserEntry { get => _selectedBrowserEntry; set => SetProperty(ref _selectedBrowserEntry, value); }
    public DirectoryTreeNode? SelectedDirectoryNode { get => _selectedDirectoryNode; private set => SetProperty(ref _selectedDirectoryNode, value); }
    public double SplitRatio => _splitRatio;
    public double BrowserTreeWidth => _browserTreeWidth;
    public double BrowserHeight => _browserHeight;
    public double ThumbnailSize
    {
        get => _thumbnailSize;
        set
        {
            if (SetProperty(ref _thumbnailSize, Math.Clamp(value, 96, 260)))
            {
                OnPropertyChanged(nameof(ThumbnailImageHeight));
                SaveSettings();
            }
        }
    }
    public double ThumbnailImageHeight => Math.Round(ThumbnailSize * 0.68);
    public string BrowserButtonText => IsBrowserExpanded ? "隐藏浏览器" : "文件浏览器";
    public string FileCountText => $"{BrowserEntries.Count:N0} 项";
    public bool CanGoParent => !string.IsNullOrWhiteSpace(CurrentDirectory) && Directory.GetParent(CurrentDirectory) is not null;
    public bool CanGoBack => _directoryHistoryIndex > 0;
    public bool CanGoForward => _directoryHistoryIndex >= 0 && _directoryHistoryIndex < _directoryHistory.Count - 1;
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
            {
                OnPropertyChanged(nameof(ThemeButtonText));
                SaveSettings();
            }
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

    public async Task NavigateDirectoryAsync(string directory, string? selectPath = null, bool addToHistory = true)
    {
        var generation = Interlocked.Increment(ref _browserGeneration);
        _browserCancellation?.Cancel();
        _browserCancellation?.Dispose();
        _browserCancellation = new CancellationTokenSource();
        var token = _browserCancellation.Token;
        try
        {
            var targetDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directory.Trim()));
            if (!Directory.Exists(targetDirectory))
                throw new DirectoryNotFoundException($"找不到文件夹：{targetDirectory}");
            BrowserStatus = $"正在读取 {targetDirectory}…";
            var entries = await _fileBrowserService.EnumerateAsync(targetDirectory, BrowserSortMode, token).ConfigureAwait(true);
            if (generation != _browserGeneration || token.IsCancellationRequested)
                return;
            CurrentDirectory = targetDirectory;
            AddressText = targetDirectory;
            BrowserEntries.Clear();
            foreach (var entry in entries)
                BrowserEntries.Add(entry);
            SelectedBrowserEntry = selectPath is null ? null : BrowserEntries.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, Path.GetFullPath(selectPath), StringComparison.OrdinalIgnoreCase));
            BrowserStatus = $"{CurrentDirectory} · {BrowserEntries.Count:N0} 项";
            RebuildFolderNavigation();
            if (addToHistory)
                AddDirectoryHistory(CurrentDirectory);
            OnPropertyChanged(nameof(FileCountText));
            OnPropertyChanged(nameof(CanGoParent));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
            await SelectTreePathAsync(CurrentDirectory, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            BrowserStatus = $"无法读取文件夹：{exception.Message}";
            AddressText = CurrentDirectory;
            StatusText = BrowserStatus;
        }
    }

    public async Task GoBackDirectoryAsync()
    {
        if (!CanGoBack) return;
        _directoryHistoryIndex--;
        await NavigateDirectoryAsync(_directoryHistory[_directoryHistoryIndex], addToHistory: false).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public async Task GoForwardDirectoryAsync()
    {
        if (!CanGoForward) return;
        _directoryHistoryIndex++;
        await NavigateDirectoryAsync(_directoryHistory[_directoryHistoryIndex], addToHistory: false).ConfigureAwait(true);
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    public async Task GoParentDirectoryAsync()
    {
        var parent = string.IsNullOrWhiteSpace(CurrentDirectory) ? null : Directory.GetParent(CurrentDirectory);
        if (parent is not null)
            await NavigateDirectoryAsync(parent.FullName).ConfigureAwait(true);
    }

    public Task RefreshBrowserAsync() => string.IsNullOrWhiteSpace(CurrentDirectory)
        ? Task.CompletedTask
        : NavigateDirectoryAsync(CurrentDirectory, FilePath, addToHistory: false);

    public async Task LoadDirectoryChildrenAsync(DirectoryTreeNode node)
    {
        if (node.IsPlaceholder || node.ChildrenLoaded || !Directory.Exists(node.FullPath))
            return;
        node.BeginLoading();
        try
        {
            var children = await _fileBrowserService.EnumerateDirectoriesAsync(node.FullPath).ConfigureAwait(true);
            node.CompleteLoading(children);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            node.FailLoading();
            BrowserStatus = $"无法展开文件夹：{exception.Message}";
        }
    }

    public async Task SelectDirectoryNodeAsync(DirectoryTreeNode? node)
    {
        if (node is null || node.IsPlaceholder || _syncingTreeSelection)
            return;
        SelectedDirectoryNode = node;
        if (!string.Equals(node.FullPath, CurrentDirectory, StringComparison.OrdinalIgnoreCase))
            await NavigateDirectoryAsync(node.FullPath).ConfigureAwait(true);
    }

    public void SetSplitRatio(double ratio)
    {
        _splitRatio = Math.Clamp(ratio, 0.3, 0.7);
        SaveSettings();
    }

    public void SetLayoutSizes(double browserTreeWidth, double browserHeight)
    {
        _browserTreeWidth = Math.Clamp(browserTreeWidth, 170, 520);
        _browserHeight = Math.Clamp(browserHeight, 190, 620);
        OnPropertyChanged(nameof(BrowserTreeWidth));
        OnPropertyChanged(nameof(BrowserHeight));
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
        var generation = _browserGeneration;
        var token = _browserCancellation?.Token ?? CancellationToken.None;
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        try
        {
            var result = await _previewService.LoadThumbnailAsync(entry.FullPath, ffmpeg, token)
                .ConfigureAwait(true);
            if (generation == _browserGeneration && !token.IsCancellationRequested)
                entry.CompleteThumbnailLoad(result.Image, result.Width, result.Height);
            else
                entry.CancelThumbnailLoad();
        }
        catch (OperationCanceledException)
        {
            entry.CancelThumbnailLoad();
        }
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
            FilePath = entry.FullPath;
            PreviewImage = null;
            PreviewMessage = $"{entry.Name}\n不支持预览；文件仍保留在浏览器中。";
            Fields.Clear();
            SummaryFields.Clear();
            SummarySections = [];
            Analysis = null;
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(FileSizeText));
            OnPropertyChanged(nameof(FileTypeText));
            OnPropertyChanged(nameof(FieldCountText));
            OnPropertyChanged(nameof(ParserSummary));
            OnPropertyChanged(nameof(RawOutput));
            OnPropertyChanged(nameof(SummarySections));
            StatusText = BrowserStatus;
            return;
        }
        if (string.Equals(entry.FullPath, FilePath, StringComparison.OrdinalIgnoreCase))
            return;
        IsBrowserExpanded = true;
        RebuildFolderNavigation(entry.FullPath);
        await LoadFileAsync(entry.FullPath).ConfigureAwait(true);
        SelectedBrowserEntry = entry;
    }

    public async Task GoPreviousAsync()
    {
        if (CanGoPrevious)
        {
            _folderIndex--;
            await LoadFileAsync(_folderFiles[_folderIndex]).ConfigureAwait(true);
            SyncSelectedBrowserEntry();
        }
        RefreshNavigation();
    }

    public async Task GoNextAsync()
    {
        if (CanGoNext)
        {
            _folderIndex++;
            await LoadFileAsync(_folderFiles[_folderIndex]).ConfigureAwait(true);
            SyncSelectedBrowserEntry();
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
                var preview = await _previewService.LoadAsync(analysis, exifTool, ffmpeg, token).ConfigureAwait(true);
                if (token.IsCancellationRequested || generation != _loadGeneration)
                    return;
                PreviewImage = preview.Image;
                PreviewMessage = preview.Description;
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

    private void AddDirectoryHistory(string directory)
    {
        if (_directoryHistoryIndex >= 0 &&
            string.Equals(_directoryHistory[_directoryHistoryIndex], directory, StringComparison.OrdinalIgnoreCase))
            return;
        if (_directoryHistoryIndex < _directoryHistory.Count - 1)
            _directoryHistory.RemoveRange(_directoryHistoryIndex + 1, _directoryHistory.Count - _directoryHistoryIndex - 1);
        _directoryHistory.Add(directory);
        _directoryHistoryIndex = _directoryHistory.Count - 1;
    }

    private void RebuildFolderNavigation(string? selectedPath = null)
    {
        _folderFiles = BrowserEntries
            .Where(item => !item.IsDirectory && MediaAnalyzer.IsKnownMediaFile(item.FullPath))
            .Select(item => item.FullPath)
            .ToArray();
        var target = selectedPath ?? FilePath;
        _folderIndex = string.IsNullOrWhiteSpace(target)
            ? -1
            : Array.FindIndex(_folderFiles.ToArray(), path =>
                string.Equals(path, target, StringComparison.OrdinalIgnoreCase));
        RefreshNavigation();
    }

    private void SyncSelectedBrowserEntry()
    {
        if (_folderIndex < 0 || _folderIndex >= _folderFiles.Count)
            return;
        SelectedBrowserEntry = BrowserEntries.FirstOrDefault(entry =>
            string.Equals(entry.FullPath, _folderFiles[_folderIndex], StringComparison.OrdinalIgnoreCase));
    }

    private async Task SelectTreePathAsync(string directory, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(directory);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
            return;
        var node = DirectoryRoots.FirstOrDefault(candidate =>
            string.Equals(Path.GetFullPath(candidate.FullPath), Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase));
        if (node is null)
            return;

        var relative = Path.GetRelativePath(rootPath, fullPath);
        if (relative != ".")
        {
            foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LoadDirectoryChildrenAsync(node).ConfigureAwait(true);
                node.IsExpanded = true;
                var next = node.Children.FirstOrDefault(child =>
                    !child.IsPlaceholder && child.DisplayName.Equals(segment, StringComparison.OrdinalIgnoreCase));
                if (next is null)
                    break;
                node = next;
            }
        }

        _syncingTreeSelection = true;
        try
        {
            if (SelectedDirectoryNode is not null)
                SelectedDirectoryNode.IsSelected = false;
            SelectedDirectoryNode = node;
            node.IsSelected = true;
        }
        finally
        {
            _syncingTreeSelection = false;
        }
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
                BrowserTreeWidth = _browserTreeWidth,
                BrowserHeight = _browserHeight,
                ThumbnailSize = ThumbnailSize,
                BrowserSortMode = BrowserSortMode,
                BrowserViewMode = BrowserViewMode,
                IsBrowserExpanded = IsBrowserExpanded,
                IsDarkTheme = IsDarkTheme
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
