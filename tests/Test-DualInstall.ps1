#requires -Version 7.4
param([Parameter(Mandatory)][string]$LegacyRelease,[Parameter(Mandatory)][string]$WinUIRelease)
$ErrorActionPreference='Stop'
$run=[IO.Directory]::CreateDirectory((Join-Path (Split-Path $PSScriptRoot) ('artifacts/dual-install/'+[guid]::NewGuid().ToString('N')))).FullName
$versions=@(
    @{v='1.2.3';src=$LegacyRelease;id='{94DCE9A2-3890-4930-8573-127760A123AA}_is1'},
    @{v='2.0.1';src=$WinUIRelease;id='{6AD6DD3D-9E20-4693-9B36-DC1200201ABA}_is1'})
$registry='Software\Microsoft\Windows\CurrentVersion\Uninstall'
function Associations {foreach ($ext in '.jpg','.jpeg','.png','.arw','.nef','.dng','.mp4') {$v=Get-ItemProperty -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$ext\UserChoice" -ErrorAction SilentlyContinue; "$ext|$($v.ProgId)|$($v.Hash)"}}
function RunSetup($exe,$params) { $p=Start-Process -FilePath $exe -ArgumentList $params -Wait -PassThru -WindowStyle Hidden; if($p.ExitCode -ne 0){throw "Installer exited $($p.ExitCode)"} }
function Uninstall($item) {
    $reg=Get-ItemProperty -LiteralPath $item.key -ErrorAction SilentlyContinue
    if(!$reg){return}
    if([IO.Path]::TrimEndingDirectorySeparator($reg.InstallLocation) -ne $item.dir){throw 'Not a disposable installation'}
    RunSetup (Join-Path $item.dir 'unins000.exe') @('/VERYSILENT','/NORESTART','/SUPPRESSMSGBOXES',('/LOG="'+$run+'/uninstall-'+$item.v+'.log"'))
    if((Test-Path -LiteralPath $item.key) -or (Test-Path -LiteralPath $item.exe)){throw 'Uninstall incomplete'}
}
function Launch($item) {
    $p=Start-Process -FilePath $item.exe -PassThru -WindowStyle Hidden -WorkingDirectory $item.dir -Environment @{METALENS_PROFILE_DIR=(Join-Path $run ('profile-'+$item.v))}
    try{$timer=[Diagnostics.Stopwatch]::StartNew();do{$p.Refresh();if($p.MainWindowHandle -and $p.Responding -and $p.MainWindowTitle -like '*EXIF查看器*'){return $p};Start-Sleep -Milliseconds 50}while(!$p.HasExited -and $timer.Elapsed.TotalSeconds -lt 15);throw 'No responding application window'}catch{if(!$p.HasExited){$p.Kill()};throw}
}
function CloseQa($p){if($p -and !$p.HasExited){if(!$p.CloseMainWindow() -or !$p.WaitForExit(5000)){$p.Kill();$p.WaitForExit()}}}
foreach($item in $versions){
    $item.dir=Join-Path $run ('中文 安装 '+$item.v);$name='EXIF查看器_v'+$item.v;$item.exe=Join-Path $item.dir ($name+'.exe');$item.setup=(Get-Item -LiteralPath (Join-Path $item.src ($name+'_Setup.exe'))).FullName
    $item.key=Join-Path "HKCU:\$registry" $item.id
    foreach($key in $item.key,(Join-Path "HKLM:\$registry" $item.id),(Join-Path 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall' $item.id)) {if(Test-Path -LiteralPath $key){throw "Found existing installation: $key"}}
    foreach($folder in 'Programs','DesktopDirectory'){if(Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath($folder)) ('EXIF查看器 v'+$item.v+'.lnk'))){throw 'Existing shortcut; refusing to touch user installation'}}
}
$before=@(Associations); $procs=@();$counts=@();$attempted=@()
try {
    foreach($item in $versions){
        $attempted+=$item; RunSetup $item.setup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/NOICONS','/MERGETASKS="!desktopicon"',('/DIR="'+$item.dir+'"'),('/LOG="'+$run+'/install-'+$item.v+'.log"'))
        $source=(Resolve-Path -LiteralPath (Join-Path $item.src 'app')).Path;$files=@(Get-ChildItem -LiteralPath $source -File -Recurse);foreach($f in $files){$dst=Join-Path $item.dir ([IO.Path]::GetRelativePath($source,$f.FullName));if((Get-FileHash -LiteralPath $dst).Hash -ne (Get-FileHash -LiteralPath $f.FullName).Hash){throw "Bad installed payload: $dst"}};$counts+=$files.Count
        $procs+=Launch $item
    }
    if(@($procs|Where-Object {$_.HasExited -or !$_.Responding}).Count){throw 'Coexistence failed'}
    CloseQa $procs[0];Uninstall $versions[0];if($procs[1].HasExited -or !$procs[1].Responding){throw 'Legacy uninstall disrupted WinUI'};CloseQa $procs[1];$after=Launch $versions[1];$procs+=$after;CloseQa $after
}finally{foreach($p in $procs){CloseQa $p};foreach($item in $attempted){Uninstall $item}}
if(Compare-Object $before @(Associations)){throw 'Default associations changed'}
[pscustomobject]@{passed=$true;versions=@($versions.v);installedFileHashesChecked=$counts;simultaneousWindows=$true;winuiRelaunchedAfterLegacyUninstall=$true;bothUninstalled=$true;userChoiceUnchanged=$true} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $run 'result.json')
