# P0 ecology-ready Frost starve — 2026-07-31

## Prior state
- FO soft-deadlock fixed+pushed (see p0_fo_bootstrap_lock_drain_20260731).
- Live smoke proved foLock=0 dispBootstrapLocked=0 after GameReady.
- Progress lines: ecoInit=1 from first sample t=0.0s through t=480s+.
- Never flipped _ecologyReady → BOOTSTRAP_TIMEOUT budget burns with no sim advance.

## Root cause
TryMarkEcologyReady() lived only on IFrostTickable.FrostTick.
readyNow = ecosystem != null && ecosystem.IsInitialized was true the entire wait.
Mark path never invoked because Frost did not deliver (dispatcher gates / deltaTime<=0 / interval accumulator never fed).

Ready-mark is a **gate**, not a substitute for sim ticks. Same starvation-proof pattern as moving the ecology wait clock off ColdTick onto MonoBehaviour.Update (p0_gameready).

## Fix (product)
HeadlessSimulationRunner.Update wait block:
1. TryArmEcologyWaitClock()
2. TryMarkEcologyReady()  // NEW
3. early return if ready
4. FO flush + wait progress (pre-ready only)

On first ready transition: LogRunnerLifecycle("ecology ready (ecosystem initialized)") before log filter muzzles Log.

Wait progress diag adds frostReg + dispFrameLocked (InternalsVisibleTo).

## Still open after this fix
- Day-boundary debt still queued in FrostTick; if Frost remains starved post-ready, days will not advance.
- If live smoke shows ready + timeDilationDelivered==0 / zero days → fix dilation/pause root in SystemDispatcher path next.
- Real-game screenshots still DECLINED until interactive proof.

## DoD
status not in {ECOLOGY_UNAVAILABLE, BATCH_TIMEOUT, BOOTSTRAP_TIMEOUT};
ecologySampledDays>0; timeDilationDelivered>0; no error CS.
