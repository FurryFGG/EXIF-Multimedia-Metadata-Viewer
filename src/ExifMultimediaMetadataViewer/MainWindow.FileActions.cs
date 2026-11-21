using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ExifMetadata.Core.Services;
using ExifMultimediaMetadataViewer.Services;
using ExifMultimediaMetadataViewer.ViewModels;
using Microsoft.Win32;

namespace ExifMultimediaMetadataViewer;

public partial class MainWindow
{
    private string? _sessionApp;
    private static BrowserEntry? HitEntry(object sender, object? source) => sender is ItemsControl owner && source is DependencyObject d
        ? (ItemsControl.ContainerFromElement(owner,d) as FrameworkElement)?.DataContext as BrowserEntry : null;
    // Intercept before ListBoxItem/DataGridRow can select and start a different preview.
    private void FileRightDown(object sender, MouseButtonEventArgs e) { if (HitEntry(sender,e.OriginalSource) != null) e.Handled=true; }
    private void FileRightUp(object sender, MouseButtonEventArgs e)
    {
        if (HitEntry(sender,e.OriginalSource) is not { } entry) return;
        e.Handled=true;
        ShowFileMenu((FrameworkElement)sender,entry,true);
    }
    private void ShowFileMenu(FrameworkElement owner,BrowserEntry entry,bool pointer)
    {
        var menu = FileMenu(entry);
        menu.PlacementTarget=owner; menu.Placement=pointer ? PlacementMode.MousePoint : PlacementMode.Center; menu.IsOpen=true;
    }
    private ContextMenu FileMenu(BrowserEntry entry)
    {
        var menu = new ContextMenu();
        MenuItem Item(string text,Func<Task> action)
        {
            var item=new MenuItem { Header=text };
            item.Click += async (_,_) => { try { await action(); } catch(Exception e) when(e is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or System.Runtime.InteropServices.ExternalException or ArgumentException) { ShowError(e.Message); } };
            return item;
        }
        MenuItem Sync(string text,Action action) => Item(text,()=>{action();return Task.CompletedTask;});
        menu.Items.Add(Item(entry.IsDirectory ? "打开文件夹" : "打开",()=>_viewModel.OpenBrowserEntryAsync(entry)));
        if(!entry.IsDirectory && MediaAnalyzer.IsKnownMediaFile(entry.FullPath))
        {
            var openWith=new MenuItem { Header="打开方式" };
            if (_sessionApp is string last) openWith.Items.Add(Sync(Path.GetFileNameWithoutExtension(last),()=>ExternalOpenService.Open(last,entry.FullPath)));
            void Choose(bool remember)
            {
                var dialog=new OpenFileDialog { Title="选择打开此文件的应用（不更改系统默认值）", Filter="应用程序 (*.exe)|*.exe", CheckFileExists=true };
                if(dialog.ShowDialog(this)!=true) return;
                ExternalOpenService.Open(dialog.FileName,entry.FullPath);
                if(remember) _sessionApp=dialog.FileName;
            }
            openWith.Items.Add(Sync("选择其他应用…",()=>Choose(false)));
            openWith.Items.Add(Sync("选择并仅在本次会话记住…",()=>Choose(true)));
            menu.Items.Add(openWith);
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Sync("复制文件路径",()=>Clipboard.SetText(entry.FullPath)));
        menu.Items.Add(Sync("复制文件名",()=>Clipboard.SetText(entry.Name)));
        menu.Items.Add(Sync("打开所在文件夹",()=>{
            var dir=Path.GetDirectoryName(entry.FullPath);
            if(dir is null || !Directory.Exists(dir)) throw new DirectoryNotFoundException("所在目录不存在。");
            using var launched=Process.Start(new ProcessStartInfo(dir){UseShellExecute=true});
        }));
        menu.Items.Add(Item("刷新",()=>_viewModel.RefreshBrowserAsync()));
        return menu;
    }
}
