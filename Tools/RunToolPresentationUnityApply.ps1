param(
    [int]$CpuLimitPercent = 50,
    [int]$CpuSamples = 4,
    [int]$CpuSampleIntervalSeconds = 2,
    [string]$UnityPath = "",
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900,
    [switch]$AllowIconOverwrite,
    [switch]$StaticOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$staticPreflight = Join-Path $projectRoot "Tools\RunToolPresentationStaticPreflight.ps1"
$materialUnityApply = Join-Path $projectRoot "Tools\RunGeminiMaterialUnityApplyAll.ps1"
$iconUnityBind = Join-Path $projectRoot "Tools\RunInventoryIconUnityBindFromMap.ps1"
$coverageValidator = Join-Path $projectRoot "Tools\ValidateToolPresentationCoverage.py"
$batch30BindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch30\InventoryIconCandidateBindingMap.json"
$batch33BindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch33\InventoryIconCandidateBindingMap.json"
$batch33SpecJson = Join-Path $projectRoot "Docs\GeneratedAssets\Gemini\Prompts\Batch33\3301_TOOL_INVENTORY_SHEET_FROM_WORLD_PREFABS_20260607.spec.json"

function Test-ItemIconIsEmpty {
    param([string]$ItemAsset)

    if (-not $ItemAsset) {
        return $false
    }

    $itemPath = if ([System.IO.Path]::IsPathRooted($ItemAsset)) { $ItemAsset } else { Join-Path $projectRoot $ItemAsset }
    if (-not (Test-Path -LiteralPath $itemPath)) {
        return $false
    }

    $text = Get-Content -LiteralPath $itemPath -Raw
    $match = [regex]::Match($text, "(?m)^\s*icon:\s*(?<value>.*?)\s*$")
    if (-not $match.Success) {
        return $true
    }

    return $match.Groups["value"].Value.Contains("fileID: 0")
}

function Get-ApprovedBindingIconState {
    param([string]$BindingMap)

    if (-not (Test-Path -LiteralPath $BindingMap)) {
        return [pscustomobject]@{ Approved = 0; Empty = 0; Assigned = 0 }
    }

    $payload = Get-Content -LiteralPath $BindingMap -Raw | ConvertFrom-Json
    $approved = 0
    $empty = 0
    $assigned = 0
    foreach ($binding in $payload.bindings) {
        if (-not $binding.enabled) {
            continue
        }

        if (-not (Test-BindingHasVisualApproval -Binding $binding)) {
            continue
        }

        $approved++
        if (Test-ItemIconIsEmpty -ItemAsset ([string]$binding.itemAsset)) {
            $empty++
        }
        else {
            $assigned++
        }
    }

    return [pscustomobject]@{ Approved = $approved; Empty = $empty; Assigned = $assigned }
}

function Test-BindingHasVisualApproval {
    param($Binding)

    if (-not $Binding.enabled) {
        return $false
    }

    $reviewStatus = if ($Binding.PSObject.Properties.Name -contains "reviewStatus") { [string]$Binding.reviewStatus } else { "" }
    $approved = $Binding.approved -or $reviewStatus.ToUpperInvariant() -eq "APPROVED"
    if (-not $approved) {
        return $false
    }

    $reviewedBy = if ($Binding.PSObject.Properties.Name -contains "reviewedBy") { [string]$Binding.reviewedBy } else { "" }
    $reviewedAt = if ($Binding.PSObject.Properties.Name -contains "reviewedAt") { [string]$Binding.reviewedAt } else { "" }
    return (-not [string]::IsNullOrWhiteSpace($reviewedBy)) -and (-not [string]::IsNullOrWhiteSpace($reviewedAt))
}

& $staticPreflight -AllowAlreadyBoundIcons
if ($LASTEXITCODE -ne 0) {
    throw "Tool presentation static preflight failed."
}

if ($StaticOnly) {
    Write-Host "Tool presentation Unity apply static-only gate passed."
    exit 0
}

$materialArgs = @{
    CpuLimitPercent = $CpuLimitPercent
    CpuSamples = $CpuSamples
    CpuSampleIntervalSeconds = $CpuSampleIntervalSeconds
    MaxWaitSeconds = $MaxWaitSeconds
}
if ($UnityPath) {
    $materialArgs["UnityPath"] = $UnityPath
}
if ($WaitForGate) {
    $materialArgs["WaitForGate"] = $true
}

& $materialUnityApply @materialArgs
if ($LASTEXITCODE -ne 0) {
    throw "Generated material Unity apply failed."
}

$iconArgs = @{
    MaxTextureSize = 512
    CpuLimitPercent = $CpuLimitPercent
    CpuSamples = $CpuSamples
    CpuSampleIntervalSeconds = $CpuSampleIntervalSeconds
    MaxWaitSeconds = $MaxWaitSeconds
}
if ($UnityPath) {
    $iconArgs["UnityPath"] = $UnityPath
}
if ($WaitForGate) {
    $iconArgs["WaitForGate"] = $true
}
if ($AllowIconOverwrite) {
    $iconArgs["AllowOverwrite"] = $true
}

foreach ($map in @($batch30BindingMap, $batch33BindingMap)) {
    $iconState = Get-ApprovedBindingIconState -BindingMap $map
    if ($iconState.Approved -eq 0) {
        Write-Host "Skipping inventory icon Unity bind; no approved binding map entries. map=$map"
        continue
    }

    if ($iconState.Empty -eq 0) {
        Write-Host "Skipping inventory icon Unity bind; approved bindings already assigned. map=$map approved=$($iconState.Approved)"
        continue
    }

    $mapAllowOverwrite = $AllowIconOverwrite
    if ($iconState.Empty -ne $iconState.Approved -and -not $AllowIconOverwrite) {
        throw "Partial approved icon bind state. map=$map approved=$($iconState.Approved) empty=$($iconState.Empty) assigned=$($iconState.Assigned). Rerun with -AllowIconOverwrite only after checking the approved map."
    }

    $mapIconArgs = $iconArgs.Clone()
    $mapIconArgs["BindingMap"] = $map
    if ($map -eq $batch33BindingMap) {
        $mapIconArgs["SpecJson"] = $batch33SpecJson
        $mapIconArgs["AllowDisabledSpecGaps"] = $true
    }
    if ($mapAllowOverwrite -and -not $mapIconArgs.ContainsKey("AllowOverwrite")) {
        $mapIconArgs["AllowOverwrite"] = $true
    }
    & $iconUnityBind @mapIconArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Inventory icon Unity bind failed. map=$map"
    }
}

python -B $coverageValidator
if ($LASTEXITCODE -ne 0) {
    throw "Tool presentation coverage validation failed after Unity apply."
}

Write-Host "Tool presentation Unity apply route passed. Pending Batch33 icons remain planned until generated sheet intake."
