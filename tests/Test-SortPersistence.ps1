#requires -Version 7.4
param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$exe=(Resolve-Path -LiteralPath $Executable).Path
$run=[IO.Directory]::CreateDirectory((Join-Path (Split-Path $PSScriptRoot) ('artifacts/sort-restart-'+[guid]::NewGuid().ToString('N')))).FullName
$profile=Join-Path $run 'profile'; $settings=Join-Path $profile 'viewer-settings.json'
function Launch {
    Start-Process -FilePath $exe -PassThru -Environment @{METALENS_PROFILE_DIR=$profile}
}
function FindNamed($proc,[string]$label) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,$label)
    do {
        $proc.Refresh(); if($proc.HasExited){throw 'Application exited'}
        $handle=$proc.MainWindowHandle
        if($handle -ne [IntPtr]::Zero){
            $window=[Windows.Automation.AutomationElement]::FromHandle($handle)
            $el=$window.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
            if($el){return $el}
        }; Start-Sleep -Milliseconds 100
    } while($clock.Elapsed.TotalSeconds -lt 8)
    throw "UIA element not found: $label"
}
function Pattern($el,$id){$obj=$null; if($el.TryGetCurrentPattern($id,[ref]$obj)){return $obj}}
function OpenMenu($proc){
    $menu=FindNamed $proc '☰'; $expand=Pattern $menu ([Windows.Automation.ExpandCollapsePattern]::Pattern)
    if($expand){$expand.Expand()}else{$menu.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()}
    $sort=FindNamed $proc '排序'; $sort.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
}
function Change($proc,$label){
    OpenMenu $proc; $el=FindNamed $proc $label
    $invoke=Pattern $el ([Windows.Automation.InvokePattern]::Pattern)
    if($invoke){$invoke.Invoke();return}
    $toggle=Pattern $el ([Windows.Automation.TogglePattern]::Pattern)
    if($toggle){$toggle.Toggle();return}
    $el.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
}
function Checked($el){
    $toggle=Pattern $el ([Windows.Automation.TogglePattern]::Pattern); if($toggle){return $toggle.Current.ToggleState -eq [Windows.Automation.ToggleState]::On}
    $el.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected
}
function CloseTest($proc){if($proc -and !$proc.HasExited){$null=$proc.CloseMainWindow(); if(!$proc.WaitForExit(4000)){$proc.Kill($true);$proc.WaitForExit()}}}
$p=Launch
try {
    foreach($label in '大小','倒序','文件夹优先'){Change $p $label; Start-Sleep -Milliseconds 300}
    Start-Sleep -Milliseconds 850
    $v=Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json
    if($v.BrowserSortMode -ne '大小' -or !$v.SortDescending -or $v.FoldersFirst){throw 'Saved sort mismatch'}
} finally {CloseTest $p}
$p=Launch
try {
    OpenMenu $p
    $size=Checked (FindNamed $p '大小'); $desc=Checked (FindNamed $p '倒序'); $folders=Checked (FindNamed $p '文件夹优先')
    if(!$size -or !$desc -or $folders){throw 'Relaunched menu mismatch'}
    [pscustomobject]@{passed=$true; field='大小'; descending=$desc; foldersFirst=$folders; verifiedWithRestart=$true} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $run 'result.json')
}finally{CloseTest $p}
