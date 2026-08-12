# NEXT CHAT — L16 batchmode step-bounded clock → LIVE Swim gate

## State

- **L15** dual-register heal on gitlab main `9f4169ffd`. LIVE residual: hop2 ABSENT, intent=0.
- **L16 dig root (HIGH confidence):** PlayModeProbe never armed `EnableStepBoundedTime`; batchmode WallClock dt=0 → no FixedTick → no hop2.
- **L16 implement:** `H8_HeadlessPlayModeProbe` mirrors `HeadlessSimulationRunner.EnsureHeadlessSimulationClock`.
- **Swim PASS still requires LIVE** (hop2 + movementIntent01max>0). Do not claim PASS from code alone.

## What shipped (this lane)

| Item | Path |
|------|------|
| Product | `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessPlayModeProbe.cs` |
| Design doc | `Docs/V0_Playtest/V0_L16_BATCHMODE_STEP_BOUNDED_CLOCK.md` |
| L15 results | `Docs/V0_Playtest/V0_L15_LIVE_RESULTS.md` (csm poll artifact corrected) |

## LIVE command (same contract as L15)

```
Unity 6000.5.0f1 batchmode
-executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run
-h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90
NO -quit, NO -nographics, NO forceMenuLoad
Log: Docs/AgentLogs/h8_playprobe_v0_L16.log (or project log path used by launch bat)
```

Remote for main: **gitlab** (not origin).

## LIVE gates

1. `[H8_PLAYPROBE] SIMCLOCK ensure` with `stepBoundAfter=1`
2. INPUTHOP hop2 present
3. `movementIntent01max > 0`
4. Prefer depth>0; immersion already 1 on L15

## If LIVE still FAIL

| Symptom | Next dig |
|---------|----------|
| No SIMCLOCK lines | compile/asmdef/method not hit — confirm assembly Hecton8.Editor |
| stepBoundAfter=0 | EnableStepBoundedTime rejected (dt coarse?) |
| SIMCLOCK ok, hop2 still absent | `IsGameplayInputBlockedByMenu` Sample early-out before GetState (L17) |
| hop2 present, intent still 0 | locomotion consume path / sticky lane residual |

## Forbidden

- Mock hop2, driver FixedTick/GetState, Unregister thrash
- Push origin/github as primary; never echo gitlab tokens
- Commit Tools/_cline_scratch junk or Docs/AgentLogs

## L15 poll note

`currentStateMove=(0,0)` in L15 poll was **artifact** (last-match hit prose help text). Real hop1 metrics were `(0,1)`.
