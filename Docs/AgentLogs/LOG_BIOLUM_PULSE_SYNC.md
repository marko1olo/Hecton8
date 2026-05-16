# BIOLUM_PULSE_SYNC Log

## 2026-05-16 Start

What was wrong: Existing flora glow is driven by scattered globals and legacy controller paths. Prompt requires a single global heartbeat and no per-object material property blocks.

What was done: Assignment extracted from `Docs/Tasks/CURRENT_BATCH.md`; relevant mandates and stable docs read; no stale `Status_BIOLUM_PULSE_SYNC.md` or `Rationale_BIOLUM_PULSE_SYNC.md` existed.

Cinematic Cheats used: Deterministic shader-global pulse selected over physical or per-flora simulation.

Exact Microseconds saved: Pending compile/profiler proof. Static estimate range: 45-120 us/frame in dense flora scenes.

## 2026-05-16 Implementation

What was wrong: Flora/floor/ocean biolum globals existed, but there was no VFX-domain owner for a synchronized multi-profile pulse, no OSHINO binary ingestion path, no acoustic ping strobe, no 15 Hz overload downgrade, and predators were not guaranteed to read the same global heartbeat.

What was done: Added `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`. It cold-loads `Biolum_Profiles.bin` from StreamingAssets/Docs when present, otherwise seeds deterministic fallback profile floats into `NativeArray<float>`. It schedules `BiolumVisualSyncJob` Burst work into a fixed `NativeArray<float4>` and uploads `_GlobalBiolumStates` with `Shader.SetGlobalVectorArray`. It consumes `AupShiftSignal`, `FrameTimeSignal`, and `AcousticPingSignal`; uses H8 dispatcher time; limits overload updates to 15 Hz; and records a 300-tick native blackbox dumping `Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin` on non-finite output. Patched `Hecton_ProceduralBio`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, `Hecton_LeviathanOrganic`, and `Hecton_LeviathanTentacleIndirect` to consume the global array while retaining legacy fallback intensity.

Cinematic Cheats used: One global shader heartbeat replaces per-flora physical simulation. Acoustic pings are faked as a white HDR emissive strobe instead of spawned lights, particles, or renderer traversal. Spatial variety is a hash/phase lane pick, not per-entity state.

Exact Microseconds saved: Estimated 45 us/frame from removing per-material pulse math in dense flora, 35 us/frame from avoiding per-renderer material state churn, 10 us/frame from low-tier one-state upload, 30 us/frame under overload by dropping job cadence to 15 Hz, and 200 us+ avoided per ping burst by not spawning lights/particles. Profiler proof is blocked by external compile failures.

Validation: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` failed outside BIOLUM. Observed external blockers include missing `Hecton8.VFX.Wakes`, `IDockingAutopilotService`, `LightShaftContribution`, duplicate `LockstepStateValidator.SanitizeFinite`, and `IEcosystemDirectorService` signature drift. No BIOLUM compile errors surfaced before the external wall.

## 2026-05-16 Multiplatform Inquisition Pass

What was wrong: BIOLUM still held private persistent NativeArray fields, used Pack=4 telemetry, and loaded profile floats through per-value binary reads. High-tier visuals were multi-lane, but not overdriven per-pixel.

What was done: Evicted BIOLUM native storage into `GlobalDataVault` with new buffer IDs `BiolumProfileFloats`, `BiolumGlobalStates`, and `BiolumBlackBox`. The runtime now stores only `VaultBufferHandle<T>` and resolves locked transient views for jobs/writes. Packed `BiolumPulseTelemetryEntry` as explicit Pack=1 Size=40 for ARM64/Quest. Replaced profile loading with a 512-byte stackalloc sequential read. Added High/Ultra shader secondary-lane overdrive driven by `_GlobalBiolumClock`, while Low/MX350 remains one global Dear Lie.

Cinematic Cheats used: Low tier uses one global pulse plus shader triangle overdrive off. High tier uses secondary lane interference from the existing global array rather than real lights, compute particles, raymarch volume, or per-flora simulation.

Exact Microseconds saved: Native eviction does not claim frame savings; it buys DataVault ownership and fragmentation control. Stackalloc sequential profile read is estimated to reduce cold MicroSD read overhead by 50-200 us versus per-float `BinaryReader` dispatch. High-tier overdrive deliberately spends extra pixel ALU; exact GPU cost requires capture after the external compile wall.

Validation: Static scan shows no BIOLUM `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, material clone, `string.Format`, or `Debug.Log`. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails externally at `GlobalDataVault.ValidateAbiLayout` duplicate before BIOLUM validation.

## 2026-05-16 Job-System Inquisition Pass

What was wrong: The DataVault pass still let `CompleteScheduledJobAndPublish()` call `JobHandle.Complete()` from `Tick()` without first proving the VISUAL_SYNC job had finished. That is a latent frame spike on saturated worker threads and violates the phase discipline even when the job payload is small.

What was done: Added explicit telemetry flags for non-finite output and job overruns. The runtime now checks `_stateJobHandle.IsCompleted` before publishing. If the job overruns, it keeps the last uploaded shader state, records the overrun in the blackbox, and does not schedule a second job while the profile/state DataVault buffers remain locked. Removed the defensive per-tick update self-registration attempt so the dispatcher registration stays in lifecycle code.

Cinematic Cheats used: On a late job, the shader reuses the previous global pulse for one frame instead of blocking the main thread or recomputing sine math on the CPU. That is the correct Dear Lie: visual continuity over simulation purity.

Exact Microseconds saved: Normal frame savings are 0 us because a completed handle still finalizes immediately. Worst-case saved time is the avoided worker-fence stall; exact microseconds require Unity profiler capture after external compile walls are cleared. The prior static estimates remain unchanged: 45 us/frame dense-flora sine avoidance, 35 us/frame renderer-state churn avoidance, 10 us/frame low-tier lane reduction, 30 us/frame overload cadence reduction, and 200 us+ per ping burst from avoiding spawned lights/particles.

Validation: `dotnet build Hecton8.Core.csproj --no-restore -v:diag -m:1 /p:UseSharedCompilation=false` still fails outside BIOLUM. Current first blockers are `GameBootstrapper.Initialize` signature drift and `ToolDurabilitySystem` missing `_itemStates`, `_pendingDecayDt`, `_wearMultipliers`, `_slotActive`, `_breakdownEvents`, and `_disposeHandle`. Filtered build evidence shows no `BiolumPulseSyncRuntime`, `GlobalDataVault`, `H8Memory`, or `HectonShaderGlobalDataVaultBridge` errors. Static scan still finds no BIOLUM `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, local `new NativeArray`, or private persistent `NativeArray` fields.

## 2026-05-16 Shader NaN Sweep

What was wrong: `Hecton_SargassumMaster` still used direct division by radius squared in cut-mask and propwash falloff math in both the forward path and the duplicate shadow/depth helper path. The radius had a clamp, but this was still weaker than the project NaN vaccination standard.

What was done: Replaced the direct divisions with `rcp(max(radiusSq, 0.0001))` and multiplication. The visual equation is unchanged under normal radii; the denominator floor is explicit for mobile GPU compilers.

Cinematic Cheats used: Same dot-product radius fake, safer reciprocal form. No physical propwash or cut simulation was added.

Exact Microseconds saved: 0 us claimed. This is a stability patch, not an optimization patch.

Validation: Follow-up shader scan shows the edited Sargassum radius sites now resolve through `rcp(max(...))`; no direct `washDistSq / (washRadius * washRadius)` or `dot(delta, delta) / (radius * radius)` remains in that shader.

## 2026-05-16 Blackbox And Lifecycle Sweep

What was wrong: The blackbox dump ABI did not match the Pack=1 telemetry entry because it wrote selected fields through `BinaryWriter` and skipped reserved bytes. The runtime also still performed external wiring from `Awake()` and could re-read profile data on repeated enables.

What was done: Replaced the dump path with raw unmanaged writes: a Pack=1 16-byte dump header plus 300 raw Pack=1 40-byte telemetry entries. Removed `Awake()` external DataVault work. Tick now uses cached `_dataVault` only and fails closed if lifecycle injection did not provide it. Added `_profilesLoaded` so `Biolum_Profiles.bin` is read once per runtime lifetime unless the editor reload command explicitly resets it.

Cinematic Cheats used: Reusing the vaulted profile after first cold load preserves the same global heartbeat without touching disk again. On low hardware the visual lie stays one global pulse; no repeated profile import is allowed to steal frame time.

Exact Microseconds saved: Blackbox ABI patch claims 0 us hot-path savings. Profile I/O latch avoids an estimated 50-200 us MicroSD cold-read stall on each repeated enable after the first load. Tick registry fallback removal avoids an unbounded hot-path lookup if lifecycle setup fails; exact microseconds are not claimed.

Validation: Static scan reports no BIOLUM `Awake`, `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, `BinaryWriter`, local `new NativeArray`, or private persistent `NativeArray` fields. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` still fails outside BIOLUM: `EcosystemRuntimeInstaller` cannot find `Hecton8.AI.Ecosystem`, and `SubmarineFluidDynamics` cannot find `VaultNativeBuffer<>`. No BIOLUM/DataVault/H8Memory/shader-bridge compile errors surfaced.

## 2026-05-16 VISUAL_SYNC Overrun Tripwire

What was wrong: Job overruns were recorded as flags, but a permanently late VISUAL_SYNC job could keep stale shader states forever without forcing a dump.

What was done: Added `_jobOverrunFrames` and a 300-frame threshold. If `_stateJobHandle.IsCompleted` stays false for 300 consecutive ticks, BIOLUM dumps the fixed-size blackbox with reason `TelemetryFlagJobOverrun`.

Cinematic Cheats used: The shader keeps rendering the last valid global pulse while the job is late. That is intentional visual continuity, not hidden simulation truth.

Exact Microseconds saved: 0 us claimed. This is survival instrumentation. Normal cost only occurs during overrun and is a saturated integer increment plus one branch.

Validation: Static scan still reports no BIOLUM `Awake`, `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, `BinaryWriter`, local `new NativeArray`, or private persistent `NativeArray` fields. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM in `ArchitectEyeVisualizer` (`VaultProbeUtility.IsFinite`) and `EcosystemPopulationBalancer` (`SignalBusRegistry`, `EntityDeathSignal`, ref return issue). No BIOLUM compile errors surfaced.

## 2026-05-16 Hot-Path Vault Acquisition Sweep

What was wrong: `Tick()` still called `EnsureVaultBuffers()`, and that function can request DataVault handles if a handle is absent. That is cold setup hiding inside VISUAL_SYNC.

What was done: Added `HasVaultBuffers()` as a pure cached-handle validator. Tick now returns immediately if vault handles are not already present. Profile/blackbox lock helpers and job scheduling also use `HasVaultBuffers()` in hot paths. Only lifecycle/editor-cold code can call `EnsureVaultBuffers()`.

Cinematic Cheats used: None added. This is architectural hygiene: if lifecycle setup failed, BIOLUM does not invent private fallback data or allocate in-frame.

Exact Microseconds saved: 0 us claimed in normal frames. Worst-case avoided cost is unbounded DataVault handle acquisition inside a frame; no fake profiler number is recorded.

Validation: Static scan reports no BIOLUM `Awake`, `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, `BinaryWriter`, local `new NativeArray`, or private persistent `NativeArray` fields. Shader scan reports no remaining direct Sargassum radius-squared division patterns. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM: duplicate `ArchitectEyeVisualizer.ValidatePackedStructSizes` and ambiguous `LaserCutterEventPayload` contracts in `PlayerCriticalProceduralAudioRenderer` and `AbyssalThermalManager`. No BIOLUM compile errors surfaced.

## 2026-05-16 AUP Overflow Quarantine

What was wrong: BIOLUM rejected non-finite AUP shift deltas, but the accumulated `_aupOriginOffset` itself was not revalidated before shader upload or telemetry.

What was done: Added `TelemetryFlagAupInvalid`. After consuming AUP shifts, the accumulated offset is checked; if non-finite, it resets to zero and dumps the 300-frame blackbox before shader upload.

Cinematic Cheats used: On invalid AUP offset, spatial phase falls back to stable zero offset. That preserves a coherent pulse rather than corrupting the GPU pipeline.

Exact Microseconds saved: 0 us claimed. This is a survival guard; cost is one finite vector check after shift consumption.

Validation: Static scan remains clean for BIOLUM hot-path allocation/delegate/logging/update debt. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM in `DroneFleetManager` and `DroneCognitionJob` due to Construction drone `double3`/`float3` AUP drift and missing `ToDouble3`/`ToFloat3`. No BIOLUM compile errors surfaced.

## 2026-05-16 DataVault Stale Handle Sweep

What was wrong: BIOLUM cached DataVault handles across disable/re-enable and did not track vault generation. A vault replacement or relocation could leave `BiolumProfileFloats`, `BiolumGlobalStates`, or `BiolumBlackBox` handles with stale pointers even though their BufferID and length still looked valid.

What was done: Added `_vaultGenerationId`, lifecycle handle release, vault-instance invalidation, and a no-allocation generation refresh path using `TryGetBufferHandle` for existing BIOLUM buffers. Cold `GetBufferHandle` allocation remains confined to lifecycle/editor setup. If the profile buffer was actually missing, `_profilesLoaded` is cleared so the cold loader reseeds or rereads the 512-byte profile instead of trusting zeroed memory.

Cinematic Cheats used: None added. The visual result is preservation of the last coherent global heartbeat through vault churn, with no private fallback arrays.

Exact Microseconds saved: 0 us claimed. This prevents stale native-pointer faults and avoids reinstating per-frame DataVault allocation. Rare generation drift does metadata lookup only; profiler proof is still blocked by external compile errors.

Validation: Static BIOLUM scan reports no `EventBus`, managed delegates, `MaterialPropertyBlock`, local `new NativeArray`, private persistent `NativeArray`, or `string.Format`. Unity lifecycle scan only matches `TryRegisterUpdate`, not a `Update()` method. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM in `GameBootstrapper` (`object` to `IDataVault`), `FluidFeedbackListener` (`_queueHash`, `PendingEventCapacity`), and PlayerTool API drift (`OnToolBroken`, `OnDurabilityLow`, `OnToolUsed`). No BIOLUM/DataVault/H8Memory/shader-bridge errors surfaced.

## 2026-05-16 Shader Overkill And Denominator Sweep

What was wrong: High/Ultra BIOLUM visuals were richer than low tier, but still too clean for top hardware because the shader helpers only blended one neighboring lane. Predator sync shaders also still had a few denominator sites outside the BIOLUM helper that relied on prior clamps instead of explicit reciprocal floors.

What was done: Added a High/Ultra-only ALU branch to the global BIOLUM helpers in `Hecton_ProceduralBio`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, `Hecton_LeviathanOrganic`, and `Hecton_LeviathanTentacleIndirect`. It layers world-position filament/spark modulation over the secondary global lane without new buffers, textures, compute kernels, or material property blocks. Patched leviathan wound/core radius and abyssal-flow spacing math to use `rcp(max(...))`.

Cinematic Cheats used: Faux particle/salt-crystal sparkle through triangle-wave filament math. This buys the visual read of high-density biolum particles without spawning particles or increasing CPU upload bandwidth.

Exact Microseconds saved: 0 us claimed. Low tier skips the branch and keeps the single-state Dear Lie. High/Ultra intentionally spends extra fragment ALU; exact GPU microseconds require Unity/RenderDoc capture after external compile walls are cleared.

Validation: Shader scan reports no `numthreads`, `RWStructuredBuffer`, `groupshared`, `SV_Group`, `DirectX`, or `d3d` constructs in the touched shader set. Follow-up scan reports no remaining `local.x / horizontalSpacing`, `local.y / verticalSpacing`, `rcp(coreRadius * coreRadius)`, `rcp(woundRadiusSq)`, or `rcp(bodyLength)` patterns in the patched predator shaders. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM in `TetherManager` (`_fixedStepClockSeconds`, `TetherFixedClockWrapSeconds`) and `PlayerCriticalProceduralAudioRenderer` (`ClearVaultBackedAudioBufferAliases`). No BIOLUM/DataVault/H8Memory/shader-bridge errors surfaced.

## 2026-05-16 OSHINO Profile Extension Sweep

What was wrong: The BIOLUM prompt references OSHINO `.h8bin`, while the numbered task names `Biolum_Profiles.bin`. Runtime discovery only checked `.bin`, so real OSHINO output with `.h8bin` would be ignored.

What was done: Added cold-path discovery for `Biolum_Profiles.h8bin` beside the existing `.bin` lookup in StreamingAssets, Docs/Generated, and Docs. The loader still reads the same 512-byte profile payload into the same DataVault buffer.

Cinematic Cheats used: None. This is data contract hygiene; deterministic fallback remains only when no authored profile file exists.

Exact Microseconds saved: 0 us claimed. Hot path unchanged. Cold discovery adds bounded file-existence checks only.

Validation: Pending build attempt after this patch. Static I/O scan still confines file work to profile cold load and blackbox dump paths.

## 2026-05-16 OSHINO Validation Follow-Up

What was wrong: Build validation after `.h8bin` discovery was still pending, and the prior log did not carry the final compile-wall evidence.

What was done: Re-ran the XML assignment extraction, static BIOLUM debt scans, shader portability scan, profile-I/O scan, and `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly`.

Cinematic Cheats used: None added in this validation pass. The implemented visual cheat set remains: low-tier single global pulse, overload 15 Hz sine cadence, acoustic strobe as shader fake light, and High/Ultra ALU filament/spark overdrive from `_GlobalBiolumClock`.

Exact Microseconds saved: 0 us newly claimed. This pass is validation and documentation only.

Validation: Static runtime scan found no BIOLUM `EventBus`, managed delegate, `MaterialPropertyBlock`, local `new NativeArray`/`NativeList`/`NativeHashMap`, private persistent native container, `string.Format`, or forbidden Unity lifecycle `Awake`/`Update`/`FixedUpdate`/`LateUpdate` method. Shader scan found no `numthreads`, `RWStructuredBuffer`, `groupshared`, `SV_Group`, `DirectX`, or `d3d` constructs in the touched BIOLUM consumer shaders. Build attempt 12 fails outside BIOLUM with 23 errors in `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs` and `Assets/_Project/Scripts/World/EcosystemDirector.cs`; no BIOLUM, `GlobalDataVault`, `H8Memory`, or shader-bridge compile errors surfaced before the wall.

## 2026-05-16 Blackbox ABI Compression And Final Build

What was wrong: BIOLUM blackbox entries were explicit Pack=1 but still 40 bytes. The project blackbox registry calls for fixed 32-byte records, and the status still carried a stale external compile block.

What was done: Compressed `BiolumPulseTelemetryEntry` to explicit Pack=1 Size=32. The packet now stores frame, active profile id, active state count, quality tier, flags, strobe, primary HDR intensity, time, and X/Z AUP phase. The dump header still writes a 16-byte Pack=1 header and now reports 32-byte entries. Re-ran the full core build.

Cinematic Cheats used: None added. Existing Dear Lie paths remain: one-state low-tier pulse, 15 Hz overloaded sine cadence, acoustic strobe through shader globals, and High/Ultra filament/spark ALU overdrive.

Exact Microseconds saved: 0 us claimed. Crash dump payload drops from 12000 bytes to 9600 bytes for the 300 telemetry records, excluding the 16-byte header.

Validation: ABI scan confirms `BlackBoxEntrySizeBytes = 32`, `BiolumPulseTelemetryEntry` is `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`, and the dump header is Pack=1 Size=16. Static scans report no BIOLUM local native allocations, persistent private native containers, managed delegates, legacy EventBus, MaterialPropertyBlock, forbidden Unity lifecycle update methods, or `string.Format`. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` succeeded with 4 warnings and 0 errors.

## 2026-05-16 Live Worktree Compile Regression

What was wrong: A later build attempt in the live multi-agent worktree no longer matched the earlier clean result. Current project compile fails outside BIOLUM.

What was done: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` and captured the actual error wall. Updated status and rationale so current validation is not misreported.

Cinematic Cheats used: None.

Exact Microseconds saved: 0 us claimed.

Validation: Latest build attempt fails with 12 external errors: `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` cannot resolve `SaturateFinite01`, and `Assets/_Project/Scripts/TetherInstance.cs` cannot resolve `ReleaseVisualBuffers`, `ReleaseGraphicsBuffer`, or `EnsureVisualGraphicsBuffer`. BIOLUM static scans remain clean for native allocation, private native ownership, legacy EventBus/delegates, MaterialPropertyBlock, forbidden lifecycle update methods, and `string.Format`.

## 2026-05-16 High-Tier Spectral Haze And Clean Build

What was wrong: High/Ultra BIOLUM pulse had lane count, secondary interference, and spark filaments, but still lacked the broad luminous body expected on top-tier hardware. The status also carried a stale external compile wall from a prior live worktree snapshot.

What was done: Added high-tier-only `godHaze` modulation to `Hecton_ProceduralBio`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, `Hecton_LeviathanOrganic`, and `Hecton_LeviathanTentacleIndirect`. The haze uses existing `_GlobalBiolumStates`, `_GlobalBiolumParams`, and `_GlobalBiolumClock`; it adds no buffers, textures, compute kernels, particles, MaterialPropertyBlocks, or CPU-side uploads. Updated status and rationale after the current build gate changed.

Cinematic Cheats used: Spectral halo fake through triangle-wave ALU and secondary-lane pulse overdrive. Low/MX350 remains the one-state Dear Lie; High/Ultra spend fragment ALU for extra glow density.

Exact Microseconds saved: 0 us newly claimed. This pass intentionally spends high-tier GPU ALU. Prior low-tier savings remain from one-state publication, no per-renderer material churn, 15 Hz overload cadence, and shader fake strobe.

Validation: Shader portability scan found no `numthreads`, `RWStructuredBuffer`, `groupshared`, `SV_Group`, `DirectX`, or `d3d` constructs in the five touched shaders. Domain debt scan found no BIOLUM local native allocation, allocator use, legacy EventBus, managed delegate, MaterialPropertyBlock, finder calls, `string.Format`, or forbidden Unity lifecycle update methods. ABI scan confirms BIOLUM blackbox entry Pack=1 Size=32 and dump header Pack=1 Size=16. Path-limited `git diff --check` reports only CRLF normalization warnings. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.

## 2026-05-16 Diffusion SSGI Survival Sweep

What was wrong: BIOLUM diffusion/SSGI shaders were still weaker than the flora/predator helpers. `BiolumDiffusion.compute` trusted `_HectonBiolumPointCount`, could write non-finite radiance through corrupted point or volume inputs, and did not clamp HDR volume output. `Hecton_BiolumSSGI.compute` and the composite shader had finite guards in places, but not at every output boundary.

What was done: Added finite sanitizers and HDR `[0,10]` clamps to `BiolumDiffusion.compute`, `Hecton_BiolumSSGI.compute`, and `Hecton_BiolumSSGIComposite.shader`. Bounded diffusion point injection to 32 GPU points, added zero-size target guards, sanitized point payloads, and converted render-output float divisions to reciprocal floors where applicable.

Cinematic Cheats used: Kept the volumetric glow and SSGI as bounded visual fakes. No physical light simulation, no extra particles, no CPU-side point validation loop, and no new buffers.

Exact Microseconds saved: 0 us claimed. This pass is survival/bounded-work hardening. Low-tier protection is the 32-point cap and finite output clamp; High/Ultra still spend GPU ALU on the existing glow path.

Validation: Thread-group scan confirms BIOLUM compute kernels remain 4x4x4 or 8x8x1, 64 threads per group and below Metal's 1024-thread limit. Shader scan found no `groupshared`, `SV_Group`, DirectX token, append/consume buffer, or interlocked path in the touched BIOLUM compute/composite files. Path-limited `git diff --check` reports only CRLF normalization warnings. Current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` fails outside BIOLUM with 23 errors in `Assets/_Project/Scripts/World/EcosystemDirector.cs` around missing index fields/helpers: `ClearIndexEntries`, `TryUpsertIndexEntry`, `TryFindIndexEntry`, `ResolveVaultIndexCapacity`, `_sectorIndexByKey`, `_biomassIndexByKey`, and `BiomassLotkaVolterraJob.CellIndexByKey`.

## 2026-05-16 Shader Cast Hygiene And Clean Build

What was wrong: The diffusion/SSGI hardening still left two avoidable cross-compiler risks: implicit uint-to-int texture-dimension conversion in `BiolumDiffusion.compute`, and half-precision finite checks in `Hecton_BiolumSSGIComposite.shader`.

What was done: Made the 3D texture clamp coordinate conversion explicit with signed casts, and changed the composite finite tests to cast half vectors/scalars to float before `isfinite`. Re-ran static scans and the full C# compile gate.

Cinematic Cheats used: None added. Existing visual fake stack remains: one-state low-tier global pulse, 15 Hz overload cadence, acoustic strobe through shader globals, High/Ultra filament/spark/spectral haze, and bounded BIOLUM diffusion/SSGI glow.

Exact Microseconds saved: 0 us claimed. This is portability hardening, not an optimization.

Validation: BIOLUM C# debt scan found no local native allocation, allocator use, legacy EventBus, managed delegate, MaterialPropertyBlock, finder call, `string.Format`, or forbidden lifecycle update method. Shader portability scan found no `groupshared`, `SV_Group`, DirectX token, append/consume buffer, or interlocked path in the touched BIOLUM shader set; compute kernels remain 64 threads per group. Path-limited `git diff --check` reports only CRLF normalization warnings on the touched shader/doc files. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` succeeded with 0 warnings and 0 errors.
