param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$BackupManifestPath = "Docs/AgentLogs/Backup_1502_Manifest.json",
    [string]$ExtractionPath = "Docs/AgentLogs/YamlExtraction_1502.json",
    [string]$ReportPath = "Docs/AgentLogs/YamlInvariant_1502.json"
)

$ErrorActionPreference = "Stop"

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
$backupManifestFull = Resolve-UnderRoot $projectFull $BackupManifestPath
$extractionFull = Resolve-UnderRoot $projectFull $ExtractionPath
$reportFull = Resolve-UnderRoot $projectFull $ReportPath

if (!(Test-Path -LiteralPath $backupManifestFull -PathType Leaf)) {
    throw "Backup manifest missing: $backupManifestFull"
}
if (!(Test-Path -LiteralPath $extractionFull -PathType Leaf)) {
    throw "Extraction report missing: $extractionFull"
}

$backupManifest = Get-Content -Raw -LiteralPath $backupManifestFull | ConvertFrom-Json
$extraction = Get-Content -Raw -LiteralPath $extractionFull | ConvertFrom-Json
$obsoleteKeyPattern = "^\s*(" + (($targetDeletedNames | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")\s*:"
$obsoleteOverridePattern = "propertyPath:\s*(" + (($targetDeletedNames | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")(\.|$)"
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()

$records = foreach ($record in $backupManifest.records) {
    $sourceFull = Resolve-UnderRoot $projectFull ([string]$record.relativePath)
    $isBinary = Test-BinaryFile $sourceFull
    $format = if ($isBinary) { "BINARY_UNITY_ASSET" } else { "TEXT_UNITY_YAML" }
    $currentInfo = Get-Item -LiteralPath $sourceFull
    $currentHash = (Get-FileHash -LiteralPath $sourceFull -Algorithm SHA256).Hash
    $hashParity = if ($currentHash -eq [string]$record.sha256 -and $currentInfo.Length -eq [int64]$record.bytes) { "UNCHANGED_FROM_BACKUP" } else { "CHANGED_FROM_BACKUP" }

    $yamlHeader = $false
    $tagHeader = $false
    $rootGameObjectMarker = $false
    $prefabGameObjectBlock = $false
    $monoBehaviourCount = $null
    $missingScriptCount = $null
    $obsoleteKeyCount = $null
    $obsoleteOverrideCount = $null

    if (!$isBinary) {
        $lines = [System.IO.File]::ReadAllLines($sourceFull)
        $yamlHeader = $lines.Length -gt 0 -and $lines[0] -eq "%YAML 1.1"
        $tagHeader = $lines.Length -gt 1 -and $lines[1].StartsWith("%TAG !u! tag:unity3d.com,2011:")
        $rootGameObjectMarker = ($lines | Select-String -Pattern "m_RootGameObject" -Quiet)
        $prefabGameObjectBlock = ($lines | Select-String -Pattern "^GameObject:" -Quiet)
        $monoBehaviourCount = @($lines | Select-String -Pattern "^--- !u!114 &").Count
        $missingScriptCount = @($lines | Select-String -Pattern "m_Script:\s*\{fileID:\s*0").Count
        $obsoleteKeyCount = @($lines | Select-String -Pattern $obsoleteKeyPattern).Count
        $obsoleteOverrideCount = @($lines | Select-String -Pattern $obsoleteOverridePattern).Count
    }

    $extractRecord = $extraction.records | Where-Object { [string]$_.relativePath -eq [string]$record.relativePath } | Select-Object -First 1
    [pscustomobject]@{
        relativePath = $record.relativePath
        format = $format
        bytes = $currentInfo.Length
        sha256 = $currentHash
        backupParity = $hashParity
        yamlHeader = $yamlHeader
        unityTagHeader = $tagHeader
        rootGameObjectMarker = $rootGameObjectMarker
        prefabGameObjectBlock = $prefabGameObjectBlock
        monoBehaviourCount = $monoBehaviourCount
        missingScriptCount = $missingScriptCount
        targetObsoleteKeyCount = $obsoleteKeyCount
        targetObsoleteOverrideCount = $obsoleteOverrideCount
        extractionAction = if ($extractRecord) { $extractRecord.rawTextAction } else { "NO_EXTRACTION_RECORD" }
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_INVARIANT"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    targetCount = $records.Count
    changedFromBackupCount = @($records | Where-Object { $_.backupParity -ne "UNCHANGED_FROM_BACKUP" }).Count
    textYamlCount = @($records | Where-Object { $_.format -eq "TEXT_UNITY_YAML" }).Count
    binaryUnityAssetCount = @($records | Where-Object { $_.format -eq "BINARY_UNITY_ASSET" }).Count
    missingScriptCount = [int](($records | Where-Object { $_.missingScriptCount -ne $null } | Measure-Object -Property missingScriptCount -Sum).Sum)
    targetObsoleteKeyCount = [int](($records | Where-Object { $_.targetObsoleteKeyCount -ne $null } | Measure-Object -Property targetObsoleteKeyCount -Sum).Sum)
    targetObsoleteOverrideCount = [int](($records | Where-Object { $_.targetObsoleteOverrideCount -ne $null } | Measure-Object -Property targetObsoleteOverrideCount -Sum).Sum)
    elapsedMicroseconds = $elapsedUs
    records = @($records)
}

$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
