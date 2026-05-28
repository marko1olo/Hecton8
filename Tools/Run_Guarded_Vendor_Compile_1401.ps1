param(
    [string[]]$ProjectNames = @(
        'CandiceAIforGames.Runtime',
        'CandiceAIforGames.Editor',
        'AmplifyImpostors.Runtime',
        'AmplifyImpostors.Editor',
        'TechniePhysicsCreator',
        'Technie.PhysicsCreator.Updater',
        'DarkTonic.MasterAudio.Runtime',
        'DarkTonic.MasterAudio.Examples',
        'DarkTonic.MasterAudio.Editor',
        'RelationsInspector.Editor'
    ),
    [double]$MaxCpuPercent = 50.0,
    [switch]$DryRun,
    [string]$OutputDir = 'Docs/AgentLogs'
)

$ErrorActionPreference = 'Stop'

function Get-HectonCpuLoad {
    try {
        $samples = Get-CimInstance -ClassName Win32_Processor
        if ($null -eq $samples) {
            return -1.0
        }

        $avg = ($samples | Measure-Object -Property LoadPercentage -Average).Average
        if ($null -eq $avg) {
            return -1.0
        }

        return [double]$avg
    }
    catch {
        return -1.0
    }
}

function Get-HectonCompilerProcesses {
    $names = @('csc', 'VBCSCompiler', 'MSBuild', 'dotnet')
    $all = Get-Process -ErrorAction SilentlyContinue
    $hits = New-Object System.Collections.Generic.List[object]
    foreach ($p in $all) {
        if ($names -contains $p.ProcessName) {
            $hits.Add([pscustomobject]@{
                name = $p.ProcessName
                id = $p.Id
                cpu = $p.CPU
            })
        }
    }

    return $hits
}

function Find-HectonProject {
    param([string]$Name)

    $direct = Join-Path (Get-Location) ($Name + '.csproj')
    if (Test-Path -LiteralPath $direct) {
        return $direct
    }

    return $null
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$cpu = Get-HectonCpuLoad
$compilers = @(Get-HectonCompilerProcesses)
$cpuBlocked = ($cpu -lt 0) -or ($cpu -gt $MaxCpuPercent)
$compilerBlocked = ($compilers.Length -gt 0)
$blocked = $cpuBlocked -or $compilerBlocked
$blockReasons = New-Object System.Collections.Generic.List[string]
if ($cpu -lt 0) {
    $blockReasons.Add('CPU_SAMPLE_UNAVAILABLE')
}
elseif ($cpu -gt $MaxCpuPercent) {
    $blockReasons.Add('CPU_LOAD_ABOVE_THRESHOLD')
}

if ($compilerBlocked) {
    $blockReasons.Add('ACTIVE_COMPILER_PROCESS')
}
$projects = New-Object System.Collections.Generic.List[object]

foreach ($name in $ProjectNames) {
    $path = Find-HectonProject -Name $name
    $projects.Add([pscustomobject]@{
        name = $name
        path = $path
        exists = ($null -ne $path)
    })
}

$summary = [ordered]@{
    agent = '1401'
    timestamp = $timestamp
    dryRun = [bool]$DryRun
    maxCpuPercent = $MaxCpuPercent
    cpuLoadPercent = $cpu
    compilerProcesses = @($compilers | ForEach-Object { $_ })
    compilerProcessCount = $compilers.Length
    blockedByContention = [bool]$blocked
    blockReasons = @($blockReasons | ForEach-Object { $_ })
    projects = @($projects | ForEach-Object { $_ })
    attempts = @()
}

if ($blocked) {
    $summaryPath = Join-Path $OutputDir ("Build_1401_Attempt_{0}_BLOCKED_BY_CONTENTION.json" -f $timestamp)
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Write-Output $summaryPath
    exit 75
}

if ($DryRun) {
    $summaryPath = Join-Path $OutputDir ("Build_1401_Attempt_{0}_DRYRUN.json" -f $timestamp)
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Write-Output $summaryPath
    exit 0
}

$attempts = New-Object System.Collections.Generic.List[object]
foreach ($project in $projects) {
    if (-not $project.exists) {
        $attempts.Add([pscustomobject]@{
            name = $project.name
            path = $project.path
            skipped = $true
            reason = 'PROJECT_FILE_MISSING'
        })
        continue
    }

    $safeName = $project.name -replace '[^A-Za-z0-9_.-]', '_'
    $logPath = Join-Path $OutputDir ("Build_1401_Attempt_{0}_{1}.log" -f $timestamp, $safeName)
    $args = @(
        'build',
        $project.path,
        '--no-restore',
        '/nologo',
        '/v:minimal',
        '/p:UseSharedCompilation=false'
    )

    $output = & dotnet @args 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath $logPath -Encoding UTF8

    $diagnostics = Select-String -LiteralPath $logPath -Pattern 'Candice', 'Amplify', 'Technie', 'MasterAudio', 'RelationsInspector', 'CS0246', 'CS1061', 'CS0618', ': error ', ': warning ' -SimpleMatch
    $vendorDiagnostics = New-Object System.Collections.Generic.List[object]
    foreach ($d in $diagnostics) {
        $vendorDiagnostics.Add([pscustomobject]@{
            line = $d.LineNumber
            text = $d.Line.Trim()
        })
    }

    $attempts.Add([pscustomobject]@{
        name = $project.name
        path = $project.path
        skipped = $false
        exitCode = $exitCode
        logPath = $logPath
        vendorDiagnosticCount = $vendorDiagnostics.Count
        vendorDiagnostics = $vendorDiagnostics
    })
}

$summary.attempts = @($attempts | ForEach-Object { $_ })
$summaryPath = Join-Path $OutputDir ("Build_1401_Attempt_{0}_SUMMARY.json" -f $timestamp)
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Output $summaryPath

foreach ($a in $attempts) {
    if (-not $a.skipped -and $a.exitCode -ne 0) {
        exit $a.exitCode
    }
}

exit 0
