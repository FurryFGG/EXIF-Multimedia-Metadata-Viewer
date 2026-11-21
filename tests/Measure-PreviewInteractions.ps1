# Times observable changes in the two unmodified release EXEs (includes UIA polling).
param([Parameter(Mandatory)][string]$Executable,[Parameter(Mandatory)][string]$SampleDirectory,
    [Parameter(Mandatory)][string]$OutputPath,[switch]$WinUI)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
$exe=(Resolve-Path -LiteralPath $Executable).Path
$sample=(Resolve-Path -LiteralPath (Join-Path $SampleDirectory 'sample.jpg')).Path
$dir=[IO.Directory]::CreateDirectory((Split-Path ([IO.Path]::GetFullPath($OutputPath)))).FullName
$env:METALENS_PROFILE_DIR=Join-Path $dir ('interaction-'+[guid]::NewGuid().ToString('N'))
$desc=[System.Windows.Automation.TreeScope]::Descendants
$type=[System.Windows.Automation.AutomationElement]::ControlTypeProperty
$name=[System.Windows.Automation.AutomationElement]::NameProperty
function Cond($key,$val){[System.Windows.Automation.PropertyCondition]::new($key,$val)}
$isText=Cond $type ([System.Windows.Automation.ControlType]::Text)
$isButton=Cond $type ([System.Windows.Automation.ControlType]::Button)
function WaitText([string]$regex){
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    do {
        foreach($node in $root.FindAll($desc,$isText)) { if($node.Current.Name -match $regex){return $node.Current.Name} }
        Start-Sleep -Milliseconds 40
    } while([DateTime]::UtcNow -lt $deadline -and !$proc.HasExited)
    throw "No observable preview text matching $regex"
}
function ClickButton([string]$label){
    $cond=[System.Windows.Automation.AndCondition]::new($isButton,(Cond $name $label))
    $node=$root.FindFirst($desc,$cond)
    if(!$node){throw "Button missing: $label"}
    $node.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}
$clock=[Diagnostics.Stopwatch]::StartNew()
$proc=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -ArgumentList ('"'+$sample+'"') -PassThru -WindowStyle Hidden
try {
    do {Start-Sleep -Milliseconds 30; $proc.Refresh()} until($proc.MainWindowHandle -ne 0 -or $proc.HasExited -or $clock.Elapsed.TotalSeconds -gt 20)
    $root=[System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
    $jpg=WaitText '(?:完整图像解码|原图).*9,?504\s*×\s*6,?336'; $initial=$clock.ElapsedMilliseconds
    $clock.Restart(); ClickButton '100%'; $one=WaitText $(if($WinUI){'原图.*100\s*%'}else{'^100\s*%$'}); $zoom=$clock.ElapsedMilliseconds
    $clock.Restart(); ClickButton '200%'; $two=WaitText $(if($WinUI){'原图.*200\s*%'}else{'^200\s*%$'}); $zoom2=$clock.ElapsedMilliseconds
    $clock.Restart()
    if($WinUI){ClickButton '上一张'}else{
        $label=$root.FindFirst($desc,(Cond $name 'sample.ARW'))
        $walker=[System.Windows.Automation.TreeWalker]::ControlViewWalker
        while($label -and $label.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem){$label=$walker.GetParent($label)}
        if(!$label){throw 'RAW file item missing'}
        $label.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    }
    $raw=WaitText '(?:RAW|完整图像解码).*6,?272\s*×\s*4,?180'; $switched=$clock.ElapsedMilliseconds
    [pscustomobject]@{release=(Get-Item $exe).VersionInfo.ProductVersion; jpegPreviewMs=$initial;
        zoom100Ms=$zoom; zoom200Ms=$zoom2; jpegToRawMs=$switched; jpeg=$jpg; scale100=$one; scale200=$two; raw=$raw} |
        ConvertTo-Json | Tee-Object -FilePath $OutputPath
} finally { if(!$proc.HasExited){$null=$proc.CloseMainWindow();if(!$proc.WaitForExit(5000)){$proc.Kill($true)}}; $proc.Dispose() }
