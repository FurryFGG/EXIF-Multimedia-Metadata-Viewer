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
    internal void RefreshView() => Items.ReplaceAll(Service.SortEntries(Entries.Where(Matches), Sort));
}
