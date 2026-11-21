using ExifMultimediaMetadataViewer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Graphics;

namespace MetaLens.WinUI;
public sealed partial class MainWindow : Window
{
    readonly ViewerSettingsStore store = new();
    readonly RecentFilesStore history = new();
    readonly CancellationTokenSource lifetime = new();
    ViewerSettings settings;
    List<string> recent = [];
    bool ready, closing;
    internal event Action<string>? PathRequested;
    internal event Action? CancelRequested;
    public MainWindow()
    {
        InitializeComponent();
        settings = store.Load();
        ApplyTheme();
        AppWindow.Resize(new SizeInt32(1280, 820));
        Root.Loaded += OnReady;
        Closed += (_, _) =>
        {
            closing = true;
            lifetime.Cancel();
            CancelRequested?.Invoke();
            store.Dispose();
        };
    }

    void ApplyTheme() => Root.RequestedTheme = settings.IsDarkTheme ? ElementTheme.Dark : ElementTheme.Light;
    void ThemeClick(object sender, RoutedEventArgs e)
    {
        settings = settings with
        {
            IsDarkTheme = !settings.IsDarkTheme
        };
        ApplyTheme();
        store.Save(settings);
    }

    void CancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();
    async void AboutClick(object sender, RoutedEventArgs e)
    {
        var version = typeof(App).Assembly.GetName().Version!.ToString(3);
        await new ContentDialog { XamlRoot = Root.XamlRoot, RequestedTheme = Root.ActualTheme,
            Title = $"关于 MetaLens v{version}", Content = "EXIF 全媒体元数据查看器 · Windows x64\nWinUI 3 · Windows App SDK\n本地只读解析，不注册系统默认打开方式。", CloseButtonText = "关闭" }.ShowAsync();
    }
    async void OnReady(object sender, RoutedEventArgs e)
    {
        if (ready)
            return;
        ready = true;
        InitializeWorkspace();
        recent = (await Task.Run(history.Load)).ToList();
        if (closing)
            return;
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        bool diagnostic = args.Length == 2 && args[0] == "--metalens-self-test" && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("METALENS_PROFILE_DIR"));
        var file = diagnostic ? args[1] : args.FirstOrDefault();
        if (file != null)
            Request(file);
        else
            OpenInitialDirectory();
        if (diagnostic)
            StartDiagnostics();
    }

    partial void InitializeWorkspace();
    partial void OpenInitialDirectory();
    partial void StartDiagnostics();
    void Request(string path) => PathRequested?.Invoke(path);
    void Notice(string s)
    {
        if (!closing)
            Status.Text = s;
    }

    void Track(string p)
    {
        recent.RemoveAll(x => StringComparer.OrdinalIgnoreCase.Equals(x, p));
        recent.Insert(0, p);
        if (recent.Count > 10)
            recent.RemoveRange(10, recent.Count - 10);
        try
        {
            history.Save(recent);
        }
        catch (IOException ex)
        {
            Notice(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Notice(ex.Message);
        }
    }

    void ShowRecent(object sender, RoutedEventArgs e)
    {
        var f = new MenuFlyout();
        foreach (var p in recent)
        {
            var m = new MenuFlyoutItem
            {
                Text = p
            };
            m.Click += (_, _) => Request(p);
            f.Items.Add(m);
        }

        if (f.Items.Count == 0)
            f.Items.Add(new MenuFlyoutItem { Text = "尚无最近文件", IsEnabled = false });
        f.ShowAt(RecentButton);
    }

    T InitPicker<T>(T picker)
        where T : class
    {
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        return picker;
    }

    async void PickMedia(object s, RoutedEventArgs e)
    {
        try
        {
            var p = InitPicker(new FileOpenPicker());
            p.FileTypeFilter.Add("*");
            if (await p.PickSingleFileAsync()is { } f)
                Request(f.Path);
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }

    async void PickFolder(object s, RoutedEventArgs e)
    {
        try
        {
            var p = InitPicker(new FolderPicker());
            p.FileTypeFilter.Add("*");
            if (await p.PickSingleFolderAsync()is { } f)
                Request(f.Path);
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }

    void DragOverRoot(object s, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None;
    }

    async void DropRoot(object s, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems) && (await e.DataView.GetStorageItemsAsync()).FirstOrDefault()is { } f)
                Request(f.Path);
        }
        catch (Exception ex)
        {
            Notice(ex.Message);
        }
    }
}
