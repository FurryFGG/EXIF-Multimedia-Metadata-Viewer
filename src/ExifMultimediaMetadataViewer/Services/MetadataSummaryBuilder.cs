using System.Globalization;
using System.Text.RegularExpressions;
using ExifMetadata.Core.Models;
using ExifMultimediaMetadataViewer.ViewModels;

namespace ExifMultimediaMetadataViewer.Services;

public static partial class MetadataSummaryBuilder
{
    public static IReadOnlyList<MetadataSummarySection> Build(MediaAnalysis analysis, string previewStatus)
    {
        var fields = analysis.AllFields;
        var file = new List<MetadataSummaryField>
        {
            Actual("文件名", "File Name", Path.GetFileName(analysis.FilePath), ""),
            Actual("完整路径", "Full Path", analysis.FilePath, ""),
            Field(fields, "文件格式", "File Format", "FileType"),
            Field(fields, "MIME 类型", "MIME Type", "MIMEType"),
            Actual("文件大小", "File Size", FormatBytes(analysis.FileSize), ""),
            Field(fields, "图像宽度", "Image Width", "ImageWidth", "ExifImageWidth", "PixelXDimension", "width"),
            Field(fields, "图像高度", "Image Height", "ImageHeight", "ExifImageHeight", "PixelYDimension", "height"),
            Field(fields, "原始方向标签", "EXIF Orientation Tag", "Orientation"),
            Field(fields, "水平分辨率", "Horizontal Resolution", "XResolution"),
            Field(fields, "垂直分辨率", "Vertical Resolution", "YResolution"),
            Field(fields, "位深", "Bit Depth", "BitsPerSample", "BitDepth"),
            Field(fields, "色彩空间", "Color Space", "ColorSpace", "colorspace"),
            Field(fields, "色彩配置文件", "Color Profile", "ProfileDescription", "ICC_Profile:ProfileDescription"),
            Field(fields, "压缩方式", "Compression", "Compression", "compression"),
            Actual("文件修改时间（系统）", "File Modified (File System)", FormatSystemTime(analysis.LastWriteTimeUtc), ""),
            Actual("文件创建时间（系统）", "File Created (File System)",
                analysis.CreationTimeUtc is { } created ? FormatSystemTime(created) : "无", ""),
            Actual("预览状态", "Preview Status", DisplayValue(previewStatus), ""),
            Actual("EXIF 读取状态", "EXIF Read Status", ExifStatus(analysis.ExifTool), "")
        };

        var camera = new List<MetadataSummaryField>
        {
            Field(fields, "相机厂商", "Camera Make", "Make"),
            Field(fields, "相机型号", "Camera Model", "Model"),
            Field(fields, "机身序列号", "Body Serial Number", "BodySerialNumber", "SerialNumber"),
            Field(fields, "固件版本", "Firmware Version", "FirmwareVersion"),
            Field(fields, "镜头厂商", "Lens Make", "LensMake"),
            Field(fields, "镜头型号", "Lens Model", "LensModel"),
            Field(fields, "镜头序列号", "Lens Serial Number", "LensSerialNumber"),
            Field(fields, "镜头识别代码", "Lens Identifier", "LensID", "LensType"),
            Field(fields, "拍摄模式", "Shooting Mode", "ExposureProgram", "ShootingMode"),
            Field(fields, "场景模式", "Scene Mode", "SceneCaptureType", "SceneType")
        };

        var exposure = new List<MetadataSummaryField>
        {
            Field(fields, "曝光时间", "Exposure Time", "ExposureTime"),
            Field(fields, "快门速度", "Shutter Speed", "ShutterSpeedValue"),
            Field(fields, "光圈", "Aperture", "FNumber", "ApertureValue"),
            Field(fields, "感光度", "ISO", "ISO", "PhotographicSensitivity"),
            Field(fields, "焦距", "Focal Length", "FocalLength"),
            Field(fields, "35mm 等效焦距", "35mm Equivalent", "FocalLengthIn35mmFormat"),
            Field(fields, "曝光补偿", "Exposure Compensation", "ExposureCompensation"),
            Field(fields, "测光模式", "Metering Mode", "MeteringMode"),
            Field(fields, "白平衡", "White Balance", "WhiteBalance"),
            Field(fields, "闪光灯", "Flash", "Flash"),
            Field(fields, "对焦模式", "Focus Mode", "FocusMode", "AFMode"),
            Field(fields, "对焦区域", "Focus Area", "FocusArea", "AFPointSelected", "SubjectArea"),
            Field(fields, "数字变焦", "Digital Zoom", "DigitalZoomRatio")
        };

        var timeAndRights = new List<MetadataSummaryField>
        {
            Field(fields, "原始拍摄时间", "Date Taken", "DateTimeOriginal"),
            Field(fields, "数字化时间", "Date Digitized", "CreateDate"),
            Field(fields, "媒体创建时间", "Media Create Time", "CreationDate", "DateCreated"),
            Field(fields, "元数据修改时间", "Metadata Modified", "MetadataDate", "ModifyDate"),
            Field(fields, "原始时间时区", "Original Time Zone", "OffsetTimeOriginal"),
            Field(fields, "数字化时间时区", "Digitized Time Zone", "OffsetTimeDigitized"),
            Field(fields, "时间亚秒", "Sub-second Time", "SubSecTimeOriginal", "SubSecTimeDigitized"),
            Field(fields, "拍摄者", "Creator", "Artist", "Creator", "By-line"),
            Field(fields, "版权", "Copyright", "Copyright"),
            Field(fields, "描述", "Description", "ImageDescription", "Description"),
            Field(fields, "标题", "Title", "Title", "ObjectName"),
            Field(fields, "关键词", "Keywords", "Keywords", "Subject")
        };

        var latitude = Find(fields, "GPSLatitude");
        var longitude = Find(fields, "GPSLongitude");
        var gps = new List<MetadataSummaryField>
        {
            Actual("纬度（十进制度）", "Latitude (Decimal)", ToDecimalCoordinate(latitude, Find(fields, "GPSLatitudeRef")), latitude?.FullPath ?? ""),
            Actual("纬度（度分秒）", "Latitude (DMS)", FieldValue(latitude), latitude?.FullPath ?? ""),
            Actual("经度（十进制度）", "Longitude (Decimal)", ToDecimalCoordinate(longitude, Find(fields, "GPSLongitudeRef")), longitude?.FullPath ?? ""),
            Actual("经度（度分秒）", "Longitude (DMS)", FieldValue(longitude), longitude?.FullPath ?? ""),
            Field(fields, "海拔", "Altitude", "GPSAltitude"),
            Field(fields, "GPS 方向", "GPS Direction", "GPSImgDirection"),
            Field(fields, "GPS 时间", "GPS Timestamp", "GPSTimeStamp"),
            Field(fields, "GPS 日期", "GPS Date", "GPSDateStamp"),
            Field(fields, "GPS 速度", "GPS Speed", "GPSSpeed"),
            Field(fields, "定位状态", "GPS Status", "GPSStatus"),
            Field(fields, "定位方式", "GPS Fix", "GPSMeasureMode", "GPSProcessingMethod"),
            Field(fields, "定位精度", "GPS Accuracy", "GPSDOP", "HorizontalPositioningError")
        };

        var video = new List<MetadataSummaryField>
        {
            Field(fields, "时长", "Duration", "Duration"),
            Field(fields, "视频编码", "Video Codec", "codec_name"),
            Field(fields, "视频帧率", "Frame Rate", "avg_frame_rate", "r_frame_rate"),
            Field(fields, "视频码率", "Video Bit Rate", "bit_rate"),
            Field(fields, "音频采样率", "Audio Sample Rate", "sample_rate"),
            Field(fields, "音频声道", "Audio Channels", "channels", "channel_layout")
        };

        return
        [
            new MetadataSummarySection("文件信息 / File", file),
            new MetadataSummarySection("相机与镜头 / Camera & Lens", camera),
            new MetadataSummarySection("曝光与拍摄参数 / Exposure", exposure),
            new MetadataSummarySection("时间与版权 / Time & Rights", timeAndRights),
            new MetadataSummarySection("地理位置 / GPS", gps),
            new MetadataSummarySection("视频与音频 / Video & Audio", video)
        ];
    }

    private static MetadataSummaryField Field(
        IReadOnlyList<MetadataField> fields,
        string labelZh,
        string labelEn,
        params string[] tagNames)
    {
        var field = Find(fields, tagNames);
        return Actual(labelZh, labelEn, FieldValue(field), field?.FullPath ?? "");
    }

    private static MetadataField? Find(IReadOnlyList<MetadataField> fields, params string[] tagNames)
    {
        foreach (var tagName in tagNames)
        {
            var field = fields.FirstOrDefault(candidate =>
                candidate.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
                return field;
        }
        return null;
    }

    private static string FieldValue(MetadataField? field)
    {
        if (field is null)
            return "无";
        var value = string.IsNullOrWhiteSpace(field.FormattedValue) ? field.RawValue : field.FormattedValue;
        return DisplayValue(value);
    }

    private static MetadataSummaryField Actual(string labelZh, string labelEn, string? value, string sourcePath) =>
        new(labelZh, labelEn, DisplayValue(value), sourcePath);

    private static string DisplayValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "无";
        var trimmed = value.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("undefined", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("nan", StringComparison.OrdinalIgnoreCase)
                ? "无"
                : trimmed;
    }

    private static string ExifStatus(ParserResult result) => result.Status switch
    {
        ParserStatus.Success => "已读取",
        ParserStatus.Partial => "部分读取",
        ParserStatus.NoMetadata => "未发现 EXIF",
        ParserStatus.Unsupported => "不支持此格式",
        _ when result.WasExecuted => $"读取失败：{DisplayValue(result.Message)}",
        _ => "未读取"
    };

    private static string FormatSystemTime(DateTime utc) =>
        utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var index = 0;
        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return $"{size:0.##} {units[index]} ({bytes:N0} B)";
    }

    private static string ToDecimalCoordinate(MetadataField? coordinate, MetadataField? reference)
    {
        if (coordinate is null)
            return "无";
        var source = FieldValue(coordinate);
        if (source == "无")
            return "无";

        var numbers = CoordinateNumberRegex().Matches(source)
            .Select(match => double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN)
            .Where(number => double.IsFinite(number))
            .ToArray();
        if (numbers.Length == 0)
            return "无";

        var absolute = numbers.Length >= 3
            ? Math.Abs(numbers[0]) + Math.Abs(numbers[1]) / 60 + Math.Abs(numbers[2]) / 3600
            : Math.Abs(numbers[0]);
        var refValue = reference is null ? source : FieldValue(reference);
        var negative = numbers[0] < 0
            || refValue.Contains("S", StringComparison.OrdinalIgnoreCase)
            || refValue.Contains("W", StringComparison.OrdinalIgnoreCase);
        return $"{(negative ? -absolute : absolute).ToString("0.########", CultureInfo.InvariantCulture)}°";
    }

    [GeneratedRegex("[+-]?\\d+(?:\\.\\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex CoordinateNumberRegex();
}
