# V0 L18 — LIVE Results (late-frame lane heal + dil=1)

**Status:** CLOSED for late-frame residual — `lateFrameTick` ADVANCES multi-sample; hop1 healthy; Swim NOT closed.
**Product HEAD at LIVE (pre-L19):** `c3003a3b9` (lane-heal + dil=1) / `2f4eda518` (LateFrame during origin frame lock)
**Primary remote:** gitlab `main`
**Swim PASS criteria (unchanged):** hop2 PRESENT + `movementIntent01max > 0` on a complete non-crash LIVE route.

---

## Run

| Run | Log | Outcome |
|-----|-----|---------|
| **L18** | `Docs/AgentLogs/h8_playprobe_v0_L18.log` (1,277,384 B) | Multi INPUTHOP; lateFrame ADVANCING; hop1 PASS; hop2 ABSENT (`readHop=1` x3); **MapMagic ActiveTerrain/SetActive native crash** mid-gameplay before Swim/VERDICT |

Flags: `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90` — NO `-quit`, NO `-nographics`.

---

## What PROVED (L18 product)

### lateFrame lane heal = PASS

| Sample | `lateFrameTick` | `presimTick` | `pumpFired` | `regLateFrame` |
|--------|-----------------|--------------|-------------|----------------|
| obs=240 | **357** | 357 | 1 | True |
| obs=1200 | **373** | 373 | 1 | True |
| obs=3600 | **398** | 398 | 1 | True |

- L17 residual was lateFrame frozen@29 across samples. L18 multi-sample proves ticks **advance** (357→373→398).
- `TryRegisterToDispatcher` LateFrame/Slow Contains heal + PreSim inject + SystemDispatcher LateFrame-under-origin-lock are LIVE-proven for this residual.
- **DEMOTED:** late-frame starvation as the hop2 blocker on this run.

### FODRAIN = PASS (still clean under dil=1)

| Signal | Value |
|--------|-------|
| `gameplay-window-start` FODRAIN | `flushClean=1 foLock=0 dispBoot=0 dil=1` |
| gameplay-tick FODRAIN | `calls=1243 clean=1243 foLock=0` |
| FO lock sticky? | **NO** |

### hop1 (CaptureState → override → publish) = PASS

| Field | obs=240 | obs=1200 | obs=3600 |
|-------|---------|----------|----------|
| `readHop` | **1** | **1** | **1** |
| `overrideApplied` | 3 | 19 | 44 |
| `lastOverrideMove` | (0,1) | (0,1) | (0,1) |
| `currentStateMove` | (0,1) | (0,1) | (0,1) |
| `postMaskMove` | (0,1) | (0,1) | (0,1) |
| `blockMaskNonZero` | 0 | 0 | 0 |
| `publishOk` | 384 | 422 | 477 |
| `captureRan` | 353 | 369 | 394 |

Hop1 pipeline is healthy end-to-end: automation override lands in `_currentState`, block mask does not erase move, publish succeeds.

### dil=1 = PASS

- FODRAIN reports `dil=1` (probe `ProbeTimeDilationScalar` 100→1 shipped with L18).
- Not a temporal-compression artifact for this crash class.

---

## What FAILED / incomplete

### hop2 ABSENT

- All three INPUTHOP samples: `readHop=1` only.
- No `hop2=PRESENT` token; no consumer GetState/ProcessPlayerInputFrame census advance in the hop line.
- **Interpretation (product dig, not driver forge):** hop1 publishes non-zero move into dispatcher state, but hop2 consumers (`HectonPlayerInputHandler` / `HectonPlayerMovement`) gate on `IsPlayerInputEnabled`. When `_nativeInputManager` is null or player map is closed, that property stays false → consumers refuse GetState even though `_currentState` holds the override.
- Related product failure: `SwitchToPlayerInput()` silent no-op when `_nativeInputManager == null` while `GlobalRegistry.NativeInputRuntime` may still hold the bootstrap InputManager — driver settle/swim ticks call SwitchTo every frame to no effect.

### Crash root (blocks Swim window)

```
GameObject:SetActive_Injected
GameObject:SetActive
MapMagic.Terrains.TerrainTile:set_ActiveTerrain   (TerrainTile.cs ~122)
MapMagic.Terrains.TerrainTile:SwitchLod           (TerrainTile.cs ~227)
TerrainTile/<ApplyRoutine>d__59:MoveNext          (TerrainTile.cs ~796)
Den.Tools.Tasks.CoroutineManager:Update
MapMagic.Core.MapMagicObject:Update
Crash!!!
```

- Native mono/native crash via destroyed/dangling Terrain `GameObject.SetActive` during LOD switch after ApplyRoutine.
- Same MapMagic LOD family as L17b; stack is specifically **ActiveTerrain setter**, not PhysX (L17a).
- No SWIM / VERDICT / `movementIntent01max` / route artifact — crash before complete gameplay window.

### Missing this run

- hop2 PRESENT
- `movementIntent01max > 0`
- SWIM / VERDICT
- Complete non-crash route

---

## Residual rank for L19 (product-only)

1. **MapMagic `ActiveTerrain`/`SwitchLod` null-live guards** — crash blocks Swim; product fix in third-party TerrainTile path used in playmode.
2. **`IsPlayerInputEnabled` must OR `_lastAutomationOverrideApplied`** — hop2 consumers must read automation-written `_currentState` when native player map is closed.
3. **`SwitchToPlayerInput`/`SwitchToUIInput` rebind via `GlobalRegistry.NativeInputRuntime`** when `_nativeInputManager` null — stop silent no-op so native enable path can also become true.
4. Only after 1–3 LIVE: if hop2 PRESENT but `movementIntent01max=0` → Sample/kinematics intent path (L14 family).

## Hard rules (carry forward)

- No mocks, no hop2 forge, no FixedTick/GetState from driver.
- Product-only; feature without gameplay = DECLINED.
- Commit product+docs under `Docs/V0_Playtest/` only; never Tools/_cline_scratch or AgentLogs.
- Primary remote: **gitlab main**.
