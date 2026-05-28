param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Id = 'node.spawn_item',
    [string]$Opcode = 'SpawnItem',
    [string]$Output = 'Generated/graph_node_snippet.json',
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

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
    foreach ($line in (Get-Content -LiteralPath $path)) {
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
$nodeId = Validate-NodeId $Id
$allowedOpcodes = Read-AllowedGraphOpcodes
$opcodeToken = Resolve-Opcode $Opcode $allowedOpcodes
$outputPath = Resolve-GeneratedOutputPath $Output

$node = [pscustomobject][ordered]@{
    Id = $nodeId
    Opcode = $opcodeToken
    Enabled = $true
    Parameters = [pscustomobject][ordered]@{}
    Notes = 'Apply with h8mod.ps1 -Action apply-node-snippet, or copy this object into Graphs/main.h8graph.json Nodes[] and run h8mod.ps1 -Action validate.'
}

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
Write-Output 'Next: h8mod.ps1 -Action apply-node-snippet. Manual fallback: copy the JSON object into Graphs/main.h8graph.json Nodes[], then run h8mod.ps1 -Action validate.'
