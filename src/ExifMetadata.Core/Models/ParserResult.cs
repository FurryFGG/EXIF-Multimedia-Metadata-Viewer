namespace ExifMetadata.Core.Models;

public enum ParserStatus
{
    NotRun,
    Success,
    Partial,
    NoMetadata,
    Unsupported,
    Failed,
    TimedOut,
    Cancelled
}

public sealed record ParserResult
{
    public required string ParserName { get; init; }
    public required ParserStatus Status { get; init; }
    public string? Version { get; init; }
    public int? ExitCode { get; init; }
    public TimeSpan? Elapsed { get; init; }
    public string Message { get; init; } = "";
    public string RawJson { get; init; } = "";
    public string StandardError { get; init; } = "";
    public IReadOnlyList<MetadataField> Fields { get; init; } = [];
    public bool WasExecuted { get; init; }
}
