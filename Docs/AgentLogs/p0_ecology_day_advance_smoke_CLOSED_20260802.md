# P0 ecology day-advance smoke — CLOSED 2026-08-02

## DoD (from BACKLOG / HANDOFF)
status not in `{ECOLOGY_UNAVAILABLE,BATCH_TIMEOUT,BOOTSTRAP_TIMEOUT}`
AND `ecologySampledDays>0`
AND `timeDilationDelivered>0`

## Result JSON
Path: `Docs/AgentLogs/HeadlessSimulationResult_HEADLESS_SIMULATION_RUNNER.json`

```json
{"agent":"HEADLESS_SIMULATION_RUNNER","status":"SUCCESS","exitCode":0,"days":5,"targetDays":5,"simulatedSeconds":304.966682571918,"timeDilationNominal":100,"timeDilationDelivered":11.37246,"progressionSignals":0,"crashSignalsConsumed":0,"lastProgressionHash":0,"lastCrashReasonHash":0,"syntheticAupShifts":208,"actualOriginShifts":6,"nativeBytes":128485584,"h8Bytes":248407680,"gasInvalidRoomId":-1,"ecologySampledDays":5,"ecologyUnsampledDays":0,"debugLogMessagesDelivered":13687,"evidenceFailureFlags":0}
```

## Checklist
| Gate | Value | Pass |
|------|-------|------|
| status | SUCCESS | YES |
| exitCode | 0 | YES |
| days / targetDays | 5 / 5 | YES |
| ecologySampledDays | 5 | YES (>0) |
| ecologyUnsampledDays | 0 | YES |
| timeDilationDelivered | 11.37246 | YES (>0) |
| evidenceFailureFlags | 0 | YES |

## Recipe
```
Unity 6000.5.0f1 -batchmode -nographics
  -projectPath C:\hades\Hecton8
  -h8headless -h8headlessDays 5 -h8headlessDaySeconds 60
  -logFile Docs/AgentLogs/p0_ecology_day_advance_smoke_20260802b.log
  -executeMethod Hecton8.QA.Headless.Editor.HeadlessSimulationBatchRunner.Run
```

## Product path that made it green
- `HeadlessSimulationRunner.EnsureHeadlessSimulationClock` unpause + `RequestHeadlessTimeDilation(100)` at lanes-registered / ecology-ready / game-ready
- Sustain every 5s while days==0
- Post-ready Warning diag every 15s
- Ready-mark on Update wait path (`80b2d9764`)
- FO lock-drain under physics pause (`411715153`)

## Compile unblock (same session, required for smoke)
Glued `GameObject` declarations + `Object` ambiguity + player-safe reflection ensure for AmbientBiota/DynamicMusic so batchmode could compile.

## Still open (not this DoD)
- Real-game screenshots still REQUIRED for full ship claim (headless green alone ≠ visual ship).
- Other Open P0: KCC 0x42, V0 Swim, Debris EXEMPT, RuntimeSmokeTester, README art, Docs/Screenshots/V0_Playtest empty.

## HEAD at close
Recorded at write time via `git rev-parse HEAD` in accompanying commit.
