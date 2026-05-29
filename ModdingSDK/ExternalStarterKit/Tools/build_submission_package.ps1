param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Output = '',
    [string]$ReviewOutput = 'Reports/review_manifest.json'
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_SUBMISSION_PACKAGE] ' + $Message)
    exit 1
}

function Join-StarterPath([string]$BasePath, [string]$RelativePath) {
    $current = $BasePath
    foreach ($segment in ($RelativePath.Replace('\','/') -split '/')) {
        if (-not [string]::IsNullOrWhiteSpace($segment)) {
            $current = Join-Path $current $segment
        }
    }
    return $current
}

function Test-SafeRelativePath([string]$RelativePath, [string]$RequiredPrefix) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) { return $false }
    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../')) { return $false }
    if (-not [string]::IsNullOrWhiteSpace($RequiredPrefix) -and
        -not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    return $true
}

function Require-File([string]$RelativePath) {
    if (-not (Test-SafeRelativePath $RelativePath '')) {
        Fail ('Unsafe package source path: ' + $RelativePath)
    }

    $path = Join-StarterPath $rootFull $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail ('Missing package source file: ' + $RelativePath)
    }
    return $path
}

function Normalize-EntryName([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.StartsWith('/')) {
        $normalized = $normalized.Substring(1)
    }
    return $normalized
}

function New-TempArtifactPath([string]$LeafName) {
    $safeLeaf = $LeafName -replace '[^a-zA-Z0-9._-]', '_'
    return (Join-Path ([System.IO.Path]::GetTempPath()) ('hecton8-' + $safeLeaf + '-' + [System.Guid]::NewGuid().ToString('N')))
}

function Remove-TempFile([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$prepareTool = Join-StarterPath $rootFull 'Tools/prepare_mod.ps1'
if (-not (Test-Path -LiteralPath $prepareTool -PathType Leaf)) {
    Fail 'Missing Tools/prepare_mod.ps1.'
}

if (-not (Test-SafeRelativePath $ReviewOutput 'Reports/')) {
    Fail 'ReviewOutput path must stay under Reports/.'
}

& $prepareTool -Root $rootFull -ReviewOutput $ReviewOutput | Out-Host

$reviewPath = Require-File $ReviewOutput
$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json
if ([string]$review.Schema -ne 'hecton8.external_review_manifest.v1') {
    Fail 'Review manifest schema mismatch.'
}
if ([string]$review.Runtime -ne 'envelope-only') {
    Fail 'Review manifest runtime must be envelope-only.'
}

$packageId = [string]$review.Identity.Id
if ([string]::IsNullOrWhiteSpace($packageId)) {
    $packageId = [string]$review.RootId
}
if ($packageId -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
    Fail 'Review manifest package id is missing or non-canonical.'
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = 'Generated/' + $packageId + '_submission.zip'
}
if (-not (Test-SafeRelativePath $Output 'Generated/')) {
    Fail 'Output path must stay under Generated/.'
}
if (-not $Output.Replace('\','/').EndsWith('.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
    Fail 'Output path must end with .zip.'
}

$outputPath = Join-StarterPath $rootFull $Output
$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
}

$sourceEntries = New-Object 'System.Collections.Generic.List[string]'
$seenEntries = @{}
foreach ($file in @($review.Files)) {
    $relative = [string]$file.Path
    if (-not (Test-SafeRelativePath $relative '')) {
        Fail ('Unsafe review file path: ' + $relative)
    }
    if ($relative.StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail ('Review manifest must not package Generated output: ' + $relative)
    }
    if ($relative.StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail ('Review manifest must not package Reports output: ' + $relative)
    }

    $entry = Normalize-EntryName $relative
    if (-not $seenEntries.ContainsKey($entry)) {
        $seenEntries[$entry] = $true
        [void]$sourceEntries.Add($relative)
    }
}

$reviewEntry = Normalize-EntryName $ReviewOutput
if (-not $seenEntries.ContainsKey($reviewEntry)) {
    $seenEntries[$reviewEntry] = $true
    [void]$sourceEntries.Add($ReviewOutput)
}

try {
    Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
    Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
} catch {
    Fail ('System.IO.Compression assemblies unavailable: ' + $_.Exception.Message)
}

$outputLeafName = [System.IO.Path]::GetFileName($outputPath)
$tempOutputPath = (New-TempArtifactPath ($outputLeafName + '.tmp.zip'))
$backupOutputPath = (New-TempArtifactPath ($outputLeafName + '.previous.zip'))

$zip = $null
try {
    $zip = [System.IO.Compression.ZipFile]::Open($tempOutputPath, [System.IO.Compression.ZipArchiveMode]::Create)
    foreach ($relative in $sourceEntries) {
        $sourcePath = Require-File $relative
        $entryName = Normalize-EntryName $relative
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $sourcePath,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} catch {
    if ($null -ne $zip) {
        $zip.Dispose()
        $zip = $null
    }
    Remove-TempFile $tempOutputPath
    Fail ('Submission package zip write failed: ' + $_.Exception.Message)
} finally {
    if ($null -ne $zip) {
        $zip.Dispose()
    }
}

$hadPreviousOutput = Test-Path -LiteralPath $outputPath -PathType Leaf
$previousCopiedToBackup = $false
try {
    if ($hadPreviousOutput) {
        Copy-Item -LiteralPath $outputPath -Destination $backupOutputPath -Force
        $previousCopiedToBackup = $true
    }
    Copy-Item -LiteralPath $tempOutputPath -Destination $outputPath -Force
    Remove-TempFile $backupOutputPath
    Remove-TempFile $tempOutputPath
} catch {
    if ($previousCopiedToBackup -and (Test-Path -LiteralPath $backupOutputPath -PathType Leaf)) {
        Copy-Item -LiteralPath $backupOutputPath -Destination $outputPath -Force -ErrorAction SilentlyContinue
    }
    if ((-not $hadPreviousOutput) -and (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue
    }
    Remove-TempFile $backupOutputPath
    Remove-TempFile $tempOutputPath
    Fail ('Submission package zip replace failed: ' + $_.Exception.Message)
}
Write-Host ('PASS HECTON-8 submission package: ' + $Output)
