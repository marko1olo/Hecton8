param(
    [int]$DurationSeconds = 1800,
    [int]$IntervalSeconds = 5,
    [string]$OutputDir = "C:\hades\Hecton8\Docs\AgentLogs"
)

$ErrorActionPreference = "SilentlyContinue"
$start = Get-Date
$stamp = $start.ToString("yyyyMMdd_HHmmss")
$samplesPath = Join-Path $OutputDir "Traffic_dvachbot_$stamp.jsonl"
$summaryPath = Join-Path $OutputDir "Traffic_dvachbot_$stamp.summary.json"
$progressPath = Join-Path $OutputDir "Traffic_dvachbot_$stamp.progress.json"

$initialAdapters = Get-NetAdapterStatistics | Select-Object Name, ReceivedBytes, SentBytes
$processConnectionTicks = @{}
$remoteTicks = @{}
$adapterSamples = @()
$maxSamples = [Math]::Max(1, [int]($DurationSeconds / $IntervalSeconds))

for ($i = 0; $i -le $maxSamples; $i++) {
    $now = Get-Date
    $processes = @{}
    Get-CimInstance Win32_Process | ForEach-Object {
        $cmd = $_.CommandLine
        if ($cmd -and $cmd.Length -gt 260) {
            $cmd = $cmd.Substring(0, 260)
        }
        $processes[[int]$_.ProcessId] = [pscustomobject]@{
            name = $_.Name
            path = $_.ExecutablePath
            commandLine = $cmd
        }
    }

    $adapters = Get-NetAdapterStatistics | Select-Object Name, ReceivedBytes, SentBytes
    $tcp = Get-NetTCPConnection -State Established | Where-Object {
        $_.RemoteAddress -and $_.RemoteAddress -notin @("127.0.0.1", "::1", "0.0.0.0")
    }

    $groups = @()
    $tcp | Group-Object OwningProcess | ForEach-Object {
        $ownerPid = [int]$_.Name
        $proc = $processes[$ownerPid]
        $key = "$ownerPid|$($proc.name)|$($proc.commandLine)"
        if (-not $processConnectionTicks.ContainsKey($key)) {
            $processConnectionTicks[$key] = 0
        }
        $processConnectionTicks[$key] += ($_.Count * $IntervalSeconds)

        $groups += [pscustomobject]@{
            pid = $ownerPid
            process = $proc.name
            path = $proc.path
            commandLine = $proc.commandLine
            connectionCount = $_.Count
        }
    }

    $tcp | Group-Object { "$($_.RemoteAddress):$($_.RemotePort)" } | ForEach-Object {
        if (-not $remoteTicks.ContainsKey($_.Name)) {
            $remoteTicks[$_.Name] = 0
        }
        $remoteTicks[$_.Name] += ($_.Count * $IntervalSeconds)
    }

    $sample = [pscustomobject]@{
        timestamp = $now.ToString("o")
        secondsFromStart = [int]($now - $start).TotalSeconds
        adapters = $adapters
        tcpGroups = $groups
    }
    $sample | ConvertTo-Json -Depth 6 -Compress | Add-Content -LiteralPath $samplesPath -Encoding UTF8
    $adapterSamples += $adapters

    [pscustomobject]@{
        started = $start.ToString("o")
        lastSample = $now.ToString("o")
        sample = $i
        maxSamples = $maxSamples
        samplesPath = $samplesPath
        summaryPath = $summaryPath
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $progressPath -Encoding UTF8

    if (($now - $start).TotalSeconds -ge $DurationSeconds) {
        break
    }
    Start-Sleep -Seconds $IntervalSeconds
}

$finalAdapters = Get-NetAdapterStatistics | Select-Object Name, ReceivedBytes, SentBytes
$adapterDeltas = foreach ($final in $finalAdapters) {
    $initial = $initialAdapters | Where-Object Name -eq $final.Name | Select-Object -First 1
    [pscustomobject]@{
        name = $final.Name
        receivedBytesDelta = [int64]($final.ReceivedBytes - $initial.ReceivedBytes)
        sentBytesDelta = [int64]($final.SentBytes - $initial.SentBytes)
        totalBytesDelta = [int64](($final.ReceivedBytes - $initial.ReceivedBytes) + ($final.SentBytes - $initial.SentBytes))
    }
}

$topProcesses = $processConnectionTicks.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 25 |
    ForEach-Object {
        $parts = $_.Key -split "\|", 3
        [pscustomobject]@{
            pid = [int]$parts[0]
            process = $parts[1]
            commandLine = $parts[2]
            connectionSeconds = [int]$_.Value
        }
    }

$topRemotes = $remoteTicks.GetEnumerator() |
    Sort-Object Value -Descending |
    Select-Object -First 40 |
    ForEach-Object {
        [pscustomobject]@{
            remote = $_.Key
            connectionSeconds = [int]$_.Value
        }
    }

[pscustomobject]@{
    started = $start.ToString("o")
    finished = (Get-Date).ToString("o")
    durationSeconds = [int]((Get-Date) - $start).TotalSeconds
    intervalSeconds = $IntervalSeconds
    samplesPath = $samplesPath
    adapterDeltas = $adapterDeltas
    topProcessesByConnectionSeconds = $topProcesses
    topRemotesByConnectionSeconds = $topRemotes
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
