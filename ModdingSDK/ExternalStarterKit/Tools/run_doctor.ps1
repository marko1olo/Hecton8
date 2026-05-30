param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxDoctorSourceFiles = 256
$MaxDoctorZipEntries = 300
$MaxDoctorZipEntryBytes = 4194304
$MaxDoctorManifestBytes = 65536
$MaxDoctorGraphBytes = 262144
$MaxDoctorAssetManifestBytes = 262144
$MaxDoctorSettingsTableBytes = 262144
$MaxDoctorLocaleBytes = 2097152
$MaxDoctorReviewManifestBytes = 1048576
$ReservedTopLevelFolders = @('Content','Docs','Generated','Graphs','Locales','Reference','Reports','Schemas','Tables','Tools','.vscode')

function Fail([string]$Message) {
    Write-Error ('[H8MOD_DOCTOR] ' + $Message)
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

function Add-UniqueText($List, [string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return }
    if (-not $List.Contains($Text)) {
        [void]$List.Add($Text)
    }
}

function Read-JsonFile([string]$Path, [string]$Label, $Issues, [long]$MaxBytes = 0) {
    try {
        return Read-H8JsonFileCapped $Path $Label $MaxBytes
    } catch {
        Add-UniqueText $Issues $_.Exception.Message
        return $null
    }
}

function Get-ArrayCount([object]$Value) {
    if ($null -eq $Value) { return 0 }
    return @($Value).Count
}

function Get-ObjectPropertyCount([object]$Value) {
    if ($null -eq $Value) { return 0 }
    return @($Value.PSObject.Properties).Count
}

function Get-NumericLong([object]$Value) {
    if ($null -eq $Value) { return [long]0 }
    $parsed = [long]0
    if ([long]::TryParse(([string]$Value), [ref]$parsed)) {
        return $parsed
    }
    return [long]0
}

function Test-Sha256Hex([string]$Hash) {
    if ([string]::IsNullOrWhiteSpace($Hash) -or $Hash.Length -ne 64) { return $false }

    for ($i = 0; $i -lt $Hash.Length; $i++) {
        $ch = $Hash[$i]
        $isDigit = $ch -ge '0' -and $ch -le '9'
        $isLowerHex = $ch -ge 'a' -and $ch -le 'f'
        if (-not ($isDigit -or $isLowerHex)) {
            return $false
        }
    }

    return $true
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

function Test-SafeRelativePath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) { return $false }
    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../')) { return $false }
    if ($normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) { return $false }
    if ($normalized.StartsWith('Reports/', [System.StringComparison]::Ordinal)) { return $false }
    if (Test-ReservedTopLevelCaseVariant $normalized) { return $false }
    return $true
}

function Test-SafeZipEntryPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { return $false }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) { return $false }

    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.StartsWith('/') -or $normalized.Contains(':')) { return $false }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../')) { return $false }
    if ($normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) { return $false }
    if ($normalized.StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ($normalized.StartsWith('Reports/', [System.StringComparison]::Ordinal) -and
        -not $normalized.Equals('Reports/review_manifest.json', [System.StringComparison]::Ordinal)) {
        return $false
    }
    if ($normalized.StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase) -and
        -not $normalized.StartsWith('Reports/', [System.StringComparison]::Ordinal)) {
        return $false
    }

    foreach ($segment in ($normalized -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            return $false
        }
    }

    return $true
}

function Normalize-ZipEntryPath([string]$RelativePath) {
    $normalized = $RelativePath.Replace('\','/')
    while ($normalized.StartsWith('/')) {
        $normalized = $normalized.Substring(1)
    }
    return $normalized
}

function Get-FileHashProof([string]$Path) {
    $file = Get-Item -LiteralPath $Path
    return [pscustomobject][ordered]@{
        Bytes = [long]$file.Length
        Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-ZipEntryHashProof($Entry, $Issues, [string]$Label) {
    if ([long]$Entry.Length -gt $MaxDoctorZipEntryBytes) {
        Add-UniqueText $Issues ('Submission zip entry exceeds doctor byte cap: ' + $Label)
        return $null
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = $Entry.Open()
        $hash = $sha.ComputeHash($stream)
        return [pscustomobject][ordered]@{
            Bytes = [long]$Entry.Length
            Sha256 = ([System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant())
        }
    } catch {
        Add-UniqueText $Issues ('Submission zip entry hash failed for ' + $Label + ': ' + $_.Exception.Message)
        return $null
    } finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
        $sha.Dispose()
    }
}

function Get-StarterSourceFiles([string]$RootPath, $Issues) {
    $rootPrefix = $RootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $files = New-Object 'System.Collections.Generic.List[object]'
    $totalBytes = [long]0

    Get-ChildItem -LiteralPath $RootPath -Recurse -File | ForEach-Object {
        $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
        if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            Add-UniqueText $Issues ('Source enumeration escaped starter root: ' + $fullPath)
            return
        }

        $relative = $fullPath.Substring($rootPrefix.Length).Replace('\','/')
        if (Test-ReservedTopLevelCaseVariant $relative) {
            Add-UniqueText $Issues ('Reserved starter top-level folder casing mismatch in source tree: ' + $relative)
            return
        }

        if (Test-ReviewOutputPath $relative) {
            return
        }

        if ($files.Count -ge $MaxDoctorSourceFiles) {
            Add-UniqueText $Issues ('Doctor source file limit exceeded: ' + $MaxDoctorSourceFiles)
            return
        }

        $totalBytes += [long]$_.Length
        [void]$files.Add([pscustomobject][ordered]@{
            Path = $relative
            FullPath = $fullPath
            Bytes = [long]$_.Length
        })
    }

    return [pscustomobject][ordered]@{
        Files = @($files | Sort-Object -Property Path)
        Count = $files.Count
        TotalBytes = $totalBytes
    }
}

function Test-ReviewFresh([string]$RootPath, [object]$Review, [object]$SourceSet, $Issues) {
    $changed = New-Object 'System.Collections.Generic.List[string]'
    $missing = New-Object 'System.Collections.Generic.List[string]'
    $unreviewed = New-Object 'System.Collections.Generic.List[string]'
    $reviewPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::Ordinal)
    $reviewCaseFoldPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $duplicateReviewPathCount = 0
    $invalidReviewRecordCount = 0

    if ($null -eq $Review) {
        return [pscustomobject][ordered]@{
            Status = 'missing'
            Fresh = $false
            ChangedCount = 0
            MissingCount = 0
            UnreviewedCount = $SourceSet.Count
        }
    }

    if ([string]$Review.Schema -ne 'hecton8.external_review_manifest.v1') {
        Add-UniqueText $Issues 'Review manifest schema mismatch.'
    }
    if ([string]$Review.Runtime -ne 'envelope-only') {
        Add-UniqueText $Issues 'Review manifest runtime must be envelope-only.'
    }

    foreach ($file in @($Review.Files)) {
        $relative = Normalize-ZipEntryPath ([string]$file.Path)
        if (-not (Test-SafeRelativePath $relative)) {
            Add-UniqueText $Issues ('Review manifest has unsafe source path: ' + $relative)
            $invalidReviewRecordCount++
            continue
        }

        $expectedBytes = Get-NumericLong $file.Bytes
        $expectedHash = [string]$file.Sha256
        if ($expectedBytes -lt 0 -or $expectedBytes -gt $MaxDoctorZipEntryBytes -or -not (Test-Sha256Hex $expectedHash)) {
            Add-UniqueText $Issues ('Review manifest has invalid file proof: ' + $relative)
            $invalidReviewRecordCount++
            continue
        }

        if ($reviewPaths.ContainsKey($relative) -or $reviewCaseFoldPaths.ContainsKey($relative)) {
            Add-UniqueText $Issues ('Review manifest has duplicate or case-fold duplicate source path: ' + $relative)
            $duplicateReviewPathCount++
            continue
        }

        $reviewPaths[$relative] = $true
        $reviewCaseFoldPaths[$relative] = $true
        $sourcePath = Join-StarterPath $RootPath $relative
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            [void]$missing.Add($relative)
            continue
        }

        $actualFile = Get-Item -LiteralPath $sourcePath
        if ([long]$actualFile.Length -ne $expectedBytes) {
            [void]$changed.Add($relative)
            continue
        }

        $actualHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $expectedHash.ToLowerInvariant()) {
            [void]$changed.Add($relative)
        }
    }

    foreach ($sourceFile in @($SourceSet.Files)) {
        if (-not $reviewPaths.ContainsKey([string]$sourceFile.Path)) {
            [void]$unreviewed.Add([string]$sourceFile.Path)
        }
    }

    if ($missing.Count -gt 0) {
        $missingPreview = @($missing | Select-Object -First 5) -join ', '
        Add-UniqueText $Issues ('Review manifest references missing source files: ' + $missingPreview)
    }
    if ($changed.Count -gt 0) {
        $changedPreview = @($changed | Select-Object -First 5) -join ', '
        Add-UniqueText $Issues ('Review manifest is stale for changed source files: ' + $changedPreview)
    }
    if ($unreviewed.Count -gt 0) {
        $unreviewedPreview = @($unreviewed | Select-Object -First 5) -join ', '
        Add-UniqueText $Issues ('Review manifest is missing current source files: ' + $unreviewedPreview)
    }

    $fresh = ($missing.Count -eq 0 -and $changed.Count -eq 0 -and $unreviewed.Count -eq 0 -and $duplicateReviewPathCount -eq 0 -and $invalidReviewRecordCount -eq 0 -and @($Issues | Where-Object { $_ -like 'Review manifest*' }).Count -eq 0)
    $status = if ($fresh) { 'fresh' } else { 'stale' }

    return [pscustomobject][ordered]@{
        Status = $status
        Fresh = $fresh
        ChangedCount = $changed.Count
        MissingCount = $missing.Count
        UnreviewedCount = $unreviewed.Count
        DuplicateReviewPathCount = $duplicateReviewPathCount
        InvalidReviewRecordCount = $invalidReviewRecordCount
    }
}

function Test-SubmissionZipIntegrity([string]$ZipPath, [string]$ReviewPath, [object]$Review, $Issues) {
    $zipEntries = [System.Collections.Generic.Dictionary[string,object]]::new([System.StringComparer]::Ordinal)
    $expectedEntries = [System.Collections.Generic.Dictionary[string,object]]::new([System.StringComparer]::Ordinal)
    $zipCaseFoldPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $expectedCaseFoldPaths = [System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $extra = New-Object 'System.Collections.Generic.List[string]'
    $missing = New-Object 'System.Collections.Generic.List[string]'
    $changed = New-Object 'System.Collections.Generic.List[string]'
    $unsafe = New-Object 'System.Collections.Generic.List[string]'
    $duplicates = New-Object 'System.Collections.Generic.List[string]'
    $duplicateReviewPathCount = 0
    $invalidReviewRecordCount = 0
    $entryCount = 0
    $checkedCount = 0
    $inspectionFailed = $false

    if ($null -eq $Review) {
        return [pscustomobject][ordered]@{
            Status = 'missing_review'
            Verified = $false
            ZipEntryCount = 0
            CheckedEntryCount = 0
            ExtraEntryCount = 0
            MissingEntryCount = 0
            ChangedEntryCount = 0
            UnsafeEntryCount = 0
            DuplicateEntryCount = 0
            DuplicateReviewPathCount = 0
            InvalidReviewRecordCount = 0
        }
    }

    foreach ($file in @($Review.Files)) {
        $relative = Normalize-ZipEntryPath ([string]$file.Path)
        if (-not (Test-SafeRelativePath $relative)) {
            $invalidReviewRecordCount++
            Add-UniqueText $Issues ('Review manifest has unsafe source path for submission zip: ' + $relative)
            continue
        }

        $expectedBytes = Get-NumericLong $file.Bytes
        $expectedHash = [string]$file.Sha256
        if ($expectedBytes -lt 0 -or $expectedBytes -gt $MaxDoctorZipEntryBytes -or -not (Test-Sha256Hex $expectedHash)) {
            $invalidReviewRecordCount++
            Add-UniqueText $Issues ('Review manifest has invalid file proof for submission zip: ' + $relative)
            continue
        }

        if ($expectedEntries.ContainsKey($relative) -or $expectedCaseFoldPaths.ContainsKey($relative)) {
            $duplicateReviewPathCount++
            Add-UniqueText $Issues ('Review manifest has duplicate or case-fold duplicate submission path: ' + $relative)
            continue
        }

        $expectedEntries[$relative] = [pscustomobject][ordered]@{
            Path = $relative
            Bytes = $expectedBytes
            Sha256 = $expectedHash.ToLowerInvariant()
        }
        $expectedCaseFoldPaths[$relative] = $true
    }

    if (Test-Path -LiteralPath $ReviewPath -PathType Leaf) {
        $reviewProof = Get-FileHashProof $ReviewPath
        $expectedEntries['Reports/review_manifest.json'] = [pscustomobject][ordered]@{
            Path = 'Reports/review_manifest.json'
            Bytes = $reviewProof.Bytes
            Sha256 = $reviewProof.Sha256
        }
        $expectedCaseFoldPaths['Reports/review_manifest.json'] = $true
    }

    try {
        Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
    } catch {
        Add-UniqueText $Issues ('Submission zip inspection unavailable: ' + $_.Exception.Message)
        return [pscustomobject][ordered]@{
            Status = 'unavailable'
            Verified = $false
            ZipEntryCount = 0
            CheckedEntryCount = 0
            ExtraEntryCount = 0
            MissingEntryCount = 0
            ChangedEntryCount = 0
            UnsafeEntryCount = 0
            DuplicateEntryCount = 0
            DuplicateReviewPathCount = $duplicateReviewPathCount
            InvalidReviewRecordCount = $invalidReviewRecordCount
        }
    }

    $zip = $null
    try {
        $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        foreach ($entry in @($zip.Entries)) {
            if ([string]::IsNullOrWhiteSpace([string]$entry.FullName) -or [string]$entry.FullName -like '*/') {
                continue
            }

            $entryCount++
            if ($entryCount -gt $MaxDoctorZipEntries) {
                Add-UniqueText $Issues ('Submission zip entry limit exceeded: ' + $MaxDoctorZipEntries)
                continue
            }

            $relative = Normalize-ZipEntryPath ([string]$entry.FullName)
            if (-not (Test-SafeZipEntryPath $relative)) {
                [void]$unsafe.Add($relative)
                continue
            }

            if ($zipEntries.ContainsKey($relative) -or $zipCaseFoldPaths.ContainsKey($relative)) {
                [void]$duplicates.Add($relative)
                continue
            }

            $zipEntries[$relative] = $entry
            $zipCaseFoldPaths[$relative] = $true
        }

        foreach ($relative in @($zipEntries.Keys)) {
            if (-not $expectedEntries.ContainsKey($relative)) {
                [void]$extra.Add([string]$zipEntries[$relative].FullName)
            }
        }

        foreach ($expected in @($expectedEntries.Values)) {
            $relative = [string]$expected.Path
            if (-not $zipEntries.ContainsKey($relative)) {
                [void]$missing.Add([string]$expected.Path)
                continue
            }

            $entry = $zipEntries[$relative]
            if ([long]$entry.Length -ne [long]$expected.Bytes) {
                [void]$changed.Add([string]$expected.Path)
                continue
            }

            $proof = Get-ZipEntryHashProof $entry $Issues ([string]$expected.Path)
            if ($null -eq $proof) {
                [void]$changed.Add([string]$expected.Path)
                continue
            }

            if ([string]$proof.Sha256 -ne [string]$expected.Sha256) {
                [void]$changed.Add([string]$expected.Path)
                continue
            }

            $checkedCount++
        }
    } catch {
        $inspectionFailed = $true
        Add-UniqueText $Issues ('Submission zip inspection failed: ' + $_.Exception.Message)
    } finally {
        if ($null -ne $zip) {
            $zip.Dispose()
        }
    }

    if ($unsafe.Count -gt 0) {
        Add-UniqueText $Issues ('Submission zip contains unsafe entry paths: ' + ((@($unsafe | Select-Object -First 5) -join ', ')))
    }
    if ($duplicates.Count -gt 0) {
        Add-UniqueText $Issues ('Submission zip contains duplicate entry paths: ' + ((@($duplicates | Select-Object -First 5) -join ', ')))
    }
    if ($extra.Count -gt 0) {
        Add-UniqueText $Issues ('Submission zip contains unreviewed entries: ' + ((@($extra | Select-Object -First 5) -join ', ')))
    }
    if ($missing.Count -gt 0) {
        Add-UniqueText $Issues ('Submission zip is missing reviewed entries: ' + ((@($missing | Select-Object -First 5) -join ', ')))
    }
    if ($changed.Count -gt 0) {
        Add-UniqueText $Issues ('Submission zip entries differ from review manifest: ' + ((@($changed | Select-Object -First 5) -join ', ')))
    }

    $verified = (-not $inspectionFailed -and $unsafe.Count -eq 0 -and $duplicates.Count -eq 0 -and $extra.Count -eq 0 -and $missing.Count -eq 0 -and $changed.Count -eq 0 -and $duplicateReviewPathCount -eq 0 -and $invalidReviewRecordCount -eq 0 -and $entryCount -le $MaxDoctorZipEntries)
    $status = if ($verified) { 'verified' } else { 'invalid' }

    return [pscustomobject][ordered]@{
        Status = $status
        Verified = $verified
        ZipEntryCount = $entryCount
        CheckedEntryCount = $checkedCount
        ExtraEntryCount = $extra.Count
        MissingEntryCount = $missing.Count
        ChangedEntryCount = $changed.Count
        UnsafeEntryCount = $unsafe.Count
        DuplicateEntryCount = $duplicates.Count
        DuplicateReviewPathCount = $duplicateReviewPathCount
        InvalidReviewRecordCount = $invalidReviewRecordCount
    }
}

function Get-SubmissionState([string]$RootPath, [string]$PackageId, [object]$Review, [object]$ReviewState, $Issues) {
    $safePackageId = if ([string]::IsNullOrWhiteSpace($PackageId)) { 'unknown' } else { $PackageId }
    $relativeZip = 'Generated/' + $safePackageId + '_submission.zip'
    $zipPath = Join-StarterPath $RootPath $relativeZip
    $reviewPath = Join-StarterPath $RootPath 'Reports/review_manifest.json'
    $status = 'missing'
    $exists = Test-Path -LiteralPath $zipPath -PathType Leaf
    $zipIntegrity = [pscustomobject][ordered]@{
        Status = 'missing'
        Verified = $false
        ZipEntryCount = 0
        CheckedEntryCount = 0
        ExtraEntryCount = 0
        MissingEntryCount = 0
        ChangedEntryCount = 0
        UnsafeEntryCount = 0
        DuplicateEntryCount = 0
        DuplicateReviewPathCount = 0
        InvalidReviewRecordCount = 0
    }

    if ($exists) {
        $status = 'present'
        $zipIntegrity = Test-SubmissionZipIntegrity $zipPath $reviewPath $Review $Issues
        if ((Test-Path -LiteralPath $reviewPath -PathType Leaf) -and
            ((Get-Item -LiteralPath $zipPath).LastWriteTimeUtc -lt (Get-Item -LiteralPath $reviewPath).LastWriteTimeUtc)) {
            $status = 'stale'
            Add-UniqueText $Issues ('Submission zip is older than Reports/review_manifest.json: ' + $relativeZip)
        }
        if (-not $zipIntegrity.Verified) {
            $status = 'invalid'
        } elseif (-not $ReviewState.Fresh) {
            $status = 'stale'
        }
    } else {
        Add-UniqueText $Issues ('Submission zip is missing: ' + $relativeZip)
    }

    return [pscustomobject][ordered]@{
        Status = $status
        Exists = $exists
        Path = $relativeZip
        IntegrityStatus = $zipIntegrity.Status
        ZipEntryCount = $zipIntegrity.ZipEntryCount
        CheckedEntryCount = $zipIntegrity.CheckedEntryCount
        ExtraEntryCount = $zipIntegrity.ExtraEntryCount
        MissingEntryCount = $zipIntegrity.MissingEntryCount
        ChangedEntryCount = $zipIntegrity.ChangedEntryCount
        UnsafeEntryCount = $zipIntegrity.UnsafeEntryCount
        DuplicateEntryCount = $zipIntegrity.DuplicateEntryCount
        DuplicateReviewPathCount = $zipIntegrity.DuplicateReviewPathCount
        InvalidReviewRecordCount = $zipIntegrity.InvalidReviewRecordCount
    }
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$issues = New-Object 'System.Collections.Generic.List[string]'
$nextActions = New-Object 'System.Collections.Generic.List[string]'

$validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'
$structureStatus = 'missing'
$structureOutputCount = 0
if (Test-Path -LiteralPath $validator -PathType Leaf) {
    try {
        $global:LASTEXITCODE = 0
        $structureOutput = & $validator -Root $rootFull -ThrowInsteadOfExit *>&1
        $structureOutputCount = @($structureOutput).Count
        $structureStatus = 'ok'
    } catch {
        $structureStatus = 'invalid'
        Add-UniqueText $issues ('Structure validation failed: ' + $_.Exception.Message)
    }
} else {
    Add-UniqueText $issues 'Missing Tools/validate_structure.ps1.'
}

$authoringPath = Join-StarterPath $rootFull 'mod.h8manifest.json'
$runtimePath = Join-StarterPath $rootFull 'mod.json'
$graphPath = Join-StarterPath $rootFull 'Graphs/main.h8graph.json'
$settingsPath = Join-StarterPath $rootFull 'Tables/settings.h8table.json'
$localePath = Join-StarterPath $rootFull 'Locales/en.h8loc.json'
$assetsPath = Join-StarterPath $rootFull 'Content/assets.h8manifest.json'
$reviewPath = Join-StarterPath $rootFull 'Reports/review_manifest.json'

$authoring = Read-JsonFile $authoringPath 'mod.h8manifest.json' $issues $MaxDoctorManifestBytes
$runtime = Read-JsonFile $runtimePath 'mod.json' $issues $MaxDoctorManifestBytes
$graph = Read-JsonFile $graphPath 'Graphs/main.h8graph.json' $issues $MaxDoctorGraphBytes
$settings = Read-JsonFile $settingsPath 'Tables/settings.h8table.json' $issues $MaxDoctorSettingsTableBytes
$locale = Read-JsonFile $localePath 'Locales/en.h8loc.json' $issues $MaxDoctorLocaleBytes
$assets = Read-JsonFile $assetsPath 'Content/assets.h8manifest.json' $issues $MaxDoctorAssetManifestBytes
$review = Read-JsonFile $reviewPath 'Reports/review_manifest.json' $issues $MaxDoctorReviewManifestBytes

$packageId = ''
if ($null -ne $runtime -and -not [string]::IsNullOrWhiteSpace([string]$runtime.Id)) {
    $packageId = [string]$runtime.Id
} elseif ($null -ne $authoring -and -not [string]::IsNullOrWhiteSpace([string]$authoring.Id)) {
    $packageId = [string]$authoring.Id
}

$assetBytes = [long]0
foreach ($assetEntry in @($assets.Assets)) {
    $assetBytes += Get-NumericLong $assetEntry.Bytes
}

$sourceSet = Get-StarterSourceFiles $rootFull $issues
$reviewState = Test-ReviewFresh $rootFull $review $sourceSet $issues
$submissionState = Get-SubmissionState $rootFull $packageId $review $reviewState $issues

if ($structureStatus -ne 'ok') {
    Add-UniqueText $nextActions 'Run h8mod.ps1 -Action validate and fix the reported structure issue.'
}
if (-not $reviewState.Fresh) {
    Add-UniqueText $nextActions 'Run h8mod.ps1 -Action prepare to rebuild Reports/review_manifest.json.'
}
if ($submissionState.Status -ne 'present') {
    Add-UniqueText $nextActions 'Run h8mod.ps1 -Action submission to rebuild the handoff zip.'
}
if ($nextActions.Count -eq 0) {
    Add-UniqueText $nextActions 'Ready for envelope-only review handoff. Runtime activation remains disabled by contract.'
}

$status = 'ready'
if ($structureStatus -eq 'invalid' -or $structureStatus -eq 'missing') {
    $status = 'invalid'
} elseif ($issues.Count -gt 0) {
    $status = 'needs_review'
}

$result = [pscustomobject][ordered]@{
    Schema = 'hecton8.starter_doctor.v1'
    Runtime = 'envelope-only'
    Status = $status
    Root = $rootFull
    Identity = [pscustomobject][ordered]@{
        Id = $packageId
        DisplayName = if ($null -eq $authoring) { '' } else { [string]$authoring.DisplayName }
        Author = if ($null -eq $runtime) { '' } else { [string]$runtime.Author }
        Version = if ($null -eq $runtime) { '' } else { [string]$runtime.Version }
        RequiredAPIVersion = if ($null -eq $runtime) { 0 } else { [int]$runtime.RequiredAPIVersion }
    }
    Counts = [pscustomobject][ordered]@{
        Dependencies = if ($null -eq $runtime) { 0 } else { Get-ArrayCount $runtime.Dependencies }
        GraphNodes = if ($null -eq $graph) { 0 } else { Get-ArrayCount $graph.Nodes }
        SettingsRows = if ($null -eq $settings) { 0 } else { Get-ArrayCount $settings.Rows }
        LocaleStrings = if ($null -eq $locale) { 0 } else { Get-ObjectPropertyCount $locale.Strings }
        AssetEntries = if ($null -eq $assets) { 0 } else { Get-ArrayCount $assets.Assets }
        AssetDeclaredBytes = $assetBytes
        SourceFiles = $sourceSet.Count
        SourceBytes = $sourceSet.TotalBytes
    }
    Structure = [pscustomobject][ordered]@{
        Status = $structureStatus
        ValidatorOutputLines = $structureOutputCount
    }
    Review = $reviewState
    Submission = $submissionState
    Issues = @($issues)
    NextActions = @($nextActions)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
} else {
    Write-Host ('HECTON-8 starter doctor: ' + $status)
    Write-Host ('Runtime boundary: ' + $result.Runtime)
    Write-Host ('Package: ' + $result.Identity.Id + ' ' + $result.Identity.Version)
    Write-Host ('Structure: ' + $result.Structure.Status)
    Write-Host ('Review: ' + $result.Review.Status + ' changed=' + $result.Review.ChangedCount + ' missing=' + $result.Review.MissingCount + ' unreviewed=' + $result.Review.UnreviewedCount)
    Write-Host ('Submission: ' + $result.Submission.Status + ' ' + $result.Submission.Path + ' integrity=' + $result.Submission.IntegrityStatus + ' checked=' + $result.Submission.CheckedEntryCount + '/' + $result.Submission.ZipEntryCount)
    Write-Host ('Counts: dependencies=' + $result.Counts.Dependencies + ' graphNodes=' + $result.Counts.GraphNodes + ' settings=' + $result.Counts.SettingsRows + ' locale=' + $result.Counts.LocaleStrings + ' assets=' + $result.Counts.AssetEntries + ' sourceFiles=' + $result.Counts.SourceFiles)
    if ($issues.Count -gt 0) {
        Write-Host 'Issues:'
        foreach ($issue in @($issues)) {
            Write-Host ('- ' + $issue)
        }
    }
    Write-Host 'Next actions:'
    foreach ($action in @($nextActions)) {
        Write-Host ('- ' + $action)
    }
}

if ($status -eq 'invalid') {
    exit 1
}
if ($status -eq 'needs_review') {
    exit 2
}
