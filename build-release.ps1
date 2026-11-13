param(
    [string]$CandidateSuffix = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$projectFile = Join-Path $repoRoot "src\ExifMultimediaMetadataViewer\ExifMultimediaMetadataViewer.csproj"
$versionFile = Join-Path $repoRoot "Directory.Build.props"
[xml]$versionProject = Get-Content -LiteralPath $versionFile
$version = [string]$versionProject.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "The project Version must use semantic version format, for example 1.2.0."
}
if ($CandidateSuffix -and $CandidateSuffix -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]*$') {
    throw "CandidateSuffix may contain only letters, digits, dots and hyphens."
}

$releaseName = "v$version" + $(if ($CandidateSuffix) { "-$CandidateSuffix" } else { "" })
$releaseRoot = Join-Path $repoRoot "releases\$releaseName"
$appOutput = Join-Path $releaseRoot "app"
$exeName = "EXIF全媒体元数据查看器_v$version.exe"
if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release already exists and will not be overwritten: $releaseRoot. Use -CandidateSuffix with a new value for an explicit candidate build."
}

$localSdk = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $localSdk)) {
    $localSdk = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "..\.tools\dotnet\dotnet.exe"))
}
if (Test-Path -LiteralPath $localSdk) {
    $dotnetPath = $localSdk
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) { throw ".NET 10 SDK is required." }
    $dotnetPath = $dotnetCommand.Source
}

foreach ($requiredFile in @(
    (Join-Path $repoRoot "third_party\exiftool\exiftool.exe"),
    (Join-Path $repoRoot "third_party\ffmpeg\bin\ffmpeg.exe"),
    (Join-Path $repoRoot "third_party\ffmpeg\bin\ffprobe.exe")
)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required bundled dependency is missing: $requiredFile"
    }
}

$stagingParent = Join-Path $repoRoot "artifacts\staging"
$stagingPath = Join-Path $stagingParent ([Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
try {
    & $dotnetPath publish $projectFile -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -o $stagingPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $stagedExe = Join-Path $stagingPath $exeName
    foreach ($requiredOutput in @(
        $stagedExe,
        (Join-Path $stagingPath "tools\exiftool\exiftool.exe"),
        (Join-Path $stagingPath "tools\ffmpeg\ffmpeg.exe"),
        (Join-Path $stagingPath "tools\ffmpeg\ffprobe.exe")
    )) {
        if (-not (Test-Path -LiteralPath $requiredOutput -PathType Leaf)) {
            throw "Publish validation failed; missing output: $requiredOutput"
        }
    }

    $versionInfo = (Get-Item -LiteralPath $stagedExe).VersionInfo
    if ($versionInfo.FileVersion -notlike "$version*") {
        throw "Published EXE file version '$($versionInfo.FileVersion)' does not match project version '$version'."
    }

    New-Item -ItemType Directory -Path $releaseRoot | Out-Null
    Move-Item -LiteralPath $stagingPath -Destination $appOutput
    $hash = (Get-FileHash -LiteralPath (Join-Path $appOutput $exeName) -Algorithm SHA256).Hash
    $buildInfo = @"
# EXIF 全媒体元数据查看器 $releaseName

- Version: $version
- Built: $([DateTimeOffset]::Now.ToString("yyyy-MM-dd HH:mm:ss zzz"))
- Runtime: win-x64 self-contained
- Main EXE: app\$exeName
- SHA-256: $hash
- Source commit: $(git -C $repoRoot rev-parse HEAD)
"@
    Set-Content -LiteralPath (Join-Path $releaseRoot "BUILD-INFO.md") -Value $buildInfo -Encoding UTF8
    Write-Host "Release created without overwriting history: $releaseRoot"
    Write-Host "Main EXE: $(Join-Path $appOutput $exeName)"
} finally {
    if (Test-Path -LiteralPath $stagingPath) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}
