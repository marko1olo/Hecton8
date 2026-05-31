param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ReportPath = "Docs/Reports/YAML_MIGRATION_REPORT_1502.json",
    [string]$CleanupPath = "Docs/AgentLogs/YamlCleanup_1502.json",
    [string]$OutputPath = "Docs/AgentLogs/YamlBackupDelta_1502.json",
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

function Add-MultisetValue {
    param(
        [hashtable]$Map,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        $Value = "<empty>"
    }

    if (!$Map.ContainsKey($Value)) {
        $Map[$Value] = 0
    }

    $Map[$Value]++
}

function Compare-Multiset {
    param(
        [string]$Name,
        [hashtable]$Before,
        [hashtable]$After
    )

    $added = [System.Collections.Generic.List[object]]::new()
    $removed = [System.Collections.Generic.List[object]]::new()
    $keys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in $Before.Keys) {
        [void]$keys.Add([string]$key)
    }
    foreach ($key in $After.Keys) {
        [void]$keys.Add([string]$key)
    }

    foreach ($key in $keys) {
        $beforeCount = if ($Before.ContainsKey($key)) { [int]$Before[$key] } else { 0 }
        $afterCount = if ($After.ContainsKey($key)) { [int]$After[$key] } else { 0 }
        $delta = $afterCount - $beforeCount
        if ($delta -gt 0) {
            [void]$added.Add([pscustomobject]@{ value = $key; count = $delta })
        } elseif ($delta -lt 0) {
            [void]$removed.Add([pscustomobject]@{ value = $key; count = -$delta })
        }
    }

    $addedTotal = ($added | Measure-Object -Property count -Sum).Sum
    $removedTotal = ($removed | Measure-Object -Property count -Sum).Sum
    if ($null -eq $addedTotal) {
        $addedTotal = 0
    }
    if ($null -eq $removedTotal) {
        $removedTotal = 0
    }

    return [pscustomobject]@{
        name = $Name
        beforeUnique = $Before.Count
        afterUnique = $After.Count
        addedCount = [int]$addedTotal
        removedCount = [int]$removedTotal
        added = @($added.ToArray())
        removed = @($removed.ToArray())
    }
}

function Get-OrCreateNestedMultiset {
    param(
        [hashtable]$Outer,
        [string]$Key
    )

    if (!$Outer.ContainsKey($Key)) {
        $Outer[$Key] = @{}
    }

    return $Outer[$Key]
}

function Add-MultisetValues {
    param(
        [hashtable]$Target,
        [hashtable]$Source
    )

    foreach ($key in $Source.Keys) {
        if (!$Target.ContainsKey($key)) {
            $Target[$key] = 0
        }

        $Target[$key] += [int]$Source[$key]
    }
}

function Get-GuidRefsFromLines {
    param([string[]]$Lines)

    $refs = @{}
    foreach ($line in $Lines) {
        foreach ($guidMatch in [regex]::Matches($line, 'guid:\s*([0-9a-fA-F]{32})')) {
            Add-MultisetValue $refs ($guidMatch.Groups[1].Value.ToLowerInvariant())
        }
    }

    return $refs
}

function Get-AllowedOrphanPayloadGuidRemovals {
    param(
        [string]$CleanupFullPath
    )

    $allowed = @{}
    if (!(Test-Path -LiteralPath $CleanupFullPath -PathType Leaf)) {
        return $allowed
    }

    $cleanup = Get-Content -Raw -LiteralPath $CleanupFullPath | ConvertFrom-Json
    if ($null -eq $cleanup -or !$cleanup.apply) {
        return $allowed
    }

    foreach ($file in @($cleanup.files)) {
        if ($null -eq $file -or ![bool]$file.applied) {
            continue
        }

        $relativePath = [string]$file.relativePath
        $backupPath = [string]$file.backupPath
        if ([string]::IsNullOrWhiteSpace($relativePath) -or [string]::IsNullOrWhiteSpace($backupPath)) {
            continue
        }
        if (!(Test-Path -LiteralPath $backupPath -PathType Leaf)) {
            continue
        }

        $backupLines = [System.IO.File]::ReadAllLines($backupPath)
        $fileAllowed = Get-OrCreateNestedMultiset $allowed $relativePath
        foreach ($removed in @($file.removedProperties)) {
            if ($null -eq $removed) {
                continue
            }

            $originalLine = [int]$removed.originalLine
            $removedLineCount = [int]$removed.removedLineCount
            if ($originalLine -le 0 -or $removedLineCount -le 0) {
                continue
            }

            $startIndex = $originalLine - 1
            if ($startIndex -lt 0 -or $startIndex -ge $backupLines.Count) {
                continue
            }

            $lineCount = [Math]::Min($removedLineCount, $backupLines.Count - $startIndex)
            $removedLines = [string[]]::new($lineCount)
            [Array]::Copy($backupLines, $startIndex, $removedLines, 0, $lineCount)
            $removedGuidRefs = Get-GuidRefsFromLines $removedLines
            Add-MultisetValues $fileAllowed $removedGuidRefs
        }
    }

    return $allowed
}

function Resolve-GuidRemovalClassification {
    param(
        [object]$GuidDelta,
        [hashtable]$AllowedRemovedRefs
    )

    $allowedCount = 0
    $unclassified = [System.Collections.Generic.List[object]]::new()

    foreach ($removed in @($GuidDelta.removed)) {
        if ($null -eq $removed) {
            continue
        }

        $value = [string]$removed.value
        $count = [int]$removed.count
        $allowedForGuid = if ($AllowedRemovedRefs.ContainsKey($value)) { [int]$AllowedRemovedRefs[$value] } else { 0 }
        if ($allowedForGuid -ge $count) {
            $allowedCount += $count
        } else {
            if ($allowedForGuid -gt 0) {
                $allowedCount += $allowedForGuid
            }

            [void]$unclassified.Add([pscustomobject]@{
                value = $value
                count = $count - $allowedForGuid
            })
        }
    }

    $unclassifiedTotal = ($unclassified | Measure-Object -Property count -Sum).Sum
    if ($null -eq $unclassifiedTotal) {
        $unclassifiedTotal = 0
    }

    return [pscustomobject]@{
        orphanPayloadGuidReferenceRemovedCount = $allowedCount
        unclassifiedGuidReferenceRemovedCount = [int]$unclassifiedTotal
        unclassifiedRemoved = @($unclassified.ToArray())
    }
}

function Get-YamlDeltaSummary {
    param([string]$Path)

    $lines = [System.IO.File]::ReadAllLines($Path)
    $fileIdAnchors = @{}
    $guidRefs = @{}
    $scriptRefs = @{}
    $componentRefs = @{}
    $propertyPaths = @{}
    $monoBehaviourCount = 0
    $gameObjectCount = 0
    $falloffCurveCount = 0
    $falloffExponentCount = 0
    $lineNumber = 0

    foreach ($line in $lines) {
        $lineNumber++
        $anchorMatch = [regex]::Match($line, '^--- !u!([0-9]+) &(-?[0-9]+)\s*$')
        if ($anchorMatch.Success) {
            $classId = $anchorMatch.Groups[1].Value
            $fileId = $anchorMatch.Groups[2].Value
            Add-MultisetValue $fileIdAnchors "$($classId):$fileId"
            if ($classId -eq "114") {
                $monoBehaviourCount++
            } elseif ($classId -eq "1") {
                $gameObjectCount++
            }
        }

        foreach ($guidMatch in [regex]::Matches($line, 'guid:\s*([0-9a-fA-F]{32})')) {
            Add-MultisetValue $guidRefs ($guidMatch.Groups[1].Value.ToLowerInvariant())
        }

        $scriptMatch = [regex]::Match($line, 'm_Script:\s*\{fileID:\s*(-?[0-9]+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*([0-9]+)\}')
        if ($scriptMatch.Success) {
            Add-MultisetValue $scriptRefs ("{0}:{1}:{2}" -f $scriptMatch.Groups[1].Value, $scriptMatch.Groups[2].Value.ToLowerInvariant(), $scriptMatch.Groups[3].Value)
        }

        $componentMatch = [regex]::Match($line, '^\s*-\s*component:\s*\{fileID:\s*(-?[0-9]+)\}')
        if ($componentMatch.Success) {
            Add-MultisetValue $componentRefs $componentMatch.Groups[1].Value
        }

        $propertyPathMatch = [regex]::Match($line, '^\s*propertyPath:\s*(.*)$')
        if ($propertyPathMatch.Success) {
            Add-MultisetValue $propertyPaths ($propertyPathMatch.Groups[1].Value.Trim())
        }

        if ($line -match '^\s*FalloffCurve:') {
            $falloffCurveCount++
        }
        if ($line -match '^\s*FalloffExponent:') {
            $falloffExponentCount++
        }
    }

    return [pscustomobject]@{
        lineCount = $lines.Count
        fileIdAnchors = $fileIdAnchors
        guidRefs = $guidRefs
        scriptRefs = $scriptRefs
        componentRefs = $componentRefs
        propertyPaths = $propertyPaths
        monoBehaviourCount = $monoBehaviourCount
        gameObjectCount = $gameObjectCount
        falloffCurveCount = $falloffCurveCount
        falloffExponentCount = $falloffExponentCount
    }
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$cleanupFull = Resolve-UnderRoot $projectFull $CleanupPath
$outputFull = Resolve-UnderRoot $projectFull $OutputPath
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()
$failures = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[object]]::new()
$fileReports = [System.Collections.Generic.List[object]]::new()

if (!(Test-Path -LiteralPath $reportFull -PathType Leaf)) {
    throw "Final report missing: $reportFull"
}

$report = Get-Content -Raw -LiteralPath $reportFull | ConvertFrom-Json
$allowedOrphanPayloadGuidRemovals = Get-AllowedOrphanPayloadGuidRemovals $cleanupFull
$filesChecked = 0
$guidAddedTotal = 0
$guidRemovedTotal = 0
$orphanPayloadGuidRemovedTotal = 0
$unclassifiedGuidRemovedTotal = 0
$scriptAddedTotal = 0
$scriptRemovedTotal = 0
$componentAddedTotal = 0
$componentRemovedTotal = 0
$anchorAddedTotal = 0
$anchorRemovedTotal = 0
$propertyPathAddedTotal = 0
$propertyPathRemovedTotal = 0

foreach ($entry in @($report.modifiedAssetHashes)) {
    if ($null -eq $entry) {
        continue
    }

    $relativePath = [string]$entry.relativePath
    $currentPath = Resolve-UnderRoot $projectFull $relativePath
    $backupPath = [string]$entry.backupPath

    if (!(Test-Path -LiteralPath $currentPath -PathType Leaf)) {
        Add-Failure $failures "CURRENT_FILE_MISSING" "Current modified file is missing." $relativePath
        continue
    }
    if ([string]::IsNullOrWhiteSpace($backupPath) -or !(Test-Path -LiteralPath $backupPath -PathType Leaf)) {
        Add-Failure $failures "BACKUP_FILE_MISSING" "Backup file is missing." $relativePath
        continue
    }

    $filesChecked++
    $before = Get-YamlDeltaSummary $backupPath
    $after = Get-YamlDeltaSummary $currentPath
    $anchorDelta = Compare-Multiset "fileIdAnchors" $before.fileIdAnchors $after.fileIdAnchors
    $guidDelta = Compare-Multiset "guidRefs" $before.guidRefs $after.guidRefs
    $scriptDelta = Compare-Multiset "scriptRefs" $before.scriptRefs $after.scriptRefs
    $componentDelta = Compare-Multiset "componentRefs" $before.componentRefs $after.componentRefs
    $propertyPathDelta = Compare-Multiset "propertyPaths" $before.propertyPaths $after.propertyPaths

    $anchorAddedTotal += [int]$anchorDelta.addedCount
    $anchorRemovedTotal += [int]$anchorDelta.removedCount
    $guidAddedTotal += [int]$guidDelta.addedCount
    $guidRemovedTotal += [int]$guidDelta.removedCount
    $scriptAddedTotal += [int]$scriptDelta.addedCount
    $scriptRemovedTotal += [int]$scriptDelta.removedCount
    $componentAddedTotal += [int]$componentDelta.addedCount
    $componentRemovedTotal += [int]$componentDelta.removedCount
    $propertyPathAddedTotal += [int]$propertyPathDelta.addedCount
    $propertyPathRemovedTotal += [int]$propertyPathDelta.removedCount

    foreach ($delta in @($anchorDelta, $scriptDelta, $componentDelta, $propertyPathDelta)) {
        if ([int]$delta.addedCount -ne 0 -or [int]$delta.removedCount -ne 0) {
            Add-Failure $failures ("STRUCTURAL_DELTA_" + $delta.name.ToUpperInvariant()) "Structural YAML multiset changed across backup/current." $relativePath
        }
    }

    $allowedGuidRefsForFile = if ($allowedOrphanPayloadGuidRemovals.ContainsKey($relativePath)) { $allowedOrphanPayloadGuidRemovals[$relativePath] } else { @{} }
    $guidRemovalClassification = Resolve-GuidRemovalClassification $guidDelta $allowedGuidRefsForFile
    $orphanPayloadGuidRemovedTotal += [int]$guidRemovalClassification.orphanPayloadGuidReferenceRemovedCount
    $unclassifiedGuidRemovedTotal += [int]$guidRemovalClassification.unclassifiedGuidReferenceRemovedCount

    if ([int]$guidDelta.addedCount -ne 0) {
        Add-Failure $failures "GUID_REFERENCE_ADDED" "GUID reference was added across backup/current." $relativePath
    }
    if ([int]$guidRemovalClassification.unclassifiedGuidReferenceRemovedCount -ne 0) {
        Add-Failure $failures "GUID_REFERENCE_REMOVED_OUTSIDE_ORPHAN_PAYLOAD" "GUID reference was removed outside the proven orphan payload deletion ranges." $relativePath
    }

    $mutationKind = [string]$entry.mutationKind
    if ($mutationKind -eq "SHAKEPROFILE_FALLOFF_CURVE_TO_EXPONENT") {
        if ([int]$before.falloffCurveCount -ne 1 -or [int]$after.falloffCurveCount -ne 0) {
            Add-Failure $failures "SHAKE_FALLOFF_CURVE_DELTA_INVALID" "ShakeProfile FalloffCurve before/after counts are not the expected 1 -> 0." $relativePath
        }
        if ([int]$before.falloffExponentCount -ne 0 -or [int]$after.falloffExponentCount -ne 1) {
            Add-Failure $failures "SHAKE_FALLOFF_EXPONENT_DELTA_INVALID" "ShakeProfile FalloffExponent before/after counts are not the expected 0 -> 1." $relativePath
        }
    } else {
        if ([int]$before.falloffCurveCount -ne [int]$after.falloffCurveCount -or [int]$before.falloffExponentCount -ne [int]$after.falloffExponentCount) {
            Add-Failure $failures "UNEXPECTED_FALLOFF_FIELD_DELTA" "Non-ShakeProfile mutation changed falloff field counts." $relativePath
        }
    }

    [void]$fileReports.Add([pscustomobject]@{
        relativePath = $relativePath
        mutationKind = $mutationKind
        lineCountBefore = $before.lineCount
        lineCountAfter = $after.lineCount
        monoBehaviourCountBefore = $before.monoBehaviourCount
        monoBehaviourCountAfter = $after.monoBehaviourCount
        gameObjectCountBefore = $before.gameObjectCount
        gameObjectCountAfter = $after.gameObjectCount
        fileIdAnchorDelta = $anchorDelta
        guidReferenceDelta = $guidDelta
        orphanPayloadGuidReferenceRemovedCount = [int]$guidRemovalClassification.orphanPayloadGuidReferenceRemovedCount
        unclassifiedGuidReferenceRemovedCount = [int]$guidRemovalClassification.unclassifiedGuidReferenceRemovedCount
        unclassifiedRemovedGuidReferences = @($guidRemovalClassification.unclassifiedRemoved)
        scriptReferenceDelta = $scriptDelta
        componentReferenceDelta = $componentDelta
        propertyPathDelta = $propertyPathDelta
        falloffCurveCountBefore = $before.falloffCurveCount
        falloffCurveCountAfter = $after.falloffCurveCount
        falloffExponentCountBefore = $before.falloffExponentCount
        falloffExponentCountAfter = $after.falloffExponentCount
    })
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$validation = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_BACKUP_CURRENT_YAML_DELTA"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    sourceReport = $ReportPath
    status = if ($failures.Count -eq 0) { "PASS" } else { "FAIL" }
    failures = @($failures.ToArray())
    warnings = @($warnings.ToArray())
    filesChecked = $filesChecked
    fileIdAnchorAddedCount = $anchorAddedTotal
    fileIdAnchorRemovedCount = $anchorRemovedTotal
    guidReferenceAddedCount = $guidAddedTotal
    guidReferenceRemovedCount = $guidRemovedTotal
    orphanPayloadGuidReferenceRemovedCount = $orphanPayloadGuidRemovedTotal
    unclassifiedGuidReferenceRemovedCount = $unclassifiedGuidRemovedTotal
    scriptReferenceAddedCount = $scriptAddedTotal
    scriptReferenceRemovedCount = $scriptRemovedTotal
    componentReferenceAddedCount = $componentAddedTotal
    componentReferenceRemovedCount = $componentRemovedTotal
    propertyPathAddedCount = $propertyPathAddedTotal
    propertyPathRemovedCount = $propertyPathRemovedTotal
    files = @($fileReports.ToArray())
    elapsedMicroseconds = $elapsedUs
}

$outputDir = [System.IO.Path]::GetDirectoryName($outputFull)
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$validation | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outputFull -Encoding UTF8

if ($FailOnError -and $failures.Count -gt 0) {
    throw "Backup/current YAML delta validation failed with $($failures.Count) failure(s). See $outputFull"
}

$validation
