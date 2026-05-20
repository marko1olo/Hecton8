param(
    [string]$ScriptsRoot = "Assets/_Project/Scripts",
    [string]$ReportPath = "Docs/Reports/DISPATCHER_OPTIMIZATION_REPORT.json"
)

$ErrorActionPreference = "Stop"

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
        $Line.Contains("PostSimulationTick(") -or
        $Line.Contains("VisualSyncTick(")
}

function Test-EditorFile {
    param([string]$Path, [string[]]$Lines)
    $leafName = [System.IO.Path]::GetFileName($Path)
    if ($Path.Contains("/Editor/") -or
        $Path.Contains("/Tests/") -or
        $Path.Contains("/Dev/") -or
        $Path.Contains("/QA/") -or
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
                $upper.Contains("SHUTDOWN") -or
                $upper.Contains("RELOAD") -or
                $upper.Contains("BARRIER") -or
                $upper.Contains("FORBARRIER") -or
                $upper.Contains("POSTSIMULATION") -or
                $upper.Contains("FIXEDSIMULATION") -or
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
                $upper.Contains("MOCK") -or
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

function Test-OwnerDisputedRuntimeRun {
    param([string]$Path)
    $leafName = [System.IO.Path]::GetFileName($Path)
    return $leafName -eq "AbyssalDeferredCausticsRuntime.cs"
}

$tokenLines = & rg -n "\.Complete\(|CompleteAll\(|\.Run\(|TryComplete\(" $ScriptsRoot -g "*.cs"
if ($LASTEXITCODE -gt 1) {
    throw "rg failed with exit code $LASTEXITCODE"
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
    reportModel = "FAST_TOKEN_CONTEXT_SCAN_WITH_LEGACY_HELPER_GATE"
    scannedTokenFiles = $hitsByFile.Count
    totalSyncTokens = 0
    coldOrEditorTokens = 0
    directCompleteTokens = 0
    directCompleteHotPathTokens = 0
    runtimeRunLexicalTokens = 0
    runtimeRunTokens = 0
    ownerDisputedRuntimeRunTokens = 0
    forcedFenceTokens = 0
    forcedHotPathTokens = 0
    centralDispatcherHardFenceTokens = 0
    teardownOrBarrierTokens = 0
    hotPathTokens = 0
    methodScopedHotPathTokens = 0
    unclassifiedRuntimeTokens = 0
    hotPathOffenders = New-Object System.Collections.Generic.List[object]
    forcedHotPathSamples = New-Object System.Collections.Generic.List[string]
    runtimeRunTokenSamples = New-Object System.Collections.Generic.List[string]
    ownerDisputedRuntimeRunSamples = New-Object System.Collections.Generic.List[string]
    editorOrToolRunResidue = New-Object System.Collections.Generic.List[string]
    coldScheduleCompleteResidue = New-Object System.Collections.Generic.List[string]
    centralDispatcherHardFenceSamples = New-Object System.Collections.Generic.List[string]
    teardownOrBarrierSamples = New-Object System.Collections.Generic.List[string]
    unclassifiedRuntimeSamples = New-Object System.Collections.Generic.List[string]
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

        if ($hasRun -and (Test-OwnerDisputedRuntimeRun $path)) {
            $result.ownerDisputedRuntimeRunTokens++
            if ($result.ownerDisputedRuntimeRunSamples.Count -lt 16) {
                $result.ownerDisputedRuntimeRunSamples.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            continue
        }

        if ($editorFile -or (Test-EditorBlockContext $lines $lineIndex) -or (Test-ColdAnnotated $lines $lineIndex)) {
            $result.coldOrEditorTokens++
            if ($hasRun) {
                $result.editorOrToolRunResidue.Add("${path}:$($lineIndex + 1)") | Out-Null
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

            continue
        }

        if (Test-TeardownOrBarrierContext $lines $lineIndex) {
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

        $isMethodHot = -not $editorFile -and (Test-LineInHotMethod $lines $lineIndex)
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

$knownResidue = @(
    "DispatcherJobFence and DispatcherJobSwap remain the central raw hard-fence surfaces for teardown, AUP, memory release, and deterministic barriers.",
    "PersistentWorldRegistry tombstone sweep requires owner-level snapshot/deferred mutation design before live delta mutation can avoid all hard fences.",
    "GlobalPhysicsStateManager tracked body mutation requires owner-level pending body-delta buffer.",
    "LockstepStateValidator POST_SIM hash validation is an explicit deterministic blocking proof point unless netcode owner accepts delayed validation.",
    "HectonFloatingOrigin transform shift is an AUP hard barrier."
)
if ($result.ownerDisputedRuntimeRunTokens -gt 0) {
    $knownResidue += "AbyssalDeferredCausticsRuntime job.Run() is owner-disputed by SHINOBU_232; SHINOBU_206 stopped direct rewrites after three overwrites and reports it separately."
}
$result.knownHardBarrierOrOwnerReviewResidue = $knownResidue

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
$result | ConvertTo-Json -Depth 3
