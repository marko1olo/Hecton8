param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string[]]$YamlRoots = @(
        "Assets/_Project/Scenes",
        "Assets/_Project/Prefabs"
    ),
    [string]$ReportPath = "Docs/AgentLogs/YamlDesync_1502_Ledger.json"
)

$ErrorActionPreference = "Stop"

$obsoleteNames = @(
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

$unityBuiltIns = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
@(
    "m_ObjectHideFlags",
    "m_CorrespondingSourceObject",
    "m_PrefabInstance",
    "m_PrefabAsset",
    "m_GameObject",
    "m_Enabled",
    "m_Active",
    "m_EditorHideFlags",
    "m_Script",
    "m_Name",
    "m_EditorClassIdentifier",
    "serializedVersion"
) | ForEach-Object { [void]$unityBuiltIns.Add($_) }

function Resolve-PathUnderRoot {
    param([string]$Root, [string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $Path))
}

function Get-RelativePathPortable {
    param(
        [string]$Root,
        [string]$Path
    )

    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $pathFull = [System.IO.Path]::GetFullPath($Path)
    if (!$rootFull.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $rootFull = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    }

    $rootUri = [Uri]::new($rootFull)
    $pathUri = [Uri]::new($pathFull)
    return [Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("/", "\")
}

function Add-ClassSchemaFieldsRecursive {
    param(
        [string]$ClassName,
        [hashtable]$ClassFields,
        [hashtable]$ClassFormerNames,
        [hashtable]$ClassBaseNames,
        [System.Collections.Generic.HashSet[string]]$Fields,
        [System.Collections.Generic.HashSet[string]]$FormerNames,
        [System.Collections.Generic.HashSet[string]]$Visited
    )

    if ([string]::IsNullOrWhiteSpace($ClassName) -or $Visited.Contains($ClassName)) {
        return
    }

    [void]$Visited.Add($ClassName)

    if ($ClassFields.ContainsKey($ClassName)) {
        foreach ($field in $ClassFields[$ClassName]) {
            [void]$Fields.Add($field)
        }
    }

    if ($ClassFormerNames.ContainsKey($ClassName)) {
        foreach ($formerName in $ClassFormerNames[$ClassName]) {
            [void]$FormerNames.Add($formerName)
        }
    }

    if ($ClassBaseNames.ContainsKey($ClassName)) {
        $baseName = $ClassBaseNames[$ClassName]
        if (![string]::IsNullOrWhiteSpace($baseName)) {
            Add-ClassSchemaFieldsRecursive `
                -ClassName $baseName `
                -ClassFields $ClassFields `
                -ClassFormerNames $ClassFormerNames `
                -ClassBaseNames $ClassBaseNames `
                -Fields $Fields `
                -FormerNames $FormerNames `
                -Visited $Visited
        }
    }
}

function Get-CSharpSchemas {
    param([string]$Root)

    $schemas = @{}
    $classFields = @{}
    $classFormerNames = @{}
    $classBaseNames = @{}
    $scriptFiles = Get-ChildItem -LiteralPath (Join-Path $Root "Assets") -Recurse -File -Filter "*.cs"
    foreach ($scriptFile in $scriptFiles) {
        $metaPath = "$($scriptFile.FullName).meta"
        if (!(Test-Path -LiteralPath $metaPath)) {
            continue
        }

        $guidLine = Select-String -LiteralPath $metaPath -Pattern "^guid:\s*([0-9a-fA-F]+)" -List
        if ($null -eq $guidLine) {
            continue
        }

        $guid = $guidLine.Matches[0].Groups[1].Value.ToLowerInvariant()
        $fields = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $formerNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $pendingSerialize = $false
        $pendingNonSerialized = $false
        $pendingFormer = New-Object System.Collections.Generic.List[string]
        $primaryClass = $null
        $fileBaseName = [System.IO.Path]::GetFileNameWithoutExtension($scriptFile.Name)
        $lineNumber = 0

        $reader = [System.IO.StreamReader]::new($scriptFile.FullName)
        try {
            while (($line = $reader.ReadLine()) -ne $null) {
                $lineNumber++
                $trimmed = $line.Trim()

                $classMatch = [regex]::Match($line, "\b(?:class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:\s*<[^>]+>)?\s*(?:\:\s*(?<bases>[^{]+))?")
                if ($classMatch.Success) {
                    $className = $classMatch.Groups["name"].Value
                    if ($null -eq $primaryClass -or $className -eq $fileBaseName) {
                        $primaryClass = $className
                    }

                    $bases = $classMatch.Groups["bases"].Value
                    if (![string]::IsNullOrWhiteSpace($bases)) {
                        $baseToken = $bases.Split(",")[0].Trim()
                        $baseMatch = [regex]::Match($baseToken, "(?:[A-Za-z_][A-Za-z0-9_]*\.)*(?<base>[A-Za-z_][A-Za-z0-9_]*)")
                        if ($baseMatch.Success) {
                            $classBaseNames[$className] = $baseMatch.Groups["base"].Value
                        }
                    }
                }

                if ($trimmed -match "\bSerializeField\b" -or $trimmed -match "\bSerializeReference\b") {
                    $pendingSerialize = $true
                }

                if ($trimmed -match "\b(?:System\.)?NonSerialized\b") {
                    $pendingNonSerialized = $true
                }

                $formerMatches = [regex]::Matches($trimmed, "FormerlySerializedAs\s*\(\s*""([^""]+)""\s*\)")
                foreach ($match in $formerMatches) {
                    [void]$pendingFormer.Add($match.Groups[1].Value)
                }

                $fieldMatch = [regex]::Match($line, "^\s*(?:\[[^\]]+\]\s*)*(?<access>(?:(?:public|private|protected|internal)\s+)+)(?:(?:unsafe|new)\s+)*(?<type>[A-Za-z_][A-Za-z0-9_<>,\.\[\]\?:\s]*?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=|;|,)")

                if ($trimmed.StartsWith("[") -and !$fieldMatch.Success) {
                    continue
                }

                if (($pendingSerialize -or $pendingFormer.Count -gt 0 -or $pendingNonSerialized) -and $trimmed -match "^[A-Za-z_][A-Za-z0-9_]*\s*\(" -and !$trimmed.Contains(";")) {
                    if ($trimmed.Contains("]")) {
                        continue
                    }

                    $pendingSerialize = $false
                    $pendingNonSerialized = $false
                    $pendingFormer.Clear()
                    continue
                }

                if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("//")) {
                    continue
                }

                if ($trimmed.Contains("(") -and !$trimmed.Contains(";")) {
                    $pendingSerialize = $false
                    $pendingNonSerialized = $false
                    $pendingFormer.Clear()
                    continue
                }

                $isStaticOrConst = $trimmed -match "\b(static|const|readonly)\b"
                $isPublicField = $fieldMatch.Success -and ($fieldMatch.Groups["access"].Value -match "\bpublic\b")
                $isNonSerialized = $pendingNonSerialized

                if ($fieldMatch.Success -and !$isStaticOrConst -and !$isNonSerialized -and ($pendingSerialize -or $isPublicField)) {
                    $fieldName = $fieldMatch.Groups["name"].Value
                    [void]$fields.Add($fieldName)
                    foreach ($former in $pendingFormer) {
                        [void]$formerNames.Add($former)
                    }
                    $pendingSerialize = $false
                    $pendingNonSerialized = $false
                    $pendingFormer.Clear()
                    continue
                }

                if ($fieldMatch.Success) {
                    $pendingSerialize = $false
                    $pendingNonSerialized = $false
                    $pendingFormer.Clear()
                    continue
                }

                if ($trimmed.EndsWith(";")) {
                    $pendingSerialize = $false
                    $pendingNonSerialized = $false
                    $pendingFormer.Clear()
                }
            }
        }
        finally {
            $reader.Dispose()
        }

        $relativePath = (Get-RelativePathPortable -Root $Root -Path $scriptFile.FullName).Replace("\", "/")
        if (![string]::IsNullOrWhiteSpace($primaryClass)) {
            if (!$classFields.ContainsKey($primaryClass)) {
                $classFields[$primaryClass] = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                $classFormerNames[$primaryClass] = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            }

            foreach ($field in $fields) {
                [void]$classFields[$primaryClass].Add($field)
            }
            foreach ($formerName in $formerNames) {
                [void]$classFormerNames[$primaryClass].Add($formerName)
            }
        }

        $schemas[$guid] = [pscustomobject]@{
            guid = $guid
            path = $relativePath
            class = $primaryClass
            baseClass = if (![string]::IsNullOrWhiteSpace($primaryClass) -and $classBaseNames.ContainsKey($primaryClass)) { $classBaseNames[$primaryClass] } else { $null }
            fields = @($fields)
            formerlySerializedAs = @($formerNames)
        }
    }

    foreach ($guid in @($schemas.Keys)) {
        $schema = $schemas[$guid]
        if ($schema -and ![string]::IsNullOrWhiteSpace($schema.class) -and $classFields.ContainsKey($schema.class)) {
            $mergedFields = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

            $mergedFormerNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            $visitedClasses = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            Add-ClassSchemaFieldsRecursive `
                -ClassName $schema.class `
                -ClassFields $classFields `
                -ClassFormerNames $classFormerNames `
                -ClassBaseNames $classBaseNames `
                -Fields $mergedFields `
                -FormerNames $mergedFormerNames `
                -Visited $visitedClasses

            $schemas[$guid] = [pscustomobject]@{
                guid = $schema.guid
                path = $schema.path
                class = $schema.class
                baseClass = if ($classBaseNames.ContainsKey($schema.class)) { $classBaseNames[$schema.class] } else { $null }
                fields = @($mergedFields)
                formerlySerializedAs = @($mergedFormerNames)
            }
        }
    }

    return $schemas
}

function Test-OrphanCandidate {
    param(
        [string]$PropertyName,
        [object]$Schema
    )

    if ($unityBuiltIns.Contains($PropertyName)) {
        return $false
    }

    if ($null -eq $Schema) {
        return $false
    }

    if ($Schema.fields -contains $PropertyName) {
        return $false
    }

    if ($Schema.formerlySerializedAs -contains $PropertyName) {
        return $false
    }

    return $true
}

function Scan-YamlFile {
    param(
        [string]$Path,
        [hashtable]$Schemas,
        [System.Collections.Generic.HashSet[string]]$ObsoleteSet
    )

    $hits = New-Object System.Collections.Generic.List[object]
    $missingScripts = New-Object System.Collections.Generic.List[object]
    $prefabOverrideHits = New-Object System.Collections.Generic.List[object]
    $monoBehaviourCount = 0

    $currentFileId = $null
    $currentStartLine = 0
    $currentScriptGuid = $null
    $currentSchema = $null
    $insideMonoBehaviour = $false
    $lineNumber = 0

    $reader = [System.IO.StreamReader]::new($Path)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            $lineNumber++

            $monoMatch = [regex]::Match($line, "^--- !u!114 &(-?\d+)")
            if ($monoMatch.Success) {
                $monoBehaviourCount++
                $insideMonoBehaviour = $true
                $currentFileId = $monoMatch.Groups[1].Value
                $currentStartLine = $lineNumber
                $currentScriptGuid = $null
                $currentSchema = $null
                continue
            }

            if ($line.StartsWith("--- ") -and !$monoMatch.Success) {
                $insideMonoBehaviour = $false
                $currentFileId = $null
                $currentScriptGuid = $null
                $currentSchema = $null
            }

            if ($insideMonoBehaviour) {
                if ($line -match "m_Script:\s*\{fileID:\s*0") {
                    [void]$missingScripts.Add([pscustomobject]@{
                        path = $Path
                        line = $lineNumber
                        componentFileID = $currentFileId
                        componentStartLine = $currentStartLine
                    })
                }

                $scriptMatch = [regex]::Match($line, "m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]+)")
                if ($scriptMatch.Success) {
                    $currentScriptGuid = $scriptMatch.Groups[1].Value.ToLowerInvariant()
                    if ($Schemas.ContainsKey($currentScriptGuid)) {
                        $currentSchema = $Schemas[$currentScriptGuid]
                    }
                    continue
                }

                $propertyMatch = [regex]::Match($line, "^  ([A-Za-z_][A-Za-z0-9_]*)\:")
                if ($propertyMatch.Success) {
                    $propertyName = $propertyMatch.Groups[1].Value
                    $isObsolete = $ObsoleteSet.Contains($propertyName)
                    $isOrphan = Test-OrphanCandidate -PropertyName $propertyName -Schema $currentSchema

                    if ($isObsolete -or $isOrphan) {
                        [void]$hits.Add([pscustomobject]@{
                            path = $Path
                            line = $lineNumber
                            componentFileID = $currentFileId
                            componentStartLine = $currentStartLine
                            scriptGuid = $currentScriptGuid
                            scriptPath = if ($null -ne $currentSchema) { $currentSchema.path } else { $null }
                            scriptClass = if ($null -ne $currentSchema) { $currentSchema.class } else { $null }
                            property = $propertyName
                            reason = if ($isObsolete) { "TARGET_OBSOLETE_NATIVE_FIELD" } else { "NOT_IN_CURRENT_CSHARP_SCHEMA" }
                        })
                    }
                }
            }

            $propertyPathMatch = [regex]::Match($line, "^\s*propertyPath:\s*([A-Za-z_][A-Za-z0-9_\.]*)")
            if ($propertyPathMatch.Success) {
                $propertyPath = $propertyPathMatch.Groups[1].Value
                $rootProperty = $propertyPath.Split(".")[0]
                if ($ObsoleteSet.Contains($rootProperty)) {
                    [void]$prefabOverrideHits.Add([pscustomobject]@{
                        path = $Path
                        line = $lineNumber
                        propertyPath = $propertyPath
                        rootProperty = $rootProperty
                        reason = "PREFAB_INSTANCE_OVERRIDE_TARGETS_OBSOLETE_FIELD"
                    })
                }
            }
        }
    }
    finally {
        $reader.Dispose()
    }

    return [pscustomobject]@{
        path = $Path
        monoBehaviourCount = $monoBehaviourCount
        orphanedSerializedProperties = $hits.ToArray()
        missingScriptReferences = $missingScripts.ToArray()
        prefabOverrideObsoleteProperties = $prefabOverrideHits.ToArray()
    }
}

$started = [System.Diagnostics.Stopwatch]::StartNew()
$absoluteProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$absoluteReportPath = Resolve-PathUnderRoot -Root $absoluteProjectRoot -Path $ReportPath
$reportDirectory = [System.IO.Path]::GetDirectoryName($absoluteReportPath)
if (!(Test-Path -LiteralPath $reportDirectory)) {
    New-Item -ItemType Directory -Path $reportDirectory | Out-Null
}

$schemas = Get-CSharpSchemas -Root $absoluteProjectRoot
$obsoleteSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$obsoleteNames | ForEach-Object { [void]$obsoleteSet.Add($_) }

$yamlFiles = New-Object System.Collections.Generic.List[string]
foreach ($root in $YamlRoots) {
    $absoluteYamlRoot = Resolve-PathUnderRoot -Root $absoluteProjectRoot -Path $root
    if (!(Test-Path -LiteralPath $absoluteYamlRoot)) {
        continue
    }

    Get-ChildItem -LiteralPath $absoluteYamlRoot -Recurse -File |
        Where-Object { $_.Extension -eq ".unity" -or $_.Extension -eq ".prefab" -or $_.Extension -eq ".asset" } |
        ForEach-Object { [void]$yamlFiles.Add($_.FullName) }
}

$fileReports = New-Object System.Collections.Generic.List[object]
foreach ($yamlFile in $yamlFiles) {
    [void]$fileReports.Add((Scan-YamlFile -Path $yamlFile -Schemas $schemas -ObsoleteSet $obsoleteSet))
}

$allOrphans = @($fileReports | ForEach-Object { $_.orphanedSerializedProperties } | Where-Object { $null -ne $_ })
$allMissingScripts = @($fileReports | ForEach-Object { $_.missingScriptReferences } | Where-Object { $null -ne $_ })
$allPrefabOverrideHits = @($fileReports | ForEach-Object { $_.prefabOverrideObsoleteProperties } | Where-Object { $null -ne $_ })
$targetObsoleteHits = @($allOrphans | Where-Object { $_.reason -eq "TARGET_OBSOLETE_NATIVE_FIELD" })

$started.Stop()

$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    projectRoot = $absoluteProjectRoot
    scannedRoots = $YamlRoots
    obsoleteNames = $obsoleteNames
    stats = [pscustomobject]@{
        scriptSchemas = $schemas.Count
        yamlFiles = $yamlFiles.Count
        monoBehaviours = ($fileReports | Measure-Object -Property monoBehaviourCount -Sum).Sum
        orphanedSerializedProperties = $allOrphans.Count
        targetObsoleteNativeFieldHits = $targetObsoleteHits.Count
        missingScriptReferences = $allMissingScripts.Count
        prefabOverrideObsoleteProperties = $allPrefabOverrideHits.Count
        elapsedMicroseconds = [int64]($started.Elapsed.TotalMilliseconds * 1000.0)
    }
    files = @($fileReports | Where-Object {
        $_.orphanedSerializedProperties.Count -gt 0 -or
        $_.missingScriptReferences.Count -gt 0 -or
        $_.prefabOverrideObsoleteProperties.Count -gt 0
    })
}

$json = $report | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText($absoluteReportPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $absoluteReportPath
