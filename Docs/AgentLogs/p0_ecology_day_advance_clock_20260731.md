# P0 ecology day-advance clock restore - evidence

**UTC written:** 2026-07-31
**Repo:** C:/hades/Hecton8
**Branch:** main
**Product file:** Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs

## Problem (proved live)

After FO lock-drain (`411715153`) and ready-mark on Update (`80b2d9764`):

- Lifecycle reached: `[HEADLESS] ecology ready (ecosystem initialized)`
- Then ~495s wall with **zero** CSV day rows
- Batch stub: status=BATCH_TIMEOUT exitCode=2 source=HeadlessSimulationBatchRunner
- Verdict: Fast/Frost received dt~0 (paused or dilation collapsed). Not a tight watchdog.

Smoke meta (prior FAIL): head=`80b2d9764`, pid=21516, log `Docs/AgentLogs/headless_smoke_20260731_p0_ecology_ready_20260731_014953.log`

## Root mechanism (code)

SystemDispatcher.ConsumeFrameTimeDilationScalar returns 0 if _simulationPaused or scalar<=1e-4.
RequestSimulationPause(true) zeros scalar; unpause restores _prePauseTimeDilationScalar (often 1, not headless 100).
Runner previously called RequestHeadlessTimeDilation(100) once at lane register - no re-assert on ecology ready / GameReady / after late pause.

## Product fix (this change)

### EnsureHeadlessSimulationClock(reason)
1. Resolve GlobalRegistry.TickDispatcher
2. If SimulationPaused -> RequestSimulationPause(false, RunnerHash)
3. RequestHeadlessTimeDilation(100, RunnerHash)
4. LogWarning: sim clock ensure reason=... pausedBefore=... dilBefore=... dilAfter=... pausedAfter=... gameReady=...

### Call sites
| reason | when |
|---|---|
| lanes-registered | after RegisterRuntimeLanes (replaces bare dilation request) |
| ecology-ready | first TryMarkEcologyReady transition |
| game-ready | TryArmEcologyWaitClock arms |
| post-ready-sustain | every 5s while ready and days==0 and (paused or dil < 100-eps) |

### Update() post-ready path
When ready: every frame TryArmEcologyWaitClock + sustain + MaybeLogPostReadyProgress + FO flush.
No early-return that skips post-ready work.

### MaybeLogPostReadyProgress (Warning, 15s)
post-ready t=...s paused=... dil=... dayAcc=... pending=... days=... simS=... gameReady=... frostReg=... lateReg=... fo*=...

## Marker verify (post-patch)

```
EnsureHeadlessSimulationClock = 7
MaybeEnsureHeadlessSimulationClockSustain = 2
MaybeLogPostReadyProgress = 2
post-ready-sustain = 1
PostReadyClockEnsureIntervalSeconds = 2
delta_bytes = +7194
```

Note: prior session claimed fix on disk; this session found it MISSING (only TimeDilationScalar const remained). Re-applied via `_agent_apply_clock_fix.py` before commit. Do not commit the patcher scratch.

## Anti-mock

- No CSV rows without Frost/LateFrame audits
- No lowered daySeconds
- No forced SUCCESS / fake _completedDays / fake _simulatedSeconds
- No skipped biomass audit

## DoD (smoke - OPEN until polled)

| Field | Required |
|---|---|
| status | not in {ECOLOGY_UNAVAILABLE, BATCH_TIMEOUT, BOOTSTRAP_TIMEOUT} |
| ecologySampledDays | > 0 |
| timeDilationDelivered | > 0 |
| compile | no error CS |

Expected log signals:
- sim clock ensure reason=lanes-registered
- sim clock ensure reason=ecology-ready ... dilAfter=100
- post-ready t=... dil=... dayAcc=... growing
- runtime WriteResult JSON (not batch stub) with ecologySampledDays>0

## Real-game

Headless green alone = DECLINED for gameplay claims. Screenshots required for ship claim.

## Related commits

| Hash | Role |
|---|---|
| 411715153 | FO lock-drain |
| 80b2d9764 | ready-mark Update |
| (this) | clock ensure + sustain + post-ready diag |
