# V0 L16 LIVE Results — step-bounded sim clock on PlayModeProbe

**Date:** 2026-08-01  
**HEAD:** `e64fd2515` — `fix(v0): arm step-bounded sim clock on PlayModeProbe batchmode route (L16)`  
**Remote:** gitlab `main` (`9f4169ffd..e64fd2515`)  
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L16.log` (~1.61 MB, 16787 lines)  
**Artifact:** `Docs/AgentLogs/h8_playprobe_v0_L16.json`  
**Launch:** `Tools/_cline_scratch/launch_v0_L16_step_bounded_clock_probe.bat` PID=12424  
**Flags:** `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90` (NO `-quit`, NO `-nographics`, NO forceMenuLoad)

---

## Verdict

| Gate | LIVE | Notes |
|------|------|--------|
| SIMCLOCK armed `stepBoundAfter=1` | **PASS** | L16 product fix confirmed on probe route |
| hop1 `CurrentInputState` / `currentStateMove` nonzero | **PASS** | `(0,1)` stable; not a poll artifact |
| hop2 `GetState` (HPM FixedTick → TryReadFrame) | **FAIL** | `readHop=1` only (3 census lines); hop2 ABSENT |
| `movementIntent01max > 0` | **FAIL** | `0.000` |
| depth span | **FAIL** | `0.000..0.000` span=0m (immersionMax=1.000) |
| **Swim Required Route** | **FAIL** | sole FAIL row; RESULT failures=1 |

**Swim PASS is NOT claimed.** L16 closed the batchmode clock gap; residual is downstream of a healthy hop1 + armed fixed-step clock.

---

## SIMCLOCK (L16 proof)

```
[H8_PLAYPROBE] SIMCLOCK ensure reason=gameplay-window-start
  pausedBefore=0 dilBefore=0.9 dilAfter=100 pausedAfter=0
  stepBoundBefore=0 stepBoundAfter=1 stepBoundOk=1 stepDt=0.04 armed=1

[H8_PLAYPROBE] SIMCLOCK ensure reason=worlddriver-begin
  pausedBefore=0 dilBefore=100 dilAfter=100 pausedAfter=0
  stepBoundBefore=1 stepBoundAfter=1 stepBoundOk=1 stepDt=0.04 armed=1
```

Probe now mirrors `HeadlessSimulationRunner.EnsureHeadlessSimulationClock`: unpause + dilation 100 + `EnableStepBoundedTime(0.04f)`.

---

## Swim MOMENT (full)

```
FAIL Swim
  driver published 16016 input overrides
  movementIntent01max=0.000
  immersionMax=1.000
  depthSampled=True depth=0.000..0.000 span=0.000m
  oxygen 139.240->139.240 pressure 1.000->1.000
  vitalsFlags[o2=False pressure=False depth=False]
  inputServiceRegistered=True inputEnabled=True switchToPlayerInputCalled=True
  blockMask=0x00000000 pdaOpen=False fabOpen=False pauseOpen=False inputEnabledNow=True
  FAIL: the input path was open but the driver's MoveDelta never reached HectonPlayerMovement

  [SCHEDULE phase=SwimSurface wall=9.340s ticks=2 tickFloor=2 tickBox=61442
    granted=5.000s yield=Timeboxed waitingOn=LocomotionHoldInProgress]
  [SCHEDULE phase=SwimDive wall=8.043s ticks=16013 tickFloor=2 tickBox=86018
    granted=7.000s yield=Timeboxed waitingOn=LocomotionHoldInProgress]
```

### Phase timing note

| Phase | wall | ticks | sec/tick |
|-------|------|-------|----------|
| SwimSurface | 9.340s | **2** | ~4.67s |
| SwimDive | 8.043s | 16013 | ~0.001s |
| SwimVerdict | 0.009s | 1 | completed |

Surface phase barely ticked (2) while Dive flooded with driver ticks. Driver is an input producer, not the fixed-step clock — high Dive tick count ≠ HPM FixedTick evidence.

---

## INPUTHOP census

Only **hop=1** observed (3 lines at obs=240/1200/3600). No `readHop=2`.

Representative (obs=3600):

| Field | Value | Read |
|-------|-------|------|
| lateFrameTick | 49 | late-frame lane alive |
| pumpFired | 1 | self-pump ran |
| presimTick / presimSubsteps | 581 / 655 | PreSimulationInputTick active |
| captureRan | 571 | CaptureState running |
| overrideApplied | 35 | override lane consuming |
| lastOverrideMove | (0,1) | driver publish value |
| blockMaskNonZero | 0 | no menu/UI mask wipe |
| postMaskMove | (0,1) | survived mask |
| currentStateMove | **(0,1)** | hop1 healthy |
| currentInputStateFrame | 573 | state frame latched |
| publishOk | 574 | publish path OK |
| regLateFrame/SlowTick/InputService | True | registration OK |

**Pipeline-order reading:** first zero from the left that matters for Swim is **missing hop2** (GetState never called). hop1 is not the drop.

`GetState_hop2` extract only hit stack frames naming `DiagRecordReadObservation` (from hop1 path) — **no live hop2 observation lines**.

---

## Route MOMENTS summary

| Row | Status |
|-----|--------|
| Boot | PASS |
| WorldLoad | PASS |
| FirstExit | NOT_EXERCISED (content) |
| **Swim** | **FAIL** |
| Resource | BLOCKED |
| Tool | BLOCKED |
| CraftRepairBuild | BLOCKED |
| Mission | BLOCKED |
| Hazard | NOT_EXERCISED (content) |
| SaveLoad | PARTIAL |
| Proof | PARTIAL |

`pass=2 partial=2 fail=1 blocked=4 notExercised=2` of 11.

---

## What L16 proved / disproved

### Proved
1. PlayModeProbe can arm step-bounded time on the batchmode route (`stepBoundOk=1`).
2. With clock armed, hop1 remains healthy: `_currentState` holds driver MoveDelta `(0,1)`.
3. blockMask / PDA / fab / pause are **not** wiping input on this run (`blockMask=0`, all UI closed).
4. L15 "currentStateMove=(0,0)" was a **poll artifact** (confirmed again LIVE: real metric is `(0,1)`).

### Disproved as sole root
- "Probe never EnableStepBoundedTime ⇒ no FixedTick ⇒ no hop2" was necessary but **not sufficient**. Clock is now armed; hop2 still ABSENT; intent still 0.

### Residual (L17 dig target)
**HPM FixedTick → SampleGameplayLocomotionInput → ProcessPlayerInputFrame → TryReadFrame → GetState (hop2) never executes** (or never reaches DiagRecord hop=2), despite:

- armed step-bounded dispatcher clock
- healthy hop1 `_currentState=(0,1)`
- open blockMask / enabled input / switchToPlayerInputCalled

Candidate roots (priority order for L17):

1. **HPM not on fixed-step dispatch list** (registration / dual-register residual beyond L15 sticky lane).
2. **SampleGameplayLocomotionInput early-out before GetState** (menu-block helper, suit/fade, locomotion gate) — even when blockMask reports 0 on hop1 sample path.
3. **Fixed steps run but HPM.FixedTick not invoked** (TickManager / system order / player not in active sim set).
4. **GetState called on a different IInputService instance** than hop1 writer (registry split / cached NoOp) — log still shows early `Input slot READ BEFORE REGISTRATION` NoOp warning.
5. **movementIntent metric samples a different field** than hop2 write (less likely given prose: "never reached HectonPlayerMovement").

**Do NOT:** mock hop2, call GetState/FixedTick from WorldDriver, Unregister thrash, treat WORLDDRIVER ticks as FixedTick proof.

---

## Comparison vs L15

| Metric | L15 LIVE | L16 LIVE |
|--------|----------|----------|
| HEAD | 9f4169ffd dual-register | e64fd2515 + clock |
| SIMCLOCK stepBound | absent / N/A | **armed=1** |
| hop1 currentStateMove | (0,1) real | (0,1) real |
| hop2 | ABSENT | ABSENT |
| movementIntent01max | 0 | 0 |
| Swim | FAIL | FAIL |

L16 = clock gap closed; Swim residual unchanged in outcome.

---

## Next

1. Document handoff `NEXT_CHAT_L17.md`.
2. Dig L17 with subagents on FixedTick→GetState path (product-only).
3. Implement smallest product fix that makes hop2 fire under LIVE probe.
4. LIVE re-probe; Swim PASS only if hop2 present **and** `movementIntent01max>0`.
