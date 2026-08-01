# V0 L18 — LateFrame lane Contains heal + probe dil stability

**Status:** CODE SHIPPED on gitlab `main` (`2f4eda518` + `c3003a3b9`) — LIVE probe required before Swim PASS.  
**Parent FAIL:** L17b LIVE (`358089b6f`) — FODRAIN PASS, SIMCLOCK PASS, hop2 ABSENT ×3, `lateFrameTick` frozen@29 while `presimTick` advanced, MapMagic crash under dil=100.  
**Product-only:** no mocks, no fake hop2, no Unregister+Register thrash every tick, no driver GetState/FixedTick.

## Discover

### Smoking gun (L17b INPUTHOP ×3)

| Signal | Behavior |
|--------|----------|
| hop1 `currentStateMove` | (0,1) healthy |
| hop2 | ABSENT all samples |
| `lateFrameTick` | **frozen @29** |
| `pumpFired` | **frozen @1** |
| `presimTick` | **advances** 340→359 |
| `regLateFrame` | True (sticky — not membership) |
| foLock / dispBoot | 0 (FO DEMOTED) |
| blockMask / menu | 0 (menu DEMOTED) |

### Why PreSim advances while LateFrame freezes

```
RunDispatcherUpdate:
  PreSimulationInputTick  ← DIRECT IInputDeterminismService call (before FO/halt gates)
  ... FO / halt gates ...
  FixedTick lanes         ← AFTER gates; empty after ClearAllLanes until healed
RunDispatcherLateFrame:
  ... FO / halt gates ...
  ILateFrameTickable lane ← AFTER gates; empty after ClearAllLanes until healed
```

`lateFrameTick` increments **only** in `InputDispatcher.LateFrameTick` via `RunDispatcherLateFrame` walking the Core late-frame lane. Counters are **instance fields** — freeze is real.

### Root: sticky without Contains heal (L15 Fixed parity gap)

Pre-L18 `InputDispatcher.TryRegisterToDispatcher`:

- Sticky `_registeredLateFrame` / `_registeredSlowTick` only.
- No `GetLateFrameLane(Core).Contains(this)` heal (unlike HPM L15 Fixed).
- After `ClearAllLanes` / Bootstrap soft-reset: sticky stays true, lane empty → never re-TryRegisters.
- `regLateFrame=True` in census is **not** proof of lane membership.

HPM L15 already healed Fixed/Tick/Cold via Contains; HPM LateFrame and InputDispatcher LateFrame/Slow did not.

### Stability companion residual

`ProbeTimeDilationScalar=100f` + `EnableStepBoundedTime(0.04)` + Fixed 0.02 MaxSubsteps 3 ⇒ ~4s dilated dt vs 0.06s fixed cap (temporal compression). L17a PhysX AABB; L17b MapMagic LOD. Step-bound already arms Fixed dt for hop2 — dil=100 is not required for Swim hop2 and destabilizes LIVE.

## Critique (ranked)

| Rank | Hypothesis | Live fit |
|------|------------|----------|
| 1 | InputDispatcher LateFrame sticky desync after ClearAllLanes | lateFrame frozen + presim advances + hop2 ABSENT + FO clean |
| 2 | dil=100 stability crashes | L17a PhysX + L17b MapMagic both under dil=100 |
| 3 | Origin frame lock skips LateFrame walk | intermittent; FO clean at sample time but still product gap |
| 4 | HPM Fixed empty until Ensure | WorldDriver calls Ensure; L15 heal exists — secondary once LateFrame/Fixed membership restored |
| — | Menu / FO lock / missing clock | DEMOTED L17 |

## Product fix (shipped)

### A. `InputDispatcher.TryRegisterToDispatcher` — Contains heal

```
if (_registeredLateFrame && !GetLateFrameLane(Core).Contains(this))
    _registeredLateFrame = false;
if (!_registeredLateFrame)
    _registeredLateFrame = TryRegisterLateFrameTickable(this, Core);
// same for Slow + GetSlowLane
```

L15 HPM Fixed parity. No Unregister thrash.

### B. `InputDispatcher.PreSimulationInputTick` — heal inject

Every PreSim (still runs after ClearAllLanes) calls `TryRegisterToDispatcher()` so LateFrame/Slow membership heals on the hot path that already proves alive (presim advances).

### C. `HectonPlayerMovement` LateFrame — Contains heal

Same pattern on Player late-frame lane (`GetLateFrameLane(Player).Contains`).

### D. `H8_HeadlessPlayModeProbe` — dil 100→1

`ProbeTimeDilationScalar = 1f`. Keep `EnableStepBoundedTime(0.04)`. Comment: step-bound supersedes dil for FixedTick arming; dil=100 caused L17 crashes. HSR may still use 100f separately — probe-only change.

### E. `SystemDispatcher.RunDispatcherLateFrame` — origin frame lock (`2f4eda518`)

Run `ILateFrameTickable` during origin shift frame lock so InputDispatcher LateFrame is not starved when FO holds the frame lock (companion to L17 TryFlush).

### Rejected

- Mock hop2 / driver GetState / driver FixedTick.
- Unregister+Register every Ensure tick.
- Treating WORLDDRIVER ticks as FixedTick evidence.
- Leaving dil=100 for "faster" probe at cost of PhysX/MapMagic crashes.

## Commits

| SHA | Change |
|-----|--------|
| `2f4eda518` | SystemDispatcher: ILateFrameTickable during origin shift frame lock |
| `c3003a3b9` | InputDispatcher LateFrame/Slow heal + PreSim inject; HPM LateFrame heal; Probe dil=1 |

## Expected LIVE signals (L18)

| Signal | Target |
|--------|--------|
| FODRAIN foLock/dispBoot | 0 (hold L17 PASS) |
| SIMCLOCK stepBoundAfter | 1 (hold L16 PASS) |
| dil | **1** (not 100) |
| `lateFrameTick` | **advances** across INPUTHOP samples |
| hop2 | **PRESENT** at least once (ideally multi-sample) |
| `movementIntent01max` | **> 0** |
| Crash | none through Swim/VERDICT |
| Swim | PASS only if hop2 + intent on complete route |

## Swim gate

Do **not** mark Swim PASS until LIVE proves hop2 PRESENT and `movementIntent01max > 0` without crash. Feature without gameplay = DECLINED.
