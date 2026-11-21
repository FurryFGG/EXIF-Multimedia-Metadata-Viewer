using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.System;
using ExifMetadata.Core.Services;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    readonly PreviewView viewer = new();
    bool fullscreen;
    partial void InitializePreview()
    {
        previewArea.Children.Add(viewer);
        viewer.Step += StepMedia;
        viewer.Full += ToggleFull;
        CancelRequested += viewer.Cancel;
        Closed += (_, _) => viewer.Clear();
        Root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(PreviewKey), true);
    }

    partial void MediaChosen(string path) => _ = viewer.Show(path, lifetime.Token);
    void StepMedia(int step)
    {
        var list = browser.Model.Items.Where(x => !x.IsDirectory && MediaAnalyzer.IsKnownMediaFile(x.FullPath)).ToArray();
        var i = Array.FindIndex(list, x => StringComparer.OrdinalIgnoreCase.Equals(x.FullPath, selected));
        if (i + step >= 0 && i + step < list.Length)
            Request(list[i + step].FullPath);
    }

    void PreviewKey(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F11 || (fullscreen && e.Key == VirtualKey.Escape))
        {
            ToggleFull();
            e.Handled = true;
            return;
        }

        for (var d = e.OriginalSource as DependencyObject; d != null; d = VisualTreeHelper.GetParent(d))
            if (d is TextBox or ComboBox or TreeView or Slider)
                return;
        if (e.Key is VirtualKey.Left or VirtualKey.Up or VirtualKey.Right or VirtualKey.Down)
        {
            StepMedia(e.Key is VirtualKey.Left or VirtualKey.Up ? -1 : 1);
            e.Handled = true;
        }
    }

    void ToggleFull()
    {
        if (!fullscreen)
        {
            previewArea.Children.Remove(viewer);
            Root.Children.Add(viewer);
            Grid.SetRowSpan(viewer, 3);
            Canvas.SetZIndex(viewer, 8);
            Workspace.Visibility = Commands.Visibility = Status.Visibility = Visibility.Collapsed;
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            Root.Children.Remove(viewer);
            Grid.SetRowSpan(viewer, 1);
            previewArea.Children.Add(viewer);
            Workspace.Visibility = Commands.Visibility = Status.Visibility = Visibility.Visible;
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        }

        fullscreen = !fullscreen;
    }
}
