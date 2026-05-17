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

## 2026-05-16 - Native HashMap Vault Eviction / Code Build Green
What was wrong:
- `PredatorCognitionDomain` still owned two local persistent `NativeParallelHashMap` containers after the DataVault `NativeArray` and active-slot passes.
- Species pack target sharing and species tuning lookup therefore still had private native state outside `GlobalDataVault`.
- `SpeciesCognitionTuning` had no explicit Pack=1 ABI contract while becoming a DataVault value payload.

What was done:
- Added DataVault lanes for species target ids, species target positions, target count, species tuning ids, species tuning values, and tuning count.
- Replaced local `NativeParallelHashMap<int, float3>` and `NativeParallelHashMap<int, SpeciesCognitionTuning>` fields with vault-backed SoA aliases.
- Replaced `SwarmAnalysisJob` hash writes with bounded atomic append into species target arrays.
- Replaced job hash lookups with bounded count-window scans for species targets and tuning data.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` to `SpeciesCognitionTuning` and validated its stride through `UnsafeUtility.SizeOf`.

Cinematic Cheats used:
- No physical light simulation was added.
- No raycast, cone collider, Unity `Light` polling, or managed lookup path was introduced.
- Low/toaster path remains bounded dot-product glare, four cached light records, stress cadence, and simple turn-away/flee.
- High/Ultra still spend saved simulation cost on deterministic retinal thrash and typed-lane biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Runtime ownership cost after cold DataVault resolve: 0 us/frame.
- Hash lookup changed to cache-linear bounded scans over <=256 active species records; exact CPU delta was not measured.
- Disk I/O remains fault-only black-box dump; no per-frame status or telemetry file write was added.

Verification:
- `rg` found no `NativeParallelHashMap`, no `NativeHashMap`, no `NativeList`, no `new NativeArray<`, no `new NativeList`, no `new NativeParallelHashMap`, no retinal raycasts/casts/overlaps, no `string.Format`, no standard `Update()`, no legacy headlight queue use, and no managed delegate usage in `PredatorCognitionDomain` or `AI/Perception`.
- PowerShell enum parsing reported `NO_BUFFERID_DUPLICATE_VALUES` and `NO_SYSTEMID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Unity Editor import, Play Mode, GCMonitor, profiler, and player build verification were not executed in this shell-only pass.

## 2026-05-16 - Shared Workspace Build Wall Recheck
What was wrong:
- A fresh project build after the native hash-map eviction report no longer matches the earlier green build state.
- The new failures are outside the retinal/cognition domain and do not cite `PredatorCognitionDomain`, `RetinalExposureMath`, `RetinalAdaptationVault`, `FaunaDataTemplate`, `H8Memory`, or `GlobalDataVault`.
- Reporting the earlier green build as current would be false.

What was done:
- Re-read `Status_RETINAL_ADAPTATION_AI.md`, `Rationale_RETINAL_ADAPTATION_AI.md`, and the original XML prompt before continuing.
- Re-ran the retinal static debt audit.
- Re-ran whitespace checks on touched code/docs.
- Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary`.
- Updated status and rationale to mark the latest build as blocked by external compile errors.

Cinematic Cheats used:
- No extra simulation was added while responding to the build wall.
- The retinal implementation remains the cheap gameplay fake: typed headlight signal, four-slot light cache, dot-product cone exposure, exponential decay, and tier-gated presentation overkill.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Runtime delta from this recheck: 0 us/frame because no runtime code changed.

Verification:
- Static debt audit returned `NO_RETINAL_DOMAIN_DEBT_MATCHES`.
- Narrowed enum parsing returned `NO_SystemID_DUPLICATE_VALUES` and `NO_BufferID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings.
- Latest build exits with 11 external errors: `GameBootstrapper` object-to-`IDataVault` calls, `FluidFeedbackListener` missing `_queueHash`/`PendingEventCapacity`, and `PlayerTool`/`PlayerToolManager`/`PlayerNoiseEmitter` missing tool event members.
- No latest build errors cite the retinal/cognition files.

## 2026-05-16 - Pack=1 Descriptor Polish / Build Green
What was wrong:
- `FaunaDataTemplate.RuntimeDescriptor` was explicit-size 64 bytes but still used `StructLayout(Pack = 4)`.
- The current ARM64/Quest ABI mandate requires Pack=1 native payloads in the retinal/fauna cognition scope.
- The previous external compile wall shifted while the shared workspace changed; current source needed a fresh build, not stale-error reporting.

What was done:
- Changed `RuntimeDescriptor` to `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)`.
- Re-ran Pack audit across `PredatorCognitionDomain`, `AI/Perception`, and `FaunaDataTemplate`.
- Re-ran static retinal debt audit and enum duplicate checks.
- Re-ran `dotnet build` from the current tree.

Cinematic Cheats used:
- No new simulation or physics truth was added.
- Retinal response remains a deterministic visual/gameplay fake: typed headlight signal, four-light cache, dot-product glare, exponential decay, simple low-tier turn-away/flee, and high-tier deterministic thrash/biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Runtime cost of this Pack=1 descriptor closure: 0 us/frame.
- Build validation cost is compile-time only.

Verification:
- Pack audit returned `NO_NON_PACK1_STRUCTLAYOUT_IN_RETINAL_SCOPE`.
- Static debt audit returned `NO_RETINAL_DOMAIN_DEBT_MATCHES`.
- Enum parsing returned `NO_SystemID_DUPLICATE_VALUES` and `NO_BufferID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:Summary` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Unity Editor import, Play Mode, GCMonitor, profiler, and player build verification were not executed.

## 2026-05-16 - Volatile Shared Build Wall Recheck / Current Build Green
What was wrong:
- Parallel workspace edits caused successive external compile walls in `PhysicsApplySystem`, `TetherInstance`, `SargassumMicroFaunaBoids`, and `LockstepStateValidator`.
- Several compiler errors were stale against the source snapshot visible after the build completed.
- Final reporting needed a current-state build, not an earlier green pass or an obsolete failure.

What was done:
- Re-ran retinal static debt audit.
- Re-ran Pack=1 ABI audit.
- Re-ran `SystemID`/`BufferID` duplicate checks.
- Re-ran whitespace validation on touched retinal/code/doc files.
- Re-ran the project build from the current tree.

Cinematic Cheats used:
- No new physical simulation was added.
- Retinal behavior remains the same deterministic fake: typed headlight lane, four cached light records, dot-product retinal exposure, exponential darkness decay, low-tier flee/turn-away, high-tier deterministic thrash and biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Runtime delta from this recheck: 0 us/frame.

Verification:
- Static debt audit returned `NO_RETINAL_DOMAIN_DEBT_MATCHES`.
- Pack audit returned `NO_NON_PACK1_STRUCTLAYOUT_IN_RETINAL_SCOPE`.
- Enum parsing returned `NO_SystemID_DUPLICATE_VALUES` and `NO_BufferID_DUPLICATE_VALUES`.
- `git diff --check` reported no whitespace errors, only CRLF normalization warnings.
- Final current-state `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:BuildInParallel=false -v:minimal -clp:ErrorsOnly` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Unity Editor import, Play Mode, GCMonitor, profiler, and player build verification were not executed.

## 2026-05-16 - Blind-Signal AUP / Helper ABI Closure
What was wrong:
- Blind-state publication could reconstruct absolute universe position from runtime-relative cognition core position.
- Private helper payloads in the cognition job used implicit layout, and the Alpha directive used bool fields that are not a clean cross-platform payload contract.
- Retinal post-evaluation telemetry could index vault aliases without first proving those two specific aliases were created.

What was done:
- `PublishFaunaBlindStateSignal` now resolves its AUP from finite slot input position plus committed floating-origin offset and calls `AbsoluteUniversePosition.FromAbsolutePosition`.
- Retinal post-evaluation telemetry now checks `_retinalExposure.IsCreated` and `_lastPublishedBlindnessState.IsCreated` before indexing.
- `RetinalLightResult` and `AlphaLeviathanDirective` now have explicit `Pack = 1` layouts and fixed sizes; directive booleans were converted to byte flags.
- Re-ran the original XML extraction, status/rationale recovery, targeted retinal debt audit, Pack audit, typed signal duplicate scan, whitespace validation, and current-state build.

Cinematic Cheats used:
- No physical eye model, no light raycast, and no shader path were added.
- The behavior remains the cheap retinal fake: typed headlight lane, four cached light records, dot-product glare, clamped exposure, exponential decay, low-tier turn-away/flee, and high-tier deterministic thrash/biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Hot candidate exposure math is unchanged.
- Added cost is edge-only signal AUP construction plus two telemetry alias guards; exact CPU delta was not measured.

Verification:
- Targeted audit returned `NO_RETINAL_HOTPATH_DEBT_OR_RUNTIME_AUP_RECONSTRUCT_MATCHES`.
- Pack audit returned `NO_NON_PACK1_STRUCTLAYOUT_IN_RETINAL_SCOPE`.
- Typed signal duplicate scan found one `SubmarineLightsChangedSignal` struct and one `FaunaStateChangedSignal` struct.
- `git diff --check` reported no whitespace errors.
- Heartbeat `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildInParallel=false -v:minimal -clp:Summary` is blocked by external error `DiegeticGyroCompassRuntime.cs(1199,27): CS1061 NativeSlice<CompassBlackBoxEntry> does not contain a definition for IsCreated`.
- No emitted build error cites `PredatorCognitionDomain`, `RetinalExposureMath`, `RetinalAdaptationVault`, `FaunaDataTemplate`, `H8Memory`, or `GlobalDataVault`.
- Unity Editor import, Play Mode, GCMonitor, profiler, shader validation, and player builds were not executed.

## 2026-05-17 - Current Build Recovery / Domain Re-Audit
What was wrong:
- The previous build evidence was stale: it referenced an external UI/navigation `NativeSlice.IsCreated` error that had already moved on disk.
- Retinal validation needed a current compiler pass and fresh static audits before claiming any status.

What was done:
- Re-read status/rationale, the original XML prompt, domain boundaries, Unity project workflow notes, and relevant mandates.
- Rechecked `DiegeticGyroCompassRuntime` and made no retinal-agent edit there because the failing line had already changed to a length guard.
- Reran current-state `dotnet build` with heartbeat output and saved the build log.
- Reran retinal debt/AUP audit, Pack=1 audit, typed signal uniqueness scan, LightTrigger purge audit, and 300-frame black-box evidence scan.

Cinematic Cheats used:
- No new physical simulation was added.
- Retinal behavior remains the deterministic fake: typed headlight lane, four cached light records, dot-product glare, clamped exposure, exponential decay, low-tier flee/turn-away, and high-tier deterministic thrash/biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Runtime delta from this loop: 0 us/frame because no runtime source changed.
- Current validation is compiler/static evidence only.

Verification:
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildInParallel=false -v:minimal -clp:Summary` succeeded with `0 Warning(s)`, `0 Error(s)`, `Time Elapsed 00:00:03.65`.
- Build output saved to `Docs/AgentLogs/Build_RETINAL_ADAPTATION_AI_loop17_current.out.txt`.
- Retinal audit returned `NO_RETINAL_HOTPATH_DEBT_OR_RUNTIME_AUP_RECONSTRUCT_MATCHES`.
- Pack audit returned `NO_NON_PACK1_STRUCTLAYOUT_IN_RETINAL_SCOPE`.
- Signal scan returned `SubmarineLightsChangedSignalStructCount=1`, `FaunaStateChangedSignalStructCount=1`, `TYPED_SIGNAL_DEFINITIONS_UNIQUE`.
- LightTrigger audit returned `NO_ACTIVE_LIGHTTRIGGER_MATCHES`.
- Retinal black-box scan confirmed `RetinalTelemetryCapacity = 300`, `TotalBlindPredators`, and `Dump_FAUNA_RETINAL_ADAPTATION.bin`.
- Unity Editor import, Play Mode, GCMonitor, profiler, shader validation, and player builds were not executed.

## 2026-05-17 - Dump-Failure Telemetry Polish
What was wrong:
- Retinal and adjacent Alpha black-box dump failure catches used `Debug.LogError` plus string concatenation.
- That was not hot-path, but it was still weak failure reporting in a critical black-box survival path.

What was done:
- Added `RetinalDumpFailureTelemetryHash` and `AlphaLeviathanDumpFailureTelemetryHash`.
- Replaced both managed log strings with `GlobalTelemetryBus.PublishPerformanceWarning` calls.
- Re-read `AI/Perception` domain files and rescanned the adjacent cognition owner for polish debt.
- Reran static audits and the project build.

Cinematic Cheats used:
- No new simulation or raycast was added.
- Retinal response remains the same deterministic fake: typed headlight lane, four-light cache, dot-product glare, clamped exposure, exponential decay, low-tier flee/turn-away, and high-tier deterministic thrash/biolum strobe.

Exact Microseconds saved:
- Measured exact savings: unavailable; Unity profiler was not run.
- Steady-frame delta is 0 us/frame.
- Failure-path managed string allocation was removed; exact CPU delta was not measured.

Verification:
- Polish debt audit returned `NO_RETINAL_DOMAIN_POLISH_DEBT_MATCHES`.
- Targeted debt/AUP audit returned `NO_RETINAL_HOTPATH_DEBT_OR_RUNTIME_AUP_RECONSTRUCT_MATCHES`.
- Pack audit returned `NO_NON_PACK1_STRUCTLAYOUT_IN_RETINAL_SCOPE`.
- `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:BuildInParallel=false -v:minimal -clp:Summary` succeeded with `0 Warning(s)`, `0 Error(s)`, `Time Elapsed 00:00:54.12`.
- Build output saved to `Docs/AgentLogs/Build_RETINAL_ADAPTATION_AI_loop18_dump_failure_telemetry.out.txt`.
- Unity Editor import, Play Mode, GCMonitor, profiler, shader validation, and player builds were not executed.
