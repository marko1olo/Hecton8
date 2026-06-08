param(
    [int]$CpuLimitPercent = 50,
    [int]$CpuSamples = 4,
    [int]$CpuSampleIntervalSeconds = 2,
    [string]$UnityPath = "",
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$projectVersionPath = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
$logDir = Join-Path $projectRoot "Temp\Hecton8ToolLogs"
$logPath = Join-Path $logDir "GeminiMaterialUnityApplyAll.log"
$executeMethod = "Hecton8.EditorTools.GeminiMaterialIntegrationApplier.ApplyAll"
$staticPreflightRunner = Join-Path $projectRoot "Tools\RunGeminiMaterialStaticPreflight.ps1"
$materialAssetValidator = Join-Path $projectRoot "Tools\ValidateExternalPbrMaterialAssets.py"
$heldToolValidator = Join-Path $projectRoot "Tools\ValidateHeldToolExternalPbrRules.py"
$worldToolValidator = Join-Path $projectRoot "Tools\ValidateWorldToolExternalPbrRules.py"
$worldProxyValidator = Join-Path $projectRoot "Tools\ValidateWorldProxyGeminiBiomeAssignments.py"
$constructionValidator = Join-Path $projectRoot "Tools\ValidateConstructionGeminiMaterialAssignments.py"
$batch34SourceAtlasImporterValidator = Join-Path $projectRoot "Tools\ValidateBatch34SourceAtlasImporter.py"
$batch34TerrainLayerBuilderValidator = Join-Path $projectRoot "Tools\ValidateBatch34TerrainLayerBuilder.py"
$productFacePlayerSuitValidator = Join-Path $projectRoot "Tools\ValidateProductFacePlayerSuitGeminiMaterialRoute.py"
$resourcePickupMaterialValidator = Join-Path $projectRoot "Tools\ValidateResourcePickupGeminiMaterialRoute.py"
$worldSupportMaterialValidator = Join-Path $projectRoot "Tools\ValidateWorldSupportGeminiMaterialRoute.py"
$toolSurfaceDetailValidator = Join-Path $projectRoot "Tools\ValidateToolSurfaceDetailGeminiRoute.py"
$uvAtlasMaterialHandoffValidator = Join-Path $projectRoot "Tools\ValidateBatch34UvAtlasMaterialHandoff.py"
$constructionInsulationValidator = Join-Path $projectRoot "Tools\ValidateConstructionInsulationBackingRoute.py"
$batch34VisorTraumaDecalArrayValidator = Join-Path $projectRoot "Tools\ValidateBatch34VisorTraumaDecalArrayRoute.py"
$batch34VisorTraumaProfileCsvValidator = Join-Path $projectRoot "Tools\ValidateBatch34VisorTraumaProfileCsv.py"

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
    $blocked = Get-Process dotnet,csc,msbuild,VBCSCompiler,Unity,'Unity Hub' -ErrorAction SilentlyContinue
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

function Invoke-PythonValidator {
    param(
        [string]$ValidatorPath,
        [string[]]$Arguments = @()
    )

    & python -B $ValidatorPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Validator failed: $ValidatorPath $($Arguments -join ' ')"
    }
}

function Get-UnityLogIssueSummary {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return [pscustomobject]@{
            WarningCount = -1
            ErrorCount = -1
            LogExists = $false
        }
    }

    $warningCount = 0
    $errorCount = 0
    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_
        if ($line -match "(?i)\b(warning|warn)\b" -and $line -notmatch "(?i)warnings\s*=\s*0") {
            $warningCount++
        }

        if ($line -match "(?i)\b(error|exception|failed|fatal)\b" -and
            $line -notmatch "(?i)(errors\s*=\s*0|warnings\s*=\s*0|No errors)") {
            $errorCount++
        }
    }

    return [pscustomobject]@{
        WarningCount = $warningCount
        ErrorCount = $errorCount
        LogExists = $true
    }
}

$resolvedUnity = Resolve-UnityPath -RequestedPath $UnityPath

& $staticPreflightRunner
if ($LASTEXITCODE -ne 0) {
    throw "Gemini material static preflight failed."
}

Wait-Or-Assert-Gate

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$unityStartedAt = (Get-Date).ToUniversalTime().ToString("o")
Write-Host "Gemini material Unity apply-all startUtc=$unityStartedAt unity=$resolvedUnity projectPath=$projectRoot executeMethod=$executeMethod log=$logPath"
& $resolvedUnity `
    -batchmode `
    -nographics `
    -quit `
    -projectPath $projectRoot `
    -executeMethod $executeMethod `
    -logFile $logPath

$unityExitCode = $LASTEXITCODE
$unityEndedAt = (Get-Date).ToUniversalTime().ToString("o")
$unityLogSummary = Get-UnityLogIssueSummary -Path $logPath
Write-Host "Gemini material Unity apply-all endUtc=$unityEndedAt exitCode=$unityExitCode warningCount=$($unityLogSummary.WarningCount) errorCount=$($unityLogSummary.ErrorCount) logExists=$($unityLogSummary.LogExists) log=$logPath"

if ($unityExitCode -ne 0 -or -not $unityLogSummary.LogExists -or $unityLogSummary.ErrorCount -gt 0) {
    throw "Gemini material Unity apply-all failed. exitCode=$unityExitCode warnings=$($unityLogSummary.WarningCount) errors=$($unityLogSummary.ErrorCount) logExists=$($unityLogSummary.LogExists) log=$logPath"
}

Invoke-PythonValidator -ValidatorPath $materialAssetValidator
Invoke-PythonValidator -ValidatorPath $heldToolValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $worldToolValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $worldProxyValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $constructionValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $batch34SourceAtlasImporterValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $batch34TerrainLayerBuilderValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $productFacePlayerSuitValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $resourcePickupMaterialValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $worldSupportMaterialValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $toolSurfaceDetailValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $uvAtlasMaterialHandoffValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $constructionInsulationValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $batch34VisorTraumaDecalArrayValidator -Arguments @("--post-apply")
Invoke-PythonValidator -ValidatorPath $batch34VisorTraumaProfileCsvValidator

Write-Host "Gemini material Unity apply-all passed. log=$logPath"
