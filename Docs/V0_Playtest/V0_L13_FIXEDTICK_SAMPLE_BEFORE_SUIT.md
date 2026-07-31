# V0 L13 — FixedTick sample-before-suit + dispatcher re-register

**Date:** 2026-07-31
**HEAD at authoring:** post-`8e02edf3f` (L12 docs on main)
**Files:**
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs`
**Status:** CODE APPLIED — commit+probe THIS session

## L12 residual (LIVE measured)

Probe `h8_playprobe_v0_L12.log` proved publish path healthy:

| Signal | L12 LIVE |
|--------|----------|
| lastOverrideMove | (0,1) |
| currentStateMove | (0,1) |
| postMaskMove | (0,1) |
| overrideApplied | >0 |
| publishedOverrides | 23471 |
| hop1 (CurrentInputState) | present |
| **hop2 (GetState)** | **ABSENT entire run** |
| movementIntent01max | **0.000** |
| immersionMax | 1.000 (NOT proof of Sample — Awake can set) |
| depth span | 0..0 |
| menus | pda/fab/pause False |
| inputEnabled | True blockMask=0 |

**Verdict L12:** Driver → InputDispatcher MoveDelta works. HPM never called GetState. Swim FAIL residual is consumer-side.

## Product defect (this lane)

`HectonPlayerMovement.FixedTick` gated:

1. `suit == null` → return
2. `_juiceProcessor == null` → return
3. **then** `SampleGameplayLocomotionInputForFixedStep()` → ProcessPlayerInputFrame → TryReadFrame → GetState (hop2)

When suit/juice not ready for any Swim tick, hop2 never fires even though dispatcher holds MoveDelta=(0,1).
`movementIntent01max` is filled only from kinematics intent after Sample — stays 0.

Secondary hedge: FixedTick may not be registered on PriorityLayer.Player yet during early Swim — driver now forces `EnsureDispatcherRegistration()`.

## L13 fix (no mocks)

### HectonPlayerMovement.FixedTick
1. `EnsureJuiceProcessor()` (soft — no hard return on null juice)
2. **`SampleGameplayLocomotionInputForFixedStep()` FIRST** (before suit gate)
3. `if (suit == null) return;` — physics-only early-out AFTER sample
4. Rest of FixedTick unchanged (PrepareTransport / immersion / kinematics)
5. Removed duplicate Sample call from inside the profiler using-block
6. Public `EnsureDispatcherRegistration()` wraps private `TryRegisterToDispatchers()`

### H8_HeadlessWorldDriver.EnsureGameplayLocomotionInputReady
- Resolve `HectonPlayerMovement` from `_movement` or `FindFirstObjectByType`
- Call `movement.EnsureDispatcherRegistration()` so Player fixed lane is live before Swim hold

## Acceptance (live probe L13)

Must measure on real playmode route (`H8_HeadlessPlayModeProbe.Run`, no `-nographics`, no mocks):

| Gate | Pass |
|------|------|
| INPUTHOP | hop2 present during swim hold (GetState called) |
| Swim intent | `movementIntent01max >= MinMovementIntent01` |
| Depth | non-zero span surface→dive (ideal; may need schedule if hop2+intent OK but depth 0) |
| Menus | remain closed during hold |
| Overrides | lastOverrideMove non-zero mid-hold (L12 already proved) |

### Residual branches after L13 probe
- **hop2 OK, intent still 0** → dig PrepareTransport / kinematics wipe / vehicle authority / short SwimSurface (2 ticks)
- **hop2 still ABSENT** → FixedTick not dispatched (Player lane bootstrap / registration still fail) — dig SystemDispatcher `blockGameplayLanes`
- **intent>0, depth 0** → swim physics / immersion / vertical intent axis, not input hop

## Probe command

```
Tools\_cline_scratch\launch_v0_L13_sample_before_suit_probe.bat
```

Requires: no `Temp\UnityLockfile`, Unity `6000.5.0f1`.

Logs: `Docs/AgentLogs/h8_playprobe_v0_L13.log` + `.json`

## Explicitly NOT claimed

- Swim PASS (not verified in-game until L13 probe numbers).
- Depth/dive kinematics fixed.
- Resource/Tool/Craft/Mission rows.

## Subagents

- `Docs/AgentLogs/scratch/L13_subA_hop2_root.mem.json` (root cause ranked, conf 0.86)
- `Docs/AgentLogs/scratch/L13_subB_fixedtick_reg.mem.json` (if present)
- L13 probe poll/critique mems written after live run

## Confidence / miss

- **Least confident:** suit/juice early-out may not be sole hop2 starve — FixedTick may never dispatch (registration / Player lane skip). Registration ensure is hedge.
- **Biggest prior miss:** L12 claimed publish-order would restore intent; live showed publish OK but HPM never GetState.
