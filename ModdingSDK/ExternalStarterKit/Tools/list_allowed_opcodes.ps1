param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'strict_json_io.ps1')

$MaxAllowedOpcodesCsvBytes = 262144

function Fail([string]$Message) {
    Write-Error ('[H8MOD_OPCODE_LIST] ' + $Message)
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

function Read-AllowedOpcodeRows() {
    $path = Require-File 'Reference/allowed_opcodes.csv'
    $rows = New-Object 'System.Collections.Generic.List[object]'
    $seenHex = @{}
    $seenAlias = @{}

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
        if ($seenHex.ContainsKey($hex)) {
            Fail ('Reference/allowed_opcodes.csv contains duplicate opcode token: ' + $hex)
        }
        $seenHex[$hex] = $true

        $alias = ''
        if (-not [string]::IsNullOrWhiteSpace($comment)) {
            $candidateAlias = @($comment -split '\s+')[0]
            if ($candidateAlias -match '^[A-Za-z][A-Za-z0-9_]*$') {
                $alias = $candidateAlias
                if ($seenAlias.ContainsKey($alias)) {
                    Fail ('Reference/allowed_opcodes.csv contains duplicate opcode alias: ' + $alias)
                }
                $seenAlias[$alias] = $true
            }
        }

        [void]$rows.Add([pscustomobject][ordered]@{
            Index = $rows.Count + 1
            Hex = $hex
            Alias = $alias
            Description = $comment
        })
    }

    if ($rows.Count -eq 0) { Fail 'Reference/allowed_opcodes.csv has no allowed graph opcodes.' }
    return $rows.ToArray()
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$rows = Read-AllowedOpcodeRows

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.allowed_graph_opcodes.v1'
        Runtime = 'envelope-only'
        Source = 'Reference/allowed_opcodes.csv'
        Count = $rows.Count
        Opcodes = $rows
    }
    Write-Output ($payload | ConvertTo-Json -Depth 6)
    exit 0
}

Write-Output 'HECTON-8 allowed graph opcodes (envelope-only)'
Write-Output 'Use Alias or Hex in Graphs/main.h8graph.json Nodes[].Opcode.'
foreach ($row in $rows) {
    $alias = [string]$row.Alias
    if ([string]::IsNullOrWhiteSpace($alias)) {
        $alias = '(no-alias)'
    }
    Write-Output ('{0,-24} {1}' -f $alias, [string]$row.Hex)
}
