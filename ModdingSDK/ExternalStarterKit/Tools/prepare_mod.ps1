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

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$identityTool = Join-StarterPath $rootFull 'Tools/set_mod_identity.ps1'
$reviewTool = Join-StarterPath $rootFull 'Tools/build_review_manifest.ps1'
$hasIdentityEdits = -not [string]::IsNullOrWhiteSpace($Id)

if ((-not $hasIdentityEdits) -and
    ((-not [string]::IsNullOrWhiteSpace($DisplayName)) -or
     (-not [string]::IsNullOrWhiteSpace($Author)) -or
     (-not [string]::IsNullOrWhiteSpace($Version)))) {
    Fail 'Id is required when changing identity fields. Omit all identity arguments to validate the existing manifests.'
}

if (-not (Test-Path -LiteralPath $reviewTool -PathType Leaf)) {
    Fail 'Missing Tools/build_review_manifest.ps1.'
}

if ($hasIdentityEdits) {
    if (-not (Test-Path -LiteralPath $identityTool -PathType Leaf)) {
        Fail 'Missing Tools/set_mod_identity.ps1.'
    }

    & $identityTool -Root $rootFull -Id $Id -DisplayName $DisplayName -Author $Author -Version $Version | Out-Host
}

& $reviewTool -Root $rootFull -Output $ReviewOutput | Out-Host

$reviewOutputPath = if ([System.IO.Path]::IsPathRooted($ReviewOutput)) {
    $ReviewOutput
} else {
    Join-StarterPath $rootFull $ReviewOutput
}

if (-not (Test-Path -LiteralPath $reviewOutputPath -PathType Leaf)) {
    Fail 'Review manifest was not written.'
}

$review = Get-Content -Raw -LiteralPath $reviewOutputPath | ConvertFrom-Json
$preparedId = [string]$review.Identity.Id
if ([string]::IsNullOrWhiteSpace($preparedId)) {
    $preparedId = [string]$review.RootId
}
if ([string]::IsNullOrWhiteSpace($preparedId)) {
    Fail 'Review manifest did not report package identity.'
}

Write-Host ('PASS HECTON-8 starter prepared: ' + $preparedId)
