param([Parameter(Mandatory)][string]$Executable, [string]$ExpectedTitle='MetaLens')
$ErrorActionPreference='Stop'
$exe=(Resolve-Path -LiteralPath $Executable).Path
$env:METALENS_PROFILE_DIR=Join-Path ([IO.Path]::GetTempPath()) ('MetaLens-QA-'+[guid]::NewGuid())
$p=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
try {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    do {Start-Sleep -Milliseconds 100; $p.Refresh()} until($p.HasExited -or ($p.MainWindowHandle -ne 0 -and $p.MainWindowTitle -like "*$ExpectedTitle*") -or $timer.Elapsed.TotalSeconds -ge 20)
    if($p.HasExited -or $p.MainWindowHandle -eq 0 -or !$p.Responding -or $p.MainWindowTitle -notlike "*$ExpectedTitle*"){throw "窗口启动失败: exit=$($p.ExitCode), HWND=$($p.MainWindowHandle), title=$($p.MainWindowTitle)"}
    [pscustomobject]@{Executable=$exe; Window=$p.MainWindowTitle; HWND=$p.MainWindowHandle; Responding=$p.Responding; FirstWindowMs=$timer.ElapsedMilliseconds}
} finally { if(!$p.HasExited){$null=$p.CloseMainWindow(); if(!$p.WaitForExit(5000)){$p.Kill()}} }
