param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$BackupRoot = "Docs/AgentLogs/_Recovery_1502",
    [string]$ManifestPath = "Docs/AgentLogs/Backup_1502_Manifest.json"
)

$ErrorActionPreference = "Stop"

$targets = @(
    "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
    "Assets/_Project/Prefabs/Player.prefab",
    "Assets/_Project/Prefabs/PFB_Submarine_Core.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Foundation.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab",
    "Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab"
)

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
$backupFull = Resolve-UnderRoot $projectFull $BackupRoot
$manifestFull = Resolve-UnderRoot $projectFull $ManifestPath
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()

New-Item -ItemType Directory -Force -Path $backupFull | Out-Null

$records = foreach ($target in $targets) {
    $sourceFull = Resolve-UnderRoot $projectFull $target
    if (!(Test-Path -LiteralPath $sourceFull -PathType Leaf)) {
        throw "Target missing: $target"
    }

    $backupFile = Resolve-UnderRoot $backupFull $target
    $backupDir = [System.IO.Path]::GetDirectoryName($backupFile)
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
    Copy-Item -LiteralPath $sourceFull -Destination $backupFile -Force

    $sourceInfo = Get-Item -LiteralPath $sourceFull
    $backupInfo = Get-Item -LiteralPath $backupFile
    $sourceHash = (Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash
    $backupHash = (Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash

    if ($sourceInfo.Length -ne $backupInfo.Length -or $sourceHash -ne $backupHash) {
        throw "Backup parity failure: $target"
    }

    [pscustomobject]@{
        relativePath = $target
        sourcePath = $sourceFull
        backupPath = $backupFile
        bytes = $sourceInfo.Length
        sha256 = $sourceHash
        parity = "MATCH"
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$manifest = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_BACKUP"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    projectRoot = $projectFull
    backupRoot = $backupFull
    targetCount = $records.Count
    totalBytes = [int64](($records | Measure-Object -Property bytes -Sum).Sum)
    elapsedMicroseconds = $elapsedUs
    records = @($records)
}

$manifestDir = [System.IO.Path]::GetDirectoryName($manifestFull)
New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestFull -Encoding UTF8
$manifest
