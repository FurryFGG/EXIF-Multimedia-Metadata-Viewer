param(
    [Parameter(Mandatory = $true)] [string]$Executable,
    [Parameter(Mandatory = $true)] [string]$MediaFile,
    [Parameter(Mandatory = $true)] [string]$OutputPath,
    [ValidateRange(2, 60)] [int]$SettleSeconds = 10
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$executablePath = [IO.Path]::GetFullPath($Executable)
$mediaPath = [IO.Path]::GetFullPath($MediaFile)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$timer = [Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $executablePath -ArgumentList @($mediaPath) -PassThru
$windowMs = $null
$firstItemMs = $null
$thumbnailList = $null
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
                    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "ThumbnailList")
                $thumbnailList = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            } catch { }
        }
        if ($null -ne $thumbnailList) {
            try {
                $children = $thumbnailList.FindAll(
                    [System.Windows.Automation.TreeScope]::Children,
                    [System.Windows.Automation.Condition]::TrueCondition)
                if ($children.Count -gt 0) {
                    $firstItemMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
                    break
                }
            } catch { $thumbnailList = $null }
        }
        Start-Sleep -Milliseconds 25
    }
    Start-Sleep -Seconds $SettleSeconds
    $process.Refresh()
    $realizedItems = if ($null -eq $thumbnailList) { 0 } else {
        $thumbnailList.FindAll(
            [System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition).Count
    }
    $result = [pscustomobject]@{
        Executable = $executablePath
        MediaFile = $mediaPath
        MainWindowMs = $windowMs
        FirstBrowserItemMs = $firstItemMs
        RealizedThumbnailContainers = $realizedItems
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
