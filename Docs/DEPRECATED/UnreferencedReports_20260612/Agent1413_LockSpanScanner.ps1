param(
    [string]$Root = "C:\hades\Hecton8\Assets\_Project\Scripts",
    [string]$Output = "C:\hades\Hecton8\Docs\Reports\LOCK_CONTENTION_SPAN_LEDGER_1413.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-NextNonWhitespace {
    param([string]$Text, [int]$Start)
    $i = $Start
    while ($i -lt $Text.Length -and [char]::IsWhiteSpace($Text[$i])) { $i++ }
    return $i
}

function Find-MatchingBrace {
    param([string]$Text, [int]$OpenIndex)
    if ($OpenIndex -lt 0 -or $OpenIndex -ge $Text.Length -or $Text[$OpenIndex] -ne '{') { return -1 }

    $depth = 0
    $inString = $false
    $inChar = $false
    $inLineComment = $false
    $inBlockComment = $false
    $verbatim = $false

    for ($i = $OpenIndex; $i -lt $Text.Length; $i++) {
        $c = $Text[$i]
        $n = if ($i + 1 -lt $Text.Length) { $Text[$i + 1] } else { [char]0 }

        if ($inLineComment) {
            if ($c -eq "`n") { $inLineComment = $false }
            continue
        }
        if ($inBlockComment) {
            if ($c -eq '*' -and $n -eq '/') { $inBlockComment = $false; $i++ }
            continue
        }
        if ($inString) {
            if ($verbatim) {
                if ($c -eq '"' -and $n -eq '"') { $i++; continue }
                if ($c -eq '"') { $inString = $false; $verbatim = $false }
            } else {
                if ($c -eq '\') { $i++; continue }
                if ($c -eq '"') { $inString = $false }
            }
            continue
        }
        if ($inChar) {
            if ($c -eq '\') { $i++; continue }
            if ($c -eq "'") { $inChar = $false }
            continue
        }

        if ($c -eq '/' -and $n -eq '/') { $inLineComment = $true; $i++; continue }
        if ($c -eq '/' -and $n -eq '*') { $inBlockComment = $true; $i++; continue }
        if ($c -eq '@' -and $n -eq '"') { $inString = $true; $verbatim = $true; $i++; continue }
        if ($c -eq '"') { $inString = $true; continue }
        if ($c -eq "'") { $inChar = $true; continue }
        if ($c -eq '{') { $depth++ }
        elseif ($c -eq '}') {
            $depth--
            if ($depth -eq 0) { return $i }
        }
    }

    return -1
}

function Get-LineStarts {
    param([string]$Text)
    $starts = New-Object System.Collections.Generic.List[int]
    $starts.Add(0)
    for ($i = 0; $i -lt $Text.Length; $i++) {
        if ($Text[$i] -eq "`n" -and $i + 1 -lt $Text.Length) {
            $starts.Add($i + 1)
        }
    }
    return $starts
}

function Get-LineNumberFast {
    param([System.Collections.Generic.List[int]]$LineStarts, [int]$Index)
    $low = 0
    $high = $LineStarts.Count - 1
    while ($low -le $high) {
        $mid = [int](($low + $high) / 2)
        if ($LineStarts[$mid] -le $Index) {
            $low = $mid + 1
        } else {
            $high = $mid - 1
        }
    }
    return $high + 1
}

function Get-MethodSpans {
    param([string]$Text)
    $methodSpans = New-Object System.Collections.Generic.List[object]
    $pattern = '(?m)^\s*(?:public|private|protected|internal|static|sealed|virtual|override|async|unsafe|partial|extern|\s)+\s*[\w<>\[\],\s\?\.]+\s+(?<name>[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?:where\s+[^{]+)?\{'
    $matches = [regex]::Matches($Text, $pattern)
    foreach ($m in $matches) {
        $open = $Text.IndexOf('{', $m.Index)
        if ($open -lt 0) { continue }
        $close = Find-MatchingBrace -Text $Text -OpenIndex $open
        if ($close -lt 0) { continue }
        $methodSpans.Add([pscustomobject]@{
            name = $m.Groups["name"].Value
            start = $m.Index
            end = $close
        })
    }
    return $methodSpans
}

function Get-CurrentMethodFast {
    param([System.Collections.Generic.List[object]]$MethodSpans, [int]$Index)
    for ($i = $MethodSpans.Count - 1; $i -ge 0; $i--) {
        $span = $MethodSpans[$i]
        if ($Index -ge $span.start -and $Index -le $span.end) {
            return $span.name
        }
    }
    return "<unknown>"
}

function Test-InLoop {
    param([string]$Text, [int]$Index)
    $prefixStart = [Math]::Max(0, $Index - 3500)
    $prefix = $Text.Substring($prefixStart, $Index - $prefixStart)
    return [regex]::IsMatch($prefix, '(?s)(for|foreach|while)\s*\([^)]*\)\s*\{(?:(?!\}).)*$')
}

function Get-TryBlockAfterLock {
    param([string]$Text, [int]$Index)
    $searchLength = [Math]::Min(2000, $Text.Length - $Index)
    $window = $Text.Substring($Index, $searchLength)
    $tryMatch = [regex]::Match($window, '\btry\s*\{')
    if (!$tryMatch.Success) { return $null }
    $open = $Index + $tryMatch.Index + $tryMatch.Value.LastIndexOf('{')
    $close = Find-MatchingBrace -Text $Text -OpenIndex $open
    if ($close -lt 0) { return $null }
    $after = $Text.Substring($close, [Math]::Min(1000, $Text.Length - $close))
    $hasFinally = [regex]::IsMatch($after, '^\s*finally\s*\{')
    $release = [regex]::Match($after, 'ReleaseWriteLock|TryUnlockBuffer')
    [pscustomobject]@{
        TryOpen = $open
        TryClose = $close
        HasFinally = $hasFinally
        ReleaseAfterTry = $release.Success
        Body = $Text.Substring($open + 1, $close - $open - 1)
    }
}

function Get-Complexity {
    param([string]$Body)
    [pscustomobject]@{
        LineCount = (($Body -split "`n").Count)
        BranchCount = ([regex]::Matches($Body, '\b(if|switch|case|else)\b')).Count
        LoopCount = ([regex]::Matches($Body, '\b(for|foreach|while)\s*\(')).Count
        ReturnCount = ([regex]::Matches($Body, '\breturn\b')).Count
        NewCount = ([regex]::Matches($Body, '\bnew\s+[A-Za-z_]\w*')).Count
        LinqCount = ([regex]::Matches($Body, '\.(Where|Select|Any|All|First|FirstOrDefault|ToList|Sum|OrderBy)\s*\(')).Count
        MathCallCount = ([regex]::Matches($Body, '\b(math|Mathf|Math)\s*\.')).Count
        AssignmentCount = ([regex]::Matches($Body, '(?<![=!<>])=(?!=)')).Count
        NestedLockCount = ([regex]::Matches($Body, 'TryAcquireWriteLock|TryLockBuffer')).Count
        StringInterpolationCount = ([regex]::Matches($Body, '\$"')).Count
    }
}

$started = Get-Date
$records = New-Object System.Collections.Generic.List[object]
$files = Get-ChildItem -Path $Root -Recurse -Filter "*.cs" -File

foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $matches = [regex]::Matches($text, 'TryAcquireWriteLock|TryLockBuffer')
    if ($matches.Count -eq 0) { continue }
    $lineStarts = Get-LineStarts -Text $text
    $methodSpans = Get-MethodSpans -Text $text
    foreach ($match in $matches) {
        $tryBlock = Get-TryBlockAfterLock -Text $text -Index $match.Index
        $body = if ($null -eq $tryBlock) { "" } else { $tryBlock.Body }
        $complexity = Get-Complexity -Body $body
        $line = Get-LineNumberFast -LineStarts $lineStarts -Index $match.Index
        $method = Get-CurrentMethodFast -MethodSpans $methodSpans -Index $match.Index
        $lineStart = $text.LastIndexOf("`n", [Math]::Max(0, $match.Index - 1)) + 1
        $lineEnd = $text.IndexOf("`n", $match.Index)
        if ($lineEnd -lt 0) { $lineEnd = $text.Length }
        $lockLine = $text.Substring($lineStart, $lineEnd - $lineStart).Trim()
        $ifWindowStart = [Math]::Max(0, $match.Index - 120)
        $ifWindow = $text.Substring($ifWindowStart, [Math]::Min(260, $text.Length - $ifWindowStart))
        $failClosedGuard = [regex]::IsMatch($ifWindow, 'if\s*\([^{;]*!\s*[^)]*(TryAcquireWriteLock|TryLockBuffer)')

        $records.Add([pscustomobject]@{
            file = $file.FullName.Replace("C:\hades\Hecton8\", "")
            method = $method
            api = $match.Value
            line = $line
            lockLine = $lockLine
            insideLoop = (Test-InLoop -Text $text -Index $match.Index)
            failClosedGuardShape = $failClosedGuard
            hasTryAfterLock = ($null -ne $tryBlock)
            releaseInFinallyShape = ($null -ne $tryBlock -and $tryBlock.HasFinally -and $tryBlock.ReleaseAfterTry)
            tryLine = if ($null -eq $tryBlock) { 0 } else { Get-LineNumberFast -LineStarts $lineStarts -Index $tryBlock.TryOpen }
            tryBodyLines = $complexity.LineCount
            complexity = $complexity
            priorityScore = (
                ($complexity.LineCount * 2) +
                ($complexity.BranchCount * 4) +
                ($complexity.LoopCount * 20) +
                ($complexity.MathCallCount * 6) +
                ($complexity.NewCount * 10) +
                ($complexity.NestedLockCount * 30) +
                ($(if ($tryBlock -eq $null -or !$tryBlock.HasFinally) { 50 } else { 0 })) +
                ($(if (Test-InLoop -Text $text -Index $match.Index) { 40 } else { 0 }))
            )
        })
    }
}

$elapsedUs = [int64]((Get-Date) - $started).TotalMilliseconds * 1000
$summary = [pscustomobject]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    root = $Root
    fileCount = $files.Count
    lockInvocationCount = $records.Count
    lockAcquireCount = ($records | Where-Object { $_.api -eq "TryAcquireWriteLock" }).Count
    tryLockBufferCount = ($records | Where-Object { $_.api -eq "TryLockBuffer" }).Count
    missingFinallyShapeCount = ($records | Where-Object { -not $_.releaseInFinallyShape }).Count
    insideLoopCount = ($records | Where-Object { $_.insideLoop }).Count
    nestedLockCount = ($records | Where-Object { $_.complexity.NestedLockCount -gt 0 }).Count
    scanMicroseconds = $elapsedUs
}

$payload = [pscustomobject]@{
    summary = $summary
    records = @($records | Sort-Object -Property priorityScore -Descending)
}

$json = $payload | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($Output, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output $Output
