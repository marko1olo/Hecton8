Set-Location 'C:\hades\Hecton8'
$patterns = @(
  @{ Path = 'Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs'; Pat = 'MarkMainMenuReached|PublishGameReady|headless|SceneActivate' },
  @{ Path = 'Assets\_Project\Scripts\Core\SystemDispatcher.cs'; Pat = 'originShiftBootstrapLock|TryFlushInitialSceneRebase|IsOriginShiftBootstrapLocked|HeadlessTimeMode|StepBounded' },
  @{ Path = 'Assets\_Project\Scripts\HectonFloatingOrigin.cs'; Pat = 'TryFlushInitialSceneRebase|BootstrapLock|originShift' },
  @{ Path = 'Assets\_Project\Scripts\Core\BootstrapContracts\BootstrapState.cs'; Pat = 'PublishGameReady|IsGameReady|HasActiveInstance|MarkMainMenu' }
)

# Find BootstrapStatus
Get-ChildItem -Path 'Assets\_Project\Scripts' -Recurse -Filter '*BootstrapStatus*' -File | ForEach-Object { $_.FullName }

foreach ($p in $patterns) {
  Write-Host "===== $($p.Path) ====="
  if (-not (Test-Path $p.Path)) { Write-Host 'MISSING'; continue }
  Select-String -Path $p.Path -Pattern $p.Pat | ForEach-Object {
    '{0}: {1}' -f $_.LineNumber, $_.Line.Trim()
  }
}

# BootstrapStatus file content needles
$bs = Get-ChildItem -Path 'Assets\_Project\Scripts' -Recurse -Filter 'BootstrapStatus.cs' -File | Select-Object -First 1
if ($bs) {
  Write-Host "===== $($bs.FullName) ====="
  Select-String -Path $bs.FullName -Pattern 'MarkMainMenuReached|PublishGameReady|IsGameReady|MainMenu' | ForEach-Object {
    '{0}: {1}' -f $_.LineNumber, $_.Line.Trim()
  }
}

# Log tail after short-circuit for origin lock / halt
$log = 'Docs\AgentLogs\headless_smoke_20260730_p0_dispfix.log'
Write-Host '===== LOG AFTER L1367 ====='
if (Test-Path $log) {
  $lines = Get-Content $log
  $start = 1360
  $end = [Math]::Min($lines.Count, 2600)
  for ($i = $start; $i -lt $end; $i++) {
    $line = $lines[$i]
    if ($line -match 'HEADLESS|Origin|origin|halt|Halt|GameReady|MainMenu|Ecosystem|short-circuit|BATCH|Fail|DISPATCH|Cold|Frost|Paused|SimulationHalted|bootstrap lock|FloatingOrigin') {
      '{0}: {1}' -f ($i+1), $line
    }
  }
}
