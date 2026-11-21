using System.Diagnostics;

namespace ExifMultimediaMetadataViewer.Services;

public static class ExternalOpenService
{
    public static ProcessStartInfo StartInfo(string program, string media)
    {
        if (!Path.IsPathFullyQualified(program) || !program.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(program))
            throw new FileNotFoundException("找不到所选应用，请重新选择 EXE。", program);
        if (!Path.IsPathFullyQualified(media) || !File.Exists(media))
            throw new FileNotFoundException("文件已移动或删除，请刷新当前目录。", media);
        var start = new ProcessStartInfo(Path.GetFullPath(program)) { UseShellExecute = false };
        start.ArgumentList.Add(Path.GetFullPath(media));
        return start;
    }
    public static void Open(string program, string media)
    {
        using var process = Process.Start(StartInfo(program,media));
    }
}
