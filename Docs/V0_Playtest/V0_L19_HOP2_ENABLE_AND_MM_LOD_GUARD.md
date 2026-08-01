# V0 L19 — hop2 enable path + MapMagic ActiveTerrain/SwitchLod guard

**Status:** PRODUCT FIX SHIPPED (unmeasured until LIVE L19 probe).
**Prior LIVE:** `V0_L18_LIVE_RESULTS.md` — lateFrame PASS; hop1 PASS; hop2 ABSENT; MapMagic ActiveTerrain crash.
**Swim PASS criteria (unchanged):** hop2 PRESENT + `movementIntent01max > 0` on complete non-crash route.

---

## Product changes

### A) InputDispatcher — hop2 enable when automation override applied

**File:** `Assets/_Project/Scripts/Core/InputDispatcher.cs`

```csharp
public bool IsPlayerInputEnabled =>
    (_nativeInputManager != null && _nativeInputManager.IsPlayerInputEnabled)
    || _lastAutomationOverrideApplied;
```

- `_lastAutomationOverrideApplied` is already set in `CaptureState` after `ApplyAutomationOverride`.
- Hop2 consumers (`HectonPlayerInputHandler`, `HectonPlayerMovement`) gate GetState / ProcessPlayerInputFrame on `IsPlayerInputEnabled`.
- L18 proved hop1 writes non-zero move into `_currentState` while consumers stayed closed because native player map was false/null.
- Zero-GC; property-only widen; no interface change (`IInputService.IsPlayerInputEnabled` semantics widened on impl).

### B) InputDispatcher — SwitchTo* rebind from GlobalRegistry

**File:** same

- `SwitchToPlayerInput` / `SwitchToUIInput` call `TryEnsureNativeInputBound()` before forwarding.
- `TryEnsureNativeInputBound`: if `_nativeInputManager == null`, bind `GlobalRegistry.NativeInputRuntime` (skip self-reference).
- DEV once-warn `[H8_INPUTNATIVE]` if still null after rebind attempt.
- Stops L18 silent no-op: driver settle/swim called `SwitchToPlayerInput` every tick via `EnsureGameplayLocomotionInputReady` with no effect when bind was lost.

### C) MapMagic TerrainTile — live Terrain guards (L18 crash stack)

**File:** `Assets/MapMagic/Terrains/TerrainTile.cs`

L18 crash:

```
set_ActiveTerrain → GameObject.SetActive_Injected
SwitchLod → ApplyRoutine → CoroutineManager.Update → MapMagicObject.Update
```

Guards:

1. `IsLiveTerrain(Terrain)` — Unity fake-null + destroyed `gameObject`.
2. `SafeSetTerrainActive`  no `SetActive` on dead wrappers; skip if already desired `activeSelf`.
3. `ActiveTerrain` get/set use live checks.
4. `SwitchLod`: early-out if `mapMagic == null`; clamp non-finite `distance`; only select main/draft when terrain live; null-safe `objectsPool`; weld only when tiles non-null and terrain live.

Note: this MapMagic revision has no `DistToLod(float)` API — L18 stack was ActiveTerrain/SetActive, not DistToLod infinite-loop. Guards target the measured crash.

---

## Non-goals / hard rules

- No driver hop2 forge, no mocks, no FixedTick/GetState from probe.
- No new systems.
- Zero-GC hot paths preserved (property OR + null checks only).
- Do not claim Swim green without L19 LIVE measurement.

---

## LIVE validation recipe

```
Unity 6000.5.0f1
-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90
-executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run
NO -quit, NO -nographics
Log: Docs/AgentLogs/h8_playprobe_v0_L19.log
```

### Pass signals

| Signal | Pass |
|--------|------|
| No `Crash!!!` / no ActiveTerrain SetActive stack | required |
| INPUTHOP `lateFrameTick` advancing | keep |
| hop1 `currentStateMove` non-zero when override applied | keep |
| `readHop=2` or hop2 PRESENT / consumer path | **required for swim chain** |
| `movementIntent01max > 0` on Swim | **required for Swim PASS** |
| FODRAIN foLock=0 dil=1 | keep |

### Fail rank if still red

1. Still crash in MM → deepen teardown/stop-token vs ApplyRoutine race.
2. No crash, hop2 still 1 → dig HPM Fixed lane / Sample early-outs / gameReady (not IsPlayerInputEnabled — already OR'd).
3. hop2 ok, intent 0 → L14 Sample publish / kinematics.
