# MetaLens 2.0：WPF → WinUI 3 功能台账

基线：v1.2.2 分支 v1.2.2-layout，构建提交 659b88d。WPF 项目及其安装包不动。新项目 src/MetaLens.WinUI；以下状态区分源码接入、构建、实机测试，不用编译代替功能验收。

|功能|v1.2.2 实现 / 方式|v2 目标实现|迁移策略|验收状态 / 差异|
|---|---|---|---|---|
|启动、系统 DPI、主题、工具栏、键盘、拖放、最近|App、MainWindow、ViewerViewModel|App、MainWindow、命令/Picker|重写 XAML，复用最近文件|待实现及测试|
|三种布局、折叠、9 个分隔条、尺寸记忆|MainWindow.Layout、WorkspaceLayout、ViewerSettingsStore|WinUI Grid + 指针事件|共享状态与存储，重写排列|待实现及测试|
|文件夹树、路径、前进后退、刷新、上级、排序、搜索|FileBrowserService、DirectoryTreeNode、ViewerViewModel|Explorer VM + TreeView|保留分批枚举、自然排序|待实现及测试|
|缩略图、列表、详细信息、长名称、多列|VirtualizingWrapPanel、BrowserEntry|GridView / ListView|重写虚拟化、适配图像对象|待实现及测试|
|图片、RAW 内嵌图、视频首帧|MediaPreviewService (WIC, ExifTool, FFmpeg)|独立 WinRT 解码、跨框架进程执行|按类型解码，主图不降采样|待实现及测试|
|原像素、缩放、平移、旋转、全屏、上一张/下一张|ImageViewport / ViewportGeometry|原生滚动/缩放及 AppWindow|重新实现输入及宿主|待实现及测试|
|视频播放暂停|v1.2.2 仅首帧，不提供播放|原生 MediaPlayerElement|超出基线的测试项需单独说明|待实现及测试|
|EXIF/MakerNotes/GPS/IPTC/XMP/ICC、重复及未知字段、零值、原始 JSON 和诊断|ExifMetadata.Core 的 MediaAnalyzer、ExifToolService、FFprobeService、ProcessRunner|直接引用同一核心|不重写解析器和字段模型|待包内样片对照|
|双语基本信息、GPS 缺失处理、全字段搜索及复制、JSON/CSV|MetadataSummaryBuilder、MetadataExportService、WPF 表格|共享汇总/导出 + WinUI 原生列表|只重写呈现和剪贴板|待实现及测试|
|右键、单次外部打开、中文/空格、不改关联|MainWindow.FileActions、ExternalOpenService|MenuFlyout + 桌面 Picker|共享 ArgumentList，不注册扩展名|待实现及测试|
|受控缓存、后台队列、取消、快速切换|BoundedLruCache、MediaPreviewService、ViewerViewModel|VM/预览服务|保留限额和过期结果保护|待并发、1000 文件及耐久测试|
|发布/体积/版本|Directory.Build.props、两个发布脚本、Inno|新项目独立版本作用域、公共防覆盖流程|原版永久保留|待发布|

部署评估：优先验证 unpackaged x64，自包含 .NET + Windows App SDK，独立 Inno Setup 安装包。无需用户管理员安装全局运行时；代价是包体较大。MSIX 需额外签名信任，并非此处的必需交付。依赖框架模式会要求用户先安装运行时；单文件无法免除外部媒体工具和原生加载。只有新程序和随包工具在目标机器上启动、测试后才归档正式包。
