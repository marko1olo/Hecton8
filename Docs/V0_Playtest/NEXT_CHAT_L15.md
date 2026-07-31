# NEXT CHAT — L15 Dual-register heal (handoff)

## State

- **L14 LIVE FAIL** documented: `Docs/V0_Playtest/V0_L14_LIVE_RESULTS.md`
- **L15 code + docs** on this commit (dual-register heal + HPM lane membership ensure)
- **Swim NOT PASS** until LIVE probe shows hop2 + movementIntent01max>0

## Parent residual (L14 LIVE @ 4dcb53307)

- hop1 HEALTHY: lastOverrideMove=(0,1), currentStateMove=(0,1)
- hop2 **ABSENT**, movementIntent01max=**0.000**, immersionMax=1, depth span=0
- Swim FAIL: driver MoveDelta never reached HPM FixedTick path

## L15 fix (product)

1. `GlobalRegistry.TryRegisterFixedTickable` / `Updatable` / `ColdTickable`: if global already Contains, still ensure dispatcher lane membership (heal desync).
2. `HectonPlayerMovement.TryRegisterToDispatchers`: if sticky true but lane `Contains` false, clear sticky and re-TryRegister. No thrash Unregister every Ensure.

## LIVE probe (required next)

```
Unity 6000.5.0f1 batchmode
-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90
NO -quit, NO -nographics, NO forceMenuLoad
Probe: Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run
Log: Docs/AgentLogs/h8_playprobe_v0_L15.log
```

### Pass criteria (do not claim early)

- INPUTHOP census readHop ≥ 2 during swim hold
- movementIntent01max > 0
- Swim gate PASS on artifact (not code-only)

### If still FAIL

Rank next dig:

1. Fixed steps not advancing (dilatedDeltaTime, dispatcherFrameId=0 DETERMINISM NeverSampled)
2. Sample menu block despite force-close
3. HPM instance bound by driver ≠ instance on fixed lane
4. Soft-reset / lifecycle still clearing registration after Ensure

## Rules

- Product-only. No mocks. No fake hop2.
- Subagents mandatory when available for residual loops.
- Docs under `Docs/V0_Playtest/` (AgentLogs gitignored).
- `git -C C:\hades\Hecton8`; never echo gitlab LFS/glpat tokens.
- Continue without asking; daemon until Swim LIVE PASS or next product root.

## Key paths

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`
- `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs`
- `Docs/V0_Playtest/V0_L15_DUAL_REGISTER_HEAL.md`
