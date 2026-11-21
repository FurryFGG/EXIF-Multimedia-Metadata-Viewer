#requires -Version 7.4
param([Parameter(Mandatory)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$app=(Resolve-Path -LiteralPath (Join-Path $ReleaseDirectory 'app')).Path
$repo=Split-Path $PSScriptRoot
$run=[IO.Directory]::CreateDirectory((Join-Path $repo ('artifacts/qa200/gps-'+[guid]::NewGuid().ToString('N')))).FullName
$files=[IO.Directory]::CreateDirectory((Join-Path $run 'synthetic')).FullName
$plain=Join-Path $files 'plain.jpg'
# Only newly generated images receive test metadata; never take a user media path as an argument.
& "$app/tools/ffmpeg/ffmpeg.exe" -v error -n -f lavfi -i 'color=c=blue:s=160x120:d=1' -frames:v 1 $plain
if($LASTEXITCODE){throw 'Could not generate the GPS test image'}
foreach($entry in @(@('ne','12 30 0','N','30 15 0','E'),@('sw','12 30 0','S','30 15 0','W'),@('zero','0','S','0','W'))){
    & "$app/tools/exiftool/exiftool.exe" -o (Join-Path $files ($entry[0]+'.jpg')) "-GPSLatitude=$($entry[1])" "-GPSLatitudeRef=$($entry[2])" "-GPSLongitude=$($entry[3])" "-GPSLongitudeRef=$($entry[4])" $plain
    if($LASTEXITCODE){throw 'Could not populate synthetic coordinates'}
}
& "$PSScriptRoot/Test-WinUIScenario.ps1" -Mode gps -Executable (Join-Path $app 'EXIF全媒体元数据查看器_v2.0.0.exe') -SampleDirectory $files -ArtifactDirectory $run
