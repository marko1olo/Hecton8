# NEXT CHAT — L17 handoff (STALE → L18)

## State

- **L17 product fix LIVE-PROVEN for FO drain** (`358089b6f`).
- **L17a + L17b LIVE CLOSED** — see `V0_L17_LIVE_RESULTS.md`.
  - FODRAIN PASS both runs (foLock=0 dispBoot=0).
  - SIMCLOCK PASS (stepBoundAfter=1).
  - hop2 ABSENT; lateFrameTick frozen@29 while presim advanced (L17b ×3).
  - L17a PhysX IncrementalAABBTree crash; L17b MapMagic TerrainTile.SwitchLod crash under dil=100.
  - Menu DEMOTED (0 log hits, blockMask=0).
- **Swim NOT PASS.** Residual re-ranked → **L18**.

## What landed (L17)

1. `H8_HeadlessPlayModeProbe.DrainProbeFloatingOriginBootstrap` — HSR-parity `TryFlushInitialSceneRebaseBeforeTicks` every gameplay tick + FODRAIN snapshot.
2. `SystemDispatcher.RunDispatcherLateFrame` — TryFlush before bootstrap-lock hard-return (parity with `RunDispatcherUpdate`).

## Do NOT re-run L17 as primary

FO residual is DEMOTED. Continue on **L18**:

- Docs: `NEXT_CHAT_L18.md`, `V0_L18_LATEFRAME_LANE_HEAL.md`
- Product already on main: LateFrame Contains heal + PreSim inject + Probe dil=1 + LateFrame during origin frame lock
- LIVE: `h8_playprobe_v0_L18.log`

## Hard rules

- No mocks, no hop2 forge, no FixedTick/GetState from driver.
- Product-only; feature without gameplay = DECLINED.
- Commit product+docs under `Docs/V0_Playtest/` only; never Tools/_cline_scratch or AgentLogs.
- Primary remote: **gitlab main**. Never push origin as primary. Never echo tokens.
- Use subagents for broad digs.

## Repo

- Path: `C:\hades\Hecton8`
- Unity: 6000.5.0f1
- L17 FO doc: `Docs/V0_Playtest/V0_L17_FO_BOOTSTRAP_DRAIN.md`
- L17 LIVE: `Docs/V0_Playtest/V0_L17_LIVE_RESULTS.md`
