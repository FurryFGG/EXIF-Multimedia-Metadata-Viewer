# MetaLens · EXIF 全媒体元数据查看器 v2.0.0

MetaLens 是一款中文 Windows x64 桌面应用。打开照片、RAW 或视频，即可在同一窗口查看原像素预览、常用拍摄信息、全部字段及解析器原始输出。媒体只在本机读取，不写回原文件，也不更改 Windows 默认打开方式。

**本地交付：** `releases/v2.0.0/build.4/EXIF全媒体元数据查看器_v2.0.0_Setup.exe`。如不安装，直接运行同目录 `app/EXIF全媒体元数据查看器_v2.0.0.exe`，请保留整个 `app` 文件夹。安装包未签名；SHA-256 与安装验证记录见[验收报告](docs/v2.0.0/VALIDATION.md)。二进制不跟随源码 Git 仓库分发。

## 怎么用

- 工具栏打开文件或文件夹，也可拖入媒体或输入路径。目录菜单 `☰` 提供导航、排序、三种视图和缩略图尺寸；搜索框仅筛选当前目录。顶部「布局」切换原版、三栏、双层布局；拖动分隔条调整比例。
- 预览工具栏选择适应、50%、100%、200%、400%、旋转或全屏。100% 是解码图像像素与屏幕物理像素的 1:1。RAW 会标注实际解码来源及尺寸；有内嵌图不等于有完整传感器图。视频可在 FFmpeg 首帧与系统播放器间切换。
- 右侧选「基本信息」「全部元数据」或「原始数据 / 诊断」。基本信息里没有的值显示「无」；查厂商私有字段或重复标签用全部元数据。可搜索、复制及导出 JSON/CSV。视频流、容器等由 FFprobe 独立报告。
- 右键媒体 →「打开方式」→「选择其他应用…」只传递当前文件；另一个选项仅在此次会话记住所选 EXE。配置、最近打开和缩略图缓存在当前用户目录，不写进相片文件夹。

## 项目结构与迁移

|位置|职责与变化|
|---|---|
|`src/MetaLens.WinUI`|真正的 WinUI 3 XAML 外壳。`MainWindow` 协调命令、布局和取消；`ExplorerView` / `ExplorerModel` 管导航和虚拟视图；`PreviewView` / `MediaImages` 管解码、缩放、播放和有界缓存；`MetadataView` / `MetadataModel` 管查询、呈现与缓存。|
|`src/ExifMetadata.Core`|新旧界面共用的纯 .NET 解析、字段模型、进程执行、JSON/CSV 导出。保留重复字段、来源、命名空间、原始值及诊断。|
|`src/ExifMultimediaMetadataViewer`|保留 WPF 项目；与 v1.2.2 基线相比仅修正共享 GPS 摘要的方向识别。v2 按源码复用设置、最近文件、布局模型、文件系统、双语摘要等，不加载 WPF 窗口或控件。|
|`tests`、`third_party`、`installer`|核心对照和真实窗口/安装测试；随包工具与许可证；按用户安装的 Inno Setup 配置。|

WPF 的自定义虚拟面板、图片视口和 `GridSplitter` 在 v2 改为 WinUI `GridView` / `ListView`、原生滚动与指针输入、共享控件的可重排 `Grid`。界面与业务状态分离，但渲染、指针捕获、剪贴板和播放器操作保留在视图侧。详见[逐项迁移矩阵](MIGRATION_MATRIX.md)、[实机验收及三种布局截图](docs/v2.0.0/VALIDATION.md)、[版本记录](CHANGELOG.md)和[依赖说明](third_party/THIRD-PARTY-NOTICES.md)。

## 构建、复验与回退

使用 Windows x64、PowerShell 7.4+、.NET 10 SDK、Windows SDK、Inno Setup 6。项目锁定 Windows App SDK WinUI/Runtime 2.7.0。正式产物自包含 .NET 与 Windows App SDK，不需要另装 .NET 桌面运行时；系统媒体解码器仍因机器而异。部署选用 EXE，不另发 MSIX。

```powershell
# 从仓库根运行；使用全新构建编号。样片只读，场景目录含 sample.jpg / sample.ARW / sample.mp4。
.\build-release.ps1 -CandidateSuffix build.5 -SampleDirectory 'D:\媒体样片' -ScenarioDirectory 'D:\场景样片'
.\build-installer.ps1 -SkipPublish -CandidateSuffix build.5
# 在没有既有 MetaLens 2 安装的测试账户复验安装和卸载；不会卸载既有用户版本。
.\tests\Test-WinUIInstaller.ps1 -ReleaseDirectory 'releases/v2.0.0/build.4'
```

两个构建脚本均拒绝覆盖已有产物。`build.4` 是本次交付序号，产品版本为 `2.0.0`；示例中的 `build.5` 是另建目录。保留所有旧发布；可直接运行 `releases/v1.2.2/app` 里的旧 EXE。要检查未经修改的旧源码，在仓库根运行 `git worktree add --detach ..\MetaLens-v122-checkout 659b88d`，不会重置当前分支。最终应用由源码 `f55a698` 构建；`v2.0.0` 标签另含验收材料。
