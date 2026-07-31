# NEXT CHAT — L13 / L13.1 Swim residual

## State (do not claim Swim PASS)

| Layer | Status |
|-------|--------|
| L12 publish-after-AdvancePhase | SHIPPED LIVE: lastOverrideMove=(0,1), hop2 ABSENT, movementIntent01max=0 |
| L13 sample-before-suit + EnsureDispatcherRegistration | CODE in `8a8290c81` |
| L13.1 CS0246 FQN WorldDriver | CODE fix applied; commit/push then LIVE probe |
| Swim LIVE | **NOT PASS** until movementIntent01max>0 |

## Immediate next steps

1. Confirm HEAD includes L13.1 FQN fix on `H8_HeadlessWorldDriver.cs` Ensure block.
2. `git pull --rebase gitlab main && git push gitlab main` if not pushed.
3. Kill stale Unity; clear `Temp/UnityLockfile` if stale.
4. Launch `Tools/_cline_scratch/launch_v0_L13_sample_before_suit_probe.bat`
   - log: `Docs/AgentLogs/h8_playprobe_v0_L13.log`
   - flags: batchmode, h8StartGame 1, NO -quit, NO -nographics, NO forceMenuLoad
5. Poll `Tools/_cline_scratch/_l13_poll.py`
6. Verdict gates:
   - compile OK (no CS0246)
   - hop2 present in INPUTHOP census
   - movementIntent01max > 0
   - depth span if schedule allows

## If LIVE still FAIL

- hop2 OK intent 0 → PrepareTransport / kinematics / vehicle / schedule length
- hop2 ABSENT → FixedTick not on Player lane / blockGameplayLanes
- Subagents mandatory; write `.mem.json` under Docs/AgentLogs/scratch/

## Key paths

- `Assets/_Project/Scripts/HectonPlayerMovement.cs` FixedTick sample-before-suit
- `Assets/_Project/Scripts/Editor/Diagnostics/H8_HeadlessWorldDriver.cs` EnsureGameplayLocomotionInputReady
- Docs: `V0_L13_FIXEDTICK_SAMPLE_BEFORE_SUIT.md`, `V0_L13_1_CS0246_FQN.md`

## Repo

- `C:\hades\Hecton8` remote `gitlab` branch `main`
- Unity 6000.5.0f1
- Probe: `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run`
