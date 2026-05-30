param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id = 'node.spawn_item',
    [string]$Opcode = 'SpawnItem',
    [string]$ParametersJson = '{}',
    [string]$Output = 'Generated/graph_node_snippet.json',
    [switch]$Disabled,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxAllowedOpcodesCsvBytes = 262144

function Fail([string]$Message) {
    Write-Error ('[H8MOD_GRAPH_SNIPPET] ' + $Message)
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

function Require-File([string]$RelativePath) {
    $path = Join-StarterPath $Root $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail ('Missing required file: ' + $RelativePath)
    }
    return $path
}

function Validate-NodeId([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Fail 'Node Id is required.'
    }

    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) {
        Fail 'Node Id must not contain leading or trailing whitespace.'
    }

    if ($trimmed.Length -gt 64) {
        Fail 'Node Id must be 64 characters or shorter.'
    }

    if ($trimmed -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
        Fail 'Node Id may contain latin letters, digits, dot, underscore, and dash, and must start with a letter or digit.'
    }

    return $trimmed
}

function Read-AllowedGraphOpcodes() {
    $path = Require-File 'Reference/allowed_opcodes.csv'
    $tokens = @{}
    try {
        $allowedOpcodeText = Read-H8TextFileCapped $path 'Reference/allowed_opcodes.csv' $MaxAllowedOpcodesCsvBytes
    } catch {
        Fail $_.Exception.Message
    }
    foreach ($line in ($allowedOpcodeText -split "\r?\n")) {
        $text = [string]$line
        $comment = ''
        $commentIndex = $text.IndexOf('#')
        if ($commentIndex -ge 0) {
            $comment = $text.Substring($commentIndex + 1).Trim()
            $text = $text.Substring(0, $commentIndex).Trim()
        } else {
            $text = $text.Trim()
        }

        if ([string]::IsNullOrWhiteSpace($text)) { continue }
        if ($text -notmatch '^0x[0-9A-Fa-f]{1,8}$') {
            Fail ('Reference/allowed_opcodes.csv contains invalid opcode token: ' + $text)
        }

        $hex = '0x' + $text.Substring(2).ToUpperInvariant()
        if ($tokens.ContainsKey($hex)) {
            Fail ('Reference/allowed_opcodes.csv contains duplicate opcode token: ' + $hex)
        }
        $tokens[$hex] = $hex

        if (-not [string]::IsNullOrWhiteSpace($comment)) {
            $alias = @($comment -split '\s+')[0]
            if ($alias -match '^[A-Za-z][A-Za-z0-9_]*$') {
                if ($tokens.ContainsKey($alias)) {
                    Fail ('Reference/allowed_opcodes.csv contains duplicate opcode alias: ' + $alias)
                }
                $tokens[$alias] = $alias
            }
        }
    }

    if ($tokens.Count -eq 0) {
        Fail 'Reference/allowed_opcodes.csv has no allowed graph opcodes.'
    }

    return $tokens
}

function Read-RelaxedParameterScalar([string]$Value) {
    $text = $Value.Trim()
    if ($text.Length -ge 2) {
        if (($text.StartsWith('"') -and $text.EndsWith('"')) -or ($text.StartsWith("'") -and $text.EndsWith("'"))) {
            return $text.Substring(1, $text.Length - 2)
        }
    }

    if ($text -match '^(?i:true|false)$') {
        return [bool]::Parse($text)
    }
    if ($text -match '^(?i:null)$') {
        return $null
    }

    $intValue = 0L
    if ([long]::TryParse($text, [System.Globalization.NumberStyles]::Integer, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$intValue)) {
        return $intValue
    }

    $doubleValue = 0.0
    if ([double]::TryParse($text, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$doubleValue)) {
        return $doubleValue
    }

    return $text
}

function Read-RelaxedParametersObject([string]$Value, [string]$JsonErrorMessage) {
    $text = $Value.Trim()
    if (-not ($text.StartsWith('{') -and $text.EndsWith('}'))) {
        Fail ('ParametersJson is invalid JSON: ' + $JsonErrorMessage)
    }

    $inner = $text.Substring(1, $text.Length - 2).Trim()
    $parameterMap = [ordered]@{}
    if ([string]::IsNullOrWhiteSpace($inner)) {
        return [pscustomobject]$parameterMap
    }

    $entries = @($inner -split ',')
    if ($entries.Count -gt 64) {
        Fail 'ParametersJson must not exceed 64 entries.'
    }

    foreach ($entry in $entries) {
        $parts = @($entry -split ':', 2)
        if ($parts.Count -ne 2) {
            Fail ('ParametersJson is invalid JSON: ' + $JsonErrorMessage)
        }

        $key = $parts[0].Trim().Trim('"').Trim("'")
        if ([string]::IsNullOrWhiteSpace($key) -or $key -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
            Fail ('ParametersJson contains invalid key: ' + $key)
        }

        $parameterMap[$key] = Read-RelaxedParameterScalar $parts[1]
    }

    return [pscustomobject]$parameterMap
}

function Resolve-Opcode([string]$Value, [hashtable]$AllowedOpcodes) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Fail 'Opcode is required.'
    }

    $trimmed = $Value.Trim()
    $candidate = $trimmed
    if ($trimmed -match '^0x[0-9A-Fa-f]{1,8}$') {
        $candidate = '0x' + $trimmed.Substring(2).ToUpperInvariant()
    }

    if (-not $AllowedOpcodes.ContainsKey($candidate)) {
        Fail ('Opcode is not in Reference/allowed_opcodes.csv: ' + $Value)
    }

    return [string]$AllowedOpcodes[$candidate]
}

function Read-ParametersJson([string]$Value) {
    $source = $Value
    if ([string]::IsNullOrWhiteSpace($source)) {
        $source = '{}'
    }
    if ($source.Length -gt 8192) {
        Fail 'ParametersJson must be 8192 characters or shorter.'
    }

    try {
        $parsed = $source | ConvertFrom-Json
    } catch {
        $parsed = Read-RelaxedParametersObject $source $_.Exception.Message
    }

    if ($null -eq $parsed -or $parsed -isnot [System.Management.Automation.PSCustomObject]) {
        Fail 'ParametersJson must be a JSON object.'
    }

    $entries = @($parsed.PSObject.Properties)
    if ($entries.Count -gt 64) {
        Fail 'ParametersJson must not exceed 64 entries.'
    }

    $parameterMap = [ordered]@{}
    foreach ($entry in $entries) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.Name) -or [string]$entry.Name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
            Fail ('ParametersJson contains invalid key: ' + [string]$entry.Name)
        }
        $parameterMap[$entry.Name] = $entry.Value
    }

    return [pscustomobject]$parameterMap
}

function Test-StrictJsonRelativePath([string]$RelativePath, [string]$RequiredPrefix, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        Fail ($Label + ' is required.')
    }

    $normalized = $RelativePath.Replace('\','/')
    if ($normalized.Trim() -cne $normalized) {
        Fail ($Label + ' must not contain leading or trailing whitespace.')
    }
    if ([System.IO.Path]::IsPathRooted($normalized) -or $normalized.StartsWith('/') -or $normalized.Contains(':')) {
        Fail ($Label + ' must be a starter-relative path.')
    }
    if (-not $normalized.StartsWith($RequiredPrefix, [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must stay under ' + $RequiredPrefix)
    }
    if (-not $normalized.EndsWith('.json', [System.StringComparison]::Ordinal)) {
        Fail ($Label + ' must end with .json.')
    }

    foreach ($segment in ($normalized -split '/')) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -eq '.' -or $segment -eq '..') {
            Fail ($Label + ' must not contain empty, dot, or dot-dot path segments.')
        }
    }

    return $normalized
}

function Resolve-GeneratedOutputPath([string]$RelativePath) {
    $normalized = Test-StrictJsonRelativePath $RelativePath 'Generated/' 'Output'

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
$nodeId = Validate-NodeId $Id
$allowedOpcodes = Read-AllowedGraphOpcodes
$opcodeToken = Resolve-Opcode $Opcode $allowedOpcodes
$parameters = Read-ParametersJson $ParametersJson
$outputPath = Resolve-GeneratedOutputPath $Output

$node = [pscustomobject][ordered]@{
    Id = $nodeId
    Opcode = $opcodeToken
    Enabled = (-not $Disabled)
    Parameters = $parameters
    Notes = 'Apply with h8mod.ps1 -Action apply-node-snippet, or copy this object into Graphs/main.h8graph.json Nodes[] and run h8mod.ps1 -Action validate.'
}
$parameterCount = @($parameters.PSObject.Properties).Count

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$nodeJson = ($node | ConvertTo-Json -Depth 8)
[System.IO.File]::WriteAllText($outputPath.Full, ($nodeJson + [System.Environment]::NewLine), $utf8NoBom)

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.graph_node_snippet.v1'
        Runtime = 'envelope-only'
        Output = $outputPath.Relative
        Node = $node
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 graph node snippet written'
Write-Output ('Output: ' + $outputPath.Relative)
Write-Output ('Node Id: ' + $nodeId)
Write-Output ('Opcode: ' + $opcodeToken)
Write-Output ('Enabled: ' + [string](-not $Disabled))
Write-Output ('Parameter Count: ' + [string]$parameterCount)
Write-Output 'Next: h8mod.ps1 -Action apply-node-snippet. Manual fallback: copy the JSON object into Graphs/main.h8graph.json Nodes[], then run h8mod.ps1 -Action validate.'
