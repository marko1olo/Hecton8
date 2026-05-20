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

function Get-MethodName {
    param([string]$Line)
    $trimmed = $Line.TrimStart()
    if ((Test-CommentLine $Line) -or
        $trimmed.StartsWith("if") -or
        $trimmed.StartsWith("for") -or
        $trimmed.StartsWith("while") -or
        $trimmed.StartsWith("switch") -or
        $trimmed.StartsWith("catch") -or
        $trimmed.StartsWith("using") -or
        $trimmed.StartsWith("lock") -or
        $trimmed.StartsWith("return") -or
        $trimmed.StartsWith("new ")) {
        return $null
    }

    $open = $Line.IndexOf("(")
    if ($open -le 0) {
        return $null
    }

    $equals = $Line.IndexOf("=")
    if ($equals -ge 0 -and $equals -lt $open) {
        return $null
    }

    $cursor = $open - 1
    while ($cursor -ge 0 -and [char]::IsWhiteSpace($Line[$cursor])) {
        $cursor--
    }

    $end = $cursor
    while ($cursor -ge 0 -and ([char]::IsLetterOrDigit($Line[$cursor]) -or $Line[$cursor] -eq "_")) {
        $cursor--
    }

    $start = $cursor + 1
    if ($start -gt $end) {
        return $null
    }

    $name = $Line.Substring($start, $end - $start + 1)
    if ($name -in @("if", "for", "while", "switch", "catch", "using", "lock", "return", "nameof")) {
        return $null
    }

    return $name
}

function Get-BodyRange {
    param([string[]]$Lines, [int]$SignatureLine)
    $depth = 0
    $started = $false
    $startLine = -1
    for ($i = $SignatureLine; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        if (-not $started) {
            $open = ($line.ToCharArray() | Where-Object { $_ -eq "{" }).Count
            $close = ($line.ToCharArray() | Where-Object { $_ -eq "}" }).Count
            if ($open -le 0) {
                if ($line.Contains(";")) {
                    return $null
                }

                continue
            }

            $started = $true
            $startLine = $i
            $depth += $open - $close
            if ($depth -le 0) {
                return [pscustomobject]@{ Start = $startLine; End = $i }
            }

            continue
        }

        $depth += (($line.ToCharArray() | Where-Object { $_ -eq "{" }).Count) -
            (($line.ToCharArray() | Where-Object { $_ -eq "}" }).Count)
        if ($depth -le 0) {
            return [pscustomobject]@{ Start = $startLine; End = $i }
        }
    }

    return $null
}

function New-HotMap {
    param([string[]]$Lines)

    $hot = New-Object bool[] $Lines.Length
    $methods = New-Object System.Collections.Generic.List[object]
    $byName = @{}
    $callRegex = [regex]"\b([A-Za-z_][A-Za-z0-9_]*)\s*\("

    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $name = Get-MethodName $Lines[$i]
        if ($null -eq $name) {
            continue
        }

        $body = Get-BodyRange $Lines $i
        if ($null -eq $body) {
            continue
        }

        $calls = New-Object System.Collections.Generic.HashSet[string]
        for ($lineIndex = $body.Start; $lineIndex -le $body.End; $lineIndex++) {
            $line = $Lines[$lineIndex]
            if (Test-CommentLine $line) {
                continue
            }

            foreach ($match in $callRegex.Matches($line)) {
                [void]$calls.Add($match.Groups[1].Value)
            }
        }

        $range = [pscustomobject]@{
            Name = $name
            Start = $body.Start
            End = $body.End
            IsHot = (Test-HotSignature $Lines[$i])
            Calls = $calls
        }

        $methods.Add($range) | Out-Null
        if (-not $byName.ContainsKey($name)) {
            $byName[$name] = New-Object System.Collections.Generic.List[object]
        }

        $byName[$name].Add($range) | Out-Null
    }

    $queue = New-Object System.Collections.Generic.Queue[object]
    foreach ($method in $methods) {
        if ($method.IsHot) {
            $queue.Enqueue($method)
        }
    }

    while ($queue.Count -gt 0) {
        $method = $queue.Dequeue()
        foreach ($call in $method.Calls) {
            if (-not $byName.ContainsKey($call)) {
                continue
            }

            foreach ($callee in $byName[$call]) {
                if ($callee.IsHot) {
                    continue
                }

                $callee.IsHot = $true
                $queue.Enqueue($callee)
            }
        }
    }

    foreach ($method in $methods) {
        if (-not $method.IsHot) {
            continue
        }

        for ($lineIndex = $method.Start; $lineIndex -le $method.End -and $lineIndex -lt $hot.Length; $lineIndex++) {
            $hot[$lineIndex] = $true
        }
    }

    return $hot
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
    reportModel = "FAST_TOKEN_FILE_CALL_GRAPH_SCAN"
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
    $hotMap = $null
    if (-not $editorFile) {
        $hotMap = New-HotMap $lines
    }

    foreach ($hit in $hitsByFile[$path]) {
        $lineIndex = $hit.LineNumber - 1
        if ($lineIndex -lt 0 -or $lineIndex -ge $lines.Length) {
            continue
        }

        $line = $lines[$lineIndex]
        if (Test-CommentLine $line) {
            continue
        }

        $hasComplete = $line.Contains(".Complete(") -or $line.Contains("CompleteAll(")
        $hasRun = $line.Contains(".Run(") -and -not $line.Contains("Task.Run(")
        $hasForcedFence = $line.Contains("TryComplete(") -and ($line.Contains("forceComplete: true") -or $line.Contains(", true)"))
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

        $isMethodHot = $hotMap -ne $null -and $hotMap[$lineIndex]
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
