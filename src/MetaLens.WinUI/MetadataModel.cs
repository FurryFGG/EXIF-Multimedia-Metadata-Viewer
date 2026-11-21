using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;
using ExifMultimediaMetadataViewer.Services;

namespace MetaLens.WinUI;
internal sealed class MetadataModel
{
    readonly BoundedLruCache<string, MediaAnalysis> cache = new(4, StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim gate = new(1);
    readonly MediaAnalyzer analyzer = new();
    internal int Parses { get; private set; }

    internal async Task<MediaAnalysis> Read(string file, CancellationToken token)
    {
        var info = new FileInfo(file);
        string key = $"{file}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        if (cache.TryGet(key, out var a))
            return a;
        await gate.WaitAsync(token);
        try
        {
            if (cache.TryGet(key, out a))
                return a;
            Parses++;
            a = await analyzer.AnalyzeAsync(file, MediaImages.Tool("exiftool"), MediaImages.Tool("ffprobe"), token);
            token.ThrowIfCancellationRequested();
            if (a.ExifTool.Status is ParserStatus.Success or ParserStatus.Partial && a.FFprobe.Status is not (ParserStatus.Cancelled or ParserStatus.TimedOut or ParserStatus.Failed))
                cache.Set(key, a);
            return a;
        }
        finally
        {
            gate.Release();
        }
    }
}
