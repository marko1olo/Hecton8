# V0 L14 LIVE RESULTS — Player lane always dispatch + Sample intent

**HEAD probed:** `4dcb53307`  
**Probe:** `H8_HeadlessPlayModeProbe.Run` batchmode playmode  
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L14.log`  
**Artifact:** `Docs/AgentLogs/h8_playprobe_v0_L14.json`  
**PID:** 3976 (dead, run complete)  
**Date:** 2026-07-31  

## Verdict

| Gate | Result |
|------|--------|
| Boot / WorldLoad | PASS |
| Swim | **FAIL** |
| Resource / Tool / Craft / Mission | BLOCKED (downstream of Swim) |
| hop1 (CurrentInputState) | HEALTHY |
| hop2 (GetState) | **ABSENT** |
| movementIntent01max | **0.000** |
| immersionMax | 1.000 |
| depth span | 0.000 m |

**Do NOT claim Swim PASS.** L14 product changes shipped and were LIVE-probed; residual hop2/intent remains.

## What L14 shipped (pre-probe)

1. `SystemDispatcher.ShouldSkipLaneDuringBootstrap` — always `return false` (Player fixed lane never bootstrap-skipped).
2. `HectonPlayerMovement.SampleGameplayLocomotionInputForFixedStep` — publishes `_lastPlayerKinematicsIntendedMovement` from raw input **before** suit/juice gates so `CurrentMovementIntent01` can observe intent.
3. `TryRegisterToDispatchers` — sticky-only (no Unregister+Register thrash).

## LIVE signals (final extract)

| Signal | Value |
|--------|-------|
| lastOverrideMove | (0, 1) |
| currentStateMove | (0, 1) |
| overrideApplied | 6 → 57 (census progression) |
| publishOk | 510 → 553 |
| INPUTHOP census | readHop=**1 only** (obs=240 / 1200 / 3600) |
| hop2 | **ABSENT** on all censuses |
| movementIntent01max | **0.000** |
| input overrides (swim) | 41578 |
| input overrides (total) | 202180 |
| inputServiceRegistered | True |
| inputEnabled | True |
| switchToPlayerInputCalled | True |
| blockMask | 0 |
| pda / fab / pause | closed (driver force-close) |
| waitingOn | LocomotionHoldInProgress |
| RESULT failures | 1 |
| DETERMINISM | NeverSampled (dispatcherFrameId=0) — side note |
| PlayerKinematicsRuntime | EXISTS, enabled |

## Interpretation

- Driver → `TryPublishInputOverride` → `InputDispatcher._currentState` is healthy (hop1).
- hop2 is marked **only** inside `InputDispatcher.GetState()` via `DiagRecordReadObservation(2)`.
- hop2 never fires ⇒ `HectonPlayerMovement.FixedTick` → `Sample` → `ProcessPlayerInputFrame` → `TryReadFrame` → `GetState` path never ran (or never reached GetState).
- L14 Player-lane bootstrap unlock was **insufficient** alone: FixedTick still not consuming overrides.

## Residual root (→ L15)

Primary hypothesis after L15 source dig:

1. **Dual-register non-heal:** `GlobalRegistry.TryRegisterFixedTickable` returns false when global `_fixedTickables.Contains(item)` without healing `SystemDispatcher` Player fixed lane. Sticky `_registeredFixedTick` then stays false forever, or sticky true while lane empty.
2. HPM sticky trust without lane `Contains` check.
3. Soft-reset / Awake clears sticky flags without Unregister (exacerbates desync if re-register hits Contains).

See `V0_L15_DUAL_REGISTER_HEAL.md`.
