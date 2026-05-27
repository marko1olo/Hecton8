param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_STARTER_VALIDATION] ' + $Message)
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

function Require-File([string]$RelativePath) {
    $path = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail ('Missing required file: ' + $RelativePath)
    }
    return $path
}

function Require-Directory([string]$RelativePath) {
    $path = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        Fail ('Missing required directory: ' + $RelativePath)
    }
}

function Read-Json([string]$RelativePath) {
    $path = Require-File $RelativePath
    try {
        return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    } catch {
        Fail ('Invalid JSON in ' + $RelativePath + ': ' + $_.Exception.Message)
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

function Read-AllowedGraphOpcodeTokens() {
    $path = Require-File 'Reference/allowed_opcodes.csv'
    $tokens = @{}
    foreach ($line in (Get-Content -LiteralPath $path)) {
        $text = [string]$line
        $comment = ''
        $commentIndex = $text.IndexOf('#')
        if ($commentIndex -ge 0) {
            $comment = $text.Substring($commentIndex + 1).Trim()
            $text = $text.Substring(0, $commentIndex).Trim()
        } else {
            $text = $text.Trim()
        }

        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        if ($text -notmatch '^0x[0-9A-Fa-f]{1,8}$') {
            Fail ('Reference/allowed_opcodes.csv contains invalid opcode token: ' + $text)
        }

        $tokens[$text.ToUpperInvariant()] = $true
        if (-not [string]::IsNullOrWhiteSpace($comment)) {
            $alias = @($comment -split '\s+')[0]
            if ($alias -match '^[A-Za-z][A-Za-z0-9_]*$') {
                $tokens[$alias] = $true
            }
        }
    }

    if ($tokens.Count -eq 0) { Fail 'Reference/allowed_opcodes.csv has no allowed graph opcodes.' }
    return $tokens
}

@('Content','Graphs','Tables','Locales','Generated','Reports','Reference','Schemas','Tools','.vscode') | ForEach-Object { Require-Directory $_ }
@(
    'README.md',
    'mod.h8manifest.json',
    'mod.json',
    'Content/README.md',
    'Content/assets.h8manifest.json',
    'Graphs/main.h8graph.json',
    'Tables/settings.h8table.json',
    'Locales/en.h8loc.json',
    'Generated/README.md',
    'Reports/README.md',
    'Reference/README.md',
    'Reference/allowed_opcodes.csv',
    'Reference/kernel_tuning_profiles.csv',
    'Schemas/assets.schema.json',
    'Schemas/h8graph.schema.json',
    'Schemas/h8mod.authoring.schema.json',
    'Schemas/locale.schema.json',
    'Schemas/runtime.mod.schema.json',
    'Schemas/settings_table.schema.json',
    'Tools/README.md',
    'Tools/build_review_manifest.ps1',
    'Tools/list_allowed_opcodes.ps1',
    'Tools/prepare_mod.ps1',
    'Tools/set_mod_identity.ps1',
    'Tools/validate_structure.ps1',
    '.vscode/settings.json'
) | ForEach-Object { [void](Require-File $_) }

$authoring = Read-Json 'mod.h8manifest.json'
$runtime = Read-Json 'mod.json'
$graph = Read-Json 'Graphs/main.h8graph.json'
$assets = Read-Json 'Content/assets.h8manifest.json'
$settings = Read-Json 'Tables/settings.h8table.json'
$locale = Read-Json 'Locales/en.h8loc.json'
$vscodeSettings = Read-Json '.vscode/settings.json'
$schemaFiles = @(
    'Schemas/assets.schema.json',
    'Schemas/h8graph.schema.json',
    'Schemas/h8mod.authoring.schema.json',
    'Schemas/locale.schema.json',
    'Schemas/runtime.mod.schema.json',
    'Schemas/settings_table.schema.json'
)
foreach ($schemaFile in $schemaFiles) {
    $schema = Read-Json $schemaFile
    if ($null -eq $schema.PSObject.Properties['$schema']) { Fail ($schemaFile + ' requires $schema.') }
    if ([string]::IsNullOrWhiteSpace([string]$schema.title)) { Fail ($schemaFile + ' requires title.') }
    if ([string]$schema.type -ne 'object') { Fail ($schemaFile + ' must describe a JSON object.') }
}

$authoringId = Validate-ModId ([string]$authoring.Id) 'mod.h8manifest.json Id'
$authoringDisplayName = Validate-RequiredText ([string]$authoring.DisplayName) 'mod.h8manifest.json DisplayName'
$authoringAuthor = Validate-RequiredText ([string]$authoring.Author) 'mod.h8manifest.json Author'
$authoringVersion = Validate-Version ([string]$authoring.Version) 'mod.h8manifest.json Version'
if ([string]$authoring.Compatibility.Runtime -ne 'envelope-only') { Fail 'mod.h8manifest.json Compatibility.Runtime must be envelope-only.' }
if ([int]$authoring.RequiredAPIVersion -lt 2) { Fail 'mod.h8manifest.json RequiredAPIVersion must be >= 2.' }
$runtimeId = Validate-ModId ([string]$runtime.Id) 'mod.json Id'
if ($authoringId -ne $runtimeId) { Fail 'mod.h8manifest.json Id must match mod.json Id.' }
$runtimeName = Validate-RequiredText ([string]$runtime.Name) 'mod.json Name'
$runtimeAuthor = Validate-RequiredText ([string]$runtime.Author) 'mod.json Author'
$runtimeVersion = Validate-Version ([string]$runtime.Version) 'mod.json Version'
if ($authoringDisplayName -ne $runtimeName) { Fail 'mod.h8manifest.json DisplayName must match mod.json Name.' }
if ($authoringAuthor -ne $runtimeAuthor) { Fail 'mod.h8manifest.json Author must match mod.json Author.' }
if ($authoringVersion -ne $runtimeVersion) { Fail 'mod.h8manifest.json Version must match mod.json Version.' }
if ($null -ne $runtime.Dependencies) {
    foreach ($dependencyId in @($runtime.Dependencies)) {
        if (-not [string]::IsNullOrWhiteSpace([string]$dependencyId)) {
            [void](Validate-ModId ([string]$dependencyId) 'mod.json Dependencies item')
        }
    }
}
if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryAssembly)) { Fail 'mod.json EntryAssembly must stay empty in envelope-only starter kits.' }
if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryType)) { Fail 'mod.json EntryType must stay empty in envelope-only starter kits.' }
if ([int]$runtime.RequiredAPIVersion -lt 2) { Fail 'mod.json RequiredAPIVersion must be >= 2.' }
if ([string]$graph.Runtime -ne 'envelope-only') { Fail 'Graphs/main.h8graph.json Runtime must be envelope-only.' }
if ([int]$graph.MaxEnvelopesPerFrame -gt [int]$authoring.Budgets.MaxEnvelopesPerFrame) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must not exceed mod.h8manifest.json Budgets.MaxEnvelopesPerFrame.' }
$allowedGraphOpcodes = Read-AllowedGraphOpcodeTokens
$graphNodeIds = @{}
$graphOpcodeNodeCount = 0
foreach ($node in @($graph.Nodes)) {
    if ($null -eq $node) { Fail 'Graphs/main.h8graph.json Nodes must not contain null entries.' }
    $nodeId = [string]$node.Id
    if ([string]::IsNullOrWhiteSpace($nodeId)) { Fail 'Graphs/main.h8graph.json node Id is required.' }
    if ($graphNodeIds.ContainsKey($nodeId)) { Fail ('Graphs/main.h8graph.json duplicate node Id: ' + $nodeId) }
    $graphNodeIds[$nodeId] = $true
    $opcode = [string]$node.Opcode
    if ([string]::IsNullOrWhiteSpace($opcode)) { Fail ('Graphs/main.h8graph.json node Opcode is required: ' + $nodeId) }
    $opcode = $opcode.Trim()
    $opcodeToken = $opcode
    if ($opcode -match '^0x[0-9A-Fa-f]{1,8}$') {
        $opcodeToken = $opcode.ToUpperInvariant()
    }
    if (-not $allowedGraphOpcodes.ContainsKey($opcodeToken)) {
        Fail ('Graphs/main.h8graph.json node Opcode is not in Reference/allowed_opcodes.csv: ' + $opcode)
    }
    $graphOpcodeNodeCount++
}
if ($graphOpcodeNodeCount -gt 0 -and [int]$graph.MaxEnvelopesPerFrame -lt 1) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must be >= 1 when opcode nodes exist.' }
if ($null -eq $assets.Assets) { Fail 'Content/assets.h8manifest.json requires Assets array.' }
if ($null -eq $settings.Rows) { Fail 'Tables/settings.h8table.json requires Rows array.' }
if ([string]::IsNullOrWhiteSpace([string]$locale.Locale)) { Fail 'Locales/en.h8loc.json requires Locale.' }
if ($null -eq $vscodeSettings.PSObject.Properties['json.schemas']) { Fail '.vscode/settings.json requires json.schemas mapping.' }
$schemaMappings = @($vscodeSettings.PSObject.Properties['json.schemas'].Value)
$requiredSchemaMappings = @(
    @{ Url = './Schemas/h8mod.authoring.schema.json'; Match = '/mod.h8manifest.json' },
    @{ Url = './Schemas/runtime.mod.schema.json'; Match = '/mod.json' },
    @{ Url = './Schemas/h8graph.schema.json'; Match = '/Graphs/*.h8graph.json' },
    @{ Url = './Schemas/assets.schema.json'; Match = '/Content/*.h8manifest.json' },
    @{ Url = './Schemas/settings_table.schema.json'; Match = '/Tables/*.h8table.json' },
    @{ Url = './Schemas/locale.schema.json'; Match = '/Locales/*.h8loc.json' }
)
foreach ($requiredMapping in $requiredSchemaMappings) {
    $matched = $false
    foreach ($schemaMapping in $schemaMappings) {
        $fileMatches = @($schemaMapping.fileMatch | ForEach-Object { [string]$_ })
        if ([string]$schemaMapping.url -eq $requiredMapping.Url -and $fileMatches -contains $requiredMapping.Match) {
            $matched = $true
            break
        }
    }
    if (-not $matched) {
        Fail ('.vscode/settings.json missing schema mapping ' + $requiredMapping.Url + ' -> ' + $requiredMapping.Match)
    }
}

Write-Host 'PASS HECTON-8 external starter structure'
