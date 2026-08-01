# NEXT CHAT — L17 handoff

## State

- **L17 product fix IMPLEMENTED** (Probe FO drain + SystemDispatcher LateFrame TryFlush parity).
- **LIVE not yet run** at doc write time — Swim still FAIL until LIVE proves hop2 + movementIntent01max>0.
- Prior: L16 clock LIVE PASS (`stepBoundAfter=1`); hop2 still ABSENT; lateFrameTick=49 / pumpFired=1 FROZEN while presim advanced.

## What landed

1. `H8_HeadlessPlayModeProbe.DrainProbeFloatingOriginBootstrap` — HSR-parity `TryFlushInitialSceneRebaseBeforeTicks` every gameplay tick + FODRAIN snapshot.
2. `SystemDispatcher.RunDispatcherLateFrame` — TryFlush before bootstrap-lock hard-return (parity with `RunDispatcherUpdate`).

## Immediate next steps

1. Kill leftover Unity processes.
2. LIVE probe:
   - Method: `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run`
   - Flags: `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90`
   - NO `-quit`, NO `-nographics`
   - Log: `Docs/AgentLogs/h8_playprobe_v0_L17.log`
3. Parse LIVE for: `FODRAIN`, `SIMCLOCK`, `INPUTHOP` hop2, `movementIntent01max`, `lateFrameTick` unfrozen.
4. Write `V0_L17_LIVE_RESULTS.md`; commit docs; push gitlab main.
5. Swim PASS only if hop2 present AND movementIntent01max>0.

## If LIVE still FAIL

Rank residual from FODRAIN snapshot:

1. `foLock`/`dispBoot` still 1 after drain → dig FO SceneRebaseTickLock residual (pending scenes / physics pause / barrier).
2. Lock clear but lateFrame still frozen → dig dilation re-zero / IsSimulationHalted / other RunDispatcherUpdate early-outs.
3. Fixed path runs (lateFrame advances) but hop2 still ABSENT → dig HPM Sample / TryReadFrame / menu block / player map.

## Hard rules

- No mocks, no hop2 forge, no FixedTick/GetState from driver.
- Product-only; feature without gameplay = DECLINED.
- Commit product+docs under `Docs/V0_Playtest/` only; never Tools/_cline_scratch or AgentLogs.
- Primary remote: **gitlab main**. Never push origin as primary. Never echo tokens.
- Use subagents for broad digs. Shell: write .py then run; cmd `&` not `&&`.

## Repo

- Path: `C:\hades\Hecton8`
- Unity: 6000.5.0f1
- Docs: `Docs/V0_Playtest/V0_L17_FO_BOOTSTRAP_DRAIN.md`
