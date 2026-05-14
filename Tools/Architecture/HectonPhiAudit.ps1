param(
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scope = Join-Path $root 'Assets/_Project/Scripts'

function Count-Pattern {
    param(
        [string[]]$Files,
        [string]$Pattern
    )

    if ($Files.Count -eq 0) {
        return 0
    }

    $matches = Select-String -LiteralPath $Files -Pattern $Pattern -AllMatches -ErrorAction SilentlyContinue
    $total = 0
    foreach ($match in $matches) {
        $total += $match.Matches.Count
    }

    return $total
}

function Get-DomainName {
    param([string]$Path)

    $relative = Resolve-Path -LiteralPath $Path -Relative
    $parts = $relative -split '[\\/]'
    $scriptsIndex = [Array]::IndexOf($parts, 'Scripts')
    if ($scriptsIndex -lt 0 -or $scriptsIndex + 1 -ge $parts.Length) {
        return '_Unknown'
    }

    $domain = $parts[$scriptsIndex + 1]
    if ($domain.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) {
        return $domain
    }

    return $domain
}

$files = @(Get-ChildItem -LiteralPath $scope -Filter '*.cs' -Recurse -File | Select-Object -ExpandProperty FullName)
$lineCount = 0
foreach ($file in $files) {
    $lineCount += [System.IO.File]::ReadLines($file).Count
}

$counts = [ordered]@{
    CsFiles = $files.Count
    Lines = $lineCount
    SignalBusPush = Count-Pattern $files 'SignalBus\s*<[^>]+>\s*\.\s*Push'
    GlobalRegistryGet = Count-Pattern $files 'GlobalRegistry\s*\.\s*Get\s*<'
    GlobalRegistrySurface = Count-Pattern $files 'GlobalRegistry\s*\.'
    EventPublish = Count-Pattern $files '\bPublish\s*\('
    UnityUpdateMethods = Count-Pattern $files '\b(?:Update|LateUpdate|FixedUpdate)\s*\('
    ISlowTickable = Count-Pattern $files '\bISlowTickable\b'
    IJob = Count-Pattern $files '\bIJob(?:ParallelFor|For|Entity|Chunk)?\b'
    ITickable = Count-Pattern $files '\bITickable\b'
    IFixedTickable = Count-Pattern $files '\bIFixedTickable\b'
    GlobalDataVaultRefs = Count-Pattern $files '\bGlobalDataVault\b'
    NativeArrayRefs = Count-Pattern $files '\bNativeArray\s*<'
    StructDeclarations = Count-Pattern $files '\bstruct\s+\w+'
    StructLayoutAttributes = Count-Pattern $files '\[StructLayout\s*\('
    BinaryBlittableSafe = Count-Pattern $files '\[BinaryBlittableSafe\]'
    StaticInstance = Count-Pattern $files '\bstatic\s+\w+\s+Instance\b|\bInstance\s*\{'
    FindObjectCalls = Count-Pattern $files '\b(?:FindObjectOfType|FindObjectsOfType|GameObject\s*\.\s*Find|FindWithTag)\s*\('
    GetComponentCalls = Count-Pattern $files '\bGetComponent(?:s|InChildren|InParent)?\s*<'
}

$signalDenominator = $counts.SignalBusPush + $counts.GlobalRegistryGet
$narrowIntegration = if ($signalDenominator -gt 0) { $counts.SignalBusPush / $signalDenominator } else { 0.0 }

$riskDenominator = $counts.SignalBusPush + $counts.GlobalRegistrySurface + $counts.EventPublish + $counts.StaticInstance + $counts.FindObjectCalls + $counts.GetComponentCalls
$riskIntegration = if ($riskDenominator -gt 0) { $counts.SignalBusPush / $riskDenominator } else { 0.0 }

$purityDenominator = $counts.UnityUpdateMethods + $counts.ISlowTickable + $counts.IJob
$architecturalPurity = if ($purityDenominator -gt 0) { ($counts.ISlowTickable + $counts.IJob) / $purityDenominator } else { 0.0 }

$expandedPurityDenominator = $counts.UnityUpdateMethods + $counts.ISlowTickable + $counts.IJob + $counts.ITickable + $counts.IFixedTickable
$architecturalPurityExpanded = if ($expandedPurityDenominator -gt 0) { ($counts.ISlowTickable + $counts.IJob + $counts.ITickable + $counts.IFixedTickable) / $expandedPurityDenominator } else { 0.0 }

$dataSovereigntyDenominator = $counts.GlobalDataVaultRefs + $counts.NativeArrayRefs
$dataSovereignty = if ($dataSovereigntyDenominator -gt 0) { $counts.GlobalDataVaultRefs / $dataSovereigntyDenominator } else { 0.0 }

$memoryAlignment = if ($counts.StructDeclarations -gt 0) { $counts.StructLayoutAttributes / $counts.StructDeclarations } else { 0.0 }
$binarySafeRatio = if ($counts.StructDeclarations -gt 0) { $counts.BinaryBlittableSafe / $counts.StructDeclarations } else { 0.0 }

$hPhiStaticNarrow = $narrowIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment
$hPhiStaticRisk = $riskIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment

$domainRows = @()
foreach ($group in ($files | Group-Object { Get-DomainName $_ })) {
    $domainFiles = @($group.Group)
    $domainLines = 0
    foreach ($file in $domainFiles) {
        $domainLines += [System.IO.File]::ReadLines($file).Count
    }

    $domainRows += [pscustomobject]@{
        Domain = $group.Name
        Files = $domainFiles.Count
        Lines = $domainLines
        NativeArrayRefs = Count-Pattern $domainFiles '\bNativeArray\s*<'
        GlobalDataVaultRefs = Count-Pattern $domainFiles '\bGlobalDataVault\b'
        RegistryRefs = Count-Pattern $domainFiles 'GlobalRegistry\s*\.'
        UpdateMethods = Count-Pattern $domainFiles '\b(?:Update|LateUpdate|FixedUpdate)\s*\('
        Structs = Count-Pattern $domainFiles '\bstruct\s+\w+'
        Layout = Count-Pattern $domainFiles '\[StructLayout\s*\('
        BinarySafe = Count-Pattern $domainFiles '\[BinaryBlittableSafe\]'
    }
}

$result = [ordered]@{
    Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
    Scope = 'Assets/_Project/Scripts'
    Counts = $counts
    Scores = [ordered]@{
        NarrowIntegration = [Math]::Round($narrowIntegration, 9)
        RiskIntegration = [Math]::Round($riskIntegration, 9)
        ArchitecturalPurity = [Math]::Round($architecturalPurity, 9)
        ArchitecturalPurityExpanded = [Math]::Round($architecturalPurityExpanded, 9)
        DataSovereignty = [Math]::Round($dataSovereignty, 9)
        MemoryAlignment = [Math]::Round($memoryAlignment, 9)
        BinarySafeRatio = [Math]::Round($binarySafeRatio, 9)
        HPhiStaticNarrow = [Math]::Round($hPhiStaticNarrow, 9)
        HPhiStaticRisk = [Math]::Round($hPhiStaticRisk, 9)
    }
    TopNativeArrayDomains = @($domainRows | Sort-Object NativeArrayRefs -Descending | Select-Object -First 8)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 6
    return
}

Write-Output "Hecton-Phi static audit"
Write-Output "Timestamp: $($result.Timestamp)"
Write-Output "Scope: $($result.Scope)"
Write-Output ''
Write-Output 'Counts:'
$counts.GetEnumerator() | ForEach-Object { Write-Output ("  {0}: {1}" -f $_.Key, $_.Value) }
Write-Output ''
Write-Output 'Scores:'
$result.Scores.GetEnumerator() | ForEach-Object { Write-Output ("  {0}: {1}" -f $_.Key, $_.Value) }
Write-Output ''
Write-Output 'Top NativeArray domains:'
$result.TopNativeArrayDomains | Format-Table -AutoSize
