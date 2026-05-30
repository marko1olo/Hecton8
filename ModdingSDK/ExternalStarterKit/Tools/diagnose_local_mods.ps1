param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$ProjectRoot = '',
    [string]$ModsRoot = '',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxManifestBytes = 32768
$MaxReviewManifestBytes = 1048576
$MaxDiscoveredManifestCount = 64
$MaxTopLevelManagedAssemblyCount = 32
$MaxTopLevelBundleCount = 4
$MaxLocalizationFileCount = 16
$CurrentApiVersion = 2
$ReservedTopLevelFolders = @(
    'Content',
    'Docs',
    'Generated',
    'Graphs',
    'Locales',
    'Reference',
    'Reports',
    'Schemas',
    'Tables',
    'Tools',
    '.vscode'
)

function Fail([string]$Message) {
    Write-Error ('[H8MOD_DIAGNOSE_LOCAL] ' + $Message)
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

function Resolve-ModsRootPath() {
    $rootFull = [System.IO.Path]::GetFullPath($Root)

    if (-not [string]::IsNullOrWhiteSpace($ModsRoot)) {
        return (Resolve-FullPath $ModsRoot $rootFull)
    }

    if (-not [string]::IsNullOrWhiteSpace($ProjectRoot)) {
        $project = Resolve-FullPath $ProjectRoot $rootFull
        return [System.IO.Path]::GetFullPath((Join-Path $project 'Mods'))
    }

    $cursor = [System.IO.DirectoryInfo]::new($rootFull)
    while ($null -ne $cursor) {
        if (Test-Path -LiteralPath (Join-Path $cursor.FullName 'Assets/_Project') -PathType Container) {
            return [System.IO.Path]::GetFullPath((Join-Path $cursor.FullName 'Mods'))
        }
        $cursor = $cursor.Parent
    }

    return [System.IO.Path]::GetFullPath((Join-StarterPath $rootFull '..' '..' 'Mods'))
}

function Test-Sha256Hex([string]$Value) {
    return (-not [string]::IsNullOrWhiteSpace($Value)) -and ($Value -cmatch '^[0-9a-f]{64}$')
}

function Test-ReservedTopLevelCaseVariant([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }

    $normalized = $RelativePath.Replace('\','/')
    $slash = $normalized.IndexOf('/')
    $topLevel = if ($slash -lt 0) { $normalized } else { $normalized.Substring(0, $slash) }

    foreach ($reserved in $ReservedTopLevelFolders) {
        if ($topLevel.Equals($reserved, [System.StringComparison]::OrdinalIgnoreCase) -and -not $topLevel.Equals($reserved, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Test-ReservedModIdSegment([string]$Segment) {
    if ([string]::IsNullOrWhiteSpace($Segment)) { return $false }
    switch ($Segment) {
        'con' { return $true }
        'prn' { return $true }
        'aux' { return $true }
        'nul' { return $true }
    }
    if (($Segment.Length -eq 4) -and (($Segment.StartsWith('com')) -or ($Segment.StartsWith('lpt'))) -and ($Segment[3] -ge '1') -and ($Segment[3] -le '9')) {
        return $true
    }
    return $false
}

function Test-ModId([string]$Value, [ref]$Reason) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        $Reason.Value = 'Mod ID is required.'
        return $false
    }

    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) {
        $Reason.Value = 'Mod ID must not contain leading or trailing whitespace.'
        return $false
    }

    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        $Reason.Value = "Mod ID may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits."
        return $false
    }

    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) {
            $Reason.Value = 'Mod ID contains a reserved filesystem device segment.'
            return $false
        }
    }

    $Reason.Value = ''
    return $true
}

function Test-SemVer([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $false }
    return $Value -match '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$'
}

function Test-EntryAssemblyFileName([string]$Value, [ref]$Reason) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        $Reason.Value = ''
        return $true
    }

    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) {
        $Reason.Value = 'EntryAssembly must not contain leading or trailing whitespace.'
        return $false
    }

    if ([System.IO.Path]::IsPathRooted($trimmed) -or $trimmed.Contains('\') -or $trimmed.Contains('/') -or ([System.IO.Path]::GetFileName($trimmed) -ne $trimmed)) {
        $Reason.Value = 'EntryAssembly must be a package-local DLL file name, not a path.'
        return $false
    }

    if ([System.IO.Path]::GetExtension($trimmed).ToLowerInvariant() -ne '.dll') {
        $Reason.Value = 'EntryAssembly must reference a .dll file.'
        return $false
    }

    $Reason.Value = ''
    return $true
}

function Test-SafeRelativePath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }
    $normalized = $RelativePath.Replace('\','/')
    if ([System.IO.Path]::IsPathRooted($normalized)) { return $false }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) { return $false }
    if ($normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) { return $false }
    if ($normalized.StartsWith('Reports/', [System.StringComparison]::Ordinal)) { return $false }
    if (Test-ReservedTopLevelCaseVariant $normalized) { return $false }
    return $true
}

function Get-Sha256Hex([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $hash = $sha.ComputeHash($stream)
            return ([System.BitConverter]::ToString($hash)).Replace('-', '').ToLowerInvariant()
        } finally {
            $sha.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Get-TopLevelFiles([string]$DirectoryPath, [string]$Pattern, [int]$Cap) {
    if (-not (Test-Path -LiteralPath $DirectoryPath -PathType Container)) {
        return @()
    }

    $files = @(Get-ChildItem -LiteralPath $DirectoryPath -File -Filter $Pattern | Sort-Object Name)
    if ($files.Count -le $Cap) {
        return $files
    }

    return @($files | Select-Object -First ($Cap + 1))
}

function Get-DiscoveredManifestFiles([string]$ModsRootPath, [ref]$Capped, [System.Collections.ArrayList]$DiscoveryIssues) {
    $manifestFiles = [System.Collections.ArrayList]::new()

    try {
        foreach ($manifestPath in [System.IO.Directory]::EnumerateFiles($ModsRootPath, 'mod.json', [System.IO.SearchOption]::AllDirectories)) {
            if ($manifestFiles.Count -ge $MaxDiscoveredManifestCount) {
                $Capped.Value = $true
                Add-Issue $DiscoveryIssues ('Manifest scan capped at ' + $MaxDiscoveredManifestCount + ' files to mirror ModLoader discovery caps.')
                break
            }

            [void]$manifestFiles.Add([System.IO.FileInfo]::new($manifestPath))
        }
    } catch {
        Add-Issue $DiscoveryIssues ('Recursive mod.json discovery failed: ' + $_.Exception.Message)
    }

    return @($manifestFiles)
}

function Add-Issue([System.Collections.ArrayList]$Issues, [string]$Message) {
    if (-not [string]::IsNullOrWhiteSpace($Message)) {
        [void]$Issues.Add($Message)
    }
}

function Read-JsonIssueFile([string]$Path, [string]$Label, [System.Collections.ArrayList]$Issues, [long]$MaxBytes) {
    try {
        return Read-H8JsonFileCapped $Path $Label $MaxBytes
    } catch {
        Add-Issue $Issues $_.Exception.Message
        return $null
    }
}

function Diagnose-ReviewManifest([string]$PackagePath, [string]$RuntimeId, [System.Collections.ArrayList]$Issues) {
    $reviewPath = Join-Path $PackagePath 'Reports/review_manifest.json'
    if (-not (Test-Path -LiteralPath $reviewPath -PathType Leaf)) {
        Add-Issue $Issues 'Reports/review_manifest.json missing; install-local reviewed copies should include it.'
        return 'missing'
    }

    $review = Read-JsonIssueFile $reviewPath 'Reports/review_manifest.json' $Issues $MaxReviewManifestBytes
    if ($null -eq $review) {
        return 'invalid'
    }

    $reviewStatus = 'ok'
    if ([string]$review.Schema -ne 'hecton8.external_review_manifest.v1') {
        Add-Issue $Issues 'Review manifest schema is not hecton8.external_review_manifest.v1.'
        $reviewStatus = 'invalid'
    }
    if ([string]$review.Runtime -ne 'envelope-only') {
        Add-Issue $Issues 'Review manifest Runtime is not envelope-only.'
        $reviewStatus = 'invalid'
    }

    $reviewId = [string]$review.RootId
    if ($null -ne $review.Identity -and -not [string]::IsNullOrWhiteSpace([string]$review.Identity.Id)) {
        $reviewId = [string]$review.Identity.Id
    }
    if (-not [string]::IsNullOrWhiteSpace($RuntimeId) -and $reviewId -ne $RuntimeId) {
        Add-Issue $Issues ('Review manifest id does not match mod.json Id: ' + $reviewId + ' != ' + $RuntimeId)
        $reviewStatus = 'invalid'
    }

    if ($null -eq $review.Files) {
        Add-Issue $Issues 'Review manifest has no Files array.'
        return 'invalid'
    }

    $reviewPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::Ordinal)
    $reviewCaseFoldPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in @($review.Files)) {
        $relative = ([string]$file.Path).Replace('\','/')
        if (-not (Test-SafeRelativePath $relative)) {
            Add-Issue $Issues ('Review file path is unsafe or not a source path: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }
        if ($reviewPaths.ContainsKey($relative) -or $reviewCaseFoldPaths.ContainsKey($relative)) {
            Add-Issue $Issues ('Review manifest contains duplicate or case-fold duplicate source path: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }
        [void]$reviewPaths.Add($relative, $true)
        [void]$reviewCaseFoldPaths.Add($relative, $true)

        if (-not (Test-Sha256Hex ([string]$file.Sha256))) {
            Add-Issue $Issues ('Review manifest contains invalid lowercase SHA-256 for: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }

        $fullPath = Join-StarterPath $PackagePath $relative
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Add-Issue $Issues ('Reviewed file missing from installed package: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }

        $expectedBytes = 0L
        try {
            $expectedBytes = [long]$file.Bytes
        } catch {
            Add-Issue $Issues ('Review manifest contains invalid byte count: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }
        if ($expectedBytes -lt 0) {
            Add-Issue $Issues ('Review manifest contains invalid byte count: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }

        $info = Get-Item -LiteralPath $fullPath
        if ($expectedBytes -ne [long]$info.Length) {
            Add-Issue $Issues ('Reviewed file byte mismatch: ' + $relative)
            $reviewStatus = 'invalid'
            continue
        }

        $actualSha = Get-Sha256Hex $fullPath
        if ($actualSha -cne [string]$file.Sha256) {
            Add-Issue $Issues ('Reviewed file SHA-256 mismatch: ' + $relative)
            $reviewStatus = 'invalid'
        }
    }

    return $reviewStatus
}

function Set-PackageGraphInvalid([object]$Package, [string]$DependencyStatus, [string]$Message) {
    $Package.DependencyStatus = $DependencyStatus
    $Package.Status = 'INVALID'
    $Package.Reason = 'ModLoader will skip or disable this package before activation.'
    Add-Issue ([System.Collections.ArrayList]$Package.IssueBuffer) $Message
}

function Diagnose-Package([System.IO.FileInfo]$ManifestFile, [int]$DiscoveryIndex) {
    $packageDirectory = $ManifestFile.Directory
    $issues = [System.Collections.ArrayList]::new()
    $manifestPath = $ManifestFile.FullName
    $runtimeId = ''
    $displayName = ''
    $version = ''
    $author = ''
    $requiredApi = 0
    $entryAssembly = ''
    $entryType = ''
    $modPriority = 0
    $dependencies = @()
    $manifestStatus = 'ok'

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Add-Issue $issues 'mod.json missing; ModLoader discovery requires a manifest named mod.json.'
        $manifestStatus = 'missing'
    } else {
        $manifestInfo = Get-Item -LiteralPath $manifestPath
        if ($manifestInfo.Length -le 0) {
            Add-Issue $issues 'mod.json is empty.'
            $manifestStatus = 'invalid'
        } elseif ($manifestInfo.Length -gt $MaxManifestBytes) {
            Add-Issue $issues ('mod.json exceeds ' + $MaxManifestBytes + ' bytes.')
            $manifestStatus = 'invalid'
        } else {
            $manifest = Read-JsonIssueFile $manifestPath 'mod.json' $issues $MaxManifestBytes
            if ($null -eq $manifest) {
                $manifestStatus = 'invalid'
            } else {
                $runtimeId = [string]$manifest.Id
                $displayName = [string]$manifest.Name
                $version = [string]$manifest.Version
                $author = [string]$manifest.Author
                $requiredApi = [int]$manifest.RequiredAPIVersion
                $entryAssembly = [string]$manifest.EntryAssembly
                $entryType = [string]$manifest.EntryType
                if ($null -ne $manifest.ModPriority) {
                    $modPriority = [int]$manifest.ModPriority
                }

                $idReason = ''
                if (-not (Test-ModId $runtimeId ([ref]$idReason))) {
                    Add-Issue $issues ('Invalid mod.json Id. ' + $idReason)
                    $manifestStatus = 'invalid'
                }
                if ([string]::IsNullOrWhiteSpace($displayName)) {
                    Add-Issue $issues 'mod.json Name is empty.'
                    $manifestStatus = 'invalid'
                }
                if ([string]::IsNullOrWhiteSpace($author)) {
                    Add-Issue $issues 'mod.json Author is empty.'
                    $manifestStatus = 'invalid'
                }
                if (-not (Test-SemVer $version)) {
                    Add-Issue $issues 'mod.json Version is not semantic version form MAJOR.MINOR.PATCH.'
                    $manifestStatus = 'invalid'
                }
                if ($requiredApi -le 0) {
                    Add-Issue $issues 'mod.json RequiredAPIVersion is missing or <= 0.'
                    $manifestStatus = 'invalid'
                } elseif ($requiredApi -gt $CurrentApiVersion) {
                    Add-Issue $issues ('mod.json RequiredAPIVersion exceeds engine API version ' + $CurrentApiVersion + '.')
                    $manifestStatus = 'invalid'
                }

                $entryReason = ''
                if (-not (Test-EntryAssemblyFileName $entryAssembly ([ref]$entryReason))) {
                    Add-Issue $issues $entryReason
                    $manifestStatus = 'invalid'
                }

                foreach ($dependency in @($manifest.Dependencies)) {
                    if ([string]::IsNullOrWhiteSpace([string]$dependency)) {
                        continue
                    }
                    $dependencyId = ([string]$dependency).Trim()
                    $dependencies += $dependencyId
                    $dependencyReason = ''
                    if (-not (Test-ModId $dependencyId ([ref]$dependencyReason))) {
                        Add-Issue $issues ('Invalid dependency ID ' + [string]$dependency + '. ' + $dependencyReason)
                        $manifestStatus = 'invalid'
                    }
                }
            }
        }
    }

    $dllFiles = @(Get-TopLevelFiles $PackageDirectory.FullName '*.dll' $MaxTopLevelManagedAssemblyCount)
    $bundleFiles = @(Get-TopLevelFiles $PackageDirectory.FullName '*.bundle' $MaxTopLevelBundleCount)
    $localeFiles = @(Get-TopLevelFiles $PackageDirectory.FullName 'lang_*.json' $MaxLocalizationFileCount)

    if ($dllFiles.Count -gt $MaxTopLevelManagedAssemblyCount) {
        Add-Issue $issues ('Package contains more than ' + $MaxTopLevelManagedAssemblyCount + ' top-level managed assemblies.')
        $manifestStatus = 'invalid'
    }
    if ($bundleFiles.Count -gt $MaxTopLevelBundleCount) {
        Add-Issue $issues ('Package contains more than ' + $MaxTopLevelBundleCount + ' top-level asset bundles.')
        $manifestStatus = 'invalid'
    }
    if ($localeFiles.Count -gt $MaxLocalizationFileCount) {
        Add-Issue $issues ('Package contains more than ' + $MaxLocalizationFileCount + ' top-level localization files.')
        $manifestStatus = 'invalid'
    }

    $hasManagedEntry = (-not [string]::IsNullOrWhiteSpace($entryAssembly)) -or (-not [string]::IsNullOrWhiteSpace($entryType)) -or ($dllFiles.Count -gt 0)
    $reviewStatus = Diagnose-ReviewManifest $PackageDirectory.FullName $runtimeId $issues

    $status = 'DISABLED_BY_RUNTIME_BOUNDARY'
    $reason = 'Filesystem content ingestion disabled. UGC assets must be approved by CRC and referenced by 64-byte FutureCommandEnvelope packets.'
    if ($manifestStatus -ne 'ok') {
        $status = 'INVALID'
        $reason = 'ModLoader will skip or disable this package before activation.'
    } elseif ($reviewStatus -ne 'ok') {
        $status = 'INVALID'
        $reason = 'Local reviewed install proof is missing or invalid. Re-run h8mod.ps1 -Action install-local from the starter kit before testing discovery.'
    } elseif ($hasManagedEntry) {
        $status = 'DISABLED_BY_RUNTIME_BOUNDARY'
        $reason = 'Managed mod entry disabled. UGC commands must use 64-byte FutureCommandEnvelope packets.'
    }

    [pscustomobject]@{
        Directory = $PackageDirectory.Name
        Path = $PackageDirectory.FullName
        ManifestPath = $manifestPath
        DiscoveryIndex = [int]$DiscoveryIndex
        Id = $runtimeId
        Name = $displayName
        Version = $version
        Author = $author
        RequiredAPIVersion = $requiredApi
        ModPriority = [int]$modPriority
        Dependencies = @($dependencies)
        ManifestStatus = $manifestStatus
        DependencyStatus = 'not_evaluated'
        LoadOrderIndex = -1
        ReviewStatus = $reviewStatus
        Status = $status
        Reason = $reason
        ManagedAssemblies = [int]$dllFiles.Count
        AssetBundles = [int]$bundleFiles.Count
        LocalizationFiles = [int]$localeFiles.Count
        IssueBuffer = $issues
        Issues = @($issues)
    }
}

function Resolve-DependencyGraph([object[]]$Packages) {
    $byId = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    $sortedIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $orderedPackages = [System.Collections.ArrayList]::new()
    $duplicateCount = 0
    $missingCount = 0
    $cycleCount = 0

    foreach ($package in @($Packages)) {
        if ([string]$package.ManifestStatus -ne 'ok') {
            $package.DependencyStatus = 'manifest_invalid'
            continue
        }

        $id = [string]$package.Id
        if ([string]::IsNullOrWhiteSpace($id)) {
            $package.DependencyStatus = 'manifest_invalid'
            continue
        }

        if ($byId.ContainsKey($id)) {
            $keptPackage = $byId[$id]
            Set-PackageGraphInvalid $package 'duplicate_id' ('Duplicate mod ID. Keeping ''' + [string]$keptPackage.ManifestPath + '''.')
            $duplicateCount++
            continue
        }

        $byId.Add($id, $package)
    }

    $unresolved = [System.Collections.ArrayList]::new()
    foreach ($package in @($Packages)) {
        if ([string]$package.ManifestStatus -eq 'ok' -and [string]$package.DependencyStatus -eq 'not_evaluated') {
            [void]$unresolved.Add($package)
        }
    }

    while ($unresolved.Count -gt 0) {
        $progress = $false
        $nextUnresolved = [System.Collections.ArrayList]::new()

        foreach ($package in @($unresolved)) {
            $missingDependency = ''
            $allSatisfied = $true

            foreach ($dependencyId in @($package.Dependencies)) {
                if ([string]::IsNullOrWhiteSpace($dependencyId)) {
                    continue
                }

                if (-not $byId.ContainsKey([string]$dependencyId)) {
                    $missingDependency = [string]$dependencyId
                    $allSatisfied = $false
                    break
                }

                $dependencyPackage = $byId[[string]$dependencyId]
                if ([string]$dependencyPackage.Status -eq 'INVALID') {
                    $missingDependency = [string]$dependencyId
                    $allSatisfied = $false
                    break
                }

                if (-not $sortedIds.Contains([string]$dependencyId)) {
                    $allSatisfied = $false
                    break
                }
            }

            if (-not [string]::IsNullOrWhiteSpace($missingDependency)) {
                Set-PackageGraphInvalid $package 'missing_dependency' ('Missing dependency ''' + $missingDependency + '''.')
                $missingCount++
                $progress = $true
                continue
            }

            if ($allSatisfied) {
                $package.DependencyStatus = 'ordered'
                $package.LoadOrderIndex = [int]$orderedPackages.Count
                [void]$orderedPackages.Add($package)
                [void]$sortedIds.Add([string]$package.Id)
                $progress = $true
                continue
            }

            [void]$nextUnresolved.Add($package)
        }

        if (-not $progress) {
            foreach ($package in @($nextUnresolved)) {
                Set-PackageGraphInvalid $package 'cycle_or_deadlock' 'Dependency cycle or unresolved ordering deadlock.'
                $cycleCount++
            }
            break
        }

        $unresolved = $nextUnresolved
    }

    foreach ($package in @($Packages)) {
        $package.Issues = @($package.IssueBuffer)
        $package.PSObject.Properties.Remove('IssueBuffer')
    }

    return [pscustomobject]@{
        RecursiveManifestDiscovery = $true
        OrderedCount = [int]$orderedPackages.Count
        DuplicateIdCount = [int]$duplicateCount
        MissingDependencyCount = [int]$missingCount
        CycleOrDeadlockCount = [int]$cycleCount
        LoadOrder = @($orderedPackages | ForEach-Object { [string]$_.Id })
    }
}

$modsRootPath = Resolve-ModsRootPath
$modsRootExists = Test-Path -LiteralPath $modsRootPath -PathType Container
$packages = @()
$capped = $false
$discoveryIssues = [System.Collections.ArrayList]::new()

if ($modsRootExists) {
    $manifestFiles = @(Get-DiscoveredManifestFiles $modsRootPath ([ref]$capped) $discoveryIssues)
    $index = 0
    foreach ($manifestFile in $manifestFiles) {
        $packages += Diagnose-Package $manifestFile $index
        $index++
    }
}

$dependencyGraph = Resolve-DependencyGraph $packages
$invalidCount = @($packages | Where-Object { [string]$_.Status -eq 'INVALID' }).Count
$boundaryCount = @($packages | Where-Object { [string]$_.Status -eq 'DISABLED_BY_RUNTIME_BOUNDARY' }).Count
$warningCount = @($packages | Where-Object { [string]$_.ReviewStatus -ne 'ok' }).Count
$discoverableCount = @($packages | Where-Object { [string]$_.ManifestStatus -eq 'ok' }).Count

$result = [pscustomobject]@{
    Schema = 'hecton8.local_mods_diagnosis.v1'
    Runtime = 'envelope-only'
    ModsRoot = $modsRootPath
    ModsRootExists = [bool]$modsRootExists
    LoaderCaps = [pscustomobject]@{
        MaxManifestBytes = $MaxManifestBytes
        MaxDiscoveredManifestCount = $MaxDiscoveredManifestCount
        MaxTopLevelManagedAssemblyCount = $MaxTopLevelManagedAssemblyCount
        MaxTopLevelBundleCount = $MaxTopLevelBundleCount
        MaxLocalizationFileCount = $MaxLocalizationFileCount
        CurrentAPIVersion = $CurrentApiVersion
    }
    Capped = [bool]$capped
    DiscoveryIssues = @($discoveryIssues)
    DependencyGraph = $dependencyGraph
    PackageCount = [int]$packages.Count
    InvalidCount = [int]$invalidCount
    BoundaryDisabledCount = [int]$boundaryCount
    ReviewWarningCount = [int]$warningCount
    DiscoverableCount = [int]$discoverableCount
    Packages = @($packages)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
    exit 0
}

Write-Host ('PASS HECTON-8 local Mods diagnosis: ' + $modsRootPath)
Write-Host 'Runtime: envelope-only'
Write-Host ('Mods root exists: ' + $modsRootExists)
Write-Host ('Packages: ' + $packages.Count + ' / recursive mod.json cap ' + $MaxDiscoveredManifestCount)
Write-Host ('Recursive manifest discovery: ' + $dependencyGraph.RecursiveManifestDiscovery)
Write-Host ('Dependency graph: ordered=' + $dependencyGraph.OrderedCount + ' duplicate=' + $dependencyGraph.DuplicateIdCount + ' missing=' + $dependencyGraph.MissingDependencyCount + ' cycle=' + $dependencyGraph.CycleOrDeadlockCount)
if ($capped) {
    Write-Host ('WARNING: package scan capped at ' + $MaxDiscoveredManifestCount + ' manifest files.')
}
if (-not $modsRootExists) {
    Write-Host 'Mods root is missing. No package will be discovered until a reviewed install creates Mods/<mod-id>.'
    exit 0
}
foreach ($issue in @($discoveryIssues)) {
    Write-Host ('Discovery issue: ' + $issue)
}

foreach ($package in $packages) {
    Write-Host ''
    Write-Host ('[' + $package.Status + '] ' + $package.Directory)
    Write-Host ('  Id: ' + $package.Id)
    Write-Host ('  Version: ' + $package.Version)
    Write-Host ('  Manifest: ' + $package.ManifestStatus)
    Write-Host ('  Dependency: ' + $package.DependencyStatus)
    Write-Host ('  Dependencies: ' + ($(if (@($package.Dependencies).Count -eq 0) { '<none>' } else { (@($package.Dependencies) -join ', ') })))
    if ($package.LoadOrderIndex -ge 0) {
        Write-Host ('  Load order: ' + $package.LoadOrderIndex)
    }
    Write-Host ('  Review: ' + $package.ReviewStatus)
    Write-Host ('  Files: dll=' + $package.ManagedAssemblies + ' bundle=' + $package.AssetBundles + ' lang=' + $package.LocalizationFiles)
    Write-Host ('  Reason: ' + $package.Reason)
    foreach ($issue in @($package.Issues)) {
        Write-Host ('  Issue: ' + $issue)
    }
}
