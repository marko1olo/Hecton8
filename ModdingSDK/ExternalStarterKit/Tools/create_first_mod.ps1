param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id = 'com.yourname.firstmod',
    [string]$DisplayName = 'First HECTON Mod',
    [string]$Author = 'YourName',
    [string]$Version = '0.1.0',
    [string]$NodeId = 'node.first_spawn_item',
    [string]$Opcode = 'SpawnItem',
    [string]$NodeParametersJson = '{"Item":"demo","Quantity":1}',
    [string]$SettingId = 'setting.first_enabled',
    [string]$SettingKind = 'bool',
    [string]$SettingDefault = 'true',
    [string]$LocaleKey = 'text.first_mod_ready',
    [string]$LocaleValue = 'First HECTON mod ready.',
    [switch]$Replace,
    [switch]$BuildSubmission,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_FIRST_MOD] ' + $Message)
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

function Resolve-Tool([string]$RelativePath) {
    $tool = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        Fail ('Missing starter tool: ' + $RelativePath)
    }
    return $tool
}

function Complete-Tool([bool]$ToolSucceeded, [int]$ToolExitCode, [string]$Step) {
    if ($ToolExitCode -ne 0) {
        exit $ToolExitCode
    }
    if (-not $ToolSucceeded) {
        Fail ($Step + ' failed.')
    }
}

function Invoke-Tool([scriptblock]$Invocation, [string]$Step) {
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $Invocation *> $null
    } else {
        & $Invocation | Out-Host
    }
    $toolSucceeded = $?
    $toolExitCode = $global:LASTEXITCODE
    Complete-Tool $toolSucceeded $toolExitCode $Step
}

function Select-TextOrDefault([string]$Value, [string]$DefaultValue) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $DefaultValue
    }
    return $Value
}

function Invoke-ApplyTool([string]$ToolPath, [string]$SnippetPath, [string]$Step) {
    if ($Replace) {
        Invoke-Tool { & $ToolPath -Root $Root -Snippet $SnippetPath -Replace } $Step
    } else {
        Invoke-Tool { & $ToolPath -Root $Root -Snippet $SnippetPath } $Step
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$Id = Select-TextOrDefault $Id 'com.yourname.firstmod'
$DisplayName = Select-TextOrDefault $DisplayName 'First HECTON Mod'
$Author = Select-TextOrDefault $Author 'YourName'
$Version = Select-TextOrDefault $Version '0.1.0'
$NodeId = Select-TextOrDefault $NodeId 'node.first_spawn_item'
$Opcode = Select-TextOrDefault $Opcode 'SpawnItem'
$NodeParametersJson = Select-TextOrDefault $NodeParametersJson '{"Item":"demo","Quantity":1}'
$SettingId = Select-TextOrDefault $SettingId 'setting.first_enabled'
$SettingKind = Select-TextOrDefault $SettingKind 'bool'
$SettingDefault = Select-TextOrDefault $SettingDefault 'true'
$LocaleKey = Select-TextOrDefault $LocaleKey 'text.first_mod_ready'
$LocaleValue = Select-TextOrDefault $LocaleValue 'First HECTON mod ready.'

$graphSnippet = 'Generated/first_mod_graph_node.json'
$settingSnippet = 'Generated/first_mod_setting.json'
$localeSnippet = 'Generated/first_mod_locale.json'

$prepareTool = Resolve-Tool 'Tools/prepare_mod.ps1'
$contractTool = Resolve-Tool 'Tools/configure_manifest_contract.ps1'
$graphCreateTool = Resolve-Tool 'Tools/create_graph_node_snippet.ps1'
$graphApplyTool = Resolve-Tool 'Tools/apply_graph_node_snippet.ps1'
$settingCreateTool = Resolve-Tool 'Tools/create_settings_row_snippet.ps1'
$settingApplyTool = Resolve-Tool 'Tools/apply_settings_row_snippet.ps1'
$localeCreateTool = Resolve-Tool 'Tools/create_locale_entry_snippet.ps1'
$localeApplyTool = Resolve-Tool 'Tools/apply_locale_entry_snippet.ps1'
$validatorTool = Resolve-Tool 'Tools/validate_structure.ps1'
$reviewTool = Resolve-Tool 'Tools/build_review_manifest.ps1'
$submissionTool = Resolve-Tool 'Tools/build_submission_package.ps1'

Invoke-Tool { & $prepareTool -Root $Root -Id $Id -DisplayName $DisplayName -Author $Author -Version $Version } 'identity setup'

Invoke-Tool { & $contractTool -Root $Root -Capability 'cap.graph.command_draft' -CapabilityState enable -MaxEnvelopesPerFrame 1 -MaxAssetBytes -1 } 'manifest contract setup'

Invoke-Tool { & $graphCreateTool -Root $Root -Id $NodeId -Opcode $Opcode -ParametersJson $NodeParametersJson -Output $graphSnippet } 'graph node snippet creation'
Invoke-ApplyTool $graphApplyTool $graphSnippet 'graph node apply'

Invoke-Tool { & $settingCreateTool -Root $Root -Id $SettingId -Kind $SettingKind -Default $SettingDefault -Output $settingSnippet } 'setting snippet creation'
Invoke-ApplyTool $settingApplyTool $settingSnippet 'setting apply'

Invoke-Tool { & $localeCreateTool -Root $Root -Key $LocaleKey -Value $LocaleValue -Output $localeSnippet } 'locale snippet creation'
Invoke-ApplyTool $localeApplyTool $localeSnippet 'locale apply'

Invoke-Tool { & $validatorTool -Root $Root } 'starter validation'

Invoke-Tool { & $reviewTool -Root $Root } 'review manifest build'

$submissionOutput = ''
if ($BuildSubmission) {
    Invoke-Tool { & $submissionTool -Root $Root } 'submission package build'
    $submissionOutput = 'Generated/' + $Id + '_submission.zip'
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.first_mod.v1'
        Runtime = 'envelope-only'
        Id = $Id
        DisplayName = $DisplayName
        GraphNode = $NodeId
        Setting = $SettingId
        Locale = $LocaleKey
        ReviewManifest = 'Reports/review_manifest.json'
        SubmissionPackage = $submissionOutput
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 first playable mod created'
Write-Output ('Id: ' + $Id)
Write-Output ('Graph node: ' + $NodeId)
Write-Output ('Setting: ' + $SettingId)
Write-Output ('Locale: ' + $LocaleKey)
Write-Output 'Review manifest: Reports/review_manifest.json'
if ($BuildSubmission) {
    Write-Output ('Submission package: ' + $submissionOutput)
} else {
    Write-Output 'Next: h8mod.ps1 -Action submission'
}
