param(
    [Parameter(Mandatory = $true)] [string]$Executable,
    [Parameter(Mandatory = $true)] [string]$MediaFile,
    [Parameter(Mandatory = $true)] [string]$OutputPath,
    [ValidateRange(2, 60)] [int]$SettleSeconds = 10,
    [string]$ListAutomationId = 'ThumbnailList'
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$executablePath = [IO.Path]::GetFullPath($Executable)
$mediaPath = [IO.Path]::GetFullPath($MediaFile)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $executablePath -ArgumentList ('"'+$mediaPath+'"') -PassThru
$windowMs = $null
$firstItemMs = $null
$thumbnailList = $null
$listItem = [System.Windows.Automation.PropertyCondition]::new(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
$deadline = [DateTime]::UtcNow.AddSeconds(45)
try {
    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0 -and $null -eq $windowMs) {
            $windowMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
        }
        if ($process.MainWindowHandle -ne 0 -and $null -eq $thumbnailList) {
            try {
                $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
                $condition = [System.Windows.Automation.PropertyCondition]::new(
                    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $ListAutomationId)
                $thumbnailList = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            } catch { }
        }
        if ($null -ne $thumbnailList) {
            try {
                $item = $thumbnailList.FindFirst(
                    [System.Windows.Automation.TreeScope]::Children,
                    $listItem)
                if ($null -ne $item) {
                    $firstItemMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
                    break
                }
            } catch { $thumbnailList = $null }
        }
        Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Seconds $SettleSeconds
    $process.Refresh()
    if ($null -eq $firstItemMs) { throw "No accessible media item in $ListAutomationId within deadline" }
    $realizedItems = if ($null -eq $thumbnailList) { 0 } else {
        $thumbnailList.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            $listItem).Count
    }
    $result = [pscustomobject]@{
        Executable = $executablePath
        MediaFile = $mediaPath
        MainWindowMs = $windowMs
        FirstBrowserItemMs = $firstItemMs
        AccessibleListItems = $realizedItems
        WorkingSetBytes = $process.WorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
        Threads = $process.Threads.Count
        Handles = $process.HandleCount
        Responding = $process.Responding
    }
    $directory = Split-Path -Parent $outputFile
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $result | Export-Csv -LiteralPath $outputFile -NoTypeInformation -Encoding UTF8
    $result | Format-List
} finally {
    if (-not $process.HasExited) {
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
    $process.Dispose()
}
