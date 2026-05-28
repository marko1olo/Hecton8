param(
    [string]$JsonPath = "Docs/AgentLogs/MsbuildGraphFuzzer_1400.json",
    [string]$LogPath = "Docs/AgentLogs/MsbuildGraphFuzzer_1400.log",
    [int]$MaxCpuLoadPercent = 50
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$jsonFullPath = Join-Path $root $JsonPath
$logFullPath = Join-Path $root $LogPath
$jsonDirectory = Split-Path -Parent $jsonFullPath
$logDirectory = Split-Path -Parent $logFullPath
if (-not (Test-Path -LiteralPath $jsonDirectory)) {
    New-Item -ItemType Directory -Path $jsonDirectory -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

function Write-FuzzerResult {
    param(
        [string]$Status,
        [double]$CpuLoadPercent,
        [object[]]$CompilerProcesses,
        [int]$ExitCode,
        [long]$DurationMilliseconds,
        [string]$Message,
        [string]$ProjectPath,
        [string]$ReportPath,
        [string]$LogPath
    )

    [pscustomobject]@{
        generatedAt = (Get-Date).ToString("o")
        status = $Status
        cpuLoadPercent = $CpuLoadPercent
        compilerProcesses = $CompilerProcesses
        exitCode = $ExitCode
        durationMilliseconds = $DurationMilliseconds
        message = $Message
        projectPath = $ProjectPath
        reportPath = $ReportPath
        logPath = $LogPath
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonFullPath -Encoding UTF8
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

$cpuLoad = Get-CpuLoadOrClosed
$compilerProcesses = @(Get-CompilerProcessSnapshot)

if ($cpuLoad -gt $MaxCpuLoadPercent -or $compilerProcesses.Count -gt 0) {
    $message = "BLOCKED_BY_CONTENTION: cpu=" + $cpuLoad + " compilerProcessCount=" + $compilerProcesses.Count
    Set-Content -LiteralPath $logFullPath -Value $message -Encoding UTF8
    Write-FuzzerResult -Status "BLOCKED_BY_CONTENTION" -CpuLoadPercent $cpuLoad -CompilerProcesses $compilerProcesses -ExitCode -1 -DurationMilliseconds 0 -Message $message -ProjectPath "" -ReportPath "" -LogPath $LogPath
    exit 3
}

$workDir = Join-Path $root "Temp\Agent1400GraphFuzzer"
$objDir = Join-Path $workDir "obj"
New-Item -ItemType Directory -Path $workDir,$objDir -Force | Out-Null
$projectPath = Join-Path $workDir "Agent1400.CircularReferenceFuzzer.csproj"
$reportPath = Join-Path $objDir "fuzzer_refs.txt"
$shaderGraphProject = [System.Security.SecurityElement]::Escape((Join-Path $root "Unity.ShaderGraph.Editor.csproj"))
$coreEditorProject = [System.Security.SecurityElement]::Escape((Join-Path $root "Unity.RenderPipelines.Core.Editor.csproj"))

$projectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <BaseIntermediateOutputPath>obj\</BaseIntermediateOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$shaderGraphProject" Private="false" />
    <ProjectReference Include="$coreEditorProject" Private="false" />
  </ItemGroup>
  <Target Name="Hecton1400ReportReferences" DependsOnTargets="FixUnityCircularReferences">
    <WriteLinesToFile File="$(BaseIntermediateOutputPath)fuzzer_refs.txt"
                      Lines="ProjectReference=@(ProjectReference);Reference=@(Reference)"
                      Overwrite="true" />
    <Error Condition="$([System.String]::Copy('@(ProjectReference)').Contains('Unity.ShaderGraph.Editor.csproj'))"
           Text="Unity.ShaderGraph.Editor project reference survived FixUnityCircularReferences." />
    <Error Condition="$([System.String]::Copy('@(ProjectReference)').Contains('Unity.RenderPipelines.Core.Editor.csproj'))"
           Text="Unity.RenderPipelines.Core.Editor project reference survived FixUnityCircularReferences." />
    <Error Condition="!$([System.String]::Copy('@(Reference)').Contains('Unity.ShaderGraph.Editor'))"
           Text="Unity.ShaderGraph.Editor DLL reference was not inserted." />
    <Error Condition="!$([System.String]::Copy('@(Reference)').Contains('Unity.RenderPipelines.Core.Editor'))"
           Text="Unity.RenderPipelines.Core.Editor DLL reference was not inserted." />
  </Target>
</Project>
"@

Set-Content -LiteralPath $projectPath -Value $projectXml -Encoding UTF8

$stdoutPath = $logFullPath + ".stdout.tmp"
$stderrPath = $logFullPath + ".stderr.tmp"
Remove-Item -LiteralPath $stdoutPath,$stderrPath -Force -ErrorAction SilentlyContinue

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $process = Start-Process -FilePath "dotnet" -ArgumentList @("msbuild", "`"$projectPath`"", "-nologo", "-t:Hecton1400ReportReferences", "-maxcpucount:1") -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
} catch {
    $stopwatch.Stop()
    $message = "DOTNET_MSBUILD_LAUNCH_FAILED: " + $_.Exception.Message
    Set-Content -LiteralPath $logFullPath -Value $message -Encoding UTF8
    Write-FuzzerResult -Status "DOTNET_MSBUILD_LAUNCH_FAILED" -CpuLoadPercent $cpuLoad -CompilerProcesses @() -ExitCode -1 -DurationMilliseconds $stopwatch.ElapsedMilliseconds -Message $message -ProjectPath $projectPath -ReportPath $reportPath -LogPath $LogPath
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

$status = if ($process.ExitCode -eq 0) { "GREEN_PROJECT_REFERENCES_REWRITTEN" } else { "FAILED" }
Write-FuzzerResult -Status $status -CpuLoadPercent $cpuLoad -CompilerProcesses @() -ExitCode $process.ExitCode -DurationMilliseconds $stopwatch.ElapsedMilliseconds -Message "MSBuild graph fuzzer completed." -ProjectPath $projectPath -ReportPath $reportPath -LogPath $LogPath
exit $process.ExitCode
