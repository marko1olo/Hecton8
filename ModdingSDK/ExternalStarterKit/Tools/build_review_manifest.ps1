param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Output = 'Reports/review_manifest.json'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxReviewFiles = 256
$MaxReviewFileBytes = 4194304
$MaxReviewTotalBytes = 33554432
$MaxManifestJsonBytes = 65536
$ReservedTopLevelFolders = @('Content','Docs','Generated','Graphs','Locales','Reference','Reports','Schemas','Tables','Tools','.vscode')

function Fail([string]$Message) {
    Write-Error ('[H8MOD_REVIEW_MANIFEST] ' + $Message)
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

function Assert-StandardReviewOutput([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail 'Output path is required.'
    }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        Fail 'Output path must be relative to the starter kit root.'
    }

    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or
        -not $normalized.Equals('Reports/review_manifest.json', [System.StringComparison]::Ordinal)) {
        Fail 'Output path must be exactly Reports/review_manifest.json.'
    }
    return $normalized
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

function Test-ReviewOutputPath([string]$RelativePath) {
    return $RelativePath.StartsWith('Generated/', [System.StringComparison]::Ordinal) -or
        $RelativePath.StartsWith('Reports/', [System.StringComparison]::Ordinal)
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

$normalizedOutput = Assert-StandardReviewOutput $Output

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    Fail 'Missing Tools/validate_structure.ps1.'
}

Invoke-RequiredTool { & $validator -Root $rootFull } 'starter validation'

$rootPrefix = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$files = New-Object 'System.Collections.Generic.List[object]'
$reviewRelativePaths = New-Object 'System.Collections.Generic.List[string]'
$totalBytes = [long]0

try {
    $sourceFiles = Get-H8SafeSourceFiles $rootFull -ExcludeGeneratedOrTransient
} catch {
    Fail $_.Exception.Message
}

foreach ($sourceFile in $sourceFiles) {
    try {
        Assert-NoFilesystemLinks $sourceFile
    } catch {
        Fail $_.Exception.Message
    }

    $fullPath = [System.IO.Path]::GetFullPath($sourceFile.FullName)
    $relative = $fullPath.Substring($rootPrefix.Length).Replace('\','/')
    if (Test-ReservedTopLevelCaseVariant $relative) {
        Fail ('Reserved starter top-level folder casing mismatch in review source: ' + $relative)
    }
    if (Test-ReviewOutputPath $relative -or (Test-H8GeneratedOrTransientPath $relative)) {
        continue
    }

    if ($files.Count -ge $MaxReviewFiles) {
        Fail ('Review manifest source file limit exceeded: ' + $MaxReviewFiles)
    }
    if ($sourceFile.Length -gt $MaxReviewFileBytes) {
        Fail ('Review file exceeds max bytes: ' + $relative)
    }
    $totalBytes += [long]$sourceFile.Length
    if ($totalBytes -gt $MaxReviewTotalBytes) {
        Fail ('Review manifest total byte limit exceeded: ' + $MaxReviewTotalBytes)
    }

    [void]$reviewRelativePaths.Add($relative)
    $hash = Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
    [void]$files.Add([pscustomobject][ordered]@{
        Path = $relative
        Bytes = $sourceFile.Length
        Sha256 = $hash.Hash.ToLowerInvariant()
    })
}

try {
    Assert-NoCaseFoldDuplicates $reviewRelativePaths.ToArray()
} catch {
    Fail $_.Exception.Message
}

$orderedFiles = @($files | Sort-Object -Property Path)
$authoring = Read-JsonFile (Join-StarterPath $rootFull 'mod.h8manifest.json') 'mod.h8manifest.json' $MaxManifestJsonBytes
$runtime = Read-JsonFile (Join-StarterPath $rootFull 'mod.json') 'mod.json' $MaxManifestJsonBytes
$manifest = [pscustomobject][ordered]@{
    Schema = 'hecton8.external_review_manifest.v1'
    Runtime = 'envelope-only'
    RootId = [string]$runtime.Id
    Identity = [pscustomobject][ordered]@{
        Id = [string]$runtime.Id
        DisplayName = [string]$authoring.DisplayName
        Author = [string]$runtime.Author
        Version = [string]$runtime.Version
        RequiredAPIVersion = [int]$runtime.RequiredAPIVersion
        ModPriority = [int]$runtime.ModPriority
    }
    FileCount = $orderedFiles.Count
    TotalBytes = $totalBytes
    Limits = [pscustomobject][ordered]@{
        MaxFiles = $MaxReviewFiles
        MaxFileBytes = $MaxReviewFileBytes
        MaxTotalBytes = $MaxReviewTotalBytes
    }
    Files = $orderedFiles
}

$outputPath = Join-StarterPath $rootFull $normalizedOutput
$outputDirectory = Split-Path -Parent $outputPath
if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $outputDirectory)
}

$json = $manifest | ConvertTo-Json -Depth 8
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($outputPath, $json + [System.Environment]::NewLine, $utf8NoBom)

Write-Host ('PASS HECTON-8 review manifest: ' + $Output)
