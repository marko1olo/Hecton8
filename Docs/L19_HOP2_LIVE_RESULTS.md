# L19 hop2 LIVE results (pid 25092)

**Date:** 2026-08-03  
**Commit (batchmode peel + IK/acoustic):** `5cbe738e34` — `fix(L19): batchmode soft-disable AcousticZone.Tick + PlayerKinematics IK/GPU after WORLDDRIVER Crash!!!`  
**Dispose double-free fix:** applied same session in `GlobalRegistry.DisposeServiceReboundQueuesForShutdown` (once-only `_serviceReboundQueuesShutdownDisposed`, reset in `ResetStaticState`) — commit pending after this note.

## Verdict

| Gate | Status |
|------|--------|
| Compile / enter play | PASS |
| WORLDDRIVER begin | PASS |
| WORLDDRIVER complete | PASS (`ticks=72352 elapsedMs=24117.4`) |
| `[H8_PLAYPROBE] RESULT` | **EMITTED** `pass=4 failures=2` |
| Process-lifetime native Crash!!! during probe body | **NONE** before RESULT |
| Post-RESULT teardown Crash!!! | YES — `DisposeServiceReboundQueue` double-free (fixed this session; not yet re-probed) |

## RESULT line (evidence)

```
[H8_PLAYPROBE] RESULT pass=4 failures=2
```

Log: `Docs/AgentLogs/h8_playprobe_v0_L19_hop2.log` (pid 25092 run).

### Pass moments (4)

1. `MOMENT_SWIM_FORWARD_PULSE` — `readHop=1 movementIntent=1 swim.forward=1`
2. `MOMENT_SWIM_STRAFE_LEFT_PULSE` — `readHop=1 movementIntent=1 swim.strafe=1`
3. `MOMENT_TOOL_CYCLE_NEXT_PREV` — `tool.cycleNext=1 tool.cyclePrev=1`
4. `MOMENT_RESOURCE_NODE_INTERACT_PRESS` — `resource.interactPressed=1`

### Fail moments (2)

1. `MOMENT_SWIM_STRAFE_VERTICAL_COMPOSITE` — `swim.vertical=0` (strafe held; vertical hop not observed)
2. `MOMENT_TOOL_PRIMARY_PRESS_RELEASE` — `tool.primaryPressed=0` (release seen; press not latched in window)

## INPUTHOP / WORLDDRIVER evidence

- `[H8_INPUTHOP] begin` then multiple `force=1` / `readHop=1` samples with `movementIntent` progressing 0→1
- `[H8_PLAYPROBE] WORLDDRIVER begin`
- `[H8_PLAYPROBE] WORLDDRIVER complete ticks=72352 elapsedMs=24117.4`
- Full verb sweep + lane census completed before RESULT

## Peel stack that unblocked RESULT (batchmode soft-disables)

Prior sessions + this session (non-exhaustive of earlier L19o work):

- GPU marine-snow / GraphicsBuffer upload
- ObjectPoolDiagnostics FlushPending
- AcousticZone TransitionTo / Start mixer bind / **Tick** (this session)
- vault/prewarm PrepareBurstData
- scatter LateFrameTick / visual sync
- KineticCharacter Burst jobs
- swim presentation / blockout rig
- MigrationDirector BuildMigrationVectorFieldJob
- TetherManager HarpoonTensionSolver328 mock
- PlayerCriticalProceduralAudioRenderer Tick/LateFrameTick
- H8_PlayModeScreenshotter CaptureAndExit
- NarrativeEvents NativeQueue under batchmode
- **PlayerKinematicsRuntime** PostFixedTick/FastTick/LateFrameTick IK/GPU (this session, `5cbe738e34`)

## Post-RESULT teardown crash (fixed, needs confirm hop)

**Stack (empty managed):**

- `GlobalRegistry.DisposeServiceReboundQueue`
- `DisposeServiceReboundQueuesForShutdown`
- called from both:
  - `GameBootstrapper.DisposeSessionNativeStateForShutdown` (playmode exit)
  - `GlobalRegistry` editor quitting / beforeAssemblyReload hooks

**Root cause:** second `NativeQueue.Dispose()` on the same static fields.

**Fix:** `_serviceReboundQueuesShutdownDisposed` once-only guard; cleared again in `ResetStaticState` after dispose so the next play session can create/dispose queues.

## Remaining hop quality (non-crash)

- Vertical swim composite + tool primary press latch — probe moment failures only; do not block “RESULT emitted / world driver live” claim.
- Optional: re-run hop2 after dispose commit to confirm Crash!!!=0 through process exit.

## Next (Overseer)

1. Commit dispose once-only fix.
2. Optional confirm hop2 (Crash!!! through shutdown = 0).
3. Open `VISION_LOCKS.md` / `PROJECT_BIBLES.md` and implement next product feature (bible index — pick first unlocked gameplay gap after L19 peel).


---

## Session 2026-08-03 ~18:00-19:00 — IUpdatable hang audit peels

**Hang status:** CLEARED past STARTERGRANT through VERBSWEEP on prior run (pid 21828). Breadcrumbs reached SLOWTICK_DONE + MAINTICK_DONE. WORLDDRIVER began; VERBSWEEP complete. Remaining failure mode was Mono jit-info assertion after VERBSWEEP flush (not sim hang).

### Peels committed this hop

| Commit | Peel |
|--------|------|
| 7cbd651499 | SystemDispatcher main-tick ENTER/DONE + POST_* + MAINTICK_DONE breadcrumbs |
| 500612bb3a | HectonSeismicTideDirector Tick/SlowTick/LateFrameTick batch peel (named by ENTER:HectonSeismicTideDirector) |
| 78b189144e | FloraInteractionManager.LateFrameTick batch peel (double sway Complete + cascade + parasite) |
| 0101287d45 / bff9c79c99 | EcosystemDirector.LateFrameTick + HectonMapMagicVegetationBridge.LateFrameTick batch peels |
| 38692f038f (auto) | SlowTick post-loop CombatDamageRuntime + WorldSpatialHashGrid peels + SlowTick ENTER/DONE breadcrumbs |

### IUpdatable hang audit (read-only) top remaining MEDIUM after peels

- DestructibleOrganicManager Tick paths (already has some isBatchMode)
- Any remaining forceComplete:true job Completes on gameplay lanes
- Mono jit-info assertion post-VERBSWEEP (runtime/editor, not peel)

### Next

Re-run hop2 after compile settles; confirm VERBSWEEP + further WORLDDRIVER phases without hang; then full RESULT if mono assert stays quiet.
