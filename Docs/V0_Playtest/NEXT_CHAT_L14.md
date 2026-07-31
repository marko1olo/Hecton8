# NEXT CHAT — L14 handoff

## State

- L13 + L13.1 SHIPPED LIVE: hop1 OK, hop2 ABSENT, movementIntent01max=0, Swim FAIL.
- L14 product code applied (uncommitted until this handoff commit):
  - SD: never skip Player lane during bootstrap (`ShouldSkipLaneDuringBootstrap` → false).
  - HPM Sample: publish `ResolveRawInputIntentVector` into `_lastPlayerKinematicsIntendedMovement`.
  - HPM reg: sticky-only (NO Unregister thrash on every Ensure).
- Doc: `Docs/V0_Playtest/V0_L14_PLAYER_LANE_AND_SAMPLE_INTENT.md`

## Do now

1. Commit product + V0_Playtest docs only (no Tools/_cline_scratch, no Desktop dumps).
2. Push `main` (Iron Gate: gitleaks + biome).
3. Kill leftover Unity Editor processes for this project.
4. Launch LIVE L14 probe (copy L13 bat flags).
5. Poll until Swim line + INPUTHOP + movementIntent01max.
6. **Swim PASS only if live intent>0 and hop2 present.** Else L15 dig with subagents.

## Probe recipe

Unity 6000.5.0f1 batchmode playmode:

- Method: `Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run`
- Flags: `-batchmode -h8StartGame 1 -h8TimeoutSeconds 900 -h8MenuSeconds 120 -h8SettleSeconds 180 -h8GameplaySeconds 90`
- No `-quit`, no `-nographics`, no forceMenuLoad.

## Subagents (mandatory next loop)

- discover / critique / write → `Docs/AgentLogs/scratch/L14_*.mem.json` or L15_*
- Feature without gameplay integration = DECLINED.

## Do not

- Claim Swim PASS from code review alone.
- Ship Unregister+Register every Ensure tick.
- Commit scratch / agent_mem dumps / LFS tokens.
