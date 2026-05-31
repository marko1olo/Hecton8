param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$LedgerPath = "Docs/AgentLogs/YamlDesync_1502_Ledger.json",
    [string]$ReportPath = "Docs/AgentLogs/YamlExtraction_1502.json"
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

$targetDeletedNames = @(
    "_cellIntegrityFront",
    "_cellIntegrityBack",
    "_cellFatigue",
    "_cellCompartmentIndices",
    "_hullBreachMaskFront",
    "_hullBreachMaskBack",
    "_compartmentBreachAreasFront",
    "_compartmentBreachAreasBack",
    "_queuedImpacts",
    "_scheduledImpacts",
    "_compartmentCentroids",
    "_fatigueCompartmentFlags",
    "_fatigueIntegrityLossPerCycle",
    "_fatiguePeakResult",
    "_breachSeveritySumResult",
    "_densityBuildSources",
    "_scavengerMatrices",
    "_scavengerBatchMetadata",
    "_publishedSonarSdf",
    "_combatDamageArray"
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

function ConvertTo-ForwardSlashPath {
    param([string]$Path)
    return $Path.Replace("\", "/")
}

function Test-BinaryFile {
    param([string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $length = [Math]::Min(4096, [int]$stream.Length)
        $buffer = New-Object byte[] $length
        [void]$stream.Read($buffer, 0, $length)
        for ($i = 0; $i -lt $length; $i++) {
            if ($buffer[$i] -eq 0) {
                return $true
            }
        }

        return $false
    }
    finally {
        $stream.Dispose()
    }
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$ledgerFull = Resolve-UnderRoot $projectFull $LedgerPath
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
if (!(Test-Path -LiteralPath $ledgerFull -PathType Leaf)) {
    throw "Ledger missing: $ledgerFull"
}

$ledger = Get-Content -Raw -LiteralPath $ledgerFull | ConvertFrom-Json
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()

$records = foreach ($target in $targets) {
    $fullPath = Resolve-UnderRoot $projectFull $target
    if (!(Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Target missing: $target"
    }

    $isBinary = Test-BinaryFile $fullPath
    $format = if ($isBinary) { "BINARY_UNITY_ASSET" } else { "TEXT_UNITY_YAML" }
    $deletedHits = @()
    $cleanupCandidates = @()

    if (!$isBinary) {
        $lines = [System.IO.File]::ReadAllLines($fullPath)
        for ($i = 0; $i -lt $lines.Length; $i++) {
            foreach ($name in $targetDeletedNames) {
                if ($lines[$i] -match ("^\s*" + [regex]::Escape($name) + "\s*:")) {
                    $deletedHits += [pscustomobject]@{
                        line = $i + 1
                        property = $name
                        raw = $lines[$i]
                        bytes = [Text.Encoding]::UTF8.GetByteCount($lines[$i])
                    }
                }
            }
        }

        $ledgerEntry = $ledger.files | Where-Object { (ConvertTo-ForwardSlashPath $_.path) -eq (ConvertTo-ForwardSlashPath $fullPath) } | Select-Object -First 1
        if ($ledgerEntry -and $ledgerEntry.orphanedSerializedProperties) {
            $cleanupCandidates = @(
                $ledgerEntry.orphanedSerializedProperties |
                    Where-Object { [string]$_.scriptPath -like "Assets/_Project/*" } |
                    ForEach-Object {
                        [pscustomobject]@{
                            line = $_.line
                            componentFileID = $_.componentFileID
                            scriptGuid = $_.scriptGuid
                            scriptPath = $_.scriptPath
                            scriptClass = $_.scriptClass
                            property = $_.property
                            reason = $_.reason
                        }
                    }
            )
        }
    }

    [pscustomobject]@{
        relativePath = $target
        path = $fullPath
        format = $format
        rawTextAccessible = -not $isBinary
        targetDeletedFieldHits = @($deletedHits)
        firstPartyCleanupCandidates = @($cleanupCandidates)
        rawTextAction = if ($deletedHits.Count -gt 0) { "EXTRACT_FOR_TRANSPLANT" } elseif ($cleanupCandidates.Count -gt 0) { "DRY_RUN_CLEANUP_ONLY" } else { "NO_RAW_ACTION" }
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_EXTRACTION"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    targetCount = $records.Count
    targetDeletedFieldHits = [int](($records | ForEach-Object { $_.targetDeletedFieldHits.Count } | Measure-Object -Sum).Sum)
    firstPartyCleanupCandidates = [int](($records | ForEach-Object { $_.firstPartyCleanupCandidates.Count } | Measure-Object -Sum).Sum)
    elapsedMicroseconds = $elapsedUs
    records = @($records)
}

$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
