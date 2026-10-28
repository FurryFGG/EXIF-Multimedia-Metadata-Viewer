namespace ExifMetadata.Core.Models;

public sealed record MediaAnalysis
{
    public required string FilePath { get; init; }
    public required long FileSize { get; init; }
    public required DateTime LastWriteTimeUtc { get; init; }
    public DateTime? CreationTimeUtc { get; init; }
    public required DateTime AnalyzedAtUtc { get; init; }
    public required ParserResult ExifTool { get; init; }
    public required ParserResult FFprobe { get; init; }
    public required IReadOnlyList<MetadataField> AllFields { get; init; }
}
