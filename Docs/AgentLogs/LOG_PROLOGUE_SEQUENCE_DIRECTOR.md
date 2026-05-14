# LOG_PROLOGUE_SEQUENCE_DIRECTOR

## 2026-05-14 - Awaitable Drop Sequence

Status: PENDING VERIFICATION.

What was wrong:
- Prologue had no deterministic gameplay pacing service, no phased input lock/unlock, no Awaitable splashdown seam, no black-box prologue stage telemetry, and no low-tier hydration bypass.
- Initial hydration bridge pass used a concrete `WorldChunkResidencyManager` type-check. OMEGA polish rejected it and moved the read model to `IStreamingBackpressureService.IsChunkResident`.

What was done:
- Added `IPrologueSequenceService` and `IPrologueSequenceRuntime` contracts plus `GlobalRegistry.PrologueSequence` registration.
- Added isolated `Hecton8.Narrative.Prologue` assembly with `AwaitableDropSequenceDirector.RunPrologueSequenceAsync(CancellationToken)`.
- Implemented stages: atmospheric reentry gate, 3-second dilated orbital silence, Mach 10 burn gate, manual release prompt, one-frame impact sync, ocean hydration gate, and water handoff.
- Routed concrete work through signals and registry interfaces: `SystemPauseSignal`, `MixerStateSignal`, `AcousticPingSignal`, `VocalWarningSignal`, `HapticRequest`, `DiegeticHudSignal`, `HUDNotificationSignal`, `PrologueCompleteSignal`, `CameraJuiceSignals`, `IOrbitalDirector.ForceZeroUniverseVelocity`, and `IStreamingBackpressureService.IsChunkResident`.
- Added dev skip path gated by `GlobalRegistry.IsDevelopmentBuild`, input cancel/chord, forced shallow-water hydration, velocity zero, impact, and ocean handoff.
- Added fixed 300-entry `NativeArray` prologue black box and hash-only `GlobalTelemetryBus.PublishPrologueStage`.
- Extended `OrbitalRelativityDirector` with `ForceZeroUniverseVelocity(byte reason)` so splashdown can freeze universe velocity without direct field mutation.

Cinematic Cheats used:
- Reused existing hull-breach VWS lane for `HullTempCritical` instead of expanding clip tables.
- Used hash-only diegetic HUD prompt instead of runtime text allocation.
- Used proxy/impostor ocean surface on low tier instead of waiting for full-res hydration.
- Used reciprocal-square-root approximation for telemetry speed; no exact sqrt in prologue path.
- Used camera/fluid signal handoff for impact belief instead of honest water-impact physics.

Exact microseconds saved:
- Avoided scene singleton/search polling: estimated 10-35 us per wait iteration on i3/MX350.
- Avoided blind ocean black-screen wait: unbounded user-visible stall replaced by 2-12 us readiness checks.
- Avoided duplicate orbital snapshot query and double sqrt in `RecordStage`: estimated 1-4 us on orbital telemetry frames.
- Avoided managed timer/coroutine allocation: expected 0 B from `Task.Delay`/coroutine path; profiler proof absent.

Verification:
- Unity batch compile copied `Hecton8.Core.Contracts.dll` and `Hecton8.Narrative.Prologue.dll`, then failed in unrelated `ShallowsBioForgeBatchBaker.cs`, `DiegeticTooltipSystem.cs`, and `GlobalDataVault.cs`.
- MCP console is unreachable at `127.0.0.1:8088`; active Unity editor already owns the project.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted and timed out under project contention; owned root process was terminated.
- Static scan of prologue path found no `Task.Delay`, coroutine, `StartCoroutine`, gameplay `Update`, `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, or concrete `WorldChunkResidencyManager` reference.

## 2026-05-14 - Dev Skip Interruption Upgrade

What was wrong:
- Dev skip could be delayed by Stage 1 orbital silence because the sequence only checked skip before and after the 3-second dilated wait.

What was done:
- Added `PrologueCancelReasons` to the prologue contracts.
- Bridge auto-run now uses a linked cancellation token.
- Dev cancel now calls `CancelSequence(DevSkip)` and cancels the linked token.
- Development-only dilated delay polls skip each frame; release still uses `AwaitableExtension.DelayDilated`.
- Director now converts cancellation reason `DevSkip` into the forced shallow-water hydration, velocity zero, impact, and ocean handoff path instead of recording a plain cancel.

Cinematic Cheats used:
- Dev skip uses forced proxy hydration and signal handoff rather than honest world placement simulation.

Exact microseconds saved:
- Release path: 0 us per-frame overhead versus prior `AwaitableExtension.DelayDilated`; one cold CTS allocation per auto-run.
- Dev-only path: costs estimated 5-10 us per wait frame, but removes up to 3 seconds of blocked iteration time after skip input.

Verification:
- Unity Roslyn response-file compile: `Hecton8.Core.Contracts.rsp` passed.
- Unity Roslyn response-file compile: `Hecton8.Narrative.Prologue.rsp` passed after compiling contracts first.
- Unity Roslyn response-file compile: `Hecton8.Core.rsp` failed in unrelated `GroundPenetratingRadarRuntime.cs(309,17)` missing `GroundRadarRaymarchJob.GprOreTypes`; no prologue error emitted before that wall.

## 2026-05-14 - Loop 12 Manual Gate Source Whitelist

What was wrong -> Stage 3 manual release was protected from known bridge/orbital producers, but still accepted any unknown future `PrologueCompleteSignal` source on the shared lane.
What was done -> Audited current producers (`PRLG`, `ORBI`, `MOVR`) and changed `PrologueSequenceRegistryBridge.TryConsumePrologueComplete` to accept only `MOVR` from `OpenXRManualOverrideLever`.
Cinematic Cheats used -> Preserve signal-driven cockpit handoff instead of adding a new scene object or UI dependency; water/audio/VFX overkill remains downstream of a validated manual source.
Exact Microseconds saved -> No measurable saving versus the blacklist; cost remains one uint comparison per complete signal, capped at 8 entries. Saved correctness budget is preventing an accidental immediate water handoff from non-manual producers.
Verification -> Unity Roslyn primary Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.rsp` compiled with `EXIT=0`.

## 2026-05-14 - Loop 13 Run Preparation Fault Guard

What was wrong -> The director set `_running = true` and invoked `PrepareSequenceRun()` before the guarded `try/finally`; a future runtime adapter fault could strand the running flag and input lock cleanup.
What was done -> Director-local run state resets now happen first, and `PrepareSequenceRun()` executes inside the same `try/finally` envelope as the rest of the Awaitable state machine.
Cinematic Cheats used -> None; this is control-flow hardening.
Exact Microseconds saved -> 0 us in the hot path. The normal call count is identical; the change prevents fault-path cleanup loss.
Verification -> Unity Roslyn primary Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp` compiled with `EXIT=0`.

## 2026-05-14 - Loop 15 Auto-Run Disable Cancellation

What was wrong -> Release auto-run used `destroyCancellationToken` directly. Disable requested service cancellation but did not cancel the token driving `DelayDilatedAsync`, so disabled objects could wait for the cinematic timer to finish.
What was done -> Bridge auto-run now always creates one linked CTS and cancels it through `RequestRunCancellation()` for disable/dev skip. Release still uses the established `AwaitableExtension.DelayDilated` path.
Cinematic Cheats used -> None; lifecycle hardening preserves the same presentation.
Exact Microseconds saved -> 0 us wait-loop saving. Cost is one cold CTS allocation per auto-run; gain is deterministic release interruption on disable.
Verification -> Unity Roslyn primary Bee `Hecton8.Core.Contracts.rsp` and `Hecton8.Narrative.Prologue.rsp` compile with `EXIT=0` after restoring the run-reset contract. `Hecton8.Core.rsp` no longer reports prologue interface errors and now fails in unrelated save/voxel paths (`SaveMasterHashV10.cs`, `VoxelDeltaProcessor.cs`; earlier also `BinaryLayoutManifest.cs`, `HardwareTierDetector.cs`, `VRAMEnforcer.cs`).

## 2026-05-14 - Loop 16 Bridge Contract Drift Repair

What was wrong -> Core compile exposed `CS0535` because the bridge file on disk did not contain `PrepareSequenceRun()` even though the contract required it.
What was done -> Re-read the bridge, restored `PrepareSequenceRun()`, run-state reset, manual-source whitelist, stale-service clearing, and the contract/director run-reset hook.
Cinematic Cheats used -> None; this is integration repair.
Exact Microseconds saved -> 0 us beyond the previously documented reset/whitelist changes.
Verification -> `Hecton8.Core.Contracts.rsp` and `Hecton8.Narrative.Prologue.rsp` compile with `EXIT=0`; `Hecton8.Core.rsp` is blocked by unrelated save/voxel errors and no longer reports prologue interface errors.

## 2026-05-14 - Loop 17 Impact-Sync Cancellation

What was wrong -> Impact sync always awaited one frame before honoring an already-requested cancel/dev skip.
What was done -> Added pre-wait cancellation/dev-skip checks while preserving the required one-frame sync and post-wait check.
Cinematic Cheats used -> None; this keeps the staging deterministic.
Exact Microseconds saved -> Up to one frame under pre-existing cancel/skip. Normal path adds two branch checks, below 1 us.
Verification -> Unity Roslyn primary Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp` compiled with `EXIT=0`.

## 2026-05-14 - Loop 14 Hydration Cancellation And Dynamic LOD

What was wrong -> The hydration wait checked readiness before cancellation and sampled low-tier policy once. A ready ocean plus cancel could still transition to water, and a mid-wait downshift could keep waiting for high-res water.
What was done -> Rewrote the wait as an explicit loop: cancellation/dev skip first, dynamic `IsLowTier` sample second, readiness third, then one-frame Awaitable wait.
Cinematic Cheats used -> Low-tier proxy surface remains the cinematic fake; dynamic sampling lets constrained devices spend less time on black-screen hydration and reach the water beat sooner.
Exact Microseconds saved -> Adds about 1-3 us per hydration wait frame for a quality-tier read; saves unbounded wait time on low-memory/downshift devices.
Verification -> Unity Roslyn primary Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp` compiled with `EXIT=0`.

## 2026-05-14 - Loop 18 Cleanup Fault-Path Hardening

What was wrong -> Director cleanup unlocked input directly in `finally` before clearing `_running`; a future runtime publish fault could strand the service and mask the original cancellation/fault.
What was done -> `_running` is cleared first, final/dev-skip input unlock goes through `ReleaseInputLockNoThrow()`, runtime black-box dump is guarded, and dump-failure telemetry uses a no-throw wrapper.
Cinematic Cheats used -> None; this is recovery-path hardening around the existing cinematic sequence.
Exact Microseconds saved -> 0 us in wait loops. Normal cleanup adds one method call and a non-throwing try boundary; fault paths save investigation time by preserving black-box dumps.
Verification -> Unity Roslyn primary Bee `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Narrative.Prologue.rsp` compiled with `EXIT=0`.
