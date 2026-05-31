param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$LedgerPath = "Docs/AgentLogs/YamlDesync_1502_Ledger.json",
    [string]$BackupRoot = "Docs/AgentLogs/_Recovery_1502_Extended",
    [string]$ReportPath = "Docs/AgentLogs/YamlCleanup_1502.json",
    [string[]]$IncludeExtensions = @(".prefab"),
    [bool]$FirstPartyOnly = $true,
    [switch]$Apply
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

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-UnityYamlStats {
    param([System.Collections.Generic.List[string]]$Lines)

    return [pscustomobject]@{
        yamlHeader = $Lines.Count -gt 0 -and $Lines[0] -eq "%YAML 1.1"
        unityTagHeader = $Lines.Count -gt 1 -and $Lines[1].StartsWith("%TAG !u! tag:unity3d.com,2011:")
        rootGameObjectMarker = [bool]($Lines | Select-String -Pattern "m_RootGameObject" -Quiet)
        prefabGameObjectBlock = [bool]($Lines | Select-String -Pattern "^GameObject:" -Quiet)
        monoBehaviourCount = @($Lines | Select-String -Pattern "^--- !u!114 &").Count
        missingScriptCount = @($Lines | Select-String -Pattern "m_Script:\s*\{fileID:\s*0").Count
    }
}

function Read-LinesPreserveStyle {
    param(
        [string]$Path,
        [ref]$NewLine,
        [ref]$HadFinalNewLine
    )

    $raw = [System.IO.File]::ReadAllText($Path)
    $NewLine.Value = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
    $HadFinalNewLine.Value = $raw.EndsWith("`r`n") -or $raw.EndsWith("`n")

    $split = [regex]::Split($raw, "\r\n|\n")
    if ($HadFinalNewLine.Value -and $split.Length -gt 0 -and $split[$split.Length - 1] -eq "") {
        $split = $split[0..($split.Length - 2)]
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $split) {
        [void]$lines.Add($line)
    }

    return ,$lines
}

function Write-LinesPreserveStyle {
    param(
        [string]$Path,
        [System.Collections.Generic.List[string]]$Lines,
        [string]$NewLine,
        [bool]$HadFinalNewLine
    )

    $text = [string]::Join($NewLine, $Lines)
    if ($HadFinalNewLine) {
        $text += $NewLine
    }

    [System.IO.File]::WriteAllText($Path, $text, [System.Text.UTF8Encoding]::new($false))
}

$started = [System.Diagnostics.Stopwatch]::StartNew()
$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$ledgerFull = Resolve-UnderRoot $projectFull $LedgerPath
$backupRootFull = Resolve-UnderRoot $projectFull $BackupRoot
$reportFull = Resolve-UnderRoot $projectFull $ReportPath

if (!(Test-Path -LiteralPath $ledgerFull -PathType Leaf)) {
    throw "Ledger missing: $ledgerFull"
}

$extensionSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($extension in $IncludeExtensions) {
    [void]$extensionSet.Add($extension)
}

$ledger = Get-Content -Raw -LiteralPath $ledgerFull | ConvertFrom-Json
$candidates = New-Object System.Collections.Generic.List[object]
foreach ($file in $ledger.files) {
    foreach ($hit in @($file.orphanedSerializedProperties)) {
        if ($null -eq $hit) {
            continue
        }

        $hitPath = [string]$hit.path
        $scriptPath = [string]$hit.scriptPath
        if (!$extensionSet.Contains([System.IO.Path]::GetExtension($hitPath))) {
            continue
        }

        if ($FirstPartyOnly -and !$scriptPath.StartsWith("Assets/_Project/", [StringComparison]::Ordinal)) {
            continue
        }

        if ([string]$hit.reason -ne "NOT_IN_CURRENT_CSHARP_SCHEMA") {
            continue
        }

        [void]$candidates.Add([pscustomobject]@{
            path = [System.IO.Path]::GetFullPath($hitPath)
            relativePath = (Get-RelativePathPortable -Root $projectFull -Path $hitPath).Replace("\", "/")
            line = [int]$hit.line
            componentFileID = [string]$hit.componentFileID
            componentStartLine = [int]$hit.componentStartLine
            scriptGuid = [string]$hit.scriptGuid
            scriptPath = $scriptPath
            scriptClass = [string]$hit.scriptClass
            property = [string]$hit.property
        })
    }
}

$fileGroups = $candidates | Group-Object path
$fileReports = New-Object System.Collections.Generic.List[object]
$deletedCount = 0
$writtenFileCount = 0
$totalBytesBefore = [int64]0
$totalBytesAfter = [int64]0

foreach ($group in $fileGroups) {
    $path = [string]$group.Name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Candidate file missing: $path"
    }

    $relativePath = (Get-RelativePathPortable -Root $projectFull -Path $path).Replace("\", "/")
    $infoBefore = Get-Item -LiteralPath $path
    $bytesBefore = [int64]$infoBefore.Length
    $shaBefore = Get-Sha256 $path
    $newLine = $null
    $hadFinalNewLine = $false
    $lines = Read-LinesPreserveStyle -Path $path -NewLine ([ref]$newLine) -HadFinalNewLine ([ref]$hadFinalNewLine)
    $preStats = Get-UnityYamlStats -Lines $lines
    $removed = New-Object System.Collections.Generic.List[object]
    $rejected = New-Object System.Collections.Generic.List[object]

    foreach ($candidate in ($group.Group | Sort-Object line -Descending)) {
        $index = [int]$candidate.line - 1
        $property = [string]$candidate.property
        if ($index -lt 0 -or $index -ge $lines.Count) {
            [void]$rejected.Add([pscustomobject]@{
                property = $property
                line = $candidate.line
                reason = "LINE_OUT_OF_RANGE"
            })
            continue
        }

        $line = $lines[$index]
        $rootPropertyPattern = "^\s{2}" + [regex]::Escape($property) + "\s*:"
        if ($line -notmatch $rootPropertyPattern) {
            [void]$rejected.Add([pscustomobject]@{
                property = $property
                line = $candidate.line
                reason = "LINE_DOES_NOT_MATCH_ROOT_PROPERTY"
                actual = $line
            })
            continue
        }

        $end = $index + 1
        while ($end -lt $lines.Count) {
            $next = $lines[$end]
            if ($next -match "^  [A-Za-z_][A-Za-z0-9_]*\s*:" -or $next -match "^--- ") {
                break
            }

            $end++
        }

        $count = $end - $index
        if ($count -le 0) {
            [void]$rejected.Add([pscustomobject]@{
                property = $property
                line = $candidate.line
                reason = "EMPTY_REMOVAL_RANGE"
            })
            continue
        }

        [void]$removed.Add([pscustomobject]@{
            property = $property
            originalLine = $candidate.line
            removedLineCount = $count
            componentFileID = $candidate.componentFileID
            componentStartLine = $candidate.componentStartLine
            scriptPath = $candidate.scriptPath
            scriptClass = $candidate.scriptClass
            firstLine = $line.Trim()
        })
        $lines.RemoveRange($index, $count)
    }

    if ($removed.Count -gt 0 -and $rejected.Count -eq 0 -and $Apply) {
        $backupPath = Resolve-UnderRoot $backupRootFull $relativePath
        $backupDirectory = [System.IO.Path]::GetDirectoryName($backupPath)
        New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
        Copy-Item -LiteralPath $path -Destination $backupPath -Force
        $backupSha = Get-Sha256 $backupPath
        if ($backupSha -ne $shaBefore) {
            throw "Backup hash mismatch for $relativePath"
        }

        Write-LinesPreserveStyle -Path $path -Lines $lines -NewLine $newLine -HadFinalNewLine $hadFinalNewLine
        $writtenFileCount++
    }

    $infoAfter = Get-Item -LiteralPath $path
    $bytesAfter = [int64]$infoAfter.Length
    $shaAfter = Get-Sha256 $path
    $postLines = $null
    if ($Apply -and $removed.Count -gt 0 -and $rejected.Count -eq 0) {
        $postNewLine = $null
        $postFinal = $false
        $postLines = Read-LinesPreserveStyle -Path $path -NewLine ([ref]$postNewLine) -HadFinalNewLine ([ref]$postFinal)
    } else {
        $postLines = $lines
    }
    $postStats = Get-UnityYamlStats -Lines $postLines

    $totalBytesBefore += $bytesBefore
    $totalBytesAfter += $bytesAfter
    $deletedCount += $removed.Count
    $appliedFile = [bool]($Apply -and $removed.Count -gt 0 -and $rejected.Count -eq 0)
    $backupPathValue = if ($appliedFile) { Resolve-UnderRoot $backupRootFull $relativePath } else { $null }
    $structureInvariant = [pscustomobject]@{
        yamlHeaderPreserved = [bool]($preStats.yamlHeader -eq $postStats.yamlHeader)
        unityTagHeaderPreserved = [bool]($preStats.unityTagHeader -eq $postStats.unityTagHeader)
        rootGameObjectMarkerPreserved = [bool]($preStats.rootGameObjectMarker -eq $postStats.rootGameObjectMarker)
        prefabGameObjectBlockPreserved = [bool]($preStats.prefabGameObjectBlock -eq $postStats.prefabGameObjectBlock)
        monoBehaviourCountPreserved = [bool]($preStats.monoBehaviourCount -eq $postStats.monoBehaviourCount)
        missingScriptCountPreserved = [bool]($preStats.missingScriptCount -eq $postStats.missingScriptCount)
    }
    $removedArray = $removed.ToArray()
    $rejectedArray = $rejected.ToArray()

    $fileReport = [pscustomobject]@{
        relativePath = $relativePath
        applied = $appliedFile
        bytesBefore = $bytesBefore
        bytesAfter = $bytesAfter
        sha256Before = $shaBefore
        sha256After = $shaAfter
        backupPath = $backupPathValue
        preStats = $preStats
        postStats = $postStats
        structureInvariant = $structureInvariant
        removedProperties = $removedArray
        rejectedProperties = $rejectedArray
    }
    [void]$fileReports.Add($fileReport)
}

$started.Stop()
$rejectedCount = 0
foreach ($fileReport in $fileReports) {
    if ($null -ne $fileReport.rejectedProperties) {
        $rejectedCount += $fileReport.rejectedProperties.Count
    }
}

$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = if ($Apply) { "STATIC_SOURCE_PREFAB_MUTATION" } else { "STATIC_SOURCE_PREFAB_MUTATION_DRY_RUN" }
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    apply = [bool]$Apply
    firstPartyOnly = $FirstPartyOnly
    includeExtensions = $IncludeExtensions
    ledgerPath = $LedgerPath
    candidateCount = $candidates.Count
    filesConsidered = $fileReports.Count
    filesWritten = $writtenFileCount
    propertiesDeleted = if ($Apply) { $deletedCount } else { 0 }
    dryRunPropertiesMatched = if ($Apply) { 0 } else { $deletedCount }
    rejectedCount = [int]$rejectedCount
    totalBytesBefore = $totalBytesBefore
    totalBytesAfter = $totalBytesAfter
    elapsedMicroseconds = [int64]($started.Elapsed.TotalMilliseconds * 1000.0)
    files = @($fileReports.ToArray())
}

$reportDirectory = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
