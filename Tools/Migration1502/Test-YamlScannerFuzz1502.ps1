param(
    [string]$ProjectRoot = "C:\hades\Hecton8",
    [string]$ReportPath = "Docs/AgentLogs/YamlFuzz_1502.json"
)

$ErrorActionPreference = "Stop"

$targetDeletedNames = @(
    "_cellIntegrityFront",
    "_densityBuildSources",
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

$obsoleteKeyPattern = "^\s*(" + (($targetDeletedNames | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")\s*:"
$obsoleteOverridePattern = "propertyPath:\s*(" + (($targetDeletedNames | ForEach-Object { [regex]::Escape($_) }) -join "|") + ")(\.|$)"

$cases = @(
    [pscustomobject]@{
        name = "root obsolete key"
        text = "  _cellIntegrityFront: 010203"
        expectKey = $true
        expectOverride = $false
    },
    [pscustomobject]@{
        name = "close root key rejected"
        text = "  _cellIntegrityFrontier: 010203"
        expectKey = $false
        expectOverride = $false
    },
    [pscustomobject]@{
        name = "override obsolete path"
        text = "      propertyPath: _densityBuildSources.Array.data[0]"
        expectKey = $false
        expectOverride = $true
    },
    [pscustomobject]@{
        name = "override close path rejected"
        text = "      propertyPath: _densityBuildSourcesLegacy.Array.data[0]"
        expectKey = $false
        expectOverride = $false
    },
    [pscustomobject]@{
        name = "sonar exact key"
        text = "_publishedSonarSdf:"
        expectKey = $true
        expectOverride = $false
    },
    [pscustomobject]@{
        name = "combat exact key"
        text = "    _combatDamageArray:"
        expectKey = $true
        expectOverride = $false
    }
)

$startTicks = [System.Diagnostics.Stopwatch]::GetTimestamp()
$results = foreach ($case in $cases) {
    $actualKey = [regex]::IsMatch([string]$case.text, $obsoleteKeyPattern)
    $actualOverride = [regex]::IsMatch([string]$case.text, $obsoleteOverridePattern)
    [pscustomobject]@{
        name = $case.name
        passed = ($actualKey -eq [bool]$case.expectKey -and $actualOverride -eq [bool]$case.expectOverride)
        actualKey = $actualKey
        expectKey = [bool]$case.expectKey
        actualOverride = $actualOverride
        expectOverride = [bool]$case.expectOverride
    }
}

$elapsedUs = [int64](([System.Diagnostics.Stopwatch]::GetTimestamp() - $startTicks) * 1000000 / [System.Diagnostics.Stopwatch]::Frequency)
$report = [pscustomobject]@{
    agentId = "1502"
    evidenceClass = "STATIC_SOURCE_FUZZ"
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    caseCount = $results.Count
    passedCount = @($results | Where-Object { $_.passed }).Count
    failedCount = @($results | Where-Object { -not $_.passed }).Count
    elapsedMicroseconds = $elapsedUs
    results = @($results)
}

$projectFull = [System.IO.Path]::GetFullPath($ProjectRoot)
$reportFull = Resolve-UnderRoot $projectFull $ReportPath
$reportDir = [System.IO.Path]::GetDirectoryName($reportFull)
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportFull -Encoding UTF8
$report
