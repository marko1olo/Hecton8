param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Output = '',
    [string]$ReviewOutput = 'Reports/review_manifest.json'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxSubmissionPackageEntryBytes = 4194304
$MaxReviewManifestBytes = 1048576
$ReservedTopLevelFolders = @('Content','Docs','Generated','Graphs','Locales','Reference','Reports','Schemas','Tables','Tools','.vscode')

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
        -not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        return $false
    }
    return $true
}

function Assert-StandardReviewOutput([string]$RelativePath) {
    if (-not (Test-SafeRelativePath $RelativePath 'Reports/')) {
        Fail 'ReviewOutput path must be exactly Reports/review_manifest.json.'
    }

    $normalized = $RelativePath.Replace('\','/')
    if (-not $normalized.Equals('Reports/review_manifest.json', [System.StringComparison]::Ordinal)) {
        Fail 'ReviewOutput path must be exactly Reports/review_manifest.json.'
    }
    return $normalized
}

function Test-Sha256Hex([string]$Value) {
    return -not [string]::IsNullOrWhiteSpace($Value) -and $Value -cmatch '^[0-9a-f]{64}$'
}

function Test-ReservedTopLevelCaseVariant([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\','/')
    $slash = $normalized.IndexOf('/')
    if ($slash -lt 0) {
        $topLevel = $normalized
    } else {
        $topLevel = $normalized.Substring(0, $slash)
    }

    foreach ($reservedName in $ReservedTopLevelFolders) {
        if ([string]::Equals($topLevel, $reservedName, [System.StringComparison]::OrdinalIgnoreCase) -and
            -not [string]::Equals($topLevel, $reservedName, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Get-NumericLong([object]$Value, [string]$Label) {
    if ($null -eq $Value) {
        Fail ($Label + ' is missing.')
    }

    [long]$number = 0
    if (-not [long]::TryParse(([string]$Value), [ref]$number)) {
        Fail ($Label + ' is not an integer.')
    }
    return $number
}

function Read-JsonFile([string]$Path, [string]$Label, [long]$MaxBytes) {
    try {
        return Read-H8JsonFileCapped $Path $Label $MaxBytes
    } catch {
        Fail $_.Exception.Message
    }
}

function Invoke-RequiredTool([scriptblock]$Invocation, [string]$Step) {
    $global:LASTEXITCODE = 0
    & $Invocation | Out-Host
    $toolSucceeded = $?
    $toolExitCode = $global:LASTEXITCODE
    if ($toolExitCode -ne 0) {
        exit $toolExitCode
    }
    if (-not $toolSucceeded) {
        Fail ($Step + ' failed.')
    }
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

$ReviewOutput = Assert-StandardReviewOutput $ReviewOutput

Invoke-RequiredTool { & $prepareTool -Root $rootFull -ReviewOutput $ReviewOutput } 'prepare/review manifest'

$reviewPath = Require-File $ReviewOutput
$review = Read-JsonFile $reviewPath 'Reports/review_manifest.json' $MaxReviewManifestBytes
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
if (-not $Output.Replace('\','/').EndsWith('.zip', [System.StringComparison]::Ordinal)) {
    Fail 'Output path must end with .zip.'
}

$outputPath = Join-StarterPath $rootFull $Output
$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
}

$sourceEntries = New-Object 'System.Collections.Generic.List[string]'
$seenEntries = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::Ordinal)
$seenCaseFoldEntries = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($file in @($review.Files)) {
    $relative = [string]$file.Path
    if (-not (Test-SafeRelativePath $relative '')) {
        Fail ('Unsafe review file path: ' + $relative)
    }
    if (Test-ReservedTopLevelCaseVariant $relative) {
        Fail ('Reserved starter top-level folder casing mismatch in review file path: ' + $relative)
    }
    $expectedBytes = Get-NumericLong $file.Bytes ('Review file byte count for ' + $relative)
    if ($expectedBytes -lt 0 -or $expectedBytes -gt $MaxSubmissionPackageEntryBytes) {
        Fail ('Review file byte count is outside submission package limit: ' + $relative)
    }
    if (-not (Test-Sha256Hex ([string]$file.Sha256))) {
        Fail ('Review file SHA-256 is invalid: ' + $relative)
    }
    if ($relative.StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail ('Review manifest must not package Generated output: ' + $relative)
    }
    if ($relative.StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase)) {
        Fail ('Review manifest must not package Reports output: ' + $relative)
    }

    $entry = Normalize-EntryName $relative
    if ($seenEntries.ContainsKey($entry) -or $seenCaseFoldEntries.ContainsKey($entry)) {
        Fail ('Review manifest source path duplicate or case-fold duplicate: ' + $entry)
    }
    $seenEntries[$entry] = $true
    $seenCaseFoldEntries[$entry] = $true
    [void]$sourceEntries.Add($relative)
}

$reviewEntry = Normalize-EntryName $ReviewOutput
if ($seenEntries.ContainsKey($reviewEntry) -or $seenCaseFoldEntries.ContainsKey($reviewEntry)) {
    Fail ('Review manifest path duplicate or case-fold duplicate: ' + $reviewEntry)
}
$seenEntries[$reviewEntry] = $true
$seenCaseFoldEntries[$reviewEntry] = $true
[void]$sourceEntries.Add($ReviewOutput)

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
    $reviewTimestampUtc = (Get-Item -LiteralPath $reviewPath).LastWriteTimeUtc
    $outputItem = Get-Item -LiteralPath $outputPath
    if ($outputItem.LastWriteTimeUtc -lt $reviewTimestampUtc) {
        $outputItem.LastWriteTimeUtc = $reviewTimestampUtc.AddSeconds(1)
    }
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
