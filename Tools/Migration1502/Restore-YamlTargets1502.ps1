param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ManifestPath = "Docs/AgentLogs/Backup_1502_Manifest.json",
    [string]$ReportPath = "Docs/AgentLogs/Restore_1502_Report.json"
)

$ErrorActionPreference = "Stop"

function Resolve-UnderRoot {
    param(
        [string]$Root,
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $Path))
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$manifestFull = Resolve-UnderRoot $projectFull $ManifestPath
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
if (!(Test-Path -LiteralPath $manifestFull -PathType Leaf)) {
    throw "Backup manifest missing: $manifestFull"
}

$manifest = Get-Content -Raw -LiteralPath $manifestFull | ConvertFrom-Json
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()

$records = foreach ($record in $manifest.records) {
    $sourceFull = Resolve-UnderRoot $projectFull $record.relativePath
    $backupFull = [string]$record.backupPath
    if (!(Test-Path -LiteralPath $backupFull -PathType Leaf)) {
        throw "Backup file missing: $backupFull"
    }

    Copy-Item -LiteralPath $backupFull -Destination $sourceFull -Force
    $restoredInfo = Get-Item -LiteralPath $sourceFull
    $restoredHash = (Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash
    $parity = if ($restoredHash -eq [string]$record.sha256 -and $restoredInfo.Length -eq [int64]$record.bytes) { "MATCH" } else { "MISMATCH" }
    if ($parity -ne "MATCH") {
        throw "Restore parity failure: $($record.relativePath)"
    }

    [pscustomobject]@{
        relativePath = $record.relativePath
        restoredPath = $sourceFull
        bytes = $restoredInfo.Length
        sha256 = $restoredHash
        parity = $parity
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_RESTORE"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    restoredCount = $records.Count
    elapsedMicroseconds = $elapsedUs
    records = @($records)
}

$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
