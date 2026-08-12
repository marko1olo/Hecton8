# V0 L14 — Player fixed-lane dispatch + Sample intent metric

Status: **CODE SHIPPED / LIVE PROBE PENDING**  
HEAD base: `125221564` (L13.1 FQN)  
Date: 2026-07-31

## Problem (L13.1 LIVE residual)

| Signal | L13.1 LIVE |
|--------|------------|
| hop1 CurrentInputState | healthy (driver overrides) |
| lastOverrideMove / currentStateMove | (0,1) path OK |
| hop2 GetState (HPM TryReadFrame) | **ABSENT** |
| movementIntent01max | **0.000** |
| Swim | **FAIL** (`waitingOn=LocomotionHoldInProgress`) |

Joint symptom: hop2-absent **AND** intent=0. Suit-gate sample-before-suit (L13) was insufficient.

## Root causes (L14 critique)

1. **Player lane bootstrap skip (primary hop2)**  
   `SystemDispatcher.ShouldSkipLaneDuringBootstrap` returned true for `PriorityLayer.Player` while  
   `_runtimeGameplayBootstrapGateActive && BootstrapState.HasActiveInstance && !BootstrapState.IsGameReady`.  
   That starved `HPM.FixedTick` → `SampleGameplayLocomotionInputForFixedStep` → `ProcessPlayerInputFrame` →  
   `TryReadFrame` / `GetState` (hop2) even when `InputDispatcher` already held non-zero MoveDelta.

2. **Intent metric path (primary intent=0)**  
   `CurrentMovementIntent01` reads `_lastPlayerKinematicsIntendedMovement`, written only post-suit in  
   `PrepareTransportAndFrameState`. Sample filled `_input*` but not the kinematics intent field, so probe  
   `movementIntent01max` stayed 0 when suit early-out or transport never refined.

3. **Registration thrash (rejected anti-pattern)**  
   `RegistryBucket.TryRegister` returns **false** when `Contains(item)` — not idempotent-true.  
   WorldDriver calls `EnsureGameplayLocomotionInputReady` → `EnsureDispatcherRegistration` **every**  
   settle/swim hold tick. Unregister+Register every call would thrash the fixed lane mid-hold.  
   **Shipped:** sticky false → TryRegister once; sticky true → leave alone. Comments document why.

## Product fix

### A. `SystemDispatcher.ShouldSkipLaneDuringBootstrap`

Always `return false` (no longer skips Player lane during bootstrap gate).  
Player fixed/update locomotion is input-authoritative simulation, not optional bootstrap garnish.

### B. `HectonPlayerMovement.SampleGameplayLocomotionInputForFixedStep`

- After `ProcessPlayerInputFrame` + wipeout:  
  `_lastPlayerKinematicsIntendedMovement = ResolveRawInputIntentVector();`  
  (pre-suit; hop2 already ran inside ProcessPlayerInputFrame)
- Menu block path: zero `_lastPlayerKinematicsIntendedMovement` so metric tracks Sample, not stale transport.

### C. `TryRegisterToDispatchers` / Ensure path

- Sticky-only register (no Unregister thrash).  
- SD L14 is the dispatch unlock; registration remains first-success sticky.

## Files

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`

## LIVE acceptance (do NOT claim Swim PASS without these)

Probe flags (same family as L13):

```
-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120
-h8SettleSeconds 180 -h8GameplaySeconds 90
```

No `-quit`, no `-nographics`, no forceMenuLoad, no `-h8headless` as proof.

| Gate | Required |
|------|----------|
| hop2 | PRESENT in INPUTHOP census (`readHop_seen` includes GetState) |
| movementIntent01max | **> 0** |
| Swim | PASS only if hold completes with intent/hop2 healthy |
| depth | prefer non-zero span when locomotion applies |

## Explicit non-claims

- L14 code alone does **not** equal Swim PASS.
- Docs under `Docs/AgentLogs` are gitignored scratch; durable notes live in `Docs/V0_Playtest/`.

## Next

1. Commit + push main (product + this doc).
2. Kill leftover Unity; launch L14 LIVE probe.
3. Poll hop2 + movementIntent01max + Swim line.
4. If residual: L15 dig with subagents (discover/critique/write `.mem.json`).
