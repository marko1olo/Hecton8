param(
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe",
    [string]$ProjectPath = "C:\hades\Hecton8",
    [string]$ArtifactDir = "C:\Users\Admin\.gemini\antigravity\brain\9412af70-ebf5-491e-80e6-e0b2fcde1017\"
)

$ErrorActionPreference = "Stop"
$LogFile   = "$ProjectPath\batchmode.log"
$SuccessF  = "${ArtifactDir}mcp_success.txt"
$ErrorF    = "${ArtifactDir}mcp_error.txt"

# -- clean previous sentinels --
if (Test-Path $SuccessF) { Remove-Item $SuccessF -Force }
if (Test-Path $ErrorF)   { Remove-Item $ErrorF   -Force }
if (Test-Path $LogFile)  { Remove-Item $LogFile  -Force }

Write-Host "[run_matrix] Launching Unity batchmode..." -ForegroundColor Cyan

$args = @(
    "-projectPath", $ProjectPath,
    "-executeMethod", "Hecton8.Editor.TerrainRenderTestGoal.Execute",
    "-batchmode",
    "-quit",
    "-logFile", $LogFile
)

$proc = Start-Process -FilePath $UnityExe -ArgumentList $args -PassThru -NoNewWindow

Write-Host "[run_matrix] Unity PID $($proc.Id). Tailing log..."

# Tail batchmode.log while Unity runs
$lastLen = 0
while (-not $proc.HasExited) {
    Start-Sleep -Milliseconds 500
    if (Test-Path $LogFile) {
        $content = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue
        if ($content -and $content.Length -gt $lastLen) {
            $newChunk = $content.Substring($lastLen)
            Write-Host $newChunk -NoNewline
            $lastLen = $content.Length
        }
    }
}

# Drain remaining log
if (Test-Path $LogFile) {
    $content = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue
    if ($content -and $content.Length -gt $lastLen) {
        Write-Host $content.Substring($lastLen) -NoNewline
    }
}

Write-Host ""
Write-Host "[run_matrix] Unity exited with code $($proc.ExitCode)" -ForegroundColor Cyan

# -- check sentinels --
if (Test-Path $SuccessF) {
    $msg = Get-Content $SuccessF -Raw
    Write-Host "[run_matrix] SUCCESS: $msg" -ForegroundColor Green

    Write-Host ""
    Write-Host "=== Artifact outputs ===" -ForegroundColor Yellow
    Get-ChildItem $ArtifactDir -Filter "*.png" | Sort-Object Name | ForEach-Object {
        Write-Host "  $($_.FullName)  [$([math]::Round($_.Length/1KB,1)) KB]"
    }
} elseif (Test-Path $ErrorF) {
    $msg = Get-Content $ErrorF -Raw
    Write-Host "[run_matrix] FAIL: $msg" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[run_matrix] No sentinel written. Check batchmode.log for crash." -ForegroundColor Yellow
    exit 2
}
