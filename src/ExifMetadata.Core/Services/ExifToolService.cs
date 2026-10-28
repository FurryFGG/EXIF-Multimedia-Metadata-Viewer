using System.Globalization;
using System.Text.Json;
using ExifMetadata.Core.Models;

namespace ExifMetadata.Core.Services;

public sealed class ExifToolService
{
    public const string ParserName = "ExifTool";

    public async Task<ParserResult> ExtractAsync(
        string executablePath,
        string mediaPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath))
            return MissingExecutable(executablePath);

        try
        {
            var execution = await ProcessRunner.RunTextAsync(
                executablePath,
                [
                    "-j",
                    "-G0:1:2:3:4:5:7",
                    "-H",
                    "-l",
                    "-a",
                    "-u",
                    "-U",
                    "-struct",
                    "-charset",
                    "filename=UTF8",
                    "-@",
                    "-"
                ],
                Path.GetDirectoryName(executablePath)!,
                timeout ?? TimeSpan.FromMinutes(2),
                cancellationToken,
                string.Concat("--", Environment.NewLine, Path.GetFullPath(mediaPath), Environment.NewLine))
                .ConfigureAwait(false);

            var fields = new List<MetadataField>();
            string? version = null;
            string? parseProblem = null;

            if (!string.IsNullOrWhiteSpace(execution.StandardOutput))
            {
                try
                {
                    using var document = JsonDocument.Parse(execution.StandardOutput);
                    if (document.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in document.RootElement.EnumerateArray())
                        {
                            if (entry.ValueKind != JsonValueKind.Object)
                                continue;
                            fields.AddRange(FlattenExifToolObject(entry));
                            version ??= GetVersion(entry);
                        }
                    }
                    else
                    {
                        parseProblem = "ExifTool returned JSON that was not an array.";
                    }
                }
                catch (JsonException exception)
                {
                    parseProblem = $"ExifTool returned invalid JSON: {exception.Message}";
                }
            }
            else if (execution.ExitCode == 0)
            {
                parseProblem = "ExifTool returned no JSON output.";
            }

            var fileTypeFound = fields.Any(field =>
                field.TagName.Equals("FileType", StringComparison.OrdinalIgnoreCase));

            var status = execution.TimedOut
                ? ParserStatus.TimedOut
                : execution.ExitCode is null
                    ? ParserStatus.Failed
                    : parseProblem is not null
                        ? fields.Count > 0 ? ParserStatus.Partial : ParserStatus.Failed
                        : execution.ExitCode != 0 || !string.IsNullOrWhiteSpace(execution.StandardError)
                            ? fields.Count > 0 ? ParserStatus.Partial : ParserStatus.Failed
                            : !fileTypeFound
                                ? fields.Count == 0 ? ParserStatus.Unsupported : ParserStatus.Partial
                                : fields.Count <= 1 ? ParserStatus.NoMetadata : ParserStatus.Success;

            var message = status switch
            {
                ParserStatus.Success => $"读取到 {fields.Count:N0} 个元数据字段。",
                ParserStatus.NoMetadata => "文件类型已识别，但未发现可展示的媒体元数据。",
                ParserStatus.Unsupported => "ExifTool 未识别该文件类型；请查看其他解析器结果。",
                ParserStatus.Partial => "收到部分结果；请检查解析器警告和退出码。",
                ParserStatus.TimedOut => "解析超时，进程已停止。",
                ParserStatus.Failed => "解析失败；请查看错误详情。",
                _ => "ExifTool 未完成。"
            };

            if (parseProblem is not null)
                message = $"{message} {parseProblem}";
            if (!string.IsNullOrWhiteSpace(execution.StandardError))
                message = $"{message} {execution.StandardError.Trim()}";

            return new ParserResult
            {
                ParserName = ParserName,
                Status = status,
                Version = version,
                ExitCode = execution.ExitCode,
                Elapsed = execution.Elapsed,
                Message = message,
                RawJson = execution.StandardOutput,
                StandardError = execution.StandardError,
                Fields = fields,
                WasExecuted = true
            };
        }
        catch (OperationCanceledException)
        {
            return new ParserResult
            {
                ParserName = ParserName,
                Status = ParserStatus.Cancelled,
                Message = "解析已取消。",
                WasExecuted = true
            };
        }
        catch (Exception exception)
        {
            return new ParserResult
            {
                ParserName = ParserName,
                Status = ParserStatus.Failed,
                Message = exception.Message,
                WasExecuted = true
            };
        }
    }

    private static IEnumerable<MetadataField> FlattenExifToolObject(JsonElement entry)
    {
        var occurrenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in entry.EnumerateObject())
        {
            var fullPath = property.Name;
            var separator = fullPath.LastIndexOf(':');
            var tagName = separator >= 0 ? fullPath[(separator + 1)..] : fullPath;
            var group = separator >= 0 ? fullPath[..separator] : "";
            var occurrenceKey = $"{group}\0{tagName}";
            occurrenceCounts.TryGetValue(occurrenceKey, out var occurrence);
            occurrenceCounts[occurrenceKey] = occurrence + 1;

            var formattedValue = property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("val", out var displayValue)
                    ? ToDisplayString(displayValue)
                    : ToDisplayString(property.Value);
            var rawValue = property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("num", out var numericValue)
                    ? numericValue.GetRawText()
                    : property.Value.ValueKind == JsonValueKind.Object
                        && property.Value.TryGetProperty("val", out var value)
                            ? value.GetRawText()
                            : property.Value.GetRawText();
            var tagId = property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("id", out var idValue)
                    ? ToDisplayString(idValue)
                    : null;
            var dataType = property.Value.ValueKind == JsonValueKind.Object
                && property.Value.TryGetProperty("val", out var typedValue)
                    ? typedValue.ValueKind.ToString()
                    : property.Value.ValueKind.ToString();

            yield return new MetadataField
            {
                Source = ParserName,
                TagName = tagName,
                FullPath = fullPath,
                Group = group,
                TagId = tagId,
                RawValue = rawValue,
                FormattedValue = formattedValue,
                DataType = dataType,
                RawJson = property.Value.GetRawText(),
                OccurrenceIndex = occurrence + 1
            };
        }
    }

    private static string? GetVersion(JsonElement entry)
    {
        foreach (var property in entry.EnumerateObject())
        {
            if (property.Name.Contains("ExifToolVersion", StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty("val", out var value)
                        ? ToDisplayString(value)
                        : ToDisplayString(property.Value);
        }
        return null;
    }

    private static string ToDisplayString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Null => "",
            JsonValueKind.Object or JsonValueKind.Array => value.GetRawText(),
            _ => value.ToString()
        };
    }

    private static ParserResult MissingExecutable(string path) => new()
    {
        ParserName = ParserName,
        Status = ParserStatus.Failed,
        Message = $"找不到 ExifTool：{path}"
    };
}
