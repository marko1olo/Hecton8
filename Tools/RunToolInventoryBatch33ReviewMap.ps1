param(
    [switch]$ApproveAllEnabled,
    [string[]]$ApprovePersistentId = @(),
    [string[]]$RejectPersistentId = @(),
    [Parameter(Mandatory = $true)]
    [string]$Reason,
    [string]$Reviewer = "codex-visual-review",
    [switch]$AllowBakeReview
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$bindingValidator = Join-Path $projectRoot "Tools\InventoryIconBindingMapValidator.py"
$reviewTool = Join-Path $projectRoot "Tools\InventoryIconReviewMap.py"
$coverageValidator = Join-Path $projectRoot "Tools\ValidateToolPresentationCoverage.py"
$bindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch33\InventoryIconCandidateBindingMap.json"
$specJson = Join-Path $projectRoot "Docs\GeneratedAssets\Gemini\Prompts\Batch33\3301_TOOL_INVENTORY_SHEET_FROM_WORLD_PREFABS_20260607.spec.json"

if (-not (Test-Path -LiteralPath $bindingMap)) {
    throw "Batch33 binding map is missing. Run Tools\RunToolInventoryBatch33SheetIntake.ps1 first."
}

if (-not $ApproveAllEnabled -and $ApprovePersistentId.Count -eq 0 -and $RejectPersistentId.Count -eq 0) {
    throw "Provide -ApproveAllEnabled, -ApprovePersistentId, or -RejectPersistentId."
}

if ($ApproveAllEnabled -and $RejectPersistentId.Count -gt 0) {
    throw "Do not combine -ApproveAllEnabled with -RejectPersistentId. Reject bad cells first, then regenerate or approve the remaining map explicitly."
}

$preArgs = @(
    "-B",
    $bindingValidator,
    "--map",
    $bindingMap,
    "--spec-json",
    $specJson,
    "--allow-disabled-spec-gaps",
    "--require-empty-icon",
    "--require-source-bake-manifest",
    "--edge-margin-px",
    32
)
if ($AllowBakeReview) {
    $preArgs += "--allow-bake-review"
}

python @preArgs
if ($LASTEXITCODE -ne 0) {
    throw "Batch33 binding map pre-review validation failed."
}

$reviewArgs = @(
    "-B",
    $reviewTool,
    "--map",
    $bindingMap,
    "--reason",
    $Reason,
    "--reviewer",
    $Reviewer
)
if ($ApproveAllEnabled) {
    $reviewArgs += "--approve-all-enabled"
}
foreach ($persistentId in $ApprovePersistentId) {
    $reviewArgs += @("--approve-persistent-id", $persistentId)
}
foreach ($persistentId in $RejectPersistentId) {
    $reviewArgs += @("--reject-persistent-id", $persistentId)
}

python @reviewArgs
if ($LASTEXITCODE -ne 0) {
    throw "Batch33 binding map visual review update failed."
}

$postArgs = @(
    "-B",
    $bindingValidator,
    "--map",
    $bindingMap,
    "--spec-json",
    $specJson,
    "--allow-disabled-spec-gaps",
    "--require-empty-icon",
    "--require-source-bake-manifest",
    "--edge-margin-px",
    32
)
if ($AllowBakeReview) {
    $postArgs += "--allow-bake-review"
}
if ($ApproveAllEnabled) {
    $postArgs += "--require-approved-bindings"
}

python @postArgs
if ($LASTEXITCODE -ne 0) {
    throw "Batch33 binding map post-review validation failed."
}

python -B $coverageValidator
if ($LASTEXITCODE -ne 0) {
    throw "Tool presentation coverage validation failed after Batch33 review."
}

Write-Host "Batch33 tool inventory review passed. bindingMap=$bindingMap"
