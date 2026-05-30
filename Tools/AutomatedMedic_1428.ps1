param(
    [ValidateSet("Audit", "Build", "TailEditorLog", "SelfTest", "All")]
    [string]$Mode = "Audit",
    [string]$SolutionPath = "Hecton8.slnx",
    [string]$LogPath = "Docs/AgentLogs/AutomatedMedic_1428.log",
    [int]$MaxCpuLoadPercent = 50,
    [int]$ThrottleDelaySeconds = 15,
    [int]$TailSeconds = 30,
    [switch]$ApplyRepairs
)

$ErrorActionPreference = "Stop"

$script:Root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$script:LogFullPath = Join-Path $script:Root $LogPath
$script:BuildStampPath = Join-Path $script:Root ".codex_tmp\AutomatedMedic_1428.lastbuild"

function Ensure-ParentDirectory {
    param([string]$Path)

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
}

function Write-MedicLog {
    param([string]$Message)

    Ensure-ParentDirectory -Path $script:LogFullPath
    $line = (Get-Date).ToString("o") + " | " + $Message + [Environment]::NewLine
    [System.IO.File]::AppendAllText($script:LogFullPath, $line, [System.Text.Encoding]::UTF8)
    Write-Output $Message
}

function New-ObjectList {
    return ,(New-Object "System.Collections.Generic.List[object]")
}

function Get-CpuLoadOrClosed {
    try {
        $sample = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
        if ($null -eq $sample) {
            return 100.0
        }

        return [double]$sample
    } catch {
        return 100.0
    }
}

function Get-CompilerProcessSnapshot {
    @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @("dotnet.exe", "csc.exe", "VBCSCompiler.exe")
    } | ForEach-Object {
        [pscustomobject]@{
            Name = $_.Name
            ProcessId = $_.ProcessId
            WorkingSetMb = [math]::Round($_.WorkingSetSize / 1MB, 2)
            CommandLine = $_.CommandLine
        }
    })
}

function Get-UnityProcessSnapshot {
    @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @("Unity.exe", "Unity Hub.exe")
    } | ForEach-Object {
        [pscustomobject]@{
            Name = $_.Name
            ProcessId = $_.ProcessId
            WorkingSetMb = [math]::Round($_.WorkingSetSize / 1MB, 2)
            CommandLine = $_.CommandLine
        }
    })
}

function Test-HostReadyForBuild {
    param([int]$MaxCpu)

    $cpu = Get-CpuLoadOrClosed
    $compilers = @(Get-CompilerProcessSnapshot)
    if ($cpu -gt $MaxCpu -or $compilers.Count -gt 0) {
        Write-MedicLog ("THROTTLED_BY_HOST cpu=" + $cpu + " compilerProcessCount=" + $compilers.Count)
        foreach ($process in $compilers) {
            Write-MedicLog ("COMPILER_PROCESS pid=" + $process.ProcessId + " name=" + $process.Name + " mb=" + $process.WorkingSetMb)
        }

        return $false
    }

    Write-MedicLog ("HOST_READY cpu=" + $cpu + " compilerProcessCount=0")
    return $true
}

function Wait-BuildThrottleWindow {
    param([int]$DelaySeconds)

    Ensure-ParentDirectory -Path $script:BuildStampPath
    if (Test-Path -LiteralPath $script:BuildStampPath) {
        $stampText = [System.IO.File]::ReadAllText($script:BuildStampPath).Trim()
        $lastTicks = 0L
        if ([long]::TryParse($stampText, [ref]$lastTicks)) {
            $last = [DateTime]::new($lastTicks, [DateTimeKind]::Utc)
            $elapsed = [DateTime]::UtcNow - $last
            $remaining = $DelaySeconds - [int][math]::Floor($elapsed.TotalSeconds)
            if ($remaining -gt 0) {
                Write-MedicLog ("BUILD_THROTTLE_SLEEP seconds=" + $remaining)
                Start-Sleep -Seconds $remaining
            }
        }
    }

    [System.IO.File]::WriteAllText($script:BuildStampPath, [DateTime]::UtcNow.Ticks.ToString(), [System.Text.Encoding]::UTF8)
}

function Parse-CompilerDiagnosticsFromText {
    param([string]$Text)

    $items = New-ObjectList
    $pattern = "^(?<file>[A-Za-z]:\\.*?\.cs)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<code>CS\d{4}): (?<message>.*?) \[(?<project>.*?)\]$"
    $reader = New-Object System.IO.StringReader($Text)
    try {
        while ($null -ne ($lineText = $reader.ReadLine())) {
            $match = [regex]::Match($lineText, $pattern)
            if (-not $match.Success) {
                continue
            }

            $items.Add([pscustomobject]@{
                File = $match.Groups["file"].Value
                Line = [int]$match.Groups["line"].Value
                Column = [int]$match.Groups["column"].Value
                Severity = $match.Groups["severity"].Value
                Code = $match.Groups["code"].Value
                Message = $match.Groups["message"].Value
                Project = $match.Groups["project"].Value
            })
        }
    } finally {
        $reader.Dispose()
    }

    return ,$items
}

function Parse-BuildSummaryFromText {
    param([string]$Text)

    $warnings = $null
    $errors = $null
    $reader = New-Object System.IO.StringReader($Text)
    try {
        while ($null -ne ($lineText = $reader.ReadLine())) {
            $warningMatch = [regex]::Match($lineText, "^\s*(\d+)\s+Warning\(s\)")
            if ($warningMatch.Success) {
                $warnings = [int]$warningMatch.Groups[1].Value
            }

            $errorMatch = [regex]::Match($lineText, "^\s*(\d+)\s+Error\(s\)")
            if ($errorMatch.Success) {
                $errors = [int]$errorMatch.Groups[1].Value
            }
        }
    } finally {
        $reader.Dispose()
    }

    [pscustomobject]@{
        Warnings = $warnings
        Errors = $errors
    }
}

function Read-TextFileShared {
    param([string]$Path)

    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
        try {
            return $reader.ReadToEnd()
        } finally {
            $reader.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Resolve-UnityEditorPath {
    $projectVersionPath = Join-Path $script:Root "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $projectVersionPath)) {
        Write-MedicLog "UNITY_VERSION_MISSING ProjectSettings/ProjectVersion.txt not found"
        return $null
    }

    $versionLine = Select-String -LiteralPath $projectVersionPath -Pattern "^m_EditorVersion:" | Select-Object -First 1
    if ($null -eq $versionLine) {
        Write-MedicLog "UNITY_VERSION_MISSING m_EditorVersion not found"
        return $null
    }

    $version = $versionLine.Line.Split(":", 2)[1].Trim()
    $candidate = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (Test-Path -LiteralPath $candidate) {
        Write-MedicLog ("UNITY_EDITOR_FOUND version=" + $version + " path=" + $candidate)
        return $candidate
    }

    Write-MedicLog ("UNITY_EDITOR_NOT_FOUND version=" + $version + " expected=" + $candidate)
    return $null
}

function Get-EditorLogPath {
    if ($IsWindows -or $env:LOCALAPPDATA) {
        return (Join-Path $env:LOCALAPPDATA "Unity\Editor\Editor.log")
    }

    return (Join-Path $HOME "Library/Logs/Unity/Editor.log")
}

function Invoke-ReportInquisition {
    Write-MedicLog "TASK01_REPORT_INQUISITION_BEGIN"
    $reportDir = Join-Path $script:Root "Docs\Reports"
    if (-not (Test-Path -LiteralPath $reportDir)) {
        Write-MedicLog "TASK01_REPORTS_MISSING"
        return
    }

    $logs = @(Get-ChildItem -LiteralPath $reportDir -File | Where-Object {
        $_.Extension -in @(".log", ".md", ".json")
    } | Sort-Object LastWriteTime -Descending | Select-Object -First 80)

    $totalErrors = 0
    $unique = @{}
    foreach ($log in $logs) {
        $text = Read-TextFileShared -Path $log.FullName
        $diagnostics = @(Parse-CompilerDiagnosticsFromText -Text $text)
        $errors = @($diagnostics | Where-Object { $_.Severity -eq "error" })
        $warnings = @($diagnostics | Where-Object { $_.Severity -eq "warning" })
        if ($errors.Count -gt 0 -or $warnings.Count -gt 0) {
            Write-MedicLog ("REPORT_DIAGNOSTICS file=" + $log.Name + " errors=" + $errors.Count + " warnings=" + $warnings.Count)
        }

        foreach ($diag in $diagnostics) {
            $key = $diag.File + "|" + $diag.Line + "|" + $diag.Code + "|" + $diag.Message
            if (-not $unique.ContainsKey($key)) {
                $unique[$key] = $diag
            }
        }

        $totalErrors += $errors.Count
    }

    Write-MedicLog ("TASK01_REPORT_INQUISITION_END scanned=" + $logs.Count + " rawErrors=" + $totalErrors + " uniqueDiagnostics=" + $unique.Count)
}

function Invoke-EnvironmentDiscovery {
    Write-MedicLog "TASK02_ENVIRONMENT_DISCOVERY_BEGIN"
    foreach ($process in @(Get-UnityProcessSnapshot)) {
        Write-MedicLog ("UNITY_PROCESS pid=" + $process.ProcessId + " name=" + $process.Name + " mb=" + $process.WorkingSetMb + " cmd=" + $process.CommandLine)
    }

    foreach ($process in @(Get-CompilerProcessSnapshot)) {
        Write-MedicLog ("COMPILER_PROCESS pid=" + $process.ProcessId + " name=" + $process.Name + " mb=" + $process.WorkingSetMb + " cmd=" + $process.CommandLine)
    }

    [void](Resolve-UnityEditorPath)
    Write-MedicLog "TASK02_ENVIRONMENT_DISCOVERY_END"
}

function Invoke-EditorLogResolution {
    Write-MedicLog "TASK03_EDITOR_LOG_RESOLUTION_BEGIN"
    $path = Get-EditorLogPath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-MedicLog ("EDITOR_LOG_MISSING path=" + $path)
        return
    }

    $item = Get-Item -LiteralPath $path
    Write-MedicLog ("EDITOR_LOG_FOUND path=" + $item.FullName + " bytes=" + $item.Length + " lastWrite=" + $item.LastWriteTime.ToString("o"))
    $text = Read-TextFileShared -Path $item.FullName
    $matches = [regex]::Matches($text, "error CS\d{4}|warning CS\d{4}|Missing Script|script class cannot be found|Shader error|Exception|Domain Reload")
    Write-MedicLog ("EDITOR_LOG_FORENSIC_MATCHES count=" + $matches.Count)
    Write-MedicLog "TASK03_EDITOR_LOG_RESOLUTION_END"
}

function Invoke-PackageBoundaryAudit {
    Write-MedicLog "TASK04_PACKAGE_BOUNDARY_AUDIT_BEGIN"
    $manifestPath = Join-Path $script:Root "Packages\manifest.json"
    $lockPath = Join-Path $script:Root "Packages\packages-lock.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        Write-MedicLog "PACKAGE_MANIFEST_MISSING"
        return
    }

    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    $dependencyNames = @($manifest.dependencies.PSObject.Properties.Name | Sort-Object)
    Write-MedicLog ("PACKAGE_MANIFEST_DEPENDENCIES count=" + $dependencyNames.Count)
    foreach ($name in $dependencyNames) {
        $value = $manifest.dependencies.$name
        Write-MedicLog ("PACKAGE_DEP name=" + $name + " value=" + $value)
    }

    if (Test-Path -LiteralPath $lockPath) {
        $lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json
        foreach ($prop in @($lock.dependencies.PSObject.Properties | Sort-Object Name)) {
            $info = $prop.Value
            if ($info.source -in @("embedded", "git", "registry", "builtin")) {
                Write-MedicLog ("PACKAGE_LOCK name=" + $prop.Name + " source=" + $info.source + " version=" + $info.version)
            }
        }
    }

    Write-MedicLog "TASK04_PACKAGE_BOUNDARY_AUDIT_END"
}

function Invoke-SerializationStateCheck {
    Write-MedicLog "TASK05_SERIALIZATION_STATE_CHECK_BEGIN"
    $path = Get-EditorLogPath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-MedicLog "SERIALIZATION_EDITOR_LOG_MISSING"
        return
    }

    $text = Read-TextFileShared -Path $path
    $patterns = @("Missing Script", "script class cannot be found", "SerializedObject", "serialization", "The referenced script")
    foreach ($pattern in $patterns) {
        $count = ([regex]::Matches($text, [regex]::Escape($pattern), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
        if ($count -gt 0) {
            Write-MedicLog ("SERIALIZATION_MATCH pattern=" + $pattern + " count=" + $count)
        }
    }

    Write-MedicLog "TASK05_SERIALIZATION_STATE_CHECK_END"
}

function Watch-EditorLog {
    param([int]$Seconds)

    $path = Get-EditorLogPath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-MedicLog ("EDITOR_LOG_TAIL_MISSING path=" + $path)
        return
    }

    Write-MedicLog ("EDITOR_LOG_TAIL_BEGIN seconds=" + $Seconds + " path=" + $path)
    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        [void]$stream.Seek(0, [System.IO.SeekOrigin]::End)
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
            while ([DateTime]::UtcNow -lt $deadline) {
                $line = $reader.ReadLine()
                if ($null -eq $line) {
                    Start-Sleep -Milliseconds 250
                    continue
                }

                if ($line -match "\[Compiler\]|error CS\d{4}|warning CS\d{4}|Shader error|Exception|Domain Reload") {
                    Write-MedicLog ("EDITOR_LOG_EVENT " + $line)
                }
            }
        } finally {
            $reader.Dispose()
        }
    } finally {
        $stream.Dispose()
    }

    Write-MedicLog "EDITOR_LOG_TAIL_END"
}

function Get-CandidateCSharpSourceFiles {
    param([string]$Pattern)

    $scriptRoot = Join-Path $script:Root "Assets\_Project\Scripts"
    $rg = Get-Command rg -ErrorAction SilentlyContinue
    if ($null -ne $rg) {
        $paths = @(& rg -l $Pattern $scriptRoot --glob "*.cs" --glob "!**/Editor/**" --glob "!**/Tests/**" 2>$null)
        return @($paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object {
            Get-Item -LiteralPath $_
        })
    }

    return @(Get-ChildItem -LiteralPath $scriptRoot -Recurse -File -Filter "*.cs" -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -notmatch "\\Editor\\" -and $_.FullName -notmatch "\\Tests\\"
    })
}

function Invoke-HotPathStaticAudit {
    Write-MedicLog "HOTPATH_STATIC_AUDIT_BEGIN"
    $hotMethodPattern = "^\s*(public|private|protected|internal|static|sealed|override|virtual|async|\s)+[\w<>\[\],\s]+\s+(Tick|FixedTick|LateFrameTick|Update|LateUpdate|FixedUpdate|Execute|OnUpdate)\s*\("
    $badCallPattern = "GlobalRegistry\.Get<|GlobalRegistry\.[A-Z]\w+|GetComponent(s|InChildren|InParent)?<|FindObjectOfType<|GameObject\.Find|Camera\.main"
    $violationCount = 0

    $candidatePattern = "GlobalRegistry\.|GetComponent|FindObjectOfType|GameObject\.Find|Camera\.main"
    foreach ($file in Get-CandidateCSharpSourceFiles -Pattern $candidatePattern) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        $inHotMethod = $false
        $methodName = ""
        $braceDepth = 0
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            if (-not $inHotMethod) {
                $match = [regex]::Match($line, $hotMethodPattern)
                if ($match.Success) {
                    $inHotMethod = $true
                    $methodName = $match.Groups[2].Value
                    $braceDepth = 0
                }
            }

            if ($inHotMethod) {
                if ($line -match $badCallPattern) {
                    $relative = $file.FullName.Substring($script:Root.Length).TrimStart("\", "/")
                    Write-MedicLog ("HOTPATH_DEPENDENCY_RISK file=" + $relative + " line=" + ($i + 1) + " method=" + $methodName + " text=" + $line.Trim())
                    $violationCount++
                }

                $openCount = ([regex]::Matches($line, "\{")).Count
                $closeCount = ([regex]::Matches($line, "\}")).Count
                $braceDepth += $openCount - $closeCount
                if ($braceDepth -le 0 -and ($openCount -gt 0 -or $closeCount -gt 0)) {
                    $inHotMethod = $false
                    $methodName = ""
                }
            }
        }
    }

    Write-MedicLog ("HOTPATH_STATIC_AUDIT_END violations=" + $violationCount)
}

function Invoke-DataVaultLockAudit {
    Write-MedicLog "DATAVAULT_LOCK_AUDIT_BEGIN"
    $lockPattern = "AcquireWriteLock|TryAcquireWriteLock|EnterWriteLock|ReleaseWriteLock"
    $riskCount = 0
    foreach ($file in Get-CandidateCSharpSourceFiles -Pattern $lockPattern) {
        $lines = [System.IO.File]::ReadAllLines($file.FullName)
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            if ($line -notmatch $lockPattern) {
                continue
            }

            $windowStart = [math]::Max(0, $i - 8)
            $windowEnd = [math]::Min($lines.Length - 1, $i + 24)
            $window = [string]::Join([Environment]::NewLine, $lines[$windowStart..$windowEnd])
            $hasAcquire = $line -match "AcquireWriteLock|TryAcquireWriteLock|EnterWriteLock"
            $hasFinally = $window -match "\bfinally\b"
            $relative = $file.FullName.Substring($script:Root.Length).TrimStart("\", "/")

            if ($hasAcquire -and -not $hasFinally) {
                Write-MedicLog ("DATAVAULT_LOCK_RISK file=" + $relative + " line=" + ($i + 1) + " reason=missing_nearby_finally text=" + $line.Trim())
                $riskCount++
            } elseif ($hasAcquire) {
                Write-MedicLog ("DATAVAULT_LOCK_OK file=" + $relative + " line=" + ($i + 1) + " text=" + $line.Trim())
            }
        }
    }

    Write-MedicLog ("DATAVAULT_LOCK_AUDIT_END risks=" + $riskCount)
}

function Invoke-GuardedBuild {
    if (-not (Test-HostReadyForBuild -MaxCpu $MaxCpuLoadPercent)) {
        return 3
    }

    Wait-BuildThrottleWindow -DelaySeconds $ThrottleDelaySeconds

    $solutionFullPath = Join-Path $script:Root $SolutionPath
    if (-not (Test-Path -LiteralPath $solutionFullPath)) {
        Write-MedicLog ("BUILD_MISSING_SOLUTION path=" + $solutionFullPath)
        return 2
    }

    $stdoutPath = $script:LogFullPath + ".build.stdout.tmp"
    $stderrPath = $script:LogFullPath + ".build.stderr.tmp"
    Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue

    $arguments = @(
        "build",
        "`"$solutionFullPath`"",
        "--no-restore",
        "-v:minimal",
        "/m:1",
        "/p:UseSharedCompilation=false"
    )

    Write-MedicLog ("BUILD_BEGIN args=" + ($arguments -join " "))
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $process = Start-Process -FilePath "dotnet" -ArgumentList $arguments -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    } catch {
        $stopwatch.Stop()
        Write-MedicLog ("BUILD_LAUNCH_FAILED message=" + $_.Exception.Message)
        return 5
    }
    $stopwatch.Stop()

    $stdout = ""
    $stderr = ""
    if (Test-Path -LiteralPath $stdoutPath) {
        $stdout = [System.IO.File]::ReadAllText($stdoutPath)
    }
    if (Test-Path -LiteralPath $stderrPath) {
        $stderr = [System.IO.File]::ReadAllText($stderrPath)
    }
    Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue

    $combined = $stdout + [Environment]::NewLine + $stderr
    $diagnostics = @(Parse-CompilerDiagnosticsFromText -Text $combined)
    $summary = Parse-BuildSummaryFromText -Text $combined
    Write-MedicLog ("BUILD_END exit=" + $process.ExitCode + " ms=" + $stopwatch.ElapsedMilliseconds + " diagnostics=" + $diagnostics.Count + " warnings=" + $summary.Warnings + " errors=" + $summary.Errors)
    foreach ($diag in $diagnostics) {
        Write-MedicLog ("BUILD_DIAGNOSTIC " + $diag.Severity + " " + $diag.Code + " " + $diag.File + ":" + $diag.Line + ":" + $diag.Column + " " + $diag.Message)
    }

    return $process.ExitCode
}

function Invoke-SelfTest {
    Write-MedicLog "SELFTEST_BEGIN"
    if (-not (Test-HostReadyForBuild -MaxCpu $MaxCpuLoadPercent)) {
        return 3
    }

    $workDir = Join-Path $script:Root "Temp\AutomatedMedic_1428SelfTest"
    $sourcePath = Join-Path $workDir "MedicSelfTest.cs"
    $projectPath = Join-Path $workDir "MedicSelfTest.csproj"
    New-Item -ItemType Directory -Path $workDir -Force | Out-Null
    [System.IO.File]::WriteAllText($sourcePath, "public static class MedicSelfTest { public static int Run() { return 42 } }", [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($projectPath, "<Project Sdk=""Microsoft.NET.Sdk""><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include=""MedicSelfTest.cs"" /></ItemGroup></Project>", [System.Text.Encoding]::UTF8)

    Wait-BuildThrottleWindow -DelaySeconds $ThrottleDelaySeconds
    $brokenOutput = & dotnet build $projectPath --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false 2>&1 | Out-String
    $brokenDiagnostics = @(Parse-CompilerDiagnosticsFromText -Text $brokenOutput)
    Write-MedicLog ("SELFTEST_BROKEN_DIAGNOSTICS count=" + $brokenDiagnostics.Count)
    if ($brokenDiagnostics.Count -lt 1) {
        Write-MedicLog "SELFTEST_FAILED parser did not detect injected syntax error"
        return 10
    }

    [System.IO.File]::WriteAllText($sourcePath, "public static class MedicSelfTest { public static int Run() { return 42; } }", [System.Text.Encoding]::UTF8)
    Wait-BuildThrottleWindow -DelaySeconds $ThrottleDelaySeconds
    $fixedOutput = & dotnet build $projectPath --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false 2>&1 | Out-String
    $fixedSummary = Parse-BuildSummaryFromText -Text $fixedOutput
    Write-MedicLog ("SELFTEST_FIXED_SUMMARY warnings=" + $fixedSummary.Warnings + " errors=" + $fixedSummary.Errors)
    Write-MedicLog "SELFTEST_END"
    return 0
}

function Invoke-Audit {
    Invoke-ReportInquisition
    Invoke-EnvironmentDiscovery
    Invoke-EditorLogResolution
    Invoke-PackageBoundaryAudit
    Invoke-SerializationStateCheck
    Invoke-HotPathStaticAudit
    Invoke-DataVaultLockAudit
}

Ensure-ParentDirectory -Path $script:LogFullPath
if (-not (Test-Path -LiteralPath $script:LogFullPath)) {
    [System.IO.File]::WriteAllText($script:LogFullPath, "", [System.Text.Encoding]::UTF8)
}

$exitCode = 0
switch ($Mode) {
    "Audit" { Invoke-Audit }
    "Build" { $exitCode = Invoke-GuardedBuild }
    "TailEditorLog" { Watch-EditorLog -Seconds $TailSeconds }
    "SelfTest" { $exitCode = Invoke-SelfTest }
    "All" {
        Invoke-Audit
        $exitCode = Invoke-GuardedBuild
        if ($exitCode -eq 0) {
            Watch-EditorLog -Seconds $TailSeconds
        }
    }
}

exit $exitCode
