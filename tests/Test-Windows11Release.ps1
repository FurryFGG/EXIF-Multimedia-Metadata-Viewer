#requires -Version 7.4
[CmdletBinding()]param(
  [Parameter(Mandatory)][string]$LegacyRelease,
  [Parameter(Mandatory)][string]$WinUIRelease,
  [Parameter(Mandatory)][string]$ScenarioDirectory,
  [Parameter(Mandatory)][string]$BaselineTools
)
$ErrorActionPreference='Stop'
$os=Get-CimInstance Win32_OperatingSystem
if ($os.ProductType -ne 1 -or [int]$os.BuildNumber -lt 22000 -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne [Runtime.InteropServices.Architecture]::X64) {throw '仅在 Windows 11 x64 客户端测试账户中运行；不能将其他系统的验证算作 Windows 11 结果。'}
$le=(Resolve-Path -LiteralPath $LegacyRelease).Path; $wi=(Resolve-Path -LiteralPath $WinUIRelease).Path
$samples=(Resolve-Path -LiteralPath $ScenarioDirectory).Path; $baseline=(Resolve-Path -LiteralPath $BaselineTools).Path
foreach($name in 'sample.jpg','sample.ARW','sample.mp4'){if(!(Test-Path -LiteralPath (Join-Path $samples $name) -PathType Leaf)){throw "缺少场景样片 $name"}}
foreach($item in @(@($le,'1.2.3','309BAFACA557DAD7F7D38D9B2AAB84A4927301D4F7F089CA8E1188F732508479'),@($wi,'2.0.1','8E3CA7FF49911B09A078EB11EC71CC9996DD6B1CC2B92C6176F4700B0D33CD7E'))){
  if((Get-FileHash -LiteralPath (Join-Path $item[0] ('EXIF查看器_v'+$item[1]+'_Setup.exe'))).Hash -ne $item[2]){throw "安装包不是受测的正式版本：$($item[1])"}
}
$exe=Join-Path $wi 'app/EXIF查看器_v2.0.1.exe'
$out=[IO.Directory]::CreateDirectory((Join-Path (Split-Path $PSScriptRoot) ('artifacts/win11-'+[guid]::NewGuid().ToString('N')))).FullName
foreach($mode in 'workspace','restore','media'){
    & (Join-Path $PSScriptRoot 'Test-WinUIScenario.ps1') -Mode $mode -Executable $exe -SampleDirectory $samples -BaselineTools $baseline -ArtifactDirectory $out
}
& (Join-Path $PSScriptRoot 'Test-SortPersistence.ps1') -Executable $exe
& (Join-Path $PSScriptRoot 'Test-DualInstall.ps1') -LegacyRelease $le -WinUIRelease $wi
& (Join-Path $PSScriptRoot 'Test-WinUIInstaller.ps1') -ReleaseDirectory $wi -SampleDirectory $samples
[pscustomobject]@{passed=$true;os=$os.Caption;version=$os.Version;build=$os.BuildNumber;architecture='x64';completed=(Get-Date -Format o);scenarios=@('workspace','restore','media','sort-restart','dual-install','reinstall')} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $out 'result.json')
