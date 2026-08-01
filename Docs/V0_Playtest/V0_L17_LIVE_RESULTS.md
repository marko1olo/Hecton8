# V0 L17 — LIVE Results (FO bootstrap drain)

**Status:** CLOSED for FO residual — FODRAIN product path PROVEN clean both runs; Swim NOT closed.  
**Product HEAD at LIVE:** `358089b6f` — `fix(v0): drain FO bootstrap lock on PlayModeProbe + LateFrame TryFlush (L17)`  
**Docs/L18 follow-on HEAD:** `c3003a3b9` (lane-heal + dil=1) / `2f4eda518` (LateFrame during origin frame lock)  
**Primary remote:** gitlab `main`  
**Swim PASS criteria (unchanged):** hop2 PRESENT + `movementIntent01max > 0` on a complete LIVE route.

---

## Runs

| Run | Log | Outcome |
|-----|-----|---------|
| **L17a** | `Docs/AgentLogs/h8_playprobe_v0_L17.log` (preserved as `h8_playprobe_v0_L17a_crash.log`) | Mid-gameplay **native PhysX crash** before Swim/VERDICT/artifact |
| **L17b** | `Docs/AgentLogs/h8_playprobe_v0_L17b.log` (876046B) | Multi INPUTHOP; FO clean; lateFrame FROZEN; hop2 ABSENT x3; **MapMagic LOD crash** before Swim |

Flags (both): `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90` — NO `-quit`, NO `-nographics`.

---

## L17a — what PROVED

### FODRAIN = PASS (product drain works)

| Signal | Value |
|--------|-------|
| `gameplay-window-start` FODRAIN | `flushClean=1 foLock=0 dispBoot=0 dil=100 stepBound=1` |
| gameplay-tick FODRAIN | `calls=243 clean=243 foLock=0 dispBoot=0` |
| FO lock sticky this run? | **NO** — lock never held after drain |
| SIMCLOCK | `stepBoundAfter=1` (both arms) |

**Verdict:** L17 FO product fix is LIVE-proven for drain cleanliness. Origin bootstrap lock is **DEMOTED** as the residual for hop2 on this run (foLock/dispBoot always 0 while gameplay advanced).

### INPUTHOP (single sample before crash)

Only **one** INPUTHOP line (~line 11440). Key fields:

| Field | Value | Note |
|-------|-------|------|
| `readHop` | **1** | hop1 census only in this sample |
| `hop2=` | **absent from line** | no `hop2=PRESENT/ABSENT` token parsed |
| `lateFrameTick` | **33** | >0 (unfrozen vs L16 frozen-at-49 across samples — only one sample though) |
| `pumpFired` | **1** | same as L16 single-fire pattern at sample time |
| `presimTick` | 338 | advancing |
| `currentStateMove` | **(0,1)** | hop1 healthy |
| `overrideApplied` | 7 | override lane alive |
| `overrideRejected` | 322 | expected noise (empty publishes) |
| `publishOk` | 368 | driver publish healthy |
| `regLateFrame` | True | registration OK |
| `inputEnabled` path | healthy hop1 | blockMask=0 |

**Cannot claim hop2 PRESENT or ABSENT with confidence** — census incomplete (one sample, crash before multi-sample + Swim). `readHop=1` is consistent with hop2 never observed by the census window, not proof FixedTick never ran.

### Crash root (L17a)

```
Crash!!!
physx::Sq::IncrementalAABBTree::remove
physx::Sq::IncrementalAABBTree::updateFast
physx::Sq::IncrementalAABBPrunerCore::updateObject
...
PhysicsManager::Simulate
FixedUpdatePhysicsFixedUpdateRegistrator
ExecutePlayerLoop
```

- Native PhysX scene-query AABB pruner crash during `PhysicsManager::Simulate` (FixedUpdate lane).
- Crash handler wrote under `%LOCALAPPDATA%/Temp/Unity/Editor/Crashes`.
- Log tail is BurstCache / stack dump; no managed Swim/VERDICT/artifact JSON.
- **Not** caused by FODRAIN itself (foLock already 0; 243 clean drains). Likely unstable collider/scene-query state under high dilation (100×) + batchmode playmode — separate stability residual.

### Missing this run

- No `movementIntent01max` in log
- No SWIM / VERDICT
- No route artifact JSON
- Multi-sample INPUTHOP hop2 timeline
- Full 90s gameplay window

---

## L17b — CLOSED evidence (same product HEAD `358089b6f`)

### FODRAIN = PASS again

| Sample | foLock | dispBoot | gameReady | notes |
|--------|--------|----------|-----------|-------|
| gameplay-window-start | 0 | 0 | 0 | flushClean=1 dil=100 stepBound=1 |
| gameplay-tick early | 0 | 0 | 0 | calls=4 |
| gameplay-tick late | 0 | 0 | **1** | calls=16254 |

FO bootstrap lock **DEMOTED** for hop2 residual at sample time (always clean while hop2 stayed ABSENT).

### SIMCLOCK = PASS

| Arm | dilBefore→After | stepBoundBefore→After | stepDt |
|-----|-----------------|----------------------|--------|
| gameplay-window-start | 0.9→100 | 0→1 | 0.04 |
| worlddriver-begin | 100→100 | 1→1 | 0.04 |

### INPUTHOP ×3 — hop2 ABSENT, lateFrame FROZEN

| # | obs | lateFrameTick | pumpFired | presimTick | currentStateMove | overrideApplied | frameIndex |
|---|-----|---------------|-----------|------------|------------------|-----------------|------------|
| A | 240 | **29** | **1** | 340 | (0,1) | 20 | 343 |
| B | 1200 | **29** | **1** | 348 | (0,1) | 28 | 351 |
| C | 3600 | **29** | **1** | 359 | (0,1) | 39 | 362 |

- **readHop=1** all three; hop2 token **ABSENT** (never PRESENT).
- **lateFrameTick frozen @29** while **presimTick advances** 340→359 — PreSim is a direct `IInputDeterminismService` call (survives empty LateFrame lane); LateFrame is Core `ILateFrameTickable` lane only.
- `regLateFrame=True` is **NOT** membership proof (sticky flag after `ClearAllLanes`).
- hop1 healthy (`currentStateMove=(0,1)`); blockMask=0; driver publish alive.
- No SWIM / VERDICT / `movementIntent01max` / artifact — route crashed before Swim.

### Crash root (L17b) — NOT PhysX AABB

```
MapMagic.Terrains.TerrainTile.SwitchLod
... set_ActiveTerrain / SplatMaterials dtor / Update
```

- Managed/native crash under MapMagic LOD switch during dilated Update path.
- L17a was PhysX IncrementalAABBTree @ FixedUpdate; L17b is MapMagic LOD — both under **dil=100** + stepBound=0.04 temporal compression.
- Last FODRAIN pre-crash: `gameReady=1 foLock=0 dil=100 calls=16254`.

### Menu path DEMOTED

- Log: **0** hits for `IsAnyOpen` / `IsGameplayInputBlockedByMenu`.
- WorldDriver EnsureGameplay closes menus; INPUTHOP `blockMask=0`.
- Menu is not the hop2 residual on L17b.

### Missing this run

- hop2 PRESENT
- `movementIntent01max > 0`
- SWIM / VERDICT / route artifact
- Complete non-crash 90s gameplay window

---

## Residual ranking after L17a+b (FO + menu + clock DEMOTED)

1. **HIGHEST (→ L18):** InputDispatcher LateFrame/Slow sticky desync after `ClearAllLanes` — sticky `_registeredLateFrame=true` + empty Core late-frame lane ⇒ `lateFrameTick` frozen while PreSim advances; Fixed lane similarly empty until healed ⇒ hop2 starves. `regLateFrame=True` is not membership proof. L15 HPM Fixed already had Contains heal; InputDispatcher LateFrame did not.
2. **HIGH stability:** dil=100 + stepBound=0.04 causes ~4s dilated dt vs 0.06s fixed cap → PhysX L17a / MapMagic L17b crashes. Product-valid mitigation: probe dil→1.0 keep stepBound=0.04 (L16 already arms Fixed dt).
3. **MEDIUM:** other LateFrame early-outs not FO (intermittent origin frame lock — addressed in L18 SystemDispatcher path).
4. **MEDIUM/LOW:** `gameReady` late 0→1 — not alone explanatory (hop2 ABSENT across samples including after gameReady=1 path timing).
5. **DEMOTED:** FO bootstrap lock (FODRAIN clean both runs); L16 step-bounded clock (SIMCLOCK PASS); menu block (0 log hits, blockMask=0).
6. **REJECTED:** mock hop2, driver-called GetState/FixedTick, Unregister thrash.

---

## Swim gate

| Gate | L17a | L17b |
|------|------|------|
| FODRAIN clean | PASS | PASS |
| SIMCLOCK stepBound | PASS | PASS |
| hop2 PRESENT | UNKNOWN (truncated) | **FAIL ABSENT ×3** |
| lateFrame advancing | single sample only | **FAIL frozen@29** |
| movementIntent01max > 0 | UNKNOWN (truncated) | **FAIL (no signal)** |
| complete non-crash route | FAIL PhysX | FAIL MapMagic |
| **Swim** | **FAIL / incomplete** | **FAIL / incomplete** |

**Do not mark Swim PASS** until hop2 + intent on a complete non-crashed route.

## Follow-on

→ **L18** product: LateFrame/Slow Contains heal (InputDispatcher + HPM LateFrame) + PreSim re-TryRegister + Probe dil 100→1 + SystemDispatcher LateFrame during origin frame lock.  
Docs: `V0_L18_LATEFRAME_LANE_HEAL.md`, `NEXT_CHAT_L18.md`.
