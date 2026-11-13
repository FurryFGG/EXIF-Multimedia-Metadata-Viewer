param(
    [Parameter(Mandatory = $true)] [string]$Executable,
    [Parameter(Mandatory = $true)] [string]$MediaFile,
    [Parameter(Mandatory = $true)] [string]$OutputPath,
    [ValidateRange(1, 60)] [int]$DurationMinutes = 15,
    [ValidateRange(10, 300)] [int]$IntervalSeconds = 30
)

$ErrorActionPreference = "Stop"
$executablePath = [IO.Path]::GetFullPath($Executable)
$mediaPath = [IO.Path]::GetFullPath($MediaFile)
$outputFile = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) { throw "Executable not found: $executablePath" }
if (-not (Test-Path -LiteralPath $mediaPath -PathType Leaf)) { throw "Media file not found: $mediaPath" }

function Get-ExplorerSnapshot {
    $items = @(Get-Process explorer -ErrorAction SilentlyContinue)
    [pscustomobject]@{
        WorkingSetBytes = [int64](($items | Measure-Object WorkingSet64 -Sum).Sum)
        Threads = [int](($items | ForEach-Object { $_.Threads.Count } | Measure-Object -Sum).Sum)
        Handles = [int](($items | Measure-Object HandleCount -Sum).Sum)
        Responding = -not ($items | Where-Object { -not $_.Responding })
    }
}

$process = Start-Process -FilePath $executablePath -ArgumentList @($mediaPath) -PassThru
$started = [DateTime]::UtcNow
$deadline = $started.AddMinutes($DurationMinutes)
$rows = [Collections.Generic.List[object]]::new()
try {
    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        Start-Sleep -Seconds $IntervalSeconds
        $process.Refresh()
        $cim = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.Id)"
        $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($process.Id)")
        $explorer = Get-ExplorerSnapshot
        $rows.Add([pscustomobject]@{
            ElapsedSeconds = [math]::Round(([DateTime]::UtcNow - $started).TotalSeconds, 1)
            Responding = $process.Responding
            WorkingSetBytes = $process.WorkingSet64
            PrivateBytes = $process.PrivateMemorySize64
            TotalCpuMs = [math]::Round($process.TotalProcessorTime.TotalMilliseconds, 1)
            Threads = $process.Threads.Count
            Handles = $process.HandleCount
            ReadBytes = [int64]$cim.ReadTransferCount
            WriteBytes = [int64]$cim.WriteTransferCount
            ChildProcessCount = $children.Count
            ExplorerWorkingSetBytes = $explorer.WorkingSetBytes
            ExplorerThreads = $explorer.Threads
            ExplorerHandles = $explorer.Handles
            ExplorerResponding = $explorer.Responding
        })
    }
} finally {
    if (-not $process.HasExited) {
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id }
    }
    $process.Dispose()
}

$outputDirectory = Split-Path -Parent $outputFile
if ($outputDirectory) { New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null }
$rows | Export-Csv -LiteralPath $outputFile -NoTypeInformation -Encoding UTF8
$first = $rows | Select-Object -First 1
$last = $rows | Select-Object -Last 1
[pscustomobject]@{
    Samples = $rows.Count
    FirstWorkingSetMB = [math]::Round($first.WorkingSetBytes / 1MB, 1)
    LastWorkingSetMB = [math]::Round($last.WorkingSetBytes / 1MB, 1)
    WorkingSetChangeMB = [math]::Round(($last.WorkingSetBytes - $first.WorkingSetBytes) / 1MB, 1)
    FirstPrivateMB = [math]::Round($first.PrivateBytes / 1MB, 1)
    LastPrivateMB = [math]::Round($last.PrivateBytes / 1MB, 1)
    ThreadChange = $last.Threads - $first.Threads
    HandleChange = $last.Handles - $first.Handles
    MaxChildren = ($rows | Measure-Object ChildProcessCount -Maximum).Maximum
    AppRespondingSamples = ($rows | Where-Object Responding).Count
    ExplorerRespondingSamples = ($rows | Where-Object ExplorerResponding).Count
} | Format-List
