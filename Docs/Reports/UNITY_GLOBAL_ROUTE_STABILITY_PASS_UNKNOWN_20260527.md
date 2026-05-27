# Unity Global Route Stability Pass - UNKNOWN - 2026-05-27

Evidence class: static source, scoped hot-path scanner, full SignalBus audit JSON, and diff hygiene.
No Unity import, Play Mode, profiler, GC capture, player build, or full-solution compile proof is claimed.
Full project compile errors were intentionally not fixed by user instruction.

## What Was Wrong

- `ConnectionSplineBatchRenderer.LateFrameTick()` unregistered itself from `GlobalRegistry`
  during the late-frame dispatcher route.
- `HectonAPI.Input.GetButtonMask()` read `GlobalRegistry.Input` from a public mod API
  getter that can be called every frame by managed mods.
- Legacy `ModCommandDispatcher` flow/acoustic command execution read
  `GlobalRegistry.AbyssalFlowGpu` and `GlobalRegistry.Audio` directly in command paths.

## What Was Done

- Replaced late-frame self-unregistration with a dormant registered state. The renderer
  still unregisters on cold disable, shutdown, or dispatcher replacement.
- Added a cold registry cache in `HectonAPI` for `IInputService`.
- Bound `HectonAPI` registry cache from `ModLoader` bootstrap/game-ready and refreshed it
  through the existing `ModEventProjectionBridge` hot-swap listener.
- Added cached `IAbyssalFlowGpuReadModel` and `IAudioService` dependencies to
  `ModCommandDispatcher`, with cold bind and hot-swap refresh.

## Proof

- Scoped hot-path scanner after fixes reports only two intentional residuals:
  `SignalBusRuntime.FlushPostSimulation` catastrophic lane-overflow kill-switch and
  `SignalCorridorRuntime.FlushPostSimulation` documented `GlobalSignals` bridge.
- Full static audit:
  `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_RECHECK.json`.
- Audit result: `scannedFiles=2442`, `shaderFilesScanned=71`, `errors=0`,
  `confirmedErrors=0`, `warnings=145`, `infos=1172`.
- Touched-file findings in the audit are INFO-only: legacy mod queues, Vault alias
  telemetry, and cold/fatal IO review.
- `git diff --check` on touched files passed with line-ending warnings only.
- Documentation gates after this report:
  `VerifyDocStructure.py pass=true activeDocCount=695 encodingWithoutUtf8Sig=0`;
  `OOP_Doc_Scanner.py finalPass=true activeFileCount=695 sourceSyncPass=true`.

## Residual Debt

- `SignalBusRuntime.FlushPostSimulation` still calls `GlobalRegistry.SetSystemKillSwitchBits`
  on lane-overflow fault. That is a catastrophic safety path, not regular hot polling.
- `SignalCorridorRuntime.FlushPostSimulation` still calls `GlobalSignals.FlushPostSimulation`.
  This is the explicit first-party bridge around legacy direct queues.
- The project still has `RUNTIME_SYNC_FILE_IO_REVIEW=65`,
  `SRP_BATCHER_HOT_PATH_MATERIAL_REVIEW=69`, and
  `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`; those were not bulk-edited without owner proof.
- Full solution build was not launched in this pass because the user assigned overall
  compilation errors to another agent.

Runtime microseconds saved: `0` claimed. The pass fixes global-route ownership and
dispatcher mutation risk; no profiler/player measurement was run.
