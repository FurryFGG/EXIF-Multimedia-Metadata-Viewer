#requires -Version 7.4
param(
    [Parameter(Mandatory)][ValidateSet('media','formats','gps','workspace','browser','restore','external')][string]$Mode,
    [string]$ProjectRoot=(Split-Path $PSScriptRoot), [string]$Executable,
    [string]$SampleDirectory, [string]$Directory, [string]$ArtifactDirectory,
    [string]$ExternalProgram, [string]$BaselineTools=$env:EXIF_QA_BASELINE_TOOLS
)
$ErrorActionPreference='Stop'
if (!$Executable) { $Executable=(Get-ChildItem (Join-Path $ProjectRoot 'src/MetaLens.WinUI/bin/Release') -Recurse -Filter '*v2.0.1.exe' | Select-Object -First 1).FullName }
if (!$SampleDirectory) { $SampleDirectory=Join-Path $ProjectRoot 'artifacts/integration-samples-v121' }
if (!$ArtifactDirectory) { $ArtifactDirectory=Join-Path $ProjectRoot 'artifacts/qa200' }
$artifact=[IO.Directory]::CreateDirectory($ArtifactDirectory).FullName
$output=Join-Path $artifact ("$Mode-"+[guid]::NewGuid().ToString('N')+'.json')
$exe=(Resolve-Path -LiteralPath $Executable).Path
$samples=(Resolve-Path -LiteralPath $SampleDirectory).Path
$folder=if ($Directory) { (Resolve-Path -LiteralPath $Directory).Path } else { $samples }
if (!$BaselineTools) { $BaselineTools=Join-Path $ProjectRoot 'releases/v1.2.2/app/tools' }
if ($Mode -eq 'workspace' -and !(Test-Path (Join-Path $BaselineTools 'exiftool/exiftool.exe'))) { throw 'Set EXIF_QA_BASELINE_TOOLS to the preserved v1.2.2 app/tools directory.' }
$envForTest=@{METALENS_PROFILE_DIR=(Join-Path $artifact 'test-profile'); METALENS_QA_OUTPUT=$output; METALENS_QA_MODE=$Mode; METALENS_QA_SAMPLES=$samples; METALENS_QA_BASELINE=$BaselineTools; METALENS_QA_HELPER=$ExternalProgram}
$p=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -ArgumentList ('--metalens-self-test "'+$folder+'"') -WindowStyle Hidden -Environment $envForTest -PassThru
try {
    if (!$p.WaitForExit(200000)) { throw '验收进程超时' }
    if ($p.ExitCode -ne 0) { throw "进程退出码 $($p.ExitCode)" }
    $res=Get-Content -LiteralPath $output -Raw -Encoding utf8 | ConvertFrom-Json
    if (@($res | Where-Object { $_.error }).Count -gt 0) { throw "运行失败：$output $($res.error)" }
    if ($Mode -eq 'media') {
        if ($res.Count -ne 3) { throw '三份样片未全部读取' }
        foreach ($item in $res) { if ($item.w -le 0 -or $item.h -le 0 -or [Math]::Abs($item.width-$item.w) -gt 1 -or [Math]::Abs($item.twice-2*$item.w) -gt 1 -or $item.Turn -ne 1) { throw "原像素 / 旋转失败：$output" } }
        $video=@($res | Where-Object f -eq 'sample.mp4')[0]
        if (!$video.paused -or $video.played -le 0 -or [Math]::Abs($video.seek-1) -gt 0.4) { throw "视频播放/暂停/跳转失败：$output" }
    } elseif ($res.passed -ne $true) { throw "验收未通过：$output" }
    Write-Output "PASS $Mode : $output"
} finally { if (!$p.HasExited) { $p.Kill($true) }; $p.Dispose() }
