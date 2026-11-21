using Microsoft.UI.Xaml;
namespace MetaLens.WinUI;
public partial class App : Application {
    private Window? _main;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args) { _main = new MainWindow(); _main.Activate(); }
}
