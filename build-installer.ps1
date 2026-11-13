param(
    [string]$CandidateSuffix = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$versionFile = Join-Path $repoRoot "Directory.Build.props"
[xml]$versionProject = Get-Content -LiteralPath $versionFile
$version = [string]$versionProject.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid project Version: $version" }
if ($CandidateSuffix -and $CandidateSuffix -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$') {
    throw "CandidateSuffix may contain only letters, digits, dots and hyphens."
}

$releaseName = "v$version" + $(if ($CandidateSuffix) { "-$CandidateSuffix" } else { "" })
$releaseRoot = Join-Path $repoRoot "releases\$releaseName"
$publishPath = Join-Path $releaseRoot "app"
$exeName = "EXIF全媒体元数据查看器_v$version.exe"
$installerBaseName = "EXIF全媒体元数据查看器_v${version}_Setup"
$installerPath = Join-Path $releaseRoot "$installerBaseName.exe"
$scriptPath = Join-Path $repoRoot "installer\EXIF-Multimedia-Metadata-Viewer.iss"

if (-not $SkipPublish -and -not (Test-Path -LiteralPath $publishPath -PathType Container)) {
    & (Join-Path $repoRoot "build-release.ps1") -CandidateSuffix $CandidateSuffix
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
if (Test-Path -LiteralPath $installerPath) {
    throw "Installer already exists and will not be overwritten: $installerPath"
}

foreach ($requiredFile in @(
    (Join-Path $publishPath $exeName),
    (Join-Path $publishPath "tools\exiftool\exiftool.exe"),
    (Join-Path $publishPath "tools\ffmpeg\ffmpeg.exe"),
    (Join-Path $publishPath "tools\ffmpeg\ffprobe.exe"),
    (Join-Path $repoRoot "third_party\THIRD-PARTY-NOTICES.md"),
    $scriptPath
)) {
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
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $compilerPath = $candidate; break }
    }
}
if (-not $compilerPath) {
    $localCompiler = Get-ChildItem -LiteralPath (Join-Path $repoRoot ".tools\innosetup") -Filter "ISCC.exe" -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $localCompiler) { $compilerPath = $localCompiler.FullName }
}
if (-not $compilerPath) { throw "Inno Setup 6 is required." }

$env:EXIF_INSTALLER_VERSION = $version
$env:EXIF_PUBLISH_DIR = $publishPath
$env:EXIF_INSTALLER_OUTPUT_DIR = $releaseRoot
$env:EXIF_APP_EXE_NAME = $exeName
$env:EXIF_INSTALLER_BASE_NAME = $installerBaseName
& $compilerPath $scriptPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Inno Setup completed without producing the expected installer: $installerPath"
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
Add-Content -LiteralPath (Join-Path $releaseRoot "BUILD-INFO.md") -Value "`n- Installer: $([IO.Path]::GetFileName($installerPath))`n- Installer SHA-256: $hash" -Encoding UTF8
Write-Host "Installer created without overwriting history: $installerPath"
