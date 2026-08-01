# NEXT CHAT — L18 handoff

## State

- **L18 product fix SHIPPED** on gitlab `main`:
  - `2f4eda518` — SystemDispatcher runs ILateFrameTickable during origin shift frame lock
  - `c3003a3b9` — InputDispatcher LateFrame/Slow Contains heal + PreSim inject; HPM LateFrame heal; Probe dil 100→1
- **L17 LIVE closed** (docs): FODRAIN PASS both runs; hop2 ABSENT; lateFrame frozen@29; menu/FO/clock DEMOTED; L17a PhysX + L17b MapMagic under dil=100.
- **Swim still FAIL** until L18 LIVE proves hop2 PRESENT + `movementIntent01max > 0` on complete non-crash route.

## What landed (product)

1. `InputDispatcher.TryRegisterToDispatcher` — L15-parity Contains heal for LateFrame + Slow Core lanes after ClearAllLanes sticky desync.
2. `InputDispatcher.PreSimulationInputTick` — calls `TryRegisterToDispatcher()` every PreSim (path that still runs when LateFrame lane is empty).
3. `HectonPlayerMovement` LateFrame registration — Contains heal via `GetLateFrameLane(Player)`.
4. `H8_HeadlessPlayModeProbe.ProbeTimeDilationScalar` — `100f` → `1f`; keep stepBound 0.04.
5. `SystemDispatcher.RunDispatcherLateFrame` — do not starve LateFrame lane under origin frame lock.

## Immediate next steps

1. Kill leftover Unity processes.
2. LIVE probe:
   - Method: `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run`
   - Flags: `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90`
   - NO `-quit`, NO `-nographics`
   - Log: `Docs/AgentLogs/h8_playprobe_v0_L18.log`
3. Parse LIVE for: `FODRAIN`, `SIMCLOCK` (dil should be **1**), `INPUTHOP` hop2, `lateFrameTick` **advancing**, `movementIntent01max`, crash absence, SWIM/VERDICT.
4. Write `V0_L18_LIVE_RESULTS.md`; commit docs; push gitlab main.
5. Swim PASS only if hop2 present AND movementIntent01max>0 on complete route.

## If LIVE still FAIL

Rank residual from INPUTHOP + FODRAIN:

1. lateFrame still frozen → dig Contains API / lane identity / ClearAllLanes timing after PreSim heal; confirm GetLateFrameLane returns same bucket Register uses.
2. lateFrame advances but hop2 ABSENT → dig HPM Fixed lane membership, Sample early-outs, TryReadFrame, gameReady gates (not menu — DEMOTED).
3. hop2 PRESENT but movementIntent01max=0 → dig L14 Sample publish / kinematics intent path.
4. Crash at dil=1 → new stability residual (not dil=100 temporal compression).
5. foLock sticky again → FO regression (unexpected after L17).

## Hard rules

- No mocks, no hop2 forge, no FixedTick/GetState from driver.
- Product-only; feature without gameplay = DECLINED.
- Commit product+docs under `Docs/V0_Playtest/` only; never Tools/_cline_scratch or AgentLogs.
- Primary remote: **gitlab main**. Never push origin as primary. Never echo tokens.
- Use subagents for broad digs. Prefer write_to_file / replace_in_file / read_file over scratch Python apply scripts.
- Shell: write .py then run when needed; cmd `&` not `&&`; absolute paths / os.chdir to `C:\hades\Hecton8`.

## Repo

- Path: `C:\hades\Hecton8`
- Unity: 6000.5.0f1
- Docs: `Docs/V0_Playtest/V0_L18_LATEFRAME_LANE_HEAL.md`, `V0_L17_LIVE_RESULTS.md`
- Prior: L17 FO drain `358089b6f`; L16 clock; L15 dual-register heal
