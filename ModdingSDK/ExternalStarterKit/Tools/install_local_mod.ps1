param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$ProjectRoot = '',
    [string]$ModsRoot = '',
    [string]$ReviewOutput = 'Reports/review_manifest.json',
    [switch]$Replace,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_INSTALL_LOCAL] ' + $Message)
    exit 1
}

function Join-StarterPath {
    param(
        [string]$BasePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Segments
    )

    $current = $BasePath
    foreach ($segment in $Segments) {
        foreach ($part in ($segment.Replace('\','/') -split '/')) {
            if (-not [string]::IsNullOrWhiteSpace($part)) {
                $current = Join-Path $current $part
            }
        }
    }
    return $current
}

function Resolve-FullPath([string]$Path, [string]$BasePath) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Normalize-PathForCompare([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-UnderPath([string]$RootPath, [string]$CandidatePath, [string]$Label) {
    $rootFull = Normalize-PathForCompare $RootPath
    $candidateFull = Normalize-PathForCompare $CandidatePath
    if ($candidateFull -eq $rootFull) {
        return
    }

    $prefix = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    $altPrefix = $rootFull + [System.IO.Path]::AltDirectorySeparatorChar
    if (-not ($candidateFull.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -or $candidateFull.StartsWith($altPrefix, [System.StringComparison]::OrdinalIgnoreCase))) {
        Fail ($Label + ' escapes Mods root: ' + $candidateFull)
    }
}

function Validate-ModId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        switch ($segment) {
            'con' { Fail ($Label + ' contains a reserved filesystem device segment.') }
            'prn' { Fail ($Label + ' contains a reserved filesystem device segment.') }
            'aux' { Fail ($Label + ' contains a reserved filesystem device segment.') }
            'nul' { Fail ($Label + ' contains a reserved filesystem device segment.') }
        }
        if (($segment.Length -eq 4) -and (($segment.StartsWith('com')) -or ($segment.StartsWith('lpt'))) -and ($segment[3] -ge '1') -and ($segment[3] -le '9')) {
            Fail ($Label + ' contains a reserved filesystem device segment.')
        }
    }
    return $trimmed
}

function Resolve-Tool([string]$RelativePath) {
    $tool = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        Fail ('Missing starter tool: ' + $RelativePath)
    }
    return $tool
}

function Complete-Tool([string]$Step) {
    if (-not $?) {
        Fail ($Step + ' failed.')
    }
    if ($global:LASTEXITCODE -ne 0) {
        exit $global:LASTEXITCODE
    }
}

function Invoke-Tool([scriptblock]$Invocation, [string]$Step) {
    $global:LASTEXITCODE = 0
    if ($Json) {
        & $Invocation *> $null
    } else {
        & $Invocation | Out-Host
    }
    Complete-Tool $Step
}

function Resolve-ReviewPath() {
    if ([System.IO.Path]::IsPathRooted($ReviewOutput)) {
        return [System.IO.Path]::GetFullPath($ReviewOutput)
    }
    return [System.IO.Path]::GetFullPath((Join-StarterPath $Root $ReviewOutput))
}

function Resolve-ProjectRootPath() {
    if (-not [string]::IsNullOrWhiteSpace($ProjectRoot)) {
        $full = Resolve-FullPath $ProjectRoot $Root
        if (-not (Test-Path -LiteralPath (Join-StarterPath $full 'Assets/_Project') -PathType Container)) {
            Fail 'ProjectRoot must contain Assets/_Project. For a built game folder pass -ModsRoot directly.'
        }
        return $full
    }

    $cursor = Get-Item -LiteralPath $Root
    while ($null -ne $cursor) {
        if (Test-Path -LiteralPath (Join-StarterPath $cursor.FullName 'Assets/_Project') -PathType Container) {
            return $cursor.FullName
        }
        $cursor = $cursor.Parent
    }

    return ''
}

function Resolve-ModsRootPath() {
    if (-not [string]::IsNullOrWhiteSpace($ModsRoot)) {
        return Resolve-FullPath $ModsRoot $Root
    }

    $projectRootFull = Resolve-ProjectRootPath
    if ([string]::IsNullOrWhiteSpace($projectRootFull)) {
        Fail 'Could not find a HECTON-8 project root above the starter kit. Pass -ProjectRoot <project root> or -ModsRoot <Mods folder>.'
    }

    return Join-StarterPath $projectRootFull 'Mods'
}

function Test-SafeRelativePath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }
    $normalized = $RelativePath.Replace('\','/').Trim()
    if ($normalized -ne $RelativePath.Replace('\','/')) { return $false }
    if ([System.IO.Path]::IsPathRooted($normalized)) { return $false }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) { return $false }
    if ($normalized.StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ($normalized.StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    return $true
}

function Assert-FileMatchesReview([string]$Path, [object]$Entry) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail ('Review manifest references missing file: ' + [string]$Entry.Path)
    }

    $fileInfo = Get-Item -LiteralPath $Path
    if ([long]$fileInfo.Length -ne [long]$Entry.Bytes) {
        Fail ('Review manifest byte count mismatch: ' + [string]$Entry.Path)
    }

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne ([string]$Entry.Sha256).ToLowerInvariant()) {
        Fail ('Review manifest SHA-256 mismatch: ' + [string]$Entry.Path)
    }
}

function Copy-ReviewedFiles([object[]]$Entries, [string]$TargetRoot) {
    foreach ($entry in $Entries) {
        $relativePath = [string]$entry.Path
        if (-not (Test-SafeRelativePath $relativePath)) {
            Fail ('Review manifest contains unsafe source path: ' + $relativePath)
        }

        $sourcePath = Join-StarterPath $Root $relativePath
        Assert-FileMatchesReview $sourcePath $entry
        $targetPath = Join-StarterPath $TargetRoot $relativePath
        $targetDirectory = Split-Path -Parent $targetPath
        if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
            [void](New-Item -ItemType Directory -Path $targetDirectory)
        }

        Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
    }
}

function Copy-ReviewManifest([string]$ReviewPath, [string]$TargetRoot) {
    $targetPath = Join-StarterPath $TargetRoot 'Reports/review_manifest.json'
    $targetDirectory = Split-Path -Parent $targetPath
    if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $targetDirectory)
    }

    Copy-Item -LiteralPath $ReviewPath -Destination $targetPath -Force
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$prepareTool = Resolve-Tool 'Tools/prepare_mod.ps1'
Invoke-Tool { & $prepareTool -Root $Root } 'prepare/review manifest'

$reviewPath = Resolve-ReviewPath
if (-not (Test-Path -LiteralPath $reviewPath -PathType Leaf)) {
    Fail ('Missing review manifest: ' + $ReviewOutput)
}

$review = Get-Content -Raw -LiteralPath $reviewPath | ConvertFrom-Json
if ([string]$review.Schema -ne 'hecton8.external_review_manifest.v1') {
    Fail 'Review manifest schema must be hecton8.external_review_manifest.v1.'
}
if ([string]$review.Runtime -ne 'envelope-only') {
    Fail 'Review manifest Runtime must be envelope-only.'
}

$packageId = Validate-ModId ([string]$review.Identity.Id) 'review Identity.Id'
if ($packageId -ne (Validate-ModId ([string]$review.RootId) 'review RootId')) {
    Fail 'Review RootId must match Identity.Id.'
}

$modsRootFull = Resolve-ModsRootPath
if (-not (Test-Path -LiteralPath $modsRootFull -PathType Container)) {
    [void](New-Item -ItemType Directory -Path $modsRootFull)
}

$installToken = [System.Guid]::NewGuid().ToString('N')
$destination = Join-StarterPath $modsRootFull $packageId
$staging = Join-StarterPath $modsRootFull ('.' + $packageId + '.install_tmp_' + $installToken)
$backup = Join-StarterPath $modsRootFull ('.' + $packageId + '.previous_' + $installToken)
Assert-UnderPath $modsRootFull $destination 'Destination'
Assert-UnderPath $modsRootFull $staging 'Staging'
Assert-UnderPath $modsRootFull $backup 'Backup'

if ((Test-Path -LiteralPath $destination) -and -not $Replace) {
    Fail ('Destination already exists: ' + $destination + '. Pass -Replace to update the local discovery copy.')
}

$backupCreated = $false
try {
    [void](New-Item -ItemType Directory -Path $staging)
    Copy-ReviewedFiles @($review.Files) $staging
    Copy-ReviewManifest $reviewPath $staging

    if (Test-Path -LiteralPath $destination) {
        Move-Item -LiteralPath $destination -Destination $backup
        $backupCreated = $true
    }

    Move-Item -LiteralPath $staging -Destination $destination

    if ($backupCreated -and (Test-Path -LiteralPath $backup)) {
        Assert-UnderPath $modsRootFull $backup 'Backup cleanup'
        Remove-Item -LiteralPath $backup -Recurse -Force
    }
} catch {
    if ($backupCreated -and (Test-Path -LiteralPath $backup) -and -not (Test-Path -LiteralPath $destination)) {
        Move-Item -LiteralPath $backup -Destination $destination
    }
    if (Test-Path -LiteralPath $staging) {
        Assert-UnderPath $modsRootFull $staging 'Staging cleanup'
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    Fail ('Local install failed: ' + $_.Exception.Message)
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.local_install.v1'
        Runtime = 'envelope-only'
        Id = $packageId
        ModsRoot = $modsRootFull
        Destination = $destination
        FileCount = [int]$review.FileCount
        TotalBytes = [long]$review.TotalBytes
        ReviewManifest = 'Reports/review_manifest.json'
        DiscoveryOnly = $true
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output ('PASS HECTON-8 local discovery install: ' + $destination)
Write-Output ('Id: ' + $packageId)
Write-Output ('Files: ' + [string]$review.FileCount)
Write-Output ('Bytes: ' + [string]$review.TotalBytes)
Write-Output 'Runtime: envelope-only discovery copy. Managed entry and loose content ingestion remain disabled until an engine-owned approval/bake route exists.'
