param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Snippet = 'Generated/asset_entry_snippet.json',
    [string]$Target = 'Content/assets.h8manifest.json',
    [string]$Manifest = 'mod.h8manifest.json',
    [switch]$Replace,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_ASSET_APPLY] ' + $Message)
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

function Validate-CanonicalId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed.Length -gt 96) { Fail ($Label + ' must be 96 characters or shorter.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }
    }
    return $trimmed
}

function Validate-AssetKind([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Kind is required.' }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail 'Kind must not contain leading or trailing whitespace.' }
    if (@('raw_texture','audio_clip','data_blob') -notcontains $trimmed) {
        Fail 'Kind must be one of: raw_texture, audio_clip, data_blob.'
    }
    return $trimmed
}

function Get-AllowedExtensions([string]$KindValue) {
    switch ($KindValue) {
        'raw_texture' { return @('.png','.jpg','.jpeg','.webp') }
        'audio_clip' { return @('.wav','.ogg') }
        'data_blob' { return @('.json','.bytes','.bin') }
        default { Fail 'Unsupported asset kind.' }
    }
}

function Resolve-StarterRelativePath([string]$RelativePath, [string]$RequiredPrefix, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail ($Label + ' is required.')
    }

    $normalized = $RelativePath.Replace('\','/').Trim()
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        Fail ($Label + ' must be a starter-relative path.')
    }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) {
        Fail ($Label + ' must not contain .. segments.')
    }
    if (-not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must stay under ' + $RequiredPrefix)
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = Join-StarterPath $Root $normalized
    }
}

function Resolve-AssetPath([string]$RelativePath, [string]$KindValue) {
    $resolved = Resolve-StarterRelativePath $RelativePath 'Content/Assets/' 'Asset Path'
    $extension = [System.IO.Path]::GetExtension($resolved.Relative).ToLowerInvariant()
    if ((Get-AllowedExtensions $KindValue) -notcontains $extension) {
        Fail ('Asset Path extension is not allowed for ' + $KindValue + ': ' + $extension)
    }
    return $resolved
}

function Read-JsonFile([string]$Path, [string]$Label) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Fail ($Label + ' is missing: ' + $Path)
    }

    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    } catch {
        Fail ($Label + ' is invalid JSON: ' + $_.Exception.Message)
    }
}

function Validate-Crc32Text([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Crc32 is required.' }
    $trimmed = $Value.Trim()
    if ($trimmed -notmatch '^[0-9A-Fa-f]{8}$') { Fail 'Crc32 must be 8 hex characters.' }
    return $trimmed.ToUpperInvariant()
}

function New-Crc32Table {
    $table = New-Object 'uint64[]' 256
    for ($i = 0; $i -lt 256; $i++) {
        [uint64]$crc = [uint64]$i
        for ($j = 0; $j -lt 8; $j++) {
            if (($crc -band 1) -ne 0) {
                $crc = ([uint64]3988292384 -bxor ($crc -shr 1)) -band [uint64]4294967295
            } else {
                $crc = ($crc -shr 1) -band [uint64]4294967295
            }
        }
        $table[$i] = $crc
    }
    return $table
}

$script:Crc32Table = New-Crc32Table

function Get-Crc32Hex([string]$FilePath) {
    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $buffer = New-Object byte[] 8192
        [uint64]$crc = [uint64]4294967295
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            for ($i = 0; $i -lt $read; $i++) {
                $index = [int](($crc -bxor [uint64]$buffer[$i]) -band 255)
                $crc = ($script:Crc32Table[$index] -bxor ($crc -shr 8)) -band [uint64]4294967295
            }
        }
        $crc = ($crc -bxor [uint64]4294967295) -band [uint64]4294967295
        return ('{0:X8}' -f $crc)
    } finally {
        $stream.Dispose()
    }
}

function Get-SnippetAsset([object]$SnippetDocument) {
    $assetProperty = $SnippetDocument.PSObject.Properties['Asset']
    if ($null -ne $assetProperty) {
        return $assetProperty.Value
    }
    return $SnippetDocument
}

function Build-CleanAssetEntry([object]$SnippetAsset) {
    if ($null -eq $SnippetAsset) { Fail 'Asset entry snippet is null.' }

    $assetId = Validate-CanonicalId ([string]$SnippetAsset.Id) 'Asset Id'
    $assetKind = Validate-AssetKind ([string]$SnippetAsset.Kind)
    $assetPath = Resolve-AssetPath ([string]$SnippetAsset.Path) $assetKind
    $assetCrc32 = Validate-Crc32Text ([string]$SnippetAsset.Crc32)
    $bytesProperty = $SnippetAsset.PSObject.Properties['Bytes']
    if ($null -eq $bytesProperty) { Fail 'Asset Bytes is required.' }
    [long]$assetBytes = 0
    try {
        $assetBytes = [long]$bytesProperty.Value
    } catch {
        Fail 'Asset Bytes must be a JSON integer.'
    }
    if ($assetBytes -lt 0) { Fail 'Asset Bytes must be >= 0.' }

    if (-not (Test-Path -LiteralPath $assetPath.Full -PathType Leaf)) {
        Fail ('Referenced asset file is missing: ' + $assetPath.Relative)
    }

    $fileInfo = Get-Item -LiteralPath $assetPath.Full
    if ([long]$fileInfo.Length -ne $assetBytes) {
        Fail ('Asset Bytes does not match file length for ' + $assetPath.Relative + '. Expected ' + $assetBytes + ', actual ' + [long]$fileInfo.Length + '.')
    }

    $computedCrc32 = Get-Crc32Hex $assetPath.Full
    if ($computedCrc32 -ne $assetCrc32) {
        Fail ('Asset Crc32 does not match file for ' + $assetPath.Relative + '. Expected ' + $assetCrc32 + ', actual ' + $computedCrc32 + '.')
    }

    return [pscustomobject][ordered]@{
        Id = $assetId
        Kind = $assetKind
        Path = $assetPath.Relative
        Crc32 = $assetCrc32
        Bytes = $assetBytes
    }
}

function Validate-AssetManifestDocument([object]$Document) {
    if ([string]$Document.Schema -ne 'hecton8.assets.draft.v1') {
        Fail 'Content/assets.h8manifest.json Schema must be hecton8.assets.draft.v1.'
    }
    $assetsProperty = $Document.PSObject.Properties['Assets']
    if ($null -eq $assetsProperty -or $null -eq $assetsProperty.Value -or -not $assetsProperty.Value.GetType().IsArray) {
        Fail 'Content/assets.h8manifest.json Assets must be a JSON array.'
    }
}

function Build-AssetManifestDocument([object]$Document, [object[]]$Assets) {
    $output = [ordered]@{}
    foreach ($property in $Document.PSObject.Properties) {
        if ($property.Name -eq 'Assets') {
            $output.Assets = $Assets
        } else {
            $output[$property.Name] = $property.Value
        }
    }
    if (-not $output.Contains('Assets')) {
        $output.Assets = $Assets
    }
    return [pscustomobject]$output
}

function Ensure-ManifestAssetBudget([object]$ManifestDocument, [long]$RequiredBytes) {
    $budgetsProperty = $ManifestDocument.PSObject.Properties['Budgets']
    if ($null -eq $budgetsProperty -or $null -eq $budgetsProperty.Value) {
        Fail 'mod.h8manifest.json Budgets is required.'
    }
    [long]$budgetValue = [long]$budgetsProperty.Value.MaxAssetBytes
    if ($budgetValue -lt $RequiredBytes) {
        $budgetsProperty.Value.MaxAssetBytes = $RequiredBytes
    }
}

function Invoke-StarterValidator([string]$RootPath) {
    $validator = Join-StarterPath $RootPath 'Tools/validate_structure.ps1'
    if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
        Fail 'Missing Tools/validate_structure.ps1.'
    }

    try {
        $global:LASTEXITCODE = 0
        & $validator -Root $RootPath -ThrowInsteadOfExit *>$null
    } catch {
        throw ('Validation failed after asset entry apply: ' + $_.Exception.Message)
    }
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $jsonText = ($Value | ConvertTo-Json -Depth 32)
    [System.IO.File]::WriteAllText($Path, ($jsonText + [System.Environment]::NewLine), $utf8NoBom)
    [void](Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json)
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$snippetPath = Resolve-StarterRelativePath $Snippet 'Generated/' 'Snippet'
$targetPath = Resolve-StarterRelativePath $Target 'Content/' 'Target'
$manifestPath = Resolve-StarterRelativePath $Manifest '' 'Manifest'
if ($targetPath.Relative -ne 'Content/assets.h8manifest.json') {
    Fail 'Target must be Content/assets.h8manifest.json for this tool.'
}
if ($manifestPath.Relative -ne 'mod.h8manifest.json') {
    Fail 'Manifest must be mod.h8manifest.json for this tool.'
}

$snippetDocument = Read-JsonFile $snippetPath.Full 'Asset entry snippet'
$newAsset = Build-CleanAssetEntry (Get-SnippetAsset $snippetDocument)
$assetManifest = Read-JsonFile $targetPath.Full 'Asset manifest'
$authoring = Read-JsonFile $manifestPath.Full 'Authoring manifest'
Validate-AssetManifestDocument $assetManifest

$sourceAssets = @($assetManifest.Assets)
$assets = New-Object 'System.Collections.Generic.List[object]'
$replaced = $false
[long]$totalBytes = 0
for ($i = 0; $i -lt $sourceAssets.Count; $i++) {
    $existingAsset = $sourceAssets[$i]
    if ($null -eq $existingAsset) { Fail ('Content/assets.h8manifest.json Assets[' + $i + '] must not be null.') }
    $existingId = Validate-CanonicalId ([string]$existingAsset.Id) ('Content/assets.h8manifest.json Assets[' + $i + '] Id')
    if ($existingId -eq $newAsset.Id) {
        if (-not $Replace) {
            Fail ('Asset entry already exists: ' + $newAsset.Id + '. Re-run with -Replace only if replacement is intended.')
        }
        [void]$assets.Add($newAsset)
        $totalBytes += [long]$newAsset.Bytes
        $replaced = $true
    } else {
        $cleanExisting = Build-CleanAssetEntry $existingAsset
        [void]$assets.Add($cleanExisting)
        $totalBytes += [long]$cleanExisting.Bytes
    }
}

if (-not $replaced) {
    if ($assets.Count -ge 512) { Fail 'Content/assets.h8manifest.json Assets already has 512 entries.' }
    [void]$assets.Add($newAsset)
    $totalBytes += [long]$newAsset.Bytes
}

$assetManifestDocument = Build-AssetManifestDocument $assetManifest $assets.ToArray()
Ensure-ManifestAssetBudget $authoring $totalBytes

$targetDirectory = Split-Path -Parent $targetPath.Full
$targetName = [System.IO.Path]::GetFileName($targetPath.Full)
$manifestDirectory = Split-Path -Parent $manifestPath.Full
$manifestName = [System.IO.Path]::GetFileName($manifestPath.Full)
$uniqueSuffix = [System.Guid]::NewGuid().ToString('N')
$targetTempPath = Join-Path $targetDirectory ('.' + $targetName + '.tmp-' + $uniqueSuffix)
$targetBackupPath = Join-Path $targetDirectory ('.' + $targetName + '.previous-' + $uniqueSuffix)
$manifestTempPath = Join-Path $manifestDirectory ('.' + $manifestName + '.tmp-' + $uniqueSuffix)
$manifestBackupPath = Join-Path $manifestDirectory ('.' + $manifestName + '.previous-' + $uniqueSuffix)

try {
    Write-JsonFile $targetTempPath $assetManifestDocument
    Write-JsonFile $manifestTempPath $authoring

    Move-Item -LiteralPath $targetPath.Full -Destination $targetBackupPath -Force
    Move-Item -LiteralPath $manifestPath.Full -Destination $manifestBackupPath -Force
    Move-Item -LiteralPath $targetTempPath -Destination $targetPath.Full -Force
    Move-Item -LiteralPath $manifestTempPath -Destination $manifestPath.Full -Force
    Invoke-StarterValidator $Root

    if (Test-Path -LiteralPath $targetBackupPath -PathType Leaf) {
        Remove-Item -LiteralPath $targetBackupPath -Force
    }
    if (Test-Path -LiteralPath $manifestBackupPath -PathType Leaf) {
        Remove-Item -LiteralPath $manifestBackupPath -Force
    }
} catch {
    if (Test-Path -LiteralPath $targetBackupPath -PathType Leaf) {
        if (Test-Path -LiteralPath $targetPath.Full -PathType Leaf) {
            Remove-Item -LiteralPath $targetPath.Full -Force
        }
        Move-Item -LiteralPath $targetBackupPath -Destination $targetPath.Full -Force
    }
    if (Test-Path -LiteralPath $manifestBackupPath -PathType Leaf) {
        if (Test-Path -LiteralPath $manifestPath.Full -PathType Leaf) {
            Remove-Item -LiteralPath $manifestPath.Full -Force
        }
        Move-Item -LiteralPath $manifestBackupPath -Destination $manifestPath.Full -Force
    }
    Fail $_.Exception.Message
} finally {
    foreach ($path in @($targetTempPath, $manifestTempPath)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.asset_entry_apply.v1'
        Runtime = 'envelope-only'
        Target = $targetPath.Relative
        Manifest = $manifestPath.Relative
        Snippet = $snippetPath.Relative
        AssetId = $newAsset.Id
        Kind = $newAsset.Kind
        Path = $newAsset.Path
        Bytes = [long]$newAsset.Bytes
        Crc32 = $newAsset.Crc32
        Replaced = $replaced
        ManifestMaxAssetBytes = [long]$authoring.Budgets.MaxAssetBytes
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 asset entry snippet applied'
Write-Output ('Target: ' + $targetPath.Relative)
Write-Output ('Asset Id: ' + $newAsset.Id)
Write-Output ('Kind: ' + $newAsset.Kind)
Write-Output ('Path: ' + $newAsset.Path)
Write-Output ('Replaced: ' + $replaced)
Write-Output ('MaxAssetBytes: ' + [string]$authoring.Budgets.MaxAssetBytes)
