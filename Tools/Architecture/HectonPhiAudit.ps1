param(
    [switch]$Json,
    [switch]$Summary,
    [switch]$CoreGraphOnly,
    [switch]$RequireCoreBuildGate,
    [int]$MaxCoreAsmdefDebtReferences = -1,
    [int]$MaxGeneratedProjectDebtReferences = -1,
    [int]$MaxSourceBackedBridgeDebtReferences = -1
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scope = Join-Path $root 'Assets/_Project/Scripts'
$regexOptions = [System.Text.RegularExpressions.RegexOptions]::Compiled -bor
    [System.Text.RegularExpressions.RegexOptions]::Multiline

function New-CounterSet {
    [ordered]@{
        CsFiles = 0
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
        DisposeCalls = 0
    }
}

function New-DomainRow {
    param([string]$Domain)

    [ordered]@{
        Domain = $Domain
        Files = 0
        Lines = 0
        NativeArrayRefs = 0
        GlobalDataVaultRefs = 0
        RegistryRefs = 0
        UpdateMethods = 0
        Structs = 0
        Layout = 0
        BinarySafe = 0
        FindObjectCalls = 0
        GetComponentCalls = 0
        DisposeCalls = 0
    }
}

function New-FileRow {
    param(
        [string]$RelativePath,
        [string]$Domain,
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [int]$LineCount
    )

    [ordered]@{
        File = $RelativePath
        Domain = $Domain
        Lines = $LineCount
        NativeArrayRefs = [int]$Counters['NativeArrayRefs']
        GlobalDataVaultRefs = [int]$Counters['GlobalDataVaultRefs']
        DisposeCalls = [int]$Counters['DisposeCalls']
        FindObjectCalls = [int]$Counters['FindObjectCalls']
        GetComponentCalls = [int]$Counters['GetComponentCalls']
    }
}

function Count-Lines {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return 0
    }

    $lineCount = 1
    for ($i = 0; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq [char]10) {
            $lineCount++
        }
    }

    if ($Text[$Text.Length - 1] -eq [char]10) {
        $lineCount--
    }

    return $lineCount
}

function Remove-UnityEditorBlocks {
    param([string]$Text)

    if ($Text.IndexOf('UNITY_EDITOR', [StringComparison]::Ordinal) -lt 0) {
        return $Text
    }

    $builder = [System.Text.StringBuilder]::new($Text.Length)
    $lines = $Text -split "`r?`n", -1
    $depth = 0
    $skipDepth = -1

    foreach ($line in $lines) {
        $trimmed = $line.Trim()

        if ($trimmed -match '^#\s*if\b') {
            $depth++
            if ($skipDepth -lt 0 -and $trimmed -match '\bUNITY_EDITOR\b') {
                $skipDepth = $depth
                continue
            }

            if ($skipDepth -lt 0) {
                [void]$builder.AppendLine($line)
            }

            continue
        }

        if ($trimmed -match '^#\s*(?:elif|else)\b') {
            if ($skipDepth -eq $depth) {
                $skipDepth = -1
                continue
            }

            if ($skipDepth -lt 0 -and $trimmed -match '^#\s*elif\b' -and $trimmed -match '\bUNITY_EDITOR\b') {
                $skipDepth = $depth
                continue
            }

            if ($skipDepth -lt 0) {
                [void]$builder.AppendLine($line)
            }

            continue
        }

        if ($trimmed -match '^#\s*endif\b') {
            if ($skipDepth -eq $depth) {
                $skipDepth = -1
                $depth--
                continue
            }

            if ($skipDepth -lt 0) {
                [void]$builder.AppendLine($line)
            }

            $depth--
            continue
        }

        if ($skipDepth -lt 0) {
            [void]$builder.AppendLine($line)
        }
    }

    return $builder.ToString()
}

function Get-DomainName {
    param([string]$Path)

    $relative = Get-RelativeSourcePath $Path
    $parts = $relative -split '[\\/]'
    if ($parts.Length -eq 0) {
        return '_Unknown'
    }

    $domain = $parts[0]
    if ($domain.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase)) {
        return $domain
    }

    return $domain
}

function Get-RelativeSourcePath {
    param([string]$Path)

    return $Path.Substring($scope.Length).TrimStart([char]'\', [char]'/')
}

function Test-IsEditorFile {
    param([string]$Path)

    return $Path -match '[\\/]Editor[\\/]'
}

function ConvertTo-RelativeProjectPath {
    param([string]$Path)

    return $Path.Substring($root.Length).TrimStart([char]'\', [char]'/')
}

function Get-CoreReferenceKind {
    param([string]$Reference)

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return 'Empty'
    }

    if ($Reference -eq 'Hecton8.Core.Contracts' -or
        $Reference.StartsWith('Hecton8.Core.', [StringComparison]::Ordinal)) {
        return 'CoreFamily'
    }

    if ($Reference -eq 'Unity.Mathematics' -or
        $Reference -eq 'Unity.Burst' -or
        $Reference -eq 'Unity.Collections') {
        return 'MathNative'
    }

    if ($Reference -eq 'Hecton8.Bootstrap.Contracts' -or
        $Reference.EndsWith('.Contracts', [StringComparison]::Ordinal)) {
        return 'Contract'
    }

    if ($Reference.StartsWith('Unity.', [StringComparison]::Ordinal) -or
        $Reference.StartsWith('UnityEngine.', [StringComparison]::Ordinal) -or
        $Reference -eq 'GPUInstancer' -or
        $Reference -eq 'Crest' -or
        $Reference.StartsWith('WaveHarmonic.', [StringComparison]::Ordinal) -or
        $Reference.StartsWith('EasySave', [StringComparison]::Ordinal) -or
        $Reference.StartsWith('Volumetric', [StringComparison]::Ordinal) -or
        $Reference.StartsWith('Shapes', [StringComparison]::Ordinal) -or
        $Reference -eq 'Unity.TextMeshPro') {
        return 'PackageOrUnity'
    }

    if ($Reference.StartsWith('Hecton8.', [StringComparison]::Ordinal)) {
        return 'LeafDomain'
    }

    return 'Other'
}

function Get-ProjectReferenceKind {
    param([string]$Reference)

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        return 'Empty'
    }

    $name = [System.IO.Path]::GetFileNameWithoutExtension($Reference)

    if ($name.StartsWith('Hecton8.Core', [StringComparison]::Ordinal) -or
        $name.EndsWith('.Contracts', [StringComparison]::Ordinal)) {
        return 'ContractOrCore'
    }

    if ($name.StartsWith('Hecton8.', [StringComparison]::Ordinal)) {
        return 'FirstPartyLeaf'
    }

    return 'PackageOrGenerated'
}

function Get-CoreGraphAudit {
    $coreAsmdefPath = Join-Path $scope 'Hecton8.Core.asmdef'
    $coreCsprojPath = Join-Path $root 'Hecton8.Core.csproj'
    $propsPath = Join-Path $root 'Directory.Build.props'
    $targetsPath = Join-Path $root 'Directory.Build.targets'

    $asmdefRows = @()
    if (Test-Path -LiteralPath $coreAsmdefPath) {
        $coreAsmdef = Get-Content -LiteralPath $coreAsmdefPath -Raw | ConvertFrom-Json
        foreach ($reference in @($coreAsmdef.references)) {
            $kind = Get-CoreReferenceKind $reference
            $asmdefRows += [pscustomobject][ordered]@{
                Reference = $reference
                Kind = $kind
                IsHPhiDebt = ($kind -eq 'LeafDomain' -or $kind -eq 'PackageOrUnity' -or $kind -eq 'Other')
            }
        }
    }

    $projectRows = @()
    if (Test-Path -LiteralPath $coreCsprojPath) {
        $coreProject = [xml](Get-Content -LiteralPath $coreCsprojPath -Raw)
        foreach ($node in @($coreProject.SelectNodes('//ProjectReference'))) {
            $include = [string]$node.Include
            $kind = Get-ProjectReferenceKind $include
            $projectRows += [pscustomobject][ordered]@{
                Reference = $include
                Kind = $kind
                IsHPhiDebt = ($kind -eq 'FirstPartyLeaf' -or $kind -eq 'PackageOrGenerated')
            }
        }
    }

    $bridgeRows = @()
    if (Test-Path -LiteralPath $targetsPath) {
        $targetsProject = [xml](Get-Content -LiteralPath $targetsPath -Raw)
        foreach ($itemGroup in @($targetsProject.Project.ItemGroup)) {
            $condition = [string]$itemGroup.Condition
            if ($condition.IndexOf("`$(MSBuildProjectName)' == 'Hecton8.Core'", [StringComparison]::Ordinal) -lt 0) {
                continue
            }

            foreach ($node in @($itemGroup.Reference)) {
                $include = [string]$node.Include
                $kind = Get-CoreReferenceKind $include
                $bridgeRows += [pscustomobject][ordered]@{
                    Reference = $include
                    Kind = $kind
                    IsHPhiDebt = ($kind -eq 'LeafDomain' -or $kind -eq 'PackageOrUnity' -or $kind -eq 'Other')
                }
            }
        }
    }

    $propsText = ''
    if (Test-Path -LiteralPath $propsPath) {
        $propsText = Get-Content -LiteralPath $propsPath -Raw
    }

    $hasCoreCondition = $propsText.IndexOf("'`$(MSBuildProjectName)' == 'Hecton8.Core'", [StringComparison]::Ordinal) -ge 0
    $coreGatePresent =
        $hasCoreCondition -and
        $propsText.IndexOf('<BuildProjectReferences>false</BuildProjectReferences>', [StringComparison]::Ordinal) -ge 0

    $coreParallelGatePresent =
        $hasCoreCondition -and
        $propsText.IndexOf('<BuildInParallel>false</BuildInParallel>', [StringComparison]::Ordinal) -ge 0

    $asmdefDebtRows = @($asmdefRows | Where-Object { $_.IsHPhiDebt })
    $projectDebtRows = @($projectRows | Where-Object { $_.IsHPhiDebt })
    $bridgeDebtRows = @($bridgeRows | Where-Object { $_.IsHPhiDebt })

    [ordered]@{
        CoreAsmdef = if (Test-Path -LiteralPath $coreAsmdefPath) { ConvertTo-RelativeProjectPath $coreAsmdefPath } else { 'MISSING' }
        CoreProject = if (Test-Path -LiteralPath $coreCsprojPath) { ConvertTo-RelativeProjectPath $coreCsprojPath } else { 'MISSING' }
        SourceBackedTargets = if (Test-Path -LiteralPath $targetsPath) { ConvertTo-RelativeProjectPath $targetsPath } else { 'MISSING' }
        BuildGraphGate = [ordered]@{
            CoreBuildProjectReferencesDisabledByDefault = $coreGatePresent
            CoreBuildInParallelDisabledByDefault = $coreParallelGatePresent
            OptInProperty = 'HectonBuildProjectReferences=true'
        }
        Counts = [ordered]@{
            CoreAsmdefReferenceCount = @($asmdefRows).Count
            CoreAsmdefDebtReferenceCount = @($asmdefDebtRows).Count
            GeneratedProjectReferenceCount = @($projectRows).Count
            GeneratedProjectDebtReferenceCount = @($projectDebtRows).Count
            SourceBackedBridgeReferenceCount = @($bridgeRows).Count
            SourceBackedBridgeDebtReferenceCount = @($bridgeDebtRows).Count
        }
        CoreAsmdefReferences = @($asmdefRows)
        CoreAsmdefDebtReferences = @($asmdefDebtRows)
        GeneratedProjectReferences = @($projectRows)
        GeneratedProjectDebtReferences = @($projectDebtRows)
        SourceBackedBridgeReferences = @($bridgeRows)
        SourceBackedBridgeDebtReferences = @($bridgeDebtRows)
    }
}

function Assert-CoreGraphBudget {
    param([System.Collections.Specialized.OrderedDictionary]$Audit)

    $violations = [System.Collections.Generic.List[string]]::new()
    $gate = $Audit.BuildGraphGate
    $counts = $Audit.Counts

    if ($RequireCoreBuildGate -and
        (-not $gate.CoreBuildProjectReferencesDisabledByDefault -or
         -not $gate.CoreBuildInParallelDisabledByDefault)) {
        [void]$violations.Add('Core build graph gate is incomplete.')
    }

    if ($MaxCoreAsmdefDebtReferences -ge 0 -and
        [int]$counts.CoreAsmdefDebtReferenceCount -gt $MaxCoreAsmdefDebtReferences) {
        [void]$violations.Add((
            'Core asmdef H-Phi debt refs {0} exceed budget {1}.' -f
            $counts.CoreAsmdefDebtReferenceCount,
            $MaxCoreAsmdefDebtReferences))
    }

    if ($MaxGeneratedProjectDebtReferences -ge 0 -and
        [int]$counts.GeneratedProjectDebtReferenceCount -gt $MaxGeneratedProjectDebtReferences) {
        [void]$violations.Add((
            'Generated Core project H-Phi debt refs {0} exceed budget {1}.' -f
            $counts.GeneratedProjectDebtReferenceCount,
            $MaxGeneratedProjectDebtReferences))
    }

    if ($MaxSourceBackedBridgeDebtReferences -ge 0 -and
        [int]$counts.SourceBackedBridgeDebtReferenceCount -gt $MaxSourceBackedBridgeDebtReferences) {
        [void]$violations.Add((
            'Source-backed Core bridge H-Phi debt refs {0} exceed budget {1}.' -f
            $counts.SourceBackedBridgeDebtReferenceCount,
            $MaxSourceBackedBridgeDebtReferences))
    }

    if ($violations.Count -le 0) {
        return
    }

    throw (
        "Core graph H-Phi budget failed with $($violations.Count) violation(s):`n" +
        ($violations -join "`n"))
}

function Add-Count {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [string]$Name,
        [int]$Value
    )

    $Counters[$Name] = [int]$Counters[$Name] + $Value
}

function Add-PatternCounts {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [string]$Content,
        [hashtable]$Patterns
    )

    foreach ($entry in $Patterns.GetEnumerator()) {
        Add-Count $Counters $entry.Key $entry.Value.Matches($Content).Count
    }
}

function Add-DomainMetrics {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Row,
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [int]$LineCount
    )

    $Row['Files'] = [int]$Row['Files'] + 1
    $Row['Lines'] = [int]$Row['Lines'] + $LineCount
    $Row['NativeArrayRefs'] = [int]$Row['NativeArrayRefs'] + [int]$Counters['NativeArrayRefs']
    $Row['GlobalDataVaultRefs'] = [int]$Row['GlobalDataVaultRefs'] + [int]$Counters['GlobalDataVaultRefs']
    $Row['RegistryRefs'] = [int]$Row['RegistryRefs'] + [int]$Counters['GlobalRegistrySurface']
    $Row['UpdateMethods'] = [int]$Row['UpdateMethods'] + [int]$Counters['UnityUpdateMethods']
    $Row['Structs'] = [int]$Row['Structs'] + [int]$Counters['StructDeclarations']
    $Row['Layout'] = [int]$Row['Layout'] + [int]$Counters['StructLayoutAttributes']
    $Row['BinarySafe'] = [int]$Row['BinarySafe'] + [int]$Counters['BinaryBlittableSafe']
    $Row['FindObjectCalls'] = [int]$Row['FindObjectCalls'] + [int]$Counters['FindObjectCalls']
    $Row['GetComponentCalls'] = [int]$Row['GetComponentCalls'] + [int]$Counters['GetComponentCalls']
    $Row['DisposeCalls'] = [int]$Row['DisposeCalls'] + [int]$Counters['DisposeCalls']
}

function Divide-OrZero {
    param(
        [double]$Numerator,
        [double]$Denominator
    )

    if ($Denominator -gt 0.0) {
        return $Numerator / $Denominator
    }

    return 0.0
}

function New-Scores {
    param([System.Collections.Specialized.OrderedDictionary]$Counts)

    $signalDenominator = [double]$Counts.SignalBusPush + [double]$Counts.GlobalRegistryGet
    $narrowIntegration = Divide-OrZero $Counts.SignalBusPush $signalDenominator

    $riskDenominator =
        [double]$Counts.SignalBusPush +
        [double]$Counts.GlobalRegistrySurface +
        [double]$Counts.EventPublish +
        [double]$Counts.StaticInstance +
        [double]$Counts.FindObjectCalls +
        [double]$Counts.GetComponentCalls
    $riskIntegration = Divide-OrZero $Counts.SignalBusPush $riskDenominator

    $purityDenominator =
        [double]$Counts.UnityUpdateMethods +
        [double]$Counts.ISlowTickable +
        [double]$Counts.IJob
    $architecturalPurity = Divide-OrZero ($Counts.ISlowTickable + $Counts.IJob) $purityDenominator

    $expandedPurityDenominator =
        [double]$Counts.UnityUpdateMethods +
        [double]$Counts.ISlowTickable +
        [double]$Counts.IJob +
        [double]$Counts.ITickable +
        [double]$Counts.IFixedTickable
    $architecturalPurityExpanded = Divide-OrZero `
        ($Counts.ISlowTickable + $Counts.IJob + $Counts.ITickable + $Counts.IFixedTickable) `
        $expandedPurityDenominator

    $dataSovereigntyDenominator = [double]$Counts.GlobalDataVaultRefs + [double]$Counts.NativeArrayRefs
    $dataSovereignty = Divide-OrZero $Counts.GlobalDataVaultRefs $dataSovereigntyDenominator

    $memoryAlignment = Divide-OrZero $Counts.StructLayoutAttributes $Counts.StructDeclarations
    $binarySafeRatio = Divide-OrZero $Counts.BinaryBlittableSafe $Counts.StructDeclarations

    $hPhiStaticNarrow = $narrowIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment
    $hPhiStaticRisk = $riskIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment

    [ordered]@{
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
}

function New-CoreGraphSummary {
    param([System.Collections.Specialized.OrderedDictionary]$CoreGraphAudit)

    [ordered]@{
        CoreAsmdef = $CoreGraphAudit.CoreAsmdef
        CoreProject = $CoreGraphAudit.CoreProject
        BuildGraphGate = $CoreGraphAudit.BuildGraphGate
        Counts = $CoreGraphAudit.Counts
        CoreAsmdefDebtReferences = @($CoreGraphAudit.CoreAsmdefDebtReferences |
            Select-Object -ExpandProperty Reference)
        GeneratedProjectDebtReferences = @($CoreGraphAudit.GeneratedProjectDebtReferences |
            Select-Object -ExpandProperty Reference)
    }
}

function New-AuditSummary {
    param([System.Collections.Specialized.OrderedDictionary]$Audit)

    [ordered]@{
        Timestamp = $Audit.Timestamp
        Scope = $Audit.Scope
        EvidenceClass = 'STATIC_SOURCE'
        Scores = [ordered]@{
            RuntimeHPhiNarrow = $Audit.Scores.HPhiStaticNarrow
            RuntimeHPhiRisk = $Audit.Scores.HPhiStaticRisk
            AllSourceHPhiNarrow = $Audit.AllSourceScores.HPhiStaticNarrow
            AllSourceHPhiRisk = $Audit.AllSourceScores.HPhiStaticRisk
            RiskIntegration = $Audit.Scores.RiskIntegration
            ArchitecturalPurity = $Audit.Scores.ArchitecturalPurity
            DataSovereignty = $Audit.Scores.DataSovereignty
            MemoryAlignment = $Audit.Scores.MemoryAlignment
            BinarySafeRatio = $Audit.Scores.BinarySafeRatio
        }
        Counts = [ordered]@{
            RuntimeFiles = $Audit.Counts.CsFiles
            RuntimeLines = $Audit.Counts.Lines
            SignalBusPush = $Audit.Counts.SignalBusPush
            GlobalRegistrySurface = $Audit.Counts.GlobalRegistrySurface
            EventPublish = $Audit.Counts.EventPublish
            UnityUpdateMethods = $Audit.Counts.UnityUpdateMethods
            DataVaultRefs = $Audit.Counts.GlobalDataVaultRefs
            NativeArrayRefs = $Audit.Counts.NativeArrayRefs
            StructDeclarations = $Audit.Counts.StructDeclarations
            StructLayoutAttributes = $Audit.Counts.StructLayoutAttributes
            FindObjectCalls = $Audit.Counts.FindObjectCalls
            GetComponentCalls = $Audit.Counts.GetComponentCalls
            DisposeCalls = $Audit.Counts.DisposeCalls
        }
        CoreGraph = New-CoreGraphSummary $Audit.CoreGraphAudit
        TopOwnerBlockedDataVaultCandidates = @($Audit.OwnerBlockedDataVaultCandidates |
            Select-Object -First 10)
    }
}

$coreGraphAudit = Get-CoreGraphAudit
Assert-CoreGraphBudget $coreGraphAudit

if ($CoreGraphOnly) {
    $result = [ordered]@{
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
        Scope = 'Core dependency graph'
        MetricModel = 'Fast graph-only H-Phi audit. STATIC_SOURCE only; no compile, Unity import, profiler, or runtime proof.'
        CoreGraphAudit = $coreGraphAudit
    }

    if ($Summary) {
        $summaryResult = [ordered]@{
            Timestamp = $result.Timestamp
            Scope = $result.Scope
            EvidenceClass = 'STATIC_SOURCE'
            CoreGraph = New-CoreGraphSummary $coreGraphAudit
        }

        if ($Json) {
            $summaryResult | ConvertTo-Json -Depth 6
            return
        }

        Write-Output 'Hecton-Phi Core graph summary'
        Write-Output "Timestamp: $($summaryResult.Timestamp)"
        Write-Output 'Counts:'
        [pscustomobject]$summaryResult.CoreGraph.Counts | Format-List
        Write-Output ''
        Write-Output 'Core asmdef H-Phi debt references:'
        $summaryResult.CoreGraph.CoreAsmdefDebtReferences | ForEach-Object { Write-Output ("  {0}" -f $_) }
        Write-Output ''
        Write-Output 'Generated Core project H-Phi debt references:'
        $summaryResult.CoreGraph.GeneratedProjectDebtReferences | ForEach-Object { Write-Output ("  {0}" -f $_) }
        return
    }

    if ($Json) {
        $result | ConvertTo-Json -Depth 6
        return
    }

    Write-Output 'Hecton-Phi Core graph audit'
    Write-Output "Timestamp: $($result.Timestamp)"
    Write-Output "Scope: $($result.Scope)"
    Write-Output "Metric model: $($result.MetricModel)"
    Write-Output ''
    Write-Output 'Build graph gate:'
    [pscustomobject]$coreGraphAudit.BuildGraphGate | Format-List
    Write-Output ''
    Write-Output 'Counts:'
    [pscustomobject]$coreGraphAudit.Counts | Format-List
    Write-Output ''
    Write-Output 'Core asmdef H-Phi debt references:'
    $coreGraphAudit.CoreAsmdefDebtReferences | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Generated Core project H-Phi debt references:'
    $coreGraphAudit.GeneratedProjectDebtReferences | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Source-backed Core bridge H-Phi debt references:'
    $coreGraphAudit.SourceBackedBridgeDebtReferences | Format-Table -AutoSize
    return
}

$patternSource = [ordered]@{
    SignalBusPush = '(?:SignalBus\s*<[^>]+>\s*\.\s*Push|GlobalSignals\s*\.\s*Publish\s*\(|VehicleCommandSignalBus\s*\.\s*Publish\w*\s*\(|PhysicsDeterminismSignals\s*\.\s*Publish\w*\s*\(|FluidFeedbackEvents\s*\.\s*Publish\w*\s*\(|LocalizationEvents\s*\.\s*Publish\w*\s*\(|VoxelChunkModifiedEvents\s*\.\s*Publish\w*\s*\()'
    GlobalRegistryGet = 'GlobalRegistry\s*\.\s*Get\s*<'
    GlobalRegistrySurface = 'GlobalRegistry\s*\.'
    EventPublish = '\b(?:HectonEventBus|WaterTransitionEvents|SuitDamageEvents)\s*\.\s*Publish\w*\s*\('
    UnityUpdateMethods = '^\s*(?:public|private|protected|internal)?\s*(?:static\s+)?void\s+(?:Update|LateUpdate|FixedUpdate)\s*\('
    ISlowTickable = '\bISlowTickable\b'
    IJob = '\bIJob(?:ParallelFor|For|Entity|Chunk)?\b'
    ITickable = '\bITickable\b'
    IFixedTickable = '\bIFixedTickable\b'
    GlobalDataVaultRefs = '\b(?:GlobalDataVault|IDataVault|VaultBufferHandle\s*<|GetBuffer\s*<|TryGetBuffer\s*(?:<|\()|GetBufferHandle\s*<|TryGetBufferHandle\s*<|ResolveBuffer\s*<)'
    NativeArrayRefs = '\bNativeArray\s*<'
    StructDeclarations = '\bstruct\s+\w+'
    StructLayoutAttributes = '\[StructLayout\s*\('
    BinaryBlittableSafe = '\[BinaryBlittableSafe\]'
    StaticInstance = '\bstatic\s+\w+\s+Instance\b|\bInstance\s*\{'
    FindObjectCalls = '\b(?:FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindWithTag)\s*(?:<|\()|GameObject\s*\.\s*Find(?:GameObjectWithTag|WithTag)?\s*\(|Resources\s*\.\s*FindObjectsOfTypeAll\s*(?:<|\()'
    GetComponentCalls = '\bGetComponent(?:s|InChildren|InParent)?\s*<'
    DisposeCalls = '\.Dispose\s*\('
}

$patterns = @{}
foreach ($entry in $patternSource.GetEnumerator()) {
    $patterns[$entry.Key] = [System.Text.RegularExpressions.Regex]::new(
        $entry.Value,
        $regexOptions)
}

$allCounters = New-CounterSet
$runtimeCounters = New-CounterSet
$editorCounters = New-CounterSet
$runtimeDomainRows = @{}
$editorDomainRows = @{}
$runtimeFileRows = [System.Collections.Generic.List[object]]::new()
$editorFileRows = [System.Collections.Generic.List[object]]::new()

$files = @(Get-ChildItem -LiteralPath $scope -Filter '*.cs' -Recurse -File |
    Sort-Object FullName |
    Select-Object -ExpandProperty FullName)

foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file)
    $lineCount = Count-Lines $content
    $isEditorFile = Test-IsEditorFile $file
    $fileCounters = New-CounterSet
    Add-Count $fileCounters 'CsFiles' 1
    Add-Count $fileCounters 'Lines' $lineCount
    Add-PatternCounts $fileCounters $content $patterns

    foreach ($key in $fileCounters.Keys) {
        Add-Count $allCounters $key $fileCounters[$key]
    }

    $domain = Get-DomainName $file
    $relativePath = Get-RelativeSourcePath $file
    if ($isEditorFile) {
        foreach ($key in $fileCounters.Keys) {
            Add-Count $editorCounters $key $fileCounters[$key]
        }

        if (-not $editorDomainRows.ContainsKey($domain)) {
            $editorDomainRows[$domain] = New-DomainRow $domain
        }

        Add-DomainMetrics $editorDomainRows[$domain] $fileCounters $lineCount
        if ([int]$fileCounters['NativeArrayRefs'] -gt 0 -or
            [int]$fileCounters['GlobalDataVaultRefs'] -gt 0 -or
            [int]$fileCounters['FindObjectCalls'] -gt 0) {
            [void]$editorFileRows.Add([pscustomobject](New-FileRow $relativePath $domain $fileCounters $lineCount))
        }
        continue
    }

    if ($content.IndexOf('UNITY_EDITOR', [StringComparison]::Ordinal) -lt 0) {
        $runtimeFileCounters = $fileCounters
    }
    else {
        $runtimeContent = Remove-UnityEditorBlocks $content
        $runtimeFileCounters = New-CounterSet
        Add-Count $runtimeFileCounters 'CsFiles' 1
        Add-Count $runtimeFileCounters 'Lines' $lineCount
        Add-PatternCounts $runtimeFileCounters $runtimeContent $patterns
    }

    foreach ($key in $fileCounters.Keys) {
        Add-Count $runtimeCounters $key $runtimeFileCounters[$key]
    }

    if (-not $runtimeDomainRows.ContainsKey($domain)) {
        $runtimeDomainRows[$domain] = New-DomainRow $domain
    }

    Add-DomainMetrics $runtimeDomainRows[$domain] $runtimeFileCounters $lineCount
    if ([int]$runtimeFileCounters['NativeArrayRefs'] -gt 0 -or
        [int]$runtimeFileCounters['GlobalDataVaultRefs'] -gt 0 -or
        [int]$runtimeFileCounters['FindObjectCalls'] -gt 0) {
        [void]$runtimeFileRows.Add([pscustomobject](New-FileRow $relativePath $domain $runtimeFileCounters $lineCount))
    }
}

$runtimeScores = New-Scores $runtimeCounters
$allSourceScores = New-Scores $allCounters
$editorScores = New-Scores $editorCounters

$result = [ordered]@{
    Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
    Scope = 'Assets/_Project/Scripts'
    MetricModel = 'Runtime H-Phi excludes Scripts/Editor from runtime debt counters; Data Sovereignty counts DataVault access surface including IDataVault, VaultBufferHandle, GetBuffer, TryGetBuffer, and GlobalDataVault; AllSourceCounts is retained for hygiene tracking.'
    CoreGraphAudit = $coreGraphAudit
    Counts = $runtimeCounters
    Scores = $runtimeScores
    AllSourceCounts = $allCounters
    AllSourceScores = $allSourceScores
    EditorCounts = $editorCounters
    EditorScores = $editorScores
    TopNativeArrayDomains = @($runtimeDomainRows.Values |
        ForEach-Object { [pscustomobject]$_ } |
        Sort-Object NativeArrayRefs -Descending |
        Select-Object -First 8)
    TopNativeArrayFiles = @($runtimeFileRows |
        Sort-Object NativeArrayRefs -Descending |
        Select-Object -First 25)
    OwnerBlockedDataVaultCandidates = @($runtimeFileRows |
        Where-Object { $_.NativeArrayRefs -gt 0 -and $_.GlobalDataVaultRefs -eq 0 } |
        Sort-Object NativeArrayRefs -Descending |
        Select-Object -First 25)
    TopEditorNativeArrayDomains = @($editorDomainRows.Values |
        ForEach-Object { [pscustomobject]$_ } |
        Sort-Object NativeArrayRefs -Descending |
        Select-Object -First 4)
    TopEditorNativeArrayFiles = @($editorFileRows |
        Sort-Object NativeArrayRefs -Descending |
        Select-Object -First 10)
}

if ($Summary) {
    $summaryResult = New-AuditSummary $result

    if ($Json) {
        $summaryResult | ConvertTo-Json -Depth 6
        return
    }

    Write-Output 'Hecton-Phi static summary'
    Write-Output "Timestamp: $($summaryResult.Timestamp)"
    Write-Output "Evidence class: $($summaryResult.EvidenceClass)"
    Write-Output ''
    Write-Output 'Scores:'
    [pscustomobject]$summaryResult.Scores | Format-List
    Write-Output ''
    Write-Output 'Counts:'
    [pscustomobject]$summaryResult.Counts | Format-List
    Write-Output ''
    Write-Output 'Core graph H-Phi debt counts:'
    [pscustomobject]$summaryResult.CoreGraph.Counts | Format-List
    Write-Output ''
    Write-Output 'Top owner-blocked DataVault candidate files:'
    $summaryResult.TopOwnerBlockedDataVaultCandidates | Format-Table -AutoSize
    return
}

if ($Json) {
    $result | ConvertTo-Json -Depth 6
    return
}

Write-Output 'Hecton-Phi static audit'
Write-Output "Timestamp: $($result.Timestamp)"
Write-Output "Scope: $($result.Scope)"
Write-Output "Metric model: $($result.MetricModel)"
Write-Output ''
Write-Output 'Runtime counts:'
$runtimeCounters.GetEnumerator() | ForEach-Object { Write-Output ("  {0}: {1}" -f $_.Key, $_.Value) }
Write-Output ''
Write-Output 'Runtime scores:'
$runtimeScores.GetEnumerator() | ForEach-Object { Write-Output ("  {0}: {1}" -f $_.Key, $_.Value) }
Write-Output ''
Write-Output 'All-source scores:'
$allSourceScores.GetEnumerator() | ForEach-Object { Write-Output ("  {0}: {1}" -f $_.Key, $_.Value) }
Write-Output ''
Write-Output 'Core graph H-Phi gate:'
[pscustomobject]$coreGraphAudit.BuildGraphGate | Format-List
Write-Output 'Core graph H-Phi debt counts:'
[pscustomobject]$coreGraphAudit.Counts | Format-List
Write-Output ''
Write-Output 'Top runtime NativeArray domains:'
$result.TopNativeArrayDomains | Format-Table -AutoSize
Write-Output ''
Write-Output 'Owner-blocked DataVault candidate files:'
$result.OwnerBlockedDataVaultCandidates | Format-Table -AutoSize
