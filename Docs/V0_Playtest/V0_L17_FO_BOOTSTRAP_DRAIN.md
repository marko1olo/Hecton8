# V0 L17 — Floating-origin bootstrap drain (PlayModeProbe + LateFrame)

**Status:** IMPLEMENTED (product). LIVE verification pending.  
**HEAD at implement:** post-L16 `a2304a9af` + this change.  
**Swim PASS still requires LIVE:** hop2 present + `movementIntent01max > 0`.

## Root (L17 dig)

L16 LIVE proved the step-bounded clock works (`SIMCLOCK stepBoundAfter=1`) and hop1 stayed healthy (`currentStateMove=(0,1)`, `inputEnabledNow=True`, `blockMask=0`, menus closed). Swim still FAIL: hop2 ABSENT, `movementIntent01max=0`.

### Smoking gun (L16 LIVE INPUTHOP)

Across all three INPUTHOP samples while gameplay advanced:

| Signal | Behavior |
|--------|----------|
| `presimTick` | 566 → 581 (advances) |
| `lateFrameTick` | **49 frozen** |
| `pumpFired` | **1 frozen** |
| hop1 | PRESENT |
| hop2 | ABSENT |

PreSimulationInputTick runs **before** origin/AUP early-outs in `RunDispatcherUpdate`. FixedTick and LateFrame run **after** those gates. Frozen lateFrame + advancing presim = full dispatcher path starved after PreSim.

### Causal chain (product path only)

1. hop2 is recorded only inside `InputDispatcher.GetState` via `DiagRecordReadObservation(2)`.
2. GetState is reached only when HPM FixedTick → Sample → ProcessPlayerInputFrame → TryReadFrame runs.
3. FixedTick runs only inside `RunFixedStepAccumulator`, which is after origin bootstrap / AUP gates in `RunDispatcherUpdate`.
4. When `IsOriginShiftBootstrapLocked` is true, `RunDispatcherUpdate` calls `TryFlushInitialSceneRebaseBeforeTicks`; if still locked → **return before FixedTick**.
5. `RunDispatcherLateFrame` previously **hard-returned** on the same lock **without** TryFlush → matches frozen `lateFrameTick` / `pumpFired`.
6. `HeadlessSimulationRunner.Update` already drains FO every tick via `HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks()`.
7. `H8_HeadlessPlayModeProbe` had **zero** FO drain calls (L16 only armed the clock). HSR parity gap = primary product root.
8. FO.Tick itself is blocked by the bootstrap lock (SceneRebaseTickLock chicken/egg); external TryFlush is the designed drain path.

### Demoted hypotheses (this dig)

| Hypothesis | Verdict |
|------------|---------|
| Sticky AUP pre-shift pause | DEMOTED. `AdvanceDispatcherFrameId` runs at the start of every `RunDispatcherUpdate`; AUP gate compares against advancing dispatcher frame id and cannot stick across frames. `ReleaseAupPreShiftPause` does not clear `_aupPreShiftPauseFrameId`, but frame mismatch exits the gate next frame. |
| INPUTHOP `frameId=0` sticky | NOT AUP evidence. `SystemDispatcher.CurrentFrameId` is `TimeSliceScheduler.CurrentFrameId`, not `_currentDispatcherFrameId`. AUP / master timing use `ResolveCurrentDispatcherFrameId` which does advance. |
| hop2 requires map enabled | FALSE. `GetState` only returns `_currentState` after DiagRecordReadObservation(2). Reader must call it; map gate is on TryReadFrame / ProcessPlayerInputFrame, not GetState itself. L16 already had inputEnabled=True. |
| Mock hop2 / driver FixedTick | REJECTED (product-only mandate). |

### Ranked residual after L16 (pre-fix)

1. **HIGHEST:** Origin bootstrap lock stuck + Probe missing FO drain + LateFrame no TryFlush.
2. MEDIUM: dilation/pause re-zero after SIMCLOCK.
3. MEDIUM: IsSimulationHalted sticky.
4. LOWER: HPM off fixed lane / Sample early-out only if Fixed DOES run.

## Fix (product, not mock)

### A) Probe — HSR-parity FO drain

File: `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs`

| Piece | Value |
|-------|--------|
| Method | `DrainProbeFloatingOriginBootstrap(reason)` |
| Call sites | gameplay-window-start, worlddriver-begin, every gameplay-tick (+ undriven path) |
| Core API | `HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks()` |
| Snapshot | `CopyBootstrapDrainSnapshot` + dispatcher lock/pause/dil/stepBound/gameReady |
| Evidence | `[H8_PLAYPROBE] FODRAIN reason=... flushClean=... foLock=... dispBoot=...` |
| Throttle | every 5s OR always when lock still held after drain |
| Reset | clear FO drain counters in `ResetRunState` |

### B) SystemDispatcher.LateFrame — TryFlush parity with RunDispatcherUpdate

File: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

Before LateFrame hard-return on bootstrap lock:

```
if (IsOriginShiftBootstrapLocked)
{
    if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())
        return;
    if (IsOriginShiftBootstrapLocked)
        return;
}
```

**Explicit non-goals (rejected):**

- Mock hop2 / forge INPUTHOP census
- Call `GetState` or `FixedTick` from WorldDriver or probe
- Unregister thrash / dual-register churn
- Treat WORLDDRIVER tick counts as FixedTick evidence
- Clear AUP pause field as primary fix (demoted)

## LIVE acceptance (Swim)

| Gate | Pass |
|------|------|
| FODRAIN log | at least one line; prefer `dispBoot=0` / `foLock=0` after drain |
| lateFrameTick / pumpFired | unfrozen (advancing across INPUTHOP samples) |
| hop2 | present in INPUTHOP census |
| `movementIntent01max` | `> 0` |
| SIMCLOCK | still `stepBoundAfter=1` (L16 must hold) |

If FODRAIN shows lock clearing but hop2 still absent → dig residual dilation re-zero / halt / HPM Sample early-out (L18), not more FO thrash.

## Related

- L16: step-bounded clock on Probe — necessary, not sufficient.
- Pattern source: `HeadlessSimulationRunner.Update` FO drain + `RunDispatcherUpdate` TryFlush before bootstrap return.
- FO API: `HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks` / `CopyBootstrapDrainSnapshot`.
