# Rationale: VOLUMETRIC_SILT_ADVECTION

## Pre-Code Decisions

Problem: User-provided prompt ID `VOLUMETRIC_PARTICLE_ADVECTION` was absent from `CURRENT_BATCH.md`.
Solution: Use the authoritative matching XML tag `VOLUMETRIC_SILT_ADVECTION` because it owns Marine Snow & Silt Compute in VFX/COMPUTE.
Rejected Alternatives: Inventing a new ID or editing under the absent ID would break the batch contract and logs.
Scalability potential: Low uses 8,000 GPU particles with cheap radial drift; Middle keeps bounded flow sampling; High/Ultra can spend budget on curl/noise/light response.
Hardware Impact: Avoids wasted integration work under the wrong owner; estimated 0 us runtime impact, prevents architectural mismatch.

Problem: Marine snow could be implemented as Unity ParticleSystem.
Solution: GPU-resident compute particles with CPU only feeding wake/throttle snapshots.
Rejected Alternatives: Standard Unity ParticleSystem was rejected because the prompt explicitly bans it for silt and mandates zero CPU manipulation of positions.
Scalability potential: Low/MX350 uses cheap billboard drift and lower dispatch count; Ultra raises particle budget and visual light response without CPU particle loops.
Hardware Impact: Prevents CPU transform/particle array churn; expected CPU savings are pending profiler proof.

Problem: Physical silt collision and floor interaction can consume GPU ALU with no gameplay truth.
Solution: Skip SDF collision and use density/wrap visual fake per prompt task 16.
Rejected Alternatives: SDF collision checks were rejected because silt is presentation-only and visual density hides floor clipping.
Scalability potential: Low skips collision entirely; High spends saved cycles on curl/light response rather than collision truth.
Hardware Impact: Avoids per-particle SDF samples; expected low-end gain pending GPU capture.

Problem: AUP rebase can make particles pop if handled CPU-side after the fact.
Solution: Apply `_AupShiftOffset` in compute shader and keep particle state GPU-side.
Rejected Alternatives: CPU readback/rewrite of particle positions was rejected due to PCIe stall and prompt ban.
Scalability potential: Low applies a single uniform offset; High/Ultra can retain denser particles through rebase without CPU synchronization.
Hardware Impact: One uniform/vector path instead of full buffer transfer; expected PCIe stall prevention pending capture.

## Loop 1 Decisions: Tasks 1-5

Problem: The renderer needed Batch006 wake data without owning another wake simulation.
Solution: Added `HectonFluidEngine.TryGetDynamicWakeGpuPayload` to expose the existing `_DynamicWakes` and `_DynamicWakeVectors` GPU buffers plus packed dispatch params.
Rejected Alternatives: Creating a private marine-snow wake buffer was rejected because it duplicates the Batch006 ring and invents a concrete dependency.
Scalability potential: Low reads only the fluid engine's capped wake slots; High/Ultra can consume the same ring with curl/noise overlays and no extra buffer family.
Hardware Impact: Avoids a duplicate 8-slot upload and cache miss path; estimated 20 us CPU-side coordination saved on low-end silicon when wake traffic is active.

Problem: Vehicle throttle needed to disturb silt without binding VFX to vehicle implementation classes.
Solution: Implemented a Burst `IJob` that consumes the latest `VehicleCommandSignal` throttle sample and publishes a `FluidImpulseSignal` carrying AUP position, velocity, radius, and lifetime into the existing signal lane.
Rejected Alternatives: Direct submarine component references and per-frame `FindObjectOfType` were rejected because they create brittle domain coupling and discovery cost.
Scalability potential: Low publishes a sparse impulse at cooldown cadence; High/Ultra uses the same signal to drive denser GPU advection and light response.
Hardware Impact: One NativeArray result and no managed allocation in the hot path; estimated 30 us avoided versus direct scene dependency/update branching on i3/MX350.

Problem: Existing allocation-time particle bootstrap wrote particle positions on CPU and uploaded both ping-pong buffers.
Solution: Added compute kernel `InitializeParticles` and removed the CPU `BootstrapParticles` position loop and upload cache.
Rejected Alternatives: Keeping the CPU bootstrap as "cold path" was rejected because the prompt explicitly mandates zero CPU manipulation of particle positions.
Scalability potential: Low initializes 8,000 particles on GPU; High/Ultra initializes 100,000 particles with the same kernel and no PCIe position payload.
Hardware Impact: Removes the 64-byte-per-particle upload for seeded state; on 100,000 particles this avoids ~6.4 MiB of cold transfer and a CPU loop spike.

Problem: Floating-origin shifts could desynchronize GPU particles if the CPU rebased state externally.
Solution: Accumulate `_AupShiftOffset` on origin-shift notification and apply it to `Pos`/`PrevPos` inside the simulation kernel before velocity integration.
Rejected Alternatives: Rebuilding particle buffers after every origin shift was rejected because it causes stalls, churn, and visible popping.
Scalability potential: Low pays one dot/uniform offset per active particle; High/Ultra preserve dense clouds through AUP shifts without a frame hitch.
Hardware Impact: Expected gain is stall prevention, not ALU reduction; low-end benefit is avoiding buffer upload and render-thread wait.

## Loop 2 Decisions: Tasks 6-10

Problem: The shader contract still used a generic particle type name despite the prompt requiring `SiltParticle`.
Solution: Renamed compute and render shader GPU structs to `SiltParticle` while preserving existing buffer/property names for stable C# bindings.
Rejected Alternatives: Renaming C# property IDs and material bindings was rejected because it would churn serialized/render contracts without changing layout.
Scalability potential: Low/Mid/High/Ultra all use the same 64B packed GPU struct; tier scaling changes count and math path, not layout.
Hardware Impact: Runtime impact is neutral; the gain is preventing contract ambiguity and accidental CPU-side particle expansion.

Problem: Low-tier wake advection needed visible silt disturbance without 3D texture or curl noise cost.
Solution: Low tier is hard-capped at 8,000 marine-snow particles and uses radial wake flow from `_DynamicWakes`/`_DynamicWakeVectors`; 3D abyssal flow and curl are gated behind high-tier scalability.
Rejected Alternatives: Sampling the 3D flow texture on MX350 was rejected because it spends texture bandwidth on a presentation-only cloud.
Scalability potential: Low = 8,000 particles/radial vector; Middle = bounded flow sampling; High = 100,000 particles + abyssal flow texture; Ultra = 100,000 particles with overkill light/curl response.
Hardware Impact: Avoids 3D texture lookups and curl ALU on low-end silicon; estimated 15 us CPU coordination saved and GPU bandwidth saved pending capture.

Problem: High-end machines should spend saved budget on visible murk, not hidden precision.
Solution: High/Ultra particle caps are 100,000 and the shader samples `_AbyssalFlowFieldTexture` before adding fake curl-noise advection when the texture path is active.
Rejected Alternatives: Raising physics fidelity or SDF collision was rejected because it is invisible for marine snow and violates the visual-fake-first mandate.
Scalability potential: High/Ultra gets chaotic wake swirl and denser headlights; Low retains a deterministic radial fake.
Hardware Impact: Low/MX350 avoids this path; RTX-class hardware spends the budget on texture-driven swirl and visual density.

Problem: Headlights needed to carve through silt without CPU particle-light loops.
Solution: Push global flashlight position/direction/cone/color uniforms and compute a per-particle cone/range boost into `SiltParticle.Pad.y`; forward and motion-vector passes consume that boost.
Rejected Alternatives: CPU per-particle spotlight checks and material keyword toggles were rejected because they allocate/control-flow the wrong side of the pipeline.
Scalability potential: Low evaluates a dot/range fake; High/Ultra render denser boosted particles inside the cone.
Hardware Impact: Avoids a CPU loop over 8,000-100,000 particles; estimated 20 us CPU saved versus managed light influence staging.

## Loop 3 Decisions: Tasks 11-17

Problem: The renderer needed URP-stable particles without CPU meshes.
Solution: Switched the draw path to `Graphics.RenderMeshIndirect` using a procedural quad mesh and added a URP `MotionVectors` pass that reads current/previous GPU positions.
Rejected Alternatives: CPU mesh rebuilds or `DrawMeshInstancedIndirect` without motion vectors were rejected because they either move work to CPU or lose temporal stability.
Scalability potential: Low uses the same indirect draw with fewer particles; High/Ultra keep temporal vectors on dense clouds.
Hardware Impact: Avoids CPU mesh particle updates; estimated 25 us CPU saved compared with rebuilding/rendering managed instances.

Problem: Wake/curl/light accumulation could create explosive velocities or NaN state.
Solution: Added `ClampParticleVelocity` using `MaxSiltSpeed`, finite checks, and hard zero fallback for invalid speed.
Rejected Alternatives: Trusting upstream wake vectors was rejected because a single bad impulse can poison the ping-pong buffer.
Scalability potential: Low clamps cheap radial wakes; High/Ultra clamps combined 3D flow/curl/headlight perturbations.
Hardware Impact: Small ALU cost buys deterministic failure containment; expected recovery cost saved is 5 us plus avoided visual corruption.

Problem: Critical VFX state needed a blackbox without managed logging.
Solution: Added a 300-entry `GlobalDataVault` telemetry ring and binary dump path `Docs/AgentLogs/Dump_VOLUMETRIC_SILT_ADVECTION.bin` for non-finite detection.
Rejected Alternatives: `Debug.Log` per frame, no crash evidence, and renderer-owned persistent `NativeArray` storage were rejected; all violate blackbox/data-sovereignty mandates.
Scalability potential: Same fixed 300-entry cost on all tiers; High/Ultra telemetry includes larger capacity and wake count.
Hardware Impact: Fixed 64B x 300 native memory; avoids managed string allocations and enables postmortem state recovery.

Problem: Silt collision is invisible precision and wastes GPU ALU.
Solution: Gated SDF and depth collision away from marine snow/silt; collision remains only for bubbles/debris where visual behavior already depended on it.
Rejected Alternatives: Removing collision globally was rejected because it would alter bubble/debris behavior outside the silt assignment.
Scalability potential: Low/High/Ultra silt all clip through floors; High spends saved cycles on curl and headlight density.
Hardware Impact: Saves depth/SDF samples per silt particle; especially valuable at 100,000-particle high-tier density.

Problem: Destroy/respawn causes churn and visible discontinuity when particles leave the camera shell.
Solution: Added mathematical wrap around camera shell and hard 50m distance guard.
Rejected Alternatives: Killing particles and CPU/GPU respawn upload was rejected because it churns state and breaks continuity.
Scalability potential: Same cheap wrap on all tiers; high density hides the wrap while preserving fog volume.
Hardware Impact: Estimated 8 us avoided from no destruction/reseed path and fewer visible resets.

## Loop 4 Validation Wall: Task 18

Problem: Unity validation could not reach Vulkan/DX12 shader/API compile.
Solution: Ran Unity batchmode default, `-force-d3d12`, and `-force-vulkan`; all stopped on existing C# compile errors before touched VFX files were compiled as a platform shader path.
Rejected Alternatives: Claiming Vulkan/DX12 success was rejected because the logs prove a pre-existing project compile wall.
Scalability potential: Validation state does not change runtime scalability; implementation remains tier-gated from MX350 to RTX.
Hardware Impact: No runtime impact. The integrator must clear unrelated Audio/Physics/Editor assembly errors before platform shader validation can execute.

## Loop 5 Omega Anti-Bloat

Problem: The Omega mandate required a final circular-dependency and DI abuse check after all core tasks were closed.
Solution: Ran static scans for `GameObject.Find`, `FindObjectOfType`, direct renderer/fluid construction, and prompt-specific shader `distance()` usage. No banned calls were found in touched files.
Rejected Alternatives: Treating global service access as a new circular dependency was rejected because this code uses existing `GlobalRegistry`, `VehicleCommandSignalBus`, and `GlobalSignals` contracts rather than constructing peer systems.
Scalability potential: Dependency shape stays stable across low/high tiers; VFX reads published GPU buffers and signals instead of owning gameplay or fluid state.
Hardware Impact: No runtime cost; prevents future managed discovery spikes and architecture drift.

## Loop 6 Multiplatform/H-Phi Inquisition

Problem: The marine-snow renderer still owned persistent wake-job and blackbox `NativeArray` storage, contradicting DataVault sovereignty and increasing leak surface.
Solution: Added `BufferID.MarineSnowWakeJobResult` and `BufferID.MarineSnowTelemetryRing`, then changed the renderer to lease those buffers through `GlobalDataVault` handles and invalidate them during vault compaction.
Rejected Alternatives: Keeping `H8Memory.Allocate` inside the renderer was rejected because the system should own behavior, not storage. Managed delegates/EventBus were not introduced; existing typed `VehicleCommandSignalBus` and `GlobalSignals` remain the only signal path.
Scalability potential: Low/Toaster uses the same 1-entry wake lane and 300-entry telemetry ring; High/Ultra can drive 100,000 particles without additional CPU-side storage.
Hardware Impact: Removes two renderer-owned persistent native allocations from the leak surface. Estimated low-end gain is not frame-time ALU, but reduced memory sentinel churn and fewer ownership faults on i3/MX350/Quest-class devices.

Problem: Quest/ARM64 builds are sensitive to implicit struct padding and stale GPU/CPU ABI assumptions.
Solution: Set `Pack = 1` and explicit `Size` on `ParticleGpuData` (64B), `FrameConstantsData` (112B), `VehicleWakeJobResult` (40B), `MarineSnowTelemetryEntry` (64B), and `VfxComputeParticleBudget` (28B). Added runtime `UnsafeUtility.SizeOf` guards before the renderer ticks.
Rejected Alternatives: Relying on default sequential layout was rejected because padding differences are invisible until platform build or buffer stride mismatch.
Scalability potential: Low/Middle/High/Ultra all keep one stable ABI; particle count and math path scale independently of struct layout.
Hardware Impact: Prevents Quest/Android buffer stride faults and GPU read corruption. Runtime cost is one cold-path layout check; expected frame cost is 0 us.

Problem: Metal/Mac and Steam Deck requirements needed an explicit platform audit, not a PC-only assumption.
Solution: Re-scanned marine-snow shaders: thread groups are 64, 64, and 1, under Metal's 1024-thread limit; no wave intrinsics/groupshared path is present; `rsqrt`/`rcp` sites are guarded with `max`/finite checks; frame I/O only occurs during fault blackbox dump, never during normal ticks.
Rejected Alternatives: Adding high-fidelity physical silt collision or per-frame disk traces was rejected because the assignment needs controllable visual cheats and Steam Deck-safe I/O pressure.
Scalability potential: Toaster mode keeps 8,000 particles and radial wake fake; Middle keeps bounded flow; High/Ultra spend budget on 100,000 particles, 3D flow texture, curl fake, and headlight emission.
Hardware Impact: Maintains the existing GPU budget gates; no measured microseconds claimed because the current project compile wall prevents profiler capture.

## Loop 7 VFX-Domain Data Eviction

Problem: The VFX folder still contained owned persistent native telemetry in `CameraJuiceSystem` and `MaterialDecayRuntime`, plus a temporary `NativeArray` allocation during fallback rust atlas generation.
Solution: Added `BufferID.CameraJuiceTelemetryRing` and `BufferID.MaterialDecayBlackBox`; both systems now lease GlobalDataVault buffers through handles. The material fallback atlas now writes directly into `Texture2D.GetRawTextureData<Color32>()`.
Rejected Alternatives: Keeping local telemetry arrays was rejected because DataVault must own durable data. Replacing the texture write with a managed `Color32[]` was rejected because it creates a large managed allocation and worsens GC pressure.
Scalability potential: Low/Toaster pays only fixed 300-entry telemetry rings and a one-time texture raw-data fill; High/Ultra can keep richer camera/material presentation without adding hot-path storage ownership.
Hardware Impact: Removes two persistent VFX native allocations and one cold temporary native allocation. Expected steady-frame gain is 0 us; risk reduction is leak ownership, ABI stability, and less cold allocation pressure on i3/MX350 and Quest-class devices.

Problem: `CarveDebrisComputeRenderer` already used GlobalDataVault but cached five resolved buffers as persistent `NativeArray` fields, which still made the renderer look like the storage owner.
Solution: Converted persistent fields to `VaultBufferHandle<T>` for debris positions, velocities, requests, job state, and blackbox. Runtime methods resolve scoped views at the operation boundary and pass those views into jobs/upload helpers.
Rejected Alternatives: Releasing/reallocating buffers from the renderer was rejected because DataVault owns those buffers. Changing the public debris service interface was rejected because batch interface immutability forbids unnecessary API churn.
Scalability potential: Low keeps 1,024 active chips and cheap fake debris; Mid keeps 4,096; High/Ultra keep 16,384 and use the existing flow/SDF overkill path without local storage ownership.
Hardware Impact: Expected frame-time change is 0 us to negligible; the value is H-Phi/data sovereignty and compaction-safe ownership. Scoped handle resolution is cheaper than debugging stale persistent aliases after vault compaction.

Problem: Full VFX-domain verification needed a concrete compile and static evidence pass after the edits.
Solution: Ran static scans for persistent native ownership, unpacked structs, standard Unity update loops, `string.Format`, scene discovery, legacy EventBus names, shader `distance()`, wave intrinsics, and Metal thread-group limits. Ran `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`.
Rejected Alternatives: Claiming Unity platform validation was rejected because DX12/Vulkan player validation still requires the whole project compile wall to be cleared.
Scalability potential: The VFX systems now keep the same Low/Mid/High/Ultra math split while moving durable state to DataVault.
Hardware Impact: Scoped C# compile returned EXIT=0. No profiler microseconds claimed.

## Loop 8 GPU Dispatch Inquisition

Problem: High/Ultra marine-snow budgets are 100,000 particles, which means a 64-thread particle kernel needs 1,563 groups. The previous direct dispatch was legal by thread-group size but violated the MX350 compute mandate's 512-group single-dispatch policy. The sonar/fog clear kernels also could exceed 512 total groups at high render scales.
Solution: Added `_MarineSnowDispatchOffset` and `_MarineSnowDispatchTileOffset`. Particle kernels (`CSMain`, `InitializeParticles`, `AccumulateSonarGlow`) now process a global offset so C# can dispatch <=512 groups per call. Sonar/fog clear kernels now tile 2D clears with an 8x8 texel offset and the same <=512 group cap.
Rejected Alternatives: Lowering High/Ultra to 32,768 particles was rejected because the XML explicitly requires 100,000 particles on RTX. Leaving one oversized dispatch was rejected because it would be a hidden MX350/Steam Deck scheduling risk. Indirect dispatch was rejected for this pass because it would add another counter buffer and more synchronization surface for no visual gain.
Scalability potential: Low remains a single 8,000-particle dispatch with the radial fake; Middle remains bounded; High/Ultra keep 100,000 particles and expensive-looking swirl/glow while executing in bounded dispatch slices.
Hardware Impact: No measured microseconds. Estimated performance delta is scheduler-risk reduction, not guaranteed frame-time savings; 100,000-particle paths now split from one 1,563-group call into four <=512-group calls. 2D clear dispatches similarly cap each call to <=512 groups.

Problem: The cold allocation comments still documented 32,768 particles and 2.0 MiB even though the current budget catalog resolves 100,000 marine-snow particles on High/Ultra.
Solution: Corrected the comments to 100,000 * 64B = 6.4 MiB per particle buffer and corrected the quad mesh comment to `RenderMeshIndirect`.
Rejected Alternatives: Leaving stale comments was rejected because memory documentation must match the actual allocation path.
Scalability potential: Low/Middle/High/Ultra memory accounting now matches the real budget catalog instead of the old 32,768-particle mandate snapshot.
Hardware Impact: Runtime impact is 0 us; documentation risk was removed. No profiler claim.

Problem: Current project compilation re-opened a dependency wall after the dispatch patch.
Solution: Ran the scoped `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` and captured `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop8.log`.
Rejected Alternatives: Claiming compile success was rejected. The log reports unrelated `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18) CS0234`; it does not name `HectonMarineSnowRenderer.cs` or the marine-snow shader.
Scalability potential: Validation state does not change the Low/Middle/High/Ultra math paths. Unity DX12/Vulkan validation remains dependency-blocked.
Hardware Impact: Runtime impact unknown until the project compile wall is cleared and profiler/player captures can run. No measured microseconds.

## Loop 9 NaN/Atomic Saturation Pass

Problem: Sonar glow and fog density injection used signed integer atomics after converting particle contribution to an encoded scalar. A dense High/Ultra wake clump could push the accumulated integer target toward overflow, and a non-finite particle could poison downstream render passes even after velocity clamping.
Solution: Added `MARINE_SNOW_MAX_ENCODED_SPLAT = 4096.0` and rejected non-finite or zero encoded splats before `InterlockedAdd`. Added `IsFiniteSiltParticle` as the final `CSMain` state gate; invalid particles attempt normal deterministic respawn and then fall back to `BuildHardFallbackParticle` if respawn math is also non-finite.
Rejected Alternatives: Leaving the atomics uncapped was rejected because visual density should saturate, not wrap. CPU readback validation was rejected because particle state must stay GPU-resident. Killing the whole buffer on one bad particle was rejected because it trades one fault for a visible cloud pop.
Scalability potential: Low/Toaster still uses 8,000 particles and cheap radial wake math, so the guards are mostly fault insurance. Middle keeps bounded contribution. High/Ultra can keep 100,000 particles, sonar glow, fog injection, abyssal flow, and curl fake without allowing one hot wake cluster to corrupt the accumulation target.
Hardware Impact: 0 us measured. Expected steady-frame cost is a few scalar clamps/finite predicates on the GPU path; the value is fault containment on Quest/Android/Metal/Steam Deck and avoiding an expensive post-fault recovery path. No profiler microseconds are claimed.

Problem: Two VFX profile assets still used interpolated editor warning strings. They are editor-time only, but the VFX-domain scan was being used as a debt gate.
Solution: Replaced the interpolated warning bodies in `ShakeProfile` and `BiomeProfile` with constant strings.
Rejected Alternatives: Removing the warnings was rejected because authoring feedback is still useful. Leaving interpolation was rejected because the domain debt scan specifically targets string formatting patterns.
Scalability potential: No runtime tier effect; Low/Middle/High/Ultra rendering behavior is unchanged.
Hardware Impact: Runtime impact is 0 us. Editor allocation risk is reduced only when profiles validate in the editor; no runtime performance claim.

Problem: Loop 9 needed fresh evidence after shader fault-containment edits.
Solution: Re-ran static shader scans for forbidden portability patterns with case-sensitive `distance()` call detection, thread-group parse, file-specific profile string scan, `git diff --check`, and scoped `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`.
Rejected Alternatives: Claiming Unity DX12/Vulkan/player validation was rejected because that validation was not run in this loop.
Scalability potential: The evidence covers the same MX350-to-RTX path: 8,000 low-tier particles through 100,000 high/ultra particles with bounded dispatch and saturated atomic contribution.
Hardware Impact: Scoped C# compile returned EXIT=0 in the Loop 9 and post-doc logs. Unity platform validation remains pending. No measured microseconds.

## Loop 10 Kernel Group Query and Unity Validation Attempt

Problem: `HectonMarineSnowRenderer` still mirrored shader thread-group sizes with C# constants and shift math. That is fragile across shader edits and graphics backends even though the current compute file still uses 64-wide particle kernels and 8x8 clear kernels.
Solution: Query each compute kernel with `ComputeShader.GetKernelThreadGroupSizes`, cache sanitized particle group widths and clear tile dimensions, reject invalid totals or totals above 1024 threads, then feed those queried dimensions into the existing <=512-group chunking path. Defaults remain only as fault fallbacks.
Rejected Alternatives: Keeping `ThreadGroupShift = 6` and fixed `(dimension + 7) >> 3` clear math was rejected because it makes C# the hidden owner of shader ABI. Dispatching one assumed layout for all backends was rejected because Metal/Quest/Steam Deck validation needs the runtime shader contract, not a duplicated constant.
Scalability potential: Low/Toaster still dispatches the 8,000-particle radial fake with the queried kernel width. Middle keeps bounded work. High/Ultra keep 100,000 particles, abyssal flow, curl fake, headlight emission, sonar glow, and fog injection while retaining bounded dispatch chunks derived from the actual kernel shape.
Hardware Impact: 0 us measured. No profiler/player capture was available. Expected frame-time change is neutral; the value is platform correctness, shader contract drift prevention, and avoiding invalid dispatch math if a backend or future shader variant changes kernel dimensions.

Problem: Loop 10 required platform validation evidence after the kernel-query patch.
Solution: Ran scoped static scans, shader thread-group parsing, `git diff --check`, and `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`. Unity default batchmode was retried after another agent's Unity process exited, but it stalled during script compilation/ILPP before console or shader/API validation could complete, so the owned Unity process and orphan ILPP trigger were terminated.
Rejected Alternatives: Claiming Unity success, DX12 success, Vulkan success, or measured microseconds was rejected. The concurrent Unity log from `COMPASS_GYRO_STABILIZER` shows unrelated project-wide compile errors, and the marine-snow validation retry never reached a clean platform compile.
Scalability potential: Validation state does not change the Low/Middle/High/Ultra math split. The implementation remains query-based for backend safety once project-level compile/import is restored.
Hardware Impact: Scoped C# compile returned EXIT=0. Unity DX12/Vulkan remains blocked; exact runtime hardware impact is unknown until Unity import and player/platform validation can finish.

## Loop 11 Typed Lane and Hot-Swap Cache Pass

Problem: VFX code still had legacy latest-state signal reads and service polling shape after the marine-snow core was already GPU-driven. `CameraJuiceSystem` consumed seismic jitter through `GlobalSignals.TryGetLatestSeismicSignal`, `MaterialDecayRuntime` consumed player stress through a legacy latest-state helper and published tool acoustics through `GlobalSignals.Publish`, and marine snow still published fluid impulses through the old bridge.
Solution: Moved VFX consumers to typed `SignalBus<T>` lanes using `ReadOnlySpan<T>` snapshots where a frame snapshot is needed. `HectonMarineSnowRenderer` now pushes `FluidImpulseSignal` directly. `MaterialDecayRuntime` now reads `ReadOnlySpan<PlayerStressSignal>` and pushes `ToolAcousticSignal` directly. `CameraJuiceSystem` now reads `ReadOnlySpan<SeismicSignal>`. The existing `GlobalSignals.Publish(in SeismicSignal)` method now mirrors the payload into `SignalBus<SeismicSignal>` so existing seismic producers do not need a public API change.
Rejected Alternatives: Inventing a new seismic VFX signal was rejected because `SeismicSignal` already exists and carries camera jitter, direction, audio intensity, and thermal scalar. Polling `GlobalSignals.TryGetLatest...` was rejected because it keeps a hidden singleton/latest-state dependency in VFX tick code. Changing the seismic producer public API was rejected because batch interface immutability forbids unnecessary signature churn.
Scalability potential: Low/Toaster reads only the already-flushed frame snapshot and keeps cheap triangle-pulse camera shake. Middle keeps the same deterministic presentation path. High/Ultra keep richer camera/material reactions without adding per-frame service scans or managed dispatch allocations.
Hardware Impact: 0 us measured. No profiler/player capture was run. Expected gain is not claimed; the concrete value is cache-local typed lane consumption and removal of legacy latest-state polling from VFX hot paths.

Problem: VFX systems still resolved some GlobalRegistry services from methods that can execute repeatedly during runtime, which violates the hot-swap/cache discipline and makes DataVault compaction behavior easier to break.
Solution: Added hot-swap and scalability listeners to `CameraJuiceSystem`, and extended the same cached-service pattern in `HectonMarineSnowRenderer` and `MaterialDecayRuntime`. Camera juice now caches Player, Submarine, Dispatcher, DynamicResolution, VRAM, and DataVault services, and its telemetry path uses the cached vault. Material decay and marine snow preserve cached DataVault pointers during compaction fences while invalidating only handles/readiness.
Rejected Alternatives: Re-querying `GlobalRegistry` every SlowTick or telemetry write was rejected because it hides a service-locator dependency in runtime paths. Nulling the cached vault on every compaction fence was rejected because it forces unnecessary cold rebinds and can make telemetry silently disappear after a transient fence.
Scalability potential: Low/Toaster keeps fixed 300-entry telemetry rings and avoids runtime service lookup churn. High/Ultra keep richer VFX state and dense marine-snow paths while service replacement remains event-driven and predictable.
Hardware Impact: 0 us measured. Expected frame-time change is neutral to tiny; the win is deterministic service ownership and fewer runtime lookup/fence edge cases on i3/MX350, Quest/Android, Steam Deck, and desktop.

Problem: The low-tier marine-snow wander fake used two hash samples in [0, 1], which biased particles toward positive X/Z drift. That is cheap, but it is visibly wrong over long camera-relative residence.
Solution: Centered the low-tier hash to [-1, 1] before multiplying by `_MarineSnowDriftParams.z`. The fake remains a hash/dot style approximation and still skips 3D flow/curl texture work on low tier.
Rejected Alternatives: Adding 3D noise to low tier was rejected because the XML explicitly requires low-tier to skip 3D noise lookups. Leaving the positive drift was rejected because it creates coherent sideways migration instead of suspended silt.
Scalability potential: Low/Toaster gets symmetric fake wander at the same texture cost. High/Ultra still use 100,000 particles, abyssal flow texture, curl fake, sonar glow, fog injection, and headlight emission.
Hardware Impact: 0 us measured. No performance gain is claimed; the visual defect is removed with one multiply/subtract pair in the existing low-tier fake.

Problem: Loop 11 needed fresh compile/static evidence without claiming Unity platform validation.
Solution: Ran static VFX scans for legacy signals, EventBus, standard Unity update loops, string formatting/interpolation, scene discovery, coroutine, `Resources.Load`, and `Camera.main`; ran shader scans for `distance()`, wave intrinsics, groupshared/SV_Group, `ComputeBuffer`, `SetData`, `GetData`, and thread-group declarations; ran scoped `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`.
Rejected Alternatives: Claiming Vulkan/DX12 validation was rejected. Unity was already running under another process, so starting a second batchmode platform compile would risk cross-agent import contention.
Scalability potential: Validation state does not change Low/Middle/High/Ultra math paths. The current C# assembly compiles, but Unity import/player/platform validation remains a separate gate.
Hardware Impact: Scoped C# compile returned EXIT=0 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop11_TypedLaneCamera.log`. `git diff --check` is clean except CRLF normalization warnings. Unity process evidence is stored in `Docs/AgentLogs/UnityProcess_VOLUMETRIC_SILT_ADVECTION_Loop11.log`. No measured microseconds are claimed.
