param(
    [Parameter(Mandatory = $true)]
    [string]$BindingMap,
    [int]$MaxTextureSize = 512,
    [int]$CpuLimitPercent = 50,
    [int]$CpuSamples = 4,
    [int]$CpuSampleIntervalSeconds = 2,
    [string]$UnityPath = "",
    [switch]$AllowOverwrite,
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$projectVersionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
$logDir = Join-Path $projectRoot "Temp\Hecton8ToolLogs"
$logPath = Join-Path $logDir "InventoryIconUnityBindFromMap.log"
$bindingValidatorScript = Join-Path $projectRoot "Tools\InventoryIconBindingMapValidator.py"
$importAuditScript = Join-Path $projectRoot "Tools\InventoryUnityImportStateAudit.py"
$executeMethod = "Hecton8.EditorTools.InventoryIconCandidateImporter.BatchBindAndValidateInventoryIconsFromArgs"

function Resolve-ProjectOrAbsolutePath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return Resolve-Path -LiteralPath $Path
    }

    return Resolve-Path -LiteralPath (Join-Path $projectRoot $Path)
}

$bindingMapPath = Resolve-ProjectOrAbsolutePath -Path $BindingMap

$validatorArgs = @(
    "-B",
    $bindingValidatorScript,
    "--map",
    $bindingMapPath,
    "--edge-margin-px",
    "32",
    "--require-source-bake-manifest",
    "--require-approved-bindings"
)
if (-not $AllowOverwrite) {
    $validatorArgs += "--require-empty-icon"
}

python @validatorArgs
if ($LASTEXITCODE -ne 0) {
    throw "Inventory icon binding map preflight failed. bindingMap=$bindingMapPath"
}

function Get-UnityEditorVersion {
    $line = Get-Content -LiteralPath $projectVersionPath | Where-Object { $_ -like "m_EditorVersion:*" } | Select-Object -First 1
    if (-not $line) {
        throw "ProjectVersion.txt does not contain m_EditorVersion."
    }

    return ($line -replace "m_EditorVersion:\s*", "").Trim()
}

function Resolve-UnityPath {
    param([string]$RequestedPath)

    if ($RequestedPath) {
        if (-not (Test-Path -LiteralPath $RequestedPath)) {
            throw "UnityPath does not exist: $RequestedPath"
        }

        return $RequestedPath
    }

    $version = Get-UnityEditorVersion
    $candidate = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    throw "Unity editor not found for version $version at $candidate"
}

function Assert-NoBuildProcesses {
    $blocked = Get-Process dotnet,csc,msbuild,VBCSCompiler,Unity -ErrorAction SilentlyContinue
    if ($blocked) {
        $summary = ($blocked | Select-Object -First 8 | ForEach-Object { "$($_.ProcessName):$($_.Id)" }) -join ", "
        throw "Blocked by active build/editor process: $summary"
    }
}

function Assert-CpuBelowLimit {
    param(
        [int]$LimitPercent,
        [int]$Samples,
        [int]$IntervalSeconds
    )

    $values = Get-Counter "\Processor(_Total)\% Processor Time" -SampleInterval $IntervalSeconds -MaxSamples $Samples |
        Select-Object -ExpandProperty CounterSamples |
        ForEach-Object { [math]::Round($_.CookedValue, 1) }

    $max = ($values | Measure-Object -Maximum).Maximum
    if ($max -gt $LimitPercent) {
        throw "CPU gate failed. limit=$LimitPercent max=$max samples=$($values -join ',')"
    }
}

function Wait-Or-Assert-Gate {
    if (-not $WaitForGate) {
        Assert-NoBuildProcesses
        Assert-CpuBelowLimit -LimitPercent $CpuLimitPercent -Samples $CpuSamples -IntervalSeconds $CpuSampleIntervalSeconds
        return
    }

    $deadline = (Get-Date).AddSeconds($MaxWaitSeconds)
    $attempt = 0
    while ((Get-Date) -lt $deadline) {
        $attempt++
        try {
            Assert-NoBuildProcesses
            Assert-CpuBelowLimit -LimitPercent $CpuLimitPercent -Samples $CpuSamples -IntervalSeconds $CpuSampleIntervalSeconds
            Write-Host "Unity gate passed after wait. attempts=$attempt"
            return
        }
        catch {
            Write-Host "Unity gate still blocked. attempts=$attempt reason=$($_.Exception.Message)"
            Start-Sleep -Seconds 10
        }
    }

    throw "Unity gate did not pass within $MaxWaitSeconds seconds."
}

$resolvedUnity = Resolve-UnityPath -RequestedPath $UnityPath
Wait-Or-Assert-Gate

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath",
    $projectRoot,
    "-executeMethod",
    $executeMethod,
    "-h8InventoryIconBindingMap",
    $bindingMapPath,
    "-h8InventoryIconMaxTextureSize",
    $MaxTextureSize,
    "-logFile",
    $logPath
)
if ($AllowOverwrite) {
    $unityArgs += "-h8InventoryIconAllowOverwrite"
}

& $resolvedUnity @unityArgs

if ($LASTEXITCODE -ne 0) {
    throw "Unity inventory icon bind failed with exit code $LASTEXITCODE. log=$logPath"
}

python -B $importAuditScript --binding-map $bindingMapPath --require-bound --require-import-settings
if ($LASTEXITCODE -ne 0) {
    throw "Post-Unity inventory bind audit failed. log=$logPath"
}

Write-Host "Inventory icon Unity bind passed. bindingMap=$bindingMapPath log=$logPath"
