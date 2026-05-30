param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id,
    [string]$DisplayName,
    [string]$Author,
    [string]$Version
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxManifestJsonBytes = 65536

function Fail([string]$Message) {
    Write-Error ('[H8MOD_SET_IDENTITY] ' + $Message)
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

function Validate-ModId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    if ($trimmed -notmatch '^[a-z0-9]+([._-][a-z0-9]+)*$') {
        Fail ($Label + " may contain only lowercase latin letters, digits, '.', '_' and '-' with single separators between letters or digits.")
    }
    foreach ($segment in ($trimmed -split '[._-]')) {
        if (Test-ReservedModIdSegment $segment) { Fail ($Label + ' contains a reserved filesystem device segment.') }
    }
    return $trimmed
}

function Validate-RequiredText([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) { Fail ($Label + ' is required.') }
    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) { Fail ($Label + ' must not contain leading or trailing whitespace.') }
    return $trimmed
}

function Validate-Version([string]$Value, [string]$Label) {
    $trimmed = Validate-RequiredText $Value $Label
    if ($trimmed -notmatch '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$') {
        Fail ($Label + ' must use semantic version form MAJOR.MINOR.PATCH with optional -prerelease or +build metadata.')
    }
    return $trimmed
}

function Read-JsonFile([string]$Path, [string]$Label, [long]$MaxBytes) {
    try {
        return Read-H8JsonFileCapped $Path $Label $MaxBytes
    } catch {
        Fail $_.Exception.Message
    }
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $json = $Value | ConvertTo-Json -Depth 16
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $json + [System.Environment]::NewLine, $utf8NoBom)
    [void](Read-H8JsonFileCapped $Path 'Written identity manifest' $MaxManifestJsonBytes)
}

function Remove-TempFile([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path) -and (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Restore-FileBackup([string]$BackupPath, [string]$TargetPath) {
    if (Test-Path -LiteralPath $BackupPath -PathType Leaf) {
        Copy-Item -LiteralPath $BackupPath -Destination $TargetPath -Force
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

if ([string]::IsNullOrWhiteSpace($Id)) {
    Fail 'Usage: powershell -NoProfile -ExecutionPolicy Bypass -File Tools/set_mod_identity.ps1 -Id com.yourname.mod -DisplayName "Your Mod" -Author "YourName" -Version 0.1.0'
}

$rootFull = (Resolve-Path -LiteralPath $Root).Path
$authoringPath = Join-StarterPath $rootFull 'mod.h8manifest.json'
$runtimePath = Join-StarterPath $rootFull 'mod.json'
$authoring = Read-JsonFile $authoringPath 'mod.h8manifest.json' $MaxManifestJsonBytes
$runtime = Read-JsonFile $runtimePath 'mod.json' $MaxManifestJsonBytes
$canonicalId = Validate-ModId $Id 'Id'

$authoring.Id = $canonicalId
$runtime.Id = $canonicalId

if (-not [string]::IsNullOrWhiteSpace($DisplayName)) {
    $canonicalDisplayName = Validate-RequiredText $DisplayName 'DisplayName'
    $authoring.DisplayName = $canonicalDisplayName
    $runtime.Name = $canonicalDisplayName
}

if (-not [string]::IsNullOrWhiteSpace($Author)) {
    $canonicalAuthor = Validate-RequiredText $Author 'Author'
    $authoring.Author = $canonicalAuthor
    $runtime.Author = $canonicalAuthor
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $canonicalVersion = Validate-Version $Version 'Version'
    $authoring.Version = $canonicalVersion
    $runtime.Version = $canonicalVersion
}

$uniqueSuffix = [System.Guid]::NewGuid().ToString('N')
$tempRoot = [System.IO.Path]::GetTempPath()
$authoringName = [System.IO.Path]::GetFileName($authoringPath)
$runtimeName = [System.IO.Path]::GetFileName($runtimePath)
$authoringTempPath = Join-Path $tempRoot ('hecton8-' + $authoringName + '.tmp-' + $uniqueSuffix)
$runtimeTempPath = Join-Path $tempRoot ('hecton8-' + $runtimeName + '.tmp-' + $uniqueSuffix)
$authoringBackupPath = Join-Path $tempRoot ('hecton8-' + $authoringName + '.previous-' + $uniqueSuffix)
$runtimeBackupPath = Join-Path $tempRoot ('hecton8-' + $runtimeName + '.previous-' + $uniqueSuffix)

try {
    Write-JsonFile $authoringTempPath $authoring
    Write-JsonFile $runtimeTempPath $runtime
    Copy-Item -LiteralPath $authoringPath -Destination $authoringBackupPath -Force
    Copy-Item -LiteralPath $runtimePath -Destination $runtimeBackupPath -Force
    Copy-Item -LiteralPath $authoringTempPath -Destination $authoringPath -Force
    Copy-Item -LiteralPath $runtimeTempPath -Destination $runtimePath -Force

    $validator = Join-StarterPath $rootFull 'Tools/validate_structure.ps1'
    if (Test-Path -LiteralPath $validator -PathType Leaf) {
        Invoke-RequiredTool { & $validator -Root $rootFull } 'starter validation'
    }
} catch {
    Restore-FileBackup $authoringBackupPath $authoringPath
    Restore-FileBackup $runtimeBackupPath $runtimePath
    Fail $_.Exception.Message
} finally {
    Remove-TempFile $authoringTempPath
    Remove-TempFile $runtimeTempPath
    Remove-TempFile $authoringBackupPath
    Remove-TempFile $runtimeBackupPath
}

Write-Host ('PASS HECTON-8 starter identity set: ' + $canonicalId)
