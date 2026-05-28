param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Snippet = 'Generated/locale_entry_snippet.json',
    [string]$Target = 'Locales/en.h8loc.json',
    [switch]$Replace,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_LOCALE_APPLY] ' + $Message)
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

function Resolve-StarterRelativePath([string]$RelativePath, [string]$RequiredPrefix, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail ($Label + ' is required.')
    }

    $normalized = $RelativePath.Replace('\','/').Trim()
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        Fail ($Label + ' must be a starter-relative path.')
    }

    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) {
        Fail ($Label + ' must not contain .. segments.')
    }

    if (-not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must stay under ' + $RequiredPrefix)
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = Join-StarterPath $Root $normalized
    }
}

function Read-JsonFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail ($Label + ' is missing: ' + $Path)
    }

    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    } catch {
        Fail ($Label + ' is invalid JSON: ' + $_.Exception.Message)
    }
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

function Get-LocaleSnippetEntry([object]$SnippetDocument) {
    $entryProperty = $SnippetDocument.PSObject.Properties['Entry']
    if ($null -ne $entryProperty) {
        return $entryProperty.Value
    }
    return $SnippetDocument
}

function Invoke-StarterValidator([string]$RootPath) {
    $validator = Join-StarterPath $RootPath 'Tools/validate_structure.ps1'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
        Fail 'Missing Tools/validate_structure.ps1.'
    }

    try {
        $global:LASTEXITCODE = 0
        & $validator -Root $RootPath -ThrowInsteadOfExit *>$null
    } catch {
        throw ('Validation failed after locale apply: ' + $_.Exception.Message)
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$snippetPath = Resolve-StarterRelativePath $Snippet 'Generated/' 'Snippet'
$targetPath = Resolve-StarterRelativePath $Target 'Locales/' 'Target'
if ($targetPath.Relative -ne 'Locales/en.h8loc.json') {
    Fail 'Target must be Locales/en.h8loc.json for this tool.'
}

$snippetDocument = Read-JsonFile $snippetPath.Full 'Locale snippet'
$snippetEntry = Get-LocaleSnippetEntry $snippetDocument
$localeKey = Validate-CanonicalId ([string]$snippetEntry.Key) 'Locale key'
$localeValue = Validate-LocaleValue ([string]$snippetEntry.Value)
$locale = Read-JsonFile $targetPath.Full 'Locale table'

if ([string]$locale.Schema -ne 'hecton8.locale.draft.v1') {
    Fail 'Locales/en.h8loc.json Schema must be hecton8.locale.draft.v1.'
}

$localeId = [string]$locale.Locale
if ([string]::IsNullOrWhiteSpace($localeId) -or $localeId -notmatch '^[a-z]{2}(-[A-Z]{2})?$') {
    Fail 'Locales/en.h8loc.json Locale must use xx or xx-YY form.'
}

$stringsProperty = $locale.PSObject.Properties['Strings']
if ($null -eq $stringsProperty -or $null -eq $stringsProperty.Value -or $stringsProperty.Value.GetType().IsArray) {
    Fail 'Locales/en.h8loc.json Strings must be a JSON object.'
}

$strings = [ordered]@{}
$replaced = $false
foreach ($entry in @($stringsProperty.Value.PSObject.Properties)) {
    $existingKey = Validate-CanonicalId ([string]$entry.Name) 'Locales/en.h8loc.json Strings key'
    if ($existingKey -eq $localeKey) {
        if (-not $Replace) {
            Fail ('Locale key already exists: ' + $localeKey + '. Re-run with -Replace only if replacement is intended.')
        }
        $strings[$existingKey] = $localeValue
        $replaced = $true
    } else {
        $strings[$existingKey] = Validate-LocaleValue ([string]$entry.Value)
    }
}

if (-not $replaced) {
    if ($strings.Count -ge 512) { Fail 'Locales/en.h8loc.json Strings already has 512 entries.' }
    $strings[$localeKey] = $localeValue
}

$document = [pscustomobject][ordered]@{
    Schema = 'hecton8.locale.draft.v1'
    Locale = $localeId
    Strings = $strings
}

$targetDirectory = Split-Path -Parent $targetPath.Full
$targetName = [System.IO.Path]::GetFileName($targetPath.Full)
$uniqueSuffix = [System.Guid]::NewGuid().ToString('N')
$tempPath = Join-Path $targetDirectory ('.' + $targetName + '.tmp-' + $uniqueSuffix)
$backupPath = Join-Path $targetDirectory ('.' + $targetName + '.previous-' + $uniqueSuffix)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

try {
    $jsonText = ($document | ConvertTo-Json -Depth 16)
    [System.IO.File]::WriteAllText($tempPath, ($jsonText + [System.Environment]::NewLine), $utf8NoBom)
    [void](Get-Content -Raw -LiteralPath $tempPath | ConvertFrom-Json)

    Move-Item -LiteralPath $targetPath.Full -Destination $backupPath -Force
    Move-Item -LiteralPath $tempPath -Destination $targetPath.Full -Force
    Invoke-StarterValidator $Root

    if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
        Remove-Item -LiteralPath $backupPath -Force
    }
} catch {
    if (Test-Path -LiteralPath $targetPath.Full -PathType Leaf) {
        Remove-Item -LiteralPath $targetPath.Full -Force
    }
    if (Test-Path -LiteralPath $backupPath -PathType Leaf) {
        Move-Item -LiteralPath $backupPath -Destination $targetPath.Full -Force
    }
    Fail $_.Exception.Message
} finally {
    if (Test-Path -LiteralPath $tempPath -PathType Leaf) {
        Remove-Item -LiteralPath $tempPath -Force
    }
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.locale_entry_apply.v1'
        Runtime = 'envelope-only'
        Target = $targetPath.Relative
        Snippet = $snippetPath.Relative
        Key = $localeKey
        Replaced = $replaced
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 locale entry snippet applied'
Write-Output ('Target: ' + $targetPath.Relative)
Write-Output ('Locale key: ' + $localeKey)
Write-Output ('Replaced: ' + $replaced)
