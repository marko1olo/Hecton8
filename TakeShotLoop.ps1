. C:\hades\.codex_ops\AgentGuiOps.ps1
for ($i = 0; $i -lt 15; $i++) {
    $unity = Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($unity -and $unity.MainWindowHandle -ne 0) {
        Save-AgentWindowShot -Handle $unity.MainWindowHandle -Path "C:\Users\danat\.gemini\antigravity\brain\389e4a53-b1e6-440c-b190-0f5c509fa8c4\Unity_Gui_Screenshot.png"
        Write-Output "Screenshot captured!"
        exit 0
    }
    Start-Sleep -Seconds 5
}
Write-Output "Unity window not found after 75 seconds."
