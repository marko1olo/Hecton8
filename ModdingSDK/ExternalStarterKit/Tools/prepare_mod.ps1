param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id,
    [string]$DisplayName,
    [string]$Author,
    [string]$Version,
    [string]$ReviewOutput = 'Reports/review_manifest.json'
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_PREPARE] ' + $Message)
    exit 1
}

function Join-StarterPath([string]$BasePath, [string]$RelativePath) {
    $current = $BasePath
    foreach ($segment in ($RelativePath.Replace('\','/') -split '/')) {
        if (-not [string]::IsNullOrWhiteSpace($segment)) {
            $current = Join-Path $current $segment
        }
    }
    return $current
}

if ([string]::IsNullOrWhiteSpace($Id)) {
    Fail 'Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/prepare_mod.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0'
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$identityTool = Join-StarterPath $rootFull 'Tools/set_mod_identity.ps1'
$reviewTool = Join-StarterPath $rootFull 'Tools/build_review_manifest.ps1'

if (-not (Test-Path -LiteralPath $identityTool -PathType Leaf)) {
    Fail 'Missing Tools/set_mod_identity.ps1.'
}

if (-not (Test-Path -LiteralPath $reviewTool -PathType Leaf)) {
    Fail 'Missing Tools/build_review_manifest.ps1.'
}

& $identityTool -Root $rootFull -Id $Id -DisplayName $DisplayName -Author $Author -Version $Version | Out-Host

& $reviewTool -Root $rootFull -Output $ReviewOutput | Out-Host

Write-Host ('PASS HECTON-8 starter prepared: ' + $Id)
