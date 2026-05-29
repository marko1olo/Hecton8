param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id = 'asset.example_blob',
    [string]$Kind = 'data_blob',
    [string]$Path = 'Content/Assets/example.bytes',
    [string]$Crc32 = '00000000',
    [long]$Bytes = 0,
    [string]$Output = 'Generated/asset_entry_snippet.json',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_ASSET_SNIPPET] ' + $Message)
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

function Resolve-AssetPath([string]$RelativePath, [string]$KindValue) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail 'Path is required.'
    }

    $normalized = $RelativePath.Replace('\','/').Trim()
    if ($normalized -ne $RelativePath.Replace('\','/')) {
        Fail 'Path must not contain leading or trailing whitespace.'
    }
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        Fail 'Path must be starter-relative.'
    }
    if ($normalized.StartsWith('../') -or $normalized.Contains('/../') -or $normalized.Contains('..')) {
        Fail 'Path must not contain .. segments.'
    }
    if (-not $normalized.StartsWith('Content/Assets/', [System.StringComparison]::Ordinal)) {
        Fail 'Path must stay under Content/Assets/.'
    }

    $extension = [System.IO.Path]::GetExtension($normalized).ToLowerInvariant()
    if ((Get-AllowedExtensions $KindValue) -notcontains $extension) {
        Fail ('Path extension is not allowed for ' + $KindValue + ': ' + $extension)
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = Join-StarterPath $Root $normalized
    }
}

function Validate-Crc32Text([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail 'Crc32 is required.' }
    $trimmed = $Value.Trim()
    if ($trimmed -ieq 'auto') { return 'auto' }
    if ($trimmed -notmatch '^[0-9A-Fa-f]{8}$') { Fail 'Crc32 must be 8 hex characters or auto.' }
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

function Resolve-GeneratedOutputPath([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail 'Output is required.'
    }

    $normalized = $RelativePath.Replace('\','/').Trim()
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        Fail 'Output must be a starter-relative path under Generated/.'
    }
    if ($normalized.Contains('..') -or -not $normalized.StartsWith('Generated/', [System.StringComparison]::Ordinal)) {
        Fail 'Output must stay under Generated/ and must not contain .. segments.'
    }

    $directory = Join-StarterPath $Root 'Generated'
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $directory -Force)
    }

    $outputPath = Join-StarterPath $Root $normalized
    $outputDirectory = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        [void](New-Item -ItemType Directory -Path $outputDirectory -Force)
    }

    return [pscustomobject][ordered]@{
        Relative = $normalized
        Full = $outputPath
    }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$assetId = Validate-CanonicalId $Id 'Asset Id'
$assetKind = Validate-AssetKind $Kind
$assetPath = Resolve-AssetPath $Path $assetKind
$assetCrc32 = Validate-Crc32Text $Crc32
$assetBytes = $Bytes

if ($assetBytes -lt 0 -and -not (Test-Path -LiteralPath $assetPath.Full -PathType Leaf)) {
    Fail 'Bytes auto mode requires the referenced asset file to exist.'
}
if ($assetCrc32 -eq 'auto' -and -not (Test-Path -LiteralPath $assetPath.Full -PathType Leaf)) {
    Fail 'Crc32 auto mode requires the referenced asset file to exist.'
}

if (Test-Path -LiteralPath $assetPath.Full -PathType Leaf) {
    $fileInfo = Get-Item -LiteralPath $assetPath.Full
    if ($assetBytes -lt 0) {
        $assetBytes = [long]$fileInfo.Length
    } elseif ([long]$fileInfo.Length -ne $assetBytes) {
        Fail ('Bytes does not match file length. Expected ' + $assetBytes + ', actual ' + [long]$fileInfo.Length + '.')
    }

    $computedCrc32 = Get-Crc32Hex $assetPath.Full
    if ($assetCrc32 -eq 'auto') {
        $assetCrc32 = $computedCrc32
    } elseif ($computedCrc32 -ne $assetCrc32) {
        Fail ('Crc32 does not match file. Expected ' + $assetCrc32 + ', actual ' + $computedCrc32 + '.')
    }
} else {
    if ($assetBytes -lt 0) {
        Fail 'Bytes must be >= 0 when the asset file is not present.'
    }
}

$outputPath = Resolve-GeneratedOutputPath $Output
$asset = [pscustomobject][ordered]@{
    Id = $assetId
    Kind = $assetKind
    Path = $assetPath.Relative
    Crc32 = $assetCrc32
    Bytes = $assetBytes
    Notes = 'Apply with h8mod.ps1 -Action apply-asset-snippet after the referenced file exists under Content/Assets/.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$assetJson = ($asset | ConvertTo-Json -Depth 8)
[System.IO.File]::WriteAllText($outputPath.Full, ($assetJson + [System.Environment]::NewLine), $utf8NoBom)

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.asset_entry_snippet.v1'
        Runtime = 'envelope-only'
        Output = $outputPath.Relative
        Asset = $asset
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 asset entry snippet written'
Write-Output ('Output: ' + $outputPath.Relative)
Write-Output ('Asset Id: ' + $assetId)
Write-Output ('Kind: ' + $assetKind)
Write-Output ('Path: ' + $assetPath.Relative)
Write-Output 'Next: h8mod.ps1 -Action apply-asset-snippet. Runtime loading still requires engine approval/bake.'
