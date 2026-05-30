param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Snippet = 'Generated/settings_row_snippet.json',
    [string]$Target = 'Tables/settings.h8table.json',
    [switch]$Replace,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxSnippetJsonBytes = 65536
$MaxSettingsTableJsonBytes = 262144

function Fail([string]$Message) {
    Write-Error ('[H8MOD_SETTINGS_APPLY] ' + $Message)
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

    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.Trim() -cne $normalized) {
        Fail ($Label + ' must not contain leading or trailing whitespace.')
    }
    if ([System.IO.Path]::IsPathRooted($normalized) -or $normalized.StartsWith('/') -or $normalized.Contains(':')) {
        Fail ($Label + ' must be a starter-relative path.')
    }
    foreach ($segment in ($normalized -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            Fail ($Label + ' must not contain empty, dot, or dot-dot path segments.')
        }
    }
    if (-not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must stay under ' + $RequiredPrefix)
    }
    if (-not $normalized.EndsWith('.json', [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must end with .json.')
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = Join-StarterPath $Root $normalized
    }
}

function Read-JsonFile([string]$Path, [string]$Label, [long]$MaxBytes) {
    try {
        return Read-H8JsonFileCapped $Path $Label $MaxBytes
    } catch {
        Fail $_.Exception.Message
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

function Validate-RequiredText([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    return $trimmed
}

function Validate-Kind([string]$Value) {
    $trimmed = Validate-RequiredText $Value 'Kind'
    if (@('bool','int','float','string','enum') -notcontains $trimmed) {
        Fail 'Kind must be one of: bool, int, float, string, enum.'
    }
    return $trimmed
}

function Validate-SettingDefault([object]$Value, [string]$KindValue, [string]$Label) {
    switch ($KindValue) {
        'bool' {
            if ($Value -isnot [bool]) { Fail ($Label + ' Default must be a JSON boolean.') }
            return $Value
        }
        'int' {
            if (-not (($Value -is [int]) -or ($Value -is [long]))) { Fail ($Label + ' Default must be a JSON integer.') }
            return $Value
        }
        'float' {
            if (-not (($Value -is [double]) -or ($Value -is [decimal]) -or ($Value -is [single]) -or ($Value -is [int]) -or ($Value -is [long]))) {
                Fail ($Label + ' Default must be a JSON number.')
            }
            $asDouble = [double]$Value
            if ([double]::IsNaN($asDouble) -or [double]::IsInfinity($asDouble)) {
                Fail ($Label + ' Default must be finite.')
            }
            return $Value
        }
        'string' {
            return (Validate-RequiredText ([string]$Value) ($Label + ' Default'))
        }
        'enum' {
            return (Validate-RequiredText ([string]$Value) ($Label + ' Default'))
        }
        default {
            Fail ($Label + ' Kind must be one of: bool, int, float, string, enum.')
        }
    }
}

function Validate-StringOptions([object]$Value, [string]$Label) {
    if ($null -eq $Value -or -not $Value.GetType().IsArray) {
        Fail ($Label + ' Options must be a JSON array.')
    }

    $options = @($Value)
    if ($options.Count -lt 1 -or $options.Count -gt 64) {
        Fail ($Label + ' Options must contain 1..64 entries.')
    }

    $seen = @{}
    $clean = New-Object 'System.Collections.Generic.List[string]'
    for ($i = 0; $i -lt $options.Count; $i++) {
        $option = Validate-RequiredText ([string]$options[$i]) ($Label + ' Options[' + $i + ']')
        if ($seen.ContainsKey($option)) {
            Fail ($Label + ' Options contains a duplicate value: ' + $option)
        }
        $seen[$option] = $true
        [void]$clean.Add($option)
    }

    return $clean.ToArray()
}

function Build-CleanSettingsRow([object]$SnippetRow) {
    if ($null -eq $SnippetRow) { Fail 'Settings snippet row is null.' }

    $rowId = Validate-CanonicalId ([string]$SnippetRow.Id) 'Setting Id'
    $kind = Validate-Kind ([string]$SnippetRow.Kind)
    $defaultProperty = $SnippetRow.PSObject.Properties['Default']
    if ($null -eq $defaultProperty) { Fail 'Settings snippet Default is required.' }
    $defaultValue = Validate-SettingDefault $defaultProperty.Value $kind 'Settings snippet'

    $clean = [ordered]@{
        Id = $rowId
        Kind = $kind
        Default = $defaultValue
    }

    $labelProperty = $SnippetRow.PSObject.Properties['Label']
    if ($null -ne $labelProperty) {
        $clean.Label = Validate-RequiredText ([string]$labelProperty.Value) 'Settings snippet Label'
    }

    $descriptionProperty = $SnippetRow.PSObject.Properties['Description']
    if ($null -ne $descriptionProperty) {
        $clean.Description = [string]$descriptionProperty.Value
    }

    foreach ($numberName in @('Min','Max')) {
        $numberProperty = $SnippetRow.PSObject.Properties[$numberName]
        if ($null -ne $numberProperty) {
            if (-not (($numberProperty.Value -is [double]) -or ($numberProperty.Value -is [decimal]) -or ($numberProperty.Value -is [single]) -or ($numberProperty.Value -is [int]) -or ($numberProperty.Value -is [long]))) {
                Fail ('Settings snippet ' + $numberName + ' must be a JSON number.')
            }
            $numberValue = [double]$numberProperty.Value
            if ([double]::IsNaN($numberValue) -or [double]::IsInfinity($numberValue)) {
                Fail ('Settings snippet ' + $numberName + ' must be finite.')
            }
            $clean[$numberName] = $numberProperty.Value
        }
    }

    $optionsProperty = $SnippetRow.PSObject.Properties['Options']
    if ($null -ne $optionsProperty) {
        $clean.Options = Validate-StringOptions $optionsProperty.Value 'Settings snippet'
    }

    return [pscustomobject]$clean
}

function Get-SettingsSnippetRow([object]$SnippetDocument) {
    $rowProperty = $SnippetDocument.PSObject.Properties['Row']
    if ($null -ne $rowProperty) {
        return $rowProperty.Value
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
        throw ('Validation failed after settings apply: ' + $_.Exception.Message)
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$snippetPath = Resolve-StarterRelativePath $Snippet 'Generated/' 'Snippet'
$targetPath = Resolve-StarterRelativePath $Target 'Tables/' 'Target'
if ($targetPath.Relative -ne 'Tables/settings.h8table.json') {
    Fail 'Target must be Tables/settings.h8table.json for this tool.'
}

$snippetDocument = Read-JsonFile $snippetPath.Full 'Settings snippet' $MaxSnippetJsonBytes
$newRow = Build-CleanSettingsRow (Get-SettingsSnippetRow $snippetDocument)
$settings = Read-JsonFile $targetPath.Full 'Settings table' $MaxSettingsTableJsonBytes

if ([string]$settings.Schema -ne 'hecton8.settings_table.draft.v1') {
    Fail 'Tables/settings.h8table.json Schema must be hecton8.settings_table.draft.v1.'
}

$rowsProperty = $settings.PSObject.Properties['Rows']
if ($null -eq $rowsProperty -or $null -eq $rowsProperty.Value -or -not $rowsProperty.Value.GetType().IsArray) {
    Fail 'Tables/settings.h8table.json Rows must be a JSON array.'
}

$sourceRows = @($rowsProperty.Value)
$rows = New-Object 'System.Collections.Generic.List[object]'
$replaced = $false
for ($i = 0; $i -lt $sourceRows.Count; $i++) {
    $existingRow = $sourceRows[$i]
    if ($null -eq $existingRow) { Fail ('Tables/settings.h8table.json Rows[' + $i + '] must not be null.') }
    $existingId = Validate-CanonicalId ([string]$existingRow.Id) ('Tables/settings.h8table.json Rows[' + $i + '] Id')
    if ($existingId -eq $newRow.Id) {
        if (-not $Replace) {
            Fail ('Setting already exists: ' + $newRow.Id + '. Re-run with -Replace only if replacement is intended.')
        }
        [void]$rows.Add($newRow)
        $replaced = $true
    } else {
        [void]$rows.Add($existingRow)
    }
}

if (-not $replaced) {
    if ($rows.Count -ge 128) { Fail 'Tables/settings.h8table.json Rows already has 128 entries.' }
    [void]$rows.Add($newRow)
}

$document = [pscustomobject][ordered]@{
    Schema = 'hecton8.settings_table.draft.v1'
    Rows = $rows.ToArray()
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
    [void](Read-H8JsonFileCapped $tempPath 'Written settings table' $MaxSettingsTableJsonBytes)

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
        Schema = 'hecton8.settings_row_apply.v1'
        Runtime = 'envelope-only'
        Target = $targetPath.Relative
        Snippet = $snippetPath.Relative
        SettingId = $newRow.Id
        Replaced = $replaced
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 settings row snippet applied'
Write-Output ('Target: ' + $targetPath.Relative)
Write-Output ('Setting Id: ' + $newRow.Id)
Write-Output ('Replaced: ' + $replaced)
