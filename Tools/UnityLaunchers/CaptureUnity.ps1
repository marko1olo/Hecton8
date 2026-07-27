. "C:\hades\.codex_ops\AgentGuiOps.ps1"

Write-Host "Waiting for Unity window..."
$window = $null
for ($i = 0; $i -lt 60; $i++) {
    $window = Get-AgentWindow -ProcessLike "Unity" -TitleLike "*Hecton8*"
    if ($window) { break }
    Start-Sleep -Seconds 5
}

if (-not $window) {
    Write-Host "Failed to find Unity window."
    exit 1
}

Write-Host "Found Unity window: $($window.Title)"
Write-Host "Waiting 20 seconds for Editor to finish layout..."
Start-Sleep -Seconds 20

Write-Host "Bringing window to front and sending Ctrl+P..."
Set-AgentWindowFront -Handle $window.Handle
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait("^p")

Write-Host "Waiting 20 seconds for Play Mode to generate terrain..."
Start-Sleep -Seconds 20

$outPath = "C:\hades\Hecton8\Docs\GeneratedAssets\Terrain\Screenshot.png"
Write-Host "Taking screenshot..."
Save-AgentWindowShot -Handle $window.Handle -Path $outPath

Write-Host "Screenshot saved."
Stop-Process -Name Unity -Force
