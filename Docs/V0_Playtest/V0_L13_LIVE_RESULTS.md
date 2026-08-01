# V0 L13 — LIVE RESULTS (sample-before-suit + L13.1 FQN)

**Date:** 2026-07-31
**Probe HEAD:** `125221564` (`1252215646bfae9f71d724621c7e52cf93ba8840`)
**Commit subject:** fix(v0): fully-qualify HectonPlayerMovement in WorldDriver L13 register (CS0246)
**Parent product:** `8a8290c81` HPM FixedTick sample-before-suit + EnsureDispatcherRegistration
**Log:** `Docs/AgentLogs/h8_playprobe_v0_L13.log`
**Launcher:** `Tools/_cline_scratch/launch_v0_L13_sample_before_suit_probe.bat`
**Status:** COMPILE OK · Swim **FAIL** · residual → **L14**

## Explicitly NOT claimed

- Swim PASS
- hop2 fixed
- FixedTick proven dispatched on Player lane
- depth / dive kinematics fixed
- Resource / Tool / Craft / Mission rows

## Compile

| Check | LIVE |
|-------|------|
| CS0246 | **0** (absent entire log) |
| error CS* | **0** |
| Scripts compile bar | Compiling Scripts progressed; probe ran full schedule to Done |

L13.1 FQN fix unblocked Editor asm. No compile residual on this HEAD.

## Swim moment (FAIL)

From `[H8_PLAYPROBE] MOMENT FAIL Swim`:

| Signal | LIVE value |
|--------|------------|
| verdict | **FAIL** |
| publishedOverrides / input overrides | **49414** |
| movementIntent01max | **0.000** |
| immersionMax | **1.000** (not proof Sample ran; Awake can set) |
| depthSampled | True |
| depth | **0.000..0.000** |
| depth span | **0.000 m** |
| oxygen | 139.240 → 139.240 |
| pressure | 1.000 → 1.000 |
| vitalsFlags | o2=False pressure=False depth=False |
| inputServiceRegistered | True |
| inputEnabled / inputEnabledNow | True |
| switchToPlayerInputCalled | True |
| blockMask | 0x00000000 |
| pdaOpen / fabOpen / pauseOpen | False / False / False |

Driver text: input path open; MoveDelta never reached `HectonPlayerMovement`.

### Schedule context (Swim)

| Phase | wall | granted | ticks | yield |
|-------|------|---------|-------|-------|
| SwimSurface | 9.197s | 5.000s | 4 | Timeboxed (LocomotionHoldInProgress) |
| SwimDive | 7.001s | 7.000s | 49409 | Completed (LocomotionHoldInProgress) |
| SwimVerdict | 0.009s | — | 1 | Completed |

SwimSurface was timeboxed (held longer than design box), not starved short — threshold miss is a real miss.

## INPUTHOP census

| Hop | Meaning | LIVE |
|-----|---------|------|
| hop1 | CurrentInputState (driver path) | **present** (readHop=1 on mid-run census lines) |
| hop2 | GetState (HPM / locomotion consumer) | **ABSENT entire run** (no readHop=2; end census readHop=0 after hold) |

### Healthy hop1 mid-hold samples (publish path OK)

Representative `[H8_INPUTHOP]` while overrides live:

- `readHop=1`
- `lastOverrideMove=(0,1)`
- `currentStateMove=(0,1)`
- `postMaskMove=(0,1)`
- `blockMaskNonZero=0`
- `overrideApplied` growing; `regInputService=True`

End-of-run census (post-authoring phases): `readHop=0`, `lastOverrideMove=(0,0)`, `currentStateMove=(0,0)` — expected after Swim authoring ends; does **not** create hop2.

## Gate scorecard vs L13 acceptance

| Gate | Required | LIVE | Result |
|------|----------|------|--------|
| compile (no CS0246) | OK | OK | met |
| hop2 during swim hold | present | **ABSENT** | miss |
| movementIntent01max | > 0 (MinMovementIntent01) | **0.000** | miss |
| depth span | non-zero ideal | **0** | miss |
| menus closed | closed | closed | met |
| lastOverrideMove mid-hold | non-zero | **(0,1)** | met (L12 path still healthy) |

## Conclusion

1. **L13 sample-before-suit is insufficient** on LIVE: product reorder + EnsureDispatcherRegistration + L13.1 compile fix did not restore hop2 or intent.
2. Publish / override path remains healthy (hop1, lastOverrideMove=(0,1), overrides=49414).
3. **hop2 still ABSENT** ⇒ HPM never called `InputDispatcher.GetState` during the window ⇒ `movementIntent01max` stays 0.
4. Leading residual: **FixedTick likely never dispatched** on PriorityLayer.Player (registration miss and/or Player-lane bootstrap skip / `blockGameplayLanes`), not suit/juice ordering alone.
5. immersionMax=1.000 remains non-diagnostic for Sample (Awake immersion path).

## Next layer

→ **L14** FixedTick / Player-lane dispatch dig.  
Handoff: `Docs/V0_Playtest/NEXT_CHAT_L14.md`

## Related

- `Docs/V0_Playtest/V0_L13_FIXEDTICK_SAMPLE_BEFORE_SUIT.md` (CODE intent)
- `Docs/V0_Playtest/V0_L13_1_CS0246_FQN.md` (compile unblock)
- `Docs/V0_Playtest/NEXT_CHAT_L13.md` (superseded residual → L14)
- `Docs/AgentLogs/scratch/L13_subA_hop2_root.mem.json`
- `Docs/AgentLogs/scratch/L13_subB_fixedtick_reg.mem.json`
