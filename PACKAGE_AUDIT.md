# MetaLens 发布包审计

基线：v1.2.0，记录日期 2026-11-14。

## 总体体积

| 组件 | 文件数 | 字节数 | 用途/结论 |
|---|---:|---:|---|
| 完整 app | 922 | 339,858,020 | win-x64 self-contained WPF 应用 |
| FFmpeg | 11 | 158,646,729 | 视频探测和首帧预览；需继续验证最小 DLL 集 |
| ExifTool | 509 | 34,532,036 | 完整 EXIF/MakerNotes/XMP 等解析；不可按标签子集裁剪 |
| 安装包 | 1 | 102,759,113 | Inno Setup LZMA2/max |

## 最大文件（按字节排序）

| 文件 | 字节数 | 判断 |
|---|---:|---|
| `tools/ffmpeg/avcodec-63.dll` | 91,810,816 | FFmpeg 编解码核心，首帧提取需要 |
| `tools/ffmpeg/avfilter-12.dll` | 31,876,096 | 当前 `scale` 视频滤镜依赖；可通过改用输出尺寸参数评估移除 |
| `tools/ffmpeg/avformat-63.dll` | 23,003,136 | 容器读取/ffprobe 必需 |
| `System.Private.CoreLib.dll` | 16,033,616 | self-contained .NET 核心运行时 |
| `PresentationFramework.dll` | 15,824,680 | WPF 必需 |
| `PresentationCore.dll` | 8,324,904 | WPF 图形必需 |
| `System.Private.Xml.dll` | 7,788,328 | self-contained 运行时组件，需发布验证后判断 |
| `tools/ffmpeg/avdevice-63.dll` | 5,008,384 | 本应用不采集设备，候选移除项；必须实测 ffmpeg/ffprobe 后再删 |
| `D3DCompiler_47_cor3.dll` | 4,741,488 | WPF/D3D 渲染依赖，保留 |
| `coreclr.dll` | 4,613,456 | self-contained CLR 必需 |
| `System.Linq.Expressions.dll` | 3,639,080 | .NET 组件，未经裁剪验证前保留 |
| `tools/exiftool/exiftool_files/perl532.dll` | 3,423,232 | Windows ExifTool Perl 运行时必需 |
| `tools/exiftool/.../Geolocation.dat` | 3,303,087 | ExifTool 地理数据，完整元数据能力保留 |
| `tools/ffmpeg/avutil-61.dll` | 3,025,408 | FFmpeg 基础库必需 |
| `System.Data.Common.dll` | 2,770,768 | 发布依赖候选；仅能通过 publish/运行测试判断 |

## 已确认可安全精简的类别

- 应用及第三方 PDB：正式发布不需要；源码与 Git 历史仍保留调试信息来源。
- 非中文/英文的 .NET 卫星资源：应用只提供简体中文 UI，可在构建后按允许列表保留 `zh-Hans` 与必要默认资源，并执行运行测试。
- 测试文件、开发脚本、样例媒体、性能 CSV、用户缓存：不得进入 app 或安装包。
- 应用缩略图/元数据缓存必须位于 `%LOCALAPPDATA%`，不纳入版本目录。

## 暂不启用的高风险精简

- `PublishTrimmed`：WPF、反射和资源解析兼容性风险较高，未经完整测试不启用。
- 强制 single-file：不保证安装包更小，且本项目仍需外置 ExifTool/FFmpeg；本轮不以文件数换取启动风险。
- 删除 ExifTool 模块：会破坏未知字段、MakerNotes、重复标签和厂商扩展的完整保留要求。
- 删除 FFmpeg DLL：必须先用 JPEG/RAW/MP4 启动及首帧测试确认依赖闭包。

## v1.2.1 审计计划

1. 发布时禁用 PDB 复制，设置 `SatelliteResourceLanguages=zh-Hans;en`。
2. 将 FFmpeg 的 `scale=1600:-2` 改为输出尺寸参数后，用真实视频验证是否可去除 `avfilter`；用依赖/运行测试确认 `avdevice` 是否可去除。
3. 保留 self-contained x64，避免要求用户预装 .NET 10；同时记录 framework-dependent 候选体积，但不作为默认发布物。
4. 生成 v1.2.1 后重新统计每组文件、安装包、减少字节和百分比，并把实测结果补入本文及 `PERFORMANCE_REPORT.md`。
