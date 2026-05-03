[CmdletBinding()]
param(
    [string]$ProjectRoot = 'C:\hades\Hecton8',
    [string]$OutputMarkdown = 'C:\hades\Hecton8\Docs\Reports\2026-05-03_FOUNDATION_GUARD_SCAN.md'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptsRoot = Join-Path $ProjectRoot 'Assets\_Project\Scripts'
if (-not (Test-Path $scriptsRoot)) {
    throw "Missing scripts root: $scriptsRoot"
}

$files = @(Get-ChildItem -Path $scriptsRoot -Recurse -Filter *.cs -ErrorAction SilentlyContinue)

$selfRegistrationRegex = [regex]'GlobalRegistry\.(?:Register[A-Za-z0-9_]*\([^;]*\bthis\b[^;]*\)|Renderables\.Register\(this\))'
$blindRegistryFlagRegex = [regex]'GlobalRegistry\.(?:Register[A-Za-z0-9_]*\([^;]*\bthis\b[^;]*\)|Renderables\.Register\(this\))\s*;\s*_[A-Za-z0-9]+\s*=\s*true'
$originShiftBlindFlagRegex = [regex]'HectonFloatingOrigin\.RegisterListener\(this\)\s*;\s*_[A-Za-z0-9]+\s*=\s*true'
$jobRunRegex = [regex]'\.Run\('
$completionRegex = [regex]'\.Complete\('
$unsafeMemCpyRegex = [regex]'UnsafeUtility\.MemCpy'
$legacyPlayerSignalRegex = [regex]'PlayerSignalEvents\.On(?:TraumaHudSignal|InteractionSignal|ToolDepletedSignal)'
$rawListenerDispatchRegex = [regex]'rawArray\s*\[\s*i\s*\]\s*\.\s*On[A-Za-z0-9_]+\s*\('
$globalRegistryInputNullableRegex = [regex]'(?:GlobalRegistry\.Input\s*(?:[!=]=\s*null|\?\.))|(?:null\s*(?:==|!=)\s*GlobalRegistry\.Input)'
$inputManagerInstanceRegex = [regex]'\bInputManager\.Instance\b'
$optimizationSingletonResidueRegex = [regex]'(?:\b_instance\b|(?:public|internal)\s+static\s+.*\bInstance\b|Instance\s*=>|DontDestroyOnLoad\(|SINGLETON)'
$unityLoopRegex = [regex]'\bvoid\s+(?:Update|FixedUpdate|LateUpdate)\s*\('
$legacyCoroutineRegex = [regex]'\b(?:StartCoroutine|StopCoroutine)\s*\(|\bIEnumerator\s+[A-Za-z_][A-Za-z0-9_]*\s*\(|\byield\s+return\b'
$forbiddenRuntimeAssetApiRegex = [regex]'\b(?:Resources\.Load|Camera\.main)\b'
$debugLogRegex = [regex]'\b(?:Debug|UnityEngine\.Debug)\.(?:Log|LogWarning|LogError)\s*\('
$runtimeFindRegex = [regex]'\b(?:GameObject\.Find(?:WithTag)?|FindObjectOfType|FindAnyObjectByType|FindFirstObjectByType|FindObjectsByType)\b'
$broadPhysicsMaskRegex = [regex]'(?:(?:\bLayerMask\s+[A-Za-z0-9_]+|[A-Za-z0-9_]*(?:Physics|Raycast|SphereCast|CapsuleCast|BoxCast|Overlap|Query)[A-Za-z0-9_]*LayerMask[A-Za-z0-9_]*)\s*=\s*(?:-1|~0))|(?:\bPhysics\.(?:Raycast|RaycastNonAlloc|SphereCast|SphereCastNonAlloc|CapsuleCast|CapsuleCastNonAlloc|BoxCast|BoxCastNonAlloc|Linecast|OverlapSphere|OverlapSphereNonAlloc|OverlapBox|OverlapBoxNonAlloc|OverlapCapsule|OverlapCapsuleNonAlloc)\b[^\r\n]*(?:-1|~0))|(?:\b(?:RaycastCommand|SphereCastCommand|CapsuleCastCommand|BoxCastCommand)\s*\([^\r\n]*(?:-1|~0))'
$callExpressionRegex = [regex]'\b(?<Name>[A-Za-z_][A-Za-z0-9_]*)\s*\('
$memberSignatureRegex = [regex]'^\s*(?:(?:public|private|protected|internal)\s+)?(?:static\s+)?(?:async\s+)?(?:unsafe\s+)?(?:partial\s+)?(?:[A-Za-z_][A-Za-z0-9_<>\[\],\.\s]*\s+)?(?<Name>[A-Za-z_][A-Za-z0-9_]*)\s*\([^;]*\)\s*(?:where\s+.*)?$'
$qualifiedCallLineRegex = [regex]'^\s*(?:[A-Za-z_][A-Za-z0-9_]*\.)+[A-Za-z_][A-Za-z0-9_]*\s*\('
$hotPathMemberRegex = [regex]'^(Tick|FixedTick|Update|LateUpdate|FixedUpdate|SlowTick|LateFrameTick|PostFixedTick|OnTriggerStay|OnTriggerEnter|OnCollisionEnter|OnCollisionStay|OnControllerColliderHit|OnGUI)$'
$controlKeywordRegex = [regex]'^(if|for|foreach|while|switch|catch|using|lock|return|new|throw)$'
$hotPathCallOwners = New-Object 'System.Collections.Generic.Dictionary[string,string]'

function New-HotPathCallOwnerKey {
    param(
        [string]$Path,
        [string]$Member
    )

    return "$Path|$Member"
}

function Test-DevelopmentLogGuardExpression {
    param([string]$Expression)

    $withoutNegatedSymbols = [regex]::Replace(
        $Expression,
        '!\s*(?:defined\s*\(\s*)?(?:UNITY_EDITOR|DEVELOPMENT_BUILD)\s*\)?',
        '')

    return $withoutNegatedSymbols -match '\b(?:UNITY_EDITOR|DEVELOPMENT_BUILD)\b'
}

$blindRegistryHits = New-Object System.Collections.Generic.List[object]
$originShiftBlindHits = New-Object System.Collections.Generic.List[object]
$selfRegistrationCount = 0
$jobRunHits = New-Object System.Collections.Generic.List[object]
$completionHits = New-Object System.Collections.Generic.List[object]
$unsafeMemCpyHits = New-Object System.Collections.Generic.List[object]
$legacyPlayerSignalHits = New-Object System.Collections.Generic.List[object]
$rawListenerDispatchHits = New-Object System.Collections.Generic.List[object]
$globalRegistryInputNullableHits = New-Object System.Collections.Generic.List[object]
$inputManagerInstanceHits = New-Object System.Collections.Generic.List[object]
$optimizationSingletonResidueHits = New-Object System.Collections.Generic.List[object]
$unauthorizedUnityLoopHits = New-Object System.Collections.Generic.List[object]
$legacyCoroutineHits = New-Object System.Collections.Generic.List[object]
$forbiddenRuntimeAssetApiHits = New-Object System.Collections.Generic.List[object]
$debugLogHits = New-Object System.Collections.Generic.List[object]
$runtimeFindHits = New-Object System.Collections.Generic.List[object]
$broadPhysicsMaskHits = New-Object System.Collections.Generic.List[object]

function New-GuardHit {
    param(
        [string]$Path,
        [int]$Line,
        [string]$Text,
        [string]$Member
    )

    $hotPathRisk = $false
    $hotPathReason = 'not traced to hot member'
    if ($Member -and $hotPathMemberRegex.IsMatch($Member)) {
        $hotPathRisk = $true
        $hotPathReason = 'direct hot member'
    } elseif ($Member) {
        $ownerKey = New-HotPathCallOwnerKey -Path $Path -Member $Member
        if ($hotPathCallOwners.ContainsKey($ownerKey)) {
            $hotPathRisk = $true
            $hotPathReason = "called by hot member: $($hotPathCallOwners[$ownerKey])"
        }
    }

    $guardClassification = 'unclassified'
    if ($Path -like '*DispatcherJobSwap.cs' -and $Text -match '\bhandle\.Complete\(\);') {
        $hotPathRisk = $false
        $hotPathReason = 'guarded by DispatcherJobSwap.TryComplete IsCompleted/swap-window contract'
        $guardClassification = 'guarded dispatcher completion'
    }

    [PSCustomObject]@{
        Path = $Path
        Line = $Line
        Text = $Text.Trim()
        Member = if ($Member) { $Member } else { '<unknown>' }
        HotPathRisk = $hotPathRisk
        HotPathReason = $hotPathReason
        GuardClassification = $guardClassification
    }
}

function Remove-GuardScanNonCodeText {
    param([string]$Line)

    $withoutStrings = $Line
    if ($withoutStrings.IndexOf('"') -ge 0) {
        $withoutStrings = [regex]::Replace($withoutStrings, '@?"(?:[^"\\]|\\.)*"', '""')
    }

    $commentIndex = $withoutStrings.IndexOf('//')
    if ($commentIndex -ge 0) {
        return $withoutStrings.Substring(0, $commentIndex)
    }

    return $withoutStrings
}

function Remove-GuardScanCommentText {
    param([string]$Line)

    $commentIndex = $Line.IndexOf('//')
    if ($commentIndex -ge 0) {
        return $Line.Substring(0, $commentIndex)
    }

    return $Line
}

function Join-GuardLineWindow {
    param(
        [string[]]$Lines,
        [int]$StartIndex,
        [int]$WindowLength
    )

    $endIndex = [Math]::Min($Lines.Length - 1, $StartIndex + $WindowLength - 1)
    $builder = [System.Text.StringBuilder]::new()
    for ($i = $StartIndex; $i -le $endIndex; $i++) {
        [void]$builder.Append($Lines[$i])
        [void]$builder.Append(' ')
    }

    return $builder.ToString()
}

foreach ($file in $files) {
    $relativePath = $file.FullName.Replace($ProjectRoot, '').TrimStart('\')
    $fileText = [System.IO.File]::ReadAllText($file.FullName)
    $hasHotPathMemberToken =
        $fileText.Contains('Tick(') -or
        $fileText.Contains('FixedTick(') -or
        $fileText.Contains('Update(') -or
        $fileText.Contains('LateUpdate(') -or
        $fileText.Contains('FixedUpdate(') -or
        $fileText.Contains('SlowTick(') -or
        $fileText.Contains('LateFrameTick(') -or
        $fileText.Contains('PostFixedTick(') -or
        $fileText.Contains('OnTriggerStay(') -or
        $fileText.Contains('OnTriggerEnter(') -or
        $fileText.Contains('OnCollisionEnter(') -or
        $fileText.Contains('OnCollisionStay(') -or
        $fileText.Contains('OnControllerColliderHit(') -or
        $fileText.Contains('OnGUI(')

    if (-not $hasHotPathMemberToken) {
        continue
    }

    $lineNumber = 0
    $currentMember = '<unknown>'
    $lines = [System.IO.File]::ReadLines($file.FullName)
    foreach ($lineText in $lines) {
        $lineNumber++
        $codeLineText = Remove-GuardScanCommentText $lineText

        if ($codeLineText.Contains('(') -and -not $codeLineText.Contains(';') -and -not $qualifiedCallLineRegex.IsMatch($codeLineText)) {
            $memberMatch = $memberSignatureRegex.Match($codeLineText)
            if ($memberMatch.Success) {
                $candidateMember = $memberMatch.Groups['Name'].Value
                if (-not $controlKeywordRegex.IsMatch($candidateMember)) {
                    $currentMember = $candidateMember
                }
            }
        }

        if (-not $hotPathMemberRegex.IsMatch($currentMember)) {
            continue
        }

        if (-not $codeLineText.Contains('(')) {
            continue
        }

        $callMatches = $callExpressionRegex.Matches($codeLineText)
        foreach ($callMatch in $callMatches) {
            $calledMember = $callMatch.Groups['Name'].Value
            if ($controlKeywordRegex.IsMatch($calledMember) -or $hotPathMemberRegex.IsMatch($calledMember)) {
                continue
            }

            $ownerKey = New-HotPathCallOwnerKey -Path $relativePath -Member $calledMember
            if (-not $hotPathCallOwners.ContainsKey($ownerKey)) {
                $hotPathCallOwners[$ownerKey] = "${relativePath}:$lineNumber $currentMember"
            }
        }
    }
}

foreach ($file in $files) {
    $relativePath = $file.FullName.Replace($ProjectRoot, '').TrimStart('\')
    $fileText = [System.IO.File]::ReadAllText($file.FullName)
    $isEditorPath = $relativePath -like '*\Editor\*'

    $hasRegistryToken = $fileText.Contains('GlobalRegistry.') -or $fileText.Contains('Renderables.Register')
    $hasOriginShiftToken = $fileText.Contains('HectonFloatingOrigin.RegisterListener(this)')
    $hasJobRunToken = $fileText.Contains('.Run(')
    $hasCompletionToken = $fileText.Contains('.Complete(')
    $hasUnsafeMemCpyToken = $fileText.Contains('UnsafeUtility.MemCpy')
    $hasLegacyPlayerSignalToken = $fileText.Contains('PlayerSignalEvents.On')
    $hasRawListenerToken = $fileText.Contains('rawArray')
    $hasGlobalRegistryInputToken = $fileText.Contains('GlobalRegistry.Input')
    $hasInputManagerInstanceToken = $fileText.Contains('InputManager.Instance')
    $hasOptimizationResidueToken =
        $relativePath -like 'Assets\_Project\Scripts\Optimization\*' -and
        ($fileText.Contains('Instance') -or $fileText.Contains('_instance') -or $fileText.Contains('DontDestroyOnLoad') -or $fileText.Contains('SINGLETON'))
    $hasUnityLoopToken = $fileText.Contains('Update(') -or $fileText.Contains('FixedUpdate(') -or $fileText.Contains('LateUpdate(')
    $hasLegacyCoroutineToken = $fileText.Contains('Coroutine') -or $fileText.Contains('IEnumerator') -or $fileText.Contains('yield return')
    $hasForbiddenRuntimeAssetToken = $fileText.Contains('Resources.Load') -or $fileText.Contains('Camera.main')
    $hasDebugLogToken = $fileText.Contains('Debug.')
    $hasBroadPhysicsMaskToken =
        ($fileText.Contains('-1') -or $fileText.Contains('~0')) -and
        ($fileText.Contains('LayerMask') -or $fileText.Contains('Physics') -or $fileText.Contains('Raycast') -or $fileText.Contains('SphereCast') -or $fileText.Contains('CapsuleCast') -or $fileText.Contains('BoxCast') -or $fileText.Contains('Overlap'))
    $hasRuntimeFindToken = $fileText.Contains('Find')
    $needsLineScan =
        $hasRegistryToken -or
        $hasOriginShiftToken -or
        $hasJobRunToken -or
        $hasCompletionToken -or
        $hasUnsafeMemCpyToken -or
        $hasLegacyPlayerSignalToken -or
        $hasRawListenerToken -or
        $hasGlobalRegistryInputToken -or
        $hasInputManagerInstanceToken -or
        $hasOptimizationResidueToken -or
        $hasUnityLoopToken -or
        $hasLegacyCoroutineToken -or
        $hasForbiddenRuntimeAssetToken -or
        $hasDebugLogToken -or
        $hasBroadPhysicsMaskToken -or
        $hasRuntimeFindToken

    if (-not $needsLineScan) {
        continue
    }

    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $developmentGuardStack = New-Object 'System.Collections.Generic.List[bool]'

    $lineNumber = 0
    $currentMember = '<unknown>'
    $currentMemberDebugConditional = $false
    $pendingDebugConditionalAttribute = $false
    foreach ($lineText in $lines) {
        $lineNumber++
        $lineIndex = $lineNumber - 1
        $trimmedLine = $lineText.Trim()

        if ($trimmedLine.StartsWith('#if ')) {
            $developmentGuardStack.Add((Test-DevelopmentLogGuardExpression -Expression $trimmedLine.Substring(3).Trim()))
            continue
        }

        if ($trimmedLine.StartsWith('#elif ')) {
            if ($developmentGuardStack.Count -gt 0) {
                $developmentGuardStack[$developmentGuardStack.Count - 1] = Test-DevelopmentLogGuardExpression -Expression $trimmedLine.Substring(5).Trim()
            }

            continue
        }

        if ($trimmedLine -eq '#else') {
            if ($developmentGuardStack.Count -gt 0) {
                $developmentGuardStack[$developmentGuardStack.Count - 1] = $false
            }

            continue
        }

        if ($trimmedLine -eq '#endif') {
            if ($developmentGuardStack.Count -gt 0) {
                $developmentGuardStack.RemoveAt($developmentGuardStack.Count - 1)
            }

            continue
        }

        $codeLineText = $lineText
        if (($lineText.IndexOf('"') -ge 0 -or $lineText.IndexOf('//') -ge 0) -and
            ($lineText.Contains('InputManager.Instance') -or $lineText.Contains('GlobalRegistry.Input') -or $lineText.Contains('Debug.'))) {
            $codeLineText = Remove-GuardScanNonCodeText $lineText
        }

        if ($hasRegistryToken -and ($codeLineText.Contains('GlobalRegistry.') -or $codeLineText.Contains('Renderables.Register'))) {
            $selfRegistrationMatches = $selfRegistrationRegex.Matches($codeLineText)
            if ($selfRegistrationMatches.Count -gt 0) {
                $selfRegistrationCount += $selfRegistrationMatches.Count
                $windowText = Join-GuardLineWindow -Lines $lines -StartIndex $lineIndex -WindowLength 4
                if ($blindRegistryFlagRegex.IsMatch($windowText)) {
                    $blindRegistryHits.Add([PSCustomObject]@{
                        Path = $relativePath
                        Line = $lineNumber
                    })
                }
            }
        }

        if ($hasOriginShiftToken -and $codeLineText.Contains('HectonFloatingOrigin.RegisterListener(this)')) {
            $windowText = Join-GuardLineWindow -Lines $lines -StartIndex $lineIndex -WindowLength 4
            if ($originShiftBlindFlagRegex.IsMatch($windowText)) {
                $originShiftBlindHits.Add([PSCustomObject]@{
                    Path = $relativePath
                    Line = $lineNumber
                })
            }
        }

        if ($trimmedLine.StartsWith('[') -and
            $trimmedLine.Contains('Conditional(') -and
            ($trimmedLine.Contains('UNITY_EDITOR') -or $trimmedLine.Contains('DEVELOPMENT_BUILD'))) {
            $pendingDebugConditionalAttribute = $true
        }

        if ($lineText.Contains('(') -and -not $lineText.Contains(';') -and -not $qualifiedCallLineRegex.IsMatch($lineText)) {
            $memberMatch = $memberSignatureRegex.Match($lineText)
            if ($memberMatch.Success) {
                $candidateMember = $memberMatch.Groups['Name'].Value
                if (-not $controlKeywordRegex.IsMatch($candidateMember)) {
                    $currentMember = $candidateMember
                    $currentMemberDebugConditional = $pendingDebugConditionalAttribute
                }

                $pendingDebugConditionalAttribute = $false
            }
        } elseif ($trimmedLine.Length -gt 0 -and -not $trimmedLine.StartsWith('[')) {
            $pendingDebugConditionalAttribute = $false
        }

        if ($hasJobRunToken -and $codeLineText.Contains('.Run(') -and $jobRunRegex.IsMatch($codeLineText)) {
            $jobRunHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasCompletionToken -and $codeLineText.Contains('.Complete(') -and $completionRegex.IsMatch($codeLineText)) {
            $completionHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasUnsafeMemCpyToken -and $codeLineText.Contains('UnsafeUtility.MemCpy') -and $unsafeMemCpyRegex.IsMatch($codeLineText) -and $relativePath -notlike '*UnsafeMemoryCopyGuard.cs') {
            $unsafeMemCpyHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasLegacyPlayerSignalToken -and $codeLineText.Contains('PlayerSignalEvents.On') -and $legacyPlayerSignalRegex.IsMatch($codeLineText)) {
            $legacyPlayerSignalHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasRawListenerToken -and $codeLineText.Contains('rawArray') -and $rawListenerDispatchRegex.IsMatch($codeLineText)) {
            $rawListenerDispatchHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasGlobalRegistryInputToken -and $codeLineText.Contains('GlobalRegistry.Input') -and $globalRegistryInputNullableRegex.IsMatch($codeLineText)) {
            $globalRegistryInputNullableHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasInputManagerInstanceToken -and $codeLineText.Contains('InputManager.Instance') -and $inputManagerInstanceRegex.IsMatch($codeLineText)) {
            $inputManagerInstanceHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasOptimizationResidueToken -and
            ($codeLineText.Contains('Instance') -or $codeLineText.Contains('_instance') -or $codeLineText.Contains('DontDestroyOnLoad') -or $codeLineText.Contains('SINGLETON')) -and
            $optimizationSingletonResidueRegex.IsMatch($codeLineText)) {
            $optimizationSingletonResidueHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasUnityLoopToken -and
            ($codeLineText.Contains('Update(') -or $codeLineText.Contains('FixedUpdate(') -or $codeLineText.Contains('LateUpdate(')) -and
            $unityLoopRegex.IsMatch($codeLineText) -and
            -not $isEditorPath -and
            $relativePath -ne 'Assets\_Project\Scripts\Core\SystemDispatcher.cs') {
            $unauthorizedUnityLoopHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasLegacyCoroutineToken -and
            ($codeLineText.Contains('Coroutine') -or $codeLineText.Contains('IEnumerator') -or $codeLineText.Contains('yield return')) -and
            $legacyCoroutineRegex.IsMatch($codeLineText) -and -not $isEditorPath) {
            $legacyCoroutineHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasForbiddenRuntimeAssetToken -and
            ($codeLineText.Contains('Resources.Load') -or $codeLineText.Contains('Camera.main')) -and
            $forbiddenRuntimeAssetApiRegex.IsMatch($codeLineText) -and -not $isEditorPath) {
            $forbiddenRuntimeAssetApiHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasDebugLogToken -and $codeLineText.Contains('Debug.') -and
            $debugLogRegex.IsMatch($codeLineText) -and -not $isEditorPath -and
            -not $developmentGuardStack.Contains($true) -and
            -not $currentMemberDebugConditional) {
            $debugLogHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasBroadPhysicsMaskToken -and
            ($codeLineText.Contains('-1') -or $codeLineText.Contains('~0')) -and
            ($codeLineText.Contains('LayerMask') -or $codeLineText.Contains('Physics') -or $codeLineText.Contains('Raycast') -or $codeLineText.Contains('SphereCast') -or $codeLineText.Contains('CapsuleCast') -or $codeLineText.Contains('BoxCast') -or $codeLineText.Contains('Overlap')) -and
            $broadPhysicsMaskRegex.IsMatch($codeLineText) -and -not $isEditorPath -and $codeLineText -notmatch 'EverythingLayerMaskValue\s*=') {
            $broadPhysicsMaskHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }

        if ($hasRuntimeFindToken -and $codeLineText.Contains('Find') -and $runtimeFindRegex.IsMatch($codeLineText) -and -not $isEditorPath) {
            $runtimeFindHits.Add((New-GuardHit -Path $relativePath -Line $lineNumber -Text $lineText -Member $currentMember))
        }
    }
}

$hotJobRunReviewCount = @($jobRunHits | Where-Object { $_.HotPathRisk }).Count
$guardedCompletionCount = @($completionHits | Where-Object { $_.GuardClassification -ne 'unclassified' }).Count
$hotInputManagerInstanceReviewCount = @($inputManagerInstanceHits | Where-Object { $_.HotPathRisk }).Count
$directHotDebugLogHits = @($debugLogHits | Where-Object { $_.HotPathReason -eq 'direct hot member' })
$hotDebugLogReviewHits = @($debugLogHits | Where-Object { $_.HotPathRisk -and $_.HotPathReason -ne 'direct hot member' })
$directHotDebugLogCount = $directHotDebugLogHits.Count
$hotDebugLogReviewCount = $hotDebugLogReviewHits.Count

$now = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# Foundation Guard Scan')
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Generated: $now")
[void]$sb.AppendLine("- Project root: $ProjectRoot")
[void]$sb.AppendLine("- Scope: Assets/_Project/Scripts/**/*.cs")
[void]$sb.AppendLine("- Status: PENDING VERIFICATION")
[void]$sb.AppendLine()
[void]$sb.AppendLine('## Guard Results')
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Guard | Count | Meaning |")
[void]$sb.AppendLine("|---|---:|---|")
[void]$sb.AppendLine("| Global registry self-registration sites | $selfRegistrationCount | Informational. Broad GlobalRegistry.Register*(this) and Renderables.Register(this) scan. Must use registry truth-state checks, not blind flags. |")
[void]$sb.AppendLine("| Blind registry flag drift | $($blindRegistryHits.Count) | Must be 0. Pattern: Register*(this, ...) followed by _field = true. |")
[void]$sb.AppendLine("| Origin shift listener blind flag drift | $($originShiftBlindHits.Count) | Must be 0. Pattern: HectonFloatingOrigin.RegisterListener(this) followed by _field = true. |")
[void]$sb.AppendLine("| Synchronous job .Run( sites | $($jobRunHits.Count) | Must be 0. Synchronous JobSystem barriers are no longer allowed in first-party runtime source. |")
[void]$sb.AppendLine("| Hot-path synchronous job .Run( review sites | $hotJobRunReviewCount | Must be 0. Secondary classifier for stale sync job barriers in gameplay cadence. |")
[void]$sb.AppendLine("| Completion .Complete( text hits | $($completionHits.Count) | Review queue. Custom dispatcher completions must be classified separately from JobHandle.Complete. |")
[void]$sb.AppendLine("| Guarded dispatcher completion sites | $guardedCompletionCount | Source-level IsCompleted/swap-window helper pattern, not runtime proof. |")
[void]$sb.AppendLine("| UnsafeUtility.MemCpy outside guard | $($unsafeMemCpyHits.Count) | Must be 0 outside UnsafeMemoryCopyGuard. |")
[void]$sb.AppendLine("| Legacy PlayerSignalEvents.On* subscriptions | $($legacyPlayerSignalHits.Count) | Must be 0 after NativeQueue/listener migration. |")
[void]$sb.AppendLine("| Direct raw-array listener dispatch | $($rawListenerDispatchHits.Count) | Must be 0. Pattern rawArray[i].On* bypasses null-slot guards during event flush. |")
[void]$sb.AppendLine("| GlobalRegistry.Input nullable misuse | $($globalRegistryInputNullableHits.Count) | Must be 0. Input service is a null-object fallback; use IsInitialized and direct service calls. |")
[void]$sb.AppendLine("| Direct InputManager.Instance sites | $($inputManagerInstanceHits.Count) | Review queue. Runtime owners should prefer GlobalRegistry.Input/InputBinding; bootstrap-owned native binding paths must be documented. |")
[void]$sb.AppendLine("| Hot-path direct InputManager.Instance review sites | $hotInputManagerInstanceReviewCount | Must be 0. One-hop source classifier for stale singleton reads in gameplay cadence. |")
[void]$sb.AppendLine("| Optimization singleton residue | $($optimizationSingletonResidueHits.Count) | Must be 0. Optimization/VRAM services are registry-owned; no private _instance, static Instance, DDOL, or SINGLETON comments. |")
[void]$sb.AppendLine("| Unauthorized Unity loop methods | $($unauthorizedUnityLoopHits.Count) | Must be 0 outside SystemDispatcher/Core-approved exceptions. Gameplay cadence belongs to ITickable/dispatcher lanes. |")
[void]$sb.AppendLine("| Legacy coroutine sites | $($legacyCoroutineHits.Count) | Must be 0 outside Editor. Coroutines bypass controlled tick lanes and allocate state. |")
[void]$sb.AppendLine("| Forbidden runtime asset API sites | $($forbiddenRuntimeAssetApiHits.Count) | Must be 0 outside Editor. Forbids Resources.Load and Camera.main. |")
[void]$sb.AppendLine("| Release-reachable direct hot-path Debug.Log sites | $directHotDebugLogCount | Must be 0. Debug.Log/Warning/Error directly inside gameplay cadence is forbidden outside UNITY_EDITOR/DEVELOPMENT_BUILD guards. |")
[void]$sb.AppendLine("| Release-reachable one-hop Debug.Log review sites | $hotDebugLogReviewCount | Review queue. Conservative same-file call classifier; owner review required before promotion to hard gate. |")
[void]$sb.AppendLine("| Broad physics layer masks outside Editor | $($broadPhysicsMaskHits.Count) | Must be 0. Forbids LayerMask=-1, ~0, and direct all-layer masks in Physics/RaycastCommand lines. |")
[void]$sb.AppendLine("| Runtime Find API text hits outside Editor folder | $($runtimeFindHits.Count) | Review queue. Bootstrap cold paths must be documented; gameplay hot paths must be removed. |")
[void]$sb.AppendLine("| One-hop hot-path callee names | $($hotPathCallOwners.Count) | Audit classifier only. Used to mark .Run/.Complete/MemCpy/Find hits inside methods called by hot members. |")
[void]$sb.AppendLine()

[void]$sb.AppendLine('## Blind Registry Flag Hits')
[void]$sb.AppendLine()
if ($blindRegistryHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $blindRegistryHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Origin Shift Listener Blind Flag Hits')
[void]$sb.AppendLine()
if ($originShiftBlindHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $originShiftBlindHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Synchronous Job Run Sites')
[void]$sb.AppendLine()
if ($jobRunHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $jobRunHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Completion Text Hits')
[void]$sb.AppendLine()
if ($completionHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $completionHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } elseif ($entry.GuardClassification -ne 'unclassified') { "guarded review: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Unsafe MemCpy Outside Guard')
[void]$sb.AppendLine()
if ($unsafeMemCpyHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $unsafeMemCpyHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Legacy PlayerSignalEvents Subscriptions')
[void]$sb.AppendLine()
if ($legacyPlayerSignalHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $legacyPlayerSignalHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Direct Raw-Array Listener Dispatch')
[void]$sb.AppendLine()
if ($rawListenerDispatchHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $rawListenerDispatchHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## GlobalRegistry.Input Nullable Misuse')
[void]$sb.AppendLine()
if ($globalRegistryInputNullableHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $globalRegistryInputNullableHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Direct InputManager.Instance Sites')
[void]$sb.AppendLine()
if ($inputManagerInstanceHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $inputManagerInstanceHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Optimization Singleton Residue')
[void]$sb.AppendLine()
if ($optimizationSingletonResidueHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $optimizationSingletonResidueHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member)] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Unauthorized Unity Loop Methods')
[void]$sb.AppendLine()
if ($unauthorizedUnityLoopHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $unauthorizedUnityLoopHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member)] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Legacy Coroutine Sites')
[void]$sb.AppendLine()
if ($legacyCoroutineHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $legacyCoroutineHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Forbidden Runtime Asset API Sites')
[void]$sb.AppendLine()
if ($forbiddenRuntimeAssetApiHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $forbiddenRuntimeAssetApiHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Release-Reachable Direct Hot-Path Debug Log Sites')
[void]$sb.AppendLine()
if ($directHotDebugLogHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $directHotDebugLogHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); DIRECT HOT PATH] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Release-Reachable One-Hop Debug Log Review Sites')
[void]$sb.AppendLine()
if ($hotDebugLogReviewHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $hotDebugLogReviewHits) {
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); HOT-PATH REVIEW: $($entry.HotPathReason)] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Broad Physics Layer Masks Outside Editor')
[void]$sb.AppendLine()
if ($broadPhysicsMaskHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $broadPhysicsMaskHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Runtime Find API Text Hits Outside Editor Folder')
[void]$sb.AppendLine()
if ($runtimeFindHits.Count -eq 0) {
    [void]$sb.AppendLine('- none')
} else {
    foreach ($entry in $runtimeFindHits) {
        $risk = if ($entry.HotPathRisk) { "HOT-PATH REVIEW: $($entry.HotPathReason)" } else { 'cold/unknown review' }
        [void]$sb.AppendLine("- $($entry.Path):$($entry.Line) [$($entry.Member); $risk] - $($entry.Text)")
    }
}

[void]$sb.AppendLine()
[void]$sb.AppendLine('## Failure Policy')
[void]$sb.AppendLine()
[void]$sb.AppendLine('- Exit code 1 when blind registry flag drift, origin-shift listener blind flag drift, synchronous job .Run( sites, raw MemCpy outside guard, legacy PlayerSignalEvents.On* subscriptions, direct raw-array listener dispatch, GlobalRegistry.Input nullable misuse, hot-path direct InputManager.Instance access, optimization singleton residue, unauthorized Unity loops, legacy coroutines, forbidden runtime asset APIs, release-reachable direct hot-path Debug.Log sites, or broad physics layer masks are found.')
[void]$sb.AppendLine('- .Run( is now a hard defect after the first-party runtime inventory reached zero. Use scheduled jobs with dispatcher-owned swap windows, direct bounded kernels, or documented cold async lanes.')
[void]$sb.AppendLine('- .Complete( remains an inventory signal until method ownership proves a hot-path stall or the site is promoted to a stricter owner-specific guard.')
[void]$sb.AppendLine('- One-hop hot-path classification is conservative. A marked site still needs owner-level review before runtime refactor.')
[void]$sb.AppendLine('- DispatcherJobSwap.TryComplete handle.Complete() is classified as guarded when the source-level IsCompleted/swap-window contract is present; this is not Play Mode proof.')
[void]$sb.AppendLine('- UnsafeUtility.MemCpy outside UnsafeMemoryCopyGuard is a hard defect; this script reports it explicitly for CI promotion.')
[void]$sb.AppendLine('- Legacy PlayerSignalEvents.On* subscriptions are stale after listener/NativeQueue migration and must remain at 0.')
[void]$sb.AppendLine('- Direct raw-array listener dispatch is a hard defect. Event lanes must copy the slot to a local listener and null-check before calling On*.')
[void]$sb.AppendLine('- GlobalRegistry.Input is a null-object fallback service. Null checks and null-conditional calls are stale service-contract drift and must remain at 0.')
[void]$sb.AppendLine('- Direct InputManager.Instance access is an inventory signal in cold paths and a hard defect when classified as hot-path. Prefer GlobalRegistry.Input/InputBinding for gameplay consumers; keep bootstrap/native-binding exceptions explicit.')
[void]$sb.AppendLine('- Optimization singleton residue is a hard defect after the registry ownership purge. Optimization/VRAM services must resolve through GlobalRegistry slots only.')
[void]$sb.AppendLine('- Unauthorized Unity loop methods are hard defects outside dispatcher-approved files. Use ITickable/IFixedTickable/ISlowTickable registration instead.')
[void]$sb.AppendLine('- Legacy coroutine sites are hard defects outside Editor. Use dispatcher state machines or Unity 6 Awaitable only in approved cold async lanes.')
[void]$sb.AppendLine('- Resources.Load and Camera.main are hard defects outside Editor. Use Addressables/registered services/cached camera references.')
[void]$sb.AppendLine('- Release-reachable direct hot-path Debug.Log/LogWarning/LogError sites are hard defects. Release-reachable one-hop Debug.Log hits are a review queue until owner-level analysis proves the call runs in gameplay cadence.')
[void]$sb.AppendLine('- Broad physics masks are hard defects. Use owner-specific cached masks such as TerrainLayerMask | VoxelCaveLayerMask; do not raycast against every layer.')
[void]$sb.AppendLine('- Runtime Find API hits are classified by folder/method. Bootstrap cold paths require documentation; gameplay hot paths require replacement.')
[void]$sb.AppendLine('- This scan is source-only. It does not prove Play Mode, GC, frame time, memory retention, or Unity console health.')

$parent = Split-Path -Parent $OutputMarkdown
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

[System.IO.File]::WriteAllText($OutputMarkdown, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "Wrote $OutputMarkdown"

$blockingDefectCount = $blindRegistryHits.Count + $originShiftBlindHits.Count + $jobRunHits.Count + $unsafeMemCpyHits.Count + $legacyPlayerSignalHits.Count + $rawListenerDispatchHits.Count + $globalRegistryInputNullableHits.Count + $hotInputManagerInstanceReviewCount + $optimizationSingletonResidueHits.Count + $unauthorizedUnityLoopHits.Count + $legacyCoroutineHits.Count + $forbiddenRuntimeAssetApiHits.Count + $directHotDebugLogCount + $broadPhysicsMaskHits.Count
if ($blockingDefectCount -gt 0) {
    exit 1
}

exit 0
