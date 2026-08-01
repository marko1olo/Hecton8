# NEXT CHAT — L19 handoff

## State

- **L19 product fix SHIPPED unmeasured** (code in tree; LIVE not yet run):
  - Input A: `IsPlayerInputEnabled` ORs `_lastAutomationOverrideApplied` so hop2 consumers open when automation lands while native player map is closed.
  - Input B: `SwitchToPlayerInput` / `SwitchToUIInput` call `TryEnsureNativeInputBound()`; DEV once-warn `[H8_INPUTNATIVE]`; bind from `GlobalRegistry.NativeInputRuntime` when local ref null (skip self).
  - MM C: `TerrainTile` — `IsLiveTerrain` + `SafeSetTerrainActive`; `SwitchLod` early-out / non-finite distance clamp / no SetActive on destroyed wrappers; null-safe objectsPool + weld.
- **L18 LIVE closed** (docs): lateFrameTick ADVANCES; hop1 PASS (`currentStateMove=(0,1)`); FODRAIN clean dil=1; hop2 ABSENT (`readHop=1` x3); Crash!!! MapMagic `ActiveTerrain`→`SetActive` (NOT DistToLod — API absent this revision).
- **Swim still FAIL** until L19 LIVE proves hop2 PRESENT + `movementIntent01max > 0` on complete non-crash route. **Do not claim green without measurement.**

## What landed (product)

1. `InputDispatcher.IsPlayerInputEnabled` — `(_nativeInputManager != null && _nativeInputManager.IsPlayerInputEnabled) || _lastAutomationOverrideApplied`
2. `InputDispatcher.SwitchToPlayerInput` / `SwitchToUIInput` — `TryEnsureNativeInputBound()` before enable/disable path
3. `InputDispatcher.TryEnsureNativeInputBound` — bind `GlobalRegistry.NativeInputRuntime` if local null
4. `TerrainTile.ActiveTerrain` get/set — live-terrain guards; no SetActive on destroyed Terrain
5. `TerrainTile.SwitchLod` — mapMagic null early-out; finite distance; live main/draft only; drop dead newActive; null-safe pool/weld

## Docs this lane

- `V0_L18_LIVE_RESULTS.md` — CLOSED L18 residuals; residual rank for L19
- `V0_L19_HOP2_ENABLE_AND_MM_LOD_GUARD.md` — A/B/C writeup + LIVE recipe + pass signals (status: PRODUCT FIX SHIPPED unmeasured)
- This file: `NEXT_CHAT_L19.md`

## Immediate next steps

1. Kill leftover Unity processes.
2. LIVE probe:
   - Method: `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run`
   - Flags: `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90`
   - NO `-quit`, NO `-nographics`
   - Log: `Docs/AgentLogs/h8_playprobe_v0_L19.log`
   - Unity: `C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe`
   - Project: `C:\hades\Hecton8`
3. Parse LIVE for: Crash absence (no ActiveTerrain/SetActive/SwitchLod death), `INPUTHOP` **readHop≥2 / hop2**, `lateFrameTick` advancing, `movementIntent01max`, `currentStateMove`, FODRAIN dil=1, SWIM/VERDICT.
4. Write `V0_L19_LIVE_RESULTS.md`; update L19 fix doc status; commit docs; push gitlab main.
5. Swim PASS only if hop2 present AND movementIntent01max>0 on complete non-crash route.

## If LIVE still FAIL

Rank residual from INPUTHOP + crash stack + FODRAIN:

1. **MM crash still** (ActiveTerrain / SwitchLod / ApplyRoutine / other MM) → deepen live guards; stack-trace exact site; no DistToLod assumption.
2. **lateFrame advances, hop2 ABSENT** → dig HPM Fixed lane membership, Sample early-outs, TryReadFrame, gameReady / `IsPlayerInputEnabled` still false (native + override both dark).
3. **hop2 PRESENT but movementIntent01max=0** → dig L14 Sample publish / kinematics intent path (not re-open hop1).
4. **hop1 regress** (overrideApplied stuck / currentStateMove=0) → CaptureState / ApplyAutomationOverride path.
5. **foLock sticky / dil≠1** → FO/clock regression (unexpected after L17/L18).

## Hard rules

- No mocks, no hop2 forge, no FixedTick/GetState from driver.
- Product-only; feature without gameplay = DECLINED.
- Commit product+docs under `Docs/V0_Playtest/` only; never Tools/_cline_scratch or AgentLogs.
- Primary remote: **gitlab main**. Never push origin as primary. Never echo tokens.
- Use subagents for broad digs. Prefer write_to_file / replace_in_file / read_file over scratch Python apply scripts.
- Shell: cmd starts on Desktop — `Set-Location C:\hades\Hecton8` or absolute paths; cmd `&` not `&&`.

## Repo

- Path: `C:\hades\Hecton8`
- Unity: 6000.5.0f1
- Docs: `V0_L19_HOP2_ENABLE_AND_MM_LOD_GUARD.md`, `V0_L18_LIVE_RESULTS.md`, `NEXT_CHAT_L18.md`
- Prior: L18 late-frame heal; L17 FO drain; L16 clock; L15 dual-register heal

## Pass signals (measurement only)

| Signal | PASS |
|--------|------|
| Crash | No Crash!!! / ActiveTerrain SetActive death through gameplay window |
| hop2 | INPUTHOP `readHop≥2` or explicit hop2 present (≥1 sample in gameplay) |
| Intent | `movementIntent01max > 0` on complete route |
| lateFrame | `lateFrameTick` advances across samples |
| FO/clock | FODRAIN clean, dil=1 |
| Swim | hop2 + intent on non-crash complete route only |
