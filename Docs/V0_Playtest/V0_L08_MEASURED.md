# V0 L08 MEASURED — Playable Loop Probe Ledger

**Date:** 2026-07-31  
**Probe:** `h8_playprobe_v0_L08`  
**HEAD at probe:** `2e0d5e3d3` (blackbox vault guard release P0)  
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L08.log` (~2.2MB)  
**JSON result:** MISSING (teardown crash)  
**Screenshots:** `Docs/Screenshots/V0_Playtest/` EMPTY — no PLAYER PNGs  
**Policy:** Feature without gameplay is DECLINED. No mocks. Real product fixes only.

---

## PASS / FAIL Matrix (L08 measured)

| Gate | Status | Evidence |
|------|--------|----------|
| Boot | PASS | Probe reached world/player attach |
| WorldLoad | PASS | World driver phases advanced |
| Input publish path open | PARTIAL | `publishOk=404/4420`; `publishGuardFail=4016` residue |
| SwitchToPlayerInput | PASS | `switchToPlayerInputCalled=True`, `inputEnabled=True`, `blockMask=0` |
| Swim / movementIntent01 | **FAIL** | `movementIntent01max=0`, final `readHop=0` despite mid-run `postMaskMove=(0,1)` |
| Tool slots available | **FAIL** | `available=0` BLOCKED |
| STARTERGRANT | **FAIL** | Deferred once with `refusalMask=0x1E` (bits 1–4) |
| Inventory gridBound | PARTIAL | `gridBound=True`, `inventoryVersion 0→0` (empty, not proof of live lanes) |
| Fauna encounter | UNPROVEN | Systems exist; route not exercised under L08 |
| Death / save bridge | PARTIAL | SaveLoad PARTIAL; death not route-proven |
| PLAYER PNGs V0-S01..S03 | **FAIL** | Zero screenshots captured |

---

## Root causes (product, not probe)

### 1. Swim intent zero — FIXED (product)

**Cause (~90–95% confidence):**  
`HectonPlayerMovement.ProcessPlayerInputFrame()` (samples `InputDispatcher.GetState` → `_inputH/_inputV/_inputVertical`) ran **only** on render `IUpdatable.Tick`.  
`FixedTick` applied locomotion/kinematics from stale `_input*` and **never** called `ProcessPlayerInputFrame`.

Batchmode / physics-heavy probe floods `FixedTick` while render `Tick` rarely runs → `CurrentMovementIntent01` stays 0 even when publish path is open (`publishOk>0`).

**Call chain (intended):**  
`Publish` / automation override → `InputDispatcher` state → `TryReadFrame` → `_inputH/_inputV` → `ResolveRawInputIntentVector` → `_lastPlayerKinematicsIntendedMovement` → `CurrentMovementIntent01`.

**Product fix applied (this session, pre-commit):**
- New `SampleGameplayLocomotionInputForFixedStep()` ~L8208  
- Called at start of `FixedTick` ~L9932 before `PrepareTransportAndFrameState`  
- Look/juice/cursor remain render-Tick owned; menu zeros locomotion on fixed path only  

**File:** `Assets/_Project/Scripts/HectonPlayerMovement.cs`

### 2. STARTERGRANT / tool slots — FIXED drive path (product); residual vault risk

**Cause:**  
`RetryRuntimeStartToolGrantIfPending` ran only from render `Tick` → same batchmode starvation.  
Early grant hit `CanServiceItemAdds=false` → `DescribeAddRefusalMask=0x1E`:
- bit1 gridMissing  
- bit2 stackLaneDead  
- bit3 simStackLaneDead  
- bit4 simOccupancyLaneDead  

Vault lanes can go stale after other vault allocs (meta.Version stamp); recovery is all-or-nothing via `TryRecoverRuntimeStorageCold`.

**Product fix applied (this session, pre-commit):**
- `PlayerToolManager` implements `IFixedTickable`  
- Registers/unregisters via `GlobalRegistry.TryRegisterFixedTickable` / `UnregisterFixedTickable`  
- `FixedTick` calls `RetryRuntimeStartToolGrantIfPending()` (also still from Tick)  

**File:** `Assets/_Project/Scripts/PlayerToolManager.cs`

**Residual risk for L09:** If vault never becomes `CanServiceItemAdds=true` after recover, grant still fails. FixedTick only multiplies retry opportunities — does not invent live lanes. Watch L09 for `STARTERGRANT applied` + STORAGE lines + `IsToolAvailableInSlot≥1`.

### 3. Secondary (not fixed this commit)

- `publishGuardFail=4016` still high (lock conflicts) — separate from intent-zero when `publishOk>0`.  
- PNG capture path never proven under V0 probe.  
- JSON missing after crash teardown.

---

## Architectural note (architect)

V0 probe is physics/batchmode-heavy. **Any gameplay that only hooks `IUpdatable.Tick` is effectively unwired in the probe** (and weak under catch-up frames in real play). Two systems hit this class of bug in L08: movement input sampling + tool grant retry.

Diagnostics showing “path open” without fixed-step consumers produced false confidence.

`gridBound=True` + `inventoryVersion=0` means empty inventory, **not** healthy service lanes. `NativeArray.IsCreated` can flip false after other vault allocs without a callback.

---

## Implemented but NOT gameplay-integrated (DECLINED until proven)

| Feature | Code exists | Wired into spawn→swim→tools→fauna→death/save |
|---------|-------------|-----------------------------------------------|
| Fauna AI / spawn | Yes | Route not proven under probe |
| Death bridge | Yes | Partial; not end-to-end proven |
| Save/Load | Partial | SaveLoad PARTIAL only |
| Screenshot / V0-S01..S03 | Pipeline present | Zero PNGs |
| FirstExit / Hazard | Content-gated | Blocked |
| Craft / loot | Upstream | Blocked on tools/loot spawn |
| Tick-only systems under batchmode | Many | Effectively unwired in probe |

---

## Verify (pre-commit)

| Check | Result |
|-------|--------|
| HPM braces | delta 0 |
| HPM `SampleGameplayLocomotionInputForFixedStep` | def L8208, call L9932, count=2 |
| PTM braces | delta 0 |
| PTM `IFixedTickable` | class + register + unregister + FixedTick |
| PTM `RetryRuntimeStartToolGrantIfPending` from FixedTick | yes (Tick + FixedTick) |

---

## L09 acceptance (real game, no mocks)

1. `movementIntent01max > 0` during Swim hold  
2. `IsToolAvailableInSlot` ≥ 1 after runtime start; STARTERGRANT applied (not stuck 0x1E)  
3. Fauna / death / save: log evidence of route exercise  
4. PLAYER PNGs under `Docs/Screenshots/V0_Playtest/` V0-S01..S03 regenerated and analyzed  
5. RESULT JSON present or explicit teardown reason documented  

---

## Commit allowlist (this wave)

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`  
- `Assets/_Project/Scripts/PlayerToolManager.cs`  
- This ledger  

**DO NOT commit:** Desktop scratch, `_agent_*`, `_eco_*`, `_slice*`, `HeadlessSimulationRunner.cs` local dirty unless reviewed as intentional product.
