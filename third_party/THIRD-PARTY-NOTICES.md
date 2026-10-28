# Third-party dependencies

The application launches these tools as separate local processes. It does not link them into the .NET application.

## ExifTool

- Version: 13.59, Windows x64 distribution supplied in the workspace.
- Files: exiftool.exe and the complete exiftool_files directory.
- Purpose: broad image, RAW, video-container, EXIF, MakerNotes, GPS, IPTC, XMP, ICC and other metadata extraction.
- License: ExifTool is distributed under the same terms as Perl (GNU GPL or Artistic License). The bundled Windows distribution includes its license material in exiftool_files\LICENSE and exiftool_files\Licenses_Strawberry_Perl.zip.
- Packaging: the executable and its exiftool_files directory are copied beside the published app under tools\exiftool.
- Source and project: https://exiftool.org/

## FFmpeg / FFprobe

- Version: `n9.0.2-36-g6882969ce9-20261027`, Windows x64, `lgpl-shared` build from BtbN/FFmpeg-Builds.
- Purpose: FFprobe reads container, stream, chapter and program metadata. FFmpeg decodes one preview frame for video files.
- License: `ffmpeg.exe -L` reports LGPL-3.0-or-later. The original package license is included as `LICENSE-LGPL-3.0.txt`. The executables and shared DLLs remain separate files under `third_party\ffmpeg\bin`; they are launched as independent processes, not linked into the .NET app. The recorded configuration lists enabled external integrations; their upstream terms also apply.
- Provenance: release asset `ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip`, asset id `682570576`, published 2026-10-27 and retrieved 2026-10-28. SHA-256: `eb01b7296e09a8dfeab550bfccd54a6a4565b5601c0b0eb6f8ca3924ccf61daa`. The observed executable version and complete configure flags are recorded in `ffmpeg-build-info.txt`.
- Build project: https://github.com/BtbN/FFmpeg-Builds
- FFmpeg licensing information: https://ffmpeg.org/legal.html

## .NET

The application targets .NET 10 WPF and is published self-contained for win-x64. The .NET runtime is not copied into this source repository; it is included in the release output produced by build-release.ps1. .NET runtime licensing is provided by Microsoft with the runtime distribution.
