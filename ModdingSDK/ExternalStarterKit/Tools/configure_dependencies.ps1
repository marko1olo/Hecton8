param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('list','add','remove','clear')]
    [string]$Action = 'list',
    [string]$DependencyId = '',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxManifestJsonBytes = 65536

function Fail([string]$Message) {
    Write-Error ('[H8MOD_DEPENDENCIES] ' + $Message)
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

function Require-File([string]$RelativePath) {
    $path = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail ('Missing required file: ' + $RelativePath)
    }
    return $path
}

function Read-JsonFile([string]$RelativePath, [long]$MaxBytes = $MaxManifestJsonBytes) {
    $path = Require-File $RelativePath
    try {
        return Read-H8JsonFileCapped $path $RelativePath $MaxBytes
    } catch {
        Fail $_.Exception.Message
    }
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

function Get-JsonStringArray([object]$Document, [string]$PropertyName, [string]$Label) {
    $property = $Document.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }

    if (-not $property.Value.GetType().IsArray) {
        Fail ($Label + ' must be a JSON array.')
    }

    $values = @()
    $seen = @{}
    foreach ($entry in @($property.Value)) {
        $dependencyId = Validate-ModId ([string]$entry) ($Label + ' item')
        if ($seen.ContainsKey($dependencyId)) {
            Fail ($Label + ' contains duplicate dependency: ' + $dependencyId)
        }
        $seen[$dependencyId] = $true
        $values += $dependencyId
    }
    return @($values)
}

function Set-JsonStringArray([object]$Document, [string]$PropertyName, [string[]]$Values) {
    $array = @($Values)
    $property = $Document.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        $Document | Add-Member -MemberType NoteProperty -Name $PropertyName -Value $array
        return
    }

    $property.Value = $array
}

function Assert-DependencyListValid([string]$ModId, [string[]]$Dependencies) {
    if ($Dependencies.Count -gt 32) {
        Fail 'Dependencies exceeds 32 entries.'
    }

    $seen = @{}
    foreach ($dependency in @($Dependencies)) {
        if ($dependency -eq $ModId) {
            Fail ('Mod must not depend on itself: ' + $dependency)
        }
        if ($seen.ContainsKey($dependency)) {
            Fail ('Duplicate dependency: ' + $dependency)
        }
        $seen[$dependency] = $true
    }
}

function Invoke-LocalValidation {
    $validator = Require-File 'Tools/validate_structure.ps1'
    $global:LASTEXITCODE = 0
    $validationOutput = & $validator -Root $Root *>&1
    if (-not $?) {
        throw ('validate_structure.ps1 failed: ' + (($validationOutput | ForEach-Object { [string]$_ }) -join ' | '))
    }
    if ($global:LASTEXITCODE -ne 0) {
        throw ('validate_structure.ps1 exit code ' + $global:LASTEXITCODE + ': ' + (($validationOutput | ForEach-Object { [string]$_ }) -join ' | '))
    }
}

function Write-TextFileUtf8NoBom([string]$Path, [string]$Text) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $utf8NoBom)
}

function Write-JsonFileUtf8NoBom([string]$Path, [object]$Value) {
    $jsonText = ($Value | ConvertTo-Json -Depth 16)
    Write-TextFileUtf8NoBom $Path ($jsonText + [System.Environment]::NewLine)
    [void](Read-H8JsonFileCapped $Path 'Written dependency manifest' $MaxManifestJsonBytes)
}

function Write-ManifestsWithValidation([object]$Authoring, [object]$Runtime) {
    $authoringPath = Require-File 'mod.h8manifest.json'
    $runtimePath = Require-File 'mod.json'
    $authoringOriginal = Read-H8TextFileCapped $authoringPath 'mod.h8manifest.json rollback copy' $MaxManifestJsonBytes
    $runtimeOriginal = Read-H8TextFileCapped $runtimePath 'mod.json rollback copy' $MaxManifestJsonBytes
    $tempId = [System.Guid]::NewGuid().ToString('N')
    $authoringTemp = Join-Path ([System.IO.Path]::GetTempPath()) ('hecton8-dependencies-authoring-' + $tempId + '.json')
    $runtimeTemp = Join-Path ([System.IO.Path]::GetTempPath()) ('hecton8-dependencies-runtime-' + $tempId + '.json')

    try {
        Write-JsonFileUtf8NoBom $authoringTemp $Authoring
        Write-JsonFileUtf8NoBom $runtimeTemp $Runtime
        Copy-Item -LiteralPath $authoringTemp -Destination $authoringPath -Force
        Copy-Item -LiteralPath $runtimeTemp -Destination $runtimePath -Force
        Invoke-LocalValidation
    } catch {
        Write-TextFileUtf8NoBom $authoringPath $authoringOriginal
        Write-TextFileUtf8NoBom $runtimePath $runtimeOriginal
        Fail ('Dependency write rejected and manifests restored: ' + $_.Exception.Message)
    } finally {
        Remove-Item -LiteralPath $authoringTemp -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $runtimeTemp -Force -ErrorAction SilentlyContinue
    }
}

$authoring = Read-JsonFile 'mod.h8manifest.json'
$runtime = Read-JsonFile 'mod.json'
$authoringId = Validate-ModId ([string]$authoring.Id) 'mod.h8manifest.json Id'
$runtimeId = Validate-ModId ([string]$runtime.Id) 'mod.json Id'
if ($authoringId -ne $runtimeId) {
    Fail 'mod.h8manifest.json Id must match mod.json Id before dependency editing.'
}

$authoringDependencies = @(Get-JsonStringArray $authoring 'Dependencies' 'mod.h8manifest.json Dependencies')
$runtimeDependencies = @(Get-JsonStringArray $runtime 'Dependencies' 'mod.json Dependencies')
if (($authoringDependencies -join "`n") -ne ($runtimeDependencies -join "`n")) {
    Fail 'mod.h8manifest.json Dependencies must match mod.json Dependencies before editing. Repair one manifest or clear dependencies through this tool after making them match.'
}

$dependencies = @($runtimeDependencies)
Assert-DependencyListValid $runtimeId $dependencies
$changed = $false

switch ($Action) {
    'list' { }
    'add' {
        $dependency = Validate-ModId $DependencyId 'DependencyId'
        if ($dependency -eq $runtimeId) { Fail ('Mod must not depend on itself: ' + $dependency) }
        if ($dependencies -contains $dependency) { Fail ('Dependency already exists: ' + $dependency) }
        $dependencies += $dependency
        $changed = $true
    }
    'remove' {
        $dependency = Validate-ModId $DependencyId 'DependencyId'
        if ($dependencies -notcontains $dependency) { Fail ('Dependency not found: ' + $dependency) }
        $dependencies = @($dependencies | Where-Object { $_ -ne $dependency })
        $changed = $true
    }
    'clear' {
        if ($dependencies.Count -gt 0) {
            $dependencies = @()
            $changed = $true
        }
    }
    default { Fail ('Unsupported action: ' + $Action) }
}

Assert-DependencyListValid $runtimeId $dependencies
if ($changed) {
    Set-JsonStringArray $authoring 'Dependencies' $dependencies
    Set-JsonStringArray $runtime 'Dependencies' $dependencies
    Write-ManifestsWithValidation $authoring $runtime
}

$result = [ordered]@{
    Schema = 'hecton8.dependencies.v1'
    Action = $Action
    ModId = $runtimeId
    Changed = $changed
    Count = $dependencies.Count
    Dependencies = @($dependencies)
    AuthoringManifest = 'mod.h8manifest.json'
    RuntimeManifest = 'mod.json'
    RuntimeBoundary = 'envelope-only'
}

if ($Json) {
    $result | ConvertTo-Json -Depth 8
} else {
    Write-Host ('PASS HECTON-8 dependencies ' + $Action + ': ' + $dependencies.Count + ' dependencies')
    if ($dependencies.Count -gt 0) {
        foreach ($dependency in $dependencies) {
            Write-Host ('- ' + $dependency)
        }
    }
}
