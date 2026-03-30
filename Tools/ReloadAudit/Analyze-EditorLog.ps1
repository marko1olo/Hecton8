[CmdletBinding()]
param(
    [string]$EditorLogPath = (Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'),
    [int]$TailLines = 8000,
    [int]$TopSteps = 20,
    [string]$OutputMarkdown = 'C:\hades\Hecton8\UNITY_RELOAD_FINDINGS.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $EditorLogPath)) {
    throw "Editor.log not found: $EditorLogPath"
}

$allLines = Get-Content -Path $EditorLogPath
$lines = $allLines | Select-Object -Last $TailLines

$domainReloadRegex = [regex]'Domain Reload Profiling:\s+(?<ms>\d+)ms'
$assetRefreshRegex = [regex]'Asset Pipeline Refresh .* Total:\s+(?<seconds>[\d\.]+)\s+seconds'
$stepRegex = [regex]'^\s+(?<name>[A-Za-z][A-Za-z0-9]+(?:[A-Za-z0-9 ]*[A-Za-z0-9])?)\s+\((?<ms>\d+)ms\)$'

$domainReloads = foreach ($line in $lines) {
    $match = $domainReloadRegex.Match($line)
    if ($match.Success) {
        [int]$match.Groups['ms'].Value
    }
}

$assetRefreshes = foreach ($line in $lines) {
    $match = $assetRefreshRegex.Match($line)
    if ($match.Success) {
        [double]::Parse($match.Groups['seconds'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
    }
}

$stepSummary = $lines |
    ForEach-Object {
        $match = $stepRegex.Match($_)
        if ($match.Success) {
            [PSCustomObject]@{
                Name = $match.Groups['name'].Value.Trim()
                Ms   = [int]$match.Groups['ms'].Value
            }
        }
    } |
    Where-Object { $_ -and $_.Ms -ge 1000 } |
    Group-Object -Property Name |
    ForEach-Object {
        [PSCustomObject]@{
            Name  = $_.Name
            MaxMs = ($_.Group | Measure-Object -Property Ms -Maximum).Maximum
            AvgMs = [int](($_.Group | Measure-Object -Property Ms -Average).Average)
            Count = $_.Count
        }
    } |
    Sort-Object -Property MaxMs -Descending |
    Select-Object -First $TopSteps

$now = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$sb = [System.Text.StringBuilder]::new()

[void]$sb.AppendLine('# Unity Reload Findings')
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Generated: $($now)")
[void]$sb.AppendLine("- Source: $($EditorLogPath)")
[void]$sb.AppendLine("- Tail lines analyzed: $($TailLines)")
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Summary')
[void]$sb.AppendLine()

if ($domainReloads.Count -gt 0) {
    $maxReload = ($domainReloads | Measure-Object -Maximum).Maximum
    $avgReload = [int](($domainReloads | Measure-Object -Average).Average)
    [void]$sb.AppendLine("- Domain reload samples: $($domainReloads.Count)")
    [void]$sb.AppendLine("- Domain reload max: $($maxReload) ms")
    [void]$sb.AppendLine("- Domain reload avg: $($avgReload) ms")
} else {
    [void]$sb.AppendLine('- Domain reload samples: none found')
}

if ($assetRefreshes.Count -gt 0) {
    $maxRefresh = ($assetRefreshes | Measure-Object -Maximum).Maximum
    $avgRefresh = ($assetRefreshes | Measure-Object -Average).Average
    [void]$sb.AppendLine("- Asset refresh samples: $($assetRefreshes.Count)")
    [void]$sb.AppendLine("- Asset refresh max: $([math]::Round($maxRefresh, 3)) s")
    [void]$sb.AppendLine("- Asset refresh avg: $([math]::Round($avgRefresh, 3)) s")
} else {
    [void]$sb.AppendLine('- Asset refresh samples: none found')
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Top Expensive Reload Steps')
[void]$sb.AppendLine()

if ($stepSummary.Count -eq 0) {
    [void]$sb.AppendLine('- none found')
} else {
    foreach ($step in $stepSummary) {
        [void]$sb.AppendLine("- ``$($step.Name)``: max $($step.MaxMs) ms, avg $($step.AvgMs) ms, seen $($step.Count)x")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Reading')
[void]$sb.AppendLine()
[void]$sb.AppendLine('- If `CompileScripts` is small but `SetupLoadedEditorAssemblies` and `ProcessInitializeOnLoadAttributes` are large, the bottleneck is editor reload work, not plain script compilation.')
[void]$sb.AppendLine('- If `AwakeInstancesAfterBackupRestoration` is large, edit-mode objects and editor-time scene state are taking too long to wake back up after reload.')
[void]$sb.AppendLine('- If asset refresh spikes into triple digits, import/refresh behavior and package editors may also be contributing.')

$parent = Split-Path -Parent $OutputMarkdown
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputMarkdown, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Wrote $OutputMarkdown"
