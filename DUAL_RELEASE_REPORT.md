# EXIF查看器双版本发布报告 · 2026-11-22

**交付结论：** 两个版本分别构建、打包，在 Windows 10 22H2 x64 完成安装、并存和实样回归。旧版维持 WPF；新版使用原生 WinUI 3。Windows 11 x64 是目标平台，尚无该系统的实机验收结果。两份安装器均未签名。以下 Git 标签为本地源码回退点，不表示已上传 GitHub Releases。

| 实测项目 | v1.2.3 Legacy | v2.0.1 WinUI 3 |
|---|---:|---:|
| 产品与基础版本 | EXIF查看器；WPF v1.2.2 `659b88d` | EXIF查看器；WinUI v2.0.0 `9ee0cad` |
| 文件夹封面 | 单图、2×2、无图时显示原图标 | 相同 |
| 文件浏览 / EXIF 摘要 | 原布局、原排序、原摘要不变 | 保留原版/三栏/双层布局；固定卡片、6 个字段双向排序、分类标题去重 |
| 正式安装器 | **94,268,492 B（89.90 MiB）** | **95,363,159 B（90.95 MiB）** |
| 完整 app 目录（逻辑字节） | 325,678,677 B（310.59 MiB），767 文件 | 367,887,816 B（350.85 MiB），984 文件 |
| 主窗口 / 可响应时间中位（5 次） | 786.2 / **911.5 ms** | 589.9 / **653.6 ms** |
| 空窗口工作集 / 私有内存中位 | 165.2 / 122.0 MiB | 182.3 / 130.5 MiB |
| 1000 个含图片的文件夹，13 个滚动位置 | 20–28 个实际 UI 容器 | 13–48 个实际 UI 容器 |
| 媒体、导出与安装 | 六份实样，JSON/CSV，真实 WPF 窗口及已安装 EXE | 六份实样，JSON/CSV；真实 WinUI 窗口、已安装 EXE 及重装 |

## 怎样使用或回退

本机统一交付目录为 `D:\EXIF\EXIF-Multimedia-Metadata-Viewer\releases`。分别进入 `v1.2.3` 或 `v2.0.1`，运行 `EXIF查看器_v<版本>_Setup.exe`。免安装使用 `app/EXIF查看器_v<版本>.exe`，请保留整个 `app` 目录，不能单独复制主 EXE。主发布树新写入的 769 / 986 个原始发布文件（包括安装器及 BUILD-INFO）逐一比对了 SHA-256；旧目录未覆盖。完整源代码、构建脚本、CHANGELOG 及本报告也分别保存在两个分支下。

| 版本 | 独立源码分支（本机工作区） | 二进制构建提交 | 安装器 SHA-256 |
|---|---|---|---|
| 1.2.3 | `release/1.2.3-legacy`（`D:\EXIF\Legacy-1.2.3`） | `75c168d7a59d28e3400cc813c184fd2f183aa495` | `309BAFACA557DAD7F7D38D9B2AAB84A4927301D4F7F089CA8E1188F732508479` |
| 2.0.1 | `release/2.0.1-winui3`（`D:\EXIF\WinUI-2.0.1`） | `483aedb101b407adc1e8e36d1bcd34a6154acc53` | `8E3CA7FF49911B09A078EB11EC71CC9996DD6B1CC2B92C6176F4700B0D33CD7E` |

标签 `v1.2.3`、`v2.0.1` 指向附有验收报告的源码快照；与上表构建提交的差异仅为文档、验证脚本和证据。需要回看源码时，在仓库中执行以下任一命令；新目录必须尚不存在，不会改变现有工作区。二进制回退直接打开保留的历史版本，不靠 Git 恢复大型发布文件。

```powershell
git worktree add --detach ..\inspect-legacy v1.2.3
git worktree add --detach ..\inspect-winui v2.0.1
```

[两个新安装包与基线文件数和体积](docs/dual-release-evidence/package-sizes.csv)；[七个历史安装器的复核哈希](docs/dual-release-evidence/historical-installers.csv)；[旧版主 EXE 哈希](docs/dual-release-evidence/legacy-build.md)；[新版主 EXE、UI DLL 哈希](docs/dual-release-evidence/winui-build.md)。七个历史包为 1.2.0、1.2.1、1.2.2、2.0.0 原版及 build.2/3/4，均与开发前留存的哈希一致。

## 包体积和依赖

- 对比 v1.2.2：安装器从 101,449,395 B 减少 **7,180,903 B（7.08%）**；app 从 329,205,098 B 减少 3,526,421 B（1.07%）。旧 WPF 运行时、照片预览、原摘要和原排序全部保留；没有把新版功能反向加入。
- 对比 v2.0.0 build.4：安装器从 102,907,981 B 减少 **7,544,822 B（7.33%）**；app 从 371,402,999 B 减少 3,515,183 B（0.95%）。两版从发布输出中排除 `createdump.exe`、`mscordbi.dll`、`mscordaccore.dll` 及版本化的 `mscordaccore_amd64…dll`：牺牲的是独立崩溃转储/外部调试诊断，不是应用的元数据诊断页。Inno 使用 LZMA2 solid 压缩；安装器变小不等于安装后同等幅度变小。
- 新版正式构建只引用纯 .NET 核心工程；文件系统、摘要、设置等独立代码位于 `src/MetaLens.WinUI/Shared`。不通过 WPF 窗口或 WebView 实现界面；没有打包 `PresentationCore`、`PresentationFramework` 或 `Microsoft.WindowsDesktop.App`。保留的 `WindowsBase.dll`（16,208 B）属于 `Microsoft.NETCore.App.Runtime.win-x64/10.0.14`，不是第二套界面框架。保留 x64 Windows App SDK WinUI/Runtime 2.7.0 的原生、XAML 和许可文件。
- 每个 app 自带一套 ExifTool、FFmpeg、FFprobe 及所需依赖。保留相机标签数据和 Perl Win32API 所需的 `cFile.pc`，没有按扩展名一刀切。未启用激进裁剪、AOT 或程序集合并；用户缓存、样片、测试工具不进入正式包。

**v2.0.1 部署实测**（相同机器；只有第一行是分发并验证的正式方案）：

| 部署 | 实测体积 | 选择依据 |
|---|---:|---|
| 不使用 MSIX 的自包含 x64 app + Inno | app 367,887,816 B；安装器 95,363,159 B | 按用户离线安装；独立 app 目录可直接运行；已实测安装/重装/卸载 |
| Framework-dependent 对照 | 230,964,064 B / 559 文件（运行目录） | 不含需另装的兼容 .NET 10 x64 / Windows App Runtime；未将依赖成本计算为零，未做完整运行门禁 |
| Single-file 自包含对照 | 366,647,220 B / 12 文件（发布目录） | 仍有外置 XAML/PRI 资源，且没有正式包的独立 `tools` 布局；不按已通过的便携单文件分发 |
| MSIX 对照 | 同一完整 app 加清单和三张标识后，MakeAppx 产出 148,095,806 B 安装包 | 未签名、未注册、未安装，仅比较体积；没有把 MSIX 与裸目录直接比较 |

## 功能验收及范围

- **文件夹封面：** 仅枚举当前层，最多取 4 张候选图，单图保留宽高比、多图使用 2×2；空目录、有不可读文件或无可解码图片时保留原图标，不递归寻找素材。封面组合缓存最多 32 项，生成并发最多 2；缓存键包含目录路径和修改时间、规格、子图路径/大小/时间，复用已有子图缩略图。窗口测试验证单图、拼图、缓存命中、内容更新失效与仅子层含图时回退。两个版本均分别测试了 **1000 个文件** 与 **1000 个不同文件夹**；后者每夹有一张同源合成 JPEG，不代表千份独立相机 RAW 的全量解码。
- **旧版限定：** WPF 窗口验证 9 次拖动分隔条、27 组尺寸、30 次布局切换、全屏、重启恢复、13 处虚拟滚动和右键外部程序的中文参数。真实 JPG、ARW、MP4 均有预览与字段；视频仍为旧版的首帧和流元数据，不把 WinUI 播放器列作旧版功能。[布局和媒体](docs/dual-release-evidence/legacy-layout.txt) · [千文件夹](docs/dual-release-evidence/legacy-1000-folders.txt)。
- **新版浏览器：** 窗口缩窄时受测卡片从头到尾是 150 × 155.5 DIP。自然名称（1/2/10）、创建/修改/拍摄时间、大小、类型这 6 项的 12 种方向及可关闭的文件夹优先规则均通过。拍摄时间只采用已解析的真实 `DateTimeOriginal`，按文件签名做 512 项有界缓存，未读取的时间明确排在同类末尾，不拿修改时间代替，不为了排序解析全目录。真实菜单选择“大小、倒序、关闭文件夹优先”后，检查设置文件，再重开 EXE 检查菜单，三项均恢复。三布局切换未使扫描数或解析数增加；另测了 13 次快速文件选择。[布局、排序和摘要](docs/dual-release-evidence/winui-layout-sort.json) · [排序重启实测](docs/dual-release-evidence/winui-sort-restart.json)。
- **数据完整性：** 对 `验证照片` 六份真实 JPEG、ARW、NEF、DNG、两种 MP4，两版都通过解析、中文路径、坏文件（部分结果而非成功）和 JSON/CSV 导出。WinUI 与 v1.2.2 基线逐字段对比：362 + 213 + 358 + 17,779 + 25,713 + 313 = **44,738 个字段**；基线、新版、JSON、CSV 数量相等，除系统访问时间 `FileAccessDate` 可变化外，字段语义差异 0。摘要实测 7 分类、71 字段，只改标题呈现，未合并 MakerNotes、来源、原始值或重复标签。另用已知坐标验证东北/西南、零坐标和无 GPS。[旧版六样本](docs/dual-release-evidence/legacy-six.txt) · [新版逐项对照](docs/dual-release-evidence/winui-six-parity.txt) · [GPS](docs/dual-release-evidence/winui-gps.json)。
- **预览、视频和外部打开：** 独立场景组的 JPEG 9504×6336、ARW 6272×4180、MP4 首帧 1600×900 在实际窗口显示（与上面的六份实样集不是同一组媒体）。WinUI 另验证竖 PNG、TIFF、DNG、NEF、50/100/200/400% 和旋转、视频播放/暂停/定位、多流样本 370 个字段及外部程序接收中文含空格路径。RAW 报告的是解码器实际给出的尺寸，不保证系统解码一定使用完整传感器像素。[媒体](docs/dual-release-evidence/winui-media.json) · [解码格式](docs/dual-release-evidence/winui-formats.json) · [多流](docs/dual-release-evidence/winui-multistream.json) · [外部打开](docs/dual-release-evidence/winui-external.json)。
- **并存与卸载：** 两个 Inno AppId、默认安装子目录和快捷方式均独立。设置、历史及缓存位于 `%LOCALAPPDATA%\EXIF查看器\Legacy-1.2.3` 与 `…\WinUI-2.0.1`；首次从旧位置复制可用设置/最近记录（新版也迁移已有卡片尺寸），不搬缓存，不改旧配置。两个安装器在中文含空格路径完成安装，逐文件 SHA-256 一致（767 + 984），同时运行；卸载旧版后，WinUI 继续响应且可重启。独立复验 WinUI 安装后从已安装 EXE 打开 JPG/ARW/MP4、同目录重装和卸载。测试前后七种扩展名的 Windows `UserChoice` 未变，测试安装均已卸载。[并存](docs/dual-release-evidence/dual-install.json) · [重装](docs/dual-release-evidence/winui-reinstall.json)。

## 性能、系统和未验证边界

2026-11-22，Windows 10 专业工作站版 22H2 x64（19045）。正式目录 EXE 分别连续空启动 5 次；使用隔离配置，不清空操作系统文件缓存。窗口句柄非零且 `Responding` 才计入可响应时间；这不是重启机器后的严格冷启动，也不是大图加载完毕。窗口出现后等 3 秒，再用约 1 秒采样空闲 CPU。两版 5/5 次采样均为 0%（全机归一）且无直接子进程，Explorer 均报告响应。可响应范围旧版 881.3–953.5 ms，新版 546.4–860.1 ms。对照历史 v1.2.1 的 901.2 ms 中位，WinUI 本轮更快；旧版差 10.3 ms，不能视为显著差异。v2.0.0 build.4 历史中位 597.8 ms，低于本轮 v2.0.1；测量轮次/采样条件不同，不能声称迭代一定加速。[旧版启动原表](docs/dual-release-evidence/legacy-startup.csv) · [新版启动原表](docs/dual-release-evidence/winui-startup.csv)。

大图切换单轮 UI 自动化计时（含控件轮询）：旧版 / 新版分别为首张 JPEG 2766 / 1878 ms、100% 缩放 872 / 53 ms、200% 缩放 734 / 100 ms、JPEG→ARW 6326 / 2618 ms。WinUI 枚举 1000 个文件 560 ms，扫描 1 次（13–30 个 UI 容器）；枚举 1000 个含封面图的文件夹 269 ms，扫描 1 次。该计时截至条目枚举完成，不是全部封面解码时间。[旧版预览](docs/dual-release-evidence/legacy-preview.json) · [新版预览](docs/dual-release-evidence/winui-preview.json) · [千文件](docs/dual-release-evidence/winui-1000-files.json) · [千文件夹](docs/dual-release-evidence/winui-1000-folders.json)。

两版分别保持一张 9504×6336 JPEG 打开约 66 秒，每约 11 秒取样。工作集首末为旧版 522.9→506.7 MiB，新版 695.8→693.8 MiB；两版应用及 Explorer 各 6/6 次有响应、采样时均无直接子进程。说明此场景短期能稳定，不证明长期无泄漏或原图解码有固定的内存上限。缓存有界，但原图分辨率仍决定实际内存。[旧版资源原表](docs/dual-release-evidence/legacy-stability.csv) · [新版资源原表](docs/dual-release-evidence/winui-stability.csv)。

WinUI 编译目标为 Windows SDK 26100，项目 API 下限 19041，正式安装器最低 19045；这三者不是同一概念。微软 [Windows App SDK 系统要求](https://learn.microsoft.com/windows/apps/windows-app-sdk/system-requirements)、[服务支持矩阵](https://learn.microsoft.com/windows/apps/windows-app-sdk/support)与 [.NET 10 操作系统列表](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)区分 API 可用性、系统版本及厂商服务支持；本机能运行并不是对所有 Win10 版本或微软支持状态的承诺。v1.2.3 不调整旧版兼容策略。

**未验：** 没有 Windows 11 实机结果，也没有逐台验证不同显卡/系统 RAW 或视频编解码器、实体横向滚轮/触控板、全部 DPI 组合。不能把构建、自动化缩放和拖分隔条当作上述硬件验收。要完成 Win11 验收，在干净的 Windows 11 x64 桌面测试账户，用 `release/2.0.1-winui3` 下的脚本、同一对安装包、场景样片（`sample.jpg`、`sample.ARW`、`sample.mp4`）及留存的 1.2.2 工具复验：

```powershell
.\tests\Test-Windows11Release.ps1 -LegacyRelease 'D:\发布\v1.2.3' -WinUIRelease 'D:\发布\v2.0.1' -ScenarioDirectory 'D:\场景样片' -BaselineTools 'D:\发布\v1.2.2\app\tools'
```

脚本首先检查是真正的 Win11 x64 客户端和相同哈希，再测试布局、排序持久化、媒体、双装及重装，成功后记录 OS build 与结果；Windows 10 上已验证它会拒绝生成 Win11 通过记录。
