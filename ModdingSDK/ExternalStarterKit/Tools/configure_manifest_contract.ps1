param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Capability = '',
    [ValidateSet('unchanged','enable','disable')]
    [string]$CapabilityState = 'unchanged',
    [int]$MaxEnvelopesPerFrame = -1,
    [long]$MaxAssetBytes = -1,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

$MaxEnvelopeBudgetCap = 256
$MaxAssetBudgetCap = 33554432

function Fail([string]$Message) {
    Write-Error ('[H8MOD_MANIFEST_CONTRACT] ' + $Message)
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

function Get-AllowedCapabilityIds {
    return @(
        'cap.graph.command_draft',
        'cap.settings.table',
        'cap.locale.en',
        'cap.content.asset_manifest',
        'cap.review.submission_package'
    )
}

function Test-AllowedCapability([string]$CapabilityId) {
    return (Get-AllowedCapabilityIds) -contains $CapabilityId
}

function Validate-CapabilityId([string]$Value) {
    $capabilityId = Validate-CanonicalId $Value 'Capability'
    if (-not (Test-AllowedCapability $capabilityId)) {
        Fail ('Capability is not in the public authoring allowlist: ' + $capabilityId)
    }
    return $capabilityId
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

function Write-JsonFile([string]$Path, [object]$Value) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $jsonText = ($Value | ConvertTo-Json -Depth 32)
    [System.IO.File]::WriteAllText($Path, ($jsonText + [System.Environment]::NewLine), $utf8NoBom)
    [void](Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json)
}

function Remove-TempFile([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Restore-ManifestBackup([string]$BackupPath, [string]$ManifestPath) {
    if (Test-Path -LiteralPath $BackupPath -PathType Leaf) {
        Copy-Item -LiteralPath $BackupPath -Destination $ManifestPath -Force
    }
}

function Validate-AuthoringManifest([object]$ManifestDocument) {
    if ([string]$ManifestDocument.Schema -ne 'hecton8.h8mod.authoring.v1') {
        Fail 'mod.h8manifest.json Schema must be hecton8.h8mod.authoring.v1.'
    }
    if ([string]$ManifestDocument.Compatibility.Runtime -ne 'envelope-only') {
        Fail 'mod.h8manifest.json Compatibility.Runtime must stay envelope-only.'
    }
    if ($null -eq $ManifestDocument.PSObject.Properties['Capabilities'] -or $null -eq $ManifestDocument.Capabilities -or -not $ManifestDocument.Capabilities.GetType().IsArray) {
        Fail 'mod.h8manifest.json Capabilities must be a JSON array.'
    }
    if ($null -eq $ManifestDocument.PSObject.Properties['Budgets'] -or $null -eq $ManifestDocument.Budgets) {
        Fail 'mod.h8manifest.json Budgets is required.'
    }
}

function Get-CleanCapabilities([object[]]$SourceCapabilities) {
    if ($SourceCapabilities.Count -gt 16) { Fail 'mod.h8manifest.json Capabilities exceeds 16 entries.' }
    $seen = @{}
    $capabilities = New-Object 'System.Collections.Generic.List[string]'
    for ($i = 0; $i -lt $SourceCapabilities.Count; $i++) {
        $capabilityId = Validate-CapabilityId ([string]$SourceCapabilities[$i])
        if ($seen.ContainsKey($capabilityId)) {
            Fail ('mod.h8manifest.json duplicate Capability: ' + $capabilityId)
        }
        $seen[$capabilityId] = $true
        [void]$capabilities.Add($capabilityId)
    }
    return ,$capabilities
}

function Get-GraphRequiredEnvelopeBudget([string]$RootPath) {
    $graphPath = Join-StarterPath $RootPath 'Graphs/main.h8graph.json'
    $graph = Read-JsonFile $graphPath 'Graph'
    try {
        return [math]::Max(0, [int]$graph.MaxEnvelopesPerFrame)
    } catch {
        Fail 'Graphs/main.h8graph.json MaxEnvelopesPerFrame must be a JSON integer.'
    }
}

function Get-AssetManifestByteTotal([string]$RootPath) {
    $assetManifestPath = Join-StarterPath $RootPath 'Content/assets.h8manifest.json'
    $assets = Read-JsonFile $assetManifestPath 'Asset manifest'
    if ($null -eq $assets.PSObject.Properties['Assets'] -or $null -eq $assets.Assets -or -not $assets.Assets.GetType().IsArray) {
        Fail 'Content/assets.h8manifest.json Assets must be a JSON array.'
    }

    [long]$totalBytes = 0
    foreach ($asset in @($assets.Assets)) {
        try {
            $totalBytes += [long]$asset.Bytes
        } catch {
            Fail 'Content/assets.h8manifest.json asset Bytes must be JSON integers.'
        }
    }
    return $totalBytes
}

function Validate-RequestedBudgets([int]$EnvelopeBudget, [long]$AssetBudget, [int]$RequiredEnvelopeBudget, [long]$RequiredAssetBytes) {
    if ($EnvelopeBudget -lt -1) { Fail 'MaxEnvelopesPerFrame must be -1 for unchanged or a value from 0 to 256.' }
    if ($EnvelopeBudget -gt $MaxEnvelopeBudgetCap) { Fail ('MaxEnvelopesPerFrame exceeds public starter cap: ' + $MaxEnvelopeBudgetCap) }
    if ($EnvelopeBudget -ge 0 -and $EnvelopeBudget -lt $RequiredEnvelopeBudget) {
        Fail ('MaxEnvelopesPerFrame cannot be lower than current graph requirement: ' + $RequiredEnvelopeBudget)
    }

    if ($AssetBudget -lt -1) { Fail 'MaxAssetBytes must be -1 for unchanged or a value from 0 to 33554432.' }
    if ($AssetBudget -gt $MaxAssetBudgetCap) { Fail ('MaxAssetBytes exceeds public starter source cap: ' + $MaxAssetBudgetCap) }
    if ($AssetBudget -ge 0 -and $AssetBudget -lt $RequiredAssetBytes) {
        Fail ('MaxAssetBytes cannot be lower than currently declared asset bytes: ' + $RequiredAssetBytes)
    }
}

function Build-ManifestDocument([object]$Document, [string[]]$Capabilities, [int]$EnvelopeBudget, [long]$AssetBudget) {
    $budgets = [pscustomobject][ordered]@{
        MaxEnvelopesPerFrame = $EnvelopeBudget
        MaxAssetBytes = $AssetBudget
    }

    $output = [ordered]@{}
    foreach ($property in $Document.PSObject.Properties) {
        if ($property.Name -eq 'Capabilities') {
            $output.Capabilities = $Capabilities
        } elseif ($property.Name -eq 'Budgets') {
            $output.Budgets = $budgets
        } else {
            $output[$property.Name] = $property.Value
        }
    }
    if (-not $output.Contains('Capabilities')) {
        $output.Capabilities = $Capabilities
    }
    if (-not $output.Contains('Budgets')) {
        $output.Budgets = $budgets
    }
    return [pscustomobject]$output
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
        throw ('Validation failed after manifest contract update: ' + $_.Exception.Message)
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$manifestPath = Join-StarterPath $Root 'mod.h8manifest.json'
$manifest = Read-JsonFile $manifestPath 'Authoring manifest'
Validate-AuthoringManifest $manifest

$requiredEnvelopeBudget = Get-GraphRequiredEnvelopeBudget $Root
$requiredAssetBytes = Get-AssetManifestByteTotal $Root
Validate-RequestedBudgets $MaxEnvelopesPerFrame $MaxAssetBytes $requiredEnvelopeBudget $requiredAssetBytes

$capabilities = Get-CleanCapabilities @($manifest.Capabilities)
$capabilityId = ''
$capabilityChanged = $false
if ($CapabilityState -ne 'unchanged') {
    $capabilityId = Validate-CapabilityId $Capability
    $existingIndex = -1
    for ($i = 0; $i -lt $capabilities.Count; $i++) {
        if ($capabilities[$i] -eq $capabilityId) {
            $existingIndex = $i
            break
        }
    }

    if ($CapabilityState -eq 'enable' -and $existingIndex -lt 0) {
        if ($capabilities.Count -ge 16) { Fail 'mod.h8manifest.json Capabilities already has 16 entries.' }
        [void]$capabilities.Add($capabilityId)
        $capabilityChanged = $true
    } elseif ($CapabilityState -eq 'disable' -and $existingIndex -ge 0) {
        $capabilities.RemoveAt($existingIndex)
        $capabilityChanged = $true
    }
}

[int]$oldEnvelopeBudget = [int]$manifest.Budgets.MaxEnvelopesPerFrame
[long]$oldAssetBudget = [long]$manifest.Budgets.MaxAssetBytes
[int]$newEnvelopeBudget = if ($MaxEnvelopesPerFrame -ge 0) { $MaxEnvelopesPerFrame } else { $oldEnvelopeBudget }
[long]$newAssetBudget = if ($MaxAssetBytes -ge 0) { $MaxAssetBytes } else { $oldAssetBudget }
$budgetChanged = $oldEnvelopeBudget -ne $newEnvelopeBudget -or $oldAssetBudget -ne $newAssetBudget

$updatedManifest = Build-ManifestDocument $manifest $capabilities.ToArray() $newEnvelopeBudget $newAssetBudget

$manifestName = [System.IO.Path]::GetFileName($manifestPath)
$uniqueSuffix = [System.Guid]::NewGuid().ToString('N')
$tempRoot = [System.IO.Path]::GetTempPath()
$tempPath = Join-Path $tempRoot ('hecton8-' + $manifestName + '.tmp-' + $uniqueSuffix)
$backupPath = Join-Path $tempRoot ('hecton8-' + $manifestName + '.previous-' + $uniqueSuffix)

try {
    Write-JsonFile $tempPath $updatedManifest
    Copy-Item -LiteralPath $manifestPath -Destination $backupPath -Force
    Copy-Item -LiteralPath $tempPath -Destination $manifestPath -Force
    Invoke-StarterValidator $Root
} catch {
    Restore-ManifestBackup $backupPath $manifestPath
    Fail $_.Exception.Message
} finally {
    Remove-TempFile $tempPath
    Remove-TempFile $backupPath
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.manifest_contract_config.v1'
        Runtime = 'envelope-only'
        Manifest = 'mod.h8manifest.json'
        Capability = $capabilityId
        CapabilityState = $CapabilityState
        CapabilityChanged = $capabilityChanged
        Capabilities = $capabilities.ToArray()
        MaxEnvelopesPerFrame = $newEnvelopeBudget
        MaxAssetBytes = $newAssetBudget
        BudgetChanged = $budgetChanged
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 manifest contract configured'
Write-Output ('Capability: ' + $(if ([string]::IsNullOrWhiteSpace($capabilityId)) { 'unchanged' } else { $capabilityId }))
Write-Output ('CapabilityState: ' + $CapabilityState)
Write-Output ('MaxEnvelopesPerFrame: ' + [string]$newEnvelopeBudget)
Write-Output ('MaxAssetBytes: ' + [string]$newAssetBudget)
