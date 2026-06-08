param(
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [Parameter(Mandatory = $true)]
    [string]$Batch,
    [Parameter(Mandatory = $true)]
    [string]$SpecJson,
    [string]$PreviousBindingMap = "",
    [int]$Limit = 12,
    [int]$GridRows = 3,
    [int]$GridColumns = 4,
    [int]$SourceEdgeMarginPx = 32,
    [string]$StemPrefix = "",
    [string]$WorkingOutput = "",
    [string]$AssetRoot = "",
    [switch]$AllowNonAssetRoot,
    [int]$CpuLimitPercent = 50,
    [int]$CpuSamples = 4,
    [int]$CpuSampleIntervalSeconds = 2,
    [string]$UnityPath = "",
    [switch]$AllowOverwrite,
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900,
    [switch]$OfflineOnly
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$pipelineScript = Join-Path $projectRoot "Tools\InventoryGapBatchPipeline.py"

function Resolve-ProjectOrAbsolutePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return Resolve-Path -LiteralPath $Path
    }

    return Resolve-Path -LiteralPath (Join-Path $projectRoot $Path)
}

$sourcePath = Resolve-ProjectOrAbsolutePath -Path $Source
$specPath = Resolve-ProjectOrAbsolutePath -Path $SpecJson
$bindingAssetRoot = if ($AssetRoot) { $AssetRoot } else { "Assets/_Project/Art/Sprites/ui/InventoryGenerated" }
$bindingMap = Join-Path (Join-Path $bindingAssetRoot $Batch) "InventoryIconCandidateBindingMap.json"

$pipelineArgs = @(
    "-B",
    $pipelineScript,
    "--source",
    $sourcePath.Path,
    "--batch",
    $Batch,
    "--spec-json",
    $specPath.Path,
    "--limit",
    $Limit,
    "--grid-rows",
    $GridRows,
    "--grid-columns",
    $GridColumns,
    "--source-edge-margin-px",
    $SourceEdgeMarginPx
)

if ($StemPrefix) {
    $pipelineArgs += @("--stem-prefix", $StemPrefix)
}

if ($WorkingOutput) {
    $pipelineArgs += @("--working-output", $WorkingOutput)
}

if ($AssetRoot) {
    $pipelineArgs += @("--asset-root", $AssetRoot)
}

if ($AllowNonAssetRoot) {
    $pipelineArgs += "--allow-non-asset-root"
}

if ($AllowOverwrite) {
    $pipelineArgs += "--allow-overwrite"
}

if ($PreviousBindingMap) {
    $previousPath = Resolve-ProjectOrAbsolutePath -Path $PreviousBindingMap
    $pipelineArgs += @("--previous-binding-map", $previousPath.Path)
}

python @pipelineArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inventory icon offline batch pipeline failed. source=$Source batch=$Batch"
}

if ($OfflineOnly) {
    Write-Host "Inventory icon sheet batch offline pass complete. bindingMap=$bindingMap"
    exit 0
}

throw "Direct Unity bind is blocked for freshly generated sheets because bindings are PENDING_VISUAL_REVIEW. Inspect the generated previews, approve/reject with Tools\InventoryIconReviewMap.py, then run Tools\RunInventoryIconUnityBindFromMap.ps1. bindingMap=$bindingMap"
