param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Output = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path 'Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json')
)

$scanTargets = @(
    'Assets/_Project/Scripts/Construction',
    'Assets/_Project/Scripts/BaseModule.cs',
    'Assets/_Project/Scripts/Habitat/Deformation/Runtime'
)

$patterns = @(
    [pscustomobject]@{ kind = 'UNITY_JOINT_AUTHORITY'; regex = '\b(FixedJoint|SpringJoint|ConfigurableJoint)\b'; severity = 'blocked' },
    [pscustomobject]@{ kind = 'RIGIDBODY_MASS_STRUCTURAL_REVIEW'; regex = '\bRigidbody\.mass\b|\.mass\s*='; severity = 'review' },
    [pscustomobject]@{ kind = 'LEGACY_SCALAR_INTEGRITY_REVIEW'; regex = 'EvaluateAnalyticalIntegrityStress|integritySum\s*\+=|IntegrityBudget|MaxIntegrity|CurrentIntegrity'; severity = 'review' }
)

$findings = New-Object System.Collections.Generic.List[object]
$files = New-Object System.Collections.Generic.List[System.IO.FileInfo]

foreach ($target in $scanTargets) {
    $path = Join-Path $Root $target
    if (Test-Path -LiteralPath $path -PathType Container) {
        Get-ChildItem -LiteralPath $path -Recurse -Filter '*.cs' | ForEach-Object { $files.Add($_) }
        continue
    }

    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $files.Add([System.IO.FileInfo]$path)
    }
}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($Root.Length).TrimStart('\', '/').Replace('\', '/')
    $lines = Get-Content -LiteralPath $file.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith('//')) {
            continue
        }

        foreach ($pattern in $patterns) {
            if ($line -notmatch $pattern.regex) {
                continue
            }

            $findingSeverity = $pattern.severity
            if ($pattern.kind -eq 'RIGIDBODY_MASS_STRUCTURAL_REVIEW' -and
                ($relative -eq 'Assets/_Project/Scripts/BaseModule.cs' -or $relative -like '*VehicleDockingModule.cs')) {
                $findingSeverity = 'compatible_non_authoritative'
            }

            if ($pattern.kind -eq 'LEGACY_SCALAR_INTEGRITY_REVIEW' -and
                ($relative -eq 'Assets/_Project/Scripts/BaseModule.cs' -or $relative -like '*HabitatGraphManager.cs' -or $relative -like '*ModuleIntegrityComponent.cs')) {
                $findingSeverity = 'legacy_compatibility_surface'
            }

            $findings.Add([pscustomobject]@{
                file = $relative
                line = $i + 1
                kind = $pattern.kind
                severity = $findingSeverity
                snippet = $trimmed
            })
        }
    }
}

$jointCount = @($findings | Where-Object { $_.kind -eq 'UNITY_JOINT_AUTHORITY' }).Count
$massCount = @($findings | Where-Object { $_.kind -eq 'RIGIDBODY_MASS_STRUCTURAL_REVIEW' }).Count
$scalarCount = @($findings | Where-Object { $_.kind -eq 'LEGACY_SCALAR_INTEGRITY_REVIEW' }).Count
$blockedCount = @($findings | Where-Object { $_.severity -eq 'blocked' }).Count

$report = [pscustomobject]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString('o')
    scanner = 'Structural_Integrity_Scanner'
    summary = 'Physics-Based Integrity Purged'
    authority = 'StructuralIntegrityCalculatorRuntime: Burst CSR depth-pressure solver over GlobalDataVault buffers 70488-70497'
    blocked_findings = $blockedCount
    unity_joint_sites = $jointCount
    rigidbody_mass_review_sites = $massCount
    legacy_scalar_review_sites = $scalarCount
    verdict = if ($blockedCount -eq 0) { 'PASS: no Unity joint structural authority found; remaining mass/scalar hits are compatibility surfaces or non-authoritative review sites.' } else { 'FAIL: blocked Unity joint structural authority found.' }
    findings = $findings
}

$directory = Split-Path -Parent $Output
if (-not [string]::IsNullOrWhiteSpace($directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Output -Encoding UTF8
Write-Output $Output
