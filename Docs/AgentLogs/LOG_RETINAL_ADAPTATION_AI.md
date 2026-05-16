# RETINAL_ADAPTATION_AI Log

## 2026-05-16 - Predator Headlight Retinal Adaptation
What was wrong:
- Predator retinal math used inverted negative dot semantics, making the prompt's `dot > 0.9` requirement easy to regress.
- Recovery in darkness used linear subtraction instead of frame-rate invariant exponential decay.
- Retinal low-cadence mode only followed hardware tier, not runtime frame stress.
- High-tier blinded predators had no deterministic thrash injection; frenzy species did not max aggression on retinal blindness.
- The new `AI/Perception` helper was missing from the explicit `Hecton8.Core.csproj` compile include list.

What was done:
- Added `Assets/_Project/Scripts/AI/Perception/RetinalExposureMath.cs` with positive predator-to-light dot helpers, 0.9 glare threshold, hold-glare threshold, direct glare scalar, and deterministic triangle wave.
- Updated `PredatorCognitionDomain` to use pure dot-product headlight exposure, finite guards, exponential Pade decay, runtime stress cadence, high-tier retinal thrash, and frenzy aggression clamp to `1f`.
- Preserved the existing `NativeArray<float> _retinalExposure`, `NativeArray<byte> _blindnessState`, signal-fed light cache, and 300-frame retinal black-box telemetry ring.
- Added the compile include for the new perception helper in `Hecton8.Core.csproj`.

Cinematic Cheats used:
- Dot-product cone fake instead of light raycasts.
- Capped 4-light retinal cache instead of scene light polling.
- Triangle-wave thrash instead of random physics impulses.
- Pade exponential decay instead of coroutine/timer recovery.
- Low/stressed 1 Hz retinal cadence instead of 10 Hz checks under pressure.

Exact Microseconds saved:
- Measured exact savings: unavailable; no profiler baseline was run in this session.
- Static budget result: 0 Unity raycasts, 0 collider triggers, 0 managed allocations in the touched retinal path.
- Estimated i3/MX350 impact: avoids an O(predators * lights) physics query path; added work is bounded to <=4 scalar light checks per due predator and high-tier thrash only while blinded.
- Runtime impact of the `Hecton8.Core.csproj` bridge entry: 0 us/frame.

Verification:
- `rg` over the touched retinal path returned `NO_RETINAL_QUERY_OR_ALLOC_MATCHES` for raycasts, overlap queries, managed list/LINQ patterns, and coroutine markers.
- `rg -n "LightTrigger" Assets Packages ProjectSettings` returned `NO_ACTIVE_LIGHTTRIGGER_MATCHES`.
- Prompt block was re-extracted from `Docs/Tasks/CURRENT_BATCH.md` after core work.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` fails outside this task: missing `Hecton8.Core.Determinism`, missing `Hecton8.Physics.KCC`, missing `Hecton8.Animation.Fauna`/IK event types, and `HectonMarineSnowRenderer` does not implement the updated vehicle signal listener interface.

## 2026-05-16 - Multiplatform / H-Phi Inquisition Pass
What was wrong:
- Retinal state still used local persistent `NativeArray` allocations in `PredatorCognitionDomain`.
- Retinal light and telemetry records did not explicitly lock ARM64-safe Pack=1 strides.
- The existing Blind fauna signal had no high-tier presentation consumer for chaotic biolum flash.

What was done:
- Added `RetinalAdaptationVault` under `Assets/_Project/Scripts/AI/Perception/`.
- Added `BufferID.PredatorRetinalExposure`, `PredatorRetinalBlindnessState`, `PredatorRetinalLastPublishedBlindnessState`, `PredatorRetinalLightSources`, and `PredatorRetinalTelemetryRing`.
- Converted retinal buffers to GlobalDataVault aliases and stopped disposing/registering them as local PredatorCognitionDomain allocations.
- Packed `LightSourceData` to Pack=1 Size=96 and `RetinalTelemetryEntry` to Pack=1 Size=32 with explicit reserved tails.
- Added a high-tier-only biolum strobe in `FaunaBrain` that consumes `SignalBus<FaunaStateChangedSignal>.GetFrameSnapshot()` as `ReadOnlySpan<FaunaStateChangedSignal>`.

Cinematic Cheats used:
- Same dot-product cone fake; no raycasts.
- Triangle-wave strobe and thrash; no random, no physics impulses, no new VFX manager dependency.
- Low/stressed devices keep the cheap AI response; high-tier devices spend scalar cycles on visible biolum flicker.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Runtime DataVault alias cost after cold resolve: 0 us/frame.
- Removed retinal local persistent allocation ownership: cold-path fragmentation risk reduced; exact frame savings not measurable from static analysis.
- Added high-tier strobe cost: nonzero only on high/ultra fauna ticks; estimated span scan plus two triangle waves, not measured.

Verification:
- `rg` returned `NO_LOCAL_RETINAL_NATIVEARRAY_ALLOCATIONS` for the retinal buffers; only unrelated Alpha telemetry still allocates locally.
- `rg` returned `NO_RETINAL_QUERY_OR_ALLOC_MATCHES` for raycasts, overlaps, LINQ/list allocation markers, coroutines, `string.Format`, and standard Update methods in the AI/Perception + retinal cognition path.
- `rg -n "LightTrigger" Assets Packages ProjectSettings` returned `NO_ACTIVE_LIGHTTRIGGER_MATCHES`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` still fails outside retinal scope: missing `Hecton8.VFX.Wakes`, missing docking autopilot contracts, missing wake structs, and `EcosystemDirector` missing new macro swarm interface members.

## 2026-05-16 - Data Sovereignty Follow-up
What was wrong:
- `PredatorCognitionDomain` still owned a private Alpha Leviathan telemetry ring beside the now-vaulted retinal buffers.
- The first attempt to align with the Alpha stalk director referenced `AlphaLeviathanStalkConstants`, which exists in source but is not visible to this project target.

What was done:
- Converted `_alphaLeviathanTelemetryRing` to a `GlobalDataVault` alias using existing `BufferID.AlphaLeviathanTelemetryRing`.
- Requested the full shared lane shape locally: 300 frames * 64 Alpha slots = 19,200 `AlphaLeviathanTelemetryEntry` records.
- Replaced the private 300-entry cursor write with frame/slot-indexed writes compatible with the existing Alpha telemetry lane.
- Removed the non-compiled `AlphaLeviathanStalkConstants` dependency and kept the lane dimensions as local constants in `PredatorCognitionDomain`.
- Added `ClearRetinalSlot` so fauna register/unregister reset retinal data only when the vault aliases are already resolved.

Cinematic Cheats used:
- No physical simulation added.
- No new signal invented.
- Fault dumps remain cold-path only; no per-frame disk I/O.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Runtime ownership cost after cold DataVault resolve: 0 us/frame.
- Removed one private persistent telemetry allocation owner from the cognition domain; exact fragmentation/frame-time impact cannot be measured from static analysis.

Verification:
- `rg` found no `new NativeArray<.*Retinal`, no `new NativeArray<AlphaLeviathanTelemetryEntry>`, no `AlphaLeviathanStalkConstants`, and no retinal raycasts/casts/overlaps/string.Format/standard `Update()` in `AI/Perception` + `PredatorCognitionDomain`.
- `git diff --check` reported only a CRLF normalization warning for `PredatorCognitionDomain.cs`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` no longer reports errors in `PredatorCognitionDomain`, `RetinalAdaptationVault`, or `RetinalExposureMath`. Remaining failures are external: `DiegeticGyroCompassRuntime` missing runtime buffers/helpers, `TetherFiredSignal` missing `ISignal`, `ItemAcquiredSignal`, HomeostasisBrain hardware/black-box fields/helpers, and `LockstepReplayBlockHeader.HashCadenceFrames`.
