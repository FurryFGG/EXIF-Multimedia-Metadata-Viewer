using System.Text;
using System.Text.Json;
using ExifMetadata.Core.Models;
using ExifMetadata.Core.Services;

var projectRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var exifToolPath = Path.Combine(projectRoot, "third_party", "exiftool", "exiftool.exe");
var ffprobePath = Path.Combine(projectRoot, "third_party", "ffmpeg", "bin", "ffprobe.exe");
var sampleDirectory = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(projectRoot, "test-samples");
var sampleFiles = Directory.Exists(sampleDirectory)
    ? Directory.EnumerateFiles(sampleDirectory)
        .Where(MediaAnalyzer.IsKnownMediaFile)
        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        .ToArray()
    : [];

var failures = new List<string>();
RunViewportGeometryChecks(failures);
var analyzer = new MediaAnalyzer();
var reports = new List<(string Name, MediaAnalysis Analysis)>();
var sawAudioStream = false;

if (!File.Exists(exifToolPath))
    failures.Add($"ExifTool executable missing: {exifToolPath}");
if (!File.Exists(ffprobePath))
    failures.Add($"FFprobe executable missing: {ffprobePath}");
if (args.Length > 1 && sampleFiles.Length == 0)
    failures.Add($"No media samples found in {sampleDirectory}");
if (args.Length <= 1 && sampleFiles.Length == 0)
    Console.WriteLine("SKIP: no external media samples were provided.");

foreach (var path in sampleFiles)
{
    var analysis = await analyzer.AnalyzeAsync(path, exifToolPath, ffprobePath);
    reports.Add((Path.GetFileName(path), analysis));

    if (analysis.ExifTool.WasExecuted && !string.IsNullOrWhiteSpace(analysis.ExifTool.RawJson))
    {
        using var document = JsonDocument.Parse(analysis.ExifTool.RawJson);
        var expectedCount = document.RootElement.EnumerateArray().Sum(
            item => item.ValueKind == JsonValueKind.Object ? item.EnumerateObject().Count() : 0);
        AssertEqual(expectedCount, analysis.ExifTool.Fields.Count, $"{Path.GetFileName(path)} ExifTool field count", failures);
    }

    if (analysis.FFprobe.WasExecuted && !string.IsNullOrWhiteSpace(analysis.FFprobe.RawJson))
    {
        using var document = JsonDocument.Parse(analysis.FFprobe.RawJson);
        var expectedCount = CountLeaves(document.RootElement);
        AssertEqual(expectedCount, analysis.FFprobe.Fields.Count, $"{Path.GetFileName(path)} FFprobe leaf count", failures);

        var root = document.RootElement;
        if (root.TryGetProperty("format", out var format)
            && format.TryGetProperty("format_name", out _)
            && !analysis.FFprobe.Fields.Any(field => field.FullPath == "format.format_name"))
            failures.Add($"{Path.GetFileName(path)} is missing its FFprobe container format field.");

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            var indexedStreams = streams.EnumerateArray().ToArray();
            foreach (var streamType in indexedStreams
                         .Where(stream => stream.TryGetProperty("codec_type", out _))
                         .Select(stream => stream.GetProperty("codec_type").GetString())
                         .Where(type => type is not null)
                         .Distinct(StringComparer.Ordinal))
            {
                var expectedTypeCount = indexedStreams.Count(stream =>
                    stream.TryGetProperty("codec_type", out var type)
                    && string.Equals(type.GetString(), streamType, StringComparison.Ordinal));
                var actualTypeCount = analysis.FFprobe.Fields.Count(field =>
                    field.FullPath.EndsWith(".codec_type", StringComparison.Ordinal)
                    && string.Equals(field.FormattedValue, streamType, StringComparison.Ordinal));
                AssertEqual(expectedTypeCount, actualTypeCount,
                    $"{Path.GetFileName(path)} {streamType} stream count", failures);
            }

            for (var index = 0; index < indexedStreams.Length; index++)
            {
                var stream = indexedStreams[index];
                var prefix = $"streams[{index}].";
                foreach (var propertyName in new[] { "codec_name", "codec_type", "avg_frame_rate", "r_frame_rate" })
                {
                    if (stream.TryGetProperty(propertyName, out _)
                        && !analysis.FFprobe.Fields.Any(field => field.FullPath == prefix + propertyName))
                        failures.Add($"{Path.GetFileName(path)} is missing stream field {prefix}{propertyName}.");
                }

                if (stream.TryGetProperty("codec_type", out var codecType)
                    && codecType.GetString() == "audio")
                    sawAudioStream = true;
            }
        }
    }

    if (analysis.ExifTool.Fields.Any(field =>
            field.RawJson.Length > 0 && !IsJson(field.RawJson)))
        failures.Add($"{Path.GetFileName(path)} contains a non-JSON raw tag payload.");

    Console.WriteLine(
        $"{Path.GetFileName(path)}: ExifTool {analysis.ExifTool.Status} " +
        $"({analysis.ExifTool.Fields.Count:N0} fields), FFprobe {analysis.FFprobe.Status} " +
        $"({analysis.FFprobe.Fields.Count:N0} fields)");
}

if (reports.Count == 0)
    Console.WriteLine("No binary-media integration assertions were run.");
else if (!sawAudioStream)
    failures.Add("The supplied video samples did not validate an audio stream.");

var arw = reports.FirstOrDefault(report => report.Name.EndsWith(".ARW", StringComparison.OrdinalIgnoreCase));
if (arw.Analysis is not null)
{
    var fields = arw.Analysis.ExifTool.Fields;
    if (fields.Count < 1_000)
        failures.Add("Sony ARW sample did not retain the expected large vendor metadata set.");
    if (!fields.Any(field => field.TagName.Contains("LensModel", StringComparison.OrdinalIgnoreCase)))
        failures.Add("Sony ARW sample is missing a lens-model tag.");
    if (!fields.Any(field => field.TagName.Contains("PreviewImage", StringComparison.OrdinalIgnoreCase)))
        failures.Add("Sony ARW sample is missing its embedded preview metadata.");
    if (!fields.GroupBy(field => field.TagName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        failures.Add("Sony ARW sample did not retain same-name tags from different groups.");

    var tempDirectory = Path.Combine(Path.GetTempPath(), "ExifMetadata-IntegrationTests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDirectory);
    try
    {
        var jsonPath = Path.Combine(tempDirectory, "结果", "metadata.json");
        await MetadataExportService.ExportJsonAsync(arw.Analysis, jsonPath);
        using var exported = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
        var exportedFields = exported.RootElement.GetProperty("fields");
        AssertEqual(arw.Analysis.AllFields.Count, exportedFields.GetArrayLength(), "JSON exported field count", failures);
        var exportedExifJson = exported.RootElement.GetProperty("parsers").GetProperty("exifTool").GetProperty("rawJson");
        using var originalExifJson = JsonDocument.Parse(arw.Analysis.ExifTool.RawJson);
        if (!JsonElement.DeepEquals(originalExifJson.RootElement, exportedExifJson))
            failures.Add("JSON export changed the ExifTool source structure or values.");

        var csvPath = Path.Combine(tempDirectory, "metadata.csv");
        await MetadataExportService.ExportCsvAsync(arw.Analysis, csvPath);
        var csv = await File.ReadAllTextAsync(csvPath, Encoding.UTF8);
        if (!csv.StartsWith("Source,Group,FullPath,TagName", StringComparison.Ordinal)
            || !csv.Contains("LensModel", StringComparison.Ordinal))
            failures.Add("CSV export is missing the header or representative metadata fields.");
        AssertEqual(arw.Analysis.AllFields.Count + 1, CountCsvRecords(csv), "CSV exported record count", failures);

        var chineseDirectory = Path.Combine(tempDirectory, "中文目录");
        Directory.CreateDirectory(chineseDirectory);
        var chinesePath = Path.Combine(chineseDirectory, "中文文件名.ARW");
        File.Copy(arw.Analysis.FilePath, chinesePath);
        var chineseAnalysis = await analyzer.AnalyzeAsync(chinesePath, exifToolPath, ffprobePath);
        if (!chineseAnalysis.ExifTool.Fields.Any(field => field.TagName.Equals("FileName", StringComparison.OrdinalIgnoreCase)
                                                          && field.FormattedValue.Contains("中文文件名", StringComparison.Ordinal)))
            failures.Add("ExifTool did not preserve the Chinese media filename in parser output.");

        var guardPath = Path.Combine(tempDirectory, "guard.jpg");
        await File.WriteAllTextAsync(guardPath, "guard", Encoding.UTF8);
        var guardAnalysis = arw.Analysis with { FilePath = guardPath };
        try
        {
            await MetadataExportService.ExportJsonAsync(guardAnalysis, guardPath);
            failures.Add("JSON export did not block overwriting the media file.");
        }
        catch (IOException)
        {
            AssertEqual("guard", await File.ReadAllTextAsync(guardPath, Encoding.UTF8), "overwrite guard", failures);
        }
    }
    catch (Exception exception)
    {
        failures.Add($"Export and Unicode-path checks threw: {exception}");
    }
    finally
    {
        Directory.Delete(tempDirectory, recursive: true);
    }
}

var corruptDirectory = Path.Combine(Path.GetTempPath(), "ExifMetadata-CorruptTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(corruptDirectory);
try
{
    var corruptPath = Path.Combine(corruptDirectory, "损坏文件.jpg");
    await File.WriteAllBytesAsync(corruptPath, [0x00, 0xFF, 0x7F, 0x80, 0x00, 0x10, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06]);
    var result = await analyzer.AnalyzeAsync(corruptPath, exifToolPath, ffprobePath);
    if (!result.ExifTool.WasExecuted)
        failures.Add("Corrupt-file analysis did not report an ExifTool attempt.");
    if (result.ExifTool.Status is not (ParserStatus.Unsupported or ParserStatus.Partial or ParserStatus.Failed))
    {
        var diagnostic = result.ExifTool.StandardError
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "no parser diagnostic";
        failures.Add($"Corrupt binary did not report unsupported, failed, or partial ({result.ExifTool.Status}; {diagnostic}).");
    }
    Console.WriteLine($"损坏文件.jpg: ExifTool {result.ExifTool.Status} ({result.ExifTool.Message})");
}
finally
{
    Directory.Delete(corruptDirectory, recursive: true);
}

foreach (var failure in failures)
    Console.Error.WriteLine("FAIL: " + failure);
Console.WriteLine(failures.Count == 0
    ? $"PASS: {reports.Count} sample files and export/path checks."
    : $"FAIL: {failures.Count} integration check(s).");
return failures.Count == 0 ? 0 : 1;

static void RunViewportGeometryChecks(List<string> failures)
{
    AssertNear(0.2, ViewportGeometry.FitScale(4000, 6000, 2000, 1200, 0), "portrait fit scale", failures);
    AssertNear(0.2, ViewportGeometry.FitScale(6000, 4000, 2000, 1200, 90), "rotated landscape fit scale", failures);
    var square = ViewportGeometry.FitScale(2000, 2000, 1200, 800, 0);
    AssertNear(0.4, square, "square fit scale", failures);

    var zoomed = ViewportGeometry.ZoomPanAt(
        new ViewportPoint(300, 250), 4000, 3000, 1000, 800, 0.2, 0.4, 0, 0, 0);
    var before = ViewportGeometry.ViewportToImage(
        new ViewportPoint(300, 250), 4000, 3000, 1000, 800, 0.2, 0, 0, 0);
    var after = ViewportGeometry.ViewportToImage(
        new ViewportPoint(300, 250), 4000, 3000, 1000, 800, 0.4, 0, zoomed.X, zoomed.Y);
    AssertNear(before.X, after.X, "zoom cursor image X anchor", failures);
    AssertNear(before.Y, after.Y, "zoom cursor image Y anchor", failures);

    var clamped = ViewportGeometry.ClampPan(10000, -10000, 4000, 3000, 1000, 800, 0.5, 0);
    AssertNear(500, clamped.X, "horizontal pan bound", failures);
    AssertNear(-350, clamped.Y, "vertical pan bound", failures);
    var fitPan = ViewportGeometry.ClampPan(200, -200, 400, 300, 1000, 800, 1, 0);
    AssertNear(0, fitPan.X, "small image horizontal pan", failures);
    AssertNear(0, fitPan.Y, "small image vertical pan", failures);
}

static void AssertNear(double expected, double actual, string name, List<string> failures)
{
    if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 1e-6)
        failures.Add($"{name}: expected {expected}, received {actual}.");
}

static int CountLeaves(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.Object => element.EnumerateObject().Sum(property => CountLeaves(property.Value)),
    JsonValueKind.Array => element.GetArrayLength() == 0 ? 1 : element.EnumerateArray().Sum(CountLeaves),
    _ => 1
};

static bool IsJson(string value)
{
    try
    {
        using var _ = JsonDocument.Parse(value);
        return true;
    }
    catch (JsonException)
    {
        return false;
    }
}

static int CountCsvRecords(string csv)
{
    var records = 0;
    var insideQuotes = false;
    for (var index = 0; index < csv.Length; index++)
    {
        var current = csv[index];
        if (current == '"')
        {
            if (insideQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
            {
                index++;
                continue;
            }
            insideQuotes = !insideQuotes;
        }
        else if (current == '\n' && !insideQuotes)
        {
            records++;
        }
    }
    return records;
}

static void AssertEqual<T>(T expected, T actual, string name, List<string> failures)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        failures.Add($"{name}: expected {expected}, received {actual}.");
}
