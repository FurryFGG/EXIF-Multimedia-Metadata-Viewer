using ExifMultimediaMetadataViewer.ViewModels;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ExifMultimediaMetadataViewer.Services;

public sealed class FileBrowserService
{
    private const int EnumerationBatchSize = 64;
    private static readonly NaturalStringComparer NaturalComparer = new();

    public Task<IReadOnlyList<BrowserEntry>> EnumerateAsync(
        string directory,
        string sortMode,
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<BrowserEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(directory);
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"找不到文件夹：{path}");

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };
            var entries = new List<BrowserEntry>();
            foreach (var item in Directory.EnumerateFileSystemEntries(path, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var attributes = File.GetAttributes(item);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        entries.Add(new BrowserEntry(item, true, 0, Directory.GetLastWriteTime(item)));
                    }
                    else
                    {
                        var info = new FileInfo(item);
                        entries.Add(new BrowserEntry(info.FullName, false, info.Length, info.LastWriteTime));
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                }
            }

            var folders = entries.Where(entry => entry.IsDirectory)
                .OrderBy(entry => entry.Name, NaturalComparer);
            var files = entries.Where(entry => !entry.IsDirectory);
            IOrderedEnumerable<BrowserEntry> sortedFiles = sortMode switch
            {
                "类型" => files.OrderBy(entry => entry.Extension, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(entry => entry.Name, NaturalComparer),
                "修改时间" => files.OrderByDescending(entry => entry.ModifiedTime)
                    .ThenBy(entry => entry.Name, NaturalComparer),
                "大小" => files.OrderByDescending(entry => entry.Size)
                    .ThenBy(entry => entry.Name, NaturalComparer),
                _ => files.OrderBy(entry => entry.Name, NaturalComparer)
            };
            return folders.Concat(sortedFiles).ToArray();
        }, cancellationToken);

    public Task<IReadOnlyList<DirectoryTreeNode>> EnumerateDirectoriesAsync(
        string directory,
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<DirectoryTreeNode>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.System
            };
            var nodes = new List<DirectoryTreeNode>();
            foreach (var path in Directory.EnumerateDirectories(directory, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                    nodes.Add(new DirectoryTreeNode(path, string.IsNullOrWhiteSpace(name) ? path : name));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                }
            }
            return nodes.OrderBy(node => node.DisplayName, NaturalComparer).ToArray();
        }, cancellationToken);

    public async IAsyncEnumerable<IReadOnlyList<BrowserEntry>> EnumerateBatchesAsync(
        string directory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<IReadOnlyList<BrowserEntry>>(new BoundedChannelOptions(2)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        _ = Task.Run(async () =>
        {
            try
            {
                var path = Path.GetFullPath(directory);
                if (!Directory.Exists(path))
                    throw new DirectoryNotFoundException($"找不到文件夹：{path}");
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false
                };
                var batch = new List<BrowserEntry>(EnumerationBatchSize);
                foreach (var item in Directory.EnumerateFileSystemEntries(path, "*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var attributes = File.GetAttributes(item);
                        if ((attributes & FileAttributes.Directory) != 0)
                            batch.Add(new BrowserEntry(item, true, 0, Directory.GetLastWriteTime(item)));
                        else
                        {
                            var info = new FileInfo(item);
                            batch.Add(new BrowserEntry(info.FullName, false, info.Length, info.LastWriteTime));
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                    }
                    if (batch.Count < EnumerationBatchSize)
                        continue;
                    await channel.Writer.WriteAsync(batch.ToArray(), cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
                if (batch.Count > 0)
                    await channel.Writer.WriteAsync(batch.ToArray(), cancellationToken).ConfigureAwait(false);
                channel.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
            }
        }, CancellationToken.None);

        await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return batch;
    }

    public IReadOnlyList<BrowserEntry> SortEntries(IEnumerable<BrowserEntry> source, string sortMode)
    {
        var entries = source.ToArray();
        var folders = entries.Where(entry => entry.IsDirectory).OrderBy(entry => entry.Name, NaturalComparer);
        var files = entries.Where(entry => !entry.IsDirectory);
        IOrderedEnumerable<BrowserEntry> sortedFiles = sortMode switch
        {
            "类型" => files.OrderBy(entry => entry.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(entry => entry.Name, NaturalComparer),
            "修改时间" => files.OrderByDescending(entry => entry.ModifiedTime).ThenBy(entry => entry.Name, NaturalComparer),
            "大小" => files.OrderByDescending(entry => entry.Size).ThenBy(entry => entry.Name, NaturalComparer),
            _ => files.OrderBy(entry => entry.Name, NaturalComparer)
        };
        return folders.Concat(sortedFiles).ToArray();
    }

    public IReadOnlyList<DirectoryTreeNode> GetDriveRoots()
    {
        var nodes = new List<DirectoryTreeNode>();
        // Do not call IsReady or VolumeLabel here. A disconnected drive can block
        // either property for tens of seconds on the UI startup path.
        foreach (var drive in DriveInfo.GetDrives().OrderBy(item => item.Name, NaturalComparer))
            nodes.Add(new DirectoryTreeNode(drive.Name, drive.Name));
        return nodes;
    }

    private sealed class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            x ??= "";
            y ??= "";
            var xi = 0;
            var yi = 0;
            while (xi < x.Length && yi < y.Length)
            {
                if (char.IsDigit(x[xi]) && char.IsDigit(y[yi]))
                {
                    var xStart = xi;
                    var yStart = yi;
                    while (xi < x.Length && char.IsDigit(x[xi])) xi++;
                    while (yi < y.Length && char.IsDigit(y[yi])) yi++;
                    var xNumber = x[xStart..xi].TrimStart('0');
                    var yNumber = y[yStart..yi].TrimStart('0');
                    if (xNumber.Length != yNumber.Length)
                        return xNumber.Length.CompareTo(yNumber.Length);
                    var numeric = string.Compare(xNumber, yNumber, StringComparison.Ordinal);
                    if (numeric != 0)
                        return numeric;
                    var digitCount = (xi - xStart).CompareTo(yi - yStart);
                    if (digitCount != 0)
                        return digitCount;
                    continue;
                }

                var comparison = char.ToUpper(x[xi], System.Globalization.CultureInfo.CurrentCulture)
                    .CompareTo(char.ToUpper(y[yi], System.Globalization.CultureInfo.CurrentCulture));
                if (comparison != 0)
                    return comparison;
                xi++;
                yi++;
            }
            return (x.Length - xi).CompareTo(y.Length - yi);
        }
    }
}
