# V0 L15 LIVE RESULTS — Dual-register heal did NOT clear Swim FAIL

**Date:** 2026-07-31  
**HEAD:** `9f4169ffd` `fix(v0): heal dual-register desync so HPM FixedTick reaches hop2 (L15)`  
**Remote:** pushed `gitlab main` (`4dcb53307..9f4169ffd`)  
**Probe:** `H8_HeadlessPlayModeProbe.Run` batchmode, `-h8StartGame 1`, gameplay 90s  
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L15.log` (~2.2MB)  
**Artifact:** `Docs/AgentLogs/h8_playprobe_v0_L15.json`  
**PID:** 42884 — exited after RESULT (symbol dump tail only)

## Verdict

| Gate | Result |
|------|--------|
| Boot | PASS |
| WorldLoad | PASS |
| **Swim** | **FAIL** |
| Resource / Tool / Craft / Mission | BLOCKED (downstream of Swim) |
| FirstExit / Hazard | NOT_EXERCISED (content) |
| SaveLoad / Proof | PARTIAL |
| **RESULT failures** | **1** |

**Swim PASS still requires LIVE:** hop2 present + `movementIntent01max > 0`.  
**Do not claim Swim PASS from L15 code alone.**

## Swim metrics (LIVE)

| Metric | L14 LIVE | L15 LIVE | Delta |
|--------|----------|----------|-------|
| movementIntent01max | 0.000 | **0.000** | unchanged FAIL |
| immersionMax | 1.000 | 1.000 | — |
| depth span | 0.000 m | **0.000 m** | unchanged |
| lastOverrideMove | (0,1) | **(0,1)** | override still published |
| currentStateMove | **(0,1)** | **(0,1)** real hop1 metrics | poll `(0,0)` was **artifact** (see note) |
| INPUTHOP readHop seen | [1] | **[1]** | hop2 still ABSENT |
| inputOverrides (driver) | high | 258115 / swim-phase ~79931 | publish path alive |
| blockMask / pdaOpen | 0 / false | 0 / false | not menu-blocked at verdict |

Swim MOMENT excerpt:

```
FAIL Swim driver published 79931 input overrides; movementIntent01max=0.000
immersionMax=1.000 depth=0.000..0.000 span=0.000m
inputServiceRegistered=True inputEnabled=True switchToPlayerInputCalled=True
blockMask=0x00000000 pdaOpen=False
```

## INPUTHOP census (L15)

- `readHop=1` only — observations grow (240 → 1200 → 3600+); **never hop2**.
- `lastOverrideMove=(0,1)` stable.
- Early samples: `overrideApplied` climbs slowly; `overrideRejected≈267` sticky baseline.
- `postMaskMove=(0,1)` on hop1 lines; real hop1 `currentStateMove` metrics stay **(0,1)**. Poll `currentStateMove=(0,0)` was last-match against prose help text, not a metric regression.
- `lateFrameTick` stuck ~22, `pumpFired=1` — LateUpdate input pump barely advances while driver ticks hundreds of thousands of times.
- `presimTick` / `presimSubsteps` advance only into low hundreds while WORLDDRIVER reports 258115 ticks → driver tick ≠ full FixedTick/presim cadence for HPM.

## DETERMINISM / sim cadence

- Hash categories all count=0, hash=0 — **NeverSampled**.
- Owner lifetime: buffer allocated by first gameplay tick, but no hash frame reached cadence.
- `PlayerKinematicState count=0` — player kinematic vault empty / never folded.
- Slow-tick discard: none.

Interpretation: dual-register heal did not put a ticking HPM onto a path that samples GetState (hop2) or advances player kinematic state under the probe driver.

## L15 product that shipped (already on gitlab)

1. `GlobalRegistry.TryRegisterFixedTickable/Updatable/ColdTickable` — heal: if global Contains, still ensure dispatcher lane membership.
2. `HectonPlayerMovement.TryRegisterToDispatchers` — if sticky true but lane missing → clear sticky and re-register (no Unregister thrash every Ensure).

## What L15 disproved

- “Global/lane dual-register desync alone” is **not** sufficient explanation for hop2 ABSENT on this LIVE route.
- Heal may still be correct hygiene, but residual is elsewhere (or heal never exercised because sticky/lane already matched while FixedTick still does not call GetState).

## Correction (L16 dig) — poll artifact, not csm regression

`_l15_poll.py` last-matched `currentStateMove=` against diagnostic **prose** containing the literal `(0,0)`. Every real hop1 METRIC line in the L15 log is `currentStateMove=(0,1)`. Residual was never “override missing from CurrentInputState”; residual was **FixedTick/GetState (hop2) never ran**.

## New residual ranking for L16 (resolved by L16 product)

1. **PRIMARY (confirmed L16):** PlayModeProbe never called `EnableStepBoundedTime`; batchmode WallClock dt often 0 → `RunFixedStepAccumulator` early-out → no HPM FixedTick → hop2 ABSENT. Fix: mirror `HeadlessSimulationRunner.EnsureHeadlessSimulationClock` on the probe route.
2. **Secondary if clock armed and hop2 still absent:** `IsGameplayInputBlockedByMenu` Sample early-out before GetState.
3. **Driver ticks ≫ pumpFired/presim** — expected once understood: WorldDriver is input producer only; WORLDDRIVER ticks ≠ FixedTick.
4. VERBSWEEP: overridesPublished high, buttons never in resolved snapshot (same plumbing family).

## Rules carried forward


- Product-only. No mocks. No fake hop2. No Unregister thrash every Ensure.
- Docs under `Docs/V0_Playtest/`. Push remote: **`gitlab`** (not origin).
- Swim NOT PASS until LIVE hop2 + intent>0.
