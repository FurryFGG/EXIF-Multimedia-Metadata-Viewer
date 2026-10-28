using System.Text.Json;
using ExifMetadata.Core.Models;

namespace ExifMetadata.Core.Services;

public sealed class FFprobeService
{
    public const string ParserName = "FFprobe";

    public async Task<ParserResult> ProbeAsync(
        string executablePath,
        string mediaPath,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(executablePath))
            return new ParserResult
            {
                ParserName = ParserName,
                Status = ParserStatus.Failed,
                Message = $"找不到 FFprobe：{executablePath}"
            };

        try
        {
            var execution = await ProcessRunner.RunTextAsync(
                executablePath,
                [
                    "-v", "error",
                    "-show_format",
                    "-show_streams",
                    "-show_chapters",
                    "-show_programs",
                    "-of", "json",
                    "-i", Path.GetFullPath(mediaPath)
                ],
                Path.GetDirectoryName(executablePath)!,
                timeout ?? TimeSpan.FromMinutes(2),
                cancellationToken).ConfigureAwait(false);

            var fields = new List<MetadataField>();
            string? version = null;
            string? parseProblem = null;
            var recognized = false;

            if (!string.IsNullOrWhiteSpace(execution.StandardOutput))
            {
                try
                {
                    using var document = JsonDocument.Parse(execution.StandardOutput);
                    var root = document.RootElement;
                    recognized = root.ValueKind == JsonValueKind.Object
                        && (root.TryGetProperty("format", out _) || root.TryGetProperty("streams", out _));
                    version = TryReadFFprobeVersion(execution.StandardError);
                    if (root.ValueKind == JsonValueKind.Object)
                        Flatten(root, "", fields, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
                }
                catch (JsonException exception)
                {
                    parseProblem = $"FFprobe returned invalid JSON: {exception.Message}";
                }
            }
            else if (execution.ExitCode == 0)
            {
                parseProblem = "FFprobe returned no JSON output.";
            }

            var status = execution.TimedOut
                ? ParserStatus.TimedOut
                : execution.ExitCode is null
                    ? ParserStatus.Failed
                    : parseProblem is not null
                        ? fields.Count > 0 ? ParserStatus.Partial : ParserStatus.Failed
                        : execution.ExitCode != 0
                            ? fields.Count > 0 ? ParserStatus.Partial : ParserStatus.Unsupported
                            : !recognized
                                ? ParserStatus.NoMetadata
                                : fields.Count == 0 ? ParserStatus.NoMetadata : ParserStatus.Success;

            var message = status switch
            {
                ParserStatus.Success => $"读取到 {fields.Count:N0} 个容器、流及标签字段。",
                ParserStatus.NoMetadata => "FFprobe 未发现可展示的容器或流信息。",
                ParserStatus.Unsupported => "FFprobe 无法识别该文件；请查看其他解析器结果。",
                ParserStatus.Partial => "收到部分结果；请检查解析器错误和退出码。",
                ParserStatus.TimedOut => "解析超时，进程已停止。",
                ParserStatus.Failed => "解析失败；请查看错误详情。",
                _ => "FFprobe 未完成。"
            };
            if (parseProblem is not null)
                message = $"{message} {parseProblem}";

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

    private static void Flatten(
        JsonElement element,
        string path,
        List<MetadataField> fields,
        Dictionary<string, int> occurrenceCounts)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = string.IsNullOrEmpty(path)
                    ? property.Name
                    : $"{path}.{property.Name}";
                Flatten(property.Value, propertyPath, fields, occurrenceCounts);
            }
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                Flatten(item, $"{path}[{index}]", fields, occurrenceCounts);
                index++;
            }
            if (index > 0)
                return;
        }

        var lastSeparator = path.LastIndexOfAny(['.', '[']);
        var tagName = lastSeparator >= 0 ? path[(lastSeparator + 1)..].TrimEnd(']') : path;
        var group = lastSeparator >= 0 ? path[..lastSeparator] : "";
        occurrenceCounts.TryGetValue(tagName, out var occurrence);
        occurrenceCounts[tagName] = occurrence + 1;

        fields.Add(new MetadataField
        {
            Source = ParserName,
            TagName = tagName,
            FullPath = path,
            Group = group,
            TagId = null,
            RawValue = element.GetRawText(),
            FormattedValue = element.ValueKind == JsonValueKind.String
                ? element.GetString() ?? ""
                : element.GetRawText(),
            DataType = element.ValueKind.ToString(),
            RawJson = element.GetRawText(),
            OccurrenceIndex = occurrence + 1
        });
    }

    private static string? TryReadFFprobeVersion(string standardError)
    {
        foreach (var line in standardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("ffprobe version ", StringComparison.OrdinalIgnoreCase))
                return line.Split(' ', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(2);
        }
        return null;
    }
}
