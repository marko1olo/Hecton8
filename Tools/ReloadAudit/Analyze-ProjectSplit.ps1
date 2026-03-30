[CmdletBinding()]
param(
    [string]$ProjectRoot = 'C:\hades\Hecton8',
    [string]$OutputMarkdown = 'C:\hades\Hecton8\UNITY_PROJECT_SPLIT_REPORT.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $ProjectRoot 'Assets\_Project'
if (-not (Test-Path $projectPath)) {
    throw "Missing _Project path: $projectPath"
}

$allScripts = @(Get-ChildItem -Path $projectPath -Recurse -Filter *.cs -ErrorAction SilentlyContinue)
$editorScripts = @($allScripts | Where-Object { $_.FullName -match '\\Editor\\' })
$runtimeScripts = @($allScripts | Where-Object { $_.FullName -notmatch '\\Editor\\' })

$editorSignals = @(
    'using UnityEditor;',
    '#if UNITY_EDITOR',
    'MenuItem(',
    'CustomEditor(',
    'EditorWindow',
    'PropertyDrawer',
    'AssetPostprocessor',
    'EditorApplication.',
    'SceneView.',
    'AssemblyReloadEvents.'
)

$runtimeEditorCoupling = @(
foreach ($file in $runtimeScripts) {
    $hits = @(Select-String -Path $file.FullName -Pattern $editorSignals -SimpleMatch)
    if ($hits.Count -gt 0) {
        [PSCustomObject]@{
            Path = $file.FullName
            RelativePath = $file.FullName.Replace($ProjectRoot, '').TrimStart('\')
            Hits = $hits.Count
        }
    }
}
)

$runtimeEditorCoupling = @(
    $runtimeEditorCoupling |
        Sort-Object `
            @{ Expression = 'Hits'; Descending = $true }, `
            @{ Expression = 'RelativePath'; Descending = $false }
)

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# Unity _Project Split Report')
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("- Project root: $ProjectRoot")
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Counts')
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Total `_Project` C# files: $($allScripts.Count)")
[void]$sb.AppendLine("- Runtime-side files: $($runtimeScripts.Count)")
[void]$sb.AppendLine("- Editor-side files: $($editorScripts.Count)")
[void]$sb.AppendLine("- Runtime files with editor coupling signals: $($runtimeEditorCoupling.Count)")
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Reading')
[void]$sb.AppendLine()
[void]$sb.AppendLine('- Runtime files that still contain `UnityEditor` usage or editor-only hooks are the first blockers for a safe asmdef/runtime split.')
[void]$sb.AppendLine('- Files in `Assets/_Project/Editor` are already natural candidates for an editor-only assembly.')
[void]$sb.AppendLine('- Files outside `Editor` with many editor coupling signals should be cleaned or partially extracted before introducing `_Project` asmdefs.')
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Runtime Files With Editor Coupling Signals')
[void]$sb.AppendLine()

if (-not $runtimeEditorCoupling) {
    [void]$sb.AppendLine('- none found')
} else {
    foreach ($entry in $runtimeEditorCoupling) {
        [void]$sb.AppendLine("- $($entry.Hits) : ``$($entry.RelativePath)``")
    }
}

$parent = Split-Path -Parent $OutputMarkdown
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputMarkdown, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Wrote $OutputMarkdown"
