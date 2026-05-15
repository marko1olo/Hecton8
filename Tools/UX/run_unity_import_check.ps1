param(
    [string]$UnityPath = "",
    [string]$ProjectPath = "",
    [string]$LogPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath "Docs\AgentLogs\UnityImport_UX_ENGINEER.log"
}

function Resolve-UnityExe {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (Test-Path -LiteralPath $ExplicitPath) {
            return (Resolve-Path -LiteralPath $ExplicitPath).Path
        }
        throw "Explicit UnityPath not found: $ExplicitPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EXE) -and (Test-Path -LiteralPath $env:UNITY_EXE)) {
        return (Resolve-Path -LiteralPath $env:UNITY_EXE).Path
    }

    $candidateRoots = @(
        "C:\Program Files\Unity\Hub\Editor",
        "C:\Program Files\Unity",
        "C:\Program Files (x86)\Unity"
    )

    foreach ($root in $candidateRoots) {
        if (Test-Path -LiteralPath $root) {
            $candidate = Get-ChildItem -LiteralPath $root -Recurse -Filter "Unity.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -ne $candidate) {
                return $candidate.FullName
            }
        }
    }

    $command = Get-Command "Unity.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Unity.exe not found. Set -UnityPath or UNITY_EXE."
}

$unityExe = Resolve-UnityExe -ExplicitPath $UnityPath
$logDirectory = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

Write-Output "Unity executable: $unityExe"
Write-Output "Project path: $ProjectPath"
Write-Output "Log path: $LogPath"

& $unityExe -batchmode -quit -nographics -projectPath $ProjectPath -logFile $LogPath
$unityExit = $LASTEXITCODE

python (Join-Path $ProjectPath "Tools\UX\unity_compile_log_audit.py") --log $LogPath --write-report
$auditExit = $LASTEXITCODE

$auditReport = Join-Path $ProjectPath "Docs\AgentLogs\UI_UnityCompileLogAudit_UX_ENGINEER.json"
if ($unityExit -eq 0 -and $auditExit -eq 0) {
    python (Join-Path $ProjectPath "Tools\UX\update_unity_verification_report.py") --check UNITY_IMPORT --status PASS --evidence $auditReport --actual "Unity batchmode import and compile-log audit passed." --write-audit
} else {
    python (Join-Path $ProjectPath "Tools\UX\update_unity_verification_report.py") --check UNITY_IMPORT --status FAIL --evidence $auditReport --actual "Unity batchmode import or compile-log audit failed." --write-audit
}
$updateExit = $LASTEXITCODE

if ($unityExit -ne 0) {
    Write-Error "Unity batchmode exited with code $unityExit"
    exit $unityExit
}

if ($updateExit -ne 0) {
    Write-Error "Unity verification report update exited with code $updateExit"
    exit $updateExit
}

exit $auditExit
