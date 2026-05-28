param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot),
    [string]$Snippet = 'Generated/graph_node_snippet.json',
    [string]$Target = 'Graphs/main.h8graph.json',
    [string]$Manifest = 'mod.h8manifest.json',
    [switch]$Replace,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error ('[H8MOD_GRAPH_APPLY] ' + $Message)
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

function Validate-NodeId([string]$Value, [string]$Label) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        Fail ($Label + ' is required.')
    }

    $trimmed = $Value.Trim()
    if ($trimmed -ne $Value) {
        Fail ($Label + ' must not contain leading or trailing whitespace.')
    }

    if ($trimmed.Length -gt 64) {
        Fail ($Label + ' must be 64 characters or shorter.')
    }

    if ($trimmed -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
        Fail ($Label + ' may contain latin letters, digits, dot, underscore, and dash, and must start with a letter or digit.')
    }

    return $trimmed
}

function Read-AllowedGraphOpcodes() {
    $path = Join-StarterPath $Root 'Reference/allowed_opcodes.csv'
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Fail 'Missing Reference/allowed_opcodes.csv.'
    }

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

function Get-GraphSnippetNode([object]$SnippetDocument) {
    $nodeProperty = $SnippetDocument.PSObject.Properties['Node']
    if ($null -ne $nodeProperty) {
        return $nodeProperty.Value
    }
    return $SnippetDocument
}

function Build-CleanGraphNode([object]$SnippetNode, [hashtable]$AllowedOpcodes) {
    if ($null -eq $SnippetNode) { Fail 'Graph node snippet is null.' }

    $nodeId = Validate-NodeId ([string]$SnippetNode.Id) 'Graph node Id'
    $opcode = Resolve-Opcode ([string]$SnippetNode.Opcode) $AllowedOpcodes

    $enabled = $true
    $enabledProperty = $SnippetNode.PSObject.Properties['Enabled']
    if ($null -ne $enabledProperty) {
        if ($enabledProperty.Value -isnot [bool]) {
            Fail 'Graph node Enabled must be a JSON boolean.'
        }
        $enabled = [bool]$enabledProperty.Value
    }

    $parameters = [pscustomobject][ordered]@{}
    $parametersProperty = $SnippetNode.PSObject.Properties['Parameters']
    if ($null -ne $parametersProperty -and $null -ne $parametersProperty.Value) {
        if ($parametersProperty.Value.GetType().IsArray) {
            Fail 'Graph node Parameters must be a JSON object.'
        }

        $parameterEntries = @($parametersProperty.Value.PSObject.Properties)
        if ($parameterEntries.Count -gt 64) {
            Fail 'Graph node Parameters must not exceed 64 entries.'
        }

        $parameterMap = [ordered]@{}
        foreach ($entry in $parameterEntries) {
            if ([string]::IsNullOrWhiteSpace([string]$entry.Name) -or [string]$entry.Name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]*$') {
                Fail ('Graph node Parameters contains invalid key: ' + [string]$entry.Name)
            }
            $parameterMap[$entry.Name] = $entry.Value
        }
        $parameters = [pscustomobject]$parameterMap
    }

    return [pscustomobject][ordered]@{
        Id = $nodeId
        Opcode = $opcode
        Enabled = $enabled
        Parameters = $parameters
    }
}

function Build-GraphDocument([object]$Graph, [object[]]$Nodes) {
    $document = [ordered]@{}
    foreach ($property in $Graph.PSObject.Properties) {
        if ($property.Name -eq 'Nodes') {
            $document.Nodes = $Nodes
        } else {
            $document[$property.Name] = $property.Value
        }
    }

    if (-not $document.Contains('Nodes')) {
        $document.Nodes = $Nodes
    }

    if ([int]$document.MaxEnvelopesPerFrame -lt 1 -and $Nodes.Count -gt 0) {
        $document.MaxEnvelopesPerFrame = 1
    }

    return [pscustomobject]$document
}

function Ensure-ManifestBudget([object]$ManifestDocument, [int]$RequiredBudget) {
    $budgetsProperty = $ManifestDocument.PSObject.Properties['Budgets']
    if ($null -eq $budgetsProperty -or $null -eq $budgetsProperty.Value) {
        Fail 'mod.h8manifest.json Budgets is required.'
    }

    $budgetValue = [int]$budgetsProperty.Value.MaxEnvelopesPerFrame
    if ($budgetValue -lt $RequiredBudget) {
        $budgetsProperty.Value.MaxEnvelopesPerFrame = $RequiredBudget
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
        throw ('Validation failed after graph node apply: ' + $_.Exception.Message)
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
$targetPath = Resolve-StarterRelativePath $Target 'Graphs/' 'Target'
$manifestPath = Resolve-StarterRelativePath $Manifest '' 'Manifest'
if ($targetPath.Relative -ne 'Graphs/main.h8graph.json') {
    Fail 'Target must be Graphs/main.h8graph.json for this tool.'
}
if ($manifestPath.Relative -ne 'mod.h8manifest.json') {
    Fail 'Manifest must be mod.h8manifest.json for this tool.'
}

$allowedOpcodes = Read-AllowedGraphOpcodes
$snippetDocument = Read-JsonFile $snippetPath.Full 'Graph node snippet'
$newNode = Build-CleanGraphNode (Get-GraphSnippetNode $snippetDocument) $allowedOpcodes
$graph = Read-JsonFile $targetPath.Full 'Graph'
$authoring = Read-JsonFile $manifestPath.Full 'Authoring manifest'

if ([string]$graph.Schema -ne 'hecton8.h8graph.draft.v1') {
    Fail 'Graphs/main.h8graph.json Schema must be hecton8.h8graph.draft.v1.'
}
if ([string]$graph.Runtime -ne 'envelope-only') {
    Fail 'Graphs/main.h8graph.json Runtime must be envelope-only.'
}

$nodesProperty = $graph.PSObject.Properties['Nodes']
if ($null -eq $nodesProperty -or $null -eq $nodesProperty.Value -or -not $nodesProperty.Value.GetType().IsArray) {
    Fail 'Graphs/main.h8graph.json Nodes must be a JSON array.'
}

$sourceNodes = @($nodesProperty.Value)
$nodes = New-Object 'System.Collections.Generic.List[object]'
$replaced = $false
for ($i = 0; $i -lt $sourceNodes.Count; $i++) {
    $existingNode = $sourceNodes[$i]
    if ($null -eq $existingNode) { Fail ('Graphs/main.h8graph.json Nodes[' + $i + '] must not be null.') }
    $existingId = Validate-NodeId ([string]$existingNode.Id) ('Graphs/main.h8graph.json Nodes[' + $i + '] Id')
    if ($existingId -eq $newNode.Id) {
        if (-not $Replace) {
            Fail ('Graph node already exists: ' + $newNode.Id + '. Re-run with -Replace only if replacement is intended.')
        }
        [void]$nodes.Add($newNode)
        $replaced = $true
    } else {
        [void]$nodes.Add($existingNode)
    }
}

if (-not $replaced) {
    if ($nodes.Count -ge 256) { Fail 'Graphs/main.h8graph.json Nodes already has 256 entries.' }
    [void]$nodes.Add($newNode)
}

$graphDocument = Build-GraphDocument $graph $nodes.ToArray()
Ensure-ManifestBudget $authoring ([int]$graphDocument.MaxEnvelopesPerFrame)

$graphDirectory = Split-Path -Parent $targetPath.Full
$graphName = [System.IO.Path]::GetFileName($targetPath.Full)
$manifestDirectory = Split-Path -Parent $manifestPath.Full
$manifestName = [System.IO.Path]::GetFileName($manifestPath.Full)
$uniqueSuffix = [System.Guid]::NewGuid().ToString('N')
$graphTempPath = Join-Path $graphDirectory ('.' + $graphName + '.tmp-' + $uniqueSuffix)
$graphBackupPath = Join-Path $graphDirectory ('.' + $graphName + '.previous-' + $uniqueSuffix)
$manifestTempPath = Join-Path $manifestDirectory ('.' + $manifestName + '.tmp-' + $uniqueSuffix)
$manifestBackupPath = Join-Path $manifestDirectory ('.' + $manifestName + '.previous-' + $uniqueSuffix)

try {
    Write-JsonFile $graphTempPath $graphDocument
    Write-JsonFile $manifestTempPath $authoring

    Move-Item -LiteralPath $targetPath.Full -Destination $graphBackupPath -Force
    Move-Item -LiteralPath $manifestPath.Full -Destination $manifestBackupPath -Force
    Move-Item -LiteralPath $graphTempPath -Destination $targetPath.Full -Force
    Move-Item -LiteralPath $manifestTempPath -Destination $manifestPath.Full -Force
    Invoke-StarterValidator $Root

    if (Test-Path -LiteralPath $graphBackupPath -PathType Leaf) {
        Remove-Item -LiteralPath $graphBackupPath -Force
    }
    if (Test-Path -LiteralPath $manifestBackupPath -PathType Leaf) {
        Remove-Item -LiteralPath $manifestBackupPath -Force
    }
} catch {
    foreach ($path in @($targetPath.Full, $manifestPath.Full)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Remove-Item -LiteralPath $path -Force
        }
    }
    if (Test-Path -LiteralPath $graphBackupPath -PathType Leaf) {
        Move-Item -LiteralPath $graphBackupPath -Destination $targetPath.Full -Force
    }
    if (Test-Path -LiteralPath $manifestBackupPath -PathType Leaf) {
        Move-Item -LiteralPath $manifestBackupPath -Destination $manifestPath.Full -Force
    }
    Fail $_.Exception.Message
} finally {
    foreach ($path in @($graphTempPath, $manifestTempPath)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

if ($Json) {
    $payload = [pscustomobject][ordered]@{
        Schema = 'hecton8.graph_node_apply.v1'
        Runtime = 'envelope-only'
        Target = $targetPath.Relative
        Manifest = $manifestPath.Relative
        Snippet = $snippetPath.Relative
        NodeId = $newNode.Id
        Opcode = $newNode.Opcode
        Replaced = $replaced
        GraphBudget = [int]$graphDocument.MaxEnvelopesPerFrame
        ManifestBudget = [int]$authoring.Budgets.MaxEnvelopesPerFrame
    }
    Write-Output ($payload | ConvertTo-Json -Depth 8)
    exit 0
}

Write-Output 'PASS HECTON-8 graph node snippet applied'
Write-Output ('Target: ' + $targetPath.Relative)
Write-Output ('Node Id: ' + $newNode.Id)
Write-Output ('Opcode: ' + $newNode.Opcode)
Write-Output ('Replaced: ' + $replaced)
Write-Output ('MaxEnvelopesPerFrame: ' + [string]$graphDocument.MaxEnvelopesPerFrame)
