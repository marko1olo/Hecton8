# V0 L12 — Tick publish-order product fix (driver)

**Date:** 2026-07-31  
**HEAD at authoring:** post-`5764a00b5` working tree (this change)  
**File:** `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs`  
**Status:** CODE SHIPPED — probe verify PENDING (UnityLockfile held)

## L11 residual (measured)

- Menus closed after L11 `ForceClose` + `EnsureGameplayLocomotionInputReady` (`pdaOpen=False fabOpen=False pauseOpen=False`).
- `inputEnabled=True`, `blockMask=0`.
- **Swim still FAIL:** `movementIntent01max=0`.
- INPUTHOP: hop1 present, **hop2 ABSENT** (HPM never called `GetState` during window — separate residual if L12 intent fix alone does not restore hop2).
- L11 window also showed `lastOverrideMove=(0,0)` and very few SwimSurface ticks vs L10 — consistent with publishing zero / stale intent and schedule starvation.

## Product defect (this lane)

`Tick()` order was:

1. `SampleObservables()`
2. `PublishLocomotionIntent()`  ← shipped **previous** tick’s `_intent` (often `default` on first hold tick)
3. `AdvancePhase()`             ← authors MoveDelta / PrimaryFire / verb mask **after** publish

`TryConsumeLatestInputOverride` is destructive (`maxFrameAge=2`). A zero publish poisons `CaptureState` for the locomotion consumer window even when phase code already wrote `(0,1)`.

## L12 fix (no mocks)

1. **Reorder:** `SampleObservables` → **`AdvancePhase`** → **`PublishLocomotionIntent`**.
2. **Deferred clear:** removed `_intent = default` on SwimDive / ToolUse / VerbSweep exit (those ran inside `AdvancePhase` and would zero the last hold frame under the new order).
3. **Post-publish clear:** after publish, if `!PhaseAuthorsInputIntent(_phase)` then `_intent = default` so verdict/resource/done do not re-ship stale bits.
4. **Authoring phases:** `SwimSurface`, `SwimDive`, `ToolUse`, `VerbSweep`.
5. **VerbSweep docs:** two-step contract comment updated (publish same driver tick; observables still see prior resolved frame).

## Acceptance (live probe L12)

Must measure on real playmode route (`H8_HeadlessPlayModeProbe.Run`, no `-nographics`, no mocks):

| Gate | Pass |
|------|------|
| Swim | `movementIntent01max >= MinMovementIntent01` |
| INPUTHOP | hop2 present during swim hold (GetState called) |
| Depth | non-zero span surface→dive |
| Menus | remain closed during hold |
| Overrides | `_publishedOverrides` during swim >> L11 (~162); lastOverrideMove non-zero mid-hold |

If intent max > 0 but hop2 still ABSENT → residual is HPM sample path (`IsPlayerInputEnabled` / FixedTick / menu short-circuit before `ProcessPlayerInputFrame`), not publish order.

## Probe command

```
Tools\_cline_scratch\launch_v0_L12_publish_order_probe.bat
```

Requires: no `Temp\UnityLockfile`, Unity `6000.5.0f1`.

Logs: `Docs/AgentLogs/h8_playprobe_v0_L12.log` + `.json`

## Explicitly NOT claimed

- Swim PASS (not verified in-game yet).
- hop2 fixed (may need follow-up).
- Resource/Tool/Craft/Mission rows.

## Subagents

- `Docs/AgentLogs/scratch/L12_subA_tick_reorder.mem.json`
- `Docs/AgentLogs/scratch/L12_subB_hop2.mem.json` (if present)
- `Docs/AgentLogs/scratch/L12_subC_ops.mem.json` (if present)
