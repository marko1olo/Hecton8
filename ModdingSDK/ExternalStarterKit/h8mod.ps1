param(
    [ValidateSet('menu','setup','validate','review','prepare','opcodes','opcodes-json')]
    [string]$Action = 'menu',
    [string]$Id = '',
    [string]$DisplayName = '',
    [string]$Author = '',
    [string]$Version = ''
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
    Write-Host '5 list graph opcodes'
    Write-Host '6 list graph opcodes JSON'
    Write-Host 'q quit'
    Write-Host ''
    $choice = Read-Host 'Select action'

    switch ($choice) {
        '1' { Invoke-Setup $true }
        '2' { Invoke-Validate }
        '3' { Invoke-Review }
        '4' { Invoke-PrepareExisting }
        '5' { Invoke-Opcodes $false }
        '6' { Invoke-Opcodes $true }
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
    'opcodes' { Invoke-Opcodes $false }
    'opcodes-json' { Invoke-Opcodes $true }
    default { Fail ('Unsupported action: ' + $Action) }
}
