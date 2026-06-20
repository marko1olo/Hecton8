. C:\hades\.codex_ops\AgentGuiOps.ps1
$unity = Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -First 1
if ($unity -and $unity.MainWindowHandle -ne 0) {
    Save-AgentWindowShot -Handle $unity.MainWindowHandle -Path "C:\Users\danat\.gemini\antigravity\brain\389e4a53-b1e6-440c-b190-0f5c509fa8c4\Unity_Screenshot.png"
    Write-Output "Screenshot taken"
} else {
    Write-Output "No Unity main window found yet."
}
