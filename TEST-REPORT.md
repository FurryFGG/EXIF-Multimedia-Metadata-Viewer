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
