# Test Report

Test date: 2026-10-28

## Environment

- Windows 10 Pro for Workstations 22H2, build 19045, x64.
- .NET SDK 10.0.403.
- ExifTool 13.59 Windows x64.
- FFmpeg and FFprobe `n9.0.2-36-g6882969ce9-20261027`, Windows x64 shared build.
- Samples read from a local external directory; no sample files were copied into the repository.

## Results

- WPF Release x64 build: passed with zero warnings and zero errors.
- Integration-test Release build: passed with zero warnings and zero errors.
- Six supplied media samples: passed parser-output field-count comparisons and export/path checks.
- Sony `_SNA6816.ARW`: ExifTool returned 25,713 fields. MakerNotes/vendor fields, lens tags, embedded-preview tags and same-name tags from different groups were present.
- Nikon `DSC_3683.NEF`: ExifTool returned 17,779 fields.
- Sony `_SNA6816.jpg`: ExifTool returned 313 fields.
- DJI `DJI_20260929164914_0035_D.DNG`: ExifTool returned 213 fields.
- `C0211.MP4`: ExifTool returned 211 fields; FFprobe returned 151 flattened fields. The sample contains a 7680x4320 HEVC video stream, a 48 kHz two-channel PCM audio stream, and a data stream. Both average and nominal frame-rate fields were retained where returned.
- `DJI_20260929170441_0085_D.MP4`: ExifTool returned 164 fields; FFprobe returned 194 flattened fields. The FFprobe JSON leaf count matched the flattened field count.
- A file in a Chinese directory with a Chinese filename was parsed successfully by ExifTool.
- JSON export preserved the ExifTool source JSON structure and values. JSON and CSV exports preserved all application fields, including repeated entries; the CSV row count was checked with a quote-aware counter.
- Export-overwrite protection left the media file contents unchanged.
- An unsupported binary presented with a `.jpg` filename produced a partial parser result instead of an empty-result success.
- FFmpeg and FFprobe executables were run locally to verify their version, licensing output and build configuration.

## Post-install startup check (2026-10-29)

- The launch failure was traced to the read-only `RawOutput` property using WPF's default two-way TextBox binding. `MainWindow.xaml` now sets `Mode=OneWay`.
- The rebuilt setup package was installed to the default per-user path. The installed application remained responsive with its main window present; a separate launch with a JPEG argument also remained alive for more than 10 seconds.
- No application crash events were recorded after the earlier binding failures on 2026-10-28.
- Actual on-screen photo rendering and metadata rows were not confirmed because Windows screenshot capture timed out during verification.

## Not Verified Yet

- MOV, subtitle tracks, chapter metadata, and files with multiple audio streams were not present in the supplied samples.
- Playback, seeking, frame-by-frame diagnostics, and packet diagnostics are not implemented; video preview extracts one frame.
- Performance was not benchmarked on a large video collection. The supplied 8K MP4 was parsed for metadata, but preview decode timing was not recorded.
- Windows 11 was not separately tested.
- The post-fix application window process is confirmed alive, but photo rendering and displayed EXIF rows remain unverified; screenshot capture timed out during the visual check.
- ExifTool may recognize a severely truncated file from its header and return generic file fields without a corruption warning. The application reports the parser's result and diagnostics; it does not invent a corruption verdict.

## Phase 2 Verification (2026-10-29)

- Release x64 WPF build: passed with zero warnings and zero errors.
- Integration test executable: passed the built-in viewport geometry checks for portrait and rotated fitting, square fitting, cursor-anchored zoom, pan clamping, and images smaller than the viewport.
- Parser corruption check: passed. No external media sample directory was supplied in this run, so camera metadata and photo/video rendering against real samples were not retested.
- Self-contained `win-x64` publish completed. Inno Setup 6.7.3 produced `artifacts/installer/EXIF-Multimedia-Metadata-Viewer-Setup.exe` (102,739,352 bytes, version 1.1.0).
- The installed version's process was present at the default per-user install path. A screen capture of the built version could not be obtained: Windows Computer Use window capture timed out on both attempts, and the existing installed instance was left untouched. On-screen rendering and pointer/keyboard interactions therefore remain unverified in this run.
- Browser thumbnail decode is asynchronous and limited to 280 pixels wide; only realized virtualized thumbnail rows request a decode. Video thumbnails use the bundled FFmpeg first-frame path. Unsupported formats remain identified in the browser and are not reported as metadata-free media.
- Known limits: video playback/seeking remains out of scope; RAW preview depends on native WIC support or an embedded JPEG exposed by ExifTool. Horizontal mouse-wheel handling, DPI-specific fullscreen behavior, and real media interaction require a successful GUI verification pass.

## v1.2.0 Release Verification (2026-11-01)

### Build and automated regression

- Release `win-x64` build using the repository-local .NET SDK 10.0.403: passed with zero warnings and zero errors.
- Integration tests passed against all six supplied real media files in `D:\EXIF\验证照片`.
- Verified ExifTool results included 25,713 fields for the Sony ARW, 17,779 fields for the Nikon NEF, 313 fields for the Sony JPEG, and 213 fields for the DJI DNG.
- Verified video results included 211 ExifTool plus 151 FFprobe fields for `C0211.MP4`, and 164 ExifTool plus 194 FFprobe fields for `DJI_20260929170441_0085_D.MP4`.
- JSON/CSV export, repeated-tag preservation, Chinese path handling, overwrite protection, corrupt-file partial-result handling, and viewport geometry assertions passed.

### Explorer browser and GUI verification

- A real 100-photo folder was loaded from `D:\EXIF\.cache\EXIF-v1.2.0-100-photo-test-9b0757db38aa421c8e8a8ba167af9af9`; the application reported `100 项`.
- Folder-tree expansion and address-bar synchronization were confirmed through the live WPF accessibility tree.
- Natural filename ordering was confirmed as `IMG_1.jpg`, `IMG_2.jpg`, ... `IMG_9.jpg`, `IMG_10.jpg`, ... `IMG_100.jpg`.
- Thumbnail, list, and details mode controls plus the thumbnail-size slider were present and enabled in the live application.
- The real 9,504 × 6,336 Sony JPEG loaded with status `完整图像解码 · 9,504 × 6,336 像素`; 313 metadata fields, `SONY`, `ILCE-7RM5`, lens/exposure/time/IPTC/XMP values, legal zero values, and missing-value `无` rendering were confirmed.
- F11 entered the full-screen overlay and exposed the `退出全屏` control; Escape removed the overlay.
- The published EXE, not the development `bin` output, was launched with `D:\EXIF\验证照片\_SNA6816.jpg` and repeated the same full-resolution/313-field/ILCE-7RM5 smoke-test result.

### Release artifacts and overwrite protection

- Self-contained application: `releases\v1.2.0\app\EXIF全媒体元数据查看器_v1.2.0.exe`.
- Application file version: `1.2.0.0`.
- Application SHA-256: `5798B474B12400F075DDCD2B6B54E44D356AC23E511EEAE6AC70C3F81C15F1CF`.
- Installer: `releases\v1.2.0\EXIF全媒体元数据查看器_v1.2.0_Setup.exe` (102,759,113 bytes, ProductVersion 1.2.0).
- Installer SHA-256: `3EF714C896FCB3E4D99DEF6D68A4DC7DA11E45DCEF400A0B33397D7660ED6C36`.
- Re-running both release and installer builds was rejected before overwrite. The complete 923-file release manifest and installer hash remained unchanged.
- The original `D:\EXIF\EXIF-Multimedia-Metadata-Viewer Ver1.1.0-Setup.exe` remained 102,739,352 bytes with SHA-256 `3B24E36A19769923310584E8836CE7E938930A79D3BDBC17B51695938BD4F821`.

### Explicit verification limits

- Windows Graphics Capture continued to fail with `FrameArrived timed out`, and accessibility button clicks reported `coordinate input geometry is unavailable`.
- Therefore, splitter dragging, pointer-based multi-column visual inspection, and direct clicks on 100%, 200%, rotate, theme, list, and details buttons were not claimed as passed. Their WPF controls, bindings, `GridSplitter` constraints, and horizontal `WrapPanel` structure were code/accessibility verified only.
- The installer was built and inspected but was not installed over the existing v1.1.0 installation. Its Authenticode status is `NotSigned` because no code-signing certificate was supplied.
