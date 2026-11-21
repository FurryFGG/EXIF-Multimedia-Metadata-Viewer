#requires -Version 7.4
param([string]$ReleaseDirectory='releases/v2.0.1', [string]$Dotnet='dotnet', [string]$ScenarioDirectory='artifacts/integration-samples-v121')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$release=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
$run=[IO.Directory]::CreateDirectory((Join-Path $repo ('artifacts/qa200/multistream-'+[guid]::NewGuid().ToString('N')))).FullName
$media=[IO.Directory]::CreateDirectory((Join-Path $run 'video')).FullName
$video=Join-Path $media 'sample.mp4'
$tools=Join-Path $release 'app/tools'
# Short local fixture, one picture track, two distinguishable AAC tones, one timed text track.
"1`n00:00:00,100 --> 00:00:02,500`nMetaLens subtitle test`n" | & "$tools/ffmpeg/ffmpeg.exe" -hide_banner -loglevel error -n -f lavfi -i 'testsrc2=size=320x180:rate=15:duration=4' -f lavfi -i 'sine=frequency=440:duration=4' -f lavfi -i 'sine=frequency=660:duration=4' -f srt -i 'pipe:0' -map 0:v -map 1:a -map 2:a -map 3:s -c:v mpeg4 -c:a aac -c:s mov_text -metadata:s:a:0 language=eng -metadata:s:a:1 language=zho -metadata:s:s:0 language=eng $video
if($LASTEXITCODE -ne 0){throw 'Could not construct multi-stream fixture'}
$streams=(& "$tools/ffmpeg/ffprobe.exe" -v error -show_entries stream=index,codec_type,codec_name:stream_tags=language -of json $video | Out-String | ConvertFrom-Json).streams
if($LASTEXITCODE -ne 0 -or ($streams.codec_type -join '/') -ne 'video/audio/audio/subtitle'){ throw 'Fixture stream layout mismatch' }
$baseline=if ($env:EXIF_QA_BASELINE_TOOLS) { $env:EXIF_QA_BASELINE_TOOLS } else { Join-Path $repo 'releases/v1.2.2/app/tools' }
& $Dotnet run -c Release --project (Join-Path $repo 'tests/ExifMetadata.IntegrationTests') -- $repo $media $tools $baseline | Tee-Object -FilePath (Join-Path $run 'parity.txt')
if($LASTEXITCODE -ne 0){throw 'Cross-version streams/export check failed'}
foreach($name in 'sample.jpg','sample.ARW'){ Copy-Item -LiteralPath (Join-Path $ScenarioDirectory $name) -Destination $media }
$exe=(Get-Item -LiteralPath (Join-Path $release 'app/EXIF查看器_v2.0.1.exe')).FullName
& "$PSScriptRoot/Test-WinUIScenario.ps1" -Mode media -Executable $exe -SampleDirectory $media -ArtifactDirectory $run
& "$PSScriptRoot/Test-WinUIScenario.ps1" -Mode workspace -Executable $exe -SampleDirectory $media -ArtifactDirectory $run
$streams | Format-Table index,codec_type,codec_name
Write-Output "PASS: stream metadata and export, real WinUI frame, playback, pause and seek. Artifacts: $run"
