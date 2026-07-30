Set-Location 'C:\hades\Hecton8'
Write-Host '=== Acquire/Release SceneRebase ==='
Select-String -Path 'Assets\_Project\Scripts\HectonFloatingOrigin.cs' -Pattern 'AcquireSceneRebaseTickLock|ReleaseSceneRebaseTickLock|ProcessPendingSceneSynchronization|_sceneRebaseTickLockHeld|_pendingLoadedScenes' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }

Write-Host '=== PublishGameReady context ==='
$lines = Get-Content 'Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs'
for ($i = 7720; $i -lt 7780; $i++) { '{0}: {1}' -f ($i+1), $lines[$i] }

Write-Host '=== Headless short-circuit full block ==='
for ($i = 3130; $i -lt 3180; $i++) { '{0}: {1}' -f ($i+1), $lines[$i] }

Write-Host '=== After SceneActivate phase return path ==='
Select-String -Path 'Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs' -Pattern 'PublishGameReady|FinalizeBootstrap|BootComplete|MarkBootComplete|SceneActivate' | Select-Object -First 40 | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }

Write-Host '=== Log timestamps near key events ==='
$log = Get-Content 'Docs\AgentLogs\headless_smoke_20260730_p0_dispfix.log'
# Unity logs often have timestamps at start
$idxs = @(640,652,1354,1367,1548,2433,2469,2505,2543)
foreach ($n in $idxs) {
  if ($n -le $log.Count) { '{0}: {1}' -f $n, $log[$n-1].Substring(0, [Math]::Min(160, $log[$n-1].Length)) }
}

Write-Host '=== MaxCadenceSubsteps / FastTick discard ==='
Select-String -Path 'Assets\_Project\Scripts\Core\SystemDispatcher.cs' -Pattern 'MaxCadenceSubstepsPerFrame|TimeDilationMaximumScalar|RequestHeadlessTimeDilation' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }

Write-Host '=== Batch runner timeout / play stop ==='
Select-String -Path 'Assets\_Project\Scripts\QA\Headless\Editor\HeadlessSimulationBatchRunner.cs' -Pattern 'Timeout|BATCH_TIMEOUT|EditorApplication.isPlaying|StopPlay|PollRunState|CompleteAfter' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
