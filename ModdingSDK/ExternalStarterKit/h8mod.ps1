param(
    [ValidateSet('menu','first-mod','install-local','diagnose-local','doctor','dependencies','setup','validate','review','prepare','submission','opcodes','opcodes-json','node-snippet','apply-node-snippet','setting-snippet','locale-snippet','apply-setting-snippet','apply-locale-snippet','asset-snippet','apply-asset-snippet','manifest-contract','capabilities')]
    [string]$Action = 'menu',
    [string]$Id = '',
    [string]$DisplayName = '',
    [string]$Author = '',
    [string]$Version = '',
    [string]$NodeId = 'node.spawn_item',
    [string]$Opcode = 'SpawnItem',
    [string]$NodeParametersJson = '{}',
    [string]$Output = 'Generated/graph_node_snippet.json',
    [string]$NodeSnippet = 'Generated/graph_node_snippet.json',
    [switch]$NodeDisabled,
    [string]$SettingId = 'setting.example_toggle',
    [string]$SettingKind = 'bool',
    [string]$SettingDefault = 'false',
    [string]$SettingOutput = 'Generated/settings_row_snippet.json',
    [string]$SettingSnippet = 'Generated/settings_row_snippet.json',
    [string]$LocaleKey = 'text.example_line',
    [string]$LocaleValue = 'Your localized text',
    [string]$LocaleOutput = 'Generated/locale_entry_snippet.json',
    [string]$LocaleSnippet = 'Generated/locale_entry_snippet.json',
    [string]$AssetId = 'asset.example_blob',
    [string]$AssetKind = 'data_blob',
    [string]$AssetPath = 'Content/Assets/example.bytes',
    [string]$AssetCrc32 = '00000000',
    [long]$AssetBytes = 0,
    [string]$AssetOutput = 'Generated/asset_entry_snippet.json',
    [string]$AssetSnippet = 'Generated/asset_entry_snippet.json',
    [string]$Capability = 'cap.graph.command_draft',
    [ValidateSet('list','add','remove','clear')]
    [string]$DependencyAction = 'list',
    [string]$DependencyId = '',
    [ValidateSet('unchanged','enable','disable')]
    [string]$CapabilityState = 'enable',
    [int]$MaxEnvelopesPerFrame = -1,
    [long]$MaxAssetBytes = -1,
    [switch]$Replace,
    [switch]$BuildSubmission,
    [string]$SubmissionOutput = '',
    [string]$ProjectRoot = '',
    [string]$ModsRoot = '',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$MaxCapabilitiesGuideBytes = 262144

function Fail([string]$Message) {
    Write-Error ('[H8MOD] ' + $Message)
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

function Resolve-StarterTool([string]$RelativePath) {
    $tool = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        Fail ('Missing starter tool: ' + $RelativePath)
    }
    return $tool
}

function Complete-StarterTool {
    $toolSucceeded = $?
    $toolExitCode = $global:LASTEXITCODE
    if ($toolExitCode -ne 0) {
        exit $toolExitCode
    }
    if (-not $toolSucceeded) {
        exit 1
    }
}

function Invoke-Validate {
    $tool = Resolve-StarterTool 'Tools/validate_structure.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root
    Complete-StarterTool
}

function Invoke-Review {
    $tool = Resolve-StarterTool 'Tools/build_review_manifest.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root
    Complete-StarterTool
}

function Invoke-PrepareExisting {
    $tool = Resolve-StarterTool 'Tools/prepare_mod.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root
    Complete-StarterTool
}

function Invoke-SubmissionPackage {
    $tool = Resolve-StarterTool 'Tools/build_submission_package.ps1'
    $global:LASTEXITCODE = 0
    if ([string]::IsNullOrWhiteSpace($SubmissionOutput)) {
        & $tool -Root $Root
    } else {
        & $tool -Root $Root -Output $SubmissionOutput
    }
    Complete-StarterTool
}

function Invoke-InstallLocal([bool]$PromptForMissingValues) {
    $installProjectRoot = $ProjectRoot
    $installModsRoot = $ModsRoot

    if ($PromptForMissingValues) {
        $installProjectRoot = Read-SetupValue $installProjectRoot 'HECTON-8 project root, blank to auto-detect from starter location'
        $installModsRoot = Read-SetupValue $installModsRoot 'Mods root override, blank to use ProjectRoot/Mods'
    }

    $tool = Resolve-StarterTool 'Tools/install_local_mod.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace) {
        & $tool -Root $Root -ProjectRoot $installProjectRoot -ModsRoot $installModsRoot -Replace
    } else {
        & $tool -Root $Root -ProjectRoot $installProjectRoot -ModsRoot $installModsRoot
    }
    Complete-StarterTool
}

function Invoke-DiagnoseLocal([bool]$PromptForMissingValues) {
    $diagnoseProjectRoot = $ProjectRoot
    $diagnoseModsRoot = $ModsRoot

    if ($PromptForMissingValues) {
        $diagnoseProjectRoot = Read-SetupValue $diagnoseProjectRoot 'HECTON-8 project root, blank to auto-detect from starter location'
        $diagnoseModsRoot = Read-SetupValue $diagnoseModsRoot 'Mods root override, blank to use ProjectRoot/Mods'
    }

    $tool = Resolve-StarterTool 'Tools/diagnose_local_mods.ps1'
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $tool -Root $Root -ProjectRoot $diagnoseProjectRoot -ModsRoot $diagnoseModsRoot -Json
    } else {
        & $tool -Root $Root -ProjectRoot $diagnoseProjectRoot -ModsRoot $diagnoseModsRoot
    }
    Complete-StarterTool
}

function Invoke-Doctor {
    $tool = Resolve-StarterTool 'Tools/run_doctor.ps1'
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $tool -Root $Root -Json
    } else {
        & $tool -Root $Root
    }
    Complete-StarterTool
}

function Invoke-Opcodes([bool]$Json) {
    $tool = Resolve-StarterTool 'Tools/list_allowed_opcodes.ps1'
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $tool -Root $Root -Json
    } else {
        & $tool -Root $Root
    }
    Complete-StarterTool
}

function Invoke-Capabilities {
    $guide = Join-StarterPath $Root 'Docs/capabilities.md'
    if (-not (Test-Path -LiteralPath $guide -PathType Leaf)) {
        Fail 'Missing Docs/capabilities.md'
    }

    $strictJsonIo = Resolve-StarterTool 'Tools/strict_json_io.ps1'
    . $strictJsonIo

    try {
        Write-Output (Read-H8TextFileCapped $guide 'Docs/capabilities.md' $MaxCapabilitiesGuideBytes)
    } catch {
        Fail $_.Exception.Message
    }
}

function Invoke-Dependencies([bool]$PromptForMissingValues) {
    $dependencyAction = $DependencyAction
    $dependencyId = $DependencyId

    if ($PromptForMissingValues) {
        $dependencyAction = Read-SetupValue $dependencyAction 'Dependency action: list, add, remove, or clear'
        if (@('list','add','remove','clear') -notcontains $dependencyAction) {
            Fail 'Dependency action must be one of: list, add, remove, clear.'
        }
        if ($dependencyAction -eq 'add' -or $dependencyAction -eq 'remove') {
            $dependencyId = Read-SetupValue $dependencyId 'Dependency mod id, example com.example.library'
        }
    }

    $tool = Resolve-StarterTool 'Tools/configure_dependencies.ps1'
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $tool -Root $Root -Action $dependencyAction -DependencyId $dependencyId -Json
    } else {
        & $tool -Root $Root -Action $dependencyAction -DependencyId $dependencyId
    }
    Complete-StarterTool
}

function Invoke-GraphNodeSnippet([bool]$PromptForMissingValues) {
    $snippetNodeId = $NodeId
    $snippetOpcode = $Opcode
    $snippetParametersJson = $NodeParametersJson
    $snippetOutput = $Output
    $snippetDisabled = $NodeDisabled

    if ($PromptForMissingValues) {
        $snippetNodeId = Read-SetupValue $snippetNodeId 'Graph node id, example node.spawn_item'
        $snippetOpcode = Read-SetupValue $snippetOpcode 'Opcode alias or hex, example SpawnItem'
        $snippetParametersJson = Read-SetupValue $snippetParametersJson 'Parameters JSON object, example {}'
        $snippetOutput = Read-SetupValue $snippetOutput 'Output path under Generated/, example Generated/graph_node_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/create_graph_node_snippet.ps1'
    $global:LASTEXITCODE = 0
    if ($snippetDisabled) {
        & $tool -Root $Root -Id $snippetNodeId -Opcode $snippetOpcode -ParametersJson $snippetParametersJson -Output $snippetOutput -Disabled
    } else {
        & $tool -Root $Root -Id $snippetNodeId -Opcode $snippetOpcode -ParametersJson $snippetParametersJson -Output $snippetOutput
    }
    Complete-StarterTool
}

function Invoke-ApplyGraphNodeSnippet([bool]$PromptForMissingValues) {
    $snippetPath = $NodeSnippet

    if ($PromptForMissingValues) {
        $snippetPath = Read-SetupValue $snippetPath 'Graph node snippet path under Generated/, example Generated/graph_node_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/apply_graph_node_snippet.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace) {
        & $tool -Root $Root -Snippet $snippetPath -Replace
    } else {
        & $tool -Root $Root -Snippet $snippetPath
    }
    Complete-StarterTool
}

function Invoke-SettingsRowSnippet([bool]$PromptForMissingValues) {
    $snippetSettingId = $SettingId
    $snippetSettingKind = $SettingKind
    $snippetSettingDefault = $SettingDefault
    $snippetSettingOutput = $SettingOutput

    if ($PromptForMissingValues) {
        $snippetSettingId = Read-SetupValue $snippetSettingId 'Setting id, example setting.example_toggle'
        $snippetSettingKind = Read-SetupValue $snippetSettingKind 'Setting kind: bool, int, float, string, or enum'
        $snippetSettingDefault = Read-SetupValue $snippetSettingDefault 'Setting default value'
        $snippetSettingOutput = Read-SetupValue $snippetSettingOutput 'Output path under Generated/, example Generated/settings_row_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/create_settings_row_snippet.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Id $snippetSettingId -Kind $snippetSettingKind -Default $snippetSettingDefault -Output $snippetSettingOutput
    Complete-StarterTool
}

function Invoke-ApplySettingsRowSnippet([bool]$PromptForMissingValues) {
    $snippetPath = $SettingSnippet

    if ($PromptForMissingValues) {
        $snippetPath = Read-SetupValue $snippetPath 'Settings snippet path under Generated/, example Generated/settings_row_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/apply_settings_row_snippet.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace) {
        & $tool -Root $Root -Snippet $snippetPath -Replace
    } else {
        & $tool -Root $Root -Snippet $snippetPath
    }
    Complete-StarterTool
}

function Invoke-LocaleEntrySnippet([bool]$PromptForMissingValues) {
    $snippetLocaleKey = $LocaleKey
    $snippetLocaleValue = $LocaleValue
    $snippetLocaleOutput = $LocaleOutput

    if ($PromptForMissingValues) {
        $snippetLocaleKey = Read-SetupValue $snippetLocaleKey 'Locale key, example text.example_line'
        $snippetLocaleValue = Read-SetupValue $snippetLocaleValue 'Localized text value'
        $snippetLocaleOutput = Read-SetupValue $snippetLocaleOutput 'Output path under Generated/, example Generated/locale_entry_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/create_locale_entry_snippet.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Key $snippetLocaleKey -Value $snippetLocaleValue -Output $snippetLocaleOutput
    Complete-StarterTool
}

function Invoke-ApplyLocaleEntrySnippet([bool]$PromptForMissingValues) {
    $snippetPath = $LocaleSnippet

    if ($PromptForMissingValues) {
        $snippetPath = Read-SetupValue $snippetPath 'Locale snippet path under Generated/, example Generated/locale_entry_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/apply_locale_entry_snippet.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace) {
        & $tool -Root $Root -Snippet $snippetPath -Replace
    } else {
        & $tool -Root $Root -Snippet $snippetPath
    }
    Complete-StarterTool
}

function Invoke-AssetEntrySnippet([bool]$PromptForMissingValues) {
    $snippetAssetId = $AssetId
    $snippetAssetKind = $AssetKind
    $snippetAssetPath = $AssetPath
    $snippetAssetCrc32 = $AssetCrc32
    $snippetAssetBytes = $AssetBytes
    $snippetAssetOutput = $AssetOutput

    if ($PromptForMissingValues) {
        $snippetAssetId = Read-SetupValue $snippetAssetId 'Asset id, example asset.example_blob'
        $snippetAssetKind = Read-SetupValue $snippetAssetKind 'Asset kind: data_blob, raw_texture, or audio_clip'
        $snippetAssetPath = Read-SetupValue $snippetAssetPath 'Asset path under Content/Assets/, example Content/Assets/example.bytes'
        $snippetAssetCrc32 = Read-SetupValue $snippetAssetCrc32 'CRC32 hex or auto'
        $snippetAssetBytesText = Read-SetupValue ([string]$snippetAssetBytes) 'Byte length, or -1 to read the file length'
        [long]$parsedBytes = 0
        if (-not [long]::TryParse($snippetAssetBytesText, [ref]$parsedBytes)) {
            Fail 'Asset bytes must be an integer.'
        }
        $snippetAssetBytes = $parsedBytes
        $snippetAssetOutput = Read-SetupValue $snippetAssetOutput 'Output path under Generated/, example Generated/asset_entry_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/create_asset_entry_snippet.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Id $snippetAssetId -Kind $snippetAssetKind -Path $snippetAssetPath -Crc32 $snippetAssetCrc32 -Bytes $snippetAssetBytes -Output $snippetAssetOutput
    Complete-StarterTool
}

function Invoke-ApplyAssetEntrySnippet([bool]$PromptForMissingValues) {
    $snippetPath = $AssetSnippet

    if ($PromptForMissingValues) {
        $snippetPath = Read-SetupValue $snippetPath 'Asset snippet path under Generated/, example Generated/asset_entry_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/apply_asset_entry_snippet.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace) {
        & $tool -Root $Root -Snippet $snippetPath -Replace
    } else {
        & $tool -Root $Root -Snippet $snippetPath
    }
    Complete-StarterTool
}

function Invoke-ManifestContractConfig([bool]$PromptForMissingValues) {
    $contractCapability = $Capability
    $contractCapabilityState = $CapabilityState
    $contractMaxEnvelopesPerFrame = $MaxEnvelopesPerFrame
    $contractMaxAssetBytes = $MaxAssetBytes

    if ($PromptForMissingValues) {
        $contractCapability = Read-SetupValue $contractCapability 'Capability id, example cap.graph.command_draft'
        $contractCapabilityState = Read-SetupValue $contractCapabilityState 'Capability state: enable, disable, or unchanged'
        if (@('unchanged','enable','disable') -notcontains $contractCapabilityState) {
            Fail 'Capability state must be one of: unchanged, enable, disable.'
        }

        $envelopeText = Read-SetupValue ([string]$contractMaxEnvelopesPerFrame) 'MaxEnvelopesPerFrame, or -1 unchanged'
        [int]$parsedEnvelopeBudget = 0
        if (-not [int]::TryParse($envelopeText, [ref]$parsedEnvelopeBudget)) {
            Fail 'MaxEnvelopesPerFrame must be an integer.'
        }
        $contractMaxEnvelopesPerFrame = $parsedEnvelopeBudget

        $assetText = Read-SetupValue ([string]$contractMaxAssetBytes) 'MaxAssetBytes, or -1 unchanged'
        [long]$parsedAssetBudget = 0
        if (-not [long]::TryParse($assetText, [ref]$parsedAssetBudget)) {
            Fail 'MaxAssetBytes must be an integer.'
        }
        $contractMaxAssetBytes = $parsedAssetBudget
    }

    $tool = Resolve-StarterTool 'Tools/configure_manifest_contract.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Capability $contractCapability -CapabilityState $contractCapabilityState -MaxEnvelopesPerFrame $contractMaxEnvelopesPerFrame -MaxAssetBytes $contractMaxAssetBytes
    Complete-StarterTool
}

function Invoke-FirstMod([bool]$PromptForMissingValues) {
    $firstId = if ([string]::IsNullOrWhiteSpace($Id)) { 'com.yourname.firstmod' } else { $Id }
    $firstDisplayName = if ([string]::IsNullOrWhiteSpace($DisplayName)) { 'First HECTON Mod' } else { $DisplayName }
    $firstAuthor = if ([string]::IsNullOrWhiteSpace($Author)) { 'YourName' } else { $Author }
    $firstVersion = if ([string]::IsNullOrWhiteSpace($Version)) { '0.1.0' } else { $Version }
    $firstNodeId = if ($NodeId -eq 'node.spawn_item') { 'node.first_spawn_item' } else { $NodeId }
    $firstOpcode = if ([string]::IsNullOrWhiteSpace($Opcode)) { 'SpawnItem' } else { $Opcode }
    $firstNodeParametersJson = if ($NodeParametersJson -eq '{}') { '{"Item":"demo","Quantity":1}' } else { $NodeParametersJson }
    $firstSettingId = if ($SettingId -eq 'setting.example_toggle') { 'setting.first_enabled' } else { $SettingId }
    $firstSettingKind = if ([string]::IsNullOrWhiteSpace($SettingKind)) { 'bool' } else { $SettingKind }
    $firstSettingDefault = if ($SettingDefault -eq 'false') { 'true' } else { $SettingDefault }
    $firstLocaleKey = if ($LocaleKey -eq 'text.example_line') { 'text.first_mod_ready' } else { $LocaleKey }
    $firstLocaleValue = if ($LocaleValue -eq 'Your localized text') { 'First HECTON mod ready.' } else { $LocaleValue }

    if ($PromptForMissingValues) {
        $firstId = Read-SetupValue $firstId 'Mod id, example com.yourname.firstmod'
        $firstDisplayName = Read-SetupValue $firstDisplayName 'Display name'
        $firstAuthor = Read-SetupValue $firstAuthor 'Author'
        $firstVersion = Read-SetupValue $firstVersion 'Version, example 0.1.0'
        $firstNodeId = Read-SetupValue $firstNodeId 'Graph node id, example node.first_spawn_item'
        $firstOpcode = Read-SetupValue $firstOpcode 'Opcode alias or hex, example SpawnItem'
        $firstNodeParametersJson = Read-SetupValue $firstNodeParametersJson 'Parameters JSON object, example {"Item":"demo","Quantity":1}'
        $firstSettingId = Read-SetupValue $firstSettingId 'Setting id, example setting.first_enabled'
        $firstSettingKind = Read-SetupValue $firstSettingKind 'Setting kind: bool, int, float, string, or enum'
        $firstSettingDefault = Read-SetupValue $firstSettingDefault 'Setting default value'
        $firstLocaleKey = Read-SetupValue $firstLocaleKey 'Locale key, example text.first_mod_ready'
        $firstLocaleValue = Read-SetupValue $firstLocaleValue 'Localized text value'
    }

    $tool = Resolve-StarterTool 'Tools/create_first_mod.ps1'
    $global:LASTEXITCODE = 0
    if ($Replace -and $BuildSubmission) {
        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -Replace -BuildSubmission
    } elseif ($Replace) {
        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -Replace
    } elseif ($BuildSubmission) {
        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue -BuildSubmission
    } else {
        & $tool -Root $Root -Id $firstId -DisplayName $firstDisplayName -Author $firstAuthor -Version $firstVersion -NodeId $firstNodeId -Opcode $firstOpcode -NodeParametersJson $firstNodeParametersJson -SettingId $firstSettingId -SettingKind $firstSettingKind -SettingDefault $firstSettingDefault -LocaleKey $firstLocaleKey -LocaleValue $firstLocaleValue
    }
    Complete-StarterTool
}

function Require-SetupValue([string]$Value, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Fail ($Name + ' is required for setup. Provide -' + $Name + ' or run menu mode.')
    }
    return $Value
}

function Read-SetupValue([string]$Value, [string]$Prompt) {
    if (-not [string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }
    return Read-Host $Prompt
}

function Invoke-Setup([bool]$PromptForMissingValues) {
    $setupId = $Id
    $setupDisplayName = $DisplayName
    $setupAuthor = $Author
    $setupVersion = $Version

    if ($PromptForMissingValues) {
        $setupId = Read-SetupValue $setupId 'Mod id, example com.yourname.mod'
        $setupDisplayName = Read-SetupValue $setupDisplayName 'Display name'
        $setupAuthor = Read-SetupValue $setupAuthor 'Author'
        $setupVersion = Read-SetupValue $setupVersion 'Version, example 0.1.0'
    } else {
        $setupId = Require-SetupValue $setupId 'Id'
        $setupDisplayName = Require-SetupValue $setupDisplayName 'DisplayName'
        $setupAuthor = Require-SetupValue $setupAuthor 'Author'
        $setupVersion = Require-SetupValue $setupVersion 'Version'
    }

    $tool = Resolve-StarterTool 'Tools/prepare_mod.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Id $setupId -DisplayName $setupDisplayName -Author $setupAuthor -Version $setupVersion
    Complete-StarterTool
}

function Show-Menu {
    Write-Host ''
    Write-Host 'HECTON-8 External Starter Kit'
    Write-Host '1 setup identity + build review'
    Write-Host '2 validate structure'
    Write-Host '3 build review manifest'
    Write-Host '4 prepare existing manifest'
    Write-Host '5 build submission package'
    Write-Host '6 list graph opcodes'
    Write-Host '7 list graph opcodes JSON'
    Write-Host '8 create graph node snippet'
    Write-Host '9 apply graph node snippet'
    Write-Host '10 create setting row snippet'
    Write-Host '11 create locale entry snippet'
    Write-Host '12 apply setting row snippet'
    Write-Host '13 apply locale entry snippet'
    Write-Host '14 create asset entry snippet'
    Write-Host '15 apply asset entry snippet'
    Write-Host '16 configure manifest capability/budgets'
    Write-Host '17 show capability matrix'
    Write-Host '18 create first playable mod'
    Write-Host '19 install local discovery copy'
    Write-Host '20 diagnose local Mods folder'
    Write-Host '21 configure dependencies'
    Write-Host '22 doctor package readiness'
    Write-Host 'q quit'
    Write-Host ''
    $choice = Read-Host 'Select action'

    switch ($choice) {
        '1' { Invoke-Setup $true }
        '2' { Invoke-Validate }
        '3' { Invoke-Review }
        '4' { Invoke-PrepareExisting }
        '5' { Invoke-SubmissionPackage }
        '6' { Invoke-Opcodes $false }
        '7' { Invoke-Opcodes $true }
        '8' { Invoke-GraphNodeSnippet $true }
        '9' { Invoke-ApplyGraphNodeSnippet $true }
        '10' { Invoke-SettingsRowSnippet $true }
        '11' { Invoke-LocaleEntrySnippet $true }
        '12' { Invoke-ApplySettingsRowSnippet $true }
        '13' { Invoke-ApplyLocaleEntrySnippet $true }
        '14' { Invoke-AssetEntrySnippet $true }
        '15' { Invoke-ApplyAssetEntrySnippet $true }
        '16' { Invoke-ManifestContractConfig $true }
        '17' { Invoke-Capabilities }
        '18' { Invoke-FirstMod $true }
        '19' { Invoke-InstallLocal $true }
        '20' { Invoke-DiagnoseLocal $true }
        '21' { Invoke-Dependencies $true }
        '22' { Invoke-Doctor }
        'q' { return }
        'Q' { return }
        default { Fail ('Unknown menu action: ' + $choice) }
    }
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

switch ($Action) {
    'menu' { Show-Menu }
    'first-mod' { Invoke-FirstMod $false }
    'install-local' { Invoke-InstallLocal $false }
    'diagnose-local' { Invoke-DiagnoseLocal $false }
    'doctor' { Invoke-Doctor }
    'dependencies' { Invoke-Dependencies $false }
    'setup' { Invoke-Setup $false }
    'validate' { Invoke-Validate }
    'review' { Invoke-Review }
    'prepare' { Invoke-PrepareExisting }
    'submission' { Invoke-SubmissionPackage }
    'opcodes' { Invoke-Opcodes $false }
    'opcodes-json' { Invoke-Opcodes $true }
    'node-snippet' { Invoke-GraphNodeSnippet $false }
    'apply-node-snippet' { Invoke-ApplyGraphNodeSnippet $false }
    'setting-snippet' { Invoke-SettingsRowSnippet $false }
    'locale-snippet' { Invoke-LocaleEntrySnippet $false }
    'apply-setting-snippet' { Invoke-ApplySettingsRowSnippet $false }
    'apply-locale-snippet' { Invoke-ApplyLocaleEntrySnippet $false }
    'asset-snippet' { Invoke-AssetEntrySnippet $false }
    'apply-asset-snippet' { Invoke-ApplyAssetEntrySnippet $false }
    'manifest-contract' { Invoke-ManifestContractConfig $false }
    'capabilities' { Invoke-Capabilities }
    default { Fail ('Unsupported action: ' + $Action) }
}
