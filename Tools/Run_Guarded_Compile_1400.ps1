param(
    [string]$SolutionPath = "Hecton8.slnx",
    [string]$LogPath = "Docs/AgentLogs/Build_1400_Output.log",
    [string]$JsonPath = "Docs/AgentLogs/Build_1400_Output.json",
    [int]$MaxCpuLoadPercent = 50
)

$ErrorActionPreference = "Stop"

function New-Result {
    param(
        [string]$Status,
        [double]$CpuLoadPercent,
        [object[]]$CompilerProcesses,
        [int]$ExitCode,
        [long]$DurationMilliseconds,
        [Nullable[int]]$Warnings,
        [Nullable[int]]$Errors,
        [string]$Message
    )

    [pscustomobject]@{
        generatedAt = (Get-Date).ToString("o")
        status = $Status
        solutionPath = $SolutionPath
        logPath = $LogPath
        cpuLoadPercent = $CpuLoadPercent
        compilerProcesses = $CompilerProcesses
        exitCode = $ExitCode
        durationMilliseconds = $DurationMilliseconds
        warnings = $Warnings
        errors = $Errors
        message = $Message
    }
}

function Get-CpuLoadOrClosed {
    try {
        $sample = (Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
        if ($null -eq $sample) {
            return 100
        }

        return [double]$sample
    } catch {
        return 100
    }
}

function Get-CompilerProcessSnapshot {
    @(Get-Process -Name dotnet,csc,VBCSCompiler -ErrorAction SilentlyContinue | ForEach-Object {
        $startTime = $null
        try {
            if ($null -ne $_.StartTime) {
                $startTime = $_.StartTime.ToString("o")
            }
        } catch {
            $startTime = "UNAVAILABLE"
        }

        [pscustomobject]@{
            Id = $_.Id
            ProcessName = $_.ProcessName
            CPU = $_.CPU
            StartTime = $startTime
        }
    })
}

function Copy-FileToStream {
    param(
        [string]$SourcePath,
        [System.IO.Stream]$Destination
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    $source = [System.IO.File]::OpenRead($SourcePath)
    try {
        $source.CopyTo($Destination)
    } finally {
        $source.Dispose()
    }
}

function Read-BuildSummary {
    param(
        [string]$Path
    )

    $warnings = $null
    $errors = $null
    $reader = [System.IO.StreamReader]::new($Path)
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            $warningMatch = [regex]::Match($line, "^\s*(\d+)\s+Warning\(s\)")
            if ($warningMatch.Success) {
                $warnings = [int]$warningMatch.Groups[1].Value
            }

            $errorMatch = [regex]::Match($line, "^\s*(\d+)\s+Error\(s\)")
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

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$solutionFullPath = Join-Path $root $SolutionPath
$logFullPath = Join-Path $root $LogPath
$jsonFullPath = Join-Path $root $JsonPath
$logDirectory = Split-Path -Parent $logFullPath
$jsonDirectory = Split-Path -Parent $jsonFullPath

if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $jsonDirectory)) {
    New-Item -ItemType Directory -Path $jsonDirectory -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $solutionFullPath)) {
    $result = New-Result -Status "MISSING_SOLUTION" -CpuLoadPercent 0 -CompilerProcesses @() -ExitCode -1 -DurationMilliseconds 0 -Warnings $null -Errors $null -Message "Solution path does not exist."
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8
    exit 2
}

$cpuLoad = Get-CpuLoadOrClosed
$compilerProcesses = @(Get-CompilerProcessSnapshot)

if ($cpuLoad -gt $MaxCpuLoadPercent -or $compilerProcesses.Count -gt 0) {
    $message = "BLOCKED_BY_CONTENTION: cpu=" + $cpuLoad + " compilerProcessCount=" + $compilerProcesses.Count
    $result = New-Result -Status "BLOCKED_BY_CONTENTION" -CpuLoadPercent $cpuLoad -CompilerProcesses $compilerProcesses -ExitCode -1 -DurationMilliseconds 0 -Warnings $null -Errors $null -Message $message
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8
    Set-Content -LiteralPath $logFullPath -Value $message -Encoding UTF8
    exit 3
}

$stdoutPath = $logFullPath + ".stdout.tmp"
$stderrPath = $logFullPath + ".stderr.tmp"
Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "build",
    "`"$solutionFullPath`"",
    "-nologo",
    "-clp:Summary",
    "-maxcpucount:1",
    "/p:UseSharedCompilation=false",
    "/p:HectonStrictWarningAudit=true"
)

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $process = Start-Process -FilePath "dotnet" -ArgumentList $arguments -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
} catch {
    $stopwatch.Stop()
    $message = "DOTNET_BUILD_LAUNCH_FAILED: " + $_.Exception.Message
    Set-Content -LiteralPath $logFullPath -Value $message -Encoding UTF8
    $result = New-Result -Status "DOTNET_BUILD_LAUNCH_FAILED" -CpuLoadPercent $cpuLoad -CompilerProcesses @() -ExitCode -1 -DurationMilliseconds $stopwatch.ElapsedMilliseconds -Warnings $null -Errors $null -Message $message
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8
    exit 5
}
$stopwatch.Stop()

$destination = [System.IO.File]::Create($logFullPath)
try {
    Copy-FileToStream -SourcePath $stdoutPath -Destination $destination
    if ((Test-Path -LiteralPath $stderrPath) -and ((Get-Item -LiteralPath $stderrPath).Length -gt 0)) {
        $newlineBytes = [System.Text.Encoding]::UTF8.GetBytes([Environment]::NewLine)
        $destination.Write($newlineBytes, 0, $newlineBytes.Length)
        Copy-FileToStream -SourcePath $stderrPath -Destination $destination
    }
} finally {
    $destination.Dispose()
}

Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue

$summary = Read-BuildSummary -Path $logFullPath
$warnings = $summary.Warnings
$errors = $summary.Errors

$status = "PARSE_FAILED"
if ($null -ne $warnings -and $null -ne $errors) {
    if ($process.ExitCode -eq 0 -and $warnings -eq 0 -and $errors -eq 0) {
        $status = "GREEN_ZERO_WARNING_ZERO_ERROR"
    } elseif ($process.ExitCode -eq 0) {
        $status = "COMPILED_WITH_WARNINGS_OR_PARSED_ERRORS"
    } else {
        $status = "FAILED_WITH_PARSED_SUMMARY"
    }
}

$result = New-Result -Status $status -CpuLoadPercent $cpuLoad -CompilerProcesses @() -ExitCode $process.ExitCode -DurationMilliseconds $stopwatch.ElapsedMilliseconds -Warnings $warnings -Errors $errors -Message "dotnet build guarded compile completed."
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8

if ($process.ExitCode -ne 0) {
    exit $process.ExitCode
}

if ($status -ne "GREEN_ZERO_WARNING_ZERO_ERROR") {
    exit 4
}

exit 0
