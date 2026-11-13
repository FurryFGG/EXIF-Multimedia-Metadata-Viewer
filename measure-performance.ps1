param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [string]$MediaFile = "",
    [string]$Scenario = "startup",
    [ValidateRange(1, 20)]
    [int]$Iterations = 5,
    [ValidateRange(2, 120)]
    [int]$SettleSeconds = 8,
    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$executablePath = [IO.Path]::GetFullPath($Executable)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Executable not found: $executablePath"
}
if ($MediaFile) {
    $mediaPath = [IO.Path]::GetFullPath($MediaFile)
    if (-not (Test-Path -LiteralPath $mediaPath -PathType Leaf)) {
        throw "Media file not found: $mediaPath"
    }
} else {
    $mediaPath = ""
}

function Get-ProcessCpuSample {
    param([Diagnostics.Process]$Process, [int]$Milliseconds = 1000)
    $Process.Refresh()
    $startCpu = $Process.TotalProcessorTime
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Start-Sleep -Milliseconds $Milliseconds
    $Process.Refresh()
    $timer.Stop()
    $cpuDelta = ($Process.TotalProcessorTime - $startCpu).TotalMilliseconds
    [math]::Round($cpuDelta / ($timer.Elapsed.TotalMilliseconds * [Environment]::ProcessorCount) * 100, 3)
}

function Get-ExplorerSnapshot {
    $items = @(Get-Process explorer -ErrorAction SilentlyContinue)
    [pscustomobject]@{
        Count = $items.Count
        WorkingSetBytes = [int64](($items | Measure-Object WorkingSet64 -Sum).Sum)
        PrivateBytes = [int64](($items | Measure-Object PrivateMemorySize64 -Sum).Sum)
        Threads = [int](($items | ForEach-Object { $_.Threads.Count } | Measure-Object -Sum).Sum)
        Handles = [int](($items | Measure-Object HandleCount -Sum).Sum)
        Responding = -not ($items | Where-Object { -not $_.Responding })
    }
}

$existing = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    try { [string]::Equals($_.Path, $executablePath, [StringComparison]::OrdinalIgnoreCase) } catch { $false }
})
if ($existing.Count -gt 0) {
    throw "The target executable is already running. Close it before measuring: $executablePath"
}

$results = [Collections.Generic.List[object]]::new()
for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
    $explorerBefore = Get-ExplorerSnapshot
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $startInfo = @{ FilePath = $executablePath; PassThru = $true }
    if ($mediaPath) { $startInfo.ArgumentList = @($mediaPath) }
    $process = Start-Process @startInfo
    $windowMs = $null
    $interactiveMs = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        $process.Refresh()
        if ($null -eq $windowMs -and $process.MainWindowHandle -ne 0) {
            $windowMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
        }
        if ($process.MainWindowHandle -ne 0 -and $process.Responding) {
            if ($null -eq $windowMs) {
                $windowMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
            }
            $interactiveMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
            break
        }
        Start-Sleep -Milliseconds 20
    }

    if ($process.HasExited) {
        throw "Application exited during measurement iteration $iteration with code $($process.ExitCode)."
    }

    Start-Sleep -Seconds $SettleSeconds
    $process.Refresh()
    $cpuPercent = Get-ProcessCpuSample -Process $process
    $process.Refresh()
    $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($process.Id)")
    $explorerAfter = Get-ExplorerSnapshot
    $results.Add([pscustomobject]@{
        Scenario = $Scenario
        Iteration = $iteration
        Executable = $executablePath
        MediaFile = $mediaPath
        MainWindowMs = $windowMs
        InteractiveMs = $interactiveMs
        SettleSeconds = $SettleSeconds
        CpuPercentAfterSettle = $cpuPercent
        TotalCpuMs = [math]::Round($process.TotalProcessorTime.TotalMilliseconds, 1)
        WorkingSetBytes = $process.WorkingSet64
        PeakWorkingSetBytes = $process.PeakWorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
        Threads = $process.Threads.Count
        Handles = $process.HandleCount
        ReadBytes = [int64]$cim.ReadTransferCount
        WriteBytes = [int64]$cim.WriteTransferCount
        ChildProcessCount = $children.Count
        ChildProcessNames = (($children.Name | Sort-Object -Unique) -join ",")
        ExplorerWorkingSetBefore = $explorerBefore.WorkingSetBytes
        ExplorerWorkingSetAfter = $explorerAfter.WorkingSetBytes
        ExplorerThreadsBefore = $explorerBefore.Threads
        ExplorerThreadsAfter = $explorerAfter.Threads
        ExplorerHandlesBefore = $explorerBefore.Handles
        ExplorerHandlesAfter = $explorerAfter.Handles
        ExplorerRespondingBefore = $explorerBefore.Responding
        ExplorerRespondingAfter = $explorerAfter.Responding
    })

    if (-not $process.CloseMainWindow()) {
        Stop-Process -Id $process.Id
    } elseif (-not $process.WaitForExit(5000)) {
        Stop-Process -Id $process.Id
    }
    $process.Dispose()
    Start-Sleep -Milliseconds 500
}

$outputDirectory = Split-Path -Parent $outputFile
if ($outputDirectory) { New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null }
$results | Export-Csv -LiteralPath $outputFile -NoTypeInformation -Encoding UTF8
$results | Format-Table Scenario,Iteration,MainWindowMs,InteractiveMs,CpuPercentAfterSettle,
    @{N='WorkingSetMB';E={[math]::Round($_.WorkingSetBytes / 1MB, 1)}},Threads,Handles,ChildProcessCount -AutoSize
