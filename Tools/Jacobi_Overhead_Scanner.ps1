param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Output = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Docs/Reports/MATH_OPTIMIZATION_REPORT.json')
)

$patterns = @(
    'for\s*\(\s*int\s+\w+\s*=\s*0\s*;\s*\w+\s*<\s*[^;]*(Iteration|iteration|Jacobi|jacobi|Pass|pass)[^;]*;'
)

$findings = New-Object System.Collections.Generic.List[object]
$searchRoots = @(
    'Assets/_Project/Scripts/Power',
    'Assets/_Project/Scripts/Thermodynamics',
    'Assets/_Project/Scripts/Habitat',
    'Assets/_Project/Scripts/Environment'
)

$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
foreach ($relativeRoot in $searchRoots) {
    $absoluteRoot = Join-Path $Root $relativeRoot
    if (Test-Path -LiteralPath $absoluteRoot) {
        Get-ChildItem -LiteralPath $absoluteRoot -Recurse -Filter '*.cs' | ForEach-Object { $files.Add($_) }
    }
}

foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $matched = $false
        foreach ($pattern in $patterns) {
            if ($line -match $pattern) {
                $matched = $true
                break
            }
        }

        if (-not $matched) {
            continue
        }

        $contextStart = [Math]::Max(0, $i - 5)
        $contextEnd = [Math]::Min($lines.Count - 1, $i + 12)
        $context = ($lines[$contextStart..$contextEnd] -join "`n")
        $hasResidual = $context -match 'Residual|residual|Tolerance|tolerance|Omega|omega|Convergence|convergence'
        $findings.Add([pscustomobject]@{
            file = $file.FullName.Substring($Root.Length).TrimStart('\')
            line = $i + 1
            text = $line.Trim()
            residual_guard = $hasResidual
        })
    }
}

$report = [pscustomobject]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString('o')
    scanner = 'Jacobi_Overhead_Scanner'
    blind_iteration_candidates = @($findings | Where-Object { -not $_.residual_guard }).Count
    guarded_iteration_sites = @($findings | Where-Object { $_.residual_guard }).Count
    findings = $findings
}

$directory = Split-Path -Parent $Output
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding UTF8
Write-Output $Output
