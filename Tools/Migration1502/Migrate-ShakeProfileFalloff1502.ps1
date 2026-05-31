param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$BackupRoot = "Docs/AgentLogs/_Recovery_1502_ShakeProfile",
    [string]$ReportPath = "Docs/AgentLogs/ShakeProfileFalloff_1502.json",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

$shakeProfileAssets = @(
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_BiteImpact.asset",
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_Damage.asset",
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_Explosion.asset",
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_ImpactHeavy.asset",
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_ImpactLight.asset",
    "Assets/_Project/Data/VFX/ShakeProfiles/ShakeProfile_ImpactMedium.asset"
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

    # Unity accepts LF; force it here so Git does not report CR-at-EOL as trailing whitespace.
    $outputNewLine = "`n"
    $text = [string]::Join($outputNewLine, $Lines)
    if ($HadFinalNewLine) {
        $text += $outputNewLine
    }

    [System.IO.File]::WriteAllText($Path, $text, [System.Text.UTF8Encoding]::new($false))
}

function Get-UnityYamlStats {
    param([System.Collections.Generic.List[string]]$Lines)

    return [pscustomobject]@{
        yamlHeader = $Lines.Count -gt 0 -and $Lines[0] -eq "%YAML 1.1"
        unityTagHeader = $Lines.Count -gt 1 -and $Lines[1].StartsWith("%TAG !u! tag:unity3d.com,2011:")
        rootGameObjectMarker = [bool]($Lines | Select-String -Pattern "m_RootGameObject" -Quiet)
        monoBehaviourCount = @($Lines | Select-String -Pattern "^--- !u!114 &").Count
        missingScriptCount = @($Lines | Select-String -Pattern "m_Script:\s*\{fileID:\s*0").Count
        shakeProfileGuidMatch = [bool]($Lines | Select-String -Pattern "m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*17ab5b96ce13779438b3efbdf414483f,\s*type:\s*3\}" -Quiet)
        falloffCurveCount = @($Lines | Select-String -Pattern "^  FalloffCurve:\s*$").Count
        falloffExponentCount = @($Lines | Select-String -Pattern "^  FalloffExponent:\s*").Count
    }
}

function Convert-CurveToFalloffExponent {
    param([string[]]$BlockLines)

    $block = [string]::Join("`n", $BlockLines)
    $outSlopes = @([regex]::Matches($block, "(?m)^\s+outSlope:\s*([-+]?[0-9]+(?:\.[0-9]+)?)") | ForEach-Object { [double]$_.Groups[1].Value })
    $firstOutSlope = if ($outSlopes.Count -gt 0) { [double]$outSlopes[0] } else { 0.0 }
    $candidate = if ($firstOutSlope -le -0.5) { [Math]::Abs($firstOutSlope) } else { 2.0 }
    $clamped = [Math]::Min(4.0, [Math]::Max(0.5, $candidate))
    return [pscustomobject]@{
        exponent = [double]$clamped
        sourceFirstOutSlope = [double]$firstOutSlope
        rule = if ($firstOutSlope -le -0.5) { "ABS_FIRST_OUT_SLOPE_CLAMPED" } else { "SOURCE_DEFAULT_FALLBACK" }
    }
}

$started = [System.Diagnostics.Stopwatch]::StartNew()
$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$backupRootFull = Resolve-UnderRoot $projectFull $BackupRoot
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$records = New-Object System.Collections.Generic.List[object]
$writtenCount = 0
$mappedCount = 0
$deletedCurveCount = 0
$bytesBeforeTotal = [int64]0
$bytesAfterTotal = [int64]0

foreach ($relativeAsset in $shakeProfileAssets) {
    $path = Resolve-UnderRoot $projectFull $relativeAsset
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "ShakeProfile asset missing: $relativeAsset"
    }

    $newLine = $null
    $hadFinalNewLine = $false
    $lines = Read-LinesPreserveStyle -Path $path -NewLine ([ref]$newLine) -HadFinalNewLine ([ref]$hadFinalNewLine)
    $beforeStats = Get-UnityYamlStats -Lines $lines
    $infoBefore = Get-Item -LiteralPath $path
    $bytesBefore = [int64]$infoBefore.Length
    $shaBefore = Get-Sha256 $path
    $bytesBeforeTotal += $bytesBefore
    $rejections = New-Object System.Collections.Generic.List[object]

    if (!$beforeStats.yamlHeader -or !$beforeStats.unityTagHeader) {
        [void]$rejections.Add("MISSING_UNITY_YAML_HEADERS")
    }
    if ($beforeStats.monoBehaviourCount -ne 1) {
        [void]$rejections.Add("MONOBEHAVIOUR_COUNT_NOT_ONE")
    }
    if (!$beforeStats.shakeProfileGuidMatch) {
        [void]$rejections.Add("SCRIPT_GUID_MISMATCH")
    }
    if ($beforeStats.falloffExponentCount -gt 0) {
        [void]$rejections.Add("FALLOFF_EXPONENT_ALREADY_PRESENT")
    }
    if ($beforeStats.falloffCurveCount -ne 1) {
        [void]$rejections.Add("FALLOFF_CURVE_COUNT_NOT_ONE")
    }

    $curveIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^  FalloffCurve:\s*$") {
            $curveIndex = $i
            break
        }
    }

    if ($curveIndex -lt 0) {
        [void]$rejections.Add("FALLOFF_CURVE_NOT_FOUND")
    }

    $curveEnd = $curveIndex + 1
    if ($curveIndex -ge 0) {
        while ($curveEnd -lt $lines.Count) {
            $next = $lines[$curveEnd]
            if ($next -match "^  [A-Za-z_][A-Za-z0-9_]*\s*:" -or $next -match "^--- ") {
                break
            }

            $curveEnd++
        }
    }

    if ($curveIndex -ge 0 -and $curveEnd -le $curveIndex + 1) {
        [void]$rejections.Add("EMPTY_FALLOFF_CURVE_BLOCK")
    }

    $axisIndex = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^  AxisWeights:\s*") {
            $axisIndex = $i
            break
        }
    }
    if ($axisIndex -lt 0) {
        [void]$rejections.Add("AXIS_WEIGHTS_NOT_FOUND")
    }

    $conversion = $null
    $removedLineCount = 0
    $normalizedEditorClassIdentifier = 0
    $applied = $false
    if ($rejections.Count -eq 0) {
        $blockLines = @()
        for ($i = $curveIndex; $i -lt $curveEnd; $i++) {
            $blockLines += $lines[$i]
        }
        $conversion = Convert-CurveToFalloffExponent -BlockLines $blockLines
        $removedLineCount = $curveEnd - $curveIndex
        $mappedCount++

        if ($Apply) {
            $backupPath = Resolve-UnderRoot $backupRootFull $relativeAsset
            $backupDirectory = [System.IO.Path]::GetDirectoryName($backupPath)
            New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
            Copy-Item -LiteralPath $path -Destination $backupPath -Force
            $backupSha = Get-Sha256 $backupPath
            if ($backupSha -ne $shaBefore) {
                throw "Backup hash mismatch for $relativeAsset"
            }

            $lines.RemoveRange($curveIndex, $removedLineCount)
            $exponentText = $conversion.exponent.ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture)
            $lines.Insert($curveIndex, "  FalloffExponent: $exponentText")
            for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
                if ($lines[$lineIndex] -match "^  m_EditorClassIdentifier:\s+$") {
                    $lines[$lineIndex] = "  m_EditorClassIdentifier:"
                    $normalizedEditorClassIdentifier++
                }
            }
            Write-LinesPreserveStyle -Path $path -Lines $lines -NewLine $newLine -HadFinalNewLine $hadFinalNewLine
            $writtenCount++
            $deletedCurveCount++
            $applied = $true
        }
    }

    $afterNewLine = $null
    $afterHadFinal = $false
    $afterLines = if ($Apply -and $applied) {
        Read-LinesPreserveStyle -Path $path -NewLine ([ref]$afterNewLine) -HadFinalNewLine ([ref]$afterHadFinal)
    } else {
        $lines
    }
    $afterStats = Get-UnityYamlStats -Lines $afterLines
    $infoAfter = Get-Item -LiteralPath $path
    $bytesAfter = [int64]$infoAfter.Length
    $shaAfter = Get-Sha256 $path
    $bytesAfterTotal += $bytesAfter

    [void]$records.Add([pscustomobject]@{
        relativePath = $relativeAsset
        applied = $applied
        bytesBefore = $bytesBefore
        bytesAfter = $bytesAfter
        sha256Before = $shaBefore
        sha256After = $shaAfter
        backupPath = if ($applied) { (Resolve-UnderRoot $backupRootFull $relativeAsset) } else { $null }
        removedLineCount = $removedLineCount
        normalizedEditorClassIdentifierLines = $normalizedEditorClassIdentifier
        conversion = $conversion
        rejections = @($rejections.ToArray())
        beforeStats = $beforeStats
        afterStats = $afterStats
        structureInvariant = [pscustomobject]@{
            yamlHeaderPreserved = [bool]($beforeStats.yamlHeader -eq $afterStats.yamlHeader)
            unityTagHeaderPreserved = [bool]($beforeStats.unityTagHeader -eq $afterStats.unityTagHeader)
            rootGameObjectMarkerPreserved = [bool]($beforeStats.rootGameObjectMarker -eq $afterStats.rootGameObjectMarker)
            monoBehaviourCountPreserved = [bool]($beforeStats.monoBehaviourCount -eq $afterStats.monoBehaviourCount)
            missingScriptCountPreserved = [bool]($beforeStats.missingScriptCount -eq $afterStats.missingScriptCount)
            scriptGuidPreserved = [bool]($beforeStats.shakeProfileGuidMatch -eq $afterStats.shakeProfileGuidMatch)
        }
    })
}

$started.Stop()
$rejectedCount = 0
foreach ($record in $records) {
    $rejectedCount += $record.rejections.Count
}

$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = if ($Apply) { "STATIC_SOURCE_ASSET_MUTATION" } else { "STATIC_SOURCE_ASSET_MUTATION_DRY_RUN" }
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    apply = [bool]$Apply
    filesConsidered = $records.Count
    filesWritten = $writtenCount
    curvesMapped = if ($Apply) { $mappedCount } else { 0 }
    dryRunCurvesMatched = if ($Apply) { 0 } else { $mappedCount }
    staleCurveBlocksDeleted = if ($Apply) { $deletedCurveCount } else { 0 }
    falloffExponentPropertiesAdded = if ($Apply) { $writtenCount } else { 0 }
    rejectedCount = $rejectedCount
    totalBytesBefore = $bytesBeforeTotal
    totalBytesAfter = $bytesAfterTotal
    bytesRemoved = $bytesBeforeTotal - $bytesAfterTotal
    elapsedMicroseconds = [int64]($started.Elapsed.TotalMilliseconds * 1000.0)
    files = @($records.ToArray())
}

$reportDirectory = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
