[CmdletBinding()]
param(
    [string]$ProjectRoot = 'C:\hades\Hecton8',
    [string]$OutputMarkdown = 'C:\hades\Hecton8\UNITY_RELOAD_HOOKS_REPORT.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$paths = @(
    (Join-Path $ProjectRoot 'Assets'),
    (Join-Path $ProjectRoot 'Packages')
) | Where-Object { Test-Path $_ }

$patterns = @(
    '\[InitializeOnLoad\]',
    '\[InitializeOnLoadMethod\]',
    '\[ExecuteAlways\]',
    '\[ExecuteInEditMode\]',
    'EditorApplication\.update',
    'EditorApplication\.delayCall',
    'EditorApplication\.playModeStateChanged',
    'AssemblyReloadEvents\.',
    '\[DidReloadScripts\]',
    'AssetPostprocessor',
    'SceneView\.duringSceneGui',
    'RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType\.SubsystemRegistration'
)

$results = foreach ($path in $paths) {
    Get-ChildItem -Path $path -Recurse -Filter *.cs -ErrorAction SilentlyContinue |
        Select-String -Pattern $patterns |
        Group-Object -Property Path |
        ForEach-Object {
            [PSCustomObject]@{
                Hits = $_.Count
                Path = $_.Name
            }
        }
}

$results = $results | Sort-Object `
    @{ Expression = 'Hits'; Descending = $true }, `
    @{ Expression = 'Path'; Descending = $false }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# Unity Reload Hooks Report')
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("- Project root: $($ProjectRoot)")
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Files With Reload / Editor Hook Signals')
[void]$sb.AppendLine()

if (-not $results) {
    [void]$sb.AppendLine('- none found')
} else {
    foreach ($entry in $results) {
        $relative = $entry.Path.Replace($ProjectRoot, '').TrimStart('\')
        [void]$sb.AppendLine("- $($entry.Hits) : ``$relative``")
    }
}

$parent = Split-Path -Parent $OutputMarkdown
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputMarkdown, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Wrote $OutputMarkdown"
