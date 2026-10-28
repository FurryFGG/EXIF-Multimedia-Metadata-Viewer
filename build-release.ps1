param(
    [string]$OutputDirectory = "artifacts\publish\win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$localSdk = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $localSdk)) {
    $localSdk = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "..\.tools\dotnet\dotnet.exe"))
}

if (Test-Path -LiteralPath $localSdk) {
    $dotnetPath = $localSdk
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) {
        throw ".NET 10 SDK is required. Install the x64 SDK, then rerun this script."
    }
    $sdkList = & $dotnetCommand.Source --list-sdks
    if (-not ($sdkList | Where-Object { $_ -match '^10\.' })) {
        throw ".NET 10 SDK is required. Install the x64 SDK, then rerun this script."
    }
    $dotnetPath = $dotnetCommand.Source
}

$exifTool = Join-Path $repoRoot "third_party\exiftool\exiftool.exe"
$ffmpeg = Join-Path $repoRoot "third_party\ffmpeg\bin\ffmpeg.exe"
$ffprobe = Join-Path $repoRoot "third_party\ffmpeg\bin\ffprobe.exe"
foreach ($requiredFile in @($exifTool, $ffmpeg, $ffprobe)) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Required bundled dependency is missing: $requiredFile"
    }
}

$projectFile = Join-Path $repoRoot "src\ExifMultimediaMetadataViewer\ExifMultimediaMetadataViewer.csproj"
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
& $dotnetPath publish $projectFile -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o $outputPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Release output: $outputPath"
