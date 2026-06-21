Import-Module C:\hades\.codex_ops\AgentGuiOps.ps1
$unity = Get-AgentWindows | Where-Object { $_.ProcessName -eq "Unity" }
if ($unity) {
    Save-AgentWindowShot -Handle $unity[0].Handle -Path "C:\Users\danat\.gemini\antigravity\brain\389e4a53-b1e6-440c-b190-0f5c509fa8c4\Terrain_Gui_0.png"
    Write-Host "Screenshot saved!"
} else {
    Write-Host "Unity window not found!"
}
