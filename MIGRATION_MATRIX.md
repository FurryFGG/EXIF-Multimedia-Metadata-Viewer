# MetaLens v2.0.0 功能迁移矩阵

基线是 `v1.2.2-layout` 的 `659b88d`，不是早期 UI 设计图。下表「旧」路径相对 `src/ExifMultimediaMetadataViewer`，「新」相对 `src/MetaLens.WinUI`；`Core` 指 `src/ExifMetadata.Core`。构建产物、运行截图、测试条件和数值见[实机验收](docs/v2.0.0/VALIDATION.md)。

「实测」指实际文件、窗口或安装器执行；「部分」指只覆盖列明的层级或场景；「接入」指有代码路径但没有独立运行断言。不是用编译成功推断功能相同。

|功能|旧：位置与做法|新：位置、迁移方式|验证、相同点与差异|
|---|---|---|---|
|原生宿主、中文工具栏、关于|`App`、`MainWindow.xaml`：WPF Window、资源字典|`App`、`MainWindow.xaml(.cs)`：重写 WinUI Window、CommandBar、ContentDialog|实测：从发布目录及安装路径启动，有响应的真实窗口；关于对话框未逐项点验。无 WPF/Web UI 宿主。|
|打开文件、目录、拖入、历史|`MainWindow`、`ViewerViewModel`、`RecentFilesStore`：WPF 对话框/拖放与最近项|`MainWindow.xaml.cs`、`MainWindow.Workspace.cs`：绑定 HWND 的 Picker、拖放、命令行、共享最近项存储|部分：路径打开 JPEG/RAW/视频和目录均实测；打开媒体/文件夹 Picker、拖入与最近列表每个入口未逐手势复验。|
|浅色、深色、用户设置|`ViewerResources.xaml`、`ViewerSettingsStore`：主题与持久化|`MainWindow.xaml.cs` 的 `RequestedTheme` 和原生 ThemeResource；共享 settings|实测：同一界面两种主题均有截图，布局可跨进程恢复。未新增自动跟随系统主题。|
|原版、三栏、双层|`MainWindow.Layout.cs`、`WorkspaceLayout`：重排 WPF Grid|`MainWindow.Layout.cs` / `Workspace.cs`：重排 Grid，浏览器、预览和元数据实例不换|实测：三种布局、切换后文件与解析对象不变，30 次切换、无额外扫描/解析。|
|分隔条、折叠、最小尺寸与恢复|`GridSplitter`、`WorkspaceLayout`、设置 JSON|`MainWindow.Layout.cs`：9 个布局位置共用 3 个指针处理器，按模式限制尺寸、折叠和复位；缩略图尺寸分模式保存|部分：每布局 3 个位置用实际 `Adjust` 改尺寸，重启恢复；非人工拖完九个位置。极小窗口和全部 DPI 组合未测。|
|驱动器、树、导航|`FileBrowserService`、`DirectoryTreeNode`、`ViewerViewModel`：按层枚举、历史栈|`ExplorerModel`、`ExplorerView`：共享文件系统服务，WinUI TreeView 惰性展开，地址栏与导航菜单重写|部分：实机列出盘符，进入样片目录、「上级」和「返回」实测；离线盘、权限拒绝、树每层点击不作全覆盖声明。|
|混合目录、搜索与排序|`FileBrowserService`、`ViewerViewModel`：目录优先、自然排序、当前目录过滤|`ExplorerModel.RefreshView`、`ExplorerView`：重用排序器；前进、返回、上级、刷新、4 种排序|部分：真实目录筛 `MP4` 只剩视频，清空后 3 项恢复；菜单可见。四种排列组合没有逐组做新旧截图对照。|
|缩略图、多列、列表、详细、文件名|`VirtualizingWrapPanel`、`BrowserEntry` 与三套模板|`ExplorerView.xaml(.cs)`、`BrowserEntry`：原生 GridView/ItemsWrapGrid 和 ListView；长名字换行/省略及完整路径提示|实测：列表真实小缩略图，详细模式「名称/类型/大小/修改时间/预览状态」五列，多列布局截图，千文件滚动虚拟化；未逐例验证所有长文件名悬浮提示。|
|缩略图队列、缓存|`MediaPreviewService`、`BoundedLruCache`：限流，内存、磁盘缓存|`MediaImages`、`ExplorerView`：独立缩略图解码；3 路异步、192 项 LRU、512 MiB/30 天，卸载容器取消|部分：照片/RAW/视频真实卡片、滚动后有界容器数；未模拟完整 30 天或逐项证明所有驱逐路径。|
|图片、RAW 主预览|`MediaPreviewService`：WIC、RAW 内嵌图回退|`MediaImages`、`PreviewView`：原生 BitmapDecoder/BitmapImage，系统 RAW 解码或 ExifTool 内嵌图，标实际来源|实测：JPEG、PNG、TIFF、ARW、NEF、DNG 实际像素；原图与缩略图不同。RAW 尺寸是解码所得，不等于传感器显影。|
|适应、缩放、旋转、平移与全屏|`ImageViewport`、`MainWindow`：图像变换、滚动与全屏|`PreviewView`、`MainWindow.Preview.cs`：按 RasterizationScale 换物理像素、鼠标锚点、指针平移、FullScreen presenter|部分：真实 JPEG 9504 像素、100%/200%、四种补充格式 50–400%、90°、桌面拖动与 F11/Esc；跨屏不同 DPI、每种媒体每个快捷键另验。|
|上一张/下一张、快速切换与取消|`ViewerViewModel`、`ImageViewport`：协调当前文件、取消任务|`MainWindow.Preview.cs` / `Workspace.cs`、`PreviewView`：选中项、方向键、代际校验与 CancellationToken|部分：快速交错 13 次，最后 JPEG 的画面与字段一致；不等于所有文件类型、键盘焦点组合均已验。|
|视频首帧、播放与多流|`MediaPreviewService` 用 FFmpeg 取帧；Core/FFprobe 输出容器与流；旧版仅看首帧|`MediaImages` 取帧；`PreviewView` 新增 MediaPlayerElement，保留全部 FFprobe 字段|实测：真实 MP4 首帧/播放/暂停/跳转；额外 1 视频+2 音轨+1 字幕流逐项解析/导出。未验证实际字幕叠字、多音轨切换、章节/MOV 和所有系统解码器。|
|基本信息、零值、GPS|`Services/MetadataSummaryBuilder`：双语摘要、来源、缺失值、DMS/十进制|`MetadataView`：共享业务摘要并绑定 ListView；修复旧汇总函数对英文 East 的符号误判，不改解析字段|实测：界面显示真实尺寸、零曝光补偿、空值；合成有/无坐标图检验南北东西、零、DMS 与十进制。数字正确性优先于重现旧版东经错误。|
|全部元数据与解析状态|Core `MediaAnalyzer`、`ExifToolService`、`FFprobeService`、`ProcessRunner` 与字段模型|Core 原样复用；`MetadataModel` 限流缓存，`MetadataView` 呈现；显示字段来源和诊断|实测：6 份样片逐字段/出现次数/来源对照，另测四流视频；MakerNotes、重复字段、ICC、原始值保留。损坏 JPEG 得到 Partial，不把警告当作成功。具体相机未提供的标签不推断。|
|搜索、复制字段、全量、原始输出|WPF 全字段视图、剪贴板及原始文本|`MetadataView`：后台过滤、原始文本分页但全量保留；`MainWindow.Metadata.cs`：DataPackage、Clipboard|部分：真实控件筛 `ImageWidth`、选行、生成复制文本、核验全量 raw；系统剪贴板的独立读回及每页翻阅未测。|
|JSON、CSV|Core `MetadataExportService`：字段/来源/原始值/重复项|共享同一导出器；`MainWindow.Metadata.cs` 重写 WinRT 保存入口|实测：新旧解析字段与导出 JSON 数量/CSV 记录数一致，检查转义及中文路径；非逐次手点保存对话框。|
|右键、外部 EXE、文件关联|`MainWindow.FileActions.cs`、`ExternalOpenService`：仅启动目标进程|`MainWindow.Actions.cs`：MenuFlyout、原生 EXE Picker，复用 ArgumentList，记忆仅限进程；安装器不写关联|实测：真实右键进入选择器并取消，测试 helper 收到单个中文带空格参数，缺失文件拒绝，七类 UserChoice 安装前后相同。未用用户的实际编辑器打开私人文件。|
|后台及 Explorer 稳定性|`ViewerSettingsStore`、`MediaPreviewService`、`FileBrowserService`、缓存与取消|`ExplorerModel`/`MediaImages`/`MetadataModel` 共享取消、分批、代际防陈旧；设置 600 ms 防抖|部分：千文件、5 次启动及 15 分钟采样；工作集、线程、句柄不全优于 WPF。没有把有限观察当作无泄漏证明。|
|发布、版本、回退|WPF x64 自包含、Inno Setup，已保留旧发布|`MetaLens.WinUI.csproj`、`build-release.ps1`、`build-installer.ps1`、`installer/`：自包含 WinUI，拒绝覆盖，独立版本目录|实测：真实安装/覆盖重装/卸载、逐文件哈希、运行；旧包仍可用。本地 Git 阶段提交与版本标签保留，未代替用户发布至 GitHub。|
