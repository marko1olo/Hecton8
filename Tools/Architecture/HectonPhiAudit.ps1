param(
    [switch]$Json,
    [switch]$Summary,
    [switch]$LexicalScrub,
    [switch]$CoreGraphOnly,
    [switch]$IncludeUnusedCoreReferenceScan,
    [switch]$RequireCoreBuildGate,
    [int]$MaxCoreAsmdefDebtReferences = -1,
    [int]$MaxGeneratedProjectDebtReferences = -1,
    [int]$MaxSourceBackedBridgeDebtReferences = -1,
    [int]$MaxSourceBackedCompileBridgeDebtReferences = -1,
    [int]$MaxProjectReferenceReplacementDebtReferences = -1,
    [int]$MaxAupPrecisionRisk = -1,
    [int]$MaxFindObjectCalls = -1,
    [int]$MaxLegacyEventPublish = -1,
    [int]$MaxDuplicateSignalNames = -1,
    [int]$MaxUnityUpdateMethods = -1,
    [int]$MaxGlobalRegistrySurface = -1,
    [int]$MaxGetComponentCalls = -1,
    [int]$MaxNativeArrayRefs = -1,
    [int]$MaxLinqSurface = -1,
    [int]$MaxCoroutineSurface = -1,
    [int]$MaxManagedFormatSurface = -1,
    [int]$MaxJobCompleteSurface = -1,
    [int]$MaxPrimaryManagedRuntimeRisk = -1,
    [double]$MinDataSovereignty = -1.0,
    [double]$MinMemoryAlignment = -1.0,
    [double]$MinRuntimeHPhiRisk = -1.0
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$scope = Join-Path $root 'Assets/_Project/Scripts'
$regexOptions = [System.Text.RegularExpressions.RegexOptions]::Compiled -bor
    [System.Text.RegularExpressions.RegexOptions]::Multiline
$codeSurfaceRegexOptions = [System.Text.RegularExpressions.RegexOptions]::Compiled -bor
    [System.Text.RegularExpressions.RegexOptions]::Singleline
$codeSurfaceNoiseRegex = [System.Text.RegularExpressions.Regex]::new(
    '(//[^\r\n]*|/\*.*?\*/|(?:\$*)"{3,}.*?"{3,}|(?:\$@|@\$|@)"(?:""|[^"])*"|\$?"(?:\\.|[^"\\\r\n])*"|''(?:\\.|[^''\\\r\n])*'')',
    $codeSurfaceRegexOptions)

function New-CounterSet {
    [ordered]@{
        CsFiles = 0
        Lines = 0
        SignalBusPush = 0
        GlobalRegistryGet = 0
        GlobalRegistrySurface = 0
        EventPublish = 0
        UnityUpdateMethodsRaw = 0
        UnityLoopShellMethods = 0
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
        AupPrecisionSafe = 0
        AupPrecisionRisk = 0
        LinqSurface = 0
        CoroutineSurface = 0
        ManagedFormatSurface = 0
        JobCompleteSurface = 0
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
        UnityUpdateMethodsRaw = 0
        UnityLoopShellMethods = 0
        UpdateMethods = 0
        Structs = 0
        Layout = 0
        BinarySafe = 0
        FindObjectCalls = 0
        GetComponentCalls = 0
        DisposeCalls = 0
        AupPrecisionSafe = 0
        AupPrecisionRisk = 0
        LinqSurface = 0
        CoroutineSurface = 0
        ManagedFormatSurface = 0
        JobCompleteSurface = 0
    }
}

function Get-FileRiskRole {
    param([string]$RelativePath)

    if ([string]::IsNullOrEmpty($RelativePath)) {
        return 'PrimaryRuntime'
    }

    $path = $RelativePath.Replace('\', '/')
    $fileName = [System.IO.Path]::GetFileNameWithoutExtension($RelativePath)

    if ($path -match '(^|/)(Editor)(/|$)') {
        return 'Editor'
    }

    if ($path -match '(^|/)(QA|Dev|Debug|Tests?)(/|$)' -or
        $fileName -match '(Smoke|SmokeTester|Stress|Profiler|Diagnostic|Verification|Validator|Benchmark|Harness|Runner|Test)') {
        return 'Instrumentation'
    }

    if ($fileName -match '(Save|Persistence|Migration|Codec|Storage|Serializer|Deserializer)') {
        return 'Persistence'
    }

    if ($path -match '(^|/)UI/') {
        return 'UI'
    }

    return 'PrimaryRuntime'
}

function New-FileRow {
    param(
        [string]$RelativePath,
        [string]$Domain,
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [int]$LineCount
    )

    $fileRole = Get-FileRiskRole $RelativePath
    $managedRuntimeRisk = [int]$Counters['LinqSurface'] +
        [int]$Counters['CoroutineSurface'] +
        [int]$Counters['ManagedFormatSurface']

    [ordered]@{
        File = $RelativePath
        Domain = $Domain
        FileRole = $fileRole
        Lines = $LineCount
        SignalBusPush = [int]$Counters['SignalBusPush']
        GlobalRegistrySurface = [int]$Counters['GlobalRegistrySurface']
        EventPublish = [int]$Counters['EventPublish']
        UnityUpdateMethodsRaw = [int]$Counters['UnityUpdateMethodsRaw']
        UnityLoopShellMethods = [int]$Counters['UnityLoopShellMethods']
        UnityUpdateMethods = [int]$Counters['UnityUpdateMethods']
        StaticInstance = [int]$Counters['StaticInstance']
        NativeArrayRefs = [int]$Counters['NativeArrayRefs']
        GlobalDataVaultRefs = [int]$Counters['GlobalDataVaultRefs']
        DisposeCalls = [int]$Counters['DisposeCalls']
        FindObjectCalls = [int]$Counters['FindObjectCalls']
        GetComponentCalls = [int]$Counters['GetComponentCalls']
        AupPrecisionSafe = [int]$Counters['AupPrecisionSafe']
        AupPrecisionRisk = [int]$Counters['AupPrecisionRisk']
        LinqSurface = [int]$Counters['LinqSurface']
        CoroutineSurface = [int]$Counters['CoroutineSurface']
        ManagedFormatSurface = [int]$Counters['ManagedFormatSurface']
        JobCompleteSurface = [int]$Counters['JobCompleteSurface']
        ManagedRuntimeRisk = $managedRuntimeRisk
        PrimaryManagedRuntimeRisk = if ($fileRole -eq 'PrimaryRuntime') { $managedRuntimeRisk } else { 0 }
        PrimaryJobCompleteRisk = if ($fileRole -eq 'PrimaryRuntime') { [int]$Counters['JobCompleteSurface'] } else { 0 }
        CouplingRisk = [int]$Counters['GlobalRegistrySurface'] +
            [int]$Counters['EventPublish'] +
            [int]$Counters['StaticInstance'] +
            [int]$Counters['FindObjectCalls'] +
            [int]$Counters['GetComponentCalls']
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

function ConvertTo-CodeSurfaceSlow {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return $Text
    }

    $builder = [System.Text.StringBuilder]::new($Text.Length)
    $length = $Text.Length
    $i = 0

    while ($i -lt $length) {
        $ch = $Text[$i]
        $next = if ($i + 1 -lt $length) { $Text[$i + 1] } else { [char]0 }

        if ($ch -eq '/' -and $next -eq '/') {
            [void]$builder.Append(' ')
            [void]$builder.Append(' ')
            $i += 2
            while ($i -lt $length) {
                $current = $Text[$i]
                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                    $i++
                    break
                }

                [void]$builder.Append(' ')
                $i++
            }

            continue
        }

        if ($ch -eq '/' -and $next -eq '*') {
            [void]$builder.Append(' ')
            [void]$builder.Append(' ')
            $i += 2
            while ($i -lt $length) {
                $current = $Text[$i]
                $after = if ($i + 1 -lt $length) { $Text[$i + 1] } else { [char]0 }
                if ($current -eq '*' -and $after -eq '/') {
                    [void]$builder.Append(' ')
                    [void]$builder.Append(' ')
                    $i += 2
                    break
                }

                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                }
                else {
                    [void]$builder.Append(' ')
                }

                $i++
            }

            continue
        }

        $rawPrefixLength = 0
        $rawQuoteStart = $i
        while ($rawQuoteStart -lt $length -and $Text[$rawQuoteStart] -eq '$') {
            $rawPrefixLength++
            $rawQuoteStart++
        }

        $rawQuoteCount = 0
        while ($rawQuoteStart + $rawQuoteCount -lt $length -and
            $Text[$rawQuoteStart + $rawQuoteCount] -eq '"') {
            $rawQuoteCount++
        }

        if ($rawQuoteCount -ge 3) {
            for ($j = 0; $j -lt $rawPrefixLength + $rawQuoteCount; $j++) {
                [void]$builder.Append(' ')
            }

            $i = $rawQuoteStart + $rawQuoteCount
            while ($i -lt $length) {
                $matchedEnd = $true
                for ($j = 0; $j -lt $rawQuoteCount; $j++) {
                    if ($i + $j -ge $length -or $Text[$i + $j] -ne '"') {
                        $matchedEnd = $false
                        break
                    }
                }

                if ($matchedEnd) {
                    for ($j = 0; $j -lt $rawQuoteCount; $j++) {
                        [void]$builder.Append(' ')
                    }

                    $i += $rawQuoteCount
                    break
                }

                $current = $Text[$i]
                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                }
                else {
                    [void]$builder.Append(' ')
                }

                $i++
            }

            continue
        }

        $verbatimStart = -1
        $verbatimPrefixLength = 0
        if ($ch -eq '@' -and $next -eq '"') {
            $verbatimStart = $i + 1
            $verbatimPrefixLength = 1
        }
        elseif ($ch -eq '$' -and $i + 2 -lt $length -and $Text[$i + 1] -eq '@' -and $Text[$i + 2] -eq '"') {
            $verbatimStart = $i + 2
            $verbatimPrefixLength = 2
        }
        elseif ($ch -eq '@' -and $i + 2 -lt $length -and $Text[$i + 1] -eq '$' -and $Text[$i + 2] -eq '"') {
            $verbatimStart = $i + 2
            $verbatimPrefixLength = 2
        }

        if ($verbatimStart -ge 0) {
            for ($j = 0; $j -lt $verbatimPrefixLength + 1; $j++) {
                [void]$builder.Append(' ')
            }

            $i = $verbatimStart + 1
            while ($i -lt $length) {
                $current = $Text[$i]
                $after = if ($i + 1 -lt $length) { $Text[$i + 1] } else { [char]0 }
                if ($current -eq '"' -and $after -eq '"') {
                    [void]$builder.Append(' ')
                    [void]$builder.Append(' ')
                    $i += 2
                    continue
                }

                if ($current -eq '"') {
                    [void]$builder.Append(' ')
                    $i++
                    break
                }

                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                }
                else {
                    [void]$builder.Append(' ')
                }

                $i++
            }

            continue
        }

        $regularStringStart = -1
        $regularPrefixLength = 0
        if ($ch -eq '"') {
            $regularStringStart = $i
        }
        elseif ($ch -eq '$' -and $next -eq '"') {
            $regularStringStart = $i + 1
            $regularPrefixLength = 1
        }

        if ($regularStringStart -ge 0) {
            for ($j = 0; $j -lt $regularPrefixLength + 1; $j++) {
                [void]$builder.Append(' ')
            }

            $i = $regularStringStart + 1
            while ($i -lt $length) {
                $current = $Text[$i]
                if ($current -eq '\') {
                    [void]$builder.Append(' ')
                    if ($i + 1 -lt $length) {
                        $escaped = $Text[$i + 1]
                        if ($escaped -eq [char]13 -or $escaped -eq [char]10) {
                            [void]$builder.Append($escaped)
                        }
                        else {
                            [void]$builder.Append(' ')
                        }

                        $i += 2
                        continue
                    }
                }

                if ($current -eq '"') {
                    [void]$builder.Append(' ')
                    $i++
                    break
                }

                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                }
                else {
                    [void]$builder.Append(' ')
                }

                $i++
            }

            continue
        }

        if ($ch -eq "'") {
            [void]$builder.Append(' ')
            $i++
            while ($i -lt $length) {
                $current = $Text[$i]
                if ($current -eq '\') {
                    [void]$builder.Append(' ')
                    if ($i + 1 -lt $length) {
                        $escaped = $Text[$i + 1]
                        if ($escaped -eq [char]13 -or $escaped -eq [char]10) {
                            [void]$builder.Append($escaped)
                        }
                        else {
                            [void]$builder.Append(' ')
                        }

                        $i += 2
                        continue
                    }
                }

                if ($current -eq "'") {
                    [void]$builder.Append(' ')
                    $i++
                    break
                }

                if ($current -eq [char]13 -or $current -eq [char]10) {
                    [void]$builder.Append($current)
                }
                else {
                    [void]$builder.Append(' ')
                }

                $i++
            }

            continue
        }

        [void]$builder.Append($ch)
        $i++
    }

    return $builder.ToString()
}

function ConvertTo-MaskedCodeSurface {
    param([System.Text.RegularExpressions.Match]$Match)

    $value = $Match.Value
    $builder = [System.Text.StringBuilder]::new($value.Length)
    for ($i = 0; $i -lt $value.Length; $i++) {
        $ch = $value[$i]
        if ($ch -eq [char]13 -or $ch -eq [char]10) {
            [void]$builder.Append($ch)
        }
        else {
            [void]$builder.Append(' ')
        }
    }

    return $builder.ToString()
}

function ConvertTo-CodeSurface {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return $Text
    }

    if ($Text.IndexOf('/') -lt 0 -and
        $Text.IndexOf('"') -lt 0 -and
        $Text.IndexOf("'") -lt 0) {
        return $Text
    }

    return $codeSurfaceNoiseRegex.Replace(
        $Text,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param([System.Text.RegularExpressions.Match]$match)
            ConvertTo-MaskedCodeSurface $match
        })
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

function Test-IsCoreMsBuildCondition {
    param([string]$Condition)

    if ([string]::IsNullOrWhiteSpace($Condition)) {
        return $false
    }

    return (
        $Condition.IndexOf("'`$(MSBuildProjectName)' == 'Hecton8.Core'", [StringComparison]::Ordinal) -ge 0 -or
        $Condition.IndexOf("`$(MSBuildProjectName)' == 'Hecton8.Core'", [StringComparison]::Ordinal) -ge 0)
}

function ConvertTo-FullProjectPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    $separator = [System.IO.Path]::DirectorySeparatorChar.ToString()
    $expanded = $Path.Replace('$(MSBuildThisFileDirectory)', ($root + $separator))
    $normalized = $expanded.Replace('/', $separator).Replace('\', $separator)

    if ([System.IO.Path]::IsPathRooted($normalized)) {
        return [System.IO.Path]::GetFullPath($normalized)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $normalized))
}

function Add-CompileSurfacePath {
    param(
        [hashtable]$PathMap,
        [string]$Include
    )

    if ([string]::IsNullOrWhiteSpace($Include) -or
        $Include.IndexOf('$(', [StringComparison]::Ordinal) -ge 0) {
        return
    }

    $fullPath = ConvertTo-FullProjectPath $Include
    if ([string]::IsNullOrWhiteSpace($fullPath) -or
        -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        return
    }

    $key = $fullPath.ToLowerInvariant()
    if (-not $PathMap.ContainsKey($key)) {
        $PathMap[$key] = $fullPath
    }
}

function Get-CoreCompileSurfaceFiles {
    param(
        [string]$CoreProjectPath,
        [string]$TargetsPath
    )

    $pathMap = @{}

    if (Test-Path -LiteralPath $CoreProjectPath) {
        $coreProject = [xml](Get-Content -LiteralPath $CoreProjectPath -Raw)
        foreach ($node in @($coreProject.SelectNodes('//Compile'))) {
            Add-CompileSurfacePath $pathMap ([string]$node.Include)
        }
    }

    if (Test-Path -LiteralPath $TargetsPath) {
        $targetsProject = [xml](Get-Content -LiteralPath $TargetsPath -Raw)
        foreach ($itemGroup in @($targetsProject.Project.ItemGroup)) {
            if (-not (Test-IsCoreMsBuildCondition ([string]$itemGroup.Condition))) {
                continue
            }

            foreach ($node in @($itemGroup.Compile)) {
                Add-CompileSurfacePath $pathMap ([string]$node.Include)
            }
        }
    }

    return @($pathMap.Values | Sort-Object)
}

function Get-AsmdefInventory {
    $inventory = @{}
    $asmdefFiles = @(Get-ChildItem -LiteralPath $scope -Filter '*.asmdef' -Recurse -File |
        Sort-Object FullName)

    foreach ($file in $asmdefFiles) {
        try {
            $asmdef = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        }
        catch {
            continue
        }

        $name = [string]$asmdef.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $inventory[$name] = [pscustomobject][ordered]@{
            Name = $name
            Path = ConvertTo-RelativeProjectPath $file.FullName
            Directory = $file.DirectoryName
            RootNamespace = [string]$asmdef.rootNamespace
        }
    }

    return $inventory
}

function Test-IsPathUnderDirectory {
    param(
        [string]$Path,
        [string]$Directory
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        [string]::IsNullOrWhiteSpace($Directory)) {
        return $false
    }

    $separator = [System.IO.Path]::DirectorySeparatorChar.ToString()
    $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd([char]'\', [char]'/')
    $fullDirectory = [System.IO.Path]::GetFullPath($Directory).TrimEnd([char]'\', [char]'/')

    return (
        $fullPath.Equals($fullDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        ($fullPath + $separator).StartsWith(
            $fullDirectory + $separator,
            [StringComparison]::OrdinalIgnoreCase))
}

function Get-AsmdefSourceFiles {
    param(
        [object]$AsmdefInfo,
        [hashtable]$Inventory
    )

    $nestedDirectories = @()
    foreach ($candidate in $Inventory.Values) {
        if ($candidate.Directory.Equals($AsmdefInfo.Directory, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if (Test-IsPathUnderDirectory $candidate.Directory $AsmdefInfo.Directory) {
            $nestedDirectories += $candidate.Directory
        }
    }

    $sourceFiles = [System.Collections.Generic.List[string]]::new()
    foreach ($file in @(Get-ChildItem -LiteralPath $AsmdefInfo.Directory -Filter '*.cs' -Recurse -File |
        Sort-Object FullName)) {
        $isNested = $false
        foreach ($nestedDirectory in $nestedDirectories) {
            if (Test-IsPathUnderDirectory $file.FullName $nestedDirectory) {
                $isNested = $true
                break
            }
        }

        if (-not $isNested) {
            [void]$sourceFiles.Add($file.FullName)
        }
    }

    return @($sourceFiles)
}

function Remove-CSharpCommentNoise {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    $withoutBlockComments = [System.Text.RegularExpressions.Regex]::Replace(
        $Text,
        '/\*.*?\*/',
        '',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    return [System.Text.RegularExpressions.Regex]::Replace(
        $withoutBlockComments,
        '//.*?$',
        '',
        [System.Text.RegularExpressions.RegexOptions]::Multiline)
}

function Get-DeclaredTypeNames {
    param([string[]]$SourceFiles)

    $typeMap = @{}
    $declarationRegex = [System.Text.RegularExpressions.Regex]::new(
        '\b(?:class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+([A-Za-z_][A-Za-z0-9_]*)',
        $regexOptions)

    foreach ($file in $SourceFiles) {
        try {
            $content = Remove-CSharpCommentNoise ([System.IO.File]::ReadAllText($file))
        }
        catch {
            continue
        }

        foreach ($match in $declarationRegex.Matches($content)) {
            $typeName = [string]$match.Groups[1].Value
            if (-not [string]::IsNullOrWhiteSpace($typeName)) {
                $typeMap[$typeName] = $true
            }
        }
    }

    return @($typeMap.Keys | Sort-Object)
}

function Get-CoreTextCache {
    param([string[]]$CoreCompileFiles)

    $textByPath = @{}
    foreach ($file in $CoreCompileFiles) {
        try {
            $textByPath[$file.ToLowerInvariant()] =
                Remove-CSharpCommentNoise ([System.IO.File]::ReadAllText($file))
        }
        catch {
            continue
        }
    }

    return $textByPath
}

function Get-IdentifierHitCount {
    param(
        [string]$Text,
        [string]$Identifier
    )

    if ([string]::IsNullOrWhiteSpace($Text) -or
        [string]::IsNullOrWhiteSpace($Identifier)) {
        return 0
    }

    $pattern = '(?<![A-Za-z0-9_])' +
        [System.Text.RegularExpressions.Regex]::Escape($Identifier) +
        '(?![A-Za-z0-9_])'

    return [System.Text.RegularExpressions.Regex]::Matches($Text, $pattern).Count
}

function Get-LiteralHitsInCoreSurface {
    param(
        [hashtable]$CoreTextByPath,
        [hashtable]$ExcludedPathSet,
        [string]$Literal
    )

    $hitCount = 0
    if ([string]::IsNullOrWhiteSpace($Literal)) {
        return $hitCount
    }

    foreach ($entry in $CoreTextByPath.GetEnumerator()) {
        if ($ExcludedPathSet.ContainsKey($entry.Key)) {
            continue
        }

        $hitCount += Get-IdentifierHitCount $entry.Value $Literal
    }

    return $hitCount
}

function Get-TypeHitsInCoreSurface {
    param(
        [hashtable]$CoreTextByPath,
        [hashtable]$ExcludedPathSet,
        [string[]]$TypeNames
    )

    $distinctTypes = @{}
    $hitCount = 0

    if ($null -eq $TypeNames -or $TypeNames.Count -le 0) {
        return [ordered]@{
            HitCount = 0
            DistinctTypeCount = 0
            Types = @()
        }
    }

    $escapedNames = @($TypeNames |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { [System.Text.RegularExpressions.Regex]::Escape($_) })

    if ($escapedNames.Count -le 0) {
        return [ordered]@{
            HitCount = 0
            DistinctTypeCount = 0
            Types = @()
        }
    }

    $typeRegex = [System.Text.RegularExpressions.Regex]::new(
        '(?<![A-Za-z0-9_])(?:' + ($escapedNames -join '|') + ')(?![A-Za-z0-9_])',
        $regexOptions)

    foreach ($entry in $CoreTextByPath.GetEnumerator()) {
        if ($ExcludedPathSet.ContainsKey($entry.Key)) {
            continue
        }

        foreach ($match in $typeRegex.Matches([string]$entry.Value)) {
            $hitCount++
            $distinctTypes[[string]$match.Value] = $true
        }
    }

    return [ordered]@{
        HitCount = $hitCount
        DistinctTypeCount = $distinctTypes.Count
        Types = @($distinctTypes.Keys | Sort-Object)
    }
}

function Get-UnusedCoreReferenceScan {
    param(
        [object[]]$DebtRows,
        [string]$CoreProjectPath,
        [string]$TargetsPath
    )

    $inventory = Get-AsmdefInventory
    $coreCompileFiles = @(Get-CoreCompileSurfaceFiles $CoreProjectPath $TargetsPath)
    $coreTextByPath = Get-CoreTextCache $coreCompileFiles
    $rows = @()

    foreach ($debtRow in @($DebtRows | Sort-Object Reference)) {
        $reference = [string]$debtRow.Reference
        $kind = [string]$debtRow.Kind
        $asmdefInfo = $null
        $assemblyFound = $inventory.ContainsKey($reference)

        if ($assemblyFound) {
            $asmdefInfo = $inventory[$reference]
        }

        $sourceFiles = @()
        $sourcePathSet = @{}
        $declaredTypes = @()
        $sourceInCoreSurfaceCount = 0
        $externalTypeHits = [ordered]@{
            HitCount = 0
            DistinctTypeCount = 0
            Types = @()
        }
        $externalNamespaceHitCount = 0
        $externalAssemblyLiteralHitCount = 0
        $candidateForRemoval = $false
        $confidence = 'NotScanned'

        if ($kind -ne 'LeafDomain') {
            $confidence = 'NotLeafDomain'
        }
        elseif (-not $assemblyFound) {
            $confidence = 'AsmdefMissing'
        }
        else {
            $sourceFiles = @(Get-AsmdefSourceFiles $asmdefInfo $inventory)
            foreach ($sourceFile in $sourceFiles) {
                $sourcePathSet[$sourceFile.ToLowerInvariant()] = $true
                if ($coreTextByPath.ContainsKey($sourceFile.ToLowerInvariant())) {
                    $sourceInCoreSurfaceCount++
                }
            }

            $declaredTypes = @(Get-DeclaredTypeNames $sourceFiles)
            $externalTypeHits = Get-TypeHitsInCoreSurface `
                $coreTextByPath `
                $sourcePathSet `
                $declaredTypes

            $namespaceLiteral = [string]$asmdefInfo.RootNamespace
            if ([string]::IsNullOrWhiteSpace($namespaceLiteral)) {
                $namespaceLiteral = $reference
            }

            $externalNamespaceHitCount = Get-LiteralHitsInCoreSurface `
                $coreTextByPath `
                $sourcePathSet `
                $namespaceLiteral

            $externalAssemblyLiteralHitCount = Get-LiteralHitsInCoreSurface `
                $coreTextByPath `
                $sourcePathSet `
                $reference

            $candidateForRemoval =
                $declaredTypes.Count -gt 0 -and
                [int]$externalTypeHits.HitCount -eq 0 -and
                $externalNamespaceHitCount -eq 0 -and
                $externalAssemblyLiteralHitCount -eq 0

            if ($candidateForRemoval -and $sourceInCoreSurfaceCount -eq 0) {
                $confidence = 'High'
            }
            elseif ($candidateForRemoval) {
                $confidence = 'ReviewSourceBackedCompile'
            }
            elseif ($declaredTypes.Count -le 0) {
                $confidence = 'NoDeclaredTypes'
            }
            else {
                $confidence = 'BlockedByExternalHits'
            }
        }

        $rows += [pscustomobject][ordered]@{
            Reference = $reference
            Kind = $kind
            AssemblyFound = $assemblyFound
            AssemblyPath = if ($assemblyFound) { $asmdefInfo.Path } else { '' }
            SourceFileCount = @($sourceFiles).Count
            SourceInCoreCompileSurfaceCount = $sourceInCoreSurfaceCount
            DeclaredTypeCount = @($declaredTypes).Count
            ExternalTypeHitCount = [int]$externalTypeHits.HitCount
            ExternalDistinctTypeHitCount = [int]$externalTypeHits.DistinctTypeCount
            ExternalNamespaceHitCount = $externalNamespaceHitCount
            ExternalAssemblyLiteralHitCount = $externalAssemblyLiteralHitCount
            CandidateForRemoval = $candidateForRemoval
            Confidence = $confidence
            ExternalTypeHits = @($externalTypeHits.Types)
        }
    }

    $candidates = @($rows | Where-Object { $_.CandidateForRemoval })

    return [ordered]@{
        Enabled = $true
        EvidenceClass = 'STATIC_SOURCE'
        Model = 'Optional Core asmdef debt scan. Candidate means generated/source-backed Core compile surface has no external text hit for declared candidate types, namespace, or assembly literal. It is not compile proof.'
        CoreCompileSurfaceFileCount = @($coreCompileFiles).Count
        ScannedDebtReferenceCount = @($rows).Count
        CandidateCount = @($candidates).Count
        Candidates = @($candidates)
        Rows = @($rows)
    }
}

function Get-DuplicateSignalNameAudit {
    param([string[]]$Files)

    $rows = [System.Collections.Generic.List[object]]::new()
    $totalFileCount = 0
    $candidateFileCount = 0
    $skippedFileCount = 0
    $structRegex = [System.Text.RegularExpressions.Regex]::new(
        '^\s*(?:public|internal|private|protected)?\s*(?:readonly\s+|partial\s+|unsafe\s+|ref\s+)*struct\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*Signal)\b',
        $regexOptions)
    $namespaceRegex = [System.Text.RegularExpressions.Regex]::new(
        '^\s*namespace\s+(?<Name>[A-Za-z_][A-Za-z0-9_.]*)\b',
        $regexOptions)

    foreach ($file in $Files) {
        if (-not [System.IO.File]::Exists($file)) {
            continue
        }

        $content = [System.IO.File]::ReadAllText($file)
        $totalFileCount++

        if ($content.IndexOf('Signal', [StringComparison]::Ordinal) -lt 0 -or
            $content.IndexOf('struct', [StringComparison]::Ordinal) -lt 0) {
            $skippedFileCount++
            continue
        }

        $candidateFileCount++
        $codeSurface = ConvertTo-CodeSurface $content
        $lines = $codeSurface -split "`r?`n", -1
        $namespace = ''

        for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
            $line = $lines[$lineIndex]
            $namespaceMatch = $namespaceRegex.Match($line)
            if ($namespaceMatch.Success) {
                $namespace = $namespaceMatch.Groups['Name'].Value
            }

            foreach ($match in $structRegex.Matches($line)) {
                [void]$rows.Add([pscustomobject][ordered]@{
                    Name = $match.Groups['Name'].Value
                    Namespace = $namespace
                    File = Get-RelativeSourcePath $file
                    Line = $lineIndex + 1
                })
            }
        }
    }

    $groups = @($rows | Group-Object Name | Where-Object { $_.Count -gt 1 } | Sort-Object Name)
    $duplicateNameSet = @{}
    $duplicateNames = [System.Collections.Generic.List[object]]::new()
    foreach ($group in $groups) {
        $duplicateNameSet[$group.Name] = $true
        [void]$duplicateNames.Add([pscustomobject][ordered]@{
            Name = $group.Name
            Count = $group.Count
            Files = @($group.Group | Select-Object -ExpandProperty File | Sort-Object -Unique)
        })
    }

    $duplicateRows = @($rows | Where-Object { $duplicateNameSet.ContainsKey($_.Name) } | Sort-Object Name, File, Line)

    [ordered]@{
        EvidenceClass = 'STATIC_SOURCE'
        Model = 'First-party struct names ending in Signal must be globally unique. This is a static name-collision scan, not compile or runtime proof.'
        SourceFileCount = $totalFileCount
        CandidateFileCount = $candidateFileCount
        PrefilterSkippedFileCount = $skippedFileCount
        SignalStructDeclarationCount = @($rows).Count
        DuplicateSignalNameCount = @($groups).Count
        DuplicateSignalDeclarationCount = @($duplicateRows).Count
        DuplicateNames = @($duplicateNames)
        Rows = @($duplicateRows)
    }
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
            if (-not (Test-IsCoreMsBuildCondition $condition)) {
                continue
            }

            $bridgeLane = 'CoreCompileBridge'
            if ($condition.IndexOf('HectonBuildProjectReferences', [StringComparison]::Ordinal) -ge 0) {
                $bridgeLane = 'ProjectReferenceReplacement'
            }

            foreach ($node in @($itemGroup.Reference)) {
                $include = [string]$node.Include
                if ([string]::IsNullOrWhiteSpace($include)) {
                    continue
                }

                $kind = Get-CoreReferenceKind $include
                $bridgeRows += [pscustomobject][ordered]@{
                    Reference = $include
                    Kind = $kind
                    BridgeLane = $bridgeLane
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
    $compileBridgeRows = @($bridgeRows | Where-Object { $_.BridgeLane -eq 'CoreCompileBridge' })
    $compileBridgeDebtRows = @($compileBridgeRows | Where-Object { $_.IsHPhiDebt })
    $replacementBridgeRows = @($bridgeRows | Where-Object { $_.BridgeLane -eq 'ProjectReferenceReplacement' })
    $replacementBridgeDebtRows = @($replacementBridgeRows | Where-Object { $_.IsHPhiDebt })
    $unusedCoreReferenceScan = [ordered]@{
        Enabled = $false
    }

    if ($IncludeUnusedCoreReferenceScan) {
        $unusedCoreReferenceScan = Get-UnusedCoreReferenceScan `
            $asmdefDebtRows `
            $coreCsprojPath `
            $targetsPath
    }

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
            SourceBackedCompileBridgeReferenceCount = @($compileBridgeRows).Count
            SourceBackedCompileBridgeDebtReferenceCount = @($compileBridgeDebtRows).Count
            ProjectReferenceReplacementReferenceCount = @($replacementBridgeRows).Count
            ProjectReferenceReplacementDebtReferenceCount = @($replacementBridgeDebtRows).Count
        }
        CoreAsmdefReferences = @($asmdefRows)
        CoreAsmdefDebtReferences = @($asmdefDebtRows)
        GeneratedProjectReferences = @($projectRows)
        GeneratedProjectDebtReferences = @($projectDebtRows)
        SourceBackedBridgeReferences = @($bridgeRows)
        SourceBackedBridgeDebtReferences = @($bridgeDebtRows)
        CoreAsmdefUnusedReferenceScan = $unusedCoreReferenceScan
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

    if ($MaxSourceBackedCompileBridgeDebtReferences -ge 0 -and
        [int]$counts.SourceBackedCompileBridgeDebtReferenceCount -gt $MaxSourceBackedCompileBridgeDebtReferences) {
        [void]$violations.Add((
            'Source-backed Core compile-bridge H-Phi debt refs {0} exceed budget {1}.' -f
            $counts.SourceBackedCompileBridgeDebtReferenceCount,
            $MaxSourceBackedCompileBridgeDebtReferences))
    }

    if ($MaxProjectReferenceReplacementDebtReferences -ge 0 -and
        [int]$counts.ProjectReferenceReplacementDebtReferenceCount -gt $MaxProjectReferenceReplacementDebtReferences) {
        [void]$violations.Add((
            'Core project-reference replacement H-Phi debt refs {0} exceed budget {1}.' -f
            $counts.ProjectReferenceReplacementDebtReferenceCount,
            $MaxProjectReferenceReplacementDebtReferences))
    }

    if ($violations.Count -le 0) {
        return
    }

    throw (
        "Core graph H-Phi budget failed with $($violations.Count) violation(s):`n" +
        ($violations -join "`n"))
}

function Assert-AupPrecisionBudget {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counts,
        [System.Collections.IEnumerable]$FileRows
    )

    if ($MaxAupPrecisionRisk -lt 0) {
        return
    }

    $riskCount = [int]$Counts.AupPrecisionRisk
    if ($riskCount -le $MaxAupPrecisionRisk) {
        return
    }

    $message =
        'AUP precision H-Phi budget failed: risk patterns {0} exceed budget {1}.' -f
        $riskCount,
        $MaxAupPrecisionRisk

    $topFiles = @($FileRows |
        Where-Object { $_.AupPrecisionRisk -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'AupPrecisionRisk'; Descending = $true },
            @{ Expression = 'AupPrecisionSafe'; Descending = $true }) |
        Select-Object -First 8)

    if ($topFiles.Count -gt 0) {
        $lines = [System.Collections.Generic.List[string]]::new()
        foreach ($file in $topFiles) {
            [void]$lines.Add((
                '{0} risk={1} safe={2}' -f
                $file.File,
                $file.AupPrecisionRisk,
                $file.AupPrecisionSafe))
        }

        $message += "`nTop AUP precision risk files:`n" + ($lines -join "`n")
    }

    throw $message
}

function Assert-DuplicateSignalNameBudget {
    param([System.Collections.Specialized.OrderedDictionary]$Audit)

    if ($MaxDuplicateSignalNames -lt 0) {
        return
    }

    $duplicateCount = [int]$Audit.DuplicateSignalNameCount
    if ($duplicateCount -le $MaxDuplicateSignalNames) {
        return
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($Audit.DuplicateNames | Select-Object -First 8)) {
        [void]$lines.Add(('{0} declarations={1}' -f $entry.Name, $entry.Count))
    }

    throw (
        'Duplicate signal-name H-Phi budget failed: duplicate names {0} exceed budget {1}.' -f
        $duplicateCount,
        $MaxDuplicateSignalNames) +
        "`nDuplicate signal names:`n" +
        ($lines -join "`n")
}

function Assert-StaticCounterBudget {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counts,
        [System.Collections.IEnumerable]$FileRows,
        [string]$CounterName,
        [int]$MaxValue,
        [string]$Label
    )

    if ($MaxValue -lt 0) {
        return
    }

    $actual = [int]$Counts[$CounterName]
    if ($actual -le $MaxValue) {
        return
    }

    $message = '{0} H-Phi budget failed: count {1} exceeds budget {2}.' -f
        $Label,
        $actual,
        $MaxValue

    $topFiles = @($FileRows |
        Where-Object { [int]$_.PSObject.Properties[$CounterName].Value -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = { [int]$_.PSObject.Properties[$CounterName].Value }; Descending = $true },
            @{ Expression = 'CouplingRisk'; Descending = $true }) |
        Select-Object -First 8)

    if ($topFiles.Count -gt 0) {
        $lines = [System.Collections.Generic.List[string]]::new()
        foreach ($file in $topFiles) {
            [void]$lines.Add((
                '{0} {1}={2} coupling={3}' -f
                $file.File,
                $CounterName,
                [int]$file.PSObject.Properties[$CounterName].Value,
                $file.CouplingRisk))
        }

        $message += "`nTop files:`n" + ($lines -join "`n")
    }

    throw $message
}

function Assert-ScalarMaxBudget {
    param(
        [int]$Actual,
        [int]$MaxValue,
        [string]$Label
    )

    if ($MaxValue -lt 0) {
        return
    }

    if ($Actual -le $MaxValue) {
        return
    }

    throw ('{0} H-Phi budget failed: count {1} exceeds budget {2}.' -f
        $Label,
        $Actual,
        $MaxValue)
}

function Assert-StaticScoreFloor {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Scores,
        [string]$ScoreName,
        [double]$MinValue,
        [string]$Label
    )

    if ($MinValue -lt 0.0) {
        return
    }

    $actual = [double]$Scores[$ScoreName]
    if ($actual -ge $MinValue) {
        return
    }

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    throw ('{0} H-Phi floor failed: score {1} is below floor {2}.' -f
        $Label,
        $actual.ToString('0.#########', $culture),
        $MinValue.ToString('0.#########', $culture))
}

function Add-Count {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [string]$Name,
        [int]$Value
    )

    $Counters[$Name] = [int]$Counters[$Name] + $Value
}

function Test-ContainsAnyLiteral {
    param(
        [string]$Content,
        [string[]]$Literals
    )

    if ([string]::IsNullOrEmpty($Content) -or
        $null -eq $Literals -or
        $Literals.Count -le 0) {
        return $false
    }

    foreach ($literal in $Literals) {
        if (-not [string]::IsNullOrEmpty($literal) -and
            $Content.IndexOf($literal, [StringComparison]::Ordinal) -ge 0) {
            return $true
        }
    }

    return $false
}

function Add-PatternCounts {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [string]$Content,
        [hashtable]$Patterns,
        [hashtable]$LiteralHints
    )

    foreach ($entry in $Patterns.GetEnumerator()) {
        if ($null -ne $LiteralHints -and
            $LiteralHints.ContainsKey($entry.Key) -and
            -not (Test-ContainsAnyLiteral $Content $LiteralHints[$entry.Key])) {
            continue
        }

        Add-Count $Counters $entry.Key $entry.Value.Matches($Content).Count
    }
}

function Normalize-UnityLoopCounters {
    param(
        [System.Collections.Specialized.OrderedDictionary]$Counters,
        [string]$RelativePath
    )

    $rawCount = [int]$Counters['UnityUpdateMethods']
    $Counters['UnityUpdateMethodsRaw'] = [int]$Counters['UnityUpdateMethodsRaw'] + $rawCount
    if ($rawCount -le 0) {
        return
    }

    $normalizedPath = $RelativePath.Replace('/', '\')
    if ($normalizedPath -eq 'Core\SystemDispatcher.cs') {
        $Counters['UnityLoopShellMethods'] = [int]$Counters['UnityLoopShellMethods'] + $rawCount
        $Counters['UnityUpdateMethods'] = 0
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
    $Row['UnityUpdateMethodsRaw'] = [int]$Row['UnityUpdateMethodsRaw'] + [int]$Counters['UnityUpdateMethodsRaw']
    $Row['UnityLoopShellMethods'] = [int]$Row['UnityLoopShellMethods'] + [int]$Counters['UnityLoopShellMethods']
    $Row['UpdateMethods'] = [int]$Row['UpdateMethods'] + [int]$Counters['UnityUpdateMethods']
    $Row['Structs'] = [int]$Row['Structs'] + [int]$Counters['StructDeclarations']
    $Row['Layout'] = [int]$Row['Layout'] + [int]$Counters['StructLayoutAttributes']
    $Row['BinarySafe'] = [int]$Row['BinarySafe'] + [int]$Counters['BinaryBlittableSafe']
    $Row['FindObjectCalls'] = [int]$Row['FindObjectCalls'] + [int]$Counters['FindObjectCalls']
    $Row['GetComponentCalls'] = [int]$Row['GetComponentCalls'] + [int]$Counters['GetComponentCalls']
    $Row['DisposeCalls'] = [int]$Row['DisposeCalls'] + [int]$Counters['DisposeCalls']
    $Row['AupPrecisionSafe'] = [int]$Row['AupPrecisionSafe'] + [int]$Counters['AupPrecisionSafe']
    $Row['AupPrecisionRisk'] = [int]$Row['AupPrecisionRisk'] + [int]$Counters['AupPrecisionRisk']
    $Row['LinqSurface'] = [int]$Row['LinqSurface'] + [int]$Counters['LinqSurface']
    $Row['CoroutineSurface'] = [int]$Row['CoroutineSurface'] + [int]$Counters['CoroutineSurface']
    $Row['ManagedFormatSurface'] = [int]$Row['ManagedFormatSurface'] + [int]$Counters['ManagedFormatSurface']
    $Row['JobCompleteSurface'] = [int]$Row['JobCompleteSurface'] + [int]$Counters['JobCompleteSurface']
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

function Divide-OrOne {
    param(
        [double]$Numerator,
        [double]$Denominator
    )

    if ($Denominator -gt 0.0) {
        return $Numerator / $Denominator
    }

    return 1.0
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
    $aupPrecisionDenominator = [double]$Counts.AupPrecisionSafe + [double]$Counts.AupPrecisionRisk
    $aupPrecisionIntegrity = Divide-OrOne $Counts.AupPrecisionSafe $aupPrecisionDenominator

    $hPhiStaticNarrow = $narrowIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment
    $hPhiStaticRisk = $riskIntegration * $architecturalPurity * $dataSovereignty * $memoryAlignment * $aupPrecisionIntegrity

    [ordered]@{
        NarrowIntegration = [Math]::Round($narrowIntegration, 9)
        RiskIntegration = [Math]::Round($riskIntegration, 9)
        ArchitecturalPurity = [Math]::Round($architecturalPurity, 9)
        ArchitecturalPurityExpanded = [Math]::Round($architecturalPurityExpanded, 9)
        DataSovereignty = [Math]::Round($dataSovereignty, 9)
        MemoryAlignment = [Math]::Round($memoryAlignment, 9)
        BinarySafeRatio = [Math]::Round($binarySafeRatio, 9)
        AupPrecisionIntegrity = [Math]::Round($aupPrecisionIntegrity, 9)
        HPhiStaticNarrow = [Math]::Round($hPhiStaticNarrow, 9)
        HPhiStaticRisk = [Math]::Round($hPhiStaticRisk, 9)
    }
}

function New-BudgetState {
    param(
        [bool]$Enabled,
        [object]$Max,
        [object]$Actual,
        [bool]$Passed,
        [string]$EvidenceClass
    )

    [ordered]@{
        Enabled = $Enabled
        Max = $Max
        Actual = $Actual
        Passed = $Passed
        EvidenceClass = $EvidenceClass
    }
}

function New-CoreGraphBudgetSummary {
    param([System.Collections.Specialized.OrderedDictionary]$CoreGraphAudit)

    $gate = $CoreGraphAudit.BuildGraphGate
    $counts = $CoreGraphAudit.Counts
    $gateActual =
        [bool]$gate.CoreBuildProjectReferencesDisabledByDefault -and
        [bool]$gate.CoreBuildInParallelDisabledByDefault

    [ordered]@{
        CoreBuildGraphGate = New-BudgetState `
            ([bool]$RequireCoreBuildGate) `
            $true `
            $gateActual `
            ((-not [bool]$RequireCoreBuildGate) -or $gateActual) `
            'STATIC_SOURCE_GRAPH'
        CoreAsmdefDebtReferences = New-BudgetState `
            ($MaxCoreAsmdefDebtReferences -ge 0) `
            $MaxCoreAsmdefDebtReferences `
            ([int]$counts.CoreAsmdefDebtReferenceCount) `
            ($MaxCoreAsmdefDebtReferences -lt 0 -or [int]$counts.CoreAsmdefDebtReferenceCount -le $MaxCoreAsmdefDebtReferences) `
            'STATIC_SOURCE_GRAPH'
        GeneratedProjectDebtReferences = New-BudgetState `
            ($MaxGeneratedProjectDebtReferences -ge 0) `
            $MaxGeneratedProjectDebtReferences `
            ([int]$counts.GeneratedProjectDebtReferenceCount) `
            ($MaxGeneratedProjectDebtReferences -lt 0 -or [int]$counts.GeneratedProjectDebtReferenceCount -le $MaxGeneratedProjectDebtReferences) `
            'STATIC_SOURCE_GRAPH'
        SourceBackedBridgeDebtReferences = New-BudgetState `
            ($MaxSourceBackedBridgeDebtReferences -ge 0) `
            $MaxSourceBackedBridgeDebtReferences `
            ([int]$counts.SourceBackedBridgeDebtReferenceCount) `
            ($MaxSourceBackedBridgeDebtReferences -lt 0 -or [int]$counts.SourceBackedBridgeDebtReferenceCount -le $MaxSourceBackedBridgeDebtReferences) `
            'STATIC_SOURCE_GRAPH'
        SourceBackedCompileBridgeDebtReferences = New-BudgetState `
            ($MaxSourceBackedCompileBridgeDebtReferences -ge 0) `
            $MaxSourceBackedCompileBridgeDebtReferences `
            ([int]$counts.SourceBackedCompileBridgeDebtReferenceCount) `
            ($MaxSourceBackedCompileBridgeDebtReferences -lt 0 -or [int]$counts.SourceBackedCompileBridgeDebtReferenceCount -le $MaxSourceBackedCompileBridgeDebtReferences) `
            'STATIC_SOURCE_GRAPH'
        ProjectReferenceReplacementDebtReferences = New-BudgetState `
            ($MaxProjectReferenceReplacementDebtReferences -ge 0) `
            $MaxProjectReferenceReplacementDebtReferences `
            ([int]$counts.ProjectReferenceReplacementDebtReferenceCount) `
            ($MaxProjectReferenceReplacementDebtReferences -lt 0 -or [int]$counts.ProjectReferenceReplacementDebtReferenceCount -le $MaxProjectReferenceReplacementDebtReferences) `
            'STATIC_SOURCE_GRAPH'
    }
}

function ConvertTo-BudgetDisplayRows {
    param([System.Collections.IDictionary]$Budgets)

    $Budgets.GetEnumerator() |
        ForEach-Object {
            $budget = $_.Value
            $hasMax = $budget.Contains('Max')
            $hasMin = $budget.Contains('Min')
            $limit = $null
            $direction = '='

            if ($hasMax) {
                $limit = $budget.Max
                $direction = if ($budget.Max -is [bool]) { '=' } else { '<=' }
            }
            elseif ($hasMin) {
                $limit = $budget.Min
                $direction = '>='
            }

            [pscustomobject]@{
                Budget = $_.Key
                Enabled = $budget.Enabled
                Direction = $direction
                Limit = $limit
                Actual = $budget.Actual
                Passed = $budget.Passed
            }
        }
}

function New-CoreGraphSummary {
    param([System.Collections.Specialized.OrderedDictionary]$CoreGraphAudit)

    $unusedScan = $CoreGraphAudit.CoreAsmdefUnusedReferenceScan
    $unusedScanSummary = [ordered]@{
        Enabled = $false
    }

    if ($null -ne $unusedScan -and $unusedScan.Enabled) {
        $unusedScanSummary = [ordered]@{
            Enabled = $true
            EvidenceClass = $unusedScan.EvidenceClass
            CoreCompileSurfaceFileCount = $unusedScan.CoreCompileSurfaceFileCount
            ScannedDebtReferenceCount = $unusedScan.ScannedDebtReferenceCount
            CandidateCount = $unusedScan.CandidateCount
            Candidates = @($unusedScan.Candidates | Select-Object Reference, Confidence, SourceFileCount, DeclaredTypeCount, SourceInCoreCompileSurfaceCount)
        }
    }

    [ordered]@{
        CoreAsmdef = $CoreGraphAudit.CoreAsmdef
        CoreProject = $CoreGraphAudit.CoreProject
        BuildGraphGate = $CoreGraphAudit.BuildGraphGate
        Counts = $CoreGraphAudit.Counts
        Budgets = New-CoreGraphBudgetSummary $CoreGraphAudit
        CoreAsmdefDebtReferences = @($CoreGraphAudit.CoreAsmdefDebtReferences |
            Select-Object -ExpandProperty Reference)
        GeneratedProjectDebtReferences = @($CoreGraphAudit.GeneratedProjectDebtReferences |
            Select-Object -ExpandProperty Reference)
        SourceBackedBridgeDebtReferences = @($CoreGraphAudit.SourceBackedBridgeDebtReferences |
            Select-Object -ExpandProperty Reference)
        CoreAsmdefUnusedReferenceScan = $unusedScanSummary
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
            AupPrecisionIntegrity = $Audit.Scores.AupPrecisionIntegrity
        }
        Counts = [ordered]@{
            RuntimeFiles = $Audit.Counts.CsFiles
            RuntimeLines = $Audit.Counts.Lines
            SignalBusPush = $Audit.Counts.SignalBusPush
            GlobalRegistrySurface = $Audit.Counts.GlobalRegistrySurface
            EventPublish = $Audit.Counts.EventPublish
            UnityUpdateMethodsRaw = $Audit.Counts.UnityUpdateMethodsRaw
            UnityLoopShellMethods = $Audit.Counts.UnityLoopShellMethods
            UnityUpdateMethods = $Audit.Counts.UnityUpdateMethods
            DataVaultRefs = $Audit.Counts.GlobalDataVaultRefs
            NativeArrayRefs = $Audit.Counts.NativeArrayRefs
            StructDeclarations = $Audit.Counts.StructDeclarations
            StructLayoutAttributes = $Audit.Counts.StructLayoutAttributes
            FindObjectCalls = $Audit.Counts.FindObjectCalls
            GetComponentCalls = $Audit.Counts.GetComponentCalls
            DisposeCalls = $Audit.Counts.DisposeCalls
            AupPrecisionSafe = $Audit.Counts.AupPrecisionSafe
            AupPrecisionRisk = $Audit.Counts.AupPrecisionRisk
            LinqSurface = $Audit.Counts.LinqSurface
            CoroutineSurface = $Audit.Counts.CoroutineSurface
            ManagedFormatSurface = $Audit.Counts.ManagedFormatSurface
            JobCompleteSurface = $Audit.Counts.JobCompleteSurface
            PrimaryManagedRuntimeRisk = $Audit.RiskSums.PrimaryManagedRuntimeRisk
            PrimaryJobCompleteRisk = $Audit.RiskSums.PrimaryJobCompleteRisk
        }
        Budgets = $Audit.Budgets
        CoreGraph = New-CoreGraphSummary $Audit.CoreGraphAudit
        DuplicateSignalNameAudit = [ordered]@{
            EvidenceClass = $Audit.DuplicateSignalNameAudit.EvidenceClass
            SourceFileCount = $Audit.DuplicateSignalNameAudit.SourceFileCount
            CandidateFileCount = $Audit.DuplicateSignalNameAudit.CandidateFileCount
            PrefilterSkippedFileCount = $Audit.DuplicateSignalNameAudit.PrefilterSkippedFileCount
            SignalStructDeclarationCount = $Audit.DuplicateSignalNameAudit.SignalStructDeclarationCount
            DuplicateSignalNameCount = $Audit.DuplicateSignalNameAudit.DuplicateSignalNameCount
            DuplicateSignalDeclarationCount = $Audit.DuplicateSignalNameAudit.DuplicateSignalDeclarationCount
            DuplicateNames = @($Audit.DuplicateSignalNameAudit.DuplicateNames | Select-Object -First 12)
        }
        TopAupPrecisionRiskFiles = @($Audit.TopAupPrecisionRiskFiles |
            Select-Object -First 10)
        TopCouplingRiskFiles = @($Audit.TopCouplingRiskFiles |
            Select-Object -First 10)
        TopManagedRuntimeRiskFiles = @($Audit.TopManagedRuntimeRiskFiles |
            Select-Object -First 10)
        TopPrimaryManagedRuntimeRiskFiles = @($Audit.TopPrimaryManagedRuntimeRiskFiles |
            Select-Object -First 10)
        TopJobCompleteRiskFiles = @($Audit.TopJobCompleteRiskFiles |
            Select-Object -First 10)
        ManagedRiskByRole = @($Audit.ManagedRiskByRole |
            Select-Object -First 8)
        TopOwnerBlockedDataVaultCandidates = @($Audit.OwnerBlockedDataVaultCandidates |
            Select-Object -First 10)
    }
}

$coreGraphAudit = Get-CoreGraphAudit
Assert-CoreGraphBudget $coreGraphAudit

if ($CoreGraphOnly) {
    if ($MaxAupPrecisionRisk -ge 0) {
        throw 'AUP precision budget requires full source scan. Remove -CoreGraphOnly when using -MaxAupPrecisionRisk.'
    }

    if ($MaxFindObjectCalls -ge 0) {
        throw 'FindObject budget requires full source scan. Remove -CoreGraphOnly when using -MaxFindObjectCalls.'
    }

    if ($MaxLegacyEventPublish -ge 0) {
        throw 'Legacy event publish budget requires full source scan. Remove -CoreGraphOnly when using -MaxLegacyEventPublish.'
    }

    if ($MaxDuplicateSignalNames -ge 0) {
        throw 'Duplicate signal-name budget requires full source scan. Remove -CoreGraphOnly when using -MaxDuplicateSignalNames.'
    }

    if ($MaxUnityUpdateMethods -ge 0) {
        throw 'Unity update-method budget requires full source scan. Remove -CoreGraphOnly when using -MaxUnityUpdateMethods.'
    }

    if ($MaxGlobalRegistrySurface -ge 0) {
        throw 'GlobalRegistry surface budget requires full source scan. Remove -CoreGraphOnly when using -MaxGlobalRegistrySurface.'
    }

    if ($MaxGetComponentCalls -ge 0) {
        throw 'GetComponent budget requires full source scan. Remove -CoreGraphOnly when using -MaxGetComponentCalls.'
    }

    if ($MaxNativeArrayRefs -ge 0) {
        throw 'NativeArray budget requires full source scan. Remove -CoreGraphOnly when using -MaxNativeArrayRefs.'
    }

    if ($MaxLinqSurface -ge 0) {
        throw 'LINQ surface budget requires full source scan. Remove -CoreGraphOnly when using -MaxLinqSurface.'
    }

    if ($MaxCoroutineSurface -ge 0) {
        throw 'Coroutine surface budget requires full source scan. Remove -CoreGraphOnly when using -MaxCoroutineSurface.'
    }

    if ($MaxManagedFormatSurface -ge 0) {
        throw 'Managed format surface budget requires full source scan. Remove -CoreGraphOnly when using -MaxManagedFormatSurface.'
    }

    if ($MaxJobCompleteSurface -ge 0) {
        throw 'Job Complete surface budget requires full source scan. Remove -CoreGraphOnly when using -MaxJobCompleteSurface.'
    }

    if ($MaxPrimaryManagedRuntimeRisk -ge 0) {
        throw 'Primary managed runtime risk budget requires full source scan. Remove -CoreGraphOnly when using -MaxPrimaryManagedRuntimeRisk.'
    }

    if ($MinDataSovereignty -ge 0.0) {
        throw 'Data Sovereignty floor requires full source scan. Remove -CoreGraphOnly when using -MinDataSovereignty.'
    }

    if ($MinMemoryAlignment -ge 0.0) {
        throw 'Memory Alignment floor requires full source scan. Remove -CoreGraphOnly when using -MinMemoryAlignment.'
    }

    if ($MinRuntimeHPhiRisk -ge 0.0) {
        throw 'Runtime H-Phi risk floor requires full source scan. Remove -CoreGraphOnly when using -MinRuntimeHPhiRisk.'
    }

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
        Write-Output 'Core graph H-Phi budgets:'
        ConvertTo-BudgetDisplayRows $summaryResult.CoreGraph.Budgets | Format-Table -AutoSize
        Write-Output ''
        Write-Output 'Core asmdef H-Phi debt references:'
        $summaryResult.CoreGraph.CoreAsmdefDebtReferences | ForEach-Object { Write-Output ("  {0}" -f $_) }
        Write-Output ''
        Write-Output 'Generated Core project H-Phi debt references:'
        $summaryResult.CoreGraph.GeneratedProjectDebtReferences | ForEach-Object { Write-Output ("  {0}" -f $_) }
        Write-Output ''
        Write-Output 'Source-backed Core bridge H-Phi debt references:'
        $summaryResult.CoreGraph.SourceBackedBridgeDebtReferences | ForEach-Object { Write-Output ("  {0}" -f $_) }
        if ($summaryResult.CoreGraph.CoreAsmdefUnusedReferenceScan.Enabled) {
            Write-Output ''
            Write-Output 'Unused Core asmdef reference candidates:'
            $summaryResult.CoreGraph.CoreAsmdefUnusedReferenceScan.Candidates | Format-Table -AutoSize
        }
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
    if ($coreGraphAudit.CoreAsmdefUnusedReferenceScan.Enabled) {
        Write-Output ''
        Write-Output 'Unused Core asmdef reference scan:'
        [pscustomobject][ordered]@{
            CoreCompileSurfaceFileCount = $coreGraphAudit.CoreAsmdefUnusedReferenceScan.CoreCompileSurfaceFileCount
            ScannedDebtReferenceCount = $coreGraphAudit.CoreAsmdefUnusedReferenceScan.ScannedDebtReferenceCount
            CandidateCount = $coreGraphAudit.CoreAsmdefUnusedReferenceScan.CandidateCount
        } | Format-List
        Write-Output ''
        Write-Output 'Unused Core asmdef reference candidates:'
        $coreGraphAudit.CoreAsmdefUnusedReferenceScan.Candidates | Format-Table -AutoSize
    }
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
    AupPrecisionSafe = '\b(?:CurrentTotalOffsetDouble|ToAbsoluteUniversePositionDouble3|ToUniverseSpaceDouble3|ToRuntimeSpaceDouble3|FromAbsolutePosition|DistanceSq\s*\(|ToRuntimeSpace\s*\(\s*double3)'
    AupPrecisionRisk = '\bHectonFloatingOrigin\s*\.\s*ToAbsoluteUniversePosition\s*\(|\bHectonMapMagicVegetationBridge\s*\.\s*ToUniverseSpace\s*\(|\bCurrentTotalOffset\s*(?:;|\.)|\b(?:New|Previous)TotalOffset\s*\.|\(float3\)\s*AUP|\bVector3\s+universePosition\b|\bVector3\s+stableUniverseRoot\b'
    LinqSurface = '\.(?:Where|Select|SelectMany|Any|All|First|FirstOrDefault|Last|LastOrDefault|Single|SingleOrDefault|ToList|ToArray|OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy|Sum|Average)\s*\('
    CoroutineSurface = '\bStartCoroutine\s*\('
    ManagedFormatSurface = '(?:\$@?|@?\$)"|(?:string|String)\s*\.\s*Format\s*\(|\.ToString\s*\('
    JobCompleteSurface = '\.Complete\s*\('
}

$patterns = @{}
foreach ($entry in $patternSource.GetEnumerator()) {
    $patterns[$entry.Key] = [System.Text.RegularExpressions.Regex]::new(
        $entry.Value,
        $regexOptions)
}

$patternLiteralHints = @{
    SignalBusPush = @('SignalBus', 'GlobalSignals', 'VehicleCommandSignalBus', 'PhysicsDeterminismSignals', 'FluidFeedbackEvents', 'LocalizationEvents', 'VoxelChunkModifiedEvents')
    GlobalRegistryGet = @('GlobalRegistry')
    GlobalRegistrySurface = @('GlobalRegistry')
    EventPublish = @('HectonEventBus', 'WaterTransitionEvents', 'SuitDamageEvents')
    UnityUpdateMethods = @('Update')
    ISlowTickable = @('ISlowTickable')
    IJob = @('IJob')
    ITickable = @('ITickable')
    IFixedTickable = @('IFixedTickable')
    GlobalDataVaultRefs = @('GlobalDataVault', 'IDataVault', 'VaultBufferHandle', 'GetBuffer', 'TryGetBuffer', 'GetBufferHandle', 'TryGetBufferHandle', 'ResolveBuffer')
    NativeArrayRefs = @('NativeArray')
    StructDeclarations = @('struct')
    StructLayoutAttributes = @('StructLayout')
    BinaryBlittableSafe = @('BinaryBlittableSafe')
    StaticInstance = @('Instance')
    FindObjectCalls = @('FindObject', 'FindFirstObjectByType', 'FindAnyObjectByType', 'FindObjectsByType', 'FindWithTag', 'GameObject.Find', 'Resources.FindObjectsOfTypeAll')
    GetComponentCalls = @('GetComponent')
    DisposeCalls = @('.Dispose')
    AupPrecisionSafe = @('CurrentTotalOffsetDouble', 'ToAbsoluteUniversePositionDouble3', 'ToUniverseSpaceDouble3', 'ToRuntimeSpaceDouble3', 'FromAbsolutePosition', 'DistanceSq', 'ToRuntimeSpace')
    AupPrecisionRisk = @('HectonFloatingOrigin', 'HectonMapMagicVegetationBridge', 'CurrentTotalOffset', 'NewTotalOffset', 'PreviousTotalOffset', 'AUP', 'universePosition', 'stableUniverseRoot')
    LinqSurface = @('.Where', '.Select', '.Any', '.First', '.ToList', '.ToArray', '.OrderBy', '.GroupBy', '.Sum', '.Average')
    CoroutineSurface = @('StartCoroutine')
    ManagedFormatSurface = @('$"', '$@"', '@$"', 'string.Format', 'String.Format', '.ToString')
    JobCompleteSurface = @('.Complete')
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

$duplicateSignalNameAudit = Get-DuplicateSignalNameAudit $files
Assert-DuplicateSignalNameBudget $duplicateSignalNameAudit

foreach ($file in $files) {
    if (-not [System.IO.File]::Exists($file)) {
        continue
    }

    $content = [System.IO.File]::ReadAllText($file)
    $codeContent = if ($LexicalScrub) { ConvertTo-CodeSurface $content } else { $content }
    $lineCount = Count-Lines $content
    $isEditorFile = Test-IsEditorFile $file
    $domain = Get-DomainName $file
    $relativePath = Get-RelativeSourcePath $file
    $fileCounters = New-CounterSet
    Add-Count $fileCounters 'CsFiles' 1
    Add-Count $fileCounters 'Lines' $lineCount
    Add-PatternCounts $fileCounters $codeContent $patterns $patternLiteralHints
    Normalize-UnityLoopCounters $fileCounters $relativePath

    foreach ($key in $fileCounters.Keys) {
        Add-Count $allCounters $key $fileCounters[$key]
    }

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
            [int]$fileCounters['GlobalRegistrySurface'] -gt 0 -or
            [int]$fileCounters['EventPublish'] -gt 0 -or
            [int]$fileCounters['StaticInstance'] -gt 0 -or
            [int]$fileCounters['FindObjectCalls'] -gt 0 -or
            [int]$fileCounters['GetComponentCalls'] -gt 0) {
            [void]$editorFileRows.Add([pscustomobject](New-FileRow $relativePath $domain $fileCounters $lineCount))
        }
        continue
    }

    if ($content.IndexOf('UNITY_EDITOR', [StringComparison]::Ordinal) -lt 0) {
        $runtimeFileCounters = $fileCounters
    }
    else {
        $runtimeContent = Remove-UnityEditorBlocks $content
        $runtimeCodeContent = if ($LexicalScrub) { ConvertTo-CodeSurface $runtimeContent } else { $runtimeContent }
        $runtimeFileCounters = New-CounterSet
        Add-Count $runtimeFileCounters 'CsFiles' 1
        Add-Count $runtimeFileCounters 'Lines' $lineCount
        Add-PatternCounts $runtimeFileCounters $runtimeCodeContent $patterns $patternLiteralHints
        Normalize-UnityLoopCounters $runtimeFileCounters $relativePath
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
        [int]$runtimeFileCounters['GlobalRegistrySurface'] -gt 0 -or
        [int]$runtimeFileCounters['EventPublish'] -gt 0 -or
        [int]$runtimeFileCounters['StaticInstance'] -gt 0 -or
        [int]$runtimeFileCounters['FindObjectCalls'] -gt 0 -or
        [int]$runtimeFileCounters['GetComponentCalls'] -gt 0 -or
        [int]$runtimeFileCounters['LinqSurface'] -gt 0 -or
        [int]$runtimeFileCounters['CoroutineSurface'] -gt 0 -or
        [int]$runtimeFileCounters['ManagedFormatSurface'] -gt 0 -or
        [int]$runtimeFileCounters['JobCompleteSurface'] -gt 0 -or
        [int]$runtimeFileCounters['AupPrecisionRisk'] -gt 0) {
        [void]$runtimeFileRows.Add([pscustomobject](New-FileRow $relativePath $domain $runtimeFileCounters $lineCount))
    }
}

$runtimeScores = New-Scores $runtimeCounters
$allSourceScores = New-Scores $allCounters
$editorScores = New-Scores $editorCounters
$primaryManagedRuntimeRisk = [int](@($runtimeFileRows |
    Measure-Object -Property PrimaryManagedRuntimeRisk -Sum).Sum)
$primaryJobCompleteRisk = [int](@($runtimeFileRows |
    Measure-Object -Property PrimaryJobCompleteRisk -Sum).Sum)

Assert-AupPrecisionBudget $runtimeCounters $runtimeFileRows
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'FindObjectCalls' $MaxFindObjectCalls 'FindObject runtime lookup'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'EventPublish' $MaxLegacyEventPublish 'Legacy event publish'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'GlobalRegistrySurface' $MaxGlobalRegistrySurface 'GlobalRegistry surface'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'GetComponentCalls' $MaxGetComponentCalls 'GetComponent'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'NativeArrayRefs' $MaxNativeArrayRefs 'NativeArray'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'LinqSurface' $MaxLinqSurface 'LINQ runtime surface'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'CoroutineSurface' $MaxCoroutineSurface 'Coroutine runtime surface'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'ManagedFormatSurface' $MaxManagedFormatSurface 'Managed format runtime surface'
Assert-StaticCounterBudget $runtimeCounters $runtimeFileRows 'JobCompleteSurface' $MaxJobCompleteSurface 'Job Complete runtime surface'
Assert-ScalarMaxBudget $primaryManagedRuntimeRisk $MaxPrimaryManagedRuntimeRisk 'Primary managed runtime risk'
Assert-StaticScoreFloor $runtimeScores 'DataSovereignty' $MinDataSovereignty 'Data Sovereignty'
Assert-StaticScoreFloor $runtimeScores 'MemoryAlignment' $MinMemoryAlignment 'Memory Alignment'
Assert-StaticScoreFloor $runtimeScores 'HPhiStaticRisk' $MinRuntimeHPhiRisk 'Runtime risk-adjusted H-Phi'

$result = [ordered]@{
    Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz')
    Scope = 'Assets/_Project/Scripts'
    MetricModel = 'Runtime H-Phi excludes Scripts/Editor from runtime debt counters; Data Sovereignty counts DataVault access surface including IDataVault, VaultBufferHandle, GetBuffer, TryGetBuffer, and GlobalDataVault; risk-adjusted score includes AUP precision integrity from qualified legacy bridge and double-safe AUP patterns; AllSourceCounts is retained for hygiene tracking.'
    CoreGraphAudit = $coreGraphAudit
    DuplicateSignalNameAudit = $duplicateSignalNameAudit
    Counts = $runtimeCounters
    Scores = $runtimeScores
    AllSourceCounts = $allCounters
    AllSourceScores = $allSourceScores
    EditorCounts = $editorCounters
    EditorScores = $editorScores
    RiskSums = [ordered]@{
        PrimaryManagedRuntimeRisk = $primaryManagedRuntimeRisk
        PrimaryJobCompleteRisk = $primaryJobCompleteRisk
    }
    Budgets = [ordered]@{
        AupPrecisionRisk = [ordered]@{
            Enabled = $MaxAupPrecisionRisk -ge 0
            Max = $MaxAupPrecisionRisk
            Actual = [int]$runtimeCounters.AupPrecisionRisk
            Passed = $MaxAupPrecisionRisk -lt 0 -or [int]$runtimeCounters.AupPrecisionRisk -le $MaxAupPrecisionRisk
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        FindObjectCalls = [ordered]@{
            Enabled = $MaxFindObjectCalls -ge 0
            Max = $MaxFindObjectCalls
            Actual = [int]$runtimeCounters.FindObjectCalls
            Passed = $MaxFindObjectCalls -lt 0 -or [int]$runtimeCounters.FindObjectCalls -le $MaxFindObjectCalls
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        LegacyEventPublish = [ordered]@{
            Enabled = $MaxLegacyEventPublish -ge 0
            Max = $MaxLegacyEventPublish
            Actual = [int]$runtimeCounters.EventPublish
            Passed = $MaxLegacyEventPublish -lt 0 -or [int]$runtimeCounters.EventPublish -le $MaxLegacyEventPublish
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        DuplicateSignalNames = [ordered]@{
            Enabled = $MaxDuplicateSignalNames -ge 0
            Max = $MaxDuplicateSignalNames
            Actual = [int]$duplicateSignalNameAudit.DuplicateSignalNameCount
            Passed = $MaxDuplicateSignalNames -lt 0 -or [int]$duplicateSignalNameAudit.DuplicateSignalNameCount -le $MaxDuplicateSignalNames
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        GlobalRegistrySurface = [ordered]@{
            Enabled = $MaxGlobalRegistrySurface -ge 0
            Max = $MaxGlobalRegistrySurface
            Actual = [int]$runtimeCounters.GlobalRegistrySurface
            Passed = $MaxGlobalRegistrySurface -lt 0 -or [int]$runtimeCounters.GlobalRegistrySurface -le $MaxGlobalRegistrySurface
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        GetComponentCalls = [ordered]@{
            Enabled = $MaxGetComponentCalls -ge 0
            Max = $MaxGetComponentCalls
            Actual = [int]$runtimeCounters.GetComponentCalls
            Passed = $MaxGetComponentCalls -lt 0 -or [int]$runtimeCounters.GetComponentCalls -le $MaxGetComponentCalls
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        NativeArrayRefs = [ordered]@{
            Enabled = $MaxNativeArrayRefs -ge 0
            Max = $MaxNativeArrayRefs
            Actual = [int]$runtimeCounters.NativeArrayRefs
            Passed = $MaxNativeArrayRefs -lt 0 -or [int]$runtimeCounters.NativeArrayRefs -le $MaxNativeArrayRefs
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        LinqSurface = [ordered]@{
            Enabled = $MaxLinqSurface -ge 0
            Max = $MaxLinqSurface
            Actual = [int]$runtimeCounters.LinqSurface
            Passed = $MaxLinqSurface -lt 0 -or [int]$runtimeCounters.LinqSurface -le $MaxLinqSurface
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        CoroutineSurface = [ordered]@{
            Enabled = $MaxCoroutineSurface -ge 0
            Max = $MaxCoroutineSurface
            Actual = [int]$runtimeCounters.CoroutineSurface
            Passed = $MaxCoroutineSurface -lt 0 -or [int]$runtimeCounters.CoroutineSurface -le $MaxCoroutineSurface
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        ManagedFormatSurface = [ordered]@{
            Enabled = $MaxManagedFormatSurface -ge 0
            Max = $MaxManagedFormatSurface
            Actual = [int]$runtimeCounters.ManagedFormatSurface
            Passed = $MaxManagedFormatSurface -lt 0 -or [int]$runtimeCounters.ManagedFormatSurface -le $MaxManagedFormatSurface
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        JobCompleteSurface = [ordered]@{
            Enabled = $MaxJobCompleteSurface -ge 0
            Max = $MaxJobCompleteSurface
            Actual = [int]$runtimeCounters.JobCompleteSurface
            Passed = $MaxJobCompleteSurface -lt 0 -or [int]$runtimeCounters.JobCompleteSurface -le $MaxJobCompleteSurface
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        PrimaryManagedRuntimeRisk = [ordered]@{
            Enabled = $MaxPrimaryManagedRuntimeRisk -ge 0
            Max = $MaxPrimaryManagedRuntimeRisk
            Actual = $primaryManagedRuntimeRisk
            Passed = $MaxPrimaryManagedRuntimeRisk -lt 0 -or $primaryManagedRuntimeRisk -le $MaxPrimaryManagedRuntimeRisk
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        DataSovereignty = [ordered]@{
            Enabled = $MinDataSovereignty -ge 0.0
            Min = $MinDataSovereignty
            Actual = [double]$runtimeScores.DataSovereignty
            Passed = $MinDataSovereignty -lt 0.0 -or [double]$runtimeScores.DataSovereignty -ge $MinDataSovereignty
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        MemoryAlignment = [ordered]@{
            Enabled = $MinMemoryAlignment -ge 0.0
            Min = $MinMemoryAlignment
            Actual = [double]$runtimeScores.MemoryAlignment
            Passed = $MinMemoryAlignment -lt 0.0 -or [double]$runtimeScores.MemoryAlignment -ge $MinMemoryAlignment
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
        RuntimeHPhiRisk = [ordered]@{
            Enabled = $MinRuntimeHPhiRisk -ge 0.0
            Min = $MinRuntimeHPhiRisk
            Actual = [double]$runtimeScores.HPhiStaticRisk
            Passed = $MinRuntimeHPhiRisk -lt 0.0 -or [double]$runtimeScores.HPhiStaticRisk -ge $MinRuntimeHPhiRisk
            EvidenceClass = 'STATIC_SOURCE_FULL_SCAN'
        }
    }
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
    TopAupPrecisionRiskFiles = @($runtimeFileRows |
        Where-Object { $_.AupPrecisionRisk -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'AupPrecisionRisk'; Descending = $true },
            @{ Expression = 'AupPrecisionSafe'; Descending = $true }) |
        Select-Object -First 25)
    TopCouplingRiskFiles = @($runtimeFileRows |
        Where-Object { $_.CouplingRisk -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'CouplingRisk'; Descending = $true },
            @{ Expression = 'GlobalRegistrySurface'; Descending = $true },
            @{ Expression = 'GetComponentCalls'; Descending = $true }) |
        Select-Object -First 25)
    TopManagedRuntimeRiskFiles = @($runtimeFileRows |
        Where-Object { $_.ManagedRuntimeRisk -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'ManagedRuntimeRisk'; Descending = $true },
            @{ Expression = 'ManagedFormatSurface'; Descending = $true },
            @{ Expression = 'LinqSurface'; Descending = $true },
            @{ Expression = 'CoroutineSurface'; Descending = $true }) |
        Select-Object -First 25)
    TopPrimaryManagedRuntimeRiskFiles = @($runtimeFileRows |
        Where-Object { $_.PrimaryManagedRuntimeRisk -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'PrimaryManagedRuntimeRisk'; Descending = $true },
            @{ Expression = 'ManagedFormatSurface'; Descending = $true },
            @{ Expression = 'LinqSurface'; Descending = $true }) |
        Select-Object -First 25)
    ManagedRiskByRole = @($runtimeFileRows |
        Group-Object FileRole |
        ForEach-Object {
            [pscustomobject][ordered]@{
                FileRole = $_.Name
                FileCount = @($_.Group).Count
                ManagedRuntimeRisk = [int](@($_.Group | Measure-Object -Property ManagedRuntimeRisk -Sum).Sum)
                LinqSurface = [int](@($_.Group | Measure-Object -Property LinqSurface -Sum).Sum)
                CoroutineSurface = [int](@($_.Group | Measure-Object -Property CoroutineSurface -Sum).Sum)
                ManagedFormatSurface = [int](@($_.Group | Measure-Object -Property ManagedFormatSurface -Sum).Sum)
                JobCompleteSurface = [int](@($_.Group | Measure-Object -Property JobCompleteSurface -Sum).Sum)
            }
        } |
        Sort-Object ManagedRuntimeRisk -Descending)
    TopJobCompleteRiskFiles = @($runtimeFileRows |
        Where-Object { $_.JobCompleteSurface -gt 0 } |
        Sort-Object -Property @(
            @{ Expression = 'JobCompleteSurface'; Descending = $true },
            @{ Expression = 'CouplingRisk'; Descending = $true }) |
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
    Write-Output 'Budgets:'
    ConvertTo-BudgetDisplayRows $summaryResult.Budgets | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Core graph H-Phi debt counts:'
    [pscustomobject]$summaryResult.CoreGraph.Counts | Format-List
    Write-Output ''
    Write-Output 'Core graph H-Phi budgets:'
    ConvertTo-BudgetDisplayRows $summaryResult.CoreGraph.Budgets | Format-Table -AutoSize
    if (@($summaryResult.TopAupPrecisionRiskFiles).Count -gt 0) {
        Write-Output ''
        Write-Output 'Top AUP precision risk files:'
        $summaryResult.TopAupPrecisionRiskFiles | Format-Table -AutoSize
    }
    Write-Output ''
    Write-Output 'Top coupling risk files:'
    $summaryResult.TopCouplingRiskFiles | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Top managed runtime risk files:'
    $summaryResult.TopManagedRuntimeRiskFiles | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Top primary managed runtime risk files:'
    $summaryResult.TopPrimaryManagedRuntimeRiskFiles | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Managed runtime risk by role:'
    $summaryResult.ManagedRiskByRole | Format-Table -AutoSize
    Write-Output ''
    Write-Output 'Top job Complete risk files:'
    $summaryResult.TopJobCompleteRiskFiles | Format-Table -AutoSize
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
