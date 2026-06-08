param(
    [string]$Source = "",
    [switch]$UseNewestDownload,
    [int]$SourceEdgeMarginPx = 32,
    [switch]$AllowOverwrite,
    [switch]$AllowSuspiciousSource
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$sheetIntake = Join-Path $projectRoot "Tools\RunInventoryIconGeminiSheetIntake.ps1"
$coverageValidator = Join-Path $projectRoot "Tools\ValidateToolPresentationCoverage.py"
$specJson = "Docs/GeneratedAssets/Gemini/Prompts/Batch33/3301_TOOL_INVENTORY_SHEET_FROM_WORLD_PREFABS_20260607.spec.json"
$bindingMap = Join-Path $projectRoot "Assets\_Project\Art\Sprites\ui\InventoryGenerated\Batch33\InventoryIconCandidateBindingMap.json"

$intakeArgs = @{
    Batch = "Batch33"
    SpecJson = $specJson
    Limit = 12
    GridRows = 3
    GridColumns = 4
    SourceEdgeMarginPx = $SourceEdgeMarginPx
}

if ($Source) {
    $intakeArgs["Source"] = $Source
}
if ($UseNewestDownload) {
    $intakeArgs["UseNewestDownload"] = $true
}
if ($AllowOverwrite) {
    $intakeArgs["AllowOverwrite"] = $true
}
if ($AllowSuspiciousSource) {
    $intakeArgs["AllowSuspiciousSource"] = $true
}

& $sheetIntake @intakeArgs
if ($LASTEXITCODE -ne 0) {
    throw "Batch33 tool inventory sheet intake failed."
}

if (-not (Test-Path -LiteralPath $bindingMap)) {
    throw "Batch33 binding map was not generated: $bindingMap"
}

python -B $coverageValidator
if ($LASTEXITCODE -ne 0) {
    throw "Tool presentation coverage validation failed after Batch33 intake."
}

Write-Host "Batch33 tool inventory sheet intake passed. Review generated previews, then approve/reject bindings before Unity bind. bindingMap=$bindingMap"
