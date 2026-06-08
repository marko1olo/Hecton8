param(
    [int]$CpuLimitPercent = 50,
    [int]$CpuSamples = 4,
    [int]$CpuSampleIntervalSeconds = 2,
    [string]$UnityPath = "",
    [switch]$WaitForGate,
    [int]$MaxWaitSeconds = 900
)

$ErrorActionPreference = "Stop"

$applyAllRunner = Join-Path $PSScriptRoot "RunGeminiMaterialUnityApplyAll.ps1"
& $applyAllRunner `
    -CpuLimitPercent $CpuLimitPercent `
    -CpuSamples $CpuSamples `
    -CpuSampleIntervalSeconds $CpuSampleIntervalSeconds `
    -UnityPath $UnityPath `
    -MaxWaitSeconds $MaxWaitSeconds `
    -WaitForGate:$WaitForGate
