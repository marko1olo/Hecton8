param(
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$OutputJson = "",
    [string]$OutputMarkdown = "",
    [switch]$IncludeSignalAudit,
    [switch]$IncludeReports,
    [int]$Recent = 12
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Convert-ToProjectRelativePath {
    param([string]$Path)

    $full = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\', '/')
    if ($full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length).TrimStart('\', '/')
    }

    return $full
}

function Get-ArtifactTimestamp {
    param(
        [object]$Json,
        [string]$Path
    )

    $candidateNames = @('Timestamp', 'timestamp', 'GeneratedUtc', 'generatedUtc')
    foreach ($name in $candidateNames) {
        if ($Json.PSObject.Properties.Name -contains $name) {
            $raw = [string]$Json.$name
            $parsed = [datetimeoffset]::MinValue
            if ([datetimeoffset]::TryParse($raw, [ref]$parsed)) {
                return $parsed
            }
        }
    }

    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $match = [regex]::Match($fileName, '(?<date>20\d{6})[_-]?(?<time>\d{6})')
    if ($match.Success) {
        $stamp = $match.Groups['date'].Value + $match.Groups['time'].Value
        $parsed = [datetime]::ParseExact($stamp, 'yyyyMMddHHmmss', [Globalization.CultureInfo]::InvariantCulture)
        return [datetimeoffset]::new($parsed, [timespan]::Zero)
    }

    return [datetimeoffset](Get-Item -LiteralPath $Path).LastWriteTimeUtc
}

function Add-ScalarMetrics {
    param(
        [object]$Value,
        [string]$Prefix,
        [hashtable]$Output
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Array]) {
        return
    }

    if ($Value -is [pscustomobject]) {
        foreach ($property in $Value.PSObject.Properties) {
            $next = if ([string]::IsNullOrWhiteSpace($Prefix)) {
                $property.Name
            }
            else {
                $Prefix + '.' + $property.Name
            }

            Add-ScalarMetrics -Value $property.Value -Prefix $next -Output $Output
        }

        return
    }

    if ($Value -is [bool]) {
        $Output[$Prefix] = if ($Value) { 1.0 } else { 0.0 }
        return
    }

    if ($Value -is [byte] -or
        $Value -is [sbyte] -or
        $Value -is [int16] -or
        $Value -is [uint16] -or
        $Value -is [int] -or
        $Value -is [uint32] -or
        $Value -is [long] -or
        $Value -is [uint64] -or
        $Value -is [single] -or
        $Value -is [double] -or
        $Value -is [decimal]) {
        $Output[$Prefix] = [double]$Value
    }
}

function Get-ArtifactType {
    param([object]$Json)

    $scope = ''
    if ($Json.PSObject.Properties.Name -contains 'Scope') {
        $scope = [string]$Json.Scope
    }

    if ($scope -like '*Core dependency graph*') {
        return 'HPhiCoreGraph'
    }

    if ($Json.PSObject.Properties.Name -contains 'Scores' -or
        $Json.PSObject.Properties.Name -contains 'Counts') {
        return 'HPhiRuntimeSummary'
    }

    if ($Json.PSObject.Properties.Name -contains 'errors' -or
        $Json.PSObject.Properties.Name -contains 'Errors') {
        return 'SignalAudit'
    }

    return 'Unknown'
}

function Get-InputFiles {
    $roots = @(
        (Join-Path $ProjectRoot 'Docs\AgentLogs'),
        (Join-Path $ProjectRoot 'Docs\Archive')
    )

    if ($IncludeReports) {
        $roots += (Join-Path $ProjectRoot 'Docs\Reports')
    }

    $patterns = @('HPhi*.json')
    if ($IncludeSignalAudit) {
        $patterns += 'SignalBusContractAuditCli_*.json'
    }

    $files = New-Object System.Collections.Generic.List[string]
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) {
            continue
        }

        foreach ($pattern in $patterns) {
            Get-ChildItem -LiteralPath $root -Recurse -File -Filter $pattern |
                Where-Object { $_.Name -notlike 'HPhiTrend_*.json' } |
                ForEach-Object { [void]$files.Add($_.FullName) }
        }
    }

    return @($files | Sort-Object -Unique)
}

function New-ArtifactRows {
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($path in Get-InputFiles) {
        try {
            $json = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            $metrics = @{}
            Add-ScalarMetrics -Value $json -Prefix '' -Output $metrics

            [void]$rows.Add([pscustomobject]@{
                Timestamp = Get-ArtifactTimestamp -Json $json -Path $path
                ArtifactType = Get-ArtifactType -Json $json
                Path = Convert-ToProjectRelativePath $path
                MetricCount = $metrics.Count
                Metrics = $metrics
            })
        }
        catch {
            [void]$rows.Add([pscustomobject]@{
                Timestamp = [datetimeoffset](Get-Item -LiteralPath $path).LastWriteTimeUtc
                ArtifactType = 'ParseFailed'
                Path = Convert-ToProjectRelativePath $path
                MetricCount = 0
                Metrics = @{}
                Error = $_.Exception.Message
            })
        }
    }

    return @($rows | Sort-Object Timestamp, Path)
}

function New-TrendRows {
    param([object[]]$Artifacts)

    $metricNames = New-Object System.Collections.Generic.HashSet[string]
    foreach ($artifact in $Artifacts) {
        foreach ($key in $artifact.Metrics.Keys) {
            [void]$metricNames.Add($key)
        }
    }

    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($name in @($metricNames | Sort-Object)) {
        $samples = @($Artifacts | Where-Object { $_.Metrics.ContainsKey($name) })
        if ($samples.Count -eq 0) {
            continue
        }

        $first = $samples[0]
        $last = $samples[$samples.Count - 1]
        $values = @($samples | ForEach-Object { [double]$_.Metrics[$name] })
        [void]$rows.Add([pscustomobject]@{
            Metric = $name
            Samples = $samples.Count
            FirstTimestamp = $first.Timestamp.ToString('yyyy-MM-dd HH:mm:ss zzz')
            LastTimestamp = $last.Timestamp.ToString('yyyy-MM-dd HH:mm:ss zzz')
            First = [double]$first.Metrics[$name]
            Last = [double]$last.Metrics[$name]
            Delta = [double]$last.Metrics[$name] - [double]$first.Metrics[$name]
            Min = [double]($values | Measure-Object -Minimum).Minimum
            Max = [double]($values | Measure-Object -Maximum).Maximum
            FirstPath = $first.Path
            LastPath = $last.Path
        })
    }

    return @($rows.ToArray())
}

function Select-KeyMetrics {
    param([object[]]$Trends)

    $keys = @(
        'Scores.RuntimeHPhiRisk',
        'Scores.RuntimeHPhiNarrow',
        'Scores.DataSovereignty',
        'Scores.MemoryAlignment',
        'Scores.RiskIntegration',
        'Scores.ArchitecturalPurity',
        'Scores.AupPrecisionIntegrity',
        'Counts.RuntimeFiles',
        'Counts.NativeArrayRefs',
        'Counts.DataVaultRefs',
        'Counts.OwnerBlockedNativeArrayRefs',
        'Counts.PrimaryOwnerBlockedNativeArrayRefs',
        'Counts.GlobalRegistrySurface',
        'Counts.GetComponentCalls',
        'Counts.ManagedFormatSurface',
        'Counts.JobCompleteSurface',
        'DuplicateSignalNameAudit.DuplicateSignalNameCount',
        'CoreGraph.Counts.CoreAsmdefDebtReferenceCount',
        'CoreGraph.Counts.GeneratedProjectDebtReferenceCount',
        'CoreGraph.Counts.SourceBackedBridgeDebtReferenceCount',
        'CoreGraph.Counts.SourceBackedCompileBridgeDebtReferenceCount',
        'CoreGraph.Counts.ProjectReferenceReplacementDebtReferenceCount'
    )

    return @($Trends | Where-Object {
        $_ -is [pscustomobject] -and
        $_.PSObject.Properties.Name -contains 'Metric' -and
        $keys -contains $_.Metric
    })
}

function Write-TrendMarkdown {
    param(
        [object]$Report,
        [string]$Path
    )

    $md = New-Object System.Text.StringBuilder
    [void]$md.AppendLine('# SHINOBU_02 H-Phi Trend')
    [void]$md.AppendLine()
    [void]$md.AppendLine('Evidence Class: STATIC_SOURCE_HISTORY')
    [void]$md.AppendLine('Generated UTC: ' + $Report.GeneratedUtc)
    [void]$md.AppendLine('Artifacts scanned: ' + $Report.ArtifactCount)
    [void]$md.AppendLine('Metric series: ' + $Report.MetricSeriesCount)
    [void]$md.AppendLine('Include Signal Audit: ' + $Report.IncludeSignalAudit)
    [void]$md.AppendLine('Include Reports: ' + $Report.IncludeReports)
    [void]$md.AppendLine()
    [void]$md.AppendLine('## Key Dynamics')
    [void]$md.AppendLine()
    [void]$md.AppendLine('| Metric | Samples | First | Last | Delta | Min | Max |')
    [void]$md.AppendLine('|---|---:|---:|---:|---:|---:|---:|')
    foreach ($trend in $Report.KeyTrends) {
        [void]$md.AppendLine(('| {0} | {1} | {2} | {3} | {4} | {5} | {6} |' -f
            $trend.Metric,
            $trend.Samples,
            $trend.First,
            $trend.Last,
            $trend.Delta,
            $trend.Min,
            $trend.Max))
    }

    [void]$md.AppendLine()
    [void]$md.AppendLine('## Largest Absolute Movement')
    [void]$md.AppendLine()
    [void]$md.AppendLine('| Metric | Samples | First | Last | Delta | Last Artifact |')
    [void]$md.AppendLine('|---|---:|---:|---:|---:|---|')
    foreach ($trend in $Report.LargestAbsoluteMovement) {
        [void]$md.AppendLine(('| {0} | {1} | {2} | {3} | {4} | `{5}` |' -f
            $trend.Metric,
            $trend.Samples,
            $trend.First,
            $trend.Last,
            $trend.Delta,
            $trend.LastPath))
    }

    [void]$md.AppendLine()
    [void]$md.AppendLine('## Recent Artifacts')
    [void]$md.AppendLine()
    [void]$md.AppendLine('| Timestamp | Type | Metrics | Path |')
    [void]$md.AppendLine('|---|---|---:|---|')
    foreach ($artifact in $Report.RecentArtifacts) {
        [void]$md.AppendLine(('| {0} | {1} | {2} | `{3}` |' -f
            $artifact.Timestamp,
            $artifact.ArtifactType,
            $artifact.MetricCount,
            $artifact.Path))
    }

    [void]$md.AppendLine()
    [void]$md.AppendLine('## Non-Claims')
    [void]$md.AppendLine()
    [void]$md.AppendLine('- This trend reads JSON artifacts only. It does not prove Unity import, Play Mode, profiler, GC, or player-build behavior.')
    [void]$md.AppendLine('- Missing or timed-out current full H-Phi artifacts are absence of evidence, not a pass.')
    [void]$md.AppendLine('- Historical artifacts are mixed batch snapshots. Treat movement as static trend, not runtime causality.')

    Set-Content -LiteralPath $Path -Value $md.ToString() -Encoding UTF8
}

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $OutputJson = Join-Path $ProjectRoot 'Docs\AgentLogs\HPhiTrend_SHINOBU_02.json'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputJson)) {
    $OutputJson = Join-Path $ProjectRoot $OutputJson
}

if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $ProjectRoot 'Docs\AgentLogs\HPhiTrend_SHINOBU_02.md'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $ProjectRoot $OutputMarkdown
}

New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($OutputJson)) | Out-Null
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($OutputMarkdown)) | Out-Null

$artifacts = New-ArtifactRows
$trends = New-TrendRows -Artifacts $artifacts
$keyTrends = Select-KeyMetrics -Trends $trends
$largest = @($trends |
    Where-Object {
        $_ -is [pscustomobject] -and
        $_.PSObject.Properties.Name -contains 'Samples' -and
        $_.Samples -gt 1
    } |
    Sort-Object @{ Expression = { [math]::Abs([double]$_.Delta) }; Descending = $true }, Metric |
    Select-Object -First 30)
$recentArtifacts = @($artifacts |
    Sort-Object Timestamp -Descending |
    Select-Object -First $Recent |
    ForEach-Object {
        [pscustomobject]@{
            Timestamp = $_.Timestamp.ToString('yyyy-MM-dd HH:mm:ss zzz')
            ArtifactType = $_.ArtifactType
            Path = $_.Path
            MetricCount = $_.MetricCount
        }
    })

$report = [ordered]@{
    GeneratedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    ProjectRoot = $ProjectRoot
    EvidenceClass = 'STATIC_SOURCE_HISTORY'
    IncludeSignalAudit = [bool]$IncludeSignalAudit
    IncludeReports = [bool]$IncludeReports
    ArtifactCount = @($artifacts).Count
    MetricSeriesCount = @($trends).Count
    KeyTrends = @($keyTrends)
    LargestAbsoluteMovement = @($largest)
    RecentArtifacts = @($recentArtifacts)
    Trends = @($trends)
    Artifacts = @($artifacts | ForEach-Object {
        [pscustomobject]@{
            Timestamp = $_.Timestamp.ToString('yyyy-MM-dd HH:mm:ss zzz')
            ArtifactType = $_.ArtifactType
            Path = $_.Path
            MetricCount = $_.MetricCount
        }
    })
}

$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $OutputJson -Encoding UTF8
Write-TrendMarkdown -Report $report -Path $OutputMarkdown

Write-Host ('HectonPhiTrend: artifacts={0} metricSeries={1} json={2} markdown={3}' -f @($artifacts).Count, @($trends).Count, (Convert-ToProjectRelativePath $OutputJson), (Convert-ToProjectRelativePath $OutputMarkdown))
