using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExifMultimediaMetadataViewer;
using ExifMultimediaMetadataViewer.Controls;
using ExifMultimediaMetadataViewer.Services;
using ExifMultimediaMetadataViewer.ViewModels;

internal static class Program
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static T Part<T>(MainWindow w,string name) where T:class => (T)w.FindName(name);
    static IEnumerable<T> Kids<T>(DependencyObject node) where T:DependencyObject {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) { var child=VisualTreeHelper.GetChild(node,i); if(child is T t)yield return t; foreach(var k in Kids<T>(child))yield return k; }
    }
    static async Task Settle(MainWindow w) { w.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); w.UpdateLayout(); }
    static void Capture(FrameworkElement root,string file) {
        var bmp=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen()) { var bounds=new Rect(root.RenderSize);dc.DrawRectangle((Brush)Application.Current.FindResource("WindowBrush"),null,bounds);dc.DrawRectangle(new VisualBrush(root){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect((Point)VisualTreeHelper.GetOffset(root),root.RenderSize),Stretch=Stretch.Fill},null,bounds); }bmp.Render(drawing);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(file);encoder.Save(stream);
    }
    static void Sample(string file,int index) {
        var visual=new DrawingVisual();using(var d=visual.RenderOpen()) {
            d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(54,(byte)(84+index*7),145),Color.FromRgb(245,194,133),90),null,new Rect(0,0,960,600));
            d.DrawEllipse(Brushes.Bisque,null,new Point(670-index*22,190),52,52);
            for(int n=0;n<3;n++){var shape=new StreamGeometry();using(var c=shape.Open()){c.BeginFigure(new Point(0,600),true,true);for(int x=0;x<=960;x+=30)c.LineTo(new Point(x,315+n*76+Math.Sin(x*.007+n+index*.17)*63+Math.Cos(x*.024+n)*18),true,false);c.LineTo(new Point(960,600),true,false);} d.DrawGeometry(new SolidColorBrush(Color.FromRgb((byte)(87-n*20),(byte)(117-n*23),(byte)(143-n*27))),null,shape);}
        }
        var bmp=new RenderTargetBitmap(960,600,96,96,PixelFormats.Pbgra32);bmp.Render(visual);BitmapEncoder enc=file.EndsWith(".png")?new PngBitmapEncoder():new JpegBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(file);enc.Save(stream);
    }
    [STAThread] static int Main(string[] args) {
        if(Environment.GetEnvironmentVariable("METALENS_ARGV_RECEIPT") is string receipt){File.WriteAllLines(receipt,args);return 0;}
        var root=Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile-"+Guid.NewGuid().ToString("N"));Environment.SetEnvironmentVariable("METALENS_PROFILE_DIR",profile);
        var app=new Application();app.Resources=new ResourceDictionary { Source=new Uri("/"+typeof(MainWindow).Assembly.GetName().Name+";component/ViewerResources.xaml",UriKind.Relative) };app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        int code=1;app.Startup+=async (_,_)=>{try{await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);await Test(root,profile,args.Length>1?Path.GetFullPath(args[1]):null,args.Length>2?Path.GetFullPath(args[2]):null);code=0;}catch(Exception ex){Console.Error.WriteLine(ex);}finally{app.Shutdown(code);}};
        return app.Run();
    }
    static async Task Test(string root,string profile,string? largeDir,string? samples) {
        var fixtures=Path.Combine(root,"样片");Directory.CreateDirectory(Path.Combine(fixtures,"Camera Roll"));
        var jpeg=Path.Combine(fixtures,"山色 01 & 日落.jpg");Sample(jpeg,0);Sample(Path.Combine(fixtures,"天空.png"),1);
        for(int i=2;i<8;i++)Sample(Path.Combine(fixtures,$"山色 {i:00} 长文件名 山脊上的日落与薄雾.jpg"),i);
        File.WriteAllText(Path.Combine(fixtures,"说明.txt"),"fixture");
        var w=new MainWindow {Width=1450,Height=900};w.Show();await Settle(w);
        var vm=(ViewerViewModel)w.DataContext;for(int i=0;i<100 && vm.BrowserEntries.Count==0;i++)await Task.Delay(30);
        await vm.OpenAsync([jpeg],jpeg);await Settle(w);Check(vm.Analysis!=null && vm.PreviewImage!=null,"media not ready");
        Console.WriteLine($"Window DPI {VisualTreeHelper.GetDpi(w).PixelsPerInchX}");
        var image=vm.PreviewImage;var analysis=vm.Analysis;var entry=vm.SelectedBrowserEntry;
        var picker=Part<ComboBox>(w,"LayoutPicker");var list=Part<ListBox>(w,"ThumbnailList");var viewport=Kids<ImageViewport>(w).Single();
        var sizes=new PanelSizes[3];
        static void Drag(GridSplitter bar,double amount){ bool col=bar.ResizeDirection==GridResizeDirection.Columns;bar.RaiseEvent(new DragStartedEventArgs(0,0){RoutedEvent=Thumb.DragStartedEvent});bar.RaiseEvent(new DragDeltaEventArgs(col?amount:0,col?0:amount){RoutedEvent=Thumb.DragDeltaEvent});bar.UpdateLayout();bar.RaiseEvent(new DragCompletedEventArgs(col?amount:0,col?0:amount,false){RoutedEvent=Thumb.DragCompletedEvent}); }
        for(int mode=0;mode<3;mode++) {
            picker.SelectedIndex=mode;await Settle(w);viewport.FitToViewport();await Settle(w);Capture((FrameworkElement)w.Content,Path.Combine(root,$"layout-{mode+1}.png"));
            foreach(var name in new[]{"BrowserSplitter","MediaSplitter","TreeSplitter"}) {
                var pre=vm.Layout.Active; Drag(Part<GridSplitter>(w,name),15);await Settle(w);
                Check(pre!=vm.Layout.Active,$"{mode} {name}: splitter did not change");Console.WriteLine($"split {mode} {name}: {pre} => {vm.Layout.Active}");
            }
            sizes[mode]=vm.Layout.Active;
            foreach(double width in new double[]{1040,1280,1480})foreach(double thumb in new double[]{96,150,260}){
                w.Width=width;vm.ThumbnailSize=thumb;await Settle(w);var wrap=Kids<VirtualizingWrapPanel>(list).Single();var cards=Kids<ListBoxItem>(wrap).ToArray();Check(cards.Length>0,"no cards");
                foreach(var card in cards){ var rect=card.TransformToAncestor(wrap).TransformBounds(new Rect(card.RenderSize));Check(rect.Left>=-1 && rect.Right<=wrap.ActualWidth+1,$"card overflow {mode}/{width}/{thumb}: {rect} vs {wrap.RenderSize}"); }
                if(wrap.ActualWidth>=2*thumb)Check(cards.Select(c=>c.TranslatePoint(new Point(),wrap).X).Distinct().Count()>1,"single column across wide panel");
            }
            w.Width=1450;vm.ThumbnailSize=150;await Settle(w);
        }
        viewport.SetActualPixelScale(2);double zoom=vm.Zoom;for(int i=0;i<30;i++){picker.SelectedIndex=i%3;await Settle(w);Check(sizes[i%3]==vm.Layout.Active && ReferenceEquals(vm.SelectedBrowserEntry,entry) && ReferenceEquals(image,vm.PreviewImage) && ReferenceEquals(analysis,vm.Analysis) && vm.Zoom==zoom,"layout state/media/zoom lost");}
        var toggle=typeof(MainWindow).GetMethod("ToggleFullScreen",BindingFlags.NonPublic|BindingFlags.Instance)!;toggle.Invoke(w,null);await Settle(w);toggle.Invoke(w,null);await Settle(w);Check(vm.Zoom==zoom,"fullscreen lost zoom");
        var expected=vm.Layout;w.Close();await Settle(w);w=new MainWindow();w.Show();await Settle(w);vm=(ViewerViewModel)w.DataContext;Check(vm.Layout==expected,"restart lost layout");
        picker=Part<ComboBox>(w,"LayoutPicker");picker.SelectedIndex=0;await Settle(w);vm.ThumbnailSize=150;
        var sparse=Path.Combine(root,"单项目");Directory.CreateDirectory(Path.Combine(sparse,"Camera Roll"));await vm.NavigateDirectoryAsync(sparse);await Settle(w);
        Capture(Part<FrameworkElement>(w,"BrowserBody"),Path.Combine(root,"single-card-after.png"));
        if(largeDir!=null){await vm.NavigateDirectoryAsync(largeDir);await Settle(w);var largeList=Part<ListBox>(w,"ThumbnailList");var wrap=Kids<VirtualizingWrapPanel>(largeList).Single();
            int count=vm.BrowserEntries.Count;Check(count>=1000,"large directory missing");for(int i=0;i<=12;i++){wrap.SetVerticalOffset(i*(wrap.ExtentHeight-wrap.ViewportHeight)/12);await Settle(w);int n=Kids<ListBoxItem>(wrap).Count();Check(n>0 && n<100,"virtualization lost");Console.WriteLine($"scroll {i}: {n}/{count}");}
            vm.BrowserQuery="IMG_1000";await Settle(w);Check(vm.VisibleEntries.Cast<BrowserEntry>().Any() && vm.VisibleEntries.Cast<BrowserEntry>().Count()<count,"search failed");vm.BrowserQuery="";
        }
        var inspect=typeof(MainWindow).GetMethod("FileMenu",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(string ext in new[]{".jpg",".png",".ARW",".mp4",".txt",""}){
            var menu=(ContextMenu)inspect.Invoke(w,[new BrowserEntry("sample"+ext,ext=="",0,DateTime.Now)])!;
            bool has=menu.Items.OfType<MenuItem>().Any(i=>Equals(i.Header,"打开方式"));Check(has==(ext!=".txt"&&ext!=""),"wrong menu: "+ext);
        }
        if(samples!=null) {
            var paths=new[]{Path.Combine(samples,"sample.jpg"),Path.Combine(samples,"sample.ARW"),Path.Combine(samples,"sample.mp4"),Path.Combine(fixtures,"天空.png"),jpeg};
            foreach(var media in paths.Concat(paths.Reverse())) {
                var timer=Stopwatch.StartNew();await vm.OpenAsync([media],media);await Settle(w);
                Check(vm.Analysis!=null && vm.Fields.Count>0 && vm.PreviewImage!=null,"no preview/fields: "+Path.GetExtension(media));
                Console.WriteLine($"Media {Path.GetExtension(media)}: {vm.PreviewImage!.PixelWidth}x{vm.PreviewImage.PixelHeight}, {vm.Fields.Count} fields, {timer.ElapsedMilliseconds} ms");
            }
        }
        var receipt=Path.Combine(root,"argv.txt");var appHost=Path.Combine(AppContext.BaseDirectory,"外部 程序.exe");File.Copy(Path.Combine(AppContext.BaseDirectory,"MetaLens.LayoutTests.exe"),appHost,true);
        Environment.SetEnvironmentVariable("METALENS_ARGV_RECEIPT",receipt);try{File.Delete(receipt);ExternalOpenService.Open(appHost,jpeg);
            for(int i=0;i<100&&!File.Exists(receipt);i++)await Task.Delay(40);Check(File.ReadAllLines(receipt).SequenceEqual(new[]{jpeg}),"one-shot argv mismatch");
        }finally{Environment.SetEnvironmentVariable("METALENS_ARGV_RECEIPT",null);}
        foreach(var pair in new[]{(appHost,jpeg+".missing"),(appHost+".missing",jpeg)}){try{ExternalOpenService.Open(pair.Item1,pair.Item2);throw new Exception("missing path accepted");}catch(FileNotFoundException){}}
        w.Close(); File.WriteAllText(Path.Combine(profile,"viewer-settings.json"),"broken"); using(var store=new ViewerSettingsStore())Check(store.Load().Layout==null,"bad settings not recovered");
        Console.WriteLine("PASS: 9 splitter drags, 27 size combinations, 30 live layout switches, full screen, restart, scroll, file menus and Unicode argv.");
    }
}
