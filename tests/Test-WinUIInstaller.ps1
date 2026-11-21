#requires -Version 7.4
param([string]$ReleaseDirectory=(Join-Path (Split-Path $PSScriptRoot) 'releases/v2.0.0'))
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$release=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
$setup=Get-Item -LiteralPath (Join-Path $release 'EXIF全媒体元数据查看器_v2.0.0_Setup.exe')
$exeName='EXIF全媒体元数据查看器_v2.0.0.exe'
$appId='{32AF6637-846F-40AC-ABAF-103524876ED8}_is1'
$uninstallRoots='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
$links='Programs','DesktopDirectory' | ForEach-Object { Join-Path ([Environment]::GetFolderPath($_)) 'MetaLens 2.lnk' }
foreach($target in @($uninstallRoots | ForEach-Object { Join-Path $_ $appId }) + $links){if(Test-Path -LiteralPath $target){throw "已有 v2 安装或快捷方式，不进行隔离安装测试：$target"}}
$run=Join-Path $repo ('artifacts/qa200/install-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $run
$testApp=Join-Path $run '中文 安装目录'
$program=Join-Path $testApp $exeName
function Associations {
    foreach ($ext in '.jpg','.jpeg','.png','.arw','.nef','.dng','.mp4') {
        $v=Get-ItemProperty -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice" -ErrorAction SilentlyContinue
        "$ext|$($v.ProgId)|$($v.Hash)"
    }
}
function RunInstaller($path,$arguments){
    $proc=Start-Process -FilePath $path -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if($proc.ExitCode -ne 0){throw "$path : $($proc.ExitCode)"}
}
function VerifyPayload {
    $source=Join-Path $release 'app'
    $files=@(Get-ChildItem -LiteralPath $source -File -Recurse)
    foreach($f in $files){
        $to=Join-Path $testApp ([IO.Path]::GetRelativePath($source,$f.FullName))
        if(!(Test-Path -LiteralPath $to -PathType Leaf) -or (Get-FileHash -LiteralPath $f.FullName).Hash -ne (Get-FileHash -LiteralPath $to).Hash){throw "安装内容不一致：$to"}
    }
    $files.Count
}
$before=@(Associations)
try {
    $argsForSetup=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/NOICONS','/MERGETASKS="!desktopicon"',('/DIR="'+$testApp+'"'))
    RunInstaller $setup.FullName ($argsForSetup + ('/LOG="'+(Join-Path $run 'install.log')+'"'))
    $fileCount=VerifyPayload
    & (Join-Path $PSScriptRoot 'Test-WinUIScenario.ps1') -Mode media -Executable $program -ArtifactDirectory $run
    RunInstaller $setup.FullName ($argsForSetup + ('/LOG="'+(Join-Path $run 'reinstall.log')+'"'))
    if((VerifyPayload) -ne $fileCount){throw '重装后内容有变化'}
} finally {
    $uninstaller=Join-Path $testApp 'unins000.exe'
    $registration=Get-ItemProperty -LiteralPath (Join-Path $uninstallRoots[0] $appId) -ErrorAction SilentlyContinue
    if($registration -and [IO.Path]::TrimEndingDirectorySeparator($registration.InstallLocation) -eq $testApp -and (Test-Path -LiteralPath $uninstaller -PathType Leaf)){
        RunInstaller $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $run 'uninstall.log')+'"'))
    }
}
if((Test-Path -LiteralPath $program) -or (Test-Path -LiteralPath (Join-Path $uninstallRoots[0] $appId)) -or @($links | Where-Object {Test-Path -LiteralPath $_}).Count){throw '隔离卸载后仍有应用、注册项或快捷方式'}
if(Compare-Object $before @(Associations)){throw '文件关联发生变化'}
[pscustomobject]@{passed=$true;installedFiles=$fileCount;reinstalled=$true;uninstalled=$true;userChoiceUnchanged=$true;setupHash=(Get-FileHash -LiteralPath $setup.FullName).Hash} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $run 'result.json')
