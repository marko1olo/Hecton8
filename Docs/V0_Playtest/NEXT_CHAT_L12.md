# NEXT_CHAT — V0 L12 residual / continue

## Done this session

1. **L12 product fix SHIPPED** in `H8_HeadlessWorldDriver.cs`:
   - `Tick`: `SampleObservables` → `AdvancePhase` → `PublishLocomotionIntent` → clear if `!PhaseAuthorsInputIntent(_phase)`
   - Removed exit `_intent=default` on SwimDive / ToolUse / VerbSweep
   - Added `PhaseAuthorsInputIntent` (SwimSurface/Dive, ToolUse, VerbSweep)
   - VerbSweep two-step comment updated
2. Docs: `Docs/AgentLogs/V0_L12_TICK_PUBLISH_ORDER.md`
3. Probe launcher: `Tools/_cline_scratch/launch_v0_L12_publish_order_probe.bat`
4. Subagent mems under `Docs/AgentLogs/scratch/L12_*`

## NOT done

- **Live L12 probe** — blocked by `Temp\UnityLockfile` at author time. Clear lock, then run launcher.
- **Swim PASS not claimed** until log shows `movementIntent01max>0` + depth/vitals.
- **hop2** may still fail independently (see subB): hop2 = GetState called; zero intent ≠ hop2 absence. If intent fixed but hop2 still ABSENT → dig HPM `SampleGameplayLocomotionInputForFixedStep` / `IsPlayerInputEnabled` / FixedTick registration.

## Immediate next

```
1. Confirm no UnityLockfile
2. Tools\_cline_scratch\launch_v0_L12_publish_order_probe.bat
3. Poll Docs\AgentLogs\h8_playprobe_v0_L12.log for Swim row + INPUTHOP
4. If PASS intent but FAIL hop2 → L13 hop2 product path
5. If FAIL intent still → CaptureState stuck (lateFrameTick) / schedule tick starvation
6. git commit push main (product cs + V0_L12 doc); pull --rebase if behind
7. Daemon: next unintegrated gameplay after Swim green
```

## Least confidence

Publish-order is necessary; may not be sufficient if HPM never FixedTicks or TryReadFrame still short-circuits.

## Biggest miss for human

L11 “gates open” ≠ locomotion consumer called GetState. Feature without live probe numbers is DECLINED.
