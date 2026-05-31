param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ReportPath = "Docs/Reports/YAML_MIGRATION_REPORT_1502.json",
    [string]$OutputPath = "Docs/AgentLogs/YamlEvidenceValidation_1502.json",
    [int64]$RawMutationMemoryLimitBytes = 104857600,
    [switch]$FailOnError
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

function Add-Failure {
    param(
        [System.Collections.Generic.List[object]]$Failures,
        [string]$Code,
        [string]$Message,
        [string]$Path = ""
    )

    [void]$Failures.Add([pscustomobject]@{
        code = $Code
        message = $Message
        path = $Path
    })
}

function Add-Warning {
    param(
        [System.Collections.Generic.List[object]]$Warnings,
        [string]$Code,
        [string]$Message,
        [string]$Path = ""
    )

    [void]$Warnings.Add([pscustomobject]@{
        code = $Code
        message = $Message
        path = $Path
    })
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$outputFull = Resolve-UnderRoot $projectFull $OutputPath
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()
$failures = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[object]]::new()

if (!(Test-Path -LiteralPath $reportFull -PathType Leaf)) {
    throw "Final report missing: $reportFull"
}

$report = Get-Content -Raw -LiteralPath $reportFull | ConvertFrom-Json

if ([string]$report.agentId -ne "1502") {
    Add-Failure $failures "AGENT_ID_MISMATCH" "Final report is not for agent 1502." $ReportPath
}
if ([string]$report.evidenceClass -ne "STATIC_SOURCE_FINAL") {
    Add-Failure $failures "EVIDENCE_CLASS_MISMATCH" "Final report evidence class is not STATIC_SOURCE_FINAL." $ReportPath
}
if ([bool]$report.dotnetBuildExecuted) {
    Add-Failure $failures "DOTNET_BUILD_EXECUTED" "Final report says dotnet build executed, violating this pass constraints." $ReportPath
}
if ([int]$report.modifiedFileHashCoverageCount -ne [int]$report.filesModified) {
    Add-Failure $failures "MODIFIED_HASH_COVERAGE_MISMATCH" "Modified file hash coverage does not equal filesModified." $ReportPath
}
if ([int]$report.modifiedFileHashMismatchCount -ne 0) {
    Add-Failure $failures "MODIFIED_HASH_MISMATCHES_PRESENT" "Final report records modified file hash mismatches." $ReportPath
}
if ($null -eq $report.referenceIntegrityStatus -or [string]$report.referenceIntegrityStatus -ne "PASS") {
    Add-Failure $failures "REFERENCE_INTEGRITY_NOT_PASSING" "Final report does not record passing modified YAML reference integrity." $ReportPath
}
if ($null -ne $report.modifiedYamlUnresolvedGuidReferences -and [int]$report.modifiedYamlUnresolvedGuidReferences -ne 0) {
    Add-Failure $failures "UNRESOLVED_GUID_REFERENCES_PRESENT" "Final report records unresolved GUID references in modified YAML." $ReportPath
}
if ($null -ne $report.modifiedYamlMissingScriptReferences -and [int]$report.modifiedYamlMissingScriptReferences -ne 0) {
    Add-Failure $failures "MISSING_SCRIPT_REFERENCES_PRESENT" "Final report records missing script references in modified YAML." $ReportPath
}
if ($null -ne $report.modifiedYamlDuplicateFileIdAnchors -and [int]$report.modifiedYamlDuplicateFileIdAnchors -ne 0) {
    Add-Failure $failures "DUPLICATE_FILE_ID_ANCHORS_PRESENT" "Final report records duplicate FileID anchors in modified YAML." $ReportPath
}
if ($null -ne $report.modifiedYamlTabLines -and [int]$report.modifiedYamlTabLines -ne 0) {
    Add-Failure $failures "YAML_TAB_LINES_PRESENT" "Final report records tab characters in modified YAML." $ReportPath
}
if ($null -eq $report.backupDeltaStatus -or [string]$report.backupDeltaStatus -ne "PASS") {
    Add-Failure $failures "BACKUP_DELTA_NOT_PASSING" "Final report does not record passing backup/current YAML delta validation." $ReportPath
}
if ($null -ne $report.backupDeltaFileIdAnchorAddedCount -and [int]$report.backupDeltaFileIdAnchorAddedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_FILEID_ANCHOR_ADDED" "Backup/current delta reports added FileID anchors." $ReportPath
}
if ($null -ne $report.backupDeltaFileIdAnchorRemovedCount -and [int]$report.backupDeltaFileIdAnchorRemovedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_FILEID_ANCHOR_REMOVED" "Backup/current delta reports removed FileID anchors." $ReportPath
}
if ($null -ne $report.backupDeltaScriptReferenceAddedCount -and [int]$report.backupDeltaScriptReferenceAddedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_SCRIPT_REFERENCE_ADDED" "Backup/current delta reports added script references." $ReportPath
}
if ($null -ne $report.backupDeltaScriptReferenceRemovedCount -and [int]$report.backupDeltaScriptReferenceRemovedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_SCRIPT_REFERENCE_REMOVED" "Backup/current delta reports removed script references." $ReportPath
}
if ($null -ne $report.backupDeltaComponentReferenceAddedCount -and [int]$report.backupDeltaComponentReferenceAddedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_COMPONENT_REFERENCE_ADDED" "Backup/current delta reports added component references." $ReportPath
}
if ($null -ne $report.backupDeltaComponentReferenceRemovedCount -and [int]$report.backupDeltaComponentReferenceRemovedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_COMPONENT_REFERENCE_REMOVED" "Backup/current delta reports removed component references." $ReportPath
}
if ($null -ne $report.backupDeltaPropertyPathAddedCount -and [int]$report.backupDeltaPropertyPathAddedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_PROPERTYPATH_ADDED" "Backup/current delta reports added prefab propertyPath entries." $ReportPath
}
if ($null -ne $report.backupDeltaPropertyPathRemovedCount -and [int]$report.backupDeltaPropertyPathRemovedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_PROPERTYPATH_REMOVED" "Backup/current delta reports removed prefab propertyPath entries." $ReportPath
}
if ($null -ne $report.backupDeltaGuidReferenceAddedCount -and [int]$report.backupDeltaGuidReferenceAddedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_GUID_REFERENCE_ADDED" "Backup/current delta reports added GUID references." $ReportPath
}
if ($null -ne $report.backupDeltaUnclassifiedGuidReferenceRemovedCount -and [int]$report.backupDeltaUnclassifiedGuidReferenceRemovedCount -ne 0) {
    Add-Failure $failures "BACKUP_DELTA_UNCLASSIFIED_GUID_REFERENCE_REMOVED" "Backup/current delta reports GUID removals outside proven orphan payload deletion ranges." $ReportPath
}

$modifiedPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$actualModifiedHashMismatches = 0
$missingModifiedFiles = 0
$missingModifiedBackups = 0
$modifiedBytesRemoved = 0L
$maxModifiedBytesBefore = 0L
$rawMutationOversizeCount = 0
$prefabMutationCount = 0
$shakeMutationCount = 0

foreach ($entry in @($report.modifiedAssetHashes)) {
    if ($null -eq $entry) {
        continue
    }

    $relativePath = [string]$entry.relativePath
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        Add-Failure $failures "EMPTY_MODIFIED_PATH" "Modified asset hash record has an empty relativePath." $ReportPath
        continue
    }

    [void]$modifiedPaths.Add($relativePath)
    $currentPath = Resolve-UnderRoot $projectFull $relativePath
    if (!(Test-Path -LiteralPath $currentPath -PathType Leaf)) {
        $missingModifiedFiles++
        Add-Failure $failures "MODIFIED_FILE_MISSING" "Modified file is missing on disk." $relativePath
        continue
    }

    $currentSha = Get-Sha256 $currentPath
    if ($currentSha -ne [string]$entry.sha256After -or $currentSha -ne [string]$entry.currentSha256 -or ![bool]$entry.currentMatchesReport) {
        $actualModifiedHashMismatches++
        Add-Failure $failures "CURRENT_SHA_MISMATCH" "Current file SHA does not match reported post-mutation SHA." $relativePath
    }

    $backupPath = [string]$entry.backupPath
    if ([string]::IsNullOrWhiteSpace($backupPath) -or !(Test-Path -LiteralPath $backupPath -PathType Leaf)) {
        $missingModifiedBackups++
        Add-Failure $failures "MODIFIED_BACKUP_MISSING" "Backup for modified file is missing." $relativePath
    } else {
        $backupSha = Get-Sha256 $backupPath
        if ($backupSha -ne [string]$entry.sha256Before -or $backupSha -ne [string]$entry.backupSha256 -or ![bool]$entry.backupMatchesBefore) {
            Add-Failure $failures "BACKUP_SHA_MISMATCH" "Backup SHA does not match reported pre-mutation SHA." $relativePath
        }
    }

    $modifiedBytesRemoved += ([int64]$entry.bytesBefore - [int64]$entry.bytesAfter)
    if ([int64]$entry.bytesBefore -gt $maxModifiedBytesBefore) {
        $maxModifiedBytesBefore = [int64]$entry.bytesBefore
    }
    if ([int64]$entry.bytesBefore -gt $RawMutationMemoryLimitBytes) {
        $rawMutationOversizeCount++
        Add-Failure $failures "RAW_MUTATION_FILE_TOO_LARGE" "Raw-mutated file exceeds the configured memory-safety limit for whole-file text mutation." $relativePath
    }
    if ($null -ne $entry.missingScriptCount -and [int]$entry.missingScriptCount -ne 0) {
        Add-Failure $failures "MISSING_SCRIPT_AFTER_MUTATION" "Mutated file reports missing script references." $relativePath
    }

    switch ([string]$entry.mutationKind) {
        "PREFAB_ORPHAN_ROOT_PROPERTY_DELETE" {
            $prefabMutationCount++
            if ($null -ne $entry.prefabGameObjectBlock -and ![bool]$entry.prefabGameObjectBlock) {
                Add-Failure $failures "PREFAB_GAMEOBJECT_BLOCK_MISSING" "Prefab mutation record lacks GameObject block proof." $relativePath
            }
        }
        "SHAKEPROFILE_FALLOFF_CURVE_TO_EXPONENT" {
            $shakeMutationCount++
            if ($null -ne $entry.falloffCurveCount -and [int]$entry.falloffCurveCount -ne 0) {
                Add-Failure $failures "SHAKE_FALLOFF_CURVE_REMAINED" "ShakeProfile migration record still has FalloffCurve." $relativePath
            }
            if ($null -ne $entry.falloffExponentCount -and [int]$entry.falloffExponentCount -ne 1) {
                Add-Failure $failures "SHAKE_FALLOFF_EXPONENT_COUNT_INVALID" "ShakeProfile migration record does not have exactly one FalloffExponent." $relativePath
            }
        }
        default {
            Add-Warning $warnings "UNKNOWN_MUTATION_KIND" "Unknown mutation kind in modified asset hash record." $relativePath
        }
    }
}

if ($modifiedPaths.Count -ne [int]$report.filesModified) {
    Add-Failure $failures "MODIFIED_UNIQUE_COUNT_MISMATCH" "Unique modified path count does not equal filesModified." $ReportPath
}
if ($prefabMutationCount -ne [int]$report.prefabFilesModified) {
    Add-Failure $failures "PREFAB_MUTATION_COUNT_MISMATCH" "Prefab mutation records do not match prefabFilesModified." $ReportPath
}
if ($shakeMutationCount -ne [int]$report.shakeProfileFilesModified) {
    Add-Failure $failures "SHAKE_MUTATION_COUNT_MISMATCH" "ShakeProfile mutation records do not match shakeProfileFilesModified." $ReportPath
}
if ($modifiedBytesRemoved -ne [int64]$report.bytesRewritten) {
    Add-Failure $failures "BYTES_REWRITTEN_MISMATCH" "Sum of modified file byte deltas does not equal bytesRewritten." $ReportPath
}

$intentionalTargetBackupDivergence = 0
$unexpectedTargetBackupDivergence = 0
foreach ($entry in @($report.assetHashes)) {
    if ($null -eq $entry) {
        continue
    }

    if ([string]$entry.backupParity -eq "CHANGED_FROM_BACKUP") {
        if ($modifiedPaths.Contains([string]$entry.relativePath)) {
            $intentionalTargetBackupDivergence++
        } else {
            $unexpectedTargetBackupDivergence++
            Add-Failure $failures "UNEXPECTED_TARGET_BACKUP_DIVERGENCE" "Audited target changed from backup but is not in modifiedAssetHashes." ([string]$entry.relativePath)
        }
    }

    if ($null -ne $entry.missingScriptCount -and [int]$entry.missingScriptCount -ne 0) {
        Add-Failure $failures "AUDITED_TARGET_MISSING_SCRIPT" "Audited target reports missing script references." ([string]$entry.relativePath)
    }
}

if ([int]$report.projectWideFirstPartyOrphanProperties -ne 0) {
    Add-Failure $failures "FIRST_PARTY_ORPHANS_REMAIN" "Project-wide first-party orphan properties remain." $ReportPath
}
if ([int]$report.projectWideMissingScriptReferences -ne 0) {
    Add-Failure $failures "PROJECT_MISSING_SCRIPTS_REMAIN" "Project-wide missing script references remain." $ReportPath
}
if ([int]$report.projectWideTargetObsoleteNativeFieldHits -ne 0) {
    Add-Failure $failures "TARGET_OBSOLETE_NATIVE_FIELDS_REMAIN" "Project-wide target obsolete native field hits remain." $ReportPath
}

if ([int]$report.thirdPartySerializedDebtProperties -ne [int]$report.thirdPartyKnownToolDebtProperties) {
    Add-Failure $failures "THIRD_PARTY_DEBT_CLASSIFICATION_INCOMPLETE" "Third-party debt classification does not cover all remaining orphan properties." $ReportPath
}
if ([int]$report.thirdPartyFirstPartyDebtProperties -ne 0) {
    Add-Failure $failures "THIRD_PARTY_DEBT_HAS_FIRST_PARTY" "Third-party debt report still contains first-party orphan count." $ReportPath
}
$scriptOwnerSum = 0
foreach ($owner in @($report.thirdPartyDebtByScriptOwner)) {
    if ($null -ne $owner) {
        $scriptOwnerSum += [int]$owner.count
    }
}
if ($scriptOwnerSum -ne [int]$report.thirdPartySerializedDebtProperties) {
    Add-Failure $failures "THIRD_PARTY_OWNER_SUM_MISMATCH" "Third-party script-owner rows do not sum to total serialized debt." $ReportPath
}

foreach ($relative in @($report.evidenceFiles)) {
    if ($null -eq $relative) {
        continue
    }

    $evidencePath = Resolve-UnderRoot $projectFull ([string]$relative)
    if (!(Test-Path -LiteralPath $evidencePath -PathType Leaf)) {
        Add-Failure $failures "EVIDENCE_FILE_MISSING" "Report references missing evidence file." ([string]$relative)
    }
}

$humanLogPath = Resolve-UnderRoot $projectFull "Docs/AgentLogs/LOG_1502.md"
if (!(Test-Path -LiteralPath $humanLogPath -PathType Leaf)) {
    Add-Failure $failures "HUMAN_LOG_MISSING" "LOG_1502.md is missing." "Docs/AgentLogs/LOG_1502.md"
} else {
    $humanLog = Get-Content -Raw -LiteralPath $humanLogPath
    if ($humanLog -match '\$\(@\{agentId=1502') {
        Add-Failure $failures "MALFORMED_HUMAN_LOG_INTERPOLATION" "LOG_1502.md contains a malformed PowerShell object interpolation block." "Docs/AgentLogs/LOG_1502.md"
    }
    if ($humanLog -notmatch 'Remaining serialized debt: 158 orphan properties') {
        Add-Warning $warnings "LATEST_HUMAN_LOG_LACKS_THIRD_PARTY_DEBT" "LOG_1502.md does not contain the third-party debt summary text." "Docs/AgentLogs/LOG_1502.md"
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$validation = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_EVIDENCE_CHAIN_VALIDATION"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    sourceReport = $ReportPath
    status = if ($failures.Count -eq 0) { "PASS" } else { "FAIL" }
    failures = @($failures.ToArray())
    warnings = @($warnings.ToArray())
    modifiedFilesReported = [int]$report.filesModified
    modifiedUniquePathCount = $modifiedPaths.Count
    missingModifiedFiles = $missingModifiedFiles
    missingModifiedBackups = $missingModifiedBackups
    actualModifiedHashMismatches = $actualModifiedHashMismatches
    intentionalTargetBackupDivergenceCount = $intentionalTargetBackupDivergence
    unexpectedTargetBackupDivergenceCount = $unexpectedTargetBackupDivergence
    prefabMutationRecords = $prefabMutationCount
    shakeProfileMutationRecords = $shakeMutationCount
    bytesRemovedFromModifiedRecords = $modifiedBytesRemoved
    rawMutationMemoryLimitBytes = $RawMutationMemoryLimitBytes
    maxModifiedBytesBefore = $maxModifiedBytesBefore
    rawMutationOversizeCount = $rawMutationOversizeCount
    rawMutationMemoryGuardStatus = if ($rawMutationOversizeCount -eq 0) { "PASS" } else { "FAIL" }
    thirdPartyScriptOwnerCountSum = $scriptOwnerSum
    elapsedMicroseconds = $elapsedUs
}

$outputDir = [System.IO.Path]::GetDirectoryName($outputFull)
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFull -Encoding UTF8

if ($FailOnError -and $failures.Count -gt 0) {
    throw "Migration evidence validation failed with $($failures.Count) failure(s). See $outputFull"
}

$validation
