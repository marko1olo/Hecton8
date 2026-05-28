param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Key = 'text.example_line',
    [string]$Value = 'Your localized text',
    [string]$Output = 'Generated/locale_entry_snippet.json',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_LOCALE_SNIPPET] ' + $Message)
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

function Validate-CanonicalId([string]$InputValue, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($InputValue)) { Fail ($Label + ' is required.') }
    $trimmed = $InputValue.Trim()
    if ($trimmed -ne $InputValue) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed.Length -gt 96) { Fail ($Label + ' must be 96 characters or shorter.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }
    }
    return $trimmed
}

function Validate-LocaleValue([string]$InputValue) {
    if ([string]::IsNullOrWhiteSpace($InputValue)) {
        Fail 'Locale value is required.'
    }
    $trimmed = $InputValue.Trim()
    if ($trimmed -ne $InputValue) {
        Fail 'Locale value must not contain leading or trailing whitespace.'
    }
    if ($trimmed.Length -gt 2048) {
        Fail 'Locale value must be 2048 characters or shorter.'
    }
    return $InputValue
}

function Resolve-GeneratedOutputPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail 'Output is required.'
    }

    $normalized = $RelativePath.Replace('\','/').Trim()
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        Fail 'Output must be a starter-relative path under Generated/.'
    }

    if ($normalized.Contains('..') -or -not $normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) {
        Fail 'Output must stay under Generated/ and must not contain .. segments.'
    }

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
$localeKey = Validate-CanonicalId $Key 'Locale key'
$localeValue = Validate-LocaleValue $Value
$outputPath = Resolve-GeneratedOutputPath $Output

$entry = [pscustomobject][ordered]@{
    Key = $localeKey
    Value = $localeValue
    Notes = 'Apply with h8mod.ps1 -Action apply-locale-snippet, or copy Key and Value into Locales/en.h8loc.json Strings and run h8mod.ps1 -Action validate.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$entryJson = ($entry | ConvertTo-Json -Depth 8)
[System.IO.File]::WriteAllText($outputPath.Full, ($entryJson + [System.Environment]::NewLine), $utf8NoBom)

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.locale_entry_snippet.v1'
        Runtime = 'envelope-only'
        Output = $outputPath.Relative
        Entry = $entry
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 locale entry snippet written'
Write-Output ('Output: ' + $outputPath.Relative)
Write-Output ('Locale key: ' + $localeKey)
Write-Output 'Next: h8mod.ps1 -Action apply-locale-snippet. Manual fallback: copy Key and Value into Locales/en.h8loc.json Strings, then run h8mod.ps1 -Action validate.'
