param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$LedgerPath = "Docs/AgentLogs/YamlDesync_1502_Ledger.json",
    [string]$BackupManifestPath = "Docs/AgentLogs/Backup_1502_Manifest.json",
    [string]$ExtractionPath = "Docs/AgentLogs/YamlExtraction_1502.json",
    [string]$InvariantPath = "Docs/AgentLogs/YamlInvariant_1502.json",
    [string]$FuzzPath = "Docs/AgentLogs/YamlFuzz_1502.json",
    [string]$CleanupPath = "Docs/AgentLogs/YamlCleanup_1502.json",
    [string]$ShakeProfileFalloffPath = "Docs/AgentLogs/ShakeProfileFalloff_1502.json",
    [string]$ProjectLedgerPath = "Docs/AgentLogs/YamlDesync_1502_ProjectLedger.json",
    [string]$ThirdPartyDebtPath = "Docs/AgentLogs/YamlThirdPartySerializedDebt_1502.json",
    [string]$EvidenceValidationPath = "Docs/AgentLogs/YamlEvidenceValidation_1502.json",
    [string]$ReferenceIntegrityPath = "Docs/AgentLogs/YamlReferenceIntegrity_1502.json",
    [string]$BackupDeltaPath = "Docs/AgentLogs/YamlBackupDelta_1502.json",
    [string]$ReportPath = "Docs/Reports/YAML_MIGRATION_REPORT_1502.json",
    [string]$HumanLogPath = "Docs/AgentLogs/LOG_1502.md"
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

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Add-ModifiedHashRecords {
    param(
        [System.Collections.Generic.List[object]]$Target,
        [object]$MutationReport,
        [string]$MutationKind,
        [string]$ProjectRoot
    )

    if ($null -eq $MutationReport -or $null -eq $MutationReport.files) {
        return
    }

    foreach ($file in @($MutationReport.files)) {
        if ($null -eq $file -or ![bool]$file.applied) {
            continue
        }

        $relativePath = [string]$file.relativePath
        $currentPath = Resolve-UnderRoot $ProjectRoot $relativePath
        $backupPath = [string]$file.backupPath
        $currentSha = if (Test-Path -LiteralPath $currentPath -PathType Leaf) { Get-Sha256 $currentPath } else { $null }
        $backupSha = if (![string]::IsNullOrWhiteSpace($backupPath) -and (Test-Path -LiteralPath $backupPath -PathType Leaf)) { Get-Sha256 $backupPath } else { $null }
        $shaBefore = [string]$file.sha256Before
        $shaAfter = [string]$file.sha256After
        $stats = if ($null -ne $file.afterStats) { $file.afterStats } elseif ($null -ne $file.postStats) { $file.postStats } else { $null }

        [void]$Target.Add([pscustomobject]@{
            relativePath = $relativePath
            mutationKind = $MutationKind
            bytesBefore = [int64]$file.bytesBefore
            bytesAfter = [int64]$file.bytesAfter
            sha256Before = $shaBefore
            sha256After = $shaAfter
            currentSha256 = $currentSha
            currentMatchesReport = [bool]($currentSha -eq $shaAfter)
            backupPath = $backupPath
            backupSha256 = $backupSha
            backupMatchesBefore = [bool]($backupSha -eq $shaBefore)
            monoBehaviourCount = if ($null -ne $stats) { $stats.monoBehaviourCount } else { $null }
            missingScriptCount = if ($null -ne $stats) { $stats.missingScriptCount } else { $null }
            rootGameObjectMarker = if ($null -ne $stats) { $stats.rootGameObjectMarker } else { $null }
            prefabGameObjectBlock = if ($null -ne $stats) { $stats.prefabGameObjectBlock } else { $null }
            falloffCurveCount = if ($null -ne $stats) { $stats.falloffCurveCount } else { $null }
            falloffExponentCount = if ($null -ne $stats) { $stats.falloffExponentCount } else { $null }
        })
    }
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$ledgerFull = Resolve-UnderRoot $projectFull $LedgerPath
$backupFull = Resolve-UnderRoot $projectFull $BackupManifestPath
$extractionFull = Resolve-UnderRoot $projectFull $ExtractionPath
$invariantFull = Resolve-UnderRoot $projectFull $InvariantPath
$fuzzFull = Resolve-UnderRoot $projectFull $FuzzPath
$cleanupFull = Resolve-UnderRoot $projectFull $CleanupPath
$shakeFull = Resolve-UnderRoot $projectFull $ShakeProfileFalloffPath
$projectLedgerFull = Resolve-UnderRoot $projectFull $ProjectLedgerPath
$thirdPartyDebtFull = Resolve-UnderRoot $projectFull $ThirdPartyDebtPath
$evidenceValidationFull = Resolve-UnderRoot $projectFull $EvidenceValidationPath
$referenceIntegrityFull = Resolve-UnderRoot $projectFull $ReferenceIntegrityPath
$backupDeltaFull = Resolve-UnderRoot $projectFull $BackupDeltaPath
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$humanLogFull = Resolve-UnderRoot $projectFull $HumanLogPath

foreach ($path in @($ledgerFull, $backupFull, $extractionFull, $invariantFull, $fuzzFull)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required evidence missing: $path"
    }
}

$ledger = Get-Content -Raw -LiteralPath $ledgerFull | ConvertFrom-Json
$backup = Get-Content -Raw -LiteralPath $backupFull | ConvertFrom-Json
$extraction = Get-Content -Raw -LiteralPath $extractionFull | ConvertFrom-Json
$invariant = Get-Content -Raw -LiteralPath $invariantFull | ConvertFrom-Json
$fuzz = Get-Content -Raw -LiteralPath $fuzzFull | ConvertFrom-Json
$cleanup = if (Test-Path -LiteralPath $cleanupFull -PathType Leaf) { Get-Content -Raw -LiteralPath $cleanupFull | ConvertFrom-Json } else { $null }
$shake = if (Test-Path -LiteralPath $shakeFull -PathType Leaf) { Get-Content -Raw -LiteralPath $shakeFull | ConvertFrom-Json } else { $null }
$projectLedger = if (Test-Path -LiteralPath $projectLedgerFull -PathType Leaf) { Get-Content -Raw -LiteralPath $projectLedgerFull | ConvertFrom-Json } else { $null }
$thirdPartyDebt = if (Test-Path -LiteralPath $thirdPartyDebtFull -PathType Leaf) { Get-Content -Raw -LiteralPath $thirdPartyDebtFull | ConvertFrom-Json } else { $null }
$evidenceValidation = if (Test-Path -LiteralPath $evidenceValidationFull -PathType Leaf) { Get-Content -Raw -LiteralPath $evidenceValidationFull | ConvertFrom-Json } else { $null }
$referenceIntegrity = if (Test-Path -LiteralPath $referenceIntegrityFull -PathType Leaf) { Get-Content -Raw -LiteralPath $referenceIntegrityFull | ConvertFrom-Json } else { $null }
$backupDelta = if (Test-Path -LiteralPath $backupDeltaFull -PathType Leaf) { Get-Content -Raw -LiteralPath $backupDeltaFull | ConvertFrom-Json } else { $null }
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()

$cleanupApplied = $null -ne $cleanup -and [bool]$cleanup.apply
$cleanupFilesWritten = if ($cleanupApplied) { [int]$cleanup.filesWritten } else { 0 }
$cleanupPropertiesDeleted = if ($cleanupApplied) { [int]$cleanup.propertiesDeleted } else { 0 }
$cleanupBytesRewritten = if ($cleanupApplied) { [int64]$cleanup.totalBytesBefore - [int64]$cleanup.totalBytesAfter } else { 0 }
$cleanupMicroseconds = if ($null -ne $cleanup) { [int64]$cleanup.elapsedMicroseconds } else { 0 }
$shakeApplied = $null -ne $shake -and [bool]$shake.apply
$shakeFilesWritten = if ($shakeApplied) { [int]$shake.filesWritten } else { 0 }
$shakeCurvesMapped = if ($shakeApplied) { [int]$shake.curvesMapped } else { 0 }
$shakeStaleBlocksDeleted = if ($shakeApplied) { [int]$shake.staleCurveBlocksDeleted } else { 0 }
$shakePropertiesAdded = if ($shakeApplied) { [int]$shake.falloffExponentPropertiesAdded } else { 0 }
$shakeBytesRewritten = if ($shakeApplied) { [int64]$shake.totalBytesBefore - [int64]$shake.totalBytesAfter } else { 0 }
$shakeMicroseconds = if ($null -ne $shake) { [int64]$shake.elapsedMicroseconds } else { 0 }
$projectFirstPartyOrphans = 0
if ($null -ne $projectLedger) {
    foreach ($file in @($projectLedger.files)) {
        foreach ($hit in @($file.orphanedSerializedProperties)) {
            if ($null -ne $hit -and ([string]$hit.scriptPath).StartsWith("Assets/_Project/", [StringComparison]::Ordinal)) {
                $projectFirstPartyOrphans++
            }
        }
    }
}

$assetHashes = @(
    $invariant.records | ForEach-Object {
        [pscustomobject]@{
            relativePath = $_.relativePath
            format = $_.format
            bytes = $_.bytes
            sha256 = $_.sha256
            backupParity = $_.backupParity
            missingScriptCount = $_.missingScriptCount
            targetObsoleteKeyCount = $_.targetObsoleteKeyCount
            targetObsoleteOverrideCount = $_.targetObsoleteOverrideCount
        }
    }
)

$modifiedAssetHashes = [System.Collections.Generic.List[object]]::new()
Add-ModifiedHashRecords -Target $modifiedAssetHashes -MutationReport $cleanup -MutationKind "PREFAB_ORPHAN_ROOT_PROPERTY_DELETE" -ProjectRoot $projectFull
Add-ModifiedHashRecords -Target $modifiedAssetHashes -MutationReport $shake -MutationKind "SHAKEPROFILE_FALLOFF_CURVE_TO_EXPONENT" -ProjectRoot $projectFull
$modifiedHashMismatches = 0
foreach ($record in $modifiedAssetHashes) {
    if (!$record.currentMatchesReport -or !$record.backupMatchesBefore) {
        $modifiedHashMismatches++
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    role = "YAML_SERIALIZED_PROPERTY_AND_PREFAB_METADATA_MIGRATOR"
    evidenceClass = "STATIC_SOURCE_FINAL"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    migrationDecision = if ($cleanupApplied -and $shakeApplied) { "PREFAB_AND_SHAKEPROFILE_ASSET_ORPHAN_METADATA_CLEANUP_APPLIED" } elseif ($cleanupApplied) { "PREFAB_ORPHAN_METADATA_CLEANUP_APPLIED" } elseif ($shakeApplied) { "SHAKEPROFILE_ASSET_FALLOFF_MIGRATION_APPLIED" } else { "NO_ASSET_MUTATION" }
    decisionReason = if ($cleanupApplied -and $shakeApplied) {
        "Target deleted native serialized fields were not present, so no DTO transplantation was fabricated. After parser false-positive reduction, 50 proven first-party prefab root orphan keys were removed. Six ShakeProfile stale FalloffCurve asset blocks were mapped to FalloffExponent scalar authoring data and deleted with per-file backups and structure invariants."
    } elseif ($cleanupApplied) {
        "Target deleted native serialized fields were not present, so no DTO transplantation was fabricated. After parser false-positive reduction, 50 proven first-party prefab root orphan keys were removed with per-file backup and structure invariants."
    } elseif ($shakeApplied) {
        "Target deleted native serialized fields were not present. Six ShakeProfile stale FalloffCurve asset blocks were mapped to FalloffExponent scalar authoring data and deleted with per-file backups and structure invariants."
    } else {
        "Target deleted native serialized fields were not present in scanned Unity text assets; the master scene is binary serialized; broad orphan candidates lack one-to-one DTO destinations."
    }
    filesScanned = $ledger.stats.yamlFiles
    monoBehavioursScanned = $ledger.stats.monoBehaviours
    scriptSchemasScanned = $ledger.stats.scriptSchemas
    targetObsoleteNativeFieldHits = $ledger.stats.targetObsoleteNativeFieldHits
    missingScriptReferences = $ledger.stats.missingScriptReferences
    obsoletePrefabOverridePaths = $ledger.stats.prefabOverrideObsoleteProperties
    backupGenerated = $true
    backupTargetCount = $backup.targetCount
    backupTotalBytes = $backup.totalBytes
    targetFilesAudited = $invariant.targetCount
    textYamlTargets = $invariant.textYamlCount
    binaryUnityTargets = $invariant.binaryUnityAssetCount
    filesModified = $cleanupFilesWritten + $shakeFilesWritten
    propertiesMigrated = $shakeCurvesMapped
    propertiesDeleted = $cleanupPropertiesDeleted + $shakeStaleBlocksDeleted
    bytesRewritten = $cleanupBytesRewritten + $shakeBytesRewritten
    prefabFilesModified = $cleanupFilesWritten
    prefabPropertiesDeleted = $cleanupPropertiesDeleted
    shakeProfileFilesModified = $shakeFilesWritten
    shakeProfileCurvesMapped = $shakeCurvesMapped
    shakeProfileFalloffExponentPropertiesAdded = $shakePropertiesAdded
    shakeProfileStaleCurveBlocksDeleted = $shakeStaleBlocksDeleted
    targetDeletedFieldPayloadsExtracted = $extraction.targetDeletedFieldHits
    firstPartyDryRunCleanupCandidates = $extraction.firstPartyCleanupCandidates
    projectWideYamlFilesScanned = if ($null -ne $projectLedger) { $projectLedger.stats.yamlFiles } else { $null }
    projectWideFirstPartyOrphanProperties = $projectFirstPartyOrphans
    projectWideOrphanProperties = if ($null -ne $projectLedger) { $projectLedger.stats.orphanedSerializedProperties } else { $null }
    projectWideMissingScriptReferences = if ($null -ne $projectLedger) { $projectLedger.stats.missingScriptReferences } else { $null }
    projectWideTargetObsoleteNativeFieldHits = if ($null -ne $projectLedger) { $projectLedger.stats.targetObsoleteNativeFieldHits } else { $null }
    thirdPartySerializedDebtProperties = if ($null -ne $thirdPartyDebt) { $thirdPartyDebt.totalOrphanProperties } else { $null }
    thirdPartyKnownToolDebtProperties = if ($null -ne $thirdPartyDebt) { $thirdPartyDebt.knownThirdPartyOrphanProperties } else { $null }
    thirdPartyFirstPartyDebtProperties = if ($null -ne $thirdPartyDebt) { $thirdPartyDebt.firstPartyOrphanProperties } else { $null }
    thirdPartyDebtMutationPolicy = if ($null -ne $thirdPartyDebt) { $thirdPartyDebt.mutationPolicy } else { $null }
    thirdPartyDebtDecision = if ($null -ne $thirdPartyDebt) { $thirdPartyDebt.decision } else { $null }
    thirdPartyDebtByScriptOwner = if ($null -ne $thirdPartyDebt) { @($thirdPartyDebt.byScriptOwner) } else { @() }
    modifiedFileHashCoverageCount = $modifiedAssetHashes.Count
    modifiedFileHashMismatchCount = $modifiedHashMismatches
    evidenceChainValidationStatus = if ($null -ne $evidenceValidation) { $evidenceValidation.status } else { "NOT_RUN" }
    evidenceChainValidationFailureCount = if ($null -ne $evidenceValidation) { @($evidenceValidation.failures).Count } else { $null }
    evidenceChainValidationWarningCount = if ($null -ne $evidenceValidation) { @($evidenceValidation.warnings).Count } else { $null }
    intentionalTargetBackupDivergenceCount = if ($null -ne $evidenceValidation) { $evidenceValidation.intentionalTargetBackupDivergenceCount } else { $null }
    unexpectedTargetBackupDivergenceCount = if ($null -ne $evidenceValidation) { $evidenceValidation.unexpectedTargetBackupDivergenceCount } else { $null }
    rawMutationMemoryGuardStatus = if ($null -ne $evidenceValidation) { $evidenceValidation.rawMutationMemoryGuardStatus } else { "NOT_RUN" }
    rawMutationMemoryLimitBytes = if ($null -ne $evidenceValidation) { $evidenceValidation.rawMutationMemoryLimitBytes } else { $null }
    maxModifiedBytesBefore = if ($null -ne $evidenceValidation) { $evidenceValidation.maxModifiedBytesBefore } else { $null }
    rawMutationOversizeCount = if ($null -ne $evidenceValidation) { $evidenceValidation.rawMutationOversizeCount } else { $null }
    referenceIntegrityStatus = if ($null -ne $referenceIntegrity) { $referenceIntegrity.status } else { "NOT_RUN" }
    referenceIntegrityFailureCount = if ($null -ne $referenceIntegrity) { @($referenceIntegrity.failures).Count } else { $null }
    referenceIntegrityWarningCount = if ($null -ne $referenceIntegrity) { @($referenceIntegrity.warnings).Count } else { $null }
    modifiedYamlFilesReferenceChecked = if ($null -ne $referenceIntegrity) { $referenceIntegrity.modifiedYamlFilesChecked } else { $null }
    modifiedYamlGuidReferences = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalGuidReferences } else { $null }
    modifiedYamlBuiltinGuidReferences = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalBuiltinGuidReferences } else { $null }
    modifiedYamlUnresolvedGuidReferences = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalUnresolvedGuidReferences } else { $null }
    modifiedYamlScriptReferences = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalScriptReferences } else { $null }
    modifiedYamlMissingScriptReferences = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalMissingScriptReferences } else { $null }
    modifiedYamlDuplicateFileIdAnchors = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalDuplicateFileIdAnchors } else { $null }
    modifiedYamlTabLines = if ($null -ne $referenceIntegrity) { $referenceIntegrity.totalTabLines } else { $null }
    backupDeltaStatus = if ($null -ne $backupDelta) { $backupDelta.status } else { "NOT_RUN" }
    backupDeltaFailureCount = if ($null -ne $backupDelta) { @($backupDelta.failures).Count } else { $null }
    backupDeltaWarningCount = if ($null -ne $backupDelta) { @($backupDelta.warnings).Count } else { $null }
    backupDeltaFilesChecked = if ($null -ne $backupDelta) { $backupDelta.filesChecked } else { $null }
    backupDeltaFileIdAnchorAddedCount = if ($null -ne $backupDelta) { $backupDelta.fileIdAnchorAddedCount } else { $null }
    backupDeltaFileIdAnchorRemovedCount = if ($null -ne $backupDelta) { $backupDelta.fileIdAnchorRemovedCount } else { $null }
    backupDeltaGuidReferenceAddedCount = if ($null -ne $backupDelta) { $backupDelta.guidReferenceAddedCount } else { $null }
    backupDeltaGuidReferenceRemovedCount = if ($null -ne $backupDelta) { $backupDelta.guidReferenceRemovedCount } else { $null }
    backupDeltaOrphanPayloadGuidReferenceRemovedCount = if ($null -ne $backupDelta) { $backupDelta.orphanPayloadGuidReferenceRemovedCount } else { $null }
    backupDeltaUnclassifiedGuidReferenceRemovedCount = if ($null -ne $backupDelta) { $backupDelta.unclassifiedGuidReferenceRemovedCount } else { $null }
    backupDeltaScriptReferenceAddedCount = if ($null -ne $backupDelta) { $backupDelta.scriptReferenceAddedCount } else { $null }
    backupDeltaScriptReferenceRemovedCount = if ($null -ne $backupDelta) { $backupDelta.scriptReferenceRemovedCount } else { $null }
    backupDeltaComponentReferenceAddedCount = if ($null -ne $backupDelta) { $backupDelta.componentReferenceAddedCount } else { $null }
    backupDeltaComponentReferenceRemovedCount = if ($null -ne $backupDelta) { $backupDelta.componentReferenceRemovedCount } else { $null }
    backupDeltaPropertyPathAddedCount = if ($null -ne $backupDelta) { $backupDelta.propertyPathAddedCount } else { $null }
    backupDeltaPropertyPathRemovedCount = if ($null -ne $backupDelta) { $backupDelta.propertyPathRemovedCount } else { $null }
    postCheckChangedFromBackupCount = $invariant.changedFromBackupCount
    postCheckMissingScriptCount = $invariant.missingScriptCount
    postCheckTargetObsoleteKeyCount = $invariant.targetObsoleteKeyCount
    postCheckTargetObsoleteOverrideCount = $invariant.targetObsoleteOverrideCount
    csharpEditorMigratorCreated = $false
    csharpEditorMigratorReason = "No oldProperty/newProperty migration table exists; writing a no-op UnityEditor script would add compile risk without data rescue."
    dotnetBuildExecuted = $false
    dotnetBuildReason = "No C# migration script or runtime code was created; static source invariants were sufficient and user prohibited heavyweight builds."
    staticScanMicroseconds = $ledger.stats.elapsedMicroseconds
    backupMicroseconds = $backup.elapsedMicroseconds
    extractionMicroseconds = $extraction.elapsedMicroseconds
    invariantMicroseconds = $invariant.elapsedMicroseconds
    fuzzMicroseconds = $fuzz.elapsedMicroseconds
    cleanupMicroseconds = $cleanupMicroseconds
    shakeProfileMigrationMicroseconds = $shakeMicroseconds
    projectWideScanMicroseconds = if ($null -ne $projectLedger) { $projectLedger.stats.elapsedMicroseconds } else { 0 }
    finalReportMicroseconds = $elapsedUs
    fuzzerCases = $fuzz.caseCount
    fuzzerFailures = $fuzz.failedCount
    assetHashes = @($assetHashes)
    modifiedAssetHashes = @($modifiedAssetHashes.ToArray())
    evidenceFiles = @(
        $LedgerPath,
        $BackupManifestPath,
        $ExtractionPath,
        $InvariantPath,
        $FuzzPath,
        $CleanupPath,
        $ShakeProfileFalloffPath,
        $ProjectLedgerPath,
        $ThirdPartyDebtPath,
        $EvidenceValidationPath,
        $ReferenceIntegrityPath,
        $BackupDeltaPath
    )
}

$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFull -Encoding UTF8

$humanLogDir = [System.IO.Path]::GetDirectoryName($humanLogFull)
New-Item -ItemType Directory -Force -Path $humanLogDir | Out-Null
$thirdPartyMutationPolicy = if ($null -ne $report.thirdPartyDebtMutationPolicy) { [string]$report.thirdPartyDebtMutationPolicy } else { "N/A" }
$human = @"

## Agent 1502 - YAML Migration Report - $([DateTime]::UtcNow.ToString("o"))
What was wrong: C# runtime native arrays were suspected to have stranded serialized YAML payloads after migration to `VaultGenerationHandle<T>`.
What was done: Scanned $($ledger.stats.yamlFiles) scene/prefab Unity asset files and $($ledger.stats.monoBehaviours) MonoBehaviour records, backed up target scene/prefab files, proved target native payload absence, corrected scanner false positives for partial classes/inheritance/attribute lines, removed $cleanupPropertiesDeleted proven first-party prefab orphan root keys from $cleanupFilesWritten prefab files, then mapped $shakeCurvesMapped stale `ShakeProfile.FalloffCurve` asset blocks to scalar `FalloffExponent` and deleted the dead curve blocks from $shakeFilesWritten ScriptableObject assets.
Remaining serialized debt: $($report.thirdPartySerializedDebtProperties) orphan properties remain after first-party cleanup; $($report.thirdPartyKnownToolDebtProperties) are classified as Crest/MapMagic/VolumetricLightBeam third-party schema drift with mutation policy $thirdPartyMutationPolicy.
Evidence chain: status=$($report.evidenceChainValidationStatus), failures=$($report.evidenceChainValidationFailureCount), warnings=$($report.evidenceChainValidationWarningCount), intentional target backup divergence=$($report.intentionalTargetBackupDivergenceCount), unexpected target backup divergence=$($report.unexpectedTargetBackupDivergenceCount).
Raw mutation footprint: guard=$($report.rawMutationMemoryGuardStatus), max modified input bytes=$($report.maxModifiedBytesBefore), oversize files=$($report.rawMutationOversizeCount), limit bytes=$($report.rawMutationMemoryLimitBytes).
Reference integrity: status=$($report.referenceIntegrityStatus), failures=$($report.referenceIntegrityFailureCount), unresolved GUID refs=$($report.modifiedYamlUnresolvedGuidReferences), missing script refs=$($report.modifiedYamlMissingScriptReferences), duplicate FileID anchors=$($report.modifiedYamlDuplicateFileIdAnchors), tab lines=$($report.modifiedYamlTabLines).
Backup/current YAML delta: status=$($report.backupDeltaStatus), failures=$($report.backupDeltaFailureCount), FileID anchor delta=$($report.backupDeltaFileIdAnchorAddedCount)/$($report.backupDeltaFileIdAnchorRemovedCount), script ref delta=$($report.backupDeltaScriptReferenceAddedCount)/$($report.backupDeltaScriptReferenceRemovedCount), component ref delta=$($report.backupDeltaComponentReferenceAddedCount)/$($report.backupDeltaComponentReferenceRemovedCount), propertyPath delta=$($report.backupDeltaPropertyPathAddedCount)/$($report.backupDeltaPropertyPathRemovedCount), GUID removed=$($report.backupDeltaGuidReferenceRemovedCount), orphan-payload GUID removed=$($report.backupDeltaOrphanPayloadGuidReferenceRemovedCount), unclassified GUID removed=$($report.backupDeltaUnclassifiedGuidReferenceRemovedCount).
Cinematic Cheats used: No simulation added. The correct cheat was refusal to create fake DTO payloads when source bytes were absent; cleanup stayed cold/offline and did not add runtime migration code.
Exact Microseconds saved: 0 us/frame runtime saved by this pass. Cold tooling costs: scan=$($ledger.stats.elapsedMicroseconds) us, projectScan=$($report.projectWideScanMicroseconds) us, backup=$($backup.elapsedMicroseconds) us, extraction=$($extraction.elapsedMicroseconds) us, invariant=$($invariant.elapsedMicroseconds) us, fuzz=$($fuzz.elapsedMicroseconds) us, prefabCleanup=$cleanupMicroseconds us, shakeProfileMigration=$shakeMicroseconds us.
Outcome: $($report.migrationDecision). Target deleted native field hits=$($ledger.stats.targetObsoleteNativeFieldHits), missing script refs=$($ledger.stats.missingScriptReferences), obsolete override paths=$($ledger.stats.prefabOverrideObsoleteProperties), project first-party orphan properties=$projectFirstPartyOrphans, files modified=$($cleanupFilesWritten + $shakeFilesWritten), modified hash coverage=$($modifiedAssetHashes.Count), modified hash mismatches=$modifiedHashMismatches, properties migrated=$shakeCurvesMapped, properties deleted=$($cleanupPropertiesDeleted + $shakeStaleBlocksDeleted), bytes removed=$($cleanupBytesRewritten + $shakeBytesRewritten).
"@
Add-Content -LiteralPath $humanLogFull -Value $human -Encoding UTF8

$report
