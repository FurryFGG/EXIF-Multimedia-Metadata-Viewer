using System.Text.Json;
using ExifMetadata.Core.Models;

namespace ExifMetadata.Core.Services;

public static class MetadataExportService
{
    public static async Task ExportJsonAsync(
        MediaAnalysis analysis,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var fullDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(fullDestination, analysis.FilePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("导出文件不能覆盖正在读取的媒体原文件。");

        var destinationDirectory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        await using var stream = new FileStream(
            fullDestination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteString("schemaVersion", "1.0");
        writer.WriteString("filePath", analysis.FilePath);
        writer.WriteString("fileName", Path.GetFileName(analysis.FilePath));
        writer.WriteNumber("fileSizeBytes", analysis.FileSize);
        writer.WriteString("lastWriteTimeUtc", analysis.LastWriteTimeUtc);
        writer.WriteString("analyzedAtUtc", analysis.AnalyzedAtUtc);

        writer.WriteStartObject("parsers");
        WriteParser(writer, "exifTool", analysis.ExifTool);
        WriteParser(writer, "ffprobe", analysis.FFprobe);
        writer.WriteEndObject();

        writer.WriteStartArray("fields");
        foreach (var field in analysis.AllFields)
        {
            writer.WriteStartObject();
            writer.WriteString("source", field.Source);
            writer.WriteString("tagName", field.TagName);
            writer.WriteString("fullPath", field.FullPath);
            writer.WriteString("group", field.Group);
            if (field.TagId is null)
                writer.WriteNull("tagId");
            else
                writer.WriteString("tagId", field.TagId);
            writer.WriteString("rawValue", field.RawValue);
            writer.WriteString("formattedValue", field.FormattedValue);
            writer.WriteString("dataType", field.DataType);
            writer.WriteNumber("occurrenceIndex", field.OccurrenceIndex);
            writer.WritePropertyName("rawTagJson");
            WriteRawOrString(writer, field.RawJson);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExportCsvAsync(
        MediaAnalysis analysis,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var fullDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(fullDestination, analysis.FilePath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("导出文件不能覆盖正在读取的媒体原文件。");

        var destinationDirectory = Path.GetDirectoryName(fullDestination);
        if (!string.IsNullOrEmpty(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        await using var stream = new FileStream(
            fullDestination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(true));
        await writer.WriteLineAsync("Source,Group,FullPath,TagName,TagId,OccurrenceIndex,DataType,RawValue,FormattedValue,RawTagJson");
        foreach (var field in analysis.AllFields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new[]
            {
                field.Source, field.Group, field.FullPath, field.TagName, field.TagId ?? "",
                field.OccurrenceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), field.DataType,
                field.RawValue, field.FormattedValue, field.RawJson
            };
            await writer.WriteLineAsync(string.Join(',', values.Select(EscapeCsv)));
        }
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteParser(Utf8JsonWriter writer, string propertyName, ParserResult result)
    {
        writer.WriteStartObject(propertyName);
        writer.WriteString("name", result.ParserName);
        writer.WriteString("status", result.Status.ToString());
        if (result.Version is null)
            writer.WriteNull("version");
        else
            writer.WriteString("version", result.Version);
        if (result.ExitCode is null)
            writer.WriteNull("exitCode");
        else
            writer.WriteNumber("exitCode", result.ExitCode.Value);
        if (result.Elapsed is null)
            writer.WriteNull("elapsedMilliseconds");
        else
            writer.WriteNumber("elapsedMilliseconds", result.Elapsed.Value.TotalMilliseconds);
        writer.WriteString("message", result.Message);
        writer.WriteBoolean("wasExecuted", result.WasExecuted);
        writer.WriteString("standardError", result.StandardError);
        writer.WritePropertyName("rawJson");
        WriteRawOrString(writer, result.RawJson);
        writer.WriteEndObject();
    }

    private static void WriteRawOrString(Utf8JsonWriter writer, string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            writer.WriteNullValue();
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            document.RootElement.WriteTo(writer);
        }
        catch (JsonException)
        {
            writer.WriteStringValue(rawJson);
        }
    }

    private static string EscapeCsv(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
