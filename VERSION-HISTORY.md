# 版本选择与更新记录

**1.x.x 是老 UI，使用 WPF；2.x.x 是新 UI，使用原生 WinUI 3 和 Windows App SDK。** 下表的链接打开该版本的源码快照。下载已发布的安装器，请在 [GitHub Releases](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/releases) 选择对应版本的 **Assets → Setup.exe**。源码标签和安装包分开发布；只有源码标签不代表安装包已上线，自动生成的 Source code (zip/tar.gz) 也不是安装包。

| 版本（源码标签） | 界面 | 这个版本更新了什么 |
| --- | --- | --- |
| [Ver1.1.0](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/Ver1.1.0) | 旧 UI · WPF | 加入交互式媒体浏览，支持滚轮缩放、拖动平移、旋转和全屏；提供 Windows x64 安装器。 |
| [v1.2.0](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v1.2.0) | 旧 UI · WPF | 加入资源管理器式目录树、地址栏、前进/后退及上级导航；可切换缩略图、列表、详情。普通图片支持真正的 100%/200% 像素查看；元数据面板改为中英双语。 |
| [v1.2.1](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v1.2.1) | 旧 UI · WPF | 修复断开的盘符导致启动等候约 22 秒的问题。目录分批显示，只生成可见区域附近的缩略图，并限制解码并发及缓存容量；不降低原图分辨率、不删减元数据字段。 |
| [v1.2.2](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v1.2.2) | 旧 UI · WPF | 新增原版、三栏、双层三种布局，分别记忆面板比例。修复文件卡片、缩略图显示问题；右键可单次使用外部程序打开，不改 Windows 默认关联。 |
| [v1.2.3](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v1.2.3) | **旧 UI · WPF 维护版** | 文件夹可显示单张预览或 2×2 封面，无图仍显示原图标。沿用旧布局、排序和摘要；独立安装与配置，可与 v2.0.1 并存。安装包比 v1.2.2 小 7.08%。 |
| [v2.0.0](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v2.0.0) | **新 UI · 首个原生 WinUI 3 版本** | 迁移浏览、预览及元数据界面，保留三种布局和完整 JSON/CSV 导出。新增视频播放、暂停、定位，修复 GPS 东经方向误判。正式安装器对应 **build.4**。 |
| [v2.0.1](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/tree/v2.0.1) | **新 UI · WinUI 3** | 增加文件夹封面；调窗口宽度只改变列数，不改变用户设定的卡片大小。新增名称、修改时间、创建时间、真实拍摄时间、大小、类型六项双向排序，记忆排序和文件夹优先设置。去除摘要重复标题；安装包比 v2.0.0 build.4 小 7.33%。 |

v2.0.1 的拍摄时间只使用已读取的元数据，没有时不拿文件修改时间代替。v1.2.3 / v2.0.1 的并存、卸载和媒体回归已在 Windows 10 22H2 x64 实测；Windows 11 尚未实机验收。这两份安装器均未签名。详见[双版本验收报告](https://github.com/FurryFGG/EXIF-Multimedia-Metadata-Viewer/blob/v2.0.1/DUAL_RELEASE_REPORT.md)。

## 校验安装包

在 PowerShell 运行 `Get-FileHash -Algorithm SHA256 -LiteralPath '安装包的完整路径'`，比对下表。大小仅供辨认，不代替哈希校验。

| 源码标签 | 安装器字节数 | SHA-256 |
| --- | ---: | --- |
| Ver1.1.0 | 102739352 | `3B24E36A19769923310584E8836CE7E938930A79D3BDBC17B51695938BD4F821` |
| v1.2.0 | 102759113 | `3EF714C896FCB3E4D99DEF6D68A4DC7DA11E45DCEF400A0B33397D7660ED6C36` |
| v1.2.1 | 101712363 | `AC42C7740DC0E8E87C85B0F9A2F9D8FB65296387D78318BBB43FAB59E1C59A0F` |
| v1.2.2 | 101449395 | `FE1C3282C8FE18462DAC2E5B1F905833AA1F8D80EE2EF18F93175C5723C910B6` |
| v1.2.3 | 94268492 | `309BAFACA557DAD7F7D38D9B2AAB84A4927301D4F7F089CA8E1188F732508479` |
| v2.0.0 (build.4) | 102907981 | `91FC98156A20D5B540343A4D3609B9300C63FB645A08E1537F1B2AA546405D0D` |
| v2.0.1 | 95363159 | `8E3CA7FF49911B09A078EB11EC71CC9996DD6B1CC2B92C6176F4700B0D33CD7E` |

## 查看或回退到旧版

要使用旧版程序，在 Releases 选择对应版本的安装器。要查看旧版源码，可在本地仓库运行以下命令；用一个尚不存在的新目录，不会改动当前工作区或覆盖未提交内容：

```powershell
git fetch origin --tags
git worktree add --detach ../inspect-legacy-v1.2.3 v1.2.3
# 或查看新版：git worktree add --detach ../inspect-winui-v2.0.1 v2.0.1
```
