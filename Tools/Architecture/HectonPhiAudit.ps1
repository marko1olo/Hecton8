param(
    [string]$Scope = "Assets/_Project/Scripts",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$root = (Get-Location).Path
$scopePath = Join-Path $root $Scope
if (-not (Test-Path $scopePath)) {
    throw "Scope not found: $scopePath"
}

$files = Get-ChildItem -Path $scopePath -Recurse -Filter *.cs -File
$counts = [ordered]@{
    CsFiles = $files.Count
    Lines = 0
    SignalBusPush = 0
    GlobalRegistryGet = 0
    GlobalRegistrySurface = 0
    EventPublish = 0
    UnityUpdateMethods = 0
    ISlowTickable = 0
    IJob = 0
    ITickable = 0
    IFixedTickable = 0
    GlobalDataVaultRefs = 0
    NativeArrayRefs = 0
    StructDeclarations = 0
    StructLayoutAttributes = 0
    BinaryBlittableSafe = 0
    StaticInstance = 0
    FindObjectCalls = 0
    GetComponentCalls = 0
}

$domain = @{}

foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    $relative = $file.FullName.Substring($scopePath.Length).TrimStart('\')
    $top = ($relative -split '\\')[0]
    if (-not $domain.ContainsKey($top)) {
        $domain[$top] = [ordered]@{
            Files = 0
            Lines = 0
            NativeArrayRefs = 0
            GlobalDataVaultRefs = 0
            GlobalRegistrySurface = 0
            UpdateMethods = 0
            Structs = 0
            Layout = 0
            BinarySafe = 0
        }
    }

    $lineCount = ([regex]::Matches($text, "`n")).Count + 1
    $counts.Lines += $lineCount
    $domain[$top].Files += 1
    $domain[$top].Lines += $lineCount

    $count = [regex]::Matches($text, 'SignalBus\s*<[^>]+>\s*\.\s*Push\s*\(').Count
    $counts.SignalBusPush += $count

    $count = [regex]::Matches($text, 'GlobalRegistry\s*\.\s*Get\s*<').Count
    $counts.GlobalRegistryGet += $count

    $count = [regex]::Matches($text, 'GlobalRegistry\s*\.').Count
    $counts.GlobalRegistrySurface += $count
    $domain[$top].GlobalRegistrySurface += $count

    $count = [regex]::Matches($text, '\bPublish\s*\(').Count
    $counts.EventPublish += $count

    $count = [regex]::Matches($text, '(?m)^\s*(?:private|public|protected|internal)?\s*(?:async\s+)?void\s+(?:Update|LateUpdate|FixedUpdate)\s*\(').Count
    $counts.UnityUpdateMethods += $count
    $domain[$top].UpdateMethods += $count

    $counts.ISlowTickable += [regex]::Matches($text, '\bISlowTickable\b').Count
    $counts.IJob += [regex]::Matches($text, '\bIJob(?:ParallelFor|Chunk|For)?\b').Count
    $counts.ITickable += [regex]::Matches($text, '\bITickable\b').Count
    $counts.IFixedTickable += [regex]::Matches($text, '\bIFixedTickable\b').Count

    $count = [regex]::Matches($text, '\bGlobalDataVault\b').Count
    $counts.GlobalDataVaultRefs += $count
    $domain[$top].GlobalDataVaultRefs += $count

    $count = [regex]::Matches($text, '\bNativeArray\s*<').Count
    $counts.NativeArrayRefs += $count
    $domain[$top].NativeArrayRefs += $count

    $count = [regex]::Matches($text, '\bstruct\s+\w+').Count
    $counts.StructDeclarations += $count
    $domain[$top].Structs += $count

    $count = [regex]::Matches($text, 'StructLayout\s*\(').Count
    $counts.StructLayoutAttributes += $count
    $domain[$top].Layout += $count

    $count = [regex]::Matches($text, 'BinaryBlittableSafe').Count
    $counts.BinaryBlittableSafe += $count
    $domain[$top].BinarySafe += $count

    $counts.StaticInstance += [regex]::Matches($text, '\bstatic\s+[^;\n]*\bInstance\b').Count
    $counts.FindObjectCalls += [regex]::Matches($text, '\bFindObject(?:OfType|sOfType)?\s*<|\bGameObject\s*\.\s*Find').Count
    $counts.GetComponentCalls += [regex]::Matches($text, '\bGetComponent(?:s|InChildren|InParent)?\s*<').Count
}

$slowJob = $counts.ISlowTickable + $counts.IJob
$tickJob = $counts.ITickable + $counts.IFixedTickable + $counts.ISlowTickable + $counts.IJob

$narrowIntegration = if (($counts.SignalBusPush + $counts.GlobalRegistryGet) -gt 0) {
    $counts.SignalBusPush / ($counts.SignalBusPush + $counts.GlobalRegistryGet)
} else { 0 }

$riskIntegration = if (($counts.SignalBusPush + $counts.EventPublish + $counts.GlobalRegistrySurface) -gt 0) {
    ($counts.SignalBusPush + $counts.EventPublish) / ($counts.SignalBusPush + $counts.EventPublish + $counts.GlobalRegistrySurface)
} else { 0 }

$purity = if (($slowJob + $counts.UnityUpdateMethods) -gt 0) {
    $slowJob / ($slowJob + $counts.UnityUpdateMethods)
} else { 0 }

$purityExpanded = if (($tickJob + $counts.UnityUpdateMethods) -gt 0) {
    $tickJob / ($tickJob + $counts.UnityUpdateMethods)
} else { 0 }

$dataSovereignty = if (($counts.GlobalDataVaultRefs + $counts.NativeArrayRefs) -gt 0) {
    $counts.GlobalDataVaultRefs / ($counts.GlobalDataVaultRefs + $counts.NativeArrayRefs)
} else { 0 }

$alignment = if ($counts.StructDeclarations -gt 0) {
    $counts.StructLayoutAttributes / $counts.StructDeclarations
} else { 0 }

$binarySafeRatio = if ($counts.StructDeclarations -gt 0) {
    $counts.BinaryBlittableSafe / $counts.StructDeclarations
} else { 0 }

$result = [ordered]@{
    Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss K")
    Scope = $Scope
    Counts = $counts
    Scores = [ordered]@{
        NarrowIntegration = [math]::Round($narrowIntegration, 9)
        RiskIntegration = [math]::Round($riskIntegration, 9)
        ArchitecturalPurity = [math]::Round($purity, 9)
        ArchitecturalPurityExpanded = [math]::Round($purityExpanded, 9)
        DataSovereignty = [math]::Round($dataSovereignty, 9)
        MemoryAlignment = [math]::Round($alignment, 9)
        BinarySafeRatio = [math]::Round($binarySafeRatio, 9)
        HPhiStaticNarrow = [math]::Round($narrowIntegration * $purity * $dataSovereignty * $alignment, 9)
        HPhiStaticRisk = [math]::Round($riskIntegration * $purity * $dataSovereignty * $alignment, 9)
    }
    TopNativeArrayDomains = @(
        $domain.GetEnumerator() |
            Sort-Object { $_.Value.NativeArrayRefs } -Descending |
            Select-Object -First 8 |
            ForEach-Object {
                [ordered]@{
                    Domain = $_.Key
                    Files = $_.Value.Files
                    Lines = $_.Value.Lines
                    NativeArrayRefs = $_.Value.NativeArrayRefs
                    GlobalDataVaultRefs = $_.Value.GlobalDataVaultRefs
                    RegistryRefs = $_.Value.GlobalRegistrySurface
                    UpdateMethods = $_.Value.UpdateMethods
                    Structs = $_.Value.Structs
                    Layout = $_.Value.Layout
                    BinarySafe = $_.Value.BinarySafe
                }
            }
    )
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    return
}

"Hecton-Phi static audit"
"Timestamp: $($result.Timestamp)"
"Scope: $Scope"
"Files: $($counts.CsFiles)"
"Lines: $($counts.Lines)"
""
"Scores:"
"  NarrowIntegration: $($result.Scores.NarrowIntegration)"
"  RiskIntegration: $($result.Scores.RiskIntegration)"
"  ArchitecturalPurity: $($result.Scores.ArchitecturalPurity)"
"  DataSovereignty: $($result.Scores.DataSovereignty)"
"  MemoryAlignment: $($result.Scores.MemoryAlignment)"
"  BinarySafeRatio: $($result.Scores.BinarySafeRatio)"
"  HPhiStaticNarrow: $($result.Scores.HPhiStaticNarrow)"
"  HPhiStaticRisk: $($result.Scores.HPhiStaticRisk)"
""
"Top NativeArray domains:"
foreach ($entry in $result.TopNativeArrayDomains) {
    "  $($entry.Domain): NativeArray=$($entry.NativeArrayRefs), Vault=$($entry.GlobalDataVaultRefs), StructLayout=$($entry.Layout)/$($entry.Structs)"
}
