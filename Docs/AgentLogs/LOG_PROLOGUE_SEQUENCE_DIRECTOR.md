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
- Bridge auto-run now uses a dev-only linked cancellation token.
- Dev cancel now calls `CancelSequence(DevSkip)` and cancels the linked token.
- Development-only dilated delay polls skip each frame; release still uses `AwaitableExtension.DelayDilated`.
- Director now converts cancellation reason `DevSkip` into the forced shallow-water hydration, velocity zero, impact, and ocean handoff path instead of recording a plain cancel.

Cinematic Cheats used:
- Dev skip uses forced proxy hydration and signal handoff rather than honest world placement simulation.

Exact microseconds saved:
- Release path: 0 us overhead versus prior `AwaitableExtension.DelayDilated`.
- Dev-only path: costs estimated 5-10 us per wait frame, but removes up to 3 seconds of blocked iteration time after skip input.

Verification:
- Unity Roslyn response-file compile: `Hecton8.Core.Contracts.rsp` passed.
- Unity Roslyn response-file compile: `Hecton8.Narrative.Prologue.rsp` passed after compiling contracts first.
- Unity Roslyn response-file compile: `Hecton8.Core.rsp` failed in unrelated `GroundPenetratingRadarRuntime.cs(309,17)` missing `GroundRadarRaymarchJob.GprOreTypes`; no prologue error emitted before that wall.
