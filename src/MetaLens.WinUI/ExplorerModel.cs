using ExifMultimediaMetadataViewer.Services;
using ExifMultimediaMetadataViewer.ViewModels;

namespace MetaLens.WinUI;
internal sealed class ExplorerModel
{
    internal readonly FileBrowserService Service = new();
    internal readonly BulkObservableCollection<BrowserEntry> Items = new();
    internal readonly List<BrowserEntry> Entries = [];
    readonly List<string> trail = [];
    int position = -1;
    CancellationTokenSource? scan;
    int revision;
    internal string Folder { get; private set; } = "";

    internal string Query = "", Sort = "名称";
    internal bool Descending, FoldersFirst = true;
    readonly BoundedLruCache<(string,long,DateTime),DateTime> taken = new(512);
    static readonly FileBrowserService.NaturalStringComparer Natural = new();
    internal void RememberTaken(ExifMetadata.Core.Models.MediaAnalysis data)
    {
        var entry = Entries.FirstOrDefault(e => StringComparer.OrdinalIgnoreCase.Equals(e.FullPath,data.FilePath));
        if (entry is null) return;
        var value = data.AllFields.FirstOrDefault(f => f.Source == "ExifTool" && f.TagName == "DateTimeOriginal")?.FormattedValue;
        if (value is {Length: >=19} && DateTime.TryParseExact(value[..19],"yyyy:MM:dd HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date))
        { entry.TakenTime=date; taken.Set((entry.FullPath,entry.Size,entry.ModifiedTime),date); if(Sort=="拍摄时间") RefreshView(); }
    }
    internal int ScanCount { get; private set; }
    internal bool Loading { get; private set; }

    internal void Cancel() => scan?.Cancel();
    internal async Task<bool> Navigate(string folder, bool remember, CancellationToken stop)
    {
        Cancel();
        using var job = CancellationTokenSource.CreateLinkedTokenSource(stop);
        scan = job;
        var token = job.Token;
        var ver = ++revision;
        Loading = true;
        try
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException(folder);
            if (remember && (position < 0 || !StringComparer.OrdinalIgnoreCase.Equals(trail[position], folder)))
            {
                trail.RemoveRange(position + 1, trail.Count - position - 1);
                trail.Add(folder);
                position = trail.Count - 1;
            }

            Folder = folder;
            ScanCount++;
            Entries.Clear();
            Items.Clear();
            await foreach (var batch in Service.EnumerateBatchesAsync(folder, token))
            {
                token.ThrowIfCancellationRequested();
                Entries.AddRange(batch);
                foreach (var e in batch) if (taken.TryGet((e.FullPath,e.Size,e.ModifiedTime),out var date)) e.TakenTime=date;
                Items.AddRange(batch.Where(Matches));
            }

            token.ThrowIfCancellationRequested();
            RefreshView();
            return ver == revision;
        }
        finally
        {
            if (ver == revision)
            {
                scan = null;
                Loading = false;
            }
        }
    }

    internal string? Step(int by)
    {
        var n = position + by;
        if (n < 0 || n >= trail.Count)
            return null;
        position = n;
        return trail[n];
    }

    bool Matches(BrowserEntry e) => e.Name.Contains(Query, StringComparison.CurrentCultureIgnoreCase);
    internal void RefreshView()
    {
        var rows=Entries.Where(Matches).ToList();
        rows.Sort((a,b) => {
            if(FoldersFirst && a.IsDirectory!=b.IsDirectory) return a.IsDirectory ? -1 : 1;
            if(Sort=="拍摄时间" && a.TakenTime.HasValue!=b.TakenTime.HasValue) return a.TakenTime.HasValue ? -1 : 1;
            int c=Sort switch { "创建时间"=>a.CreatedTime.CompareTo(b.CreatedTime), "修改时间"=>a.ModifiedTime.CompareTo(b.ModifiedTime), "拍摄时间"=>Nullable.Compare(a.TakenTime,b.TakenTime), "大小"=>a.Size.CompareTo(b.Size), "类型"=>StringComparer.OrdinalIgnoreCase.Compare(a.IsDirectory?"":a.Extension,b.IsDirectory?"":b.Extension), _=>Natural.Compare(a.Name,b.Name) };
            if(c!=0) return Descending ? -Math.Sign(c) : Math.Sign(c);
            c=Natural.Compare(a.Name,b.Name); return c!=0 ? c : StringComparer.Ordinal.Compare(a.FullPath,b.FullPath);
        });
        Items.ReplaceAll(rows);
    }
}
