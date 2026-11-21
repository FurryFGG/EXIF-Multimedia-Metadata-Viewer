namespace ExifMetadata.Core.Services;

/// <summary>Build a bounded contact sheet from cached child thumbnails, never from a recursive media scan.</summary>
public static class FolderCover
{
    public sealed record Pixels(int Width, int Height, byte[] Bgra);
    private static readonly SemaphoreSlim Workers = new(2);
    private static readonly Dictionary<string, byte[]> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> Order = new();
    public const int Edge = 240;

    public static Task<byte[]?> ReadAsync(string folder, Func<string, CancellationToken, Task<Pixels?>> decode, CancellationToken token) =>
        Task.Run(async () =>
        {
            await Workers.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var dir = new DirectoryInfo(folder);
                var files = new List<FileInfo>(4);
                foreach (var file in dir.EnumerateFiles("*", new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint }))
                {
                    token.ThrowIfCancellationRequested();
                    if (MediaAnalyzer.IsImageFile(file.Name)) files.Add(file);
                    if (files.Count == 4) break;
                }
                if (files.Count == 0) return null;
                var key = $"{dir.FullName}|{dir.LastWriteTimeUtc.Ticks}|{Edge}|" + string.Join("|",files.Select(f => $"{f.FullName}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
                lock (Cache) if (Cache.TryGetValue(key, out var hit)) return hit;
                var tiles = new List<Pixels>(4);
                foreach (var f in files)
                {
                    token.ThrowIfCancellationRequested();
                    try { if (await decode(f.FullName, token).ConfigureAwait(false) is { } p && p.Width > 0 && p.Height > 0 && (long)p.Width*p.Height*4 == p.Bgra.Length) tiles.Add(p); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or System.Runtime.InteropServices.COMException or ArgumentException) { }
                }
                token.ThrowIfCancellationRequested();
                if (tiles.Count == 0) return null;
                var bmp = Compose(tiles);
                lock (Cache)
                {
                    if (Cache.TryAdd(key, bmp)) Order.Enqueue(key);
                    if (Order.Count > 32) Cache.Remove(Order.Dequeue());
                }
                return bmp;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
            finally { Workers.Release(); }
        }, token);

    private static byte[] Compose(List<Pixels> tiles)
    {
        var cell = tiles.Count == 1 ? Edge : Edge / 2;
        var width = Edge; var height = Edge;
        if (tiles.Count == 1) { var p=tiles[0]; width=Math.Max(1, Edge*p.Width/Math.Max(p.Width,p.Height)); height=Math.Max(1, Edge*p.Height/Math.Max(p.Width,p.Height)); }
        // Top-down 32-bit BI_RGB works with both native decoders without an extra imaging runtime.
        var bytes = new byte[54 + width * height * 4];
        using (var w = new BinaryWriter(new MemoryStream(bytes)))
        { w.Write((ushort)0x4d42); w.Write(bytes.Length); w.Write(0); w.Write(54); w.Write(40); w.Write(width); w.Write(-height); w.Write((ushort)1); w.Write((ushort)32); }
        for (int i=0;i<tiles.Count;i++)
        {
            var p=tiles[i]; int cw=tiles.Count==1 ? width : cell-2, ch=tiles.Count==1 ? height : cell-2;
            var scale=Math.Max(cw/(double)p.Width,ch/(double)p.Height);
            int dx=(i%2)*cell, dy=(i/2)*cell;
            for(int y=0;y<ch;y++) for(int x=0;x<cw;x++)
            {
                int sy=Math.Clamp((int)((y-ch*.5)/scale+p.Height*.5),0,p.Height-1), sx=Math.Clamp((int)((x-cw*.5)/scale+p.Width*.5),0,p.Width-1);
                int a=(sy*p.Width+sx)*4,b=54+((dy+y)*width+dx+x)*4;
                bytes[b]=p.Bgra[a]; bytes[b+1]=p.Bgra[a+1]; bytes[b+2]=p.Bgra[a+2]; bytes[b+3]=255;
            }
        }
        return bytes;
    }
}
