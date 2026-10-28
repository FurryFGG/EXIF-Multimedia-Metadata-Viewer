# EXIF Multimedia Metadata Viewer

Windows x64 桌面媒体检查工具，默认使用中文界面。照片与视频仅在本机只读解析；不会上传媒体文件，也不会修改原文件。

## 当前功能

- 左侧照片预览，支持适应窗口、缩放、旋转；RAW 无法直接解码时尝试 ExifTool 可提取的嵌入预览。
- 视频用 FFmpeg 解码首帧作为预览；暂不提供播放、时间轴和帧/数据包级扫描。
- ExifTool 读取全部分组、重复、未知和厂商标签；保留解析器的原始 JSON、标签 ID、原始数值、易读值和命名空间。
- 视频使用 FFprobe 同时读取容器、流、章节和程序信息；完整原始 JSON 与字段浏览结果分别保留。
- “基本信息”“全部元数据”“原始数据 / 诊断”三个视图；全部元数据支持虚拟化、搜索和复制。
- 导出完整 JSON 或 CSV；重复字段及其来源不会合并。导出文件不可覆盖当前媒体文件。
- 打开文件、文件夹、拖放、最近打开、同目录上/下一个、取消解析、浅色/深色主题。最近文件绝对路径只保存在当前 Windows 用户的 LocalAppData，不会上传。

扩展名只用于文件选择和预览路线，不代表文件一定是对应编码。实际解析结果与错误由 ExifTool、FFprobe 分别报告。解析器原始输出保留，以便发现扁平字段表之外的结构信息。

## 环境与依赖

- Windows x64。最低已验证系统为 Windows 10 专业工作站版 22H2、build 19045；Windows 11 未单独验证。
- .NET 10 SDK。工程没有额外 NuGet 运行时依赖。
- ExifTool 13.59 Windows x64 包含在 third_party/exiftool，使用时需要配套的 exiftool_files 目录。
- FFmpeg/FFprobe Windows x64 共享构建包含在 third_party/ffmpeg/bin，实测版本为 `n9.0.2-36-g6882969ce9-20261027`，许可证为 LGPL-3.0-or-later。下载资产 SHA-256、实际构建参数和许可证资料见 third_party/ffmpeg-build-info.txt 与 third_party/THIRD-PARTY-NOTICES.md。
- 依赖来源、许可证和具体文件见 third_party/THIRD-PARTY-NOTICES.md。FFmpeg 与 ExifTool 均作为独立进程调用，不与应用静态链接。

应用运行时不联网。打开文件后，ExifTool 对所有媒体运行；常见视频扩展名会并行运行 FFprobe。不会默认执行逐帧或逐数据包分析。

## 构建

运行 .\build-release.ps1。脚本发布自包含的 win-x64 版本到 artifacts\publish\win-x64；发布目录需要连同 tools\exiftool 和 tools\ffmpeg 一起保留。

先安装 Inno Setup 6（例如运行 `winget install --id JRSoftware.InnoSetup -e`），再运行 .\build-installer.ps1 生成 Windows 安装程序。默认输出到 artifacts\installer\EXIF-Multimedia-Metadata-Viewer-Setup.exe；安装程序包含完整的自包含运行时、ExifTool、FFmpeg/FFprobe 和第三方许可资料。安装包是本机构建产物，不会提交到 Git。

双击 “EXIF Multimedia Metadata Viewer.exe” 启动。也可以在 Windows 文件选择器中打开媒体，或把媒体文件拖到窗口。

## 测试

不带样片时运行：

    .\run-integration-tests.ps1

如需使用本机样片核对完整字段：

    .\run-integration-tests.ps1 -SampleDirectory "D:\媒体样片"

测试程序比较解析器原始 JSON 的字段数量与应用数据模型，验证 Sony RAW 厂商字段、重复标签、中文路径、JSON 原始输出保留，以及 JSON/CSV 导出。提供样片只读；中文路径和导出检查在临时目录完成并清理。

本工作区实际样片的构建与集成测试结果见 [TEST-REPORT.md](TEST-REPORT.md)。

## 已知限制

- 视频当前预览首帧，不播放视频；耗时的帧级/数据包级诊断未实现。
- RAW 预览依赖 ExifTool 可提取且 WIC 可解码的嵌入预览；元数据读取与预览互相独立。
- HEIC/HEIF/AVIF/WebP 等预览取决于 Windows 上可用的 WIC 解码器；不能预览时仍尝试显示元数据。
- 解析完整性仅对 ExifTool / FFprobe 返回的数据负责；解析器本身不识别的格式或厂商数据仍会报告限制。
- 当前集成样片中没有 MOV、字幕或章节样片；这些结构可由 FFprobe 返回并完整保留，但尚未用真实对应样片验证。
- 预览方向暂以像素数据和 ExifTool/FFmpeg 解码结果为准；当前没有单独旋转矩阵控制。

详细阶段验证结果见 TEST-REPORT.md。
