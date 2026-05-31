param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ReportPath = "Docs/Reports/YAML_MIGRATION_REPORT_1502.json",
    [string]$OutputPath = "Docs/AgentLogs/YamlReferenceIntegrity_1502.json",
    [switch]$SkipPackageCache,
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

function Convert-ToProjectRelative {
    param(
        [string]$ProjectRoot,
        [string]$Path
    )

    $fullRoot = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith($fullRoot, [StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($fullRoot.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace("\", "/")
    }

    return $Path.Replace("\", "/")
}

function Add-Failure {
    param(
        [System.Collections.Generic.List[object]]$Failures,
        [string]$Code,
        [string]$Message,
        [string]$Path = "",
        [int]$Line = 0
    )

    [void]$Failures.Add([pscustomobject]@{
        code = $Code
        message = $Message
        path = $Path
        line = $Line
    })
}

function Add-Warning {
    param(
        [System.Collections.Generic.List[object]]$Warnings,
        [string]$Code,
        [string]$Message,
        [string]$Path = "",
        [int]$Line = 0
    )

    [void]$Warnings.Add([pscustomobject]@{
        code = $Code
        message = $Message
        path = $Path
        line = $Line
    })
}

function Get-MetaGuidIndex {
    param(
        [string]$ProjectRoot,
        [bool]$IncludePackageCache
    )

    $map = @{}
    $roots = @("Assets", "Packages")
    if ($IncludePackageCache) {
        $roots += "Library/PackageCache"
    }

    foreach ($root in $roots) {
        $fullRoot = Resolve-UnderRoot $ProjectRoot $root
        if (!(Test-Path -LiteralPath $fullRoot -PathType Container)) {
            continue
        }

        foreach ($meta in Get-ChildItem -LiteralPath $fullRoot -Recurse -Filter "*.meta" -File) {
            $reader = [System.IO.StreamReader]::new($meta.FullName, [System.Text.Encoding]::UTF8, $true)
            try {
                while (($line = $reader.ReadLine()) -ne $null) {
                    $match = [regex]::Match($line, '^guid:\s*([0-9a-fA-F]{32})\s*$')
                    if ($match.Success) {
                        $guid = $match.Groups[1].Value.ToLowerInvariant()
                        if (!$map.ContainsKey($guid)) {
                            $map[$guid] = Convert-ToProjectRelative $ProjectRoot $meta.FullName
                        }
                        break
                    }
                }
            } finally {
                $reader.Dispose()
            }
        }
    }

    return $map
}

function Test-UnityBuiltinGuid {
    param([string]$Guid)

    $guidLower = $Guid.ToLowerInvariant()
    return $guidLower -eq "00000000000000000000000000000000" -or
        $guidLower -eq "0000000000000000e000000000000000"
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$outputFull = Resolve-UnderRoot $projectFull $OutputPath
$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()
$failures = [System.Collections.Generic.List[object]]::new()
$warnings = [System.Collections.Generic.List[object]]::new()
$fileReports = [System.Collections.Generic.List[object]]::new()

if (!(Test-Path -LiteralPath $reportFull -PathType Leaf)) {
    throw "Final report missing: $reportFull"
}

$report = Get-Content -Raw -LiteralPath $reportFull | ConvertFrom-Json
$guidIndex = Get-MetaGuidIndex $projectFull (!$SkipPackageCache)
$totalGuidRefs = 0
$totalBuiltinGuidRefs = 0
$totalUnresolvedGuidRefs = 0
$totalScriptRefs = 0
$totalMissingScriptRefs = 0
$totalDuplicateFileIds = 0
$totalTabLines = 0
$totalYamlFiles = 0

foreach ($entry in @($report.modifiedAssetHashes)) {
    if ($null -eq $entry) {
        continue
    }

    $relativePath = [string]$entry.relativePath
    $fullPath = Resolve-UnderRoot $projectFull $relativePath
    if (!(Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        Add-Failure $failures "MODIFIED_FILE_MISSING" "Modified YAML file is missing." $relativePath
        continue
    }

    $lines = [System.IO.File]::ReadAllLines($fullPath)
    $totalYamlFiles++
    $guidRefs = 0
    $builtinGuidRefs = 0
    $unresolvedGuidRefs = 0
    $scriptRefs = 0
    $missingScriptRefs = 0
    $duplicateFileIds = 0
    $tabLines = 0
    $anchors = @{}
    $hasYamlHeader = $false
    $hasTagHeader = $false
    $hasGameObjectBlock = $false
    $monoBehaviourBlocks = 0
    $lineNumber = 0

    foreach ($line in $lines) {
        $lineNumber++
        if ($lineNumber -eq 1 -and $line -match '^%YAML\s+1\.1') {
            $hasYamlHeader = $true
        }
        if ($lineNumber -eq 2 -and $line -match '^%TAG\s+!u!\s+tag:unity3d\.com,2011:') {
            $hasTagHeader = $true
        }
        if ($line -match "`t") {
            $tabLines++
            Add-Failure $failures "TAB_IN_YAML" "YAML line contains a tab character." $relativePath $lineNumber
        }
        if ($line -match '^--- !u!1 &') {
            $hasGameObjectBlock = $true
        }
        if ($line -match '^--- !u!114 &') {
            $monoBehaviourBlocks++
        }

        $anchorMatch = [regex]::Match($line, '^--- !u![0-9]+ &(-?[0-9]+)\s*$')
        if ($anchorMatch.Success) {
            $anchor = $anchorMatch.Groups[1].Value
            if ($anchors.ContainsKey($anchor)) {
                $duplicateFileIds++
                Add-Failure $failures "DUPLICATE_FILE_ID_ANCHOR" "YAML document FileID anchor is duplicated." $relativePath $lineNumber
            } else {
                $anchors[$anchor] = $lineNumber
            }
        }

        foreach ($guidMatch in [regex]::Matches($line, 'guid:\s*([0-9a-fA-F]{32})')) {
            $guid = $guidMatch.Groups[1].Value.ToLowerInvariant()
            if (Test-UnityBuiltinGuid $guid) {
                $builtinGuidRefs++
                continue
            }

            $guidRefs++
            if (!$guidIndex.ContainsKey($guid)) {
                $unresolvedGuidRefs++
                Add-Failure $failures "UNRESOLVED_GUID_REFERENCE" "Referenced GUID was not found in Assets/Packages meta files." $relativePath $lineNumber
            }
        }

        $scriptMatch = [regex]::Match($line, 'm_Script:\s*\{fileID:\s*(-?[0-9]+),\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*([0-9]+)\}')
        if ($scriptMatch.Success) {
            $scriptRefs++
            $scriptFileId = $scriptMatch.Groups[1].Value
            $scriptGuid = $scriptMatch.Groups[2].Value.ToLowerInvariant()
            if ($scriptFileId -eq "0" -or $scriptGuid -eq "00000000000000000000000000000000" -or !$guidIndex.ContainsKey($scriptGuid)) {
                $missingScriptRefs++
                Add-Failure $failures "MISSING_OR_UNRESOLVED_SCRIPT_REFERENCE" "MonoBehaviour m_Script reference is empty or unresolved." $relativePath $lineNumber
            }
        } elseif ($line -match 'm_Script:\s*\{fileID:\s*0') {
            $missingScriptRefs++
            Add-Failure $failures "MISSING_SCRIPT_REFERENCE" "MonoBehaviour m_Script reference has fileID 0." $relativePath $lineNumber
        }
    }

    if (!$hasYamlHeader) {
        Add-Failure $failures "YAML_HEADER_MISSING" "Modified YAML file does not start with %YAML 1.1." $relativePath 1
    }
    if (!$hasTagHeader) {
        Add-Failure $failures "UNITY_TAG_HEADER_MISSING" "Modified YAML file does not contain the Unity TAG header on line 2." $relativePath 2
    }
    if ($relativePath.EndsWith(".prefab", [StringComparison]::OrdinalIgnoreCase) -and !$hasGameObjectBlock) {
        Add-Failure $failures "PREFAB_GAMEOBJECT_BLOCK_MISSING" "Modified prefab lacks a GameObject document block." $relativePath
    }
    if ($monoBehaviourBlocks -gt 0 -and $scriptRefs -eq 0) {
        Add-Failure $failures "MONOBEHAVIOUR_WITHOUT_SCRIPT_REF" "Modified file contains MonoBehaviour blocks but no m_Script references." $relativePath
    }
    if ($guidRefs -eq 0) {
        Add-Warning $warnings "NO_GUID_REFERENCES" "Modified file contains no GUID references." $relativePath
    }

    $totalGuidRefs += $guidRefs
    $totalBuiltinGuidRefs += $builtinGuidRefs
    $totalUnresolvedGuidRefs += $unresolvedGuidRefs
    $totalScriptRefs += $scriptRefs
    $totalMissingScriptRefs += $missingScriptRefs
    $totalDuplicateFileIds += $duplicateFileIds
    $totalTabLines += $tabLines

    [void]$fileReports.Add([pscustomobject]@{
        relativePath = $relativePath
        lineCount = $lines.Count
        hasYamlHeader = $hasYamlHeader
        hasTagHeader = $hasTagHeader
        hasGameObjectBlock = $hasGameObjectBlock
        monoBehaviourBlocks = $monoBehaviourBlocks
        fileIdAnchorCount = $anchors.Count
        duplicateFileIdAnchors = $duplicateFileIds
        guidReferences = $guidRefs
        builtinGuidReferences = $builtinGuidRefs
        unresolvedGuidReferences = $unresolvedGuidRefs
        scriptReferences = $scriptRefs
        missingScriptReferences = $missingScriptRefs
        tabLines = $tabLines
    })
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$validation = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_YAML_REFERENCE_INTEGRITY"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    sourceReport = $ReportPath
    status = if ($failures.Count -eq 0) { "PASS" } else { "FAIL" }
    failures = @($failures.ToArray())
    warnings = @($warnings.ToArray())
    modifiedYamlFilesChecked = $totalYamlFiles
    packageCacheIndexed = !$SkipPackageCache
    metaGuidsIndexed = $guidIndex.Count
    totalGuidReferences = $totalGuidRefs
    totalBuiltinGuidReferences = $totalBuiltinGuidRefs
    totalUnresolvedGuidReferences = $totalUnresolvedGuidRefs
    totalScriptReferences = $totalScriptRefs
    totalMissingScriptReferences = $totalMissingScriptRefs
    totalDuplicateFileIdAnchors = $totalDuplicateFileIds
    totalTabLines = $totalTabLines
    files = @($fileReports.ToArray())
    elapsedMicroseconds = $elapsedUs
}

$outputDir = [System.IO.Path]::GetDirectoryName($outputFull)
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$validation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFull -Encoding UTF8

if ($FailOnError -and $failures.Count -gt 0) {
    throw "Modified YAML reference integrity failed with $($failures.Count) failure(s). See $outputFull"
}

$validation
