param(
    [string]$Source = "",
    [switch]$UseNewestDownload,
    [string]$Batch = "Batch32",
    [string]$SpecJson = "Docs/GeneratedAssets/Gemini/Prompts/Batch32/3203_INVENTORY_GAP_SHEET_FROM_PLANNED_MAP_20260607.spec.json",
    [int]$Limit = 12,
    [int]$GridRows = 3,
    [int]$GridColumns = 4,
    [int]$SourceEdgeMarginPx = 32,
    [switch]$ImportWithUnity,
    [switch]$AllowOverwrite,
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$intakeRoot = Join-Path $projectRoot "Docs\GeneratedAssets\Gemini\Outputs\$Batch\InventoryGapObjects_$(Get-Date -Format yyyyMMdd)"
$runner = Join-Path $projectRoot "Tools\RunInventoryIconSheetBatch.ps1"

function Resolve-ProjectOrAbsolutePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return Resolve-Path -LiteralPath $Path
    }

    return Resolve-Path -LiteralPath (Join-Path $projectRoot $Path)
}

function Find-NewestDownloadedImage {
    $downloads = Join-Path $env:USERPROFILE "Downloads"
    if (-not (Test-Path -LiteralPath $downloads)) {
        throw "Downloads folder not found: $downloads"
    }

    $candidates = Get-ChildItem -LiteralPath $downloads -File |
        Where-Object { $_.Extension -match '^\.(png|jpg|jpeg|webp)$' } |
        Sort-Object LastWriteTime -Descending

    $candidate = $candidates | Select-Object -First 1
    if (-not $candidate) {
        throw "No PNG/JPG/WEBP image found in Downloads. Pass -Source explicitly."
    }

    return $candidate.FullName
}

if (-not $Source -and -not $UseNewestDownload) {
    throw "Pass -Source explicitly, or use -UseNewestDownload to intentionally pick the newest image from Downloads."
}

$sourcePath = if ($Source) { (Resolve-ProjectOrAbsolutePath -Path $Source).Path } else { Find-NewestDownloadedImage }
$extension = [System.IO.Path]::GetExtension($sourcePath).ToLowerInvariant()
if ($extension -notin @(".png", ".jpg", ".jpeg", ".webp")) {
    throw "Unsupported sheet extension '$extension'. Expected PNG/JPG/WEBP."
}

$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash

New-Item -ItemType Directory -Force -Path $intakeRoot | Out-Null
$safeTimestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$targetName = "TX_${Batch}_InventoryGap_Source_${safeTimestamp}${extension}"
$targetPath = Join-Path $intakeRoot $targetName
Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
Write-Host "Inventory icon Gemini source copied. source=$sourcePath sha256=$sourceHash target=$targetPath"

$runnerArgs = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $runner,
    "-Source",
    $targetPath,
    "-Batch",
    $Batch,
    "-SpecJson",
    $SpecJson,
    "-Limit",
    $Limit,
    "-GridRows",
    $GridRows,
    "-GridColumns",
    $GridColumns,
    "-SourceEdgeMarginPx",
    $SourceEdgeMarginPx
)

if ($ImportWithUnity) {
    Write-Warning "Fresh Gemini sheets cannot be imported in the same pass. They must pass visual review first."
}

$runnerArgs += "-OfflineOnly"

if ($AllowOverwrite) {
    $runnerArgs += "-AllowOverwrite"
}

if ($WaitForGate) {
    $runnerArgs += @("-WaitForGate", "-MaxWaitSeconds", $MaxWaitSeconds)
}

& powershell @runnerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inventory icon Gemini sheet intake failed. copiedSource=$targetPath batch=$Batch"
}

Write-Host "Inventory icon Gemini sheet intake passed. source=$targetPath batch=$Batch"
