param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id,
    [string]$DisplayName,
    [string]$Author,
    [string]$Version
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_SET_IDENTITY] ' + $Message)
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

function Test-ReservedModIdSegment([string]$Segment) {
    if ([string]::IsNullOrWhiteSpace($Segment)) { return $false }
    switch ($Segment) {
        'con' { return $true }
        'prn' { return $true }
        'aux' { return $true }
        'nul' { return $true }
    }
    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) {
        return $true
    }
    return $false
}

function Validate-ModId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }
    }
    return $trimmed
}

function Validate-RequiredText([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    return $trimmed
}

function Validate-Version([string]$Value, [string]$Label) {
    $trimmed = Validate-RequiredText $Value $Label
    if ($trimmed -notmatch '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {
        Fail ($Label + ' must use semantic version form MAJOR.MINOR.PATCH with optional -prerelease or +build metadata.')
    }
    return $trimmed
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail ('Missing file: ' + $Path)
    }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 16
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $json + [System.Environment]::NewLine, $utf8NoBom)
}

if ([string]::IsNullOrWhiteSpace($Id)) {
    Fail 'Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0'
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$authoringPath = Join-StarterPath $rootFull 'mod.h8manifest.json'
$runtimePath = Join-StarterPath $rootFull 'mod.json'
$authoring = Read-JsonFile $authoringPath
$runtime = Read-JsonFile $runtimePath
$canonicalId = Validate-ModId $Id 'Id'

$authoring.Id = $canonicalId
$runtime.Id = $canonicalId

if (-not [string]::IsNullOrWhiteSpace($DisplayName)) {
    $canonicalDisplayName = Validate-RequiredText $DisplayName 'DisplayName'
    $authoring.DisplayName = $canonicalDisplayName
    $runtime.Name = $canonicalDisplayName
}

if (-not [string]::IsNullOrWhiteSpace($Author)) {
    $canonicalAuthor = Validate-RequiredText $Author 'Author'
    $authoring.Author = $canonicalAuthor
    $runtime.Author = $canonicalAuthor
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $canonicalVersion = Validate-Version $Version 'Version'
    $authoring.Version = $canonicalVersion
    $runtime.Version = $canonicalVersion
}

Write-JsonFile $authoringPath $authoring
Write-JsonFile $runtimePath $runtime

$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'
if (Test-Path -LiteralPath $validator -PathType Leaf) {
    & $validator -Root $rootFull | Out-Host
}

Write-Host ('PASS HECTON-8 starter identity set: ' + $canonicalId)
