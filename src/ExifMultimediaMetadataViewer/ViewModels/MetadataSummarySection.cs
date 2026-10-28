namespace ExifMultimediaMetadataViewer.ViewModels;

public sealed record MetadataSummaryField(
    string LabelZh,
    string LabelEn,
    string Value,
    string SourcePath);

public sealed record MetadataSummarySection(
    string Title,
    IReadOnlyList<MetadataSummaryField> Fields);
