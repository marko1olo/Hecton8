. "C:\hades\.codex_ops\AgentGuiOps.ps1"

Write-Host "Launching Unity..."
$process = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -ArgumentList "-projectPath `"C:\hades\Hecton8`" -scene `"Assets\_Project\Scenes\010_TEST.unity`"" -PassThru

Write-Host "Waiting for Unity window..."
$window = $null
for ($i = 0; $i -lt 120; $i++) {
    $window = Get-AgentWindow -TitleLike "*Unity 6000.4.10f1*" -ProcessLike "Unity"
    if ($window) { break }
    Start-Sleep -Seconds 1
}

if (-not $window) {
    Write-Host "Unity window not found."
    Stop-Process -Id $process.Id -Force
    exit 1
}

Write-Host "Unity window found. Waiting for load..."
Start-Sleep -Seconds 20

Write-Host "Entering Play Mode (Ctrl+P)..."
Set-AgentWindowFront -Handle $window.Handle

Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait("^p")

Write-Host "Waiting for Play Mode to start and terrain to generate..."
Start-Sleep -Seconds 15

$outPath = "C:\hades\Hecton8\Docs\GeneratedAssets\Terrain\Screenshot.png"
Write-Host "Taking screenshot..."
Save-AgentWindowShot -Handle $window.Handle -Path $outPath

Write-Host "Screenshot saved. Closing Unity..."
Stop-Process -Id $process.Id -Force
