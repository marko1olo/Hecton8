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
    if ($Path.Contains("/Editor/") -or $Path.Contains("/Tests/") -or $Path.Contains("/Dev/") -or $Path.EndsWith("SmokeTester.cs")) {
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
    $start = [Math]::Max(0, $LineIndex - 2)
    for ($i = $LineIndex; $i -ge $start; $i--) {
        $line = $Lines[$i]
        if ($line.Contains("COLD SYNC JOB") -or
            $line.Contains("COLD/EDITOR") -or
            $line.Contains("COLD PROFILE PATH") -or
            $line.Contains("Cold Boot") -or
            $line.Contains("smoke validation")) {
            return $true
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
    forcedFenceTokens = 0
    forcedHotPathTokens = 0
    hotPathTokens = 0
    methodScopedHotPathTokens = 0
    unclassifiedRuntimeTokens = 0
    hotPathOffenders = New-Object System.Collections.Generic.List[object]
    forcedHotPathSamples = New-Object System.Collections.Generic.List[string]
    runtimeRunTokenSamples = New-Object System.Collections.Generic.List[string]
    editorOrToolRunResidue = New-Object System.Collections.Generic.List[string]
    coldScheduleCompleteResidue = New-Object System.Collections.Generic.List[string]
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

        if ($editorFile -or (Test-ColdAnnotated $lines $lineIndex)) {
            $result.coldOrEditorTokens++
            if ($hasRun) {
                $result.editorOrToolRunResidue.Add("${path}:$($lineIndex + 1)") | Out-Null
            }

            if ($hasComplete -and $line.Contains("Schedule(")) {
                $result.coldScheduleCompleteResidue.Add("${path}:$($lineIndex + 1)") | Out-Null
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
        }
    }
}

$result.knownHardBarrierOrOwnerReviewResidue = @(
    "DispatcherJobFence and DispatcherJobSwap remain the central raw hard-fence surfaces for teardown, AUP, memory release, and deterministic barriers.",
    "PersistentWorldRegistry tombstone sweep requires owner-level snapshot/deferred mutation design before live delta mutation can avoid all hard fences.",
    "GlobalPhysicsStateManager tracked body mutation requires owner-level pending body-delta buffer.",
    "LockstepStateValidator POST_SIM hash validation is an explicit deterministic blocking proof point unless netcode owner accepts delayed validation.",
    "HectonFloatingOrigin transform shift is an AUP hard barrier."
)

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
$result | ConvertTo-Json -Depth 3
