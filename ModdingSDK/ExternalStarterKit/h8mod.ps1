param(
    [ValidateSet('menu','setup','validate','review','prepare','submission','opcodes','opcodes-json','node-snippet','apply-node-snippet','setting-snippet','locale-snippet','apply-setting-snippet','apply-locale-snippet','capabilities')]
    [string]$Action = 'menu',
    [string]$Id = '',
    [string]$DisplayName = '',
    [string]$Author = '',
    [string]$Version = '',
    [string]$NodeId = 'node.spawn_item',
    [string]$Opcode = 'SpawnItem',
    [string]$Output = 'Generated/graph_node_snippet.json',
    [string]$NodeSnippet = 'Generated/graph_node_snippet.json',
    [string]$SettingId = 'setting.example_toggle',
    [string]$SettingKind = 'bool',
    [string]$SettingDefault = 'false',
    [string]$SettingOutput = 'Generated/settings_row_snippet.json',
    [string]$SettingSnippet = 'Generated/settings_row_snippet.json',
    [string]$LocaleKey = 'text.example_line',
    [string]$LocaleValue = 'Your localized text',
    [string]$LocaleOutput = 'Generated/locale_entry_snippet.json',
    [string]$LocaleSnippet = 'Generated/locale_entry_snippet.json',
    [switch]$Replace,
    [string]$SubmissionOutput = ''
)

$ErrorActionPreference = 'Stop'

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
    if (-not $?) {
        exit 1
    }
    if ($global:LASTEXITCODE -ne 0) {
        exit $global:LASTEXITCODE
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
    Get-Content -LiteralPath $guide
}

function Invoke-GraphNodeSnippet([bool]$PromptForMissingValues) {
    $snippetNodeId = $NodeId
    $snippetOpcode = $Opcode
    $snippetOutput = $Output

    if ($PromptForMissingValues) {
        $snippetNodeId = Read-SetupValue $snippetNodeId 'Graph node id, example node.spawn_item'
        $snippetOpcode = Read-SetupValue $snippetOpcode 'Opcode alias or hex, example SpawnItem'
        $snippetOutput = Read-SetupValue $snippetOutput 'Output path under Generated/, example Generated/graph_node_snippet.json'
    }

    $tool = Resolve-StarterTool 'Tools/create_graph_node_snippet.ps1'
    $global:LASTEXITCODE = 0
    & $tool -Root $Root -Id $snippetNodeId -Opcode $snippetOpcode -Output $snippetOutput
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
    Write-Host '14 show capability matrix'
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
        '14' { Invoke-Capabilities }
        'q' { return }
        'Q' { return }
        default { Fail ('Unknown menu action: ' + $choice) }
    }
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

switch ($Action) {
    'menu' { Show-Menu }
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
    'capabilities' { Invoke-Capabilities }
    default { Fail ('Unsupported action: ' + $Action) }
}
