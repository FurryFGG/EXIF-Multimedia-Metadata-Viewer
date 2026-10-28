namespace ExifMetadata.Core.Models;

/// <summary>
/// A loss-aware view of one value returned by a parser. RawJson keeps the
/// parser's complete value object, including descriptions and numeric values.
/// </summary>
public sealed record MetadataField
{
    public required string Source { get; init; }
    public required string TagName { get; init; }
    public required string FullPath { get; init; }
    public required string Group { get; init; }
    public string? TagId { get; init; }
    public required string RawValue { get; init; }
    public required string FormattedValue { get; init; }
    public required string DataType { get; init; }
    public required string RawJson { get; init; }
    public int OccurrenceIndex { get; init; }
}
