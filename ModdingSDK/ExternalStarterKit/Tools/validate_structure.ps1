param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$ThrowInsteadOfExit
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    if ($ThrowInsteadOfExit) {
        throw ('[H8MOD_STARTER_VALIDATION] ' + $Message)
    }

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

function Get-AllowedManifestCapabilities() {
    return @(
        'cap.graph.command_draft',
        'cap.settings.table',
        'cap.locale.en',
        'cap.content.asset_manifest',
        'cap.review.submission_package'
    )
}

function Validate-ManifestCapabilities([object]$Value) {
    [void](Validate-JsonArray $Value 'mod.h8manifest.json Capabilities')
    $capabilities = @($Value)
    if ($capabilities.Count -gt 16) { Fail 'mod.h8manifest.json Capabilities exceeds 16 entries.' }
    $allowed = Get-AllowedManifestCapabilities
    $seen = @{}
    for ($i = 0; $i -lt $capabilities.Count; $i++) {
        $capabilityId = Validate-ModId ([string]$capabilities[$i]) ('mod.h8manifest.json Capabilities[' + $i + ']')
        if ($seen.ContainsKey($capabilityId)) { Fail ('mod.h8manifest.json duplicate Capability: ' + $capabilityId) }
        if ($allowed -notcontains $capabilityId) { Fail ('mod.h8manifest.json Capability is not public: ' + $capabilityId) }
        $seen[$capabilityId] = $true
    }
}

function Validate-DependencyList([object]$Value, [string]$OwnerId, [string]$Label) {
    [void](Validate-JsonArray $Value $Label)
    $dependencies = @($Value)
    if ($dependencies.Count -gt 32) { Fail ($Label + ' exceeds 32 entries.') }
    $seen = @{}
    $result = @()
    for ($i = 0; $i -lt $dependencies.Count; $i++) {
        $dependencyId = Validate-ModId ([string]$dependencies[$i]) ($Label + '[' + $i + ']')
        if ($dependencyId -eq $OwnerId) { Fail ($Label + ' must not contain self dependency: ' + $dependencyId) }
        if ($seen.ContainsKey($dependencyId)) { Fail ($Label + ' contains duplicate dependency: ' + $dependencyId) }
        $seen[$dependencyId] = $true
        $result += $dependencyId
    }
    return @($result)
}

function Validate-JsonArray([object]$Value, [string]$Label) {
    if ($null -eq $Value -or -not $Value.GetType().IsArray) {
        Fail ($Label + ' must be a JSON array.')
    }

    return @($Value)
}

function Validate-SettingDefault([object]$Value, [string]$Kind, [string]$Label) {
    switch ($Kind) {
        'bool' {
            if ($Value -isnot [bool]) { Fail ($Label + ' Default must be a JSON boolean.') }
            return
        }
        'int' {
            if (-not (($Value -is [int]) -or ($Value -is [long]))) { Fail ($Label + ' Default must be a JSON integer.') }
            return
        }
        'float' {
            if (-not (($Value -is [double]) -or ($Value -is [decimal]) -or ($Value -is [single]) -or ($Value -is [int]) -or ($Value -is [long]))) {
                Fail ($Label + ' Default must be a JSON number.')
            }
            return
        }
        'string' {
            [void](Validate-RequiredText ([string]$Value) ($Label + ' Default'))
            return
        }
        'enum' {
            [void](Validate-RequiredText ([string]$Value) ($Label + ' Default'))
            return
        }
        default {
            Fail ($Label + ' Kind must be one of: bool, int, float, string, enum.')
        }
    }
}

function Validate-SettingsTable([object]$Settings) {
    if ([string]$Settings.Schema -ne 'hecton8.settings_table.draft.v1') {
        Fail 'Tables/settings.h8table.json Schema must be hecton8.settings_table.draft.v1.'
    }

    [void](Validate-JsonArray $Settings.Rows 'Tables/settings.h8table.json Rows')
    $rows = @($Settings.Rows)
    if ($rows.Count -gt 128) { Fail 'Tables/settings.h8table.json Rows exceeds 128 entries.' }
    $rowIds = @{}
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $row = $rows[$i]
        if ($null -eq $row) { Fail ('Tables/settings.h8table.json Rows[' + $i + '] must not be null.') }
        $label = 'Tables/settings.h8table.json Rows[' + $i + ']'
        $rowId = Validate-ModId ([string]$row.Id) ($label + ' Id')
        if ($rowIds.ContainsKey($rowId)) { Fail ('Tables/settings.h8table.json duplicate row Id: ' + $rowId) }
        $rowIds[$rowId] = $true

        $kind = Validate-RequiredText ([string]$row.Kind) ($label + ' Kind')
        if (@('bool','int','float','string','enum') -notcontains $kind) {
            Fail ($label + ' Kind must be one of: bool, int, float, string, enum.')
        }

        $defaultProperty = $row.PSObject.Properties['Default']
        if ($null -eq $defaultProperty) { Fail ($label + ' Default is required.') }
        Validate-SettingDefault $defaultProperty.Value $kind $label
    }
}

function Validate-LocaleTable([object]$LocaleDocument) {
    if ([string]$LocaleDocument.Schema -ne 'hecton8.locale.draft.v1') {
        Fail 'Locales/en.h8loc.json Schema must be hecton8.locale.draft.v1.'
    }

    $localeId = Validate-RequiredText ([string]$LocaleDocument.Locale) 'Locales/en.h8loc.json Locale'
    if ($localeId -notmatch '^[a-z]{2}(-[A-Z]{2})?$') {
        Fail 'Locales/en.h8loc.json Locale must use xx or xx-YY form.'
    }

    $stringsProperty = $LocaleDocument.PSObject.Properties['Strings']
    if ($null -eq $stringsProperty -or $null -eq $stringsProperty.Value -or $stringsProperty.Value.GetType().IsArray) {
        Fail 'Locales/en.h8loc.json Strings must be a JSON object.'
    }

    $stringEntries = @($stringsProperty.Value.PSObject.Properties)
    if ($stringEntries.Count -gt 512) { Fail 'Locales/en.h8loc.json Strings exceeds 512 entries.' }
    foreach ($entry in $stringEntries) {
        [void](Validate-ModId ([string]$entry.Name) 'Locales/en.h8loc.json Strings key')
        [void](Validate-RequiredText ([string]$entry.Value) ('Locales/en.h8loc.json Strings.' + [string]$entry.Name))
    }
}

function Get-AssetAllowedExtensions([string]$Kind) {
    switch ($Kind) {
        'raw_texture' { return @('.png','.jpg','.jpeg','.webp') }
        'audio_clip' { return @('.wav','.ogg') }
        'data_blob' { return @('.json','.bytes','.bin') }
        default { Fail 'Asset Kind must be one of: raw_texture, audio_clip, data_blob.' }
    }
}

function Resolve-AssetRelativePath([string]$RelativePath, [string]$Kind, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { Fail ($Label + ' Path is required.') }
    $normalized = $RelativePath.Replace('\','/').Trim()
    if ($normalized -ne $RelativePath.Replace('\','/')) { Fail ($Label + ' Path must not contain leading or trailing whitespace.') }
    if ([System.IO.Path]::IsPathRooted($normalized)) { Fail ($Label + ' Path must be starter-relative.') }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) { Fail ($Label + ' Path must not contain .. segments.') }
    if (-not $normalized.StartsWith('Content/Assets/', [System.StringComparison]::Ordinal)) { Fail ($Label + ' Path must stay under Content/Assets/.') }
    $extension = [System.IO.Path]::GetExtension($normalized).ToLowerInvariant()
    if ((Get-AssetAllowedExtensions $Kind) -notcontains $extension) {
        Fail ($Label + ' Path extension is not allowed for ' + $Kind + ': ' + $extension)
    }
    return $normalized
}

function New-Crc32Table {
    $table = New-Object 'uint64[]' 256
    for ($i = 0; $i -lt 256; $i++) {
        [uint64]$crc = [uint64]$i
        for ($j = 0; $j -lt 8; $j++) {
            if (($crc -band 1) -ne 0) {
                $crc = ([uint64]3988292384 -bxor ($crc -shr 1)) -band [uint64]4294967295
            } else {
                $crc = ($crc -shr 1) -band [uint64]4294967295
            }
        }
        $table[$i] = $crc
    }
    return $table
}

$script:Crc32Table = New-Crc32Table

function Get-Crc32Hex([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $buffer = New-Object byte[] 8192
        [uint64]$crc = [uint64]4294967295
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            for ($i = 0; $i -lt $read; $i++) {
                $index = [int](($crc -bxor [uint64]$buffer[$i]) -band 255)
                $crc = ($script:Crc32Table[$index] -bxor ($crc -shr 8)) -band [uint64]4294967295
            }
        }
        $crc = ($crc -bxor [uint64]4294967295) -band [uint64]4294967295
        return ('{0:X8}' -f $crc)
    } finally {
        $stream.Dispose()
    }
}

function Validate-AssetManifest([object]$AssetDocument, [long]$MaxAssetBytes) {
    if ([string]$AssetDocument.Schema -ne 'hecton8.assets.draft.v1') {
        Fail 'Content/assets.h8manifest.json Schema must be hecton8.assets.draft.v1.'
    }

    [void](Validate-JsonArray $AssetDocument.Assets 'Content/assets.h8manifest.json Assets')
    $assetRows = @($AssetDocument.Assets)
    if ($assetRows.Count -gt 512) { Fail 'Content/assets.h8manifest.json Assets exceeds 512 entries.' }
    $assetIds = @{}
    [long]$totalBytes = 0
    for ($i = 0; $i -lt $assetRows.Count; $i++) {
        $asset = $assetRows[$i]
        if ($null -eq $asset) { Fail ('Content/assets.h8manifest.json Assets[' + $i + '] must not be null.') }
        $label = 'Content/assets.h8manifest.json Assets[' + $i + ']'
        $assetId = Validate-ModId ([string]$asset.Id) ($label + ' Id')
        if ($assetIds.ContainsKey($assetId)) { Fail ('Content/assets.h8manifest.json duplicate asset Id: ' + $assetId) }
        $assetIds[$assetId] = $true

        $kind = Validate-RequiredText ([string]$asset.Kind) ($label + ' Kind')
        if (@('raw_texture','audio_clip','data_blob') -notcontains $kind) {
            Fail ($label + ' Kind must be one of: raw_texture, audio_clip, data_blob.')
        }

        $relativePath = Resolve-AssetRelativePath ([string]$asset.Path) $kind $label
        $fullPath = Join-StarterPath $Root $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Fail ($label + ' file is missing: ' + $relativePath)
        }

        $fileInfo = Get-Item -LiteralPath $fullPath
        if ([long]$fileInfo.Length -gt 4194304) {
            Fail ($label + ' file exceeds 4194304 bytes: ' + $relativePath)
        }

        $bytesProperty = $asset.PSObject.Properties['Bytes']
        if ($null -eq $bytesProperty) { Fail ($label + ' Bytes is required.') }
        [long]$declaredBytes = 0
        try {
            $declaredBytes = [long]$bytesProperty.Value
        } catch {
            Fail ($label + ' Bytes must be a JSON integer.')
        }
        if ($declaredBytes -lt 0) { Fail ($label + ' Bytes must be >= 0.') }
        if ($declaredBytes -ne [long]$fileInfo.Length) {
            Fail ($label + ' Bytes does not match file length for ' + $relativePath + '.')
        }

        $crc32 = Validate-RequiredText ([string]$asset.Crc32) ($label + ' Crc32')
        if ($crc32 -notmatch '^[0-9A-Fa-f]{8}$') {
            Fail ($label + ' Crc32 must be 8 hex characters.')
        }
        $computedCrc32 = Get-Crc32Hex $fullPath
        if ($computedCrc32 -ne $crc32.ToUpperInvariant()) {
            Fail ($label + ' Crc32 does not match file for ' + $relativePath + '.')
        }

        $totalBytes += $declaredBytes
    }

    if ($totalBytes -gt $MaxAssetBytes) {
        Fail 'Content/assets.h8manifest.json total Bytes must not exceed mod.h8manifest.json Budgets.MaxAssetBytes.'
    }
}

function Validate-VsCodeTasks([object]$TasksJson) {
    if ([string]$TasksJson.version -ne '2.0.0') {
        Fail '.vscode/tasks.json requires version 2.0.0.'
    }

    [void](Validate-JsonArray $TasksJson.tasks '.vscode/tasks.json tasks')
    [void](Validate-JsonArray $TasksJson.inputs '.vscode/tasks.json inputs')

    $taskByLabel = @{}
    foreach ($task in @($TasksJson.tasks)) {
        if ($null -eq $task) { Fail '.vscode/tasks.json tasks must not contain null entries.' }
        $label = Validate-RequiredText ([string]$task.label) '.vscode/tasks.json task label'
        if ($taskByLabel.ContainsKey($label)) { Fail ('.vscode/tasks.json duplicate task label: ' + $label) }
        $taskByLabel[$label] = $task
    }

    $requiredTaskLabels = @(
        'HECTON-8: setup identity',
        'HECTON-8: create first playable mod',
        'HECTON-8: validate starter',
        'HECTON-8: prepare review manifest',
        'HECTON-8: build submission zip',
        'HECTON-8: install local discovery copy',
        'HECTON-8: diagnose local Mods folder',
        'HECTON-8: list dependencies',
        'HECTON-8: add dependency',
        'HECTON-8: remove dependency',
        'HECTON-8: clear dependencies',
        'HECTON-8: show capabilities',
        'HECTON-8: show opcodes',
        'HECTON-8: create graph node snippet',
        'HECTON-8: create disabled graph node snippet',
        'HECTON-8: apply graph node snippet',
        'HECTON-8: replace graph node snippet',
        'HECTON-8: create settings row snippet',
        'HECTON-8: apply settings row snippet',
        'HECTON-8: replace settings row snippet',
        'HECTON-8: create locale entry snippet',
        'HECTON-8: apply locale entry snippet',
        'HECTON-8: replace locale entry snippet',
        'HECTON-8: create asset entry snippet',
        'HECTON-8: apply asset entry snippet',
        'HECTON-8: replace asset entry snippet',
        'HECTON-8: configure manifest contract'
    )
    foreach ($requiredTaskLabel in $requiredTaskLabels) {
        if (-not $taskByLabel.ContainsKey($requiredTaskLabel)) {
            Fail ('.vscode/tasks.json missing task: ' + $requiredTaskLabel)
        }

        $task = $taskByLabel[$requiredTaskLabel]
        if ([string]$task.type -ne 'shell') { Fail ('.vscode/tasks.json task must use shell type: ' + $requiredTaskLabel) }
        if ([string]$task.command -ne '${config:hecton8.powerShellExecutable}') {
            Fail ('.vscode/tasks.json task must use ${config:hecton8.powerShellExecutable}: ' + $requiredTaskLabel)
        }

        $args = @($task.args | ForEach-Object { [string]$_ })
        if ($args -notcontains 'h8mod.ps1') { Fail ('.vscode/tasks.json task must route through h8mod.ps1: ' + $requiredTaskLabel) }
        if ($args -notcontains '-Action') { Fail ('.vscode/tasks.json task missing -Action: ' + $requiredTaskLabel) }
        foreach ($arg in $args) {
            $normalizedArg = ([string]$arg).Replace('\','/')
            if ($normalizedArg -match '^Tools/.*[.]ps1$') {
                Fail ('.vscode/tasks.json task must not call Tools scripts directly: ' + $requiredTaskLabel)
            }
        }
    }

    $disabledNodeTask = $taskByLabel['HECTON-8: create disabled graph node snippet']
    if (@($disabledNodeTask.args | ForEach-Object { [string]$_ }) -notcontains '-NodeDisabled') {
        Fail '.vscode/tasks.json disabled graph task must pass -NodeDisabled.'
    }

    foreach ($replaceTaskLabel in @('HECTON-8: replace graph node snippet','HECTON-8: replace settings row snippet','HECTON-8: replace locale entry snippet','HECTON-8: replace asset entry snippet')) {
        $replaceTask = $taskByLabel[$replaceTaskLabel]
        if (@($replaceTask.args | ForEach-Object { [string]$_ }) -notcontains '-Replace') {
            Fail ('.vscode/tasks.json replace task must pass -Replace: ' + $replaceTaskLabel)
        }
    }

    $firstModTask = $taskByLabel['HECTON-8: create first playable mod']
    $firstModArgs = @($firstModTask.args | ForEach-Object { [string]$_ })
    if ($firstModArgs -notcontains 'first-mod') {
        Fail '.vscode/tasks.json first playable mod task must pass -Action first-mod.'
    }
    if ($firstModArgs -notcontains '-Replace') {
        Fail '.vscode/tasks.json first playable mod task must pass -Replace for rerunnable starter onboarding.'
    }

    $installLocalTask = $taskByLabel['HECTON-8: install local discovery copy']
    $installLocalArgs = @($installLocalTask.args | ForEach-Object { [string]$_ })
    if ($installLocalArgs -notcontains 'install-local') {
        Fail '.vscode/tasks.json local install task must pass -Action install-local.'
    }
    if ($installLocalArgs -notcontains '-ProjectRoot') {
        Fail '.vscode/tasks.json local install task must pass -ProjectRoot so copied starter folders have an explicit destination.'
    }
    if ($installLocalArgs -notcontains '-Replace') {
        Fail '.vscode/tasks.json local install task must pass -Replace for rerunnable discovery-copy updates.'
    }

    $diagnoseLocalTask = $taskByLabel['HECTON-8: diagnose local Mods folder']
    $diagnoseLocalArgs = @($diagnoseLocalTask.args | ForEach-Object { [string]$_ })
    if ($diagnoseLocalArgs -notcontains 'diagnose-local') {
        Fail '.vscode/tasks.json local diagnose task must pass -Action diagnose-local.'
    }
    if ($diagnoseLocalArgs -notcontains '-ProjectRoot') {
        Fail '.vscode/tasks.json local diagnose task must pass -ProjectRoot so copied starter folders inspect an explicit Mods root.'
    }

    foreach ($dependencyTaskLabel in @('HECTON-8: list dependencies','HECTON-8: add dependency','HECTON-8: remove dependency','HECTON-8: clear dependencies')) {
        $dependencyTask = $taskByLabel[$dependencyTaskLabel]
        $dependencyArgs = @($dependencyTask.args | ForEach-Object { [string]$_ })
        if ($dependencyArgs -notcontains 'dependencies') {
            Fail ('.vscode/tasks.json dependency task must pass -Action dependencies: ' + $dependencyTaskLabel)
        }
        if ($dependencyArgs -notcontains '-DependencyAction') {
            Fail ('.vscode/tasks.json dependency task must pass -DependencyAction: ' + $dependencyTaskLabel)
        }
    }

    foreach ($dependencyIdTaskLabel in @('HECTON-8: add dependency','HECTON-8: remove dependency')) {
        $dependencyTask = $taskByLabel[$dependencyIdTaskLabel]
        if (@($dependencyTask.args | ForEach-Object { [string]$_ }) -notcontains '-DependencyId') {
            Fail ('.vscode/tasks.json dependency id task must pass -DependencyId: ' + $dependencyIdTaskLabel)
        }
    }

    $inputIds = @{}
    foreach ($input in @($TasksJson.inputs)) {
        if ($null -eq $input) { Fail '.vscode/tasks.json inputs must not contain null entries.' }
        $inputId = Validate-RequiredText ([string]$input.id) '.vscode/tasks.json input id'
        $inputIds[$inputId] = $true
    }
    foreach ($requiredInputId in @('modId','displayName','author','version','projectRoot','dependencyId','nodeId','opcode','nodeParametersJson','settingId','settingKind','settingDefault','localeKey','localeValue','assetId','assetKind','assetPath','capability','capabilityState','maxEnvelopesPerFrame','maxAssetBytes')) {
        if (-not $inputIds.ContainsKey($requiredInputId)) {
            Fail ('.vscode/tasks.json missing input: ' + $requiredInputId)
        }
    }
}

@('Content','Content/Assets','Docs','Graphs','Tables','Locales','Generated','Reports','Reference','Schemas','Tools','.vscode') | ForEach-Object { Require-Directory $_ }
@(
    'README.md',
    'Docs/capabilities.md',
    'h8mod.ps1',
    'mod.h8manifest.json',
    'mod.json',
    'Content/README.md',
    'Content/Assets/README.md',
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
    'Tools/apply_asset_entry_snippet.ps1',
    'Tools/build_review_manifest.ps1',
    'Tools/build_submission_package.ps1',
    'Tools/configure_dependencies.ps1',
    'Tools/create_first_mod.ps1',
    'Tools/install_local_mod.ps1',
    'Tools/diagnose_local_mods.ps1',
    'Tools/apply_graph_node_snippet.ps1',
    'Tools/apply_locale_entry_snippet.ps1',
    'Tools/apply_settings_row_snippet.ps1',
    'Tools/create_locale_entry_snippet.ps1',
    'Tools/create_asset_entry_snippet.ps1',
    'Tools/create_graph_node_snippet.ps1',
    'Tools/create_settings_row_snippet.ps1',
    'Tools/configure_manifest_contract.ps1',
    'Tools/list_allowed_opcodes.ps1',
    'Tools/prepare_mod.ps1',
    'Tools/set_mod_identity.ps1',
    'Tools/validate_structure.ps1',
    '.vscode/settings.json',
    '.vscode/tasks.json'
) | ForEach-Object { [void](Require-File $_) }

$capabilitiesText = Get-Content -Raw -LiteralPath (Require-File 'Docs/capabilities.md')
foreach ($requiredCapabilityText in @('Supported now','Not public rights','envelope-only','FutureCommandEnvelope','Harmony','BepInEx','h8mod.ps1 -Action capabilities','h8mod.ps1 -Action manifest-contract','configure_manifest_contract.ps1','h8mod.ps1 -Action dependencies','configure_dependencies.ps1','h8mod.ps1 -Action install-local','install_local_mod.ps1','h8mod.ps1 -Action diagnose-local','diagnose_local_mods.ps1','recursive manifest discovery','dependency cycles','load order','h8mod.ps1 -Action node-snippet','h8mod.ps1 -Action apply-node-snippet','h8mod.ps1 -Action setting-snippet','h8mod.ps1 -Action locale-snippet','h8mod.ps1 -Action apply-setting-snippet','h8mod.ps1 -Action apply-locale-snippet','h8mod.ps1 -Action asset-snippet','h8mod.ps1 -Action apply-asset-snippet')) {
    if (-not $capabilitiesText.Contains($requiredCapabilityText)) {
        Fail ('Docs/capabilities.md missing required capability text: ' + $requiredCapabilityText)
    }
}

$authoring = Read-Json 'mod.h8manifest.json'
$runtime = Read-Json 'mod.json'
$graph = Read-Json 'Graphs/main.h8graph.json'
$assets = Read-Json 'Content/assets.h8manifest.json'
$settings = Read-Json 'Tables/settings.h8table.json'
$locale = Read-Json 'Locales/en.h8loc.json'
$vscodeSettings = Read-Json '.vscode/settings.json'
$vscodeTasks = Read-Json '.vscode/tasks.json'
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
$authoringDependencies = @(Validate-DependencyList $authoring.Dependencies $authoringId 'mod.h8manifest.json Dependencies')
Validate-ManifestCapabilities $authoring.Capabilities
if ([int]$authoring.Budgets.MaxEnvelopesPerFrame -lt 0) { Fail 'mod.h8manifest.json Budgets.MaxEnvelopesPerFrame must be >= 0.' }
if ([int]$authoring.Budgets.MaxEnvelopesPerFrame -gt 256) { Fail 'mod.h8manifest.json Budgets.MaxEnvelopesPerFrame exceeds 256.' }
if ([long]$authoring.Budgets.MaxAssetBytes -lt 0) { Fail 'mod.h8manifest.json Budgets.MaxAssetBytes must be >= 0.' }
if ([long]$authoring.Budgets.MaxAssetBytes -gt 33554432) { Fail 'mod.h8manifest.json Budgets.MaxAssetBytes exceeds 33554432.' }
$runtimeId = Validate-ModId ([string]$runtime.Id) 'mod.json Id'
if ($authoringId -ne $runtimeId) { Fail 'mod.h8manifest.json Id must match mod.json Id.' }
$runtimeName = Validate-RequiredText ([string]$runtime.Name) 'mod.json Name'
$runtimeAuthor = Validate-RequiredText ([string]$runtime.Author) 'mod.json Author'
$runtimeVersion = Validate-Version ([string]$runtime.Version) 'mod.json Version'
if ($authoringDisplayName -ne $runtimeName) { Fail 'mod.h8manifest.json DisplayName must match mod.json Name.' }
if ($authoringAuthor -ne $runtimeAuthor) { Fail 'mod.h8manifest.json Author must match mod.json Author.' }
if ($authoringVersion -ne $runtimeVersion) { Fail 'mod.h8manifest.json Version must match mod.json Version.' }
if ($null -eq $runtime.PSObject.Properties['Dependencies']) {
    Fail 'mod.json Dependencies is required.'
}
$runtimeDependencies = @(Validate-DependencyList $runtime.Dependencies $runtimeId 'mod.json Dependencies')
if (($authoringDependencies -join "`n") -ne ($runtimeDependencies -join "`n")) {
    Fail 'mod.h8manifest.json Dependencies must match mod.json Dependencies in the same order.'
}
if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryAssembly)) { Fail 'mod.json EntryAssembly must stay empty in envelope-only starter kits.' }
if (-not [string]::IsNullOrWhiteSpace([string]$runtime.EntryType)) { Fail 'mod.json EntryType must stay empty in envelope-only starter kits.' }
if ([int]$runtime.RequiredAPIVersion -lt 2) { Fail 'mod.json RequiredAPIVersion must be >= 2.' }
if ([string]$graph.Runtime -ne 'envelope-only') { Fail 'Graphs/main.h8graph.json Runtime must be envelope-only.' }
if ([int]$graph.MaxEnvelopesPerFrame -gt [int]$authoring.Budgets.MaxEnvelopesPerFrame) { Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must not exceed mod.h8manifest.json Budgets.MaxEnvelopesPerFrame.' }
$allowedGraphOpcodes = Read-AllowedGraphOpcodeTokens
$graphNodeIds = @{}
$graphOpcodeNodeCount = 0
$graphNodes = @($graph.Nodes)
if ($graphNodes.Count -gt 256) { Fail 'Graphs/main.h8graph.json Nodes exceeds 256 entries.' }
foreach ($node in $graphNodes) {
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
Validate-AssetManifest $assets ([long]$authoring.Budgets.MaxAssetBytes)
Validate-SettingsTable $settings
Validate-LocaleTable $locale
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
Validate-VsCodeTasks $vscodeTasks

Write-Host 'PASS HECTON-8 external starter structure'
