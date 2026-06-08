param(
    [switch]$FailOnPending,
    [switch]$AllowAlreadyBoundIcons
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$materialPreflight = Join-Path $projectRoot "Tools\RunGeminiMaterialStaticPreflight.ps1"
$bindingValidator = Join-Path $projectRoot "Tools\InventoryIconBindingMapValidator.py"
$coverageValidator = Join-Path $projectRoot "Tools\ValidateToolPresentationCoverage.py"
$batch30BindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch30\InventoryIconCandidateBindingMap.json"
$batch33BindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch33\InventoryIconCandidateBindingMap.json"
$batch33SpecJson = Join-Path $projectRoot "Docs\GeneratedAssets\Gemini\Prompts\Batch33\3301_TOOL_INVENTORY_SHEET_FROM_WORLD_PREFABS_20260607.spec.json"

& $materialPreflight
if ($LASTEXITCODE -ne 0) {
    throw "Gemini material static preflight failed."
}

$batch30Args = @(
    "-B",
    $bindingValidator,
    "--map",
    $batch30BindingMap,
    "--require-approved-bindings",
    "--require-source-bake-manifest",
    "--allow-bake-review"
)
if (-not $AllowAlreadyBoundIcons) {
    $batch30Args += "--require-empty-icon"
}

python @batch30Args
if ($LASTEXITCODE -ne 0) {
    throw "Inventory icon Batch30 approved binding validation failed."
}

if (Test-Path -LiteralPath $batch33BindingMap) {
    $batch33Args = @(
        "-B",
        $bindingValidator,
        "--map",
        $batch33BindingMap,
        "--spec-json",
        $batch33SpecJson,
        "--require-source-bake-manifest",
        "--allow-disabled-spec-gaps",
        "--edge-margin-px",
        32
    )
    if (-not $AllowAlreadyBoundIcons) {
        $batch33Args += "--require-empty-icon"
    }

    python @batch33Args
    if ($LASTEXITCODE -ne 0) {
        throw "Inventory icon Batch33 binding validation failed."
    }
}

$coverageArgs = @("-B", $coverageValidator)
if ($FailOnPending) {
    $coverageArgs += "--fail-on-pending"
}

python @coverageArgs
if ($LASTEXITCODE -ne 0) {
    throw "Tool presentation coverage validation failed."
}

Write-Host "Tool presentation static preflight passed."
