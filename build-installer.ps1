param(
    [string]$Version = "1.0.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$projectVersionPattern = '^\d+(\.\d+){1,3}$'
if ($Version -notmatch $projectVersionPattern) {
    throw "Version must contain two to four numeric components, for example 1.0.0."
}

$publishPath = Join-Path $repoRoot "artifacts\publish\win-x64"
$outputPath = Join-Path $repoRoot "artifacts\installer"
$scriptPath = Join-Path $repoRoot "installer\EXIF-Multimedia-Metadata-Viewer.iss"

if (-not $SkipPublish) {
    & (Join-Path $repoRoot "build-release.ps1")
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$requiredFiles = @(
    (Join-Path $publishPath "EXIF Multimedia Metadata Viewer.exe"),
    (Join-Path $publishPath "tools\exiftool\exiftool.exe"),
    (Join-Path $publishPath "tools\ffmpeg\ffmpeg.exe"),
    (Join-Path $publishPath "tools\ffmpeg\ffprobe.exe"),
    (Join-Path $publishPath "tools\ffmpeg\LICENSE-LGPL-3.0.txt"),
    (Join-Path $publishPath "tools\exiftool\exiftool_files\LICENSE"),
    (Join-Path $repoRoot "third_party\THIRD-PARTY-NOTICES.md"),
    $scriptPath
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required installer input is missing: $requiredFile"
    }
}

$compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$compilerPath = if ($null -ne $compilerCommand) { $compilerCommand.Source } else { $null }
if (-not $compilerPath) {
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $compilerPath = $candidate
            break
        }
    }
}
if (-not $compilerPath) {
    $localCompiler = Get-ChildItem -LiteralPath (Join-Path $repoRoot ".tools\innosetup") -Filter "ISCC.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $localCompiler) {
        $compilerPath = $localCompiler.FullName
    }
}
if (-not $compilerPath) {
    throw "Inno Setup 6 is required. Install it with: winget install --id JRSoftware.InnoSetup -e"
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$env:EXIF_INSTALLER_VERSION = $Version
$env:EXIF_PUBLISH_DIR = $publishPath
$env:EXIF_INSTALLER_OUTPUT_DIR = $outputPath
& $compilerPath $scriptPath
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$installer = Join-Path $outputPath "EXIF-Multimedia-Metadata-Viewer-Setup.exe"
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Inno Setup completed without producing the expected installer: $installer"
}

Write-Host "Installer created: $installer"
