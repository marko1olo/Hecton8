. "C:\hades\.codex_ops\AgentGuiOps.ps1"

Write-Host "Launching Unity in interactive session..."
$process = Start-Process -FilePath "C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -ArgumentList "-projectPath `"C:\hades\Hecton8`" -scene `"Assets\_Project\Scenes\010_TEST.unity`"" -PassThru

Write-Host "Waiting for Unity window..."
$window = $null
for ($i = 0; $i -lt 120; $i++) {
    $window = Get-AgentWindow -ProcessLike "Unity" -TitleLike "*Hecton8*"
    if ($window) { break }
    Start-Sleep -Seconds 2
}

if (-not $window) {
    Write-Host "Failed to find Unity window."
    Stop-Process -Id $process.Id -Force
    exit 1
}

Write-Host "Found Unity window: $($window.Title)"
Write-Host "Waiting 30 seconds for Editor to finish layout and shaders to compile..."
Start-Sleep -Seconds 30

Write-Host "Bringing window to front and sending Ctrl+P..."
Set-AgentWindowFront -Handle $window.Handle
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.SendKeys]::SendWait("^p")

Write-Host "Waiting 25 seconds for Play Mode to generate terrain..."
Start-Sleep -Seconds 25

$outPath = "C:\hades\Hecton8\Docs\GeneratedAssets\Terrain\Screenshot.png"
Write-Host "Taking screenshot..."
Save-AgentWindowShot -Handle $window.Handle -Path $outPath

Write-Host "Screenshot saved."
Stop-Process -Name Unity -Force
