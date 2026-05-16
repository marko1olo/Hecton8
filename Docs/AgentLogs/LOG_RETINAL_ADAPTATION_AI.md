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

## 2026-05-16 - NaN / Signal Edge Polish
What was wrong:
- Retinal cache upsert still trusted dequeued light signal scalars after the global queue boundary.
- Brownout-suppressed lights were not explicitly removed in the retinal signal drain.
- High-tier biolum strobe used frame `0` as the implicit duplicate-suppression sentinel, which can suppress a valid first-frame Blind signal or stale pooled fauna state.
- `GlobalDataVault` had a duplicated `ValidateAbiLayout()` method, blocking compilation of the DataVault surface that retinal state now uses.

What was done:
- Rejected non-finite `SubmarineLightsChangedSignal` AUP/range/intensity/spot values before they can occupy the retinal light cache.
- Reinstated explicit brownout-suppressed light removal in the retinal drain.
- Clamped cached retinal light range to `[0.1, 10000]`, intensity to `[0, 100000]`, and spot cosine to `[-1, 1]`.
- Changed `_lastRetinalBlindSignalFrame` to `uint.MaxValue` and reset it on spawn, despawn, and death presentation.
- Removed the duplicate identical `GlobalDataVault.ValidateAbiLayout()` method body.

Cinematic Cheats used:
- Corrupt light inputs are dropped at the cache boundary; no raycast or physical light query was added.
- High-tier strobe remains deterministic triangle-wave presentation only.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Added sanitation cost is per dequeued light signal, not per predator.
- Runtime cost of duplicate DataVault method removal: 0 us/frame.

Verification:
- `rg` found no retinal local `NativeArray`, no Alpha telemetry local allocation, no retinal raycasts/casts/overlaps, no `string.Format`, and no standard `Update()` in `AI/Perception` + `PredatorCognitionDomain`.
- `git diff --check` reported only CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` no longer reports errors in `PredatorCognitionDomain`, `FaunaBrain`, `GlobalDataVault`, `RetinalAdaptationVault`, or `RetinalExposureMath`. Remaining failures are external: `SargassumMicroFaunaBoids.EnsureVaultBufferHandle`, `HectonMarineSnowRenderer` wake/telemetry fields, and `VehicleDockingModule` runtime-cache helpers.

## 2026-05-16 - Typed Headlight Signal Lane Pass
What was wrong:
- Retinal cognition still consumed `SubmarineLightsChangedSignal` through the legacy compatibility queue method.
- That API shape is destructive and single-consumer by design, while the project mandate requires typed lanes and `ReadOnlySpan<T>` snapshots.

What was done:
- Changed `PredatorCognitionDomain.ProcessSubmarineLightSignals` to consume `SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot()` as `ReadOnlySpan<SubmarineLightsChangedSignal>`.
- Bounded retinal signal processing to the newest 64 headlight records before upserting the existing four-slot retinal light cache.
- Verified the current `GlobalSignals.Publish(in SubmarineLightsChangedSignal)` path already writes the typed lane and the compatibility reader maps back to that lane.

Cinematic Cheats used:
- No physical light query was added.
- No new signal type was created.
- The cheap truth remains a typed headlight packet, four cached light records, dot-product exposure, and tier-gated presentation overkill.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Static runtime delta: legacy destructive queue drain replaced with a bounded `ReadOnlySpan<T>` snapshot scan of at most 64 records.
- Per-predator hot-loop cost unchanged; the four-light dot-product cache remains the only retinal exposure input.

Verification:
- `rg` confirmed `PredatorCognitionDomain` and `SargassumMicroFaunaBoids` consume `ReadOnlySpan<SubmarineLightsChangedSignal>` from `SignalBus<SubmarineLightsChangedSignal>`.
- `rg` found no `_submarineLightsChangedSignals.Enqueue` in the checked path.
- `rg` found no retinal local `NativeArray`, no local Alpha telemetry allocation, no retinal raycasts/casts/overlaps, no `string.Format`, and no standard `Update()` in `AI/Perception` + `PredatorCognitionDomain`.
- `git diff --check` reported no whitespace errors for the typed-lane files and retinal docs.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` no longer reports errors in `PredatorCognitionDomain`, `GlobalSignals`, `FaunaBrain`, `GlobalDataVault`, `RetinalAdaptationVault`, or `RetinalExposureMath`. Remaining failures are external: `ProceduralLadderClimbRuntime`, `EcosystemDirector`, `SubmarineFluidDynamics`, `AcousticEchoLocationRuntime`, and `LockstepStateValidator`.

## 2026-05-16 - Core Cognition NativeArray Vault Eviction
What was wrong:
- Retinal buffers were vaulted, but `PredatorCognitionDomain` still owned broad persistent `NativeArray` lanes locally.
- Several cognition structs still relied on implicit sequential packing or Pack=4, which is not acceptable for ARM64/Quest ABI discipline.
- Partial DataVault resolution could create one alias while leaving required sibling lanes missing.

What was done:
- Added `BufferID.PredatorCognition*` lanes for all domain-owned `NativeArray` cognition state.
- Replaced local persistent `new NativeArray` allocations with `GlobalDataVault.GetBuffer<T>(..., SystemID.AICognition)` aliases.
- Added alias-only teardown for these vault arrays.
- Added partial-resolution cleanup and cold clearing of reused vault buffers on domain initialization.
- Set all cognition structs in `PredatorCognitionDomain` to `Pack = 1` with explicit sizes and added ABI validation.

Cinematic Cheats used:
- No simulation complexity was added.
- The low-tier path still uses bounded dot-product vision, 1Hz stress cadence, and a four-light cache.
- High/Ultra still buy visual overkill through deterministic thrash and biolum strobe from the same vault-owned blind truth.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Runtime ownership cost after cold DataVault resolve: 0 us/frame.
- Cold initialization clearing is bounded to 256-slot lanes and fixed memory banks only.

Verification:
- `rg` found no `new NativeArray<` in `PredatorCognitionDomain` or `AI/Perception`.
- `rg` confirmed all `StructLayout` entries in the retinal/cognition domain are `Pack = 1` with explicit sizes.
- PowerShell enum parsing reported `NO_BUFFERID_DUPLICATE_VALUES` and `NO_SYSTEMID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors for the touched code/docs, only CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` no longer reports errors in `PredatorCognitionDomain`, `H8Memory`, `GlobalSignals`, `FaunaBrain`, `GlobalDataVault`, `RetinalAdaptationVault`, or `RetinalExposureMath`. Remaining failures are external: `SargassumMicroFaunaBoids`, `HectonUnderwaterVisuals`, and `RepairTool`.

## 2026-05-16 - Active Slot Vault Alias / Code Build Green
What was wrong:
- The retinal/cognition owner still had one local persistent `NativeList<int>` for active slots after the broad `NativeArray` migration.
- Job scheduling and swarm neighbor iteration previously depended on list length; a raw vault array needs an explicit dense active count or it risks scanning cleared capacity.

What was done:
- Added `BufferID.PredatorCognitionActiveSlots`.
- Converted `_activeSlots` in `PredatorCognitionDomain` from `NativeList<int>` to a `GlobalDataVault` `NativeArray<int>` alias.
- Added `_activeSlotCount` and rewired activation, deactivation, telemetry scans, swarm bounds, and job schedules to use the dense count.
- Reset `_activeSlotCount` on partial vault resolution failure, alias release, successful cold clear, and full dispose.
- Hardened `Register`, `Unregister`, and `SetSlotActive` against unavailable vault aliases instead of indexing default arrays.

Cinematic Cheats used:
- No new physical simulation was added.
- The low-tier path remains bounded dot-product glare plus simple turn-away/flee.
- High/Ultra still buy visual overkill through deterministic retinal thrash and biolum strobe from the same blind truth.

Exact Microseconds saved:
- Measured exact savings: unavailable; profiler was not run.
- Runtime ownership cost after cold DataVault resolve: 0 us/frame.
- Avoids stale 256-slot capacity scans in active-slot consumers; exact CPU delta not measured.

Verification:
- `rg` found no `NativeList`, no `new NativeArray<`, no `new NativeList`, no retinal raycasts/casts/overlaps, no `string.Format`, no standard `Update()`, no legacy `GlobalSignals.TryDequeueSubmarineLightsChanged`, and no managed delegate usage in `PredatorCognitionDomain` or `AI/Perception`.
- `H8Memory` still contains allocator-internal `NativeList` registries; this is the memory subsystem owner, not the retinal/cognition domain.
- PowerShell enum parsing reported `NO_BUFFERID_DUPLICATE_VALUES` and `NO_SYSTEMID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Unity Editor import, Play Mode, GCMonitor, profiler, and player build verification were not executed in this shell-only pass.
