param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Output = 'Reports/review_manifest.json'
)

$ErrorActionPreference = 'Stop'

$MaxReviewFiles = 256
$MaxReviewFileBytes = 4194304
$MaxReviewTotalBytes = 33554432

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

if ([System.IO.Path]::IsPathRooted($Output)) {
    Fail 'Output path must be relative to the starter kit root.'
}

$normalizedOutput = $Output.Replace('\','/')
if ($normalizedOutput.StartsWith('../') -or $normalizedOutput.Contains('/../') -or -not $normalizedOutput.StartsWith('Reports/')) {
    Fail 'Output path must stay under Reports/.'
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    Fail 'Missing Tools/validate_structure.ps1.'
}

& $validator -Root $rootFull | Out-Host

$rootPrefix = $rootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$excludePrefixes = @('Generated/','Reports/')
$files = New-Object 'System.Collections.Generic.List[object]'
$totalBytes = [long]0

Get-ChildItem -LiteralPath $rootFull -Recurse -File | ForEach-Object {
    $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
    $relative = $fullPath.Substring($rootPrefix.Length).Replace('\','/')
    $excluded = $false
    foreach ($prefix in $excludePrefixes) {
        if ($relative.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $excluded = $true
            break
        }
    }

    if (-not $excluded) {
        if ($files.Count -ge $MaxReviewFiles) {
            Fail ('Review manifest source file limit exceeded: ' + $MaxReviewFiles)
        }
        if ($_.Length -gt $MaxReviewFileBytes) {
            Fail ('Review file exceeds max bytes: ' + $relative)
        }
        $totalBytes += [long]$_.Length
        if ($totalBytes -gt $MaxReviewTotalBytes) {
            Fail ('Review manifest total byte limit exceeded: ' + $MaxReviewTotalBytes)
        }
        $hash = Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        [void]$files.Add([pscustomobject][ordered]@{
            Path = $relative
            Bytes = $_.Length
            Sha256 = $hash.Hash.ToLowerInvariant()
        })
    }
}

$orderedFiles = @($files | Sort-Object -Property Path)
$authoring = Get-Content -Raw -LiteralPath (Join-StarterPath $rootFull 'mod.h8manifest.json') | ConvertFrom-Json
$runtime = Get-Content -Raw -LiteralPath (Join-StarterPath $rootFull 'mod.json') | ConvertFrom-Json
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
