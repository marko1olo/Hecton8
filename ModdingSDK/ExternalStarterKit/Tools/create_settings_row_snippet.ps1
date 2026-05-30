param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id = 'setting.example_toggle',
    [string]$Kind = 'bool',
    [string]$Default = 'false',
    [string]$Output = 'Generated/settings_row_snippet.json',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_SETTINGS_SNIPPET] ' + $Message)
    exit 1
}

function Join-StarterPath {
    param(
        [string]$BasePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Segments
    )

    $current = $BasePath
    foreach ($segment in $Segments) {
        foreach ($part in ($segment.Replace('\','/') -split '/')) {
            if (-not [string]::IsNullOrWhiteSpace($part)) {
                $current = Join-Path $current $part
            }
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

function Validate-CanonicalId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed.Length -gt 96) { Fail ($Label + ' must be 96 characters or shorter.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }
    }
    return $trimmed
}

function Validate-Kind([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Kind is required.' }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail 'Kind must not contain leading or trailing whitespace.' }
    if (@('bool','int','float','string','enum') -notcontains $trimmed) {
        Fail 'Kind must be one of: bool, int, float, string, enum.'
    }
    return $trimmed
}

function Convert-DefaultValue([string]$Value, [string]$KindValue) {
    if ($null -eq $Value) { Fail 'Default is required.' }
    $trimmed = $Value.Trim()
    switch ($KindValue) {
        'bool' {
            if ($trimmed -ieq 'true') { return $true }
            if ($trimmed -ieq 'false') { return $false }
            Fail 'Default for bool settings must be true or false.'
        }
        'int' {
            $parsed = [long]0
            if (-not [long]::TryParse($trimmed, [ref]$parsed)) {
                Fail 'Default for int settings must be a JSON integer.'
            }
            return $parsed
        }
        'float' {
            $parsed = [double]0
            if (-not [double]::TryParse($trimmed, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
                Fail 'Default for float settings must be a JSON number.'
            }
            if ([double]::IsNaN($parsed) -or [double]::IsInfinity($parsed)) {
                Fail 'Default for float settings must be finite.'
            }
            return $parsed
        }
        'string' {
            if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Default for string settings must not be empty.' }
            if ($trimmed -ne $Value) { Fail 'Default for string settings must not contain leading or trailing whitespace.' }
            return $Value
        }
        'enum' {
            if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Default for enum settings must not be empty.' }
            if ($trimmed -ne $Value) { Fail 'Default for enum settings must not contain leading or trailing whitespace.' }
            return $Value
        }
    }
}

function Test-StrictJsonRelativePath([string]$RelativePath, [string]$RequiredPrefix, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail ($Label + ' is required.')
    }

    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.Trim() -cne $normalized) {
        Fail ($Label + ' must not contain leading or trailing whitespace.')
    }
    if ([System.IO.Path]::IsPathRooted($normalized) -or $normalized.StartsWith('/') -or $normalized.Contains(':')) {
        Fail ($Label + ' must be a starter-relative path.')
    }
    if (-not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must stay under ' + $RequiredPrefix)
    }
    if (-not $normalized.EndsWith('.json', [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must end with .json.')
    }

    foreach ($segment in ($normalized -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            Fail ($Label + ' must not contain empty, dot, or dot-dot path segments.')
        }
    }

    return $normalized
}

function Resolve-GeneratedOutputPath([string]$RelativePath) {
    $normalized = Test-StrictJsonRelativePath $RelativePath 'Generated/' 'Output'

    $directory = Join-StarterPath $Root 'Generated'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $directory -Force)
    }

    $outputPath = Join-StarterPath $Root $normalized
    $outputDirectory = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $outputDirectory -Force)
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = $outputPath
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$settingId = Validate-CanonicalId $Id 'Setting Id'
$settingKind = Validate-Kind $Kind
$defaultValue = Convert-DefaultValue $Default $settingKind
$outputPath = Resolve-GeneratedOutputPath $Output

$row = [pscustomobject][ordered]@{
    Id = $settingId
    Kind = $settingKind
    Default = $defaultValue
    Notes = 'Apply with h8mod.ps1 -Action apply-setting-snippet, or copy this object into Tables/settings.h8table.json Rows[] and run h8mod.ps1 -Action validate.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$rowJson = ($row | ConvertTo-Json -Depth 8)
[System.IO.File]::WriteAllText($outputPath.Full, ($rowJson + [System.Environment]::NewLine), $utf8NoBom)

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.settings_row_snippet.v1'
        Runtime = 'envelope-only'
        Output = $outputPath.Relative
        Row = $row
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 settings row snippet written'
Write-Output ('Output: ' + $outputPath.Relative)
Write-Output ('Setting Id: ' + $settingId)
Write-Output ('Kind: ' + $settingKind)
Write-Output 'Next: h8mod.ps1 -Action apply-setting-snippet. Manual fallback: copy the JSON object into Tables/settings.h8table.json Rows[], then run h8mod.ps1 -Action validate.'
