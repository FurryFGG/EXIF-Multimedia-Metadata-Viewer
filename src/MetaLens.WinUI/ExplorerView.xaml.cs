using ExifMultimediaMetadataViewer.ViewModels;
using ExifMultimediaMetadataViewer.Services;
using ExifMetadata.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
namespace MetaLens.WinUI;
public sealed partial class ExplorerView:UserControl
{
 internal readonly ExplorerModel Model=new();internal event Action<string>? Open,Message;internal event Action? OptionsChanged;internal event Action<FrameworkElement,BrowserEntry>? Context;
 internal string View="缩略图";internal double TileSize=150;internal CancellationToken Stop;readonly Dictionary<Image,CancellationTokenSource> tileJobs=[]; readonly HashSet<TreeViewNode> expanding=[]; readonly SemaphoreSlim treeWorkers=new(2);bool init;
 readonly Grid detailHeader=new(){Padding=new Thickness(12,4,12,4),ColumnSpacing=4};
 public ExplorerView(){InitializeComponent();
  var style=new Style(typeof(ListViewItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));Rows.ItemContainerStyle=style;
  foreach(var (label,weight) in new[]{("名称",3),("类型",1),("大小",1),("修改时间",2),("预览状态",2)}){
   var title=new TextBlock{Text=label,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
   Grid.SetColumn(title,detailHeader.ColumnDefinitions.Count);detailHeader.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(weight,GridUnitType.Star)});detailHeader.Children.Add(title);
  }
 }
 internal void Setup(ViewerSettings s,CancellationToken stop){Stop=stop;Model.Sort=s.BrowserSortMode; View=s.BrowserViewMode;TileSize=s.ThumbnailSize;Tiles.ItemsSource=Rows.ItemsSource=Model.Items;SetView(View);foreach(var n in Model.Service.GetDriveRoots())Tree.RootNodes.Add(Node(n));
 void Cmd(string title,Action a){var m=new MenuFlyoutItem{Text=title};m.Click+=(_,_)=>a();NavMenu.Items.Add(m);} Cmd("返回",()=>Go(Model.Step(-1),false));Cmd("前进",()=>Go(Model.Step(1),false));Cmd("上级",()=>Go(Directory.GetParent(Model.Folder)?.FullName));Cmd("刷新",()=>Go(Model.Folder,false));NavMenu.Items.Add(new MenuFlyoutSeparator());foreach(var v in new[]{"缩略图","列表","详细信息"})Cmd("视图 · "+v,()=>SetView(v));foreach(var v in new[]{"名称","类型","修改时间","大小"})Cmd("排序 · "+v,()=>{Model.Sort=v;Model.RefreshView();OptionsChanged?.Invoke();});Cmd("增大缩略图",()=>ResizeTiles(20));Cmd("缩小缩略图",()=>ResizeTiles(-20));init=true;}
 void ResizeTiles(int delta){TileSize=Math.Clamp(TileSize+delta,96,260);FitTiles();OptionsChanged?.Invoke();}
 internal void FitTiles(){if(Tiles.ItemsPanelRoot is ItemsWrapGrid p){var w=Math.Max(1,Tiles.ActualWidth-18);var cols=Math.Max(1,(int)(w/TileSize));p.ItemWidth=Math.Max(1,Math.Floor(w/cols));p.ItemHeight=Math.Max(88,Math.Min(170,p.ItemWidth*.65)+58);}}
 void TileSizeChanged(object s,SizeChangedEventArgs e)=>FitTiles();internal void SetView(string v){View=v;Tiles.Visibility=v=="缩略图"?Visibility.Visible:Visibility.Collapsed;Rows.Visibility=v!="缩略图"?Visibility.Visible:Visibility.Collapsed;Rows.Header=v=="详细信息"?detailHeader:null;Rows.ItemTemplate=(DataTemplate)Resources[v=="详细信息"?"Detail":"Compact"];if(init)OptionsChanged?.Invoke();}
 internal async Task<bool> Navigate(string folder,bool remember=true){try{Address.Text=folder;return await Model.Navigate(folder,remember,Stop);}catch(OperationCanceledException){return false;}catch(Exception ex){Message?.Invoke(ex.Message);return false;}}
 async void Go(string? dir,bool remember=true){if(!string.IsNullOrEmpty(dir))await Navigate(dir,remember);}
 TreeViewNode Node(DirectoryTreeNode n)=>new(){Content=n.DisplayName,HasUnrealizedChildren=true, /* path kept separate from the visible name */ };
 string TreePath(TreeViewNode n)=>n.Parent is {Content:string} p?Path.Combine(TreePath(p),n.Content.ToString()!):n.Content.ToString()!;
 async void ExpandTree(TreeView s,TreeViewExpandingEventArgs e){var n=e.Node;if(!n.HasUnrealizedChildren||!expanding.Add(n))return;try{await treeWorkers.WaitAsync(Stop);try{var nodes=await Model.Service.EnumerateDirectoriesAsync(TreePath(n),Stop);Stop.ThrowIfCancellationRequested();foreach(var child in nodes)n.Children.Add(Node(child));n.HasUnrealizedChildren=false;}finally{treeWorkers.Release();}}catch(OperationCanceledException){}catch(Exception ex){Message?.Invoke(ex.Message);}finally{expanding.Remove(n);}}
 void TreeInvoked(TreeView s,TreeViewItemInvokedEventArgs e){if(e.InvokedItem is TreeViewNode n)Go(TreePath(n));}
 void AddressKey(object s,KeyRoutedEventArgs e){if(e.Key==VirtualKey.Enter){Open?.Invoke(Address.Text);e.Handled=true;}}
 void FilterChanged(object s,TextChangedEventArgs e){if(init){Model.Query=Filter.Text;Model.RefreshView();}}
 void ItemClicked(object s,ItemClickEventArgs e){if(e.ClickedItem is BrowserEntry b)Open?.Invoke(b.FullPath);}
 void ContextItem(object s,RightTappedRoutedEventArgs e){if(s is FrameworkElement f && f.DataContext is BrowserEntry b){e.Handled=true;Context?.Invoke(f,b);}}
 void TileLoaded(object s,RoutedEventArgs e)=>Load((Image)s);void TileChanged(FrameworkElement s,DataContextChangedEventArgs e){if(s.IsLoaded)Load((Image)s);}void TileUnloaded(object s,RoutedEventArgs e)=>Release((Image)s);
 void Release(Image img){if(tileJobs.Remove(img,out var old))old.Cancel();img.Source=null;}
 async void Load(Image img){
  Release(img);
  if(img.DataContext is not BrowserEntry b||b.IsDirectory||!MediaAnalyzer.IsKnownMediaFile(b.FullPath)||Stop.IsCancellationRequested)return;
  using var job=CancellationTokenSource.CreateLinkedTokenSource(Stop);tileJobs[img]=job;
  try{
   var bytes=await Task.Run(()=>MediaImages.Thumb(b.FullPath,job.Token),job.Token);job.Token.ThrowIfCancellationRequested();
   using var data=new MemoryStream(bytes);var image=new BitmapImage();await image.SetSourceAsync(data.AsRandomAccessStream()).AsTask(job.Token);
   job.Token.ThrowIfCancellationRequested();img.Source=image;b.ReportPreview("缩略图已加载");
  }catch(OperationCanceledException){}catch(Exception){if(!job.IsCancellationRequested)b.ReportPreview("不支持预览或读取失败");}
  finally{if(tileJobs.TryGetValue(img,out var active)&&active==job)tileJobs.Remove(img);}
 }
 internal void SelectPath(string file){var b=Model.Items.FirstOrDefault(e=>StringComparer.OrdinalIgnoreCase.Equals(e.FullPath,file));if(b!=null){var list=View=="缩略图"?(ListViewBase)Tiles:Rows;list.SelectedItem=b;list.ScrollIntoView(b);}}
 internal Grid TreeLayout=>Panels; internal TreeView FolderTree=>Tree;internal FrameworkElement Handle=>TreeGrip;internal Grid FilePane=>Files;
}
