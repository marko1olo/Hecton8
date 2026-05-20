param(
    [string]$ProjectRoot = "",
    [string]$OutputJson = "",
    [string]$OutputMarkdown = "",
    [ValidateSet("Full", "SignalCritical")]
    [string]$Scope = "Full",
    [switch]$IncludeHotPathHeuristics,
    [switch]$FailOnError
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
}

$scriptsRoot = Join-Path $ProjectRoot "Assets/_Project/Scripts"
$assetsRoot = Join-Path $ProjectRoot "Assets"
$agentLogRoot = Join-Path $ProjectRoot "Docs/AgentLogs"
if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $OutputJson = Join-Path $agentLogRoot "SignalBusContractAudit_SHINOBU_02.json"
}
if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $agentLogRoot "SignalBusContractAudit_SHINOBU_02.md"
}

if (-not (Test-Path -LiteralPath $scriptsRoot)) {
    throw "Scripts root not found: $scriptsRoot"
}
if (-not (Test-Path -LiteralPath $agentLogRoot)) {
    New-Item -ItemType Directory -Path $agentLogRoot | Out-Null
}

$findings = New-Object System.Collections.Generic.List[object]
$signalDefinitions = New-Object System.Collections.Generic.List[object]
$scannedFiles = 0
$shaderFilesScanned = 0
$pack1Count = 0
$runtimeSignalPack1Count = 0
$managedEventCount = 0
$localNativeTelemetryCount = 0
$registeredLocalTelemetryCount = 0
$hotPathRiskCount = 0
$computeThreadGroupRiskCount = 0

function Convert-ToRelativePath {
    param([string]$Path)

    $full = $Path
    if (-not [System.IO.Path]::IsPathRooted($full)) {
        $full = [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $full))
    }
    if ($full.StartsWith($ProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("\", "/")
    }

    return $full.Replace("\", "/")
}

function Remove-CodeTrivia {
    param([string]$Line)

    if ([string]::IsNullOrEmpty($Line)) {
        return ""
    }

    $commentIndex = $Line.IndexOf("//", [System.StringComparison]::Ordinal)
    if ($commentIndex -ge 0) {
        return $Line.Substring(0, $commentIndex)
    }

    return $Line
}

function Add-Finding {
    param(
        [ValidateSet("ERROR", "WARN", "INFO")]
        [string]$Severity,
        [string]$Rule,
        [int]$Confidence,
        [string]$Classification,
        [string]$EvidenceKind,
        [string]$Path,
        [int]$Line,
        [string]$Symbol,
        [string]$Evidence,
        [string]$RequiredAction,
        [hashtable]$Tags = @{}
    )

    if ($Confidence -lt 1) {
        $Confidence = 1
    } elseif ($Confidence -gt 100) {
        $Confidence = 100
    }

    $findings.Add([pscustomobject]@{
        severity = $Severity
        rule = $Rule
        confidence = $Confidence
        classification = $Classification
        evidenceKind = $EvidenceKind
        path = $Path
        line = $Line
        symbol = $Symbol
        evidence = $Evidence.Trim()
        requiredAction = $RequiredAction
        tags = [pscustomobject]$Tags
    }) | Out-Null
}

function Count-BraceDelta {
    param([string]$Line)

    $open = 0
    $close = 0
    for ($i = 0; $i -lt $Line.Length; $i++) {
        if ($Line[$i] -eq "{") {
            $open++
        } elseif ($Line[$i] -eq "}") {
            $close++
        }
    }

    return $open - $close
}

function Test-IsEditorPath {
    param([string]$RelativePath)

    return $RelativePath -match "(^|/)Editor(/|$)|(^|/)Tests/Editor(/|$)|SmokeTester|SmokeTest|Automation|QA/Headless|TOOL_"
}

function Test-IsCoreSignalFile {
    param([string]$RelativePath)

    return $RelativePath -match "Core/GlobalSignals\.cs$|Core/Signals/"
}

function Test-IsFileFormatLike {
    param(
        [string]$RelativePath,
        [string]$Symbol
    )

    $combined = "$RelativePath/$Symbol"
    return $combined -match "Save|Persistence|Persist|Serialize|Deserialize|Binary|Codec|Compression|Archive|Header|Record|Wal|WAL|Pager|Page|Snapshot|Modding|Protocol|Manifest|Layout|Disk|FileFormat|StaticData|DataArena"
}

function Test-IsSignalLikeName {
    param([string]$Symbol)

    return $Symbol -match "(Signal|Command|Packet|Telemetry|BlackBox|Aup|AbsoluteUniversePosition)$|Telemetry|BlackBox"
}

function Test-IsHotMethodName {
    param([string]$MethodName)

    return $MethodName -match "^(Tick|Update|LateUpdate|FixedUpdate|Execute|OnUpdate|Run|Schedule|Simulate|Step|Process|Dispatch|Flush|Render|Sync)"
}

function Resolve-SyncFileIoFinding {
    param(
        [string]$RelativePath,
        [string]$ClassName,
        [string]$MethodName,
        [bool]$InsideEditorOrDevelopmentGuard
    )

    $tags = @{
        isEditor = $false
        className = $ClassName
        methodName = $MethodName
        insideEditorOrDevelopmentGuard = $InsideEditorOrDevelopmentGuard
    }

    $default = [pscustomobject]@{
        severity = "WARN"
        rule = "RUNTIME_SYNC_FILE_IO_REVIEW"
        confidence = 76
        classification = "IO_PRESSURE_HEURISTIC"
        requiredAction = "Confirm this synchronous file I/O is cold/fatal only. Runtime WAL/save paths should stage work off the main thread."
        tags = $tags
    }

    if ($RelativePath -eq "Assets/_Project/Scripts/Core/SystemDispatcher.cs") {
        if ($MethodName -match "^(DumpMasterFenceTelemetry|DumpMasterPipelineTelemetry|DumpDispatcherBlackBox|WriteDispatcherBlackBoxDump)$") {
            $tags.syncIoContext = "dispatcher_fault_blackbox"
            return [pscustomobject]@{
                severity = "INFO"
                rule = "FAULT_BLACKBOX_SYNC_DUMP_REVIEW"
                confidence = 82
                classification = "FAULT_BLACKBOX_SYNC_IO"
                requiredAction = "Static context classifies this as dispatcher fault/stall blackbox I/O. Keep call sites one-shot or backoff-gated; do not reuse this path for WAL/save or normal runtime persistence."
                tags = $tags
            }
        }

        if ($MethodName -eq "ParseMasterExecutionPriorityCsv" -and $InsideEditorOrDevelopmentGuard) {
            $tags.syncIoContext = "editor_development_csv_hotswap"
            return [pscustomobject]@{
                severity = "INFO"
                rule = "EDITOR_DEV_CSV_HOTSWAP_SYNC_IO_REVIEW"
                confidence = 84
                classification = "EDITOR_DEV_CSV_HOTSWAP_SYNC_IO"
                requiredAction = "Static context classifies this as UNITY_EDITOR/DEVELOPMENT_BUILD tuning I/O. Keep it out of release-player paths or stage it off-thread before production use."
                tags = $tags
            }
        }
    }

    if ($RelativePath -eq "Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs") {
        if (($ClassName -eq "SignalTelemetryRingBuffer" -or $ClassName -eq "SignalThreadLocalScratchpad") -and $MethodName -eq "DumpToDisk") {
            $tags.syncIoContext = "signal_fault_blackbox"
            return [pscustomobject]@{
                severity = "INFO"
                rule = "FAULT_BLACKBOX_SYNC_DUMP_REVIEW"
                confidence = 82
                classification = "FAULT_BLACKBOX_SYNC_IO"
                requiredAction = "Static context classifies this as SignalBus fault blackbox I/O. Keep runtime callers one-shot or 300-frame backoff-gated; do not reuse this path for normal persistence."
                tags = $tags
            }
        }

        if (($ClassName -eq "SignalPriorityTable" -and $MethodName -eq "TryLoadFile") -or
            ($ClassName -eq "SignalTuningCsvHotSwap" -and $MethodName -eq "TryLoad") -or
            ($ClassName -eq "SignalThreadContentionCsvHotSwap" -and $MethodName -eq "TryLoad")) {
            $tags.syncIoContext = "cold_bootstrap_config"
            return [pscustomobject]@{
                severity = "INFO"
                rule = "COLD_BOOTSTRAP_CONFIG_SYNC_IO_REVIEW"
                confidence = 80
                classification = "COLD_BOOTSTRAP_CONFIG_SYNC_IO"
                requiredAction = "Static context classifies this as boot/editor configuration ingestion. Keep it out of Tick/dispatcher hot paths and keep file size bounded by the existing scratch buffers."
                tags = $tags
            }
        }
    }

    return $default
}

function New-StructMetadata {
    param(
        [string]$Name,
        [string]$Declaration,
        [string]$RelativePath,
        [int]$LineNumber,
        [bool]$HasLayout,
        [bool]$ImplementsISignal
    )

    return [pscustomobject]@{
        name = $Name
        declaration = $Declaration.Trim()
        path = $RelativePath
        line = $LineNumber
        hasStructLayout = $HasLayout
        implementsISignal = $ImplementsISignal
        isEditor = Test-IsEditorPath $RelativePath
        isCoreGlobalSignals = $RelativePath -eq "Assets/_Project/Scripts/Core/GlobalSignals.cs"
        isCoreSignalFile = Test-IsCoreSignalFile $RelativePath
        isSignalLikeName = Test-IsSignalLikeName $Name
    }
}

function Test-HasStructLayoutBefore {
    param(
        [string[]]$CodeLines,
        [int]$Index
    )

    $start = [Math]::Max(0, $Index - 8)
    for ($i = $Index; $i -ge $start; $i--) {
        if ($CodeLines[$i] -match "\[StructLayout") {
            return $true
        }
        if ($i -ne $Index -and $CodeLines[$i] -match "^\s*(?:public|internal|private|protected)?\s*(?:class|interface|enum)\s+") {
            return $false
        }
    }

    return $false
}

function Test-StructImplementsISignal {
    param(
        [string[]]$CodeLines,
        [int]$Index
    )

    $builder = New-Object System.Text.StringBuilder
    $limit = [Math]::Min($CodeLines.Length - 1, $Index + 3)
    for ($i = $Index; $i -le $limit; $i++) {
        [void]$builder.Append(" ")
        [void]$builder.Append($CodeLines[$i])
        if ($CodeLines[$i] -match "{") {
            break
        }
    }

    return $builder.ToString() -match ":\s*[^{};]*\bISignal\b"
}

function Find-NearestStructMetadata {
    param(
        [hashtable]$StructByIndex,
        [int]$Index
    )

    for ($offset = 0; $offset -le 24; $offset++) {
        $candidateIndex = $Index + $offset
        if ($StructByIndex.ContainsKey($candidateIndex)) {
            return $StructByIndex[$candidateIndex]
        }
    }

    for ($offset = 1; $offset -le 8; $offset++) {
        $candidateIndex = $Index - $offset
        if ($StructByIndex.ContainsKey($candidateIndex)) {
            return $StructByIndex[$candidateIndex]
        }
    }

    return $null
}

function Test-StructBodyContainsWideField {
    param(
        [string[]]$CodeLines,
        [int]$Index
    )

    $limit = [Math]::Min($CodeLines.Length - 1, $Index + 100)
    for ($i = $Index; $i -le $limit; $i++) {
        if ($i -gt $Index -and $CodeLines[$i] -match "^\s*\[StructLayout") {
            return $false
        }
        if ($i -gt $Index -and $CodeLines[$i] -match "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:readonly|partial|unsafe|ref)\s+)*struct\s+[A-Za-z_][A-Za-z0-9_]*\b") {
            return $false
        }
        if ($CodeLines[$i] -match "\b(double|double2|double3|double4|long|ulong|IntPtr|UIntPtr)\b") {
            return $true
        }
    }

    return $false
}

function Test-FileHasOwnershipPath {
    param(
        [string]$RawText,
        [string]$DeclarationLine,
        [string]$FieldName,
        [string]$CollectionKind
    )

    $escaped = [regex]::Escape($FieldName)
    $registerToken = "RegisterNative$CollectionKind"
    $unregisterToken = "UnregisterNative$CollectionKind"

    $hasRegister = $false
    $hasUnregister = $false
    $hasDispose = $false
    $hasVaultAlias = $false

    $handlePattern = "\bVaultBufferHandle\s*<[^>]+>\s+$escaped" + "Handle\b"
    if ($DeclarationLine -match "(?i)Vault alias|GlobalDataVault owns|VaultBufferHandle") {
        $hasVaultAlias = $true
    } elseif ($DeclarationLine.IndexOf("ResolveNativeBuffer", [System.StringComparison]::Ordinal) -ge 0) {
        $hasVaultAlias = $true
    } elseif ($RawText -match "(?i)(Vault alias|GlobalDataVault owns)[^\r\n]*\b$escaped\b|\b$escaped\b[^\r\n]*(Vault alias|GlobalDataVault owns)") {
        $hasVaultAlias = $true
    } elseif ($RawText -match $handlePattern -or
        $RawText -match "$escaped\s*=\s*[A-Za-z_][A-Za-z0-9_]*Handle\s*\.\s*Resolve\s*\(" -or
        $RawText -match "$escaped\s*=\s*ResolveNativeBuffer\s*<") {
        $hasVaultAlias = $true
    }

    if ($RawText.IndexOf($registerToken, [System.StringComparison]::Ordinal) -ge 0 -and
        $RawText.IndexOf($FieldName, [System.StringComparison]::Ordinal) -ge 0) {
        $hasRegister = $RawText -match "(?s)$registerToken\s*\([^;]*($escaped|nameof\s*\(\s*$escaped\s*\))"
    }
    if ($RawText.IndexOf($unregisterToken, [System.StringComparison]::Ordinal) -ge 0 -and
        $RawText.IndexOf($FieldName, [System.StringComparison]::Ordinal) -ge 0) {
        $hasUnregister = $RawText -match "(?s)$unregisterToken\s*\([^;]*($escaped|nameof\s*\(\s*$escaped\s*\))"
    }
    if ($RawText.IndexOf(".Dispose", [System.StringComparison]::Ordinal) -ge 0 -and
        $RawText.IndexOf($FieldName, [System.StringComparison]::Ordinal) -ge 0) {
        $hasDispose = $RawText -match "(?s)$escaped\s*\.\s*Dispose\s*\("
    }
    $hasHelperDispose = $false
    $fieldPassedToDisposeHelper = $RawText -match "(?s)\b(?:Dispose|Release)[A-Za-z0-9_]*(?:Array|Buffer|Native)[A-Za-z0-9_]*\s*\(\s*(?:ref\s+)?$escaped\b"
    $helperUnregistersArray = $RawText.IndexOf($unregisterToken, [System.StringComparison]::Ordinal) -ge 0 -and
        $RawText -match "(?is)$unregisterToken\s*\([^;]*(array|buffer|nativeArray)"
    $helperDisposesArray = $RawText -match "(?is)\b(array|buffer|nativeArray)\s*\.\s*Dispose\s*\("
    if ($fieldPassedToDisposeHelper -and $helperUnregistersArray -and $helperDisposesArray) {
        $hasHelperDispose = $true
        $hasUnregister = $true
        $hasDispose = $true
    }

    return [pscustomobject]@{
        hasRegister = $hasRegister
        hasUnregister = $hasUnregister
        hasDispose = $hasDispose
        hasVaultAlias = $hasVaultAlias
        hasHelperDispose = $hasHelperDispose
        isOwned = $hasRegister -and $hasUnregister -and $hasDispose
    }
}

function Get-ContainerTypes {
    param([string[]]$CodeLines)

    $signalBusTypes = New-Object "System.Collections.Generic.HashSet[string]"
    $nativeQueueTypes = New-Object "System.Collections.Generic.HashSet[string]"
    $nativeListTypes = New-Object "System.Collections.Generic.HashSet[string]"
    $nativeArrayTypes = New-Object "System.Collections.Generic.HashSet[string]"

    foreach ($code in $CodeLines) {
        if ($code.IndexOf("<", [System.StringComparison]::Ordinal) -lt 0) {
            continue
        }
        if ($code.IndexOf("SignalBus", [System.StringComparison]::Ordinal) -lt 0 -and
            $code.IndexOf("NativeQueue", [System.StringComparison]::Ordinal) -lt 0 -and
            $code.IndexOf("NativeList", [System.StringComparison]::Ordinal) -lt 0 -and
            $code.IndexOf("NativeArray", [System.StringComparison]::Ordinal) -lt 0) {
            continue
        }

        if ($code.IndexOf("SignalBus", [System.StringComparison]::Ordinal) -ge 0) {
            foreach ($match in [regex]::Matches($code, "\bSignalBus\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>")) {
                [void]$signalBusTypes.Add($match.Groups[1].Value)
            }
        }
        if ($code.IndexOf("NativeQueue", [System.StringComparison]::Ordinal) -ge 0) {
            foreach ($match in [regex]::Matches($code, "\bNativeQueue\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>")) {
                [void]$nativeQueueTypes.Add($match.Groups[1].Value)
            }
        }
        if ($code.IndexOf("NativeList", [System.StringComparison]::Ordinal) -ge 0) {
            foreach ($match in [regex]::Matches($code, "\bNativeList\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>")) {
                [void]$nativeListTypes.Add($match.Groups[1].Value)
            }
        }
        if ($code.IndexOf("NativeArray", [System.StringComparison]::Ordinal) -ge 0) {
            foreach ($match in [regex]::Matches($code, "\bNativeArray\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>")) {
                [void]$nativeArrayTypes.Add($match.Groups[1].Value)
            }
        }
    }

    return [pscustomobject]@{
        signalBus = $signalBusTypes
        nativeQueue = $nativeQueueTypes
        nativeList = $nativeListTypes
        nativeArray = $nativeArrayTypes
    }
}

$files = Get-ChildItem -LiteralPath $scriptsRoot -Filter "*.cs" -Recurse
if ($Scope -eq "SignalCritical") {
    $files = @($files | Where-Object {
        $candidate = $_.FullName
        if ($candidate.StartsWith($ProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $candidate = $candidate.Substring($ProjectRoot.Length).TrimStart("\", "/").Replace("\", "/")
        } else {
            $candidate = $candidate.Replace("\", "/")
        }

        $candidate -match "Assets/_Project/Scripts/Core/GlobalSignals\.cs$|Assets/_Project/Scripts/Core/Signals/|Assets/_Project/Scripts/Core/SystemDispatcher\.cs$|Assets/_Project/Scripts/Editor/SignalTrafficMonitorWindow\.cs$"
    })
}
foreach ($file in $files) {
    $relativePath = Convert-ToRelativePath $file.FullName
    $rawText = [System.IO.File]::ReadAllText($file.FullName)
    $scannedFiles++

    $hasRelevantText =
        $rawText.IndexOf("struct", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("StructLayout", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("NativeArray", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("NativeQueue", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("NativeList", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("SignalBus", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("UnityEvent", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("Action", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("Func", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("SendMessage", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("BroadcastMessage", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("File.", [System.StringComparison]::Ordinal) -ge 0 -or
        $rawText.IndexOf("Directory.", [System.StringComparison]::Ordinal) -ge 0

    if (-not $hasRelevantText) {
        continue
    }

    $rawLines = [System.IO.File]::ReadAllLines($file.FullName)
    $codeLines = New-Object string[] $rawLines.Length
    for ($i = 0; $i -lt $rawLines.Length; $i++) {
        $codeLines[$i] = Remove-CodeTrivia $rawLines[$i]
    }

    $isEditor = Test-IsEditorPath $relativePath
    $isCoreSignalFile = Test-IsCoreSignalFile $relativePath
    $containerTypes = Get-ContainerTypes $codeLines

    $structs = New-Object System.Collections.Generic.List[object]
    $structByIndex = @{}
    for ($lineIndex = 0; $lineIndex -lt $codeLines.Length; $lineIndex++) {
        $code = $codeLines[$lineIndex]
        if ($code.IndexOf("struct", [System.StringComparison]::Ordinal) -ge 0 -and
            $code -match "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:readonly|partial|unsafe|ref)\s+)*struct\s+([A-Za-z_][A-Za-z0-9_]*)\b") {
            $name = $Matches[1]
            $metadata = New-StructMetadata `
                -Name $name `
                -Declaration $code `
                -RelativePath $relativePath `
                -LineNumber ($lineIndex + 1) `
                -HasLayout (Test-HasStructLayoutBefore $codeLines $lineIndex) `
                -ImplementsISignal (Test-StructImplementsISignal $codeLines $lineIndex)
            $metadata | Add-Member -NotePropertyName index -NotePropertyValue $lineIndex
            $structs.Add($metadata) | Out-Null
            $structByIndex[$lineIndex] = $metadata

            $strictSignal = -not $metadata.isEditor -and ($metadata.implementsISignal -or $metadata.isCoreSignalFile -or $containerTypes.signalBus.Contains($name) -or $containerTypes.nativeQueue.Contains($name))
            if ($metadata.isSignalLikeName -or $metadata.implementsISignal) {
                $signalDefinitions.Add([pscustomobject]@{
                    name = $metadata.name
                    path = $metadata.path
                    line = $metadata.line
                    hasStructLayout = $metadata.hasStructLayout
                    implementsISignal = $metadata.implementsISignal
                    isEditor = $metadata.isEditor
                    inCoreGlobalSignals = $metadata.isCoreGlobalSignals
                    isStrictRuntimeContract = $strictSignal
                }) | Out-Null
            }

            $advisorySignal = $metadata.isSignalLikeName -or $name -match "(Signal|Command|Packet)$"
            if ($advisorySignal -and -not $metadata.hasStructLayout) {
                if ($strictSignal) {
                    Add-Finding "WARN" "SIGNAL_LAYOUT_UNDECLARED" 86 "PROBABLE_RUNTIME_PAYLOAD" "ANCHORED_STRUCT_DECLARATION" $relativePath ($lineIndex + 1) $name $rawLines[$lineIndex] "Add explicit StructLayout or document unmanaged field order before this payload crosses Burst/native/binary boundaries." @{
                        isEditor = $metadata.isEditor
                        implementsISignal = $metadata.implementsISignal
                        isCoreSignalFile = $metadata.isCoreSignalFile
                    }
                } elseif ($metadata.isEditor) {
                    Add-Finding "INFO" "EDITOR_SIGNAL_LAYOUT_REVIEW" 55 "EDITOR_ONLY_REVIEW" "ANCHORED_STRUCT_DECLARATION" $relativePath ($lineIndex + 1) $name $rawLines[$lineIndex] "Editor/test signal-like structs do not gate runtime, but should not shadow production contracts." @{
                        isEditor = $metadata.isEditor
                        implementsISignal = $metadata.implementsISignal
                    }
                } else {
                    Add-Finding "WARN" "SIGNAL_LAYOUT_REVIEW" 65 "NAME_BASED_REVIEW" "ANCHORED_STRUCT_DECLARATION" $relativePath ($lineIndex + 1) $name $rawLines[$lineIndex] "Confirm whether this signal-like struct crosses native/Burst boundaries; if yes, add explicit layout." @{
                        isEditor = $metadata.isEditor
                        implementsISignal = $metadata.implementsISignal
                    }
                }
            }
        }
    }

    $currentStruct = $null
    $currentStructIsSignalCandidate = $false
    $currentStructIsStrictRuntimeContract = $false
    $structBraceDepth = 0
    $structStarted = $false
    $currentHotMethod = ""
    $hotMethodBraceDepth = 0
    $hotMethodStarted = $false
    $currentClass = ""
    $currentMethod = ""
    $methodBraceDepth = 0
    $methodStarted = $false
    $editorOrDevelopmentGuardDepth = 0

    for ($lineIndex = 0; $lineIndex -lt $codeLines.Length; $lineIndex++) {
        $line = $rawLines[$lineIndex]
        $code = $codeLines[$lineIndex]
        $trimmed = $code.TrimStart()
        $lineNumber = $lineIndex + 1

        if ($trimmed -match "^#if\b") {
            if ($editorOrDevelopmentGuardDepth -gt 0 -or $trimmed -match "UNITY_EDITOR|DEVELOPMENT_BUILD") {
                $editorOrDevelopmentGuardDepth++
            }
        } elseif ($trimmed -match "^#endif\b" -and $editorOrDevelopmentGuardDepth -gt 0) {
            $editorOrDevelopmentGuardDepth--
        }

        if ($trimmed.Length -eq 0) {
            continue
        }

        if ($code.IndexOf("class", [System.StringComparison]::Ordinal) -ge 0 -and
            $code -match "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|sealed|partial|abstract)\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)\b") {
            $currentClass = $Matches[1]
        }

        $methodDeclMatch = $null
        if ($code.IndexOf("(", [System.StringComparison]::Ordinal) -ge 0) {
            $methodDeclMatch = [regex]::Match($code, "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|unsafe|virtual|override|sealed|async|partial)\s+)*(?:void|bool|int|float|double|JobHandle|ValueTask|Task|NativeArray<[^>]+>|NativeList<[^>]+>|[A-Za-z_][A-Za-z0-9_<>,\s\.\[\]]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>]+>)?\s*\(")
        }
        if ($null -ne $methodDeclMatch -and $methodDeclMatch.Success) {
            $currentMethod = $methodDeclMatch.Groups[1].Value
            $methodBraceDepth = Count-BraceDelta $code
            $methodStarted = $code.Contains("{")
        } elseif ($currentMethod.Length -gt 0) {
            if ($code.Contains("{")) {
                $methodStarted = $true
            }
            if ($methodStarted) {
                $methodBraceDepth += Count-BraceDelta $code
                if ($methodBraceDepth -le 0 -and $code.Contains("}")) {
                    $currentMethod = ""
                    $methodBraceDepth = 0
                    $methodStarted = $false
                }
            }
        }

        if ($IncludeHotPathHeuristics) {
            $declMatch = $null
            if ($code.IndexOf("(", [System.StringComparison]::Ordinal) -ge 0 -and
                $code -match "\b(Tick|Update|LateUpdate|FixedUpdate|Execute|OnUpdate|Run|Schedule|Simulate|Step|Process|Dispatch|Flush|Render|Sync)\b") {
                $declMatch = [regex]::Match($code, "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:static|unsafe|virtual|override|sealed|async|partial)\s+)*(?:void|bool|int|float|double|JobHandle|ValueTask|Task|NativeArray<[^>]+>|NativeList<[^>]+>|[A-Za-z_][A-Za-z0-9_<>,\s\.\[\]]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(")
            }
            if ($null -ne $declMatch -and $declMatch.Success -and (Test-IsHotMethodName $declMatch.Groups[1].Value)) {
                $currentHotMethod = $declMatch.Groups[1].Value
                $hotMethodBraceDepth = Count-BraceDelta $code
                $hotMethodStarted = $code.Contains("{")
            } elseif ($currentHotMethod.Length -gt 0) {
                if ($code.Contains("{")) {
                    $hotMethodStarted = $true
                }
                if ($hotMethodStarted) {
                    $hotMethodBraceDepth += Count-BraceDelta $code
                    if ($hotMethodBraceDepth -le 0 -and $code.Contains("}")) {
                        $currentHotMethod = ""
                        $hotMethodBraceDepth = 0
                        $hotMethodStarted = $false
                    }
                }
            }
        }

        if ($code.IndexOf("struct", [System.StringComparison]::Ordinal) -ge 0 -and
            $code -match "^\s*(?:(?:public|internal|private|protected)\s+)*(?:(?:readonly|partial|unsafe|ref)\s+)*struct\s+([A-Za-z_][A-Za-z0-9_]*)\b") {
            $structName = $Matches[1]
            $currentStruct = $structByIndex[$lineIndex]
            $currentStructIsSignalCandidate = $false
            $currentStructIsStrictRuntimeContract = $false
            if ($null -ne $currentStruct) {
                $currentStructIsSignalCandidate = $currentStruct.implementsISignal -or $currentStruct.isSignalLikeName -or $containerTypes.signalBus.Contains($currentStruct.name) -or $containerTypes.nativeQueue.Contains($currentStruct.name)
                $currentStructIsStrictRuntimeContract = -not $currentStruct.isEditor -and ($currentStruct.implementsISignal -or $currentStruct.isCoreSignalFile -or $containerTypes.signalBus.Contains($currentStruct.name) -or $containerTypes.nativeQueue.Contains($currentStruct.name))
            }
            $structBraceDepth = Count-BraceDelta $code
            $structStarted = $code.Contains("{")
        } elseif ($null -ne $currentStruct) {
            if ($code.Contains("{")) {
                $structStarted = $true
            }
            if ($structStarted) {
                $structBraceDepth += Count-BraceDelta $code
                if ($structBraceDepth -le 0 -and $code.Contains("}")) {
                    $currentStruct = $null
                    $currentStructIsSignalCandidate = $false
                    $currentStructIsStrictRuntimeContract = $false
                    $structStarted = $false
                    $structBraceDepth = 0
                }
            }
        }

        if ($code.IndexOf("StructLayout", [System.StringComparison]::Ordinal) -ge 0 -and
            $code.IndexOf("Pack", [System.StringComparison]::Ordinal) -ge 0 -and
            $code -match "\[StructLayout\([^\]]*Pack\s*=\s*1") {
            $pack1Count++
            $metadata = Find-NearestStructMetadata $structByIndex $lineIndex
            $symbol = ""
            $implementsISignal = $false
            $symbolIsEditor = $isEditor
            $symbolIsCoreSignalFile = $isCoreSignalFile
            $symbolIsSignalLike = $false
            if ($null -ne $metadata) {
                $symbol = $metadata.name
                $implementsISignal = $metadata.implementsISignal
                $symbolIsEditor = $metadata.isEditor
                $symbolIsCoreSignalFile = $metadata.isCoreSignalFile
                $symbolIsSignalLike = $metadata.isSignalLikeName
            }

            $usedAsSignalContainer = $containerTypes.signalBus.Contains($symbol) -or $containerTypes.nativeQueue.Contains($symbol) -or $containerTypes.nativeList.Contains($symbol)
            $usedAsNativeArray = $containerTypes.nativeArray.Contains($symbol)
            $fileFormatLike = Test-IsFileFormatLike $relativePath $symbol
            $strictRuntimeSignal = -not $symbolIsEditor -and ($implementsISignal -or $symbolIsCoreSignalFile -or $usedAsSignalContainer)
            $wideField = Test-StructBodyContainsWideField $codeLines $lineIndex

            if ($strictRuntimeSignal) {
                $runtimeSignalPack1Count++
                $confidence = 90
                if ($implementsISignal -or $usedAsSignalContainer) {
                    $confidence = 96
                }
                Add-Finding "ERROR" "RUNTIME_SIGNAL_PACK1_FORBIDDEN" $confidence "CONFIRMED_OR_PROBABLE_RUNTIME_SIGNAL" "STRUCTLAYOUT_ATTRIBUTE" $relativePath $lineNumber $symbol $line "Remove Pack=1 from runtime signal/native payloads. Reorder wide fields first, use explicit padding, and keep sizeof(T) a multiple of 8." @{
                    isEditor = $symbolIsEditor
                    implementsISignal = $implementsISignal
                    isCoreSignalFile = $symbolIsCoreSignalFile
                    usedAsSignalContainer = $usedAsSignalContainer
                    fileFormatLike = $fileFormatLike
                }
            } elseif ($symbolIsEditor) {
                Add-Finding "INFO" "EDITOR_PACK1_REVIEW" 50 "EDITOR_ONLY_REVIEW" "STRUCTLAYOUT_ATTRIBUTE" $relativePath $lineNumber $symbol $line "Editor/test Pack=1 does not gate runtime memory, but avoid copying it into player DTOs." @{
                    isEditor = $true
                    fileFormatLike = $fileFormatLike
                }
            } elseif ($fileFormatLike) {
                Add-Finding "INFO" "PACK1_FILE_FORMAT_BOUNDARY_REVIEW" 62 "FILE_FORMAT_OR_SERIALIZATION_CANDIDATE" "STRUCTLAYOUT_ATTRIBUTE" $relativePath $lineNumber $symbol $line "If this is disk/network/binary layout, keep it behind a codec boundary and do not pass it to Burst/native runtime memory." @{
                    isEditor = $false
                    fileFormatLike = $true
                    usedAsNativeArray = $usedAsNativeArray
                }
            } elseif ($symbolIsSignalLike -or $usedAsNativeArray) {
                Add-Finding "WARN" "PACK1_RUNTIME_NATIVE_REVIEW" 78 "PROBABLE_RUNTIME_NATIVE_PAYLOAD" "STRUCTLAYOUT_ATTRIBUTE" $relativePath $lineNumber $symbol $line "Confirm this runtime/native payload is not in hot memory. Prefer natural alignment and explicit padding on ARM64." @{
                    isEditor = $false
                    isSignalLikeName = $symbolIsSignalLike
                    usedAsNativeArray = $usedAsNativeArray
                }
            } else {
                Add-Finding "WARN" "PACK1_REQUIRES_OWNER_JUSTIFICATION" 68 "STATIC_LAYOUT_REVIEW" "STRUCTLAYOUT_ATTRIBUTE" $relativePath $lineNumber $symbol $line "Document why Pack=1 is safe here, or replace it with explicit layout/padding before it enters runtime native memory." @{
                    isEditor = $false
                    fileFormatLike = $fileFormatLike
                }
            }

            if ($wideField) {
                if ($strictRuntimeSignal -or ($symbolIsSignalLike -and -not $symbolIsEditor)) {
                    Add-Finding "ERROR" "PACK1_WIDE_FIELD_ALIGNMENT_RISK" 98 "CONFIRMED_ARM64_ALIGNMENT_RISK" "STRUCT_BODY_FIELD_SCAN" $relativePath $lineNumber $symbol $line "This Pack=1 struct contains double/long/pointer-sized fields. Reorder 8-byte fields first and add explicit padding to 8-byte size." @{
                        isEditor = $symbolIsEditor
                        implementsISignal = $implementsISignal
                        usedAsSignalContainer = $usedAsSignalContainer
                    }
                } else {
                    Add-Finding "WARN" "PACK1_WIDE_FIELD_REVIEW" 84 "PROBABLE_ARM64_ALIGNMENT_RISK" "STRUCT_BODY_FIELD_SCAN" $relativePath $lineNumber $symbol $line "Pack=1 plus 8-byte fields is risky on ARM64 even outside signal lanes. Verify it never enters runtime native memory." @{
                        isEditor = $symbolIsEditor
                        fileFormatLike = $fileFormatLike
                    }
                }
            }
        }

        if (($code.IndexOf("Action", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Func", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("UnityEvent", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("SendMessage", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("BroadcastMessage", [System.StringComparison]::Ordinal) -ge 0) -and
            ($relativePath -match "Signal|Signals|Events|Core/GlobalSignals\.cs|Core/Contracts") -and
            $code -match "\b(event\s+(System\.)?Action|UnityEvent|SendMessage\s*\(|BroadcastMessage\s*\(|SendMessageUpwards\s*\(|System\.Action|System\.Func|Action<|Func<)") {
            $managedEventCount++
            if ($isEditor) {
                Add-Finding "WARN" "EDITOR_MANAGED_EVENT_SURFACE_REVIEW" 62 "EDITOR_ONLY_REVIEW" "SANITIZED_LINE_REGEX" $relativePath $lineNumber "" $line "Editor managed delegates are not runtime transport, but do not copy this surface into player signal paths." @{
                    isEditor = $true
                }
            } else {
                Add-Finding "ERROR" "MANAGED_EVENT_SURFACE_IN_SIGNAL_DOMAIN" 88 "PROBABLE_RUNTIME_TRANSPORT_VIOLATION" "SANITIZED_LINE_REGEX" $relativePath $lineNumber "" $line "Route broadcasts through unmanaged SignalBus<T> lanes or cold GlobalRegistry interfaces. Do not add managed delegates to transport surfaces." @{
                    isEditor = $false
                }
            }
        }

        if ($currentStructIsSignalCandidate -and
            ($code.IndexOf("string", [System.StringComparison]::Ordinal) -ge 0 -or $code.IndexOf("String", [System.StringComparison]::Ordinal) -ge 0) -and
            $code -match "\b(string|System\.String)\s+[A-Za-z_][A-Za-z0-9_]*") {
            $symbol = ""
            $implementsISignal = $false
            if ($null -ne $currentStruct) {
                $symbol = $currentStruct.name
                $implementsISignal = $currentStruct.implementsISignal
            }
            if ($isEditor) {
                Add-Finding "WARN" "EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW" 60 "EDITOR_ONLY_REVIEW" "STRUCT_BODY_FIELD_SCAN" $relativePath $lineNumber $symbol $line "Editor/test signal-like structs can use managed strings, but must not become runtime payload contracts." @{
                    isEditor = $true
                    implementsISignal = $implementsISignal
                }
            } elseif (-not $currentStructIsStrictRuntimeContract) {
                Add-Finding "WARN" "MANAGED_STRING_IN_SIGNAL_LIKE_REVIEW" 72 "STATIC_CONTRACT_REVIEW" "STRUCT_BODY_FIELD_SCAN" $relativePath $lineNumber $symbol $line "This signal-like private/native-adjacent struct carries a managed string. Confirm it never crosses SignalBus<T>, NativeQueue<T>, Burst, or NativeArray boundaries; otherwise replace with FixedString or a stable uint hash." @{
                    isEditor = $false
                    implementsISignal = $implementsISignal
                    strictRuntimeContract = $false
                }
            } else {
                Add-Finding "ERROR" "MANAGED_STRING_IN_SIGNAL_PAYLOAD" 94 "CONFIRMED_OR_PROBABLE_RUNTIME_PAYLOAD" "STRUCT_BODY_FIELD_SCAN" $relativePath $lineNumber $symbol $line "Use FixedString32Bytes/64Bytes or a stable uint hash inside signal payloads." @{
                    isEditor = $false
                    implementsISignal = $implementsISignal
                    strictRuntimeContract = $true
                }
            }
        }

        $telemetryFieldMatch = [System.Text.RegularExpressions.Match]::Empty
        if ($code.IndexOf("NativeArray", [System.StringComparison]::Ordinal) -ge 0 -and
            ($code.IndexOf("Telemetry", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("BlackBox", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Signal", [System.StringComparison]::Ordinal) -ge 0)) {
            $telemetryFieldMatch = [regex]::Match($code, "\bprivate\s+(?:static\s+)?(?:readonly\s+)?NativeArray\s*<[^>]*(Telemetry|BlackBox|Signal)[^>]*>\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:=[^;]*)?;")
        }
        if ($telemetryFieldMatch.Success) {
            $fieldName = $telemetryFieldMatch.Groups[2].Value
            $ownership = Test-FileHasOwnershipPath $rawText $line $fieldName "Array"
            $isTelemetryOrBlackBox = $code.IndexOf("Telemetry", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("BlackBox", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Blackbox", [System.StringComparison]::Ordinal) -ge 0 -or
                $fieldName.IndexOf("Telemetry", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                $fieldName.IndexOf("BlackBox", [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -or
                $fieldName.IndexOf("Blackbox", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
            if (-not $isTelemetryOrBlackBox) {
                Add-Finding "INFO" "LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW" 68 "SIGNAL_SCRATCH_REVIEW" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "This NativeArray stores signal-like scratch data, not a telemetry/blackbox ring. Confirm it is a bounded staging buffer and not a private SignalBus<T> replacement." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    hasHelperDisposePath = $ownership.hasHelperDispose
                    isEditor = $isEditor
                }
                continue
            }

            $localNativeTelemetryCount++
            if ($isEditor) {
                Add-Finding "INFO" "EDITOR_LOCAL_NATIVE_TELEMETRY_REVIEW" 56 "EDITOR_ONLY_REVIEW" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "Editor-only telemetry buffers do not gate player H-Phi, but should still dispose deterministically when the window closes." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    hasHelperDisposePath = $ownership.hasHelperDispose
                    isEditor = $true
                }
            } elseif ($ownership.hasVaultAlias) {
                Add-Finding "INFO" "LOCAL_NATIVE_TELEMETRY_RING_VAULT_ALIAS" 92 "CONFIRMED_VAULT_ALIAS_REVIEW" "FIELD_DECLARATION_PLUS_VAULT_ALIAS" $relativePath $lineNumber $fieldName $line "This field is documented as a GlobalDataVault alias. Verify generation checks and dispose ownership stay in the vault; do not count it as a private owner breach." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    hasHelperDisposePath = $ownership.hasHelperDispose
                    isEditor = $isEditor
                }
            } elseif ($ownership.isOwned) {
                $registeredLocalTelemetryCount++
                Add-Finding "WARN" "LOCAL_NATIVE_TELEMETRY_RING_REGISTERED_NON_VAULT" 88 "CONFIRMED_NON_VAULT_OWNERSHIP_WITH_SENTINEL" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "This private telemetry ring has register/unregister/dispose coverage, but H-Phi still prefers VaultBufferHandle<T> from GlobalDataVault for persistent blackbox state." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    hasHelperDisposePath = $ownership.hasHelperDispose
                    isEditor = $isEditor
                }
            } else {
                Add-Finding "ERROR" "LOCAL_NATIVE_TELEMETRY_RING_UNOWNED" 90 "PROBABLE_NATIVE_OWNERSHIP_BREACH" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "Persistent telemetry/blackbox rings must be DataVault-owned or at least registered, unregistered, and disposed through the native sentinel." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    hasHelperDisposePath = $ownership.hasHelperDispose
                    isEditor = $isEditor
                }
            }
        }

        $queueFieldMatch = [System.Text.RegularExpressions.Match]::Empty
        if ($code.IndexOf("NativeQueue<", [System.StringComparison]::Ordinal) -ge 0 -and
            ($code.IndexOf("Signal", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Command", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Packet", [System.StringComparison]::Ordinal) -ge 0)) {
            $queueFieldMatch = [regex]::Match($code, "\b(?:private|internal|public|protected)\s+(?:static\s+)?(?:readonly\s+)?NativeQueue<[^>]*(Signal|Command|Packet)[^>]*>\s+([A-Za-z_][A-Za-z0-9_]*)\b")
        }
        if ($queueFieldMatch.Success -and $relativePath -notmatch "Core/GlobalSignals\.cs|Core/Signals/SignalWardenRuntime\.cs|Editor/") {
            $fieldName = $queueFieldMatch.Groups[2].Value
            $ownership = Test-FileHasOwnershipPath $rawText $line $fieldName "Queue"
            if ($ownership.isOwned) {
                Add-Finding "INFO" "LOCAL_SIGNAL_QUEUE_REGISTERED_NON_BUS_REVIEW" 70 "REGISTERED_LOCAL_QUEUE_REVIEW" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "This local signal queue has sentinel ownership, but confirm it intentionally bypasses SignalBus<T> and does not fragment the global signal corridor." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    isEditor = $isEditor
                }
            } else {
                Add-Finding "WARN" "POSSIBLE_ORPHANED_SIGNAL_QUEUE" 82 "PROBABLE_SIGNAL_CORRIDOR_BYPASS" "FIELD_DECLARATION_PLUS_SENTINEL_SCAN" $relativePath $lineNumber $fieldName $line "Confirm this queue is registered as a typed lane or migrate producers to SignalBus<T>." @{
                    hasSentinelRegistration = $ownership.hasRegister
                    hasSentinelUnregister = $ownership.hasUnregister
                    hasDisposePath = $ownership.hasDispose
                    hasVaultAlias = $ownership.hasVaultAlias
                    isEditor = $isEditor
                }
            }
        }

        if ($IncludeHotPathHeuristics -and $currentHotMethod.Length -gt 0 -and -not $isEditor) {
            if (($code.IndexOf("foreach", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".Where", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".Select", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".OrderBy", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".ToList", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".ToArray", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf("Enumerable", [System.StringComparison]::Ordinal) -ge 0) -and
                $code -match "\bforeach\s*\(|\.Where\s*\(|\.Select\s*\(|\.OrderBy\s*\(|\.ToList\s*\(|\.ToArray\s*\(|Enumerable\.") {
                $hotPathRiskCount++
                Add-Finding "WARN" "ZERO_GC_HOT_PATH_ENUMERATION_REVIEW" 72 "HOT_PATH_HEURISTIC" "HOT_METHOD_REGEX" $relativePath $lineNumber $currentHotMethod $line "Review this hot-path enumeration/LINQ surface for allocations, boxing, or hidden iterator state." @{
                    hotMethod = $currentHotMethod
                    isEditor = $false
                }
            }

            if ($code.IndexOf("new ", [System.StringComparison]::Ordinal) -ge 0 -and
                $code -match "\bnew\s+(List|Dictionary|HashSet|Queue|Stack|StringBuilder|string)\b|new\s+[A-Za-z_][A-Za-z0-9_]*\s*\(") {
                if ($code -notmatch "new\s+(NativeArray|NativeList|NativeQueue|NativeHashMap|NativeParallel|UnsafeList|UnsafeHashMap)\b") {
                    $hotPathRiskCount++
                    Add-Finding "WARN" "ZERO_GC_HOT_PATH_ALLOCATION_REVIEW" 66 "HOT_PATH_HEURISTIC" "HOT_METHOD_REGEX" $relativePath $lineNumber $currentHotMethod $line "Review this hot-path allocation. If intentional, move it to bootstrap/cold path or document the pooled owner." @{
                        hotMethod = $currentHotMethod
                        isEditor = $false
                    }
                }
            }

            if (($code.IndexOf("GetComponent", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf("FindObject", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf("GameObject.Find", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf("Object.Find", [System.StringComparison]::Ordinal) -ge 0) -and
                $code -match "GetComponent\s*<|FindObjectOfType|FindObjectsOfType|GameObject\.Find|Object\.Find") {
                $hotPathRiskCount++
                Add-Finding "WARN" "HOT_PATH_UNITY_LOOKUP_REVIEW" 82 "HOT_PATH_HEURISTIC" "HOT_METHOD_REGEX" $relativePath $lineNumber $currentHotMethod $line "Cache component/object references outside Tick/Update/Schedule paths. Do not perform Unity hierarchy lookups in hot loops." @{
                    hotMethod = $currentHotMethod
                    isEditor = $false
                }
            }

            if (($code.IndexOf(".material", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf("Material.Set", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".SetFloat", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".SetColor", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".SetVector", [System.StringComparison]::Ordinal) -ge 0 -or
                    $code.IndexOf(".SetTexture", [System.StringComparison]::Ordinal) -ge 0) -and
                $code -match "\.material\b|Material\.Set(Float|Int|Color|Vector|Texture)|\.Set(Float|Int|Color|Vector|Texture)\s*\(") {
                $hotPathRiskCount++
                Add-Finding "WARN" "SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW" 64 "HOT_PATH_HEURISTIC" "HOT_METHOD_REGEX" $relativePath $lineNumber $currentHotMethod $line "Review material mutation in hot path. Prefer MaterialPropertyBlock/GraphicsBuffer/CBUFFER patterns that keep SRP batching intact." @{
                    hotMethod = $currentHotMethod
                    isEditor = $false
                }
            }
        }

        if (($code.IndexOf("File", [System.StringComparison]::Ordinal) -ge 0 -or
                $code.IndexOf("Directory", [System.StringComparison]::Ordinal) -ge 0) -and
            -not $isEditor -and $relativePath -notmatch "Save|Persistence|Crash|Dump|Telemetry|Tools|Editor" -and
            $code -match "\b(File|Directory)\.(Read|Write|Append|Open|Create|Delete)|new\s+FileStream\s*\(") {
            $ioFinding = Resolve-SyncFileIoFinding $relativePath $currentClass $currentMethod ($editorOrDevelopmentGuardDepth -gt 0)
            Add-Finding $ioFinding.severity $ioFinding.rule $ioFinding.confidence $ioFinding.classification "SANITIZED_LINE_REGEX" $relativePath $lineNumber "" $line $ioFinding.requiredAction $ioFinding.tags
        }
    }
}

$computeFiles = Get-ChildItem -LiteralPath $assetsRoot -Filter "*.compute" -Recurse -ErrorAction SilentlyContinue
foreach ($file in $computeFiles) {
    $relativePath = Convert-ToRelativePath $file.FullName
    $rawLines = [System.IO.File]::ReadAllLines($file.FullName)
    $shaderFilesScanned++
    for ($lineIndex = 0; $lineIndex -lt $rawLines.Length; $lineIndex++) {
        $line = $rawLines[$lineIndex]
        if ($line -match "numthreads\s*\(\s*1024\s*,") {
            $computeThreadGroupRiskCount++
            Add-Finding "WARN" "COMPUTE_THREADS_1024_REVIEW" 80 "GPU_PORTABILITY_HEURISTIC" "COMPUTE_SHADER_SCAN" $relativePath ($lineIndex + 1) "" $line "Use tiered thread-group constants; 1024-wide groups are PC-biased and risky on mobile/Metal-class GPUs." @{
                shader = $true
            }
        }
    }
}

$duplicateGroups = $signalDefinitions | Group-Object -Property name | Where-Object { $_.Count -gt 1 }
foreach ($group in $duplicateGroups) {
    $runtimeEntries = @($group.Group | Where-Object { -not $_.isEditor })
    $strictRuntimeEntries = @($group.Group | Where-Object { $_.isStrictRuntimeContract })
    $editorEntries = @($group.Group | Where-Object { $_.isEditor })
    $runtimeCount = $runtimeEntries.Count
    $strictRuntimeCount = $strictRuntimeEntries.Count
    foreach ($entry in $group.Group) {
        if ($strictRuntimeCount -gt 1 -and $entry.isStrictRuntimeContract) {
            Add-Finding "ERROR" "DUPLICATE_RUNTIME_SIGNAL_NAME" 92 "CONFIRMED_RUNTIME_CONTRACT_COLLISION" "ANCHORED_STRUCT_GROUP" $entry.path $entry.line $entry.name "struct $($entry.name)" "Signal names must be globally unique across runtime contracts. Merge duplicate contracts or wrap mock/domain-local payloads behind explicit names." @{
                duplicateCount = $group.Count
                runtimeDuplicateCount = $runtimeCount
                strictRuntimeDuplicateCount = $strictRuntimeCount
                editorDuplicateCount = $editorEntries.Count
            }
        } elseif ($runtimeCount -ge 1 -and $entry.isEditor) {
            Add-Finding "WARN" "EDITOR_SIGNAL_NAME_SHADOWS_RUNTIME" 68 "EDITOR_ONLY_REVIEW" "ANCHORED_STRUCT_GROUP" $entry.path $entry.line $entry.name "struct $($entry.name)" "Editor/test structs should not shadow runtime signal names; rename smoke payloads or fully isolate them." @{
                duplicateCount = $group.Count
                runtimeDuplicateCount = $runtimeCount
                strictRuntimeDuplicateCount = $strictRuntimeCount
                editorDuplicateCount = $editorEntries.Count
            }
        } else {
            Add-Finding "WARN" "DUPLICATE_SIGNAL_LIKE_NAME_REVIEW" 74 "STATIC_CONTRACT_REVIEW" "ANCHORED_STRUCT_GROUP" $entry.path $entry.line $entry.name "struct $($entry.name)" "Review duplicate signal-like names. They may be namespace-safe C#, but telemetry/AOT/operator tooling treats names as global identifiers." @{
                duplicateCount = $group.Count
                runtimeDuplicateCount = $runtimeCount
                strictRuntimeDuplicateCount = $strictRuntimeCount
                editorDuplicateCount = $editorEntries.Count
            }
        }
    }
}

$coreGlobalSignalDefinitions = @($signalDefinitions | Where-Object { $_.inCoreGlobalSignals }).Count
$signalsWithoutLayout = @($signalDefinitions | Where-Object { -not $_.hasStructLayout }).Count
$errorCount = @($findings | Where-Object { $_.severity -eq "ERROR" }).Count
$warnCount = @($findings | Where-Object { $_.severity -eq "WARN" }).Count
$infoCount = @($findings | Where-Object { $_.severity -eq "INFO" }).Count
$confirmedErrorCount = @($findings | Where-Object { $_.severity -eq "ERROR" -and $_.confidence -ge 90 }).Count
$reviewOnlyCount = @($findings | Where-Object { $_.confidence -lt 75 }).Count

$ruleStats = @(
    $findings |
        Group-Object -Property rule |
        Sort-Object -Property Count -Descending |
        ForEach-Object {
            $groupFindings = @($_.Group)
            [pscustomobject]@{
                rule = $_.Name
                count = $_.Count
                errors = @($groupFindings | Where-Object { $_.severity -eq "ERROR" }).Count
                warnings = @($groupFindings | Where-Object { $_.severity -eq "WARN" }).Count
                infos = @($groupFindings | Where-Object { $_.severity -eq "INFO" }).Count
                averageConfidence = [Math]::Round((($groupFindings | Measure-Object -Property confidence -Average).Average), 1)
            }
        }
)

$classificationStats = @(
    $findings |
        Group-Object -Property classification |
        Sort-Object -Property Count -Descending |
        ForEach-Object {
            [pscustomobject]@{
                classification = $_.Name
                count = $_.Count
            }
        }
)

$result = [pscustomobject]@{
    agent = "SHINOBU_02"
    evidenceClass = "STATIC_SOURCE_CLASSIFIED"
    scope = $Scope
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    projectRoot = $ProjectRoot
    scannedFiles = $scannedFiles
    shaderFilesScanned = $shaderFilesScanned
    pack1Layouts = $pack1Count
    runtimeSignalPack1Layouts = $runtimeSignalPack1Count
    managedEventSurfaceHits = $managedEventCount
    localNativeTelemetryRings = $localNativeTelemetryCount
    registeredLocalTelemetryRings = $registeredLocalTelemetryCount
    hotPathRiskHits = $hotPathRiskCount
    computeThreadGroupRiskHits = $computeThreadGroupRiskCount
    signalDefinitions = $signalDefinitions.Count
    coreGlobalSignalDefinitions = $coreGlobalSignalDefinitions
    signalsWithoutLayout = $signalsWithoutLayout
    errors = $errorCount
    warnings = $warnCount
    infos = $infoCount
    confirmedErrors = $confirmedErrorCount
    reviewOnlyFindings = $reviewOnlyCount
    ruleStats = $ruleStats
    classificationStats = $classificationStats
    findings = $findings.ToArray()
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputJson -Encoding UTF8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# SHINOBU_02 Signal Bus Contract Audit")
[void]$md.AppendLine()
[void]$md.AppendLine("Evidence Class: STATIC_SOURCE_CLASSIFIED")
[void]$md.AppendLine("Scope: $Scope")
[void]$md.AppendLine("Generated UTC: $($result.generatedUtc)")
[void]$md.AppendLine()
[void]$md.AppendLine("## Summary")
[void]$md.AppendLine()
[void]$md.AppendLine("- Files scanned: $scannedFiles C# / $shaderFilesScanned compute")
[void]$md.AppendLine("- Signal-like definitions found: $($signalDefinitions.Count)")
[void]$md.AppendLine("- Signal definitions still in Core/GlobalSignals.cs: $coreGlobalSignalDefinitions")
[void]$md.AppendLine("- Pack=1 layouts: $pack1Count")
[void]$md.AppendLine("- Runtime signal Pack=1 layouts: $runtimeSignalPack1Count")
[void]$md.AppendLine("- Signal-like definitions without nearby StructLayout: $signalsWithoutLayout")
[void]$md.AppendLine("- Managed event surface hits: $managedEventCount")
[void]$md.AppendLine("- Local native telemetry ring hits: $localNativeTelemetryCount")
[void]$md.AppendLine("- Registered local telemetry rings: $registeredLocalTelemetryCount")
[void]$md.AppendLine("- Hot-path heuristic hits: $hotPathRiskCount")
[void]$md.AppendLine("- Compute 1024-thread-group hits: $computeThreadGroupRiskCount")
[void]$md.AppendLine("- Errors: $errorCount")
[void]$md.AppendLine("- Warnings: $warnCount")
[void]$md.AppendLine("- Infos: $infoCount")
[void]$md.AppendLine("- Confirmed/probable errors at confidence >= 90: $confirmedErrorCount")
[void]$md.AppendLine("- Review-only findings below confidence 75: $reviewOnlyCount")
[void]$md.AppendLine()
[void]$md.AppendLine("## Rule Breakdown")
[void]$md.AppendLine()
foreach ($stat in $ruleStats) {
    [void]$md.AppendLine("- $($stat.rule): total $($stat.count), errors $($stat.errors), warnings $($stat.warnings), infos $($stat.infos), avg confidence $($stat.averageConfidence)")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Classification Breakdown")
[void]$md.AppendLine()
foreach ($stat in $classificationStats) {
    [void]$md.AppendLine("- $($stat.classification): $($stat.count)")
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Findings")
[void]$md.AppendLine()
if ($findings.Count -eq 0) {
    [void]$md.AppendLine("No findings. This is static-source only, not runtime proof.")
} else {
    foreach ($finding in $findings) {
        $findingHeader = "- [$($finding.severity)][$($finding.confidence)%][$($finding.classification)] $($finding.rule) | $($finding.path):$($finding.line)"
        if (-not [string]::IsNullOrWhiteSpace($finding.symbol)) {
            $findingHeader += " | $($finding.symbol)"
        }
        [void]$md.AppendLine($findingHeader)
        [void]$md.AppendLine("  Evidence kind: $($finding.evidenceKind)")
        [void]$md.AppendLine("  Evidence: ``$($finding.evidence)``")
        [void]$md.AppendLine("  Required action: $($finding.requiredAction)")
    }
}
[void]$md.AppendLine()
[void]$md.AppendLine("## Non-Claims")
[void]$md.AppendLine()
[void]$md.AppendLine("- This audit does not prove Unity import, player build, IL2CPP, runtime GC, profiler, scene wiring, or actual struct sizeof(T).")
[void]$md.AppendLine("- Static confidence is not semantic proof. The next precision step is an out-of-band Roslyn runner using Assets/Plugins/Roslyn without wiring analyzers into Unity projects.")
[void]$md.AppendLine("- This audit intentionally reports legacy/shared ownership debt instead of silently modifying cross-domain contracts.")

$md.ToString() | Set-Content -LiteralPath $OutputMarkdown -Encoding UTF8

Write-Output ("SignalBusContractAudit: files={0} shaders={1} errors={2} warnings={3} infos={4} confirmedErrors={5} output={6}" -f $scannedFiles, $shaderFilesScanned, $errorCount, $warnCount, $infoCount, $confirmedErrorCount, $OutputMarkdown)

if ($FailOnError -and $errorCount -gt 0) {
    exit 2
}
