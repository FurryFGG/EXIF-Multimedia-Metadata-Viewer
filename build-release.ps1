param(
    [string]$CandidateSuffix = "",
    [string]$SampleDirectory = "",
    [string]$ScenarioDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$projectFile = Join-Path $repoRoot "src\MetaLens.WinUI\MetaLens.WinUI.csproj"
$versionFile = Join-Path $repoRoot 'Directory.Build.props'
[xml]$versionProject = Get-Content -LiteralPath $versionFile
$version = [string]$versionProject.Project.PropertyGroup.MetaLensVersion
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
    $testProject = Join-Path $repoRoot "tests\ExifMetadata.IntegrationTests\ExifMetadata.IntegrationTests.csproj"
    $sampleArgs = @()
    if ($SampleDirectory) { $sampleArgs = @((Resolve-Path -LiteralPath $SampleDirectory).Path) }
    & $dotnetPath run --project $testProject -c Release -- $repoRoot @sampleArgs
    if ($LASTEXITCODE -ne 0) { throw "Integration tests failed; release was not created." }

    & $dotnetPath publish $projectFile -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=false -p:PublishTrimmed=false -o $stagingPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $stagedExe = Join-Path $stagingPath $exeName
    $assets=Get-Content -LiteralPath (Join-Path (Split-Path $projectFile) 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach ($lib in $assets.libraries.Values | Where-Object { $_.type -eq 'package' -and $_.path -match '^(microsoft\.netcore\.app\.runtime\.win-x64|microsoft\.windowsappsdk\.(?!ddlm)|microsoft\.windows\.sdk\.net\.ref|microsoft\.web\.webview2|microsoft\.windows\.cswinrt)' -and $_.path -notmatch 'buildtools' }) {
        $source=$assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $lib.path } | Where-Object { Test-Path $_ -PathType Container } | Select-Object -First 1
        $notices=Get-ChildItem -LiteralPath $source -File | Where-Object Name -match '^(license|notice|third-party-notices|thirdpartynotices)(\..*)?$'
        if ($notices) { $dest=Join-Path $stagingPath ('licenses/' + $lib.path); [IO.Directory]::CreateDirectory($dest) | Out-Null; $notices | Copy-Item -Destination $dest }
    }
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

    if ($SampleDirectory) {
        & $dotnetPath run --no-build --project $testProject -c Release -- $repoRoot @sampleArgs (Join-Path $stagingPath "tools")
        if ($LASTEXITCODE -ne 0) { throw "Integration tests against packaged tools failed." }
    }
    foreach ($file in 'App.xbf','MainWindow.xbf','PreviewView.xbf','MetadataView.xbf','ExplorerView.xbf','resources.pri','Microsoft.UI.Xaml.dll','licenses/WindowsAppSDK.WinUI/license.txt','licenses/WindowsAppSDK.Runtime/NOTICE.txt') {
        if (!(Test-Path -LiteralPath (Join-Path $stagingPath $file) -PathType Leaf)) { throw "Missing WinUI resource: $file" }
    }
    if ($ScenarioDirectory -or $SampleDirectory) {
        $qaDir = Join-Path $repoRoot ('artifacts/qa200/publish-' + [IO.Path]::GetFileName($stagingPath))
        $uiSamples = if ($ScenarioDirectory) { (Resolve-Path -LiteralPath $ScenarioDirectory).Path } else { $sampleArgs[0] }
        foreach ($mode in 'media','workspace','restore') {
            & (Join-Path $repoRoot 'tests/Test-WinUIScenario.ps1') -Executable $stagedExe -SampleDirectory $uiSamples -Mode $mode -ArtifactDirectory $qaDir
        }
    }
    $smokeProcess = Start-Process -FilePath $stagedExe -WindowStyle Hidden -Environment @{METALENS_PROFILE_DIR=(Join-Path $repoRoot 'artifacts/qa200/smoke-profile')} -PassThru
    $smokeDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $smokeDeadline -and -not $smokeProcess.HasExited) {
        $smokeProcess.Refresh()
        if ($smokeProcess.MainWindowHandle -ne 0 -and $smokeProcess.Responding) { break }
        Start-Sleep -Milliseconds 50
    }
    $smokeProcess.Refresh()
    if ($smokeProcess.HasExited -or $smokeProcess.MainWindowHandle -eq 0 -or -not $smokeProcess.Responding) {
        if (-not $smokeProcess.HasExited) { Stop-Process -Id $smokeProcess.Id }
        throw "Published EXE did not create a responding main window within 10 seconds."
    }
    if (-not $smokeProcess.CloseMainWindow() -or -not $smokeProcess.WaitForExit(5000)) {
        Stop-Process -Id $smokeProcess.Id
    }
    $smokeProcess.Dispose()

    New-Item -ItemType Directory -Path $releaseRoot | Out-Null
    $stageFull = (Resolve-Path -LiteralPath $stagingPath).Path
    if ([IO.Path]::GetDirectoryName($stageFull) -ne (Resolve-Path -LiteralPath $stagingParent).Path -or [IO.Path]::GetFileName($stageFull) -notmatch '^[a-f0-9]{32}$' -or [IO.Path]::GetDirectoryName($appOutput) -ne (Resolve-Path -LiteralPath $releaseRoot).Path) { throw 'Unexpected release target' }
    Move-Item -LiteralPath $stageFull -Destination $appOutput
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
        $resolvedStage = (Resolve-Path -LiteralPath $stagingPath).Path
        if ([IO.Path]::GetDirectoryName($resolvedStage) -ne (Resolve-Path -LiteralPath $stagingParent).Path -or [IO.Path]::GetFileName($resolvedStage) -notmatch '^[a-f0-9]{32}$') { throw "Unsafe staging path: $resolvedStage" }
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
