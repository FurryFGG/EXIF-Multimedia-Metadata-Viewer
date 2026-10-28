using ExifMultimediaMetadataViewer.ViewModels;

namespace ExifMultimediaMetadataViewer.Services;

public sealed class FileBrowserService
{
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
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase);
            var files = entries.Where(entry => !entry.IsDirectory);
            IOrderedEnumerable<BrowserEntry> sortedFiles = sortMode switch
            {
                "类型" => files.OrderBy(entry => entry.Extension, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                "修改时间" => files.OrderByDescending(entry => entry.ModifiedTime)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                "大小" => files.OrderByDescending(entry => entry.Size)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => files.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            };
            return folders.Concat(sortedFiles).ToArray();
        }, cancellationToken);
}
