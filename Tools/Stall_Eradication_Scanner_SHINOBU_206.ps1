param(
    [string]$ScriptsRoot = "Assets/_Project/Scripts",
    [string]$ReportPath = "Docs/Reports/DISPATCHER_OPTIMIZATION_REPORT.json",
    [string]$ReadAccessorScope = "SHINOBU_206_EXPANDED_TOUCHED_RUNTIME_FILES",
    [string[]]$ReadAccessorAuditFiles = @(
        "Assets/_Project/Scripts/Core/SystemDispatcher.cs",
        "Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs",
        "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs",
        "Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs",
        "Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs",
        "Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs",
        "Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs",
        "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs",
        "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs"
    ),
    [string]$AsmdefCompileWallAuditScope = "SHINOBU_206_CORE_DISPATCHER_ASMDEFS",
    [string[]]$AsmdefCompileWallAuditFiles = @(
        "Assets/_Project/Scripts/Core/Scheduling/Hecton8.Core.Scheduling.asmdef",
        "Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef",
        "Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef",
        "Assets/_Project/Scripts/Hecton8.Core.asmdef"
    ),
    [switch]$FailOnFatalNativeSafetyCandidates,
    [int]$FatalNativeSafetyExitCode = 206
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ScriptsRoot -PathType Container)) {
    throw "ScriptsRoot must be an existing directory. Received: $ScriptsRoot"
}

function Test-CommentLine {
    param([string]$Line)
    $trimmed = $Line.TrimStart()
    return $trimmed.StartsWith("//") -or $trimmed.StartsWith("/*") -or $trimmed.StartsWith("*")
}

function Test-HotSignature {
    param([string]$Line)
    return $Line.Contains(" Tick(") -or
        $Line.Contains("Tick(float") -or
        $Line.Contains("FixedTick(") -or
        $Line.Contains("FastTick(") -or
        $Line.Contains("LateFrameTick(") -or
        $Line.Contains("ToolTick(") -or
        $Line.Contains("PostFixedTick(") -or
        $Line.Contains("Update(") -or
        $Line.Contains("FixedUpdate(") -or
        $Line.Contains("LateUpdate(") -or
        $Line.Contains("ScheduleSimulation(") -or
        $Line.Contains("DispatchFixedStep(") -or
        $Line.Contains("RunMasterPostSimulationPhase(") -or
        $Line.Contains("CompleteMasterFixedSimulationBridge(") -or
        $Line.Contains("PostSimulationTick(") -or
        $Line.Contains("VisualSyncTick(")
}

function Test-QaFuzzerFile {
    param([string]$Path, [string]$LeafName, [string[]]$Lines)
    if (-not $LeafName.Contains("Fuzzer")) {
        return $false
    }

    if ($Path.Contains("/Editor/") -or $Path.Contains("/Tests/") -or $Path.Contains("/QA/")) {
        return $true
    }

    $text = $Lines -join "`n"
    return $text.Contains("QA_OPTIMIZATION_REPORT") -or
        $text.Contains("HEADLESS_") -or
        $text.Contains("Headless") -or
        $text.Contains("NUnit.Framework") -or
        $text.Contains("EditorWindow")
}

function Test-EditorFile {
    param([string]$Path, [string[]]$Lines)
    $leafName = [System.IO.Path]::GetFileName($Path)
    if ($Path.Contains("/Editor/") -or
        $Path.Contains("/Tests/") -or
        $Path.Contains("/Dev/") -or
        $Path.Contains("/QA/") -or
        (Test-QaFuzzerFile $Path $leafName $Lines) -or
        $leafName.Contains("SmokeTester") -or
        ($leafName.Contains("MapMagic") -and $leafName.EndsWith("Node.cs"))) {
        return $true
    }

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i].Trim()
        if ($line.Length -eq 0) {
            continue
        }

        if ($line.StartsWith("//") -or $line.StartsWith("/*") -or $line.StartsWith("*")) {
            continue
        }

        return $line.StartsWith("#if UNITY_EDITOR") -or $line.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD")
    }

    return $false
}

function Test-ColdAnnotated {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 8)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i].ToUpperInvariant()
        if ($line.Contains("COLD") -or
            $line.Contains("COLD/EDITOR") -or
            $line.Contains("COLD PROFILE PATH") -or
            $line.Contains("COLD BOOT") -or
            $line.Contains("COLD BLOCKING") -or
            $line.Contains("COLD SYNC") -or
            $line.Contains("SYNC JOB") -or
            $line.Contains("BLOCKING_SYNC_POINT") -or
            $line.Contains("OUTSIDE TICK") -or
            $line.Contains("BOOTSTRAP") -or
            $line.Contains("WARMUP") -or
            $line.Contains("EDITOR PREVIEW") -or
            $line.Contains("SMOKE VALIDATION")) {
            return $true
        }
    }

    return $false
}

function Test-EditorBlockContext {
    param([string[]]$Lines, [int]$LineIndex)
    $depth = 0
    for ($i = $LineIndex; $i -ge 0; $i--) {
        $trimmed = $Lines[$i].Trim()
        if ($trimmed.StartsWith("#endif")) {
            $depth++
            continue
        }

        if ($trimmed.StartsWith("#if") -or $trimmed.StartsWith("#elif")) {
            if ($depth -gt 0) {
                $depth--
                continue
            }

            return $trimmed.Contains("UNITY_EDITOR") -or $trimmed.Contains("DEVELOPMENT_BUILD")
        }
    }

    return $false
}

function Get-ContainingMethodName {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 420)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(") -and
            ($trimmed -match '\b(?<Name>[A-Za-z_][A-Za-z0-9_]*)\s*\(')) {
            return $Matches["Name"]
        }
    }

    return "UNKNOWN"
}

function Get-RunReceiverName {
    param([string]$Line)
    if ($Line -match '(?<Receiver>\b[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*Run\s*\(') {
        return $Matches["Receiver"]
    }

    return ""
}

function Get-RunJobType {
    param([string[]]$Lines, [int]$LineIndex)

    $receiver = Get-RunReceiverName $Lines[$LineIndex]
    if ([string]::IsNullOrEmpty($receiver)) {
        return "UNKNOWN"
    }

    $escapedReceiver = [regex]::Escape($receiver)
    $start = [Math]::Max(0, $LineIndex - 80)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        if ($line -match "\b(?<Type>[A-Za-z_][A-Za-z0-9_\.]*)\s+$escapedReceiver\s*=\s*new\s+(?<Ctor>[A-Za-z_][A-Za-z0-9_\.]*)") {
            $typeName = $Matches["Type"]
            if ($typeName -eq "var") {
                return $Matches["Ctor"]
            }

            return $typeName
        }

        if ($line -match "\b(?<Type>[A-Za-z_][A-Za-z0-9_\.]*)\s+$escapedReceiver\s*=") {
            return $Matches["Type"]
        }
    }

    return "UNKNOWN"
}

function Get-ColdOrEditorRunDetail {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex, [bool]$EditorFile)

    $editorBlock = Test-EditorBlockContext $Lines $LineIndex
    $coldAnnotated = Test-ColdAnnotated $Lines $LineIndex
    $disposition = "COLD_OR_EDITOR_REVIEW"
    $reason = "Synchronous run is quarantined outside the gameplay-loop debt bucket and requires cold/editor proof."

    if ($EditorFile) {
        $disposition = "EDITOR_FILE"
        $reason = "File path/name or compile surface identifies this as editor, QA, test, smoke, or development-only code."
    } elseif ($editorBlock) {
        $disposition = "EDITOR_OR_DEVELOPMENT_BLOCK"
        $reason = "Nearest active preprocessor block is UNITY_EDITOR or DEVELOPMENT_BUILD."
    } elseif ($coldAnnotated) {
        $disposition = "COLD_ANNOTATED"
        $reason = "Nearby source comment marks the call as cold/bootstrap/warmup/smoke validation; scanner keeps it out of runtime gameplay-loop debt."
    }

    $evidenceLine = ""
    $evidenceLineNumber = 0
    $start = [Math]::Max(0, $LineIndex - 8)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $upper = $Lines[$i].ToUpperInvariant()
        if ($upper.Contains("COLD") -or
            $upper.Contains("BOOTSTRAP") -or
            $upper.Contains("WARMUP") -or
            $upper.Contains("EDITOR") -or
            $upper.Contains("SMOKE VALIDATION")) {
            $evidenceLine = $Lines[$i].Trim()
            $evidenceLineNumber = $i + 1
            break
        }
    }

    return [ordered]@{
        path = $Path
        line = $LineIndex + 1
        token = "IJob.Run"
        method = Get-ContainingMethodName $Lines $LineIndex
        disposition = $disposition
        reason = $reason
        editorFile = $EditorFile
        editorBlock = $editorBlock
        coldAnnotated = $coldAnnotated
        hotMethod = Test-LineInHotMethod $Lines $LineIndex
        evidenceLine = $evidenceLine
        evidenceLineNumber = $evidenceLineNumber
        source = $Lines[$LineIndex].Trim()
    }
}

function Test-TeardownOrBarrierContext {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 320)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(")) {
            $upper = $trimmed.ToUpperInvariant()
            return $upper.Contains("DISPOSE") -or
                $upper.Contains("ONDISABLE") -or
                $upper.Contains("ONDESTROY") -or
                $upper.Contains("LIFECYCLE") -or
                $upper.Contains("TEARDOWN") -or
                $upper.Contains("FORTEARDOWN") -or
                $upper.Contains("RELEASE") -or
                $upper.Contains("RESET") -or
                $upper.Contains("CLEAR") -or
                $upper.Contains("CANCEL") -or
                $upper.Contains("GLOBALREGISTRYSERVICEREPLACED") -or
                $upper.Contains("SERVICEREPLACED") -or
                $upper.Contains("SHUTDOWN") -or
                $upper.Contains("RELOAD") -or
                $upper.Contains("BARRIER") -or
                $upper.Contains("FORBARRIER") -or
                $upper.Contains("SWAPWINDOW") -or
                $upper.Contains("ORIGINSHIFT") -or
                $upper.Contains("TELEPORT") -or
                $upper.Contains("AUP") -or
                $upper.Contains("AUTHORITATIVEWRITE") -or
                $upper.Contains("READBACK") -or
                $upper.Contains("GPUUPLOAD") -or
                $upper.Contains("UPLOAD") -or
                $upper.Contains("SAVE") -or
                $upper.Contains("WRITE") -or
                $upper.Contains("FLUSH") -or
                $upper.Contains("PREVIEW") -or
                $upper.Contains("VALIDATION") -or
                $upper.Contains("VAULT") -or
                $upper.Contains("BOOT") -or
                $upper.Contains("INIT") -or
                $upper.Contains("BOOTSTRAP") -or
                $upper.Contains("PREWARM") -or
                $upper.Contains("WARMUP")
        }
    }

    return $false
}

function Test-FailureRollbackFenceContext {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 24)
    $end = [Math]::Min($Lines.Length - 1, $LineIndex + 16)
    if ($Lines.Length -eq 0) {
        return $false
    }

    $window = $Lines[$start..$end] -join "`n"
    return $window.Contains("finally") -and
        $window.Contains("scheduleCommitted") -and
        $window.Contains("lastScheduledHandle") -and
        $window.Contains("ReleasePendingVaultJobLocks")
}

function Test-LikelyHotContext {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 16)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        if (Test-CommentLine $Lines[$i]) {
            continue
        }

        if (Test-HotSignature $Lines[$i]) {
            return $true
        }
    }

    return $false
}

function Test-LineInHotMethod {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 320)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        if (Test-HotSignature $line) {
            return $true
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(") -and
            -not (Test-HotSignature $line)) {
            return $false
        }
    }

    return $false
}

function Test-CentralDispatcherHardFence {
    param([string]$Path)
    $leafName = [System.IO.Path]::GetFileName($Path)
    return $leafName -eq "DispatcherJobFence.cs" -or $leafName -eq "DispatcherJobSwap.cs"
}

function Get-CentralDispatcherHardFenceDetail {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex)

    $leafName = [System.IO.Path]::GetFileName($Path)
    $method = "UNKNOWN"
    $reason = "Central dispatcher helper owns the raw JobHandle.Complete call; leaf domains must route through this helper instead of calling Complete directly."
    $disposition = "CENTRAL_HELPER_RAW_COMPLETE"

    $start = [Math]::Max(0, $LineIndex - 96)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(")) {
            if ($trimmed.Contains("TryFinalizeCompleted(")) {
                $method = "TryFinalizeCompleted"
                $reason = "Central non-blocking finalizer: it checks IsCompleted before the raw Complete call and clears the caller-owned handle only after completion."
                $disposition = "CENTRAL_HELPER_NONBLOCKING_FINALIZER"
            } elseif ($trimmed.Contains("TryComplete(")) {
                $method = "TryComplete"
                $reason = "Central guarded completion helper: non-forced calls fail closed while incomplete; forced calls are reserved for dispatcher swap windows, teardown, AUP, and deterministic barriers."
                $disposition = "CENTRAL_HELPER_GUARDED_COMPLETE"
            }

            break
        }
    }

    return [ordered]@{
        path = $Path
        line = $LineIndex + 1
        file = $leafName
        method = $method
        token = "JobHandle.Complete"
        disposition = $disposition
        reason = $reason
    }
}

function Test-CentralDispatcherRuntimeFenceContext {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex)
    $leafName = [System.IO.Path]::GetFileName($Path)
    if ($leafName -ne "SystemDispatcher.cs") {
        return $false
    }

    $start = [Math]::Max(0, $LineIndex - 320)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(")) {
            return $trimmed.Contains("RunMasterPostSimulationPhase(") -or
                $trimmed.Contains("CompleteMasterFixedSimulationBridge(") -or
                $trimmed.Contains("DispatchFixedStep(")
        }
    }

    return $false
}

function Get-AsmdefReferenceDisposition {
    param([string]$AssemblyName, [string]$Reference, [string]$Path)

    $disposition = "ALLOWED_REFERENCE"
    $reason = "Reference is outside HECTON-8 sibling runtime coupling scope."

    if ([string]::IsNullOrWhiteSpace($Reference)) {
        $disposition = "EMPTY_REFERENCE"
        $reason = "Empty asmdef reference ignored."
    } elseif ($Reference.StartsWith("Unity.") -or $Reference -eq "UnityEngine.UI") {
        $disposition = "UNITY_PACKAGE_REFERENCE"
        $reason = "Unity package references do not create HECTON-8 sibling runtime compile-wall coupling."
    } elseif ($Reference -eq "GPUInstancer") {
        $disposition = "THIRD_PARTY_REFERENCE"
        $reason = "Third-party reference is reported separately from HECTON-8 sibling runtime coupling."
    } elseif ($Reference.StartsWith("Hecton8.Core.")) {
        $disposition = "CORE_INTERNAL_REFERENCE"
        $reason = "Core sub-assembly reference stays inside the Echelon 1 Core boundary."
    } elseif ($Reference.StartsWith("Hecton8.") -and $Reference.EndsWith(".Contracts")) {
        $disposition = "CONTRACT_REFERENCE"
        $reason = "Contract assembly reference is the approved cross-domain compile-wall route."
    } elseif ($Reference.StartsWith("Hecton8.")) {
        if ($AssemblyName -eq "Hecton8.Core.Scheduling") {
            $disposition = "FORBIDDEN_SCHEDULING_SIBLING_RUNTIME_REFERENCE"
            $reason = "Core Scheduling must route sibling domains through contracts, SignalBus payloads, Vault handles, or cached cold interfaces; direct sibling runtime reference is forbidden."
        } elseif ($AssemblyName -eq "Hecton8.Core") {
            $disposition = "ROOT_CORE_DIRECT_SIBLING_RUNTIME_REFERENCE_REQUIRES_ROUTE_CARD"
            $reason = "Root Core carries a direct sibling runtime reference; SHINOBU_206 records this as compile-wall debt and does not delete it without an owner contract migration."
        } else {
            $disposition = "FORBIDDEN_CORE_ASSEMBLY_SIBLING_RUNTIME_REFERENCE"
            $reason = "Core assembly references a sibling runtime assembly instead of a contract route."
        }
    }

    return [ordered]@{
        path = $Path
        assemblyName = $AssemblyName
        reference = $Reference
        disposition = $disposition
        reason = $reason
    }
}

function Get-CentralDispatcherRuntimeFenceDetail {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex)

    $phase = "UNKNOWN"
    $reason = "Central dispatcher completion fence requires manual review."
    $start = [Math]::Max(0, $LineIndex - 320)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(")) {
            if ($trimmed.Contains("RunMasterPostSimulationPhase(")) {
                $phase = "POST_SIMULATION"
                $reason = "Required phase barrier: master simulation jobs write gameplay truth and PostSimulationTick consumes stable buffers/signals immediately after the fence."
            } elseif ($trimmed.Contains("CompleteMasterFixedSimulationBridge(") -or
                      $trimmed.Contains("DispatchFixedStep(")) {
                $phase = "POST_FIXED_SIMULATION"
                $reason = "Required fixed-step barrier: fixed simulation jobs complete before PostFixedSimulation and post-fixed tick consumers read the deterministic fixed snapshot."
            }

            break
        }
    }

    return [ordered]@{
        path = $Path
        line = $LineIndex + 1
        token = "TryComplete(forceComplete:true)"
        phase = $phase
        disposition = "CENTRAL_PHASE_BARRIER"
        reason = $reason
    }
}

function Test-OwnerDisputedRuntimeRun {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex)
    $leafName = [System.IO.Path]::GetFileName($Path)
    $methodName = Get-ContainingMethodName $Lines $LineIndex
    $jobType = Get-RunJobType $Lines $LineIndex

    if ($leafName -eq "AbyssalDeferredCausticsRuntime.cs") {
        return ($methodName -eq "RunPendingCausticsKernel") -and
               ($jobType -eq "GenerateMockCausticLightingJob" -or $jobType -eq "CalculateCausticParametersJob")
    }

    if ($leafName -eq "HectonBilateralDrsUpscalerRuntime.cs") {
        return ($methodName -eq "ScheduleOwnerSimulation") -and
               ($jobType -eq "GenerateMockDrsStateJob" -or $jobType -eq "CalculateUpscalerParamsJob")
    }

    if ($leafName -eq "HectonVisorUberPostFeature.Noir.cs") {
        return ($methodName -eq "TryUpdateNoirConstants") -and
               ($jobType -eq "GenerateMockPsychologicalStressJob" -or $jobType -eq "CalculateNoirParametersJob")
    }

    if ($leafName -eq "HectonMarineSnowRenderer.cs") {
        if ($methodName -eq "PublishVehicleWakeImpulse" -and $jobType -eq "BuildVehicleWakeSignalJob") {
            return $true
        }

        if ($methodName -eq "CommitVehicleWakePropwashEvent" -and $jobType -eq "CommitVehicleWakePropwashEventJob") {
            return $true
        }

        if ($methodName -eq "RefreshMockWakeSignals") {
            return $jobType -eq "BuildMockFlowFieldJob" -or
                   $jobType -eq "GenerateMockPropwashEventsJob" -or
                   $jobType -eq "BuildMockWakeSignalJob"
        }

        if ($methodName -eq "HarvestProceduralWakeSourcesIntoPropwash" -and $jobType -eq "HarvestWakeSourcePropwashJob") {
            return $true
        }
    }

    return $false
}

function Get-OwnerDisputedRuntimeRunDetail {
    param([string]$Path, [string[]]$Lines, [int]$LineIndex)
    $leafName = [System.IO.Path]::GetFileName($Path)
    $owner = "UNKNOWN"
    $reason = "Runtime IJob.Run() is in an owner-disputed file and requires integrator arbitration."
    $lineNumber = $LineIndex + 1
    $methodName = Get-ContainingMethodName $Lines $LineIndex
    $jobType = Get-RunJobType $Lines $LineIndex

    if ($leafName -eq "AbyssalDeferredCausticsRuntime.cs") {
        $owner = "SHINOBU_232"
        $reason = "Caustics owner logs require job.Run(); SHINOBU_206 stopped direct rewrites after three overwrites."
    } elseif ($leafName -eq "HectonBilateralDrsUpscalerRuntime.cs") {
        $owner = "SHINOBU_236"
        $reason = "Bilateral DRS runners were reintroduced after three SHINOBU_206 clamps; route is owner-disputed."
    } elseif ($leafName -eq "HectonVisorUberPostFeature.Noir.cs") {
        $owner = "SHINOBU_235"
        $reason = "Noir owner logs require IJob.Run for the immediate Burst proof route."
    } elseif ($leafName -eq "HectonMarineSnowRenderer.cs") {
        $owner = "SHINOBU_237"
        $reason = "Propwash owner status/rationale require IJob.Run and explicitly gate against local Execute() callsites."
    }

    return [ordered]@{
        path = $Path
        line = $lineNumber
        owner = $owner
        token = "IJob.Run"
        method = $methodName
        jobType = $jobType
        disposition = "OWNER_DISPUTED"
        reason = $reason
    }
}

function Get-ReadAccessorContext {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 420)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (($trimmed.StartsWith("public ") -or
             $trimmed.StartsWith("private ") -or
             $trimmed.StartsWith("internal ") -or
             $trimmed.StartsWith("protected ")) -and
            $trimmed.Contains("(")) {
            if ($trimmed -match '\b(?<Name>(TryGet|Get|TryResolve|Resolve|TryRead|Read)[A-Za-z0-9_]*)\s*\(') {
                return [pscustomobject]@{
                    Name = $Matches["Name"]
                    Signature = $trimmed
                    Line = $i + 1
                }
            }

            return $null
        }
    }

    return $null
}

function Get-ReadAccessorForbiddenKind {
    param([string]$Line)
    if ($Line -match 'GlobalDataVault\s*\.\s*TryGetLatestCreated') { return "GlobalDataVaultLatestCreated" }
    if ($Line -match 'GlobalRegistry\s*\.') { return "GlobalRegistryPoll" }
    if ($Line -match '\bTryEnsure') { return "TryEnsure" }
    if ($Line -match '\bEnsure[A-Za-z0-9_]*\s*\(') { return "Ensure" }
    if ($Line -match '\bAcquire[A-Za-z0-9_]*\s*\(') { return "Acquire" }
    if ($Line -match '\bGetGenerationHandle\s*\(') { return "VaultHandleAcquire" }
    if ($Line -match '\bRefresh[A-Za-z0-9_]*\s*\(') { return "Refresh" }
    if ($Line -match '\.\s*Complete\s*\(' -or $Line -match '\bCompleteAll\s*\(' -or $Line -match '\bTryComplete\s*\(') { return "JobCompletion" }
    if ($Line -match '\.\s*Run\s*\(' -and -not ($Line -match '\bTask\s*\.\s*Run\s*\(')) { return "SynchronousJobRun" }
    if ($Line -match '\.\s*Push\s*\(' -or $Line -match '\.\s*TryPush\s*\(' -or $Line -match '\bPublish\s*\(') { return "PublishOrSignal" }
    if ($Line -match '\bRegister[A-Za-z0-9_]*\s*\(' -or $Line -match '\bUnregister[A-Za-z0-9_]*\s*\(') { return "RegisterOrUnregister" }
    if ($Line -match 'FindObject' -or $Line -match 'GameObject\s*\.\s*Find') { return "SceneSearch" }
    if ($Line -match '\bTryLockBuffer\s*\(' -or $Line -match '\bTryUnlockBuffer\s*\(') { return "VaultLockMutation" }
    return $null
}

function Test-ThreeParagraphSafetyJustification {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 32)
    $context = ""
    if ($LineIndex -gt 0) {
        $context = $Lines[$start..($LineIndex - 1)] -join "`n"
    }

    return $context.Contains("SAFETY_JUSTIFICATION_PARAGRAPH_1") -and
        $context.Contains("SAFETY_JUSTIFICATION_PARAGRAPH_2") -and
        $context.Contains("SAFETY_JUSTIFICATION_PARAGRAPH_3")
}

function Get-ContainingTypeName {
    param([string[]]$Lines, [int]$LineIndex)
    $start = [Math]::Max(0, $LineIndex - 420)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if (Test-CommentLine $line) {
            continue
        }

        if ($line -match '\b(class|struct)\s+(?<Name>[A-Za-z0-9_]+)') {
            return $Matches["Name"]
        }
    }

    return "UNKNOWN"
}

function Get-NativeSafetyFieldName {
    param([string[]]$Lines, [int]$LineIndex)

    $end = [Math]::Min($Lines.Length - 1, $LineIndex + 8)
    $window = ""
    if ($Lines.Length -gt 0 -and $LineIndex -ge 0 -and $LineIndex -le $end) {
        $parts = New-Object System.Collections.Generic.List[string]
        for ($i = $LineIndex; $i -le $end; $i++) {
            $segment = $Lines[$i].Trim()
            if ($segment.Contains(";")) {
                $segment = $segment.Substring(0, $segment.IndexOf(";") + 1)
                $parts.Add($segment) | Out-Null
                break
            }

            $parts.Add($segment) | Out-Null
        }

        $window = $parts -join " "
    }

    if ($window -match '\bNativeQueue<[^>]+>\.ParallelWriter\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)') {
        return $Matches["Name"]
    }

    if ($window -match '\bNative(?:Array|List|HashMap|Reference|Slice)<[^>]+>\s+(?<Name>[A-Za-z_][A-Za-z0-9_]*)') {
        return $Matches["Name"]
    }

    $declaration = [regex]::Replace($window, '\[[^\]]+\]', ' ')
    $declaration = [regex]::Replace($declaration, '\s+', ' ').Trim()
    if ($declaration.Contains("=")) {
        $declaration = $declaration.Substring(0, $declaration.IndexOf("=")).Trim()
    }

    $fieldMatches = [regex]::Matches($declaration, '\b(?<Name>[A-Za-z_][A-Za-z0-9_]*)\s*(?=;)')
    if ($fieldMatches.Count -gt 0) {
        return $fieldMatches[$fieldMatches.Count - 1].Groups["Name"].Value
    }

    $pointerMatches = [regex]::Matches($declaration, '\*\s*(?<Name>[A-Za-z_][A-Za-z0-9_]*)\b')
    if ($pointerMatches.Count -gt 0) {
        return $pointerMatches[$pointerMatches.Count - 1].Groups["Name"].Value
    }

    return "UNKNOWN"
}

function Get-NativeSafetyOwnerBoundaryDetail {
    param(
        [string]$Path,
        [string]$ContainingType,
        [object]$JobTypeScheduleEvidence
    )

    $normalizedPath = $Path.Replace("\", "/")
    $owner = "UNKNOWN"
    $domain = "UNKNOWN"
    $ownerDisputed = $false
    $reason = "No owner boundary card matched; integrator must route this native safety bypass to the source owner."

    if ($normalizedPath.Contains("/Rendering/BilateralDrs/")) {
        $owner = "SHINOBU_236"
        $domain = "BILATERAL_DRS_UPSCALER"
        $reason = "Bilateral DRS native container bypass is in SHINOBU_236 rendering owner scope."
        if ($JobTypeScheduleEvidence -ne $null -and $JobTypeScheduleEvidence.hasRunEvidence -and -not $JobTypeScheduleEvidence.hasScheduleEvidence) {
            $ownerDisputed = $true
            $reason = "Bilateral DRS job type has only IJob.Run() evidence; SHINOBU_236 rationale/status explicitly reintroduced Run() after SHINOBU_206 clamps, so this remains owner-disputed run-only debt."
        }
    } elseif ($normalizedPath.Contains("/Physics/")) {
        $owner = "PHYSICS_OWNER"
        $domain = "PHYSICS"
        $reason = "Physics native container bypass remains owner review debt unless its job type lacks dispatcher registered schedule/fence proof."
    } elseif ($normalizedPath.Contains("/World/")) {
        $owner = "WORLD_OWNER"
        $domain = "WORLD"
        $reason = "World native container bypass remains owner review debt unless its job type lacks dispatcher registered schedule/fence proof."
    } elseif ($normalizedPath.Contains("/Atmosphere/")) {
        $owner = "ATMOSPHERE_OWNER"
        $domain = "ATMOSPHERE"
        $reason = "Atmosphere native container bypass remains owner review debt unless its gas-step job type lacks dispatcher schedule/fence proof."
    } elseif ($normalizedPath.Contains("/Rendering/")) {
        $owner = "RENDERING_OWNER"
        $domain = "RENDERING"
        $reason = "Rendering native container bypass remains owner review debt unless its job type lacks dispatcher registered schedule/fence proof."
    }

    return [ordered]@{
        mode = "PATH_AND_JOBTYPE_STATIC_OWNER_BOUNDARY"
        owner = $owner
        domain = $domain
        containingType = $ContainingType
        ownerDisputed = $ownerDisputed
        reason = $reason
    }
}

function Get-NativeSafetyDispatcherEvidence {
    param([string[]]$Lines, [int]$LineIndex)

    $start = [Math]::Max(0, $LineIndex - 640)
    $end = [Math]::Min($Lines.Length - 1, $LineIndex + 640)
    $window = ""
    if ($Lines.Length -gt 0) {
        $window = $Lines[$start..$end] -join "`n"
    }

    $fileText = $Lines -join "`n"
    $hasRegisterActiveJob = $fileText.Contains("H8Memory.RegisterActiveJob") -or $fileText.Contains("RegisterActiveJob(")
    $hasDispatcherFence = $fileText.Contains("DispatcherJobFence.TryFinalizeCompleted") -or $fileText.Contains("DispatcherJobFence.TryComplete")
    $hasJobHandleStorage = $fileText -match '\bJobHandle\s+_[A-Za-z0-9_]*Handle\b'
    $hasSystemId = $fileText.Contains("SystemID.")
    $hasScheduleNearAttribute = $window.Contains(".Schedule(")
    $hasFenceNearAttribute = $window.Contains("DispatcherJobFence.") -or $window.Contains("RegisterActiveJob(")
    $hasAnyEvidence = $hasRegisterActiveJob -or $hasDispatcherFence -or ($hasJobHandleStorage -and $hasSystemId -and $hasScheduleNearAttribute)

    return [ordered]@{
        mode = "SAME_FILE_STATIC_HEURISTIC"
        hasAnyEvidence = $hasAnyEvidence
        hasRegisterActiveJob = $hasRegisterActiveJob
        hasDispatcherFence = $hasDispatcherFence
        hasJobHandleStorage = $hasJobHandleStorage
        hasSystemId = $hasSystemId
        hasScheduleNearAttribute = $hasScheduleNearAttribute
        hasFenceNearAttribute = $hasFenceNearAttribute
    }
}

$script:NativeSafetyJobTypeEvidenceCache = @{}

function Get-NativeSafetyJobTypeScheduleEvidence {
    param([string]$TypeName, [string]$ScriptsRoot)

    if ([string]::IsNullOrWhiteSpace($TypeName) -or $TypeName -eq "UNKNOWN") {
        return [ordered]@{
            mode = "CROSS_FILE_TYPE_STATIC_HEURISTIC"
            typeName = $TypeName
            referenceCount = 0
            hasScheduleEvidence = $false
            hasRunEvidence = $false
            hasRegisterActiveJobEvidence = $false
            hasDispatcherFenceEvidence = $false
            hasRegisteredScheduleEvidence = $false
            samples = @()
        }
    }

    if ($script:NativeSafetyJobTypeEvidenceCache.ContainsKey($TypeName)) {
        return $script:NativeSafetyJobTypeEvidenceCache[$TypeName]
    }

    $escapedType = [Regex]::Escape($TypeName)
    $typeHits = & rg -n "\b$escapedType\b" $ScriptsRoot -g "*.cs"
    if ($LASTEXITCODE -gt 1) {
        throw "rg native safety job-type scan failed for $TypeName with exit code $LASTEXITCODE"
    }

    $referenceCount = 0
    $hasScheduleEvidence = $false
    $hasRunEvidence = $false
    $hasRegisterActiveJobEvidence = $false
    $hasDispatcherFenceEvidence = $false
    $samples = New-Object System.Collections.Generic.List[object]

    foreach ($hit in $typeHits) {
        if ($hit -notmatch "^(.*?):(\d+):(.*)$") {
            continue
        }

        $path = $Matches[1].Replace("\", "/")
        $lineNumber = [int]$Matches[2]
        $source = $Matches[3]
        $referenceCount++

        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $lines = @(Get-Content -LiteralPath $path)
        $lineIndex = $lineNumber - 1
        if ($lineIndex -lt 0 -or $lineIndex -ge $lines.Length) {
            continue
        }

        $start = [Math]::Max(0, $lineIndex - 64)
        $end = [Math]::Min($lines.Length - 1, $lineIndex + 192)
        $window = $lines[$start..$end] -join "`n"
        $fileText = $lines -join "`n"

        $windowHasSchedule = $window.Contains(".Schedule(")
        $windowHasRun = $window.Contains(".Run(")
        $windowHasRegisterActiveJob = $window.Contains("H8Memory.RegisterActiveJob") -or $window.Contains("RegisterActiveJob(")
        $fileHasRegisterActiveJob = $fileText.Contains("H8Memory.RegisterActiveJob") -or $fileText.Contains("RegisterActiveJob(")
        $windowHasDispatcherFence = $window.Contains("DispatcherJobFence.")
        $fileHasDispatcherFence = $fileText.Contains("DispatcherJobFence.")

        $hasScheduleEvidence = $hasScheduleEvidence -or $windowHasSchedule
        $hasRunEvidence = $hasRunEvidence -or $windowHasRun
        $hasRegisterActiveJobEvidence = $hasRegisterActiveJobEvidence -or $windowHasRegisterActiveJob -or ($windowHasSchedule -and $fileHasRegisterActiveJob)
        $hasDispatcherFenceEvidence = $hasDispatcherFenceEvidence -or $windowHasDispatcherFence -or ($windowHasSchedule -and $fileHasDispatcherFence)

        if (($windowHasSchedule -or $windowHasRun) -and $samples.Count -lt 8) {
            $samples.Add([ordered]@{
                path = $path
                line = $lineNumber
                hasScheduleNearType = $windowHasSchedule
                hasRunNearType = $windowHasRun
                hasRegisterActiveJobNearType = $windowHasRegisterActiveJob
                hasRegisterActiveJobInFile = $fileHasRegisterActiveJob
                hasDispatcherFenceNearType = $windowHasDispatcherFence
                hasDispatcherFenceInFile = $fileHasDispatcherFence
                source = $source.Trim()
            }) | Out-Null
        }
    }

    $result = [ordered]@{
        mode = "CROSS_FILE_TYPE_STATIC_HEURISTIC"
        typeName = $TypeName
        referenceCount = $referenceCount
        hasScheduleEvidence = $hasScheduleEvidence
        hasRunEvidence = $hasRunEvidence
        hasRegisterActiveJobEvidence = $hasRegisterActiveJobEvidence
        hasDispatcherFenceEvidence = $hasDispatcherFenceEvidence
        hasRegisteredScheduleEvidence = $hasScheduleEvidence -and ($hasRegisterActiveJobEvidence -or $hasDispatcherFenceEvidence)
        samples = $samples
    }
    $script:NativeSafetyJobTypeEvidenceCache[$TypeName] = $result
    return $result
}

function Get-NativeSafetyDisposition {
    param([string]$Attribute, [bool]$EditorFile, [bool]$HasJustification, [string]$Source)
    if ($Attribute -eq "NativeDisableContainerSafetyRestriction") {
        if (-not $HasJustification -and -not $EditorFile) {
            return "FATAL_ARCHITECTURE_CANDIDATE"
        }

        if (-not $HasJustification -and $EditorFile) {
            return "EDITOR_REVIEW_REQUIRED"
        }

        if ($Source.Contains("ParallelWriter")) {
            return "DOCUMENTED_PARALLEL_WRITER_ROUTE"
        }

        return "DOCUMENTED_CONTAINER_BYPASS"
    }

    if ($Attribute -eq "NativeDisableParallelForRestriction") {
        if ($EditorFile) {
            return "EDITOR_PARALLEL_FOR_RESTRICTION_BYPASS"
        }

        return "RUNTIME_PARALLEL_FOR_RESTRICTION_REQUIRES_OWNER_FENCE_PROOF"
    }

    if ($Attribute -eq "NativeDisableUnsafePtrRestriction") {
        if ($EditorFile) {
            return "EDITOR_UNSAFE_PTR_ROUTE"
        }

        return "RUNTIME_UNSAFE_PTR_ROUTE_REQUIRES_OWNER_LIFETIME_PROOF"
    }

    return "UNKNOWN"
}

$tokenLines = & rg -n "\.Complete\(|CompleteAll\(|\.Run\(|TryComplete\(" $ScriptsRoot -g "*.cs"
if ($LASTEXITCODE -gt 1) {
    throw "rg failed with exit code $LASTEXITCODE"
}

$nativeSafetyLines = & rg -n "NativeDisableContainerSafetyRestriction|NativeDisableParallelForRestriction|NativeDisableUnsafePtrRestriction" $ScriptsRoot -g "*.cs"
if ($LASTEXITCODE -gt 1) {
    throw "rg native safety scan failed with exit code $LASTEXITCODE"
}

$readAccessorScriptFiles = New-Object System.Collections.Generic.List[string]
foreach ($auditFile in $ReadAccessorAuditFiles) {
    if ([string]::IsNullOrWhiteSpace($auditFile)) {
        continue
    }

    if (Test-Path -LiteralPath $auditFile) {
        $readAccessorScriptFiles.Add($auditFile.Replace("\", "/")) | Out-Null
    }
}
if ($readAccessorScriptFiles.Count -eq 0) {
    throw "No read-accessor audit files found."
}

$hitsByFile = @{}
foreach ($hit in $tokenLines) {
    if ($hit -notmatch "^(.*?):(\d+):(.*)$") {
        continue
    }

    $path = $Matches[1].Replace("\", "/")
    $lineNumber = [int]$Matches[2]
    $source = $Matches[3]
    if (-not $hitsByFile.ContainsKey($path)) {
        $hitsByFile[$path] = New-Object System.Collections.Generic.List[object]
    }

    $hitsByFile[$path].Add([pscustomobject]@{ LineNumber = $lineNumber; Source = $source }) | Out-Null
}

$result = [ordered]@{
    agent = "SHINOBU_206"
    status = "PENDING_VERIFICATION"
    scope = $ScriptsRoot
    reportModel = "FAST_TOKEN_CONTEXT_SCAN_WITH_LEGACY_HELPER_GATE_STRUCTURED_CENTRAL_FENCE_BUCKETS_NATIVE_SAFETY_AUDIT_COLD_RUN_EVIDENCE_AND_OWNER_BOUNDARIES"
    scannedTokenFiles = $hitsByFile.Count
    readAccessorScope = $ReadAccessorScope
    readAccessorAuditFiles = New-Object System.Collections.Generic.List[string]
    skippedReadAccessorAuditFiles = New-Object System.Collections.Generic.List[string]
    scannedReadAccessorFiles = 0
    asmdefCompileWallAuditScope = $AsmdefCompileWallAuditScope
    asmdefCompileWallAuditMode = "STATIC_ASMDEF_REFERENCE_GATE"
    asmdefCompileWallAuditFiles = New-Object System.Collections.Generic.List[string]
    asmdefCompileWallMissingFiles = New-Object System.Collections.Generic.List[string]
    asmdefCompileWallScannedFiles = 0
    asmdefCompileWallReferenceTokens = 0
    asmdefCompileWallDirectSiblingRuntimeReferenceTokens = 0
    asmdefCompileWallSchedulingSiblingRuntimeReferenceTokens = 0
    asmdefCompileWallCoreAssemblySiblingRuntimeReferenceTokens = 0
    asmdefCompileWallRootCoreDirectSiblingRuntimeReferenceTokens = 0
    asmdefCompileWallThirdPartyReferenceTokens = 0
    asmdefCompileWallDetails = New-Object System.Collections.Generic.List[object]
    asmdefCompileWallViolationDetails = New-Object System.Collections.Generic.List[object]
    nativeSafetyAuditScope = $ScriptsRoot
    nativeSafetyAuditMode = "STATIC_TOKEN_GATE"
    nativeSafetyFatalEnforcement = if ($FailOnFatalNativeSafetyCandidates) { "FAIL_FAST_AFTER_REPORT_WRITE" } else { "REPORT_ONLY" }
    nativeSafetyFatalExceptionType = "Hecton8.Core.FatalArchitectureException"
    nativeSafetyFatalExitCode = $FatalNativeSafetyExitCode
    nativeSafetyFatalGateReason = "Runtime NativeDisableContainerSafetyRestriction without registered schedule/fence proof, or routed only through IJob.Run(), must be treated as FatalArchitectureException-equivalent architecture debt by CI/integrator gates."
    nativeSafetyDispatcherEvidenceMode = "SAME_FILE_STATIC_HEURISTIC"
    nativeSafetyOwnerBoundaryMode = "PATH_AND_JOBTYPE_STATIC_OWNER_BOUNDARY"
    nativeDisableContainerSafetyRestrictionTokens = 0
    nativeDisableContainerSafetyRestrictionRuntimeTokens = 0
    nativeDisableContainerSafetyRestrictionEditorTokens = 0
    nativeDisableContainerSafetyRestrictionMissingJustificationTokens = 0
    nativeDisableContainerSafetyRestrictionRuntimeMissingJustificationTokens = 0
    nativeContainerSafetyFatalCandidateTokens = 0
    nativeContainerSafetyRegisteredScheduleReviewTokens = 0
    nativeContainerSafetyFatalUnregisteredTokens = 0
    nativeContainerSafetyFatalRunOnlyTokens = 0
    nativeContainerSafetyOwnerDisputedRunOnlyTokens = 0
    nativeContainerSafetyFatalMissingScheduleTokens = 0
    nativeContainerSafetyFatalCandidateSameFileDispatcherEvidenceTokens = 0
    nativeContainerSafetyFatalCandidateMissingDispatcherEvidenceTokens = 0
    nativeContainerSafetyFatalCandidateCrossFileScheduleEvidenceTokens = 0
    nativeContainerSafetyFatalCandidateCrossFileRegisteredScheduleEvidenceTokens = 0
    nativeContainerSafetyFatalCandidateCrossFileRunOnlyEvidenceTokens = 0
    nativeContainerSafetyFatalCandidateMissingCrossFileScheduleEvidenceTokens = 0
    nativeDisableParallelForRestrictionTokens = 0
    nativeDisableParallelForRestrictionRuntimeTokens = 0
    nativeDisableParallelForRestrictionEditorTokens = 0
    nativeDisableUnsafePtrRestrictionTokens = 0
    nativeDisableUnsafePtrRestrictionRuntimeTokens = 0
    nativeDisableUnsafePtrRestrictionEditorTokens = 0
    totalSyncTokens = 0
    coldOrEditorTokens = 0
    coldAnnotatedRunTokens = 0
    editorFileRunTokens = 0
    editorBlockRunTokens = 0
    directCompleteTokens = 0
    directCompleteHotPathTokens = 0
    runtimeRunLexicalTokens = 0
    runtimeRunTokens = 0
    ownerDisputedRuntimeRunTokens = 0
    forcedFenceTokens = 0
    forcedHotPathTokens = 0
    centralDispatcherHardFenceTokens = 0
    centralDispatcherRuntimeFenceTokens = 0
    teardownOrBarrierTokens = 0
    hotPathTokens = 0
    methodScopedHotPathTokens = 0
    unclassifiedRuntimeTokens = 0
    readAccessorForbiddenTokens = 0
    hotPathOffenders = New-Object System.Collections.Generic.List[object]
    forcedHotPathSamples = New-Object System.Collections.Generic.List[string]
    runtimeRunTokenSamples = New-Object System.Collections.Generic.List[string]
    ownerDisputedRuntimeRunSamples = New-Object System.Collections.Generic.List[string]
    ownerDisputedRuntimeRunDetails = New-Object System.Collections.Generic.List[object]
    editorOrToolRunResidue = New-Object System.Collections.Generic.List[string]
    coldOrEditorRunDetails = New-Object System.Collections.Generic.List[object]
    coldScheduleCompleteResidue = New-Object System.Collections.Generic.List[string]
    centralDispatcherHardFenceSamples = New-Object System.Collections.Generic.List[string]
    centralDispatcherHardFenceDetails = New-Object System.Collections.Generic.List[object]
    centralDispatcherRuntimeFenceSamples = New-Object System.Collections.Generic.List[string]
    centralDispatcherRuntimeFenceDetails = New-Object System.Collections.Generic.List[object]
    teardownOrBarrierSamples = New-Object System.Collections.Generic.List[string]
    unclassifiedRuntimeSamples = New-Object System.Collections.Generic.List[string]
    readAccessorForbiddenSamples = New-Object System.Collections.Generic.List[object]
    nativeDisableContainerSafetyRestrictionDetails = New-Object System.Collections.Generic.List[object]
    nativeSafetyRestrictionFatalCandidateDetails = New-Object System.Collections.Generic.List[object]
    nativeSafetyRestrictionRegisteredScheduleReviewDetails = New-Object System.Collections.Generic.List[object]
    nativeSafetyRestrictionFatalUnregisteredDetails = New-Object System.Collections.Generic.List[object]
    nativeSafetyRestrictionSummarySamples = New-Object System.Collections.Generic.List[object]
}

foreach ($rawAsmdefPath in $AsmdefCompileWallAuditFiles) {
    if ([string]::IsNullOrWhiteSpace($rawAsmdefPath)) {
        continue
    }

    $asmdefPath = $rawAsmdefPath.Replace("\", "/")
    if (-not (Test-Path -LiteralPath $asmdefPath -PathType Leaf)) {
        $result.asmdefCompileWallMissingFiles.Add($asmdefPath) | Out-Null
        continue
    }

    $result.asmdefCompileWallAuditFiles.Add($asmdefPath) | Out-Null
    $result.asmdefCompileWallScannedFiles++

    $asmdef = Get-Content -Raw -LiteralPath $asmdefPath | ConvertFrom-Json
    $assemblyName = [string]$asmdef.name
    foreach ($reference in @($asmdef.references)) {
        $referenceName = [string]$reference
        $detail = Get-AsmdefReferenceDisposition $assemblyName $referenceName $asmdefPath
        $result.asmdefCompileWallReferenceTokens++
        if ($result.asmdefCompileWallDetails.Count -lt 96) {
            $result.asmdefCompileWallDetails.Add($detail) | Out-Null
        }

        if ($detail.disposition -eq "THIRD_PARTY_REFERENCE") {
            $result.asmdefCompileWallThirdPartyReferenceTokens++
            continue
        }

        if ($detail.disposition -eq "FORBIDDEN_SCHEDULING_SIBLING_RUNTIME_REFERENCE") {
            $result.asmdefCompileWallDirectSiblingRuntimeReferenceTokens++
            $result.asmdefCompileWallSchedulingSiblingRuntimeReferenceTokens++
            if ($result.asmdefCompileWallViolationDetails.Count -lt 32) {
                $result.asmdefCompileWallViolationDetails.Add($detail) | Out-Null
            }
            continue
        }

        if ($detail.disposition -eq "FORBIDDEN_CORE_ASSEMBLY_SIBLING_RUNTIME_REFERENCE") {
            $result.asmdefCompileWallDirectSiblingRuntimeReferenceTokens++
            $result.asmdefCompileWallCoreAssemblySiblingRuntimeReferenceTokens++
            if ($result.asmdefCompileWallViolationDetails.Count -lt 32) {
                $result.asmdefCompileWallViolationDetails.Add($detail) | Out-Null
            }
            continue
        }

        if ($detail.disposition -eq "ROOT_CORE_DIRECT_SIBLING_RUNTIME_REFERENCE_REQUIRES_ROUTE_CARD") {
            $result.asmdefCompileWallDirectSiblingRuntimeReferenceTokens++
            $result.asmdefCompileWallRootCoreDirectSiblingRuntimeReferenceTokens++
            if ($result.asmdefCompileWallViolationDetails.Count -lt 32) {
                $result.asmdefCompileWallViolationDetails.Add($detail) | Out-Null
            }
        }
    }
}

foreach ($path in $hitsByFile.Keys) {
    $lines = @(Get-Content -LiteralPath $path)
    $editorFile = Test-EditorFile $path $lines
    foreach ($hit in $hitsByFile[$path]) {
        $lineIndex = $hit.LineNumber - 1
        if ($lineIndex -lt 0 -or $lineIndex -ge $lines.Length) {
            continue
        }

        $line = $lines[$lineIndex]
        if (Test-CommentLine $line) {
            continue
        }

        $hasComplete = ($line -match '\.\s*Complete\s*\(') -or ($line -match '\bCompleteAll\s*\(')
        $hasRun = ($line -match '\.\s*Run\s*\(') -and -not ($line -match '\bTask\s*\.\s*Run\s*\(')
        $hasForcedFence = ($line -match '\bTryComplete\s*\(') -and (($line -match 'forceComplete\s*:\s*true') -or ($line -match ',\s*true\s*\)'))
        if (-not ($hasComplete -or $hasRun -or $hasForcedFence)) {
            continue
        }

        $result.totalSyncTokens++
        if ($hasComplete) {
            $result.directCompleteTokens++
        }

        if ($hasRun) {
            $result.runtimeRunLexicalTokens++
        }

        if ($hasRun -and (Test-OwnerDisputedRuntimeRun $path $lines $lineIndex)) {
            $result.ownerDisputedRuntimeRunTokens++
            if ($result.ownerDisputedRuntimeRunSamples.Count -lt 16) {
                $result.ownerDisputedRuntimeRunSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            if ($result.ownerDisputedRuntimeRunDetails.Count -lt 32) {
                $detail = Get-OwnerDisputedRuntimeRunDetail $path $lines $lineIndex
                $result.ownerDisputedRuntimeRunDetails.Add($detail) | Out-Null
            }

            continue
        }

        $editorBlockContext = Test-EditorBlockContext $lines $lineIndex
        $coldAnnotatedContext = Test-ColdAnnotated $lines $lineIndex
        $methodHotContext = -not $editorFile -and (Test-LineInHotMethod $lines $lineIndex)
        if ($editorFile -or $editorBlockContext -or ($coldAnnotatedContext -and -not $methodHotContext)) {
            $result.coldOrEditorTokens++
            if ($hasRun) {
                $result.editorOrToolRunResidue.Add("${path}:$($lineIndex + 1)") | Out-Null
                if ($editorFile) {
                    $result.editorFileRunTokens++
                } elseif ($editorBlockContext) {
                    $result.editorBlockRunTokens++
                } elseif ($coldAnnotatedContext) {
                    $result.coldAnnotatedRunTokens++
                }

                if ($result.coldOrEditorRunDetails.Count -lt 64) {
                    $detail = Get-ColdOrEditorRunDetail $path $lines $lineIndex $editorFile
                    $result.coldOrEditorRunDetails.Add($detail) | Out-Null
                }
            }

            if ($hasComplete -and $line.Contains("Schedule(")) {
                $result.coldScheduleCompleteResidue.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            continue
        }

        if ($hasComplete -and (Test-CentralDispatcherHardFence $path)) {
            $result.centralDispatcherHardFenceTokens++
            if ($result.centralDispatcherHardFenceSamples.Count -lt 16) {
                $result.centralDispatcherHardFenceSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            if ($result.centralDispatcherHardFenceDetails.Count -lt 16) {
                $detail = Get-CentralDispatcherHardFenceDetail $path $lines $lineIndex
                $result.centralDispatcherHardFenceDetails.Add($detail) | Out-Null
            }

            continue
        }

        if (($hasComplete -or $hasForcedFence) -and (Test-CentralDispatcherRuntimeFenceContext $path $lines $lineIndex)) {
            $result.centralDispatcherRuntimeFenceTokens++
            if ($result.centralDispatcherRuntimeFenceSamples.Count -lt 16) {
                $result.centralDispatcherRuntimeFenceSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            if ($result.centralDispatcherRuntimeFenceDetails.Count -lt 16) {
                $detail = Get-CentralDispatcherRuntimeFenceDetail $path $lines $lineIndex
                $result.centralDispatcherRuntimeFenceDetails.Add($detail) | Out-Null
            }

            continue
        }

        if ((Test-TeardownOrBarrierContext $lines $lineIndex) -or (Test-FailureRollbackFenceContext $lines $lineIndex)) {
            $result.teardownOrBarrierTokens++
            if ($result.teardownOrBarrierSamples.Count -lt 80) {
                $result.teardownOrBarrierSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            continue
        }

        if ($hasRun) {
            $result.runtimeRunTokens++
            $result.runtimeRunTokenSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
        }

        if ($hasForcedFence) {
            $result.forcedFenceTokens++
        }

        $isMethodHot = $methodHotContext
        $isHot = $isMethodHot -or (Test-LikelyHotContext $lines $lineIndex)
        if ($isHot) {
            $result.hotPathTokens++
            if ($isMethodHot) {
                $result.methodScopedHotPathTokens++
            }

            if ($hasComplete) {
                $result.directCompleteHotPathTokens++
            }

            if ($hasForcedFence) {
                $result.forcedHotPathTokens++
                $result.forcedHotPathSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            $token = "Run"
            if ($hasForcedFence) {
                $token = "ForcedFence"
            } elseif ($hasComplete) {
                $token = "Complete"
            }

            $result.hotPathOffenders.Add([ordered]@{
                path = $path
                line = $lineIndex + 1
                token = $token
                source = $line.Trim()
            }) | Out-Null
        } else {
            $result.unclassifiedRuntimeTokens++
            if ($result.unclassifiedRuntimeSamples.Count -lt 120) {
                $result.unclassifiedRuntimeSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }
        }
    }
}

$nativeSafetyHitsByFile = @{}
foreach ($hit in $nativeSafetyLines) {
    if ($hit -notmatch "^(.*?):(\d+):(.*)$") {
        continue
    }

    $path = $Matches[1].Replace("\", "/")
    $lineNumber = [int]$Matches[2]
    $source = $Matches[3]
    if (-not $nativeSafetyHitsByFile.ContainsKey($path)) {
        $nativeSafetyHitsByFile[$path] = New-Object System.Collections.Generic.List[object]
    }

    $nativeSafetyHitsByFile[$path].Add([pscustomobject]@{ LineNumber = $lineNumber; Source = $source }) | Out-Null
}

foreach ($path in $nativeSafetyHitsByFile.Keys) {
    $lines = @(Get-Content -LiteralPath $path)
    $editorFile = Test-EditorFile $path $lines
    foreach ($hit in $nativeSafetyHitsByFile[$path]) {
        $lineIndex = $hit.LineNumber - 1
        if ($lineIndex -lt 0 -or $lineIndex -ge $lines.Length) {
            continue
        }

        $line = $lines[$lineIndex]
        if (Test-CommentLine $line) {
            continue
        }

        $attribute = ""
        if ($line.Contains("NativeDisableContainerSafetyRestriction")) {
            $attribute = "NativeDisableContainerSafetyRestriction"
            $result.nativeDisableContainerSafetyRestrictionTokens++
            if ($editorFile) {
                $result.nativeDisableContainerSafetyRestrictionEditorTokens++
            } else {
                $result.nativeDisableContainerSafetyRestrictionRuntimeTokens++
            }
        } elseif ($line.Contains("NativeDisableParallelForRestriction")) {
            $attribute = "NativeDisableParallelForRestriction"
            $result.nativeDisableParallelForRestrictionTokens++
            if ($editorFile) {
                $result.nativeDisableParallelForRestrictionEditorTokens++
            } else {
                $result.nativeDisableParallelForRestrictionRuntimeTokens++
            }
        } elseif ($line.Contains("NativeDisableUnsafePtrRestriction")) {
            $attribute = "NativeDisableUnsafePtrRestriction"
            $result.nativeDisableUnsafePtrRestrictionTokens++
            if ($editorFile) {
                $result.nativeDisableUnsafePtrRestrictionEditorTokens++
            } else {
                $result.nativeDisableUnsafePtrRestrictionRuntimeTokens++
            }
        } else {
            continue
        }

        $hasJustification = Test-ThreeParagraphSafetyJustification $lines $lineIndex
        $disposition = Get-NativeSafetyDisposition $attribute $editorFile $hasJustification $line
        $containingType = Get-ContainingTypeName $lines $lineIndex
        $dispatcherEvidence = Get-NativeSafetyDispatcherEvidence $lines $lineIndex
        $jobTypeScheduleEvidence = $null
        if ($attribute -eq "NativeDisableContainerSafetyRestriction" -and $disposition -eq "FATAL_ARCHITECTURE_CANDIDATE") {
            $jobTypeScheduleEvidence = Get-NativeSafetyJobTypeScheduleEvidence $containingType $ScriptsRoot
        }
        $ownerBoundary = Get-NativeSafetyOwnerBoundaryDetail $path $containingType $jobTypeScheduleEvidence

        $detail = [ordered]@{
            path = $path
            line = $lineIndex + 1
            attribute = $attribute
            fieldName = Get-NativeSafetyFieldName $lines $lineIndex
            containingType = $containingType
            editorOnly = $editorFile
            hasThreeParagraphJustification = $hasJustification
            parallelWriter = $line.Contains("ParallelWriter")
            readOnlyAnnotated = $line.Contains("ReadOnly") -or (($lineIndex + 1 -lt $lines.Length) -and $lines[$lineIndex + 1].Contains("ReadOnly")) -or (($lineIndex + 2 -lt $lines.Length) -and $lines[$lineIndex + 2].Contains("ReadOnly"))
            disposition = $disposition
            ownerBoundary = $ownerBoundary
            sameFileDispatcherEvidence = $dispatcherEvidence
            crossFileJobTypeScheduleEvidence = $jobTypeScheduleEvidence
            source = $line.Trim()
        }

        if ($attribute -eq "NativeDisableContainerSafetyRestriction") {
            if (-not $hasJustification) {
                $result.nativeDisableContainerSafetyRestrictionMissingJustificationTokens++
                if (-not $editorFile) {
                    $result.nativeDisableContainerSafetyRestrictionRuntimeMissingJustificationTokens++
                }
            }

            if ($disposition -eq "FATAL_ARCHITECTURE_CANDIDATE") {
                $result.nativeContainerSafetyFatalCandidateTokens++
                if ($dispatcherEvidence.hasAnyEvidence) {
                    $result.nativeContainerSafetyFatalCandidateSameFileDispatcherEvidenceTokens++
                } else {
                    $result.nativeContainerSafetyFatalCandidateMissingDispatcherEvidenceTokens++
                }

                if ($jobTypeScheduleEvidence -ne $null -and $jobTypeScheduleEvidence.hasScheduleEvidence) {
                    $result.nativeContainerSafetyFatalCandidateCrossFileScheduleEvidenceTokens++
                    if ($jobTypeScheduleEvidence.hasRegisteredScheduleEvidence) {
                        $result.nativeContainerSafetyFatalCandidateCrossFileRegisteredScheduleEvidenceTokens++
                    }
                } elseif ($jobTypeScheduleEvidence -ne $null -and $jobTypeScheduleEvidence.hasRunEvidence) {
                    $result.nativeContainerSafetyFatalCandidateCrossFileRunOnlyEvidenceTokens++
                } else {
                    $result.nativeContainerSafetyFatalCandidateMissingCrossFileScheduleEvidenceTokens++
                }

                if ($result.nativeSafetyRestrictionFatalCandidateDetails.Count -lt 80) {
                    $result.nativeSafetyRestrictionFatalCandidateDetails.Add($detail) | Out-Null
                }

                $hasRegisteredScheduleProof = $jobTypeScheduleEvidence -ne $null -and $jobTypeScheduleEvidence.hasRegisteredScheduleEvidence
                $hasRunOnlyProof = $jobTypeScheduleEvidence -ne $null -and $jobTypeScheduleEvidence.hasRunEvidence -and -not $jobTypeScheduleEvidence.hasScheduleEvidence
                if ($hasRegisteredScheduleProof) {
                    $result.nativeContainerSafetyRegisteredScheduleReviewTokens++
                    $detail.disposition = "REGISTERED_SCHEDULE_REVIEW"
                    if ($result.nativeSafetyRestrictionRegisteredScheduleReviewDetails.Count -lt 80) {
                        $result.nativeSafetyRestrictionRegisteredScheduleReviewDetails.Add($detail) | Out-Null
                    }
                } else {
                    $result.nativeContainerSafetyFatalUnregisteredTokens++
                    if ($hasRunOnlyProof) {
                        $result.nativeContainerSafetyFatalRunOnlyTokens++
                        $detail.disposition = "FATAL_RUN_ONLY_NO_DISPATCHER_REGISTRATION"
                        if ($ownerBoundary.ownerDisputed) {
                            $result.nativeContainerSafetyOwnerDisputedRunOnlyTokens++
                        }
                    } else {
                        $result.nativeContainerSafetyFatalMissingScheduleTokens++
                        $detail.disposition = "FATAL_MISSING_SCHEDULE_OR_REGISTRATION_PROOF"
                    }

                    if ($result.nativeSafetyRestrictionFatalUnregisteredDetails.Count -lt 80) {
                        $result.nativeSafetyRestrictionFatalUnregisteredDetails.Add($detail) | Out-Null
                    }
                }
            }

            if ($result.nativeDisableContainerSafetyRestrictionDetails.Count -lt 96) {
                $result.nativeDisableContainerSafetyRestrictionDetails.Add($detail) | Out-Null
            }
        } elseif ($result.nativeSafetyRestrictionSummarySamples.Count -lt 96) {
            $result.nativeSafetyRestrictionSummarySamples.Add($detail) | Out-Null
        }
    }
}

foreach ($rawPath in $readAccessorScriptFiles) {
    $path = $rawPath.Replace("\", "/")
    $lines = @(Get-Content -LiteralPath $path)
    $editorFile = Test-EditorFile $path $lines
    if ($editorFile) {
        $result.skippedReadAccessorAuditFiles.Add($path) | Out-Null
        continue
    }

    $result.readAccessorAuditFiles.Add($path) | Out-Null
    $result.scannedReadAccessorFiles++
    $inReadAccessor = $false
    $readAccessorName = ""
    $readAccessorLine = 0
    $braceDepth = 0
    $seenOpenBrace = $false
    for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
        $line = $lines[$lineIndex]
        if (Test-CommentLine $line) {
            continue
        }

        $trimmed = $line.TrimStart()
        if (-not $inReadAccessor) {
            if (($trimmed.StartsWith("public ") -or
                 $trimmed.StartsWith("private ") -or
                 $trimmed.StartsWith("internal ") -or
                 $trimmed.StartsWith("protected ")) -and
                $trimmed.Contains("(") -and
                ($trimmed -match '\b(?<Name>(TryGet|Get|TryResolve|Resolve|TryRead|Read)[A-Za-z0-9_]*)\s*\(')) {
                $inReadAccessor = $true
                $readAccessorName = $Matches["Name"]
                $readAccessorLine = $lineIndex + 1
                $braceDepth = 0
                $seenOpenBrace = $false
            } else {
                continue
            }
        }

        if ($lineIndex -ne ($readAccessorLine - 1)) {
            $kind = Get-ReadAccessorForbiddenKind $line
            if (-not [string]::IsNullOrEmpty($kind)) {
                $result.readAccessorForbiddenTokens++
                if ($result.readAccessorForbiddenSamples.Count -lt 160) {
                    $result.readAccessorForbiddenSamples.Add([ordered]@{
                        path = $path
                        line = $lineIndex + 1
                        method = $readAccessorName
                        methodLine = $readAccessorLine
                        kind = $kind
                        source = $line.Trim()
                    }) | Out-Null
                }
            }
        }

        $openCount = ([regex]::Matches($line, '\{')).Count
        $closeCount = ([regex]::Matches($line, '\}')).Count
        if ($openCount -gt 0) {
            $seenOpenBrace = $true
        }
        $braceDepth += $openCount - $closeCount
        if ($seenOpenBrace -and $braceDepth -le 0) {
            $inReadAccessor = $false
            $readAccessorName = ""
            $readAccessorLine = 0
            $braceDepth = 0
            $seenOpenBrace = $false
        }
    }
}

$knownResidue = @(
    "DispatcherJobFence and DispatcherJobSwap remain the central raw hard-fence surfaces for teardown, AUP, memory release, and deterministic barriers.",
    "SystemDispatcher master simulation and fixed-step fences are central phase barriers, not leaf-domain completion debt; they are legal only in POST_SIMULATION/POST_FIXED windows and must be watched by the 300-frame SHINOBU_206 fence telemetry dump.",
    "PersistentWorldRegistry tombstone sweep requires owner-level snapshot/deferred mutation design before live delta mutation can avoid all hard fences.",
    "GlobalPhysicsStateManager tracked body mutation requires owner-level pending body-delta buffer.",
    "LockstepStateValidator POST_SIM hash validation is an explicit deterministic blocking proof point unless netcode owner accepts delayed validation.",
    "HectonFloatingOrigin transform shift is an AUP hard barrier."
)
if ($result.ownerDisputedRuntimeRunTokens -gt 0) {
    $knownResidue += "AbyssalDeferredCausticsRuntime job.Run() classifier remains encoded for recurrence after the SHINOBU_232 owner dispute; current reports count it only if source reintroduces the token."
    $knownResidue += "HectonBilateralDrsUpscalerRuntime job.Run() is owner-disputed by SHINOBU_236 after three SHINOBU_206 clamps were reintroduced; reported separately for integrator arbitration."
    $knownResidue += "HectonVisorUberPostFeature.Noir job.Run() is owner-disputed by SHINOBU_235; SHINOBU_235 logs require IJob.Run for its immediate Burst proof route."
    $knownResidue += "HectonMarineSnowRenderer job.Run() is owner-disputed by SHINOBU_237; SHINOBU_237 status/rationale require IJob.Run and gate against local Execute() callsites."
}
if ($result.nativeContainerSafetyFatalCandidateTokens -gt 0) {
    $knownResidue += "NativeDisableContainerSafetyRestriction fatal candidates require owner proof or CI invocation of -FailOnFatalNativeSafetyCandidates; current scanner writes JSON first, then exits with the configured FatalArchitectureException-equivalent code."
}
if ($result.nativeContainerSafetyOwnerDisputedRunOnlyTokens -gt 0) {
    $knownResidue += "Bilateral DRS native container safety bypasses are owner-disputed SHINOBU_236 run-only debt when their job types show IJob.Run() evidence without registered schedule/fence proof."
}
if ($result.asmdefCompileWallSchedulingSiblingRuntimeReferenceTokens -gt 0) {
    $knownResidue += "Core Scheduling asmdef has direct sibling runtime coupling; this is a compile-wall violation until routed through Core contracts or an approved route card."
}
if ($result.asmdefCompileWallRootCoreDirectSiblingRuntimeReferenceTokens -gt 0) {
    $knownResidue += "Root Hecton8.Core asmdef has direct sibling runtime coupling; SHINOBU_206 records it as route-card debt and does not delete it without owner migration."
}
$result.knownHardBarrierOrOwnerReviewResidue = $knownResidue

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
$result | ConvertTo-Json -Depth 3

if ($FailOnFatalNativeSafetyCandidates -and $result.nativeContainerSafetyFatalUnregisteredTokens -gt 0) {
    Write-Error -ErrorAction Continue "SHINOBU_206 FatalArchitectureException-equivalent native safety gate: $($result.nativeContainerSafetyFatalUnregisteredTokens) runtime NativeDisableContainerSafetyRestriction candidate(s) lack registered schedule/fence proof; owner-disputed run-only: $($result.nativeContainerSafetyOwnerDisputedRunOnlyTokens)."
    exit $FatalNativeSafetyExitCode
}
