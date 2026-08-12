# V0 L15 — Dual-register heal + HPM lane membership ensure

**Status:** CODE SHIPPED (this commit) — LIVE probe required before Swim PASS.  
**Parent FAIL:** L14 LIVE (`4dcb53307`) hop2 ABSENT, movementIntent01max=0.  
**Product-only:** no mocks, no fake hop2, no Unregister+Register thrash every Ensure tick.

## Discover

### INPUTHOP chain (unchanged truth)

```
Driver PublishLocomotionIntent
  → CoreDeterminismSignals.TryPublishInputOverride
  → InputDispatcher.CaptureState ApplyAutomationOverride → _currentState
  → hop1 = CurrentInputState (driver SampleObservables)     [L14 LIVE: HEALTHY]
  → HPM FixedTick → SampleGameplayLocomotionInputForFixedStep
  → ProcessPlayerInputFrame → HectonPlayerInputHandler.TryReadFrame
  → inputService.GetState() → DiagRecordReadObservation(2)   [L14 LIVE: ABSENT]
  → movementIntent01 via _lastPlayerKinematicsIntendedMovement (L14 Sample publish)
```

hop2 is **only** marked in `InputDispatcher.GetState()`.  
`HectonPlayerInputHandler` has no local hop mark.

### Why FixedTick may never run

Registration is dual:

1. `GlobalRegistry._fixedTickables.TryRegister(item)`
2. `SystemDispatcher.Register(item, layer)` → `GetFixedLane(layer).TryRegister(item)`

**Pre-L15 bug:** if step 1 returns false because `Contains(item)`, the method returned false **without** attempting step 2. A desync where global has HPM but the Player fixed lane does not is permanent.

Desync sources:

- `SystemDispatcher` lane `Clear()` without clearing `GlobalRegistry` buckets.
- HPM `Awake` soft-reset sets `_registeredFixedTick = false` **without** `UnregisterFixedTickable` — next Ensure hits global Contains → false → sticky stays false forever.
- Failed second register historically rolled back global; partial failures + later lane clears widen the split.

L14 sticky-only Ensure trusted sticky true without verifying lane membership, and sticky false could never heal past global Contains.

## Critique (ranked)

| Rank | Hypothesis | Live fit |
|------|------------|----------|
| 1 | Dual-register non-heal + sticky stuck | hop2 ABSENT, hop1 healthy, Ensure called every settle/swim tick, L14 lane skip already false |
| 2 | Menu block early-out before GetState | Less likely — driver force-closes PDA/Fab/Pause, blockMask=0 |
| 3 | dilatedDeltaTime<=0 / no fixed steps | DETERMINISM dispatcherFrameId=0 NeverSampled is a clue; secondary |
| 4 | Wrong input service without hop2 diag | Contradicted by hop1 on real InputDispatcher |

## Product fix (this change)

### A. `GlobalRegistry.TryRegisterFixedTickable` / `TryRegisterUpdatable` / `TryRegisterColdTickable`

```
addedToGlobal = bucket.TryRegister(item)
if !added && !bucket.Contains → false
if dispatcherLane.Contains → true   // already healthy
if !SystemDispatcher.Register → rollback only if addedToGlobal; return false
return true
```

Heals global-has / lane-missing without thrashing Unregister every call.

### B. `HectonPlayerMovement.TryRegisterToDispatchers`

- If sticky true but `SystemDispatcher.GetFixedLane(Player).Contains(this)` false → sticky = false.
- Same for Update + Cold lanes.
- Then `TryRegister*` as before.
- Still **no** Unregister+Register every driver Ensure tick.

### Rejected

- Unregister+Register thrash on every Ensure (driver hot path).
- Mock hop2 / driver calling GetState to fake census.
- Fake movementIntent01 without FixedTick Sample.

## Expected LIVE signals after L15

| Signal | Target |
|--------|--------|
| hop2 | PRESENT (readHop≥2 on INPUTHOP census during swim hold) |
| movementIntent01max | > 0 |
| lastOverrideMove / currentStateMove | remain (0,1) or non-zero |
| Swim | PASS only if live numbers confirm depth/intent gates |

## Files

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- Docs: this file, `V0_L14_LIVE_RESULTS.md`, `NEXT_CHAT_L15.md`
