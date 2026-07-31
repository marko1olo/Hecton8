# V0 L09 MEASURED — Playable Loop Probe Ledger

**Date:** 2026-07-31  
**Probe:** `h8_playprobe_v0_L09`  
**HEAD at probe:** `c06bb3957` (headless clock re-assert; L08 product FixedTick sample/grant already on main)  
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L09.log` (~2.3MB)  
**JSON result:** `Docs/AgentLogs/h8_playprobe_v0_L09.json` (present)  
**Screenshots:** PLAYER PNGs still not accepted as route proof this loop  
**Policy:** Feature without gameplay is DECLINED. No mocks. Real product fixes only.

---

## PASS / FAIL Matrix (L09 measured)

| Gate | Status | Evidence |
|------|--------|----------|
| Boot | PASS | `allSystemsReady=True gameReady=True` Dispatcher/TickManager/Save/ObjectPool live; scene `02_HECTON_WORLD` activation Complete |
| WorldLoad | PASS | Gameplay scene finished loading after ~10s; menu unload background |
| Input publish path open | PASS (stronger than L08) | `publishAttempt` high; `publishGuardFail=0`; `overrideApplied` 3→15; `postMaskMove=(0,1)`; `currentStateMove=(0,1)` |
| SwitchToPlayerInput | PASS | `switchToPlayerInputCalled=True`, `inputEnabled=True`, `blockMask=0x00000000` |
| INPUTHOP census | **FAIL residual** | Final `readHop=1` only (driver `CurrentInputState`); **never** `readHop=2` (`GetState`) during swim hold |
| Swim / movementIntent01 | **FAIL** | `movementIntent01max=0.000` despite `immersionMax=1.000`, open input path, ~97k overrides |
| Tool slots available | **BLOCKED** | `slotCount=4`, `IsToolAvailableInSlot=false` all slots; inventory `gridBound=True` version 0→0 |
| STARTERGRANT | **FAIL residual** | Deferred once `refusalMask=0x1E` (bits 1–4 vault lanes); retry was Tick/FixedTick-registration incomplete pre-fix |
| Resource | **BLOCKED** | Deplete reached then loot-spawn rollback (`TakeDamage` failed-TrySpawnLoot path) |
| CraftRepairBuild | **BLOCKED** | Fabricator live `visibleRecipes=0`; upstream Resource delivered nothing |
| Mission | **BLOCKED** | 12 quests authored, autoActivated=2, completions=0 |
| FirstExit | NOT_EXERCISED | No life-pod / drop-pod prefab authored in scenes |
| Hazard | NOT_EXERCISED | No hazard AddComponent sites / placement |
| SaveLoad | PARTIAL | Save half wrote files under LocalLow; LOAD half not exercised |
| Proof | PARTIAL | JSON + phase clock table written; no comparable state hash this run |
| PLAYER PNGs V0-S01..S03 | **FAIL** | Not accepted as route proof this loop |

**Route summary:** `pass=2 partial=2 fail=1 blocked=4 notExercised=2` of 11 Required rows. Exit reflects Swim FAIL.

---

## Root causes (product, not probe)

### 1. Swim intent zero after L08 FixedTick sample — residual hop2 starve — FIXED (product, this session)

**Measured:**
- L08 fix landed: `SampleGameplayLocomotionInputForFixedStep` runs on HPM `FixedTick`.
- L09 still: publish path open, `inputEnabled=True`, `blockMask=0`, `postMaskMove=(0,1)`, but `readHop` stays **1** and `movementIntent01max=0`.
- `InputDispatcher.GetState` always records hop2 if called. Only gameplay consumer on the movement path is `HectonPlayerInputHandler.TryReadFrame` → `GetState` via `ProcessPlayerInputFrame`.
- `SampleGameplayLocomotionInputForFixedStep` short-circuits on `IsGameplayInputBlockedByMenu` **before** `ProcessPlayerInputFrame` (zeros locomotion, no GetState).
- `IsGameplayInputBlockedByMenu` = Fabricator open \|\| PDA open \|\| `PauseMenuController.IsAnyOpen`.
- PDA / Fabricator static open flags **do** reset on `SubsystemRegistration`.
- `PauseMenuController.IsAnyOpen` => `_openMenuCount > 0`.
- `ResetActiveRuntimeForSubsystemRegistration` previously only cleared `ActiveRuntimeInstance` — **did not** zero `_openMenuCount`.
- Editor: `m_EnterPlayModeOptionsEnabled=1`, `m_EnterPlayModeOptions=1` → **domain reload disabled** → sticky statics across play sessions are a live product class.
- Confidence: high that sticky pause open-count is the hop2 killer under this editor config; immersionMax=1 does **not** prove GetState ran (water immersion updates on FixedTick independently of SampleGameplay GetState).

**Product fix applied (this session):**
- `PauseMenuController.ResetActiveRuntimeForSubsystemRegistration` now sets `_openMenuCount = 0` alongside `ActiveRuntimeInstance = null`.
- File: `Assets/_Project/Scripts/UI/PauseMenuController.cs`

**Not a mock:** restores real menu-open accounting so gameplay input sampling can call GetState when no menu is open.

### 2. STARTERGRANT / tool slots — FixedTick registration hole — FIXED (product, this session)

**Measured:**
- L08 moved `RetryRuntimeStartToolGrantIfPending` onto PTM `FixedTick`.
- L09 still deferred once with `refusalMask=0x1E` and tools never became available.
- `PlayerToolManager.TryRegisterToTickManager` early-out required only `_registeredToTick && _registeredToLateFrame` — if those two were already true, **FixedTick registration was skipped forever**.
- Same hole on unregister early-out (could leave FixedTick registered / skip cleanup symmetrically).

**Product fix applied (this session):**
- Register early-out now requires all three: `_registeredToTick && _registeredToLateFrame && _registeredToFixedTick`.
- Unregister early-out requires all three flags false.
- File: `Assets/_Project/Scripts/PlayerToolManager.cs`

**Residual risk (inventory vault 0x1E):** if FixedTick retry now runs and vault lanes are still dead after recover, a separate inventory vault bind/recover product fix may still be required. L10 must measure grant after this registration fix before further vault surgery.

### 3. Headless day clock under batchmode WallClock — FIXED (product, companion)

**Cause:** batchmode often yields `Time.unscaledDeltaTime == 0`, so FastTick never accumulates dayAcc despite dilation=100.

**Product fix (unstaged companion in this working tree):**
- `HeadlessSimulationRunner.EnsureHeadlessSimulationClock` arms `SystemDispatcher.EnableStepBoundedTime(0.04f)` (real dispatcher time source, InternalsVisibleTo QA).
- File: `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs`

Not a mock: does not write day counters; supplies real unscaled dt so ecology day rows can advance.

---

## Architect answers (required)

| Question | Answer |
|----------|--------|
| **Least confident claim** | Vault `refusalMask=0x1E` root timing + PNG capture path. Grant may still fail after FixedTick registration if vault lanes stay dead; screenshot pipeline was not re-proven this loop. |
| **Biggest missing coverage** | Tick-only systems starve under batchmode / FixedTick-heavy probe. Any gameplay that still samples only on render `IUpdatable.Tick` (or depends on hop2 while a sticky menu static blocks SampleGameplay) will look “alive” in dispatcher metrics and still fail intent/tools. |
| **Implemented but not integrated** | Fauna encounter, death bridge, FirstExit (no pod prefab), Hazard (no placement), craft/repair consume path (blocked on empty resource + zero visible recipes). |

---

## Fixes staged for commit (post-L09 product)

| File | Change |
|------|--------|
| `PlayerToolManager.cs` | FixedTick included in register/unregister early-outs |
| `PauseMenuController.cs` | `_openMenuCount = 0` on SubsystemRegistration reset |
| `HeadlessSimulationRunner.cs` | Step-bounded dispatcher time on headless clock ensure |
| `Docs/V0_Playtest/V0_L09_MEASURED.md` | This ledger |

---

## L10 must prove

1. INPUTHOP reaches `readHop>=2` during swim hold (GetState called).  
2. `movementIntent01max > 0` with open publish path.  
3. STARTERGRANT completes or refusalMask changes meaningfully after PTM FixedTick registration.  
4. Tool `IsToolAvailableInSlot` true for at least one slot if grant succeeds.  
5. No sticky `PauseMenuController.IsAnyOpen` at probe start under disabled domain reload.

---

## Policy reminder

- Do **not** commit `Tools/_cline_scratch`, `*agent*`, Desktop scratch, or staged junk tmp docs.  
- No mocks. Feature without gameplay remains DECLINED.
