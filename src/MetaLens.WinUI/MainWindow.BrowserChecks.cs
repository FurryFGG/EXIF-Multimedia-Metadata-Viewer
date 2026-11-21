using ExifMultimediaMetadataViewer.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace MetaLens.WinUI;
public sealed partial class MainWindow
{
    async Task CheckBrowserChanges(string output, string jpg, List<object> steps)
    {
        var root=Directory.CreateDirectory(Path.Combine(output,"covers-"+Guid.NewGuid().ToString("N"))).FullName;
        var cover=Directory.CreateDirectory(Path.Combine(root,"相册")).FullName;
        var nested=Directory.CreateDirectory(Path.Combine(root,"空目录","下一级")).FullName;
        File.Copy(jpg,Path.Combine(nested,"not-direct.jpg"));
        File.Copy(jpg,Path.Combine(cover,"one.jpg"));
        var token=lifetime.Token;
        var one=await MediaImages.Folder(cover,token) ?? throw new Exception("单图封面缺失");
        if(!ReferenceEquals(one,await MediaImages.Folder(cover,token)) || await MediaImages.Folder(Path.GetDirectoryName(nested)!,token)!=null) throw new Exception("缓存或非递归失败");
        foreach(var n in new[]{2,3,4}) File.Copy(jpg,Path.Combine(cover,n+".jpg"));
        var four=await MediaImages.Folder(cover,token) ?? throw new Exception("拼图封面缺失");
        if(ReferenceEquals(four,one) || BitConverter.ToInt32(four,18)!=240 || Math.Abs(BitConverter.ToInt32(four,22))!=240) throw new Exception("拼图尺寸或失效失败");
        await browser.Navigate(root,false); browser.SetView("缩略图");
        await Until(()=>browser.Model.Items.Any(e=>e.FullPath==cover && e.PreviewStatus=="缩略图已加载"));
        var grid=(GridView)browser.FindName("Tiles");
        var panel=(ItemsWrapGrid)grid.ItemsPanelRoot;
        var size=AppWindow.Size; var first=(panel.ItemWidth,panel.ItemHeight); double viewport=Root.ActualWidth;
        AppWindow.Resize(new SizeInt32(size.Width-220,size.Height)); await Until(()=>Math.Abs(Root.ActualWidth-viewport)>15);
        if(first!=(panel.ItemWidth,panel.ItemHeight)) throw new Exception("窗口宽度更改了卡片尺寸");
        AppWindow.Resize(size);
        var model=new ExplorerModel(); var day=new DateTime(2020,1,1);
        BrowserEntry Row(string name,int index,int modified,int length,DateTime? taken) => new(Path.Combine(root,name),false,length,day.AddDays(modified),day.AddDays(index)){TakenTime=taken};
        model.Entries.AddRange([Row("IMG_10.png",1,2,3,null),Row("IMG_2.jpg",2,3,1,day),Row("IMG_1.tif",3,1,2,day.AddDays(1)),new(Path.Combine(root,"Z"),true,0,day)]);
        foreach(var (field,asc) in new[]{("名称","1,2,10"),("创建时间","10,2,1"),("修改时间","1,10,2"),("大小","2,1,10"),("类型","2,10,1"),("拍摄时间","2,1,10")})
        foreach(var desc in new[]{false,true}) {
            model.Sort=field; model.Descending=desc; model.RefreshView();
            var expected=asc.Split(','); if(desc) expected=field=="拍摄时间"?["1","2","10"]:expected.Reverse().ToArray();
            if(!model.Items[0].IsDirectory || !model.Items.Skip(1).Select(e=>Path.GetFileNameWithoutExtension(e.Name)[4..]).SequenceEqual(expected)) throw new Exception("排序失败："+field+desc);
        }
        model.FoldersFirst=false; model.Sort="名称";model.Descending=false;model.RefreshView();
        if(!model.Items.Last().IsDirectory) throw new Exception("文件夹优先不可关闭");
        await browser.Navigate(Path.GetDirectoryName(jpg)!,false);
        steps.Add(new {coverCacheHit=true,coverInvalidated=true,directOnly=true,renderedFolder=true,cardWidth=first.ItemWidth,cardHeight=first.ItemHeight,sortDirections=12});
    }
}
