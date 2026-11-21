# Size experiment only. Never registers a package, creates a certificate or modifies the formal release.
param([Parameter(Mandatory)][string]$AppDirectory,[Parameter(Mandatory)][string]$MakeAppx)
$ErrorActionPreference='Stop';$app=(Resolve-Path -LiteralPath $AppDirectory).Path
$root=[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot ('../../artifacts/compare/msix-'+[guid]::NewGuid().ToString('N')))).FullName
$stage=Join-Path $root 'app';Copy-Item -LiteralPath $app -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ComparisonManifest.xml') -Destination (Join-Path $stage 'AppxManifest.xml')
$icons=[IO.Directory]::CreateDirectory((Join-Path $stage 'ExperimentAssets')).FullName
foreach($size in 44,50,150){ & (Join-Path $app 'tools/ffmpeg/ffmpeg.exe') -hide_banner -loglevel error -n -f lavfi -i "color=c=gray:s=${size}x${size}" -frames:v 1 (Join-Path $icons "$size.png"); if($LASTEXITCODE){throw 'Test asset error'} }
$package=Join-Path $root 'compare-unsigned.msix'; & $MakeAppx pack /d $stage /p $package | Select-Object -Last 4;if($LASTEXITCODE){throw 'MSIX pack failed'}
Get-Item -LiteralPath $package | Select-Object FullName,Length
