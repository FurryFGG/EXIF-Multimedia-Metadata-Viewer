using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ExifMultimediaMetadataViewer.Services;

namespace ExifMultimediaMetadataViewer;

public partial class MainWindow
{
    private bool _arranging;
    private static GridLength Star(double value) => new(value, GridUnitType.Star);
    private void LayoutPicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutPicker.SelectedIndex is < 0 or > 2 || Workspace is null) return;
        var layout = _viewModel.Layout with { Mode = (LayoutMode)LayoutPicker.SelectedIndex };
        _viewModel.StoreLayout(layout);
        _arranging = true;
        try { _viewModel.IsBrowserExpanded = layout.Active.Expanded; }
        finally { _arranging = false; }
        ArrangeWorkspace();
    }
    private void BrowserVisibilityChanged()
    {
        if (_arranging) return;
        var layout = _viewModel.Layout;
        _viewModel.StoreLayout(layout.WithActive(layout.Active with { Expanded = _viewModel.IsBrowserExpanded }));
        ArrangeWorkspace();
    }
    private void LimitOriginalSizes()
    {
        if (_viewModel.Layout.Mode != LayoutMode.Original || Workspace.RowDefinitions.Count != 3) return;
        Workspace.RowDefinitions[0].MaxHeight = Math.Max(175, Workspace.ActualHeight - 212);
        FolderTreeColumn.MaxWidth = Math.Max(70, BrowserBody.ActualWidth - 130);
    }
    private void ArrangeWorkspace()
    {
        var layout = _viewModel.Layout;
        var p = layout.Active;
        Workspace.ColumnDefinitions.Clear(); Workspace.RowDefinitions.Clear();
        void Col(GridLength width, double min = 0) => Workspace.ColumnDefinitions.Add(new ColumnDefinition { Width=width, MinWidth=min });
        void Row(GridLength height, double min = 0) => Workspace.RowDefinitions.Add(new RowDefinition { Height=height, MinHeight=min });
        static void Put(UIElement element,int col,int row,int cols=1,int rows=1)
        { Grid.SetColumn(element,col); Grid.SetRow(element,row); Grid.SetColumnSpan(element,cols); Grid.SetRowSpan(element,rows); }
        static void Orient(GridSplitter s, bool vertical)
        {
            s.ResizeDirection = vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows;
            s.HorizontalAlignment = HorizontalAlignment.Stretch; s.VerticalAlignment = VerticalAlignment.Stretch;
            s.Cursor = vertical ? Cursors.SizeWE : Cursors.SizeNS;
        }
        bool show=p.Expanded;
        BrowserSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        GridLength gap = new(show ? 7 : 0), zero = new(0);
        FolderTreeColumn.MaxWidth = double.PositiveInfinity;
        FolderTreeColumn.Width = layout.Mode == LayoutMode.Original ? new GridLength(p.Tree) : Star(p.Tree);
        ContentColumn.Width = Star(layout.Mode == LayoutMode.Original ? 1 : 1-p.Tree);
        Orient(BrowserSplitter,layout.Mode != LayoutMode.Original); Orient(MediaSplitter,layout.Mode != LayoutMode.Stacked);
        switch(layout.Mode)
        {
            case LayoutMode.Original:
                Col(Star(p.Preview),290); Col(new(7)); Col(Star(1-p.Preview),260);
                Row(show ? new(p.Browser) : zero,show ? 175 : 0); Row(gap); Row(Star(1),205);
                Put(FileBrowserPanel,0,0,3); Put(BrowserSplitter,0,1,3);
                Put(PreviewPanel,0,2); Put(MediaSplitter,1,2); Put(MetadataPanel,2,2);
                LimitOriginalSizes(); break;
            case LayoutMode.Columns:
                Col(show ? Star(p.Browser) : zero,show ? 222 : 0); Col(gap);
                Col(Star((1-p.Browser)*p.Preview),300); Col(new(7)); Col(Star((1-p.Browser)*(1-p.Preview)),240); Row(Star(1));
                Put(FileBrowserPanel,0,0); Put(BrowserSplitter,1,0); Put(PreviewPanel,2,0); Put(MediaSplitter,3,0); Put(MetadataPanel,4,0); break;
            default:
                Col(show ? Star(p.Browser) : zero,show ? 222 : 0); Col(gap); Col(Star(1-p.Browser),320);
                Row(Star(p.Preview),170); Row(new(7)); Row(Star(1-p.Preview),150);
                Put(FileBrowserPanel,0,0,1,3); Put(BrowserSplitter,1,0,1,3); Put(PreviewPanel,2,0); Put(MediaSplitter,2,1); Put(MetadataPanel,2,2); break;
        }
    }
    private void WorkspaceSplit_End(object sender, DragCompletedEventArgs e)
    {
        var layout = _viewModel.Layout; var p = layout.Active;
        bool orig = layout.Mode == LayoutMode.Original, stacked = layout.Mode == LayoutMode.Stacked;
        if (sender == TreeSplitter)
            p = p with { Tree = orig ? FolderTreeColumn.ActualWidth : FolderTreeColumn.ActualWidth / Math.Max(1,FolderTreeColumn.ActualWidth+ContentColumn.ActualWidth) };
        else
        {
            double preview = stacked ? PreviewPanel.ActualHeight : PreviewPanel.ActualWidth;
            double meta = stacked ? MetadataPanel.ActualHeight : MetadataPanel.ActualWidth;
            p = p with { Preview = preview / Math.Max(1, preview + meta) };
            if (p.Expanded) p = p with { Browser = orig ? FileBrowserPanel.ActualHeight : FileBrowserPanel.ActualWidth / Math.Max(1, FileBrowserPanel.ActualWidth + (stacked ? PreviewPanel.ActualWidth : preview + meta)) };
        }
        _viewModel.StoreLayout(layout.WithActive(p.Checked(layout.Mode)));
    }
}
