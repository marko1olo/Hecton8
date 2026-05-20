# SHINOBU_131 Rationale

Status: STATIC SOURCE VERIFIED / UNITY COMPILE AND RUNTIME PROFILER PROOF PENDING

## Preflight

Problem: Unity built-in `LightProbeGroup` and managed `LightProbes.GetInterpolatedProbe` do not scale for a 100x100x10 km procedural AUP world and cannot be a Burst/Vault-owned hot path.
Solution: Build a presentation-only SH L2 grid with explicit 128-byte DTOs, AUP-relative indexing, Burst jobs, Vault buffer IDs, and double-buffered GPU upload.
Rejected Alternatives: Standard Unity Light Probe Groups and per-object managed SH sampling are rejected because they bind scene bake infrastructure and main-thread managed APIs to a streaming world. Real radiosity/raytraced GI is rejected under the Dear Lie mandate.
Scalability potential: Low uses sparse grid, nearest/L0-L1 collapse, low cadence; Middle uses trilinear L2 at moderate spacing; High increases density and dynamic bounce cadence; Ultra spends saved CPU on richer SH variation and shader-side caustic/silt response.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding managed probe interpolation and LightProbeGroup streaming overhead; exact microseconds pending static integration and Unity profiler proof.

## Decisions

- Decision 00: Create durable status/rationale before source edits.
  Problem: Context can compress and chat memory is not authoritative.
  Solution: Store task matrix and rationale on disk under `Docs/Tasks` and `Docs/AgentLogs`.
  Rejected Alternatives: Chat-only planning rejected because batch protocol requires disk state.
  Scalability potential: No runtime effect; prevents agent drift.
  Hardware Impact: 0 us runtime.

- Decision 01: Use spatial-hash header for `CustomLightProbeDTO` instead of storing `double3` in every probe.
  Problem: The XML requests `double3` at offset 0 plus 27 SH floats in a 128-byte DTO. The math is impossible: 24 + 108 = 132 bytes before any flags or padding.
  Solution: Store `ulong SpatialHash64`, packed grid coordinate, and flags in the 16-byte header, then pack all 27 SH L2 coefficients into seven explicit `float4` lanes from offset 16 to 127. The grid root AUP remains owner-local in `InteriorGITuningDTO.RootAup`.
  Rejected Alternatives: Oversizing the DTO to 144/160 bytes rejected because the assignment demanded 128 bytes and two cache lines. Truncating SH coefficients rejected because L2 output is the actual contract.
  Scalability potential: Low uses the same 128-byte stride but fewer active cells; Ultra keeps full L2 lanes without reallocating.
  Hardware Impact: Two 64-byte cache lines per probe; avoids unaligned ARM64 reads and keeps GPU structured-buffer stride stable.

- Decision 02: Remove fallback private DataVault/native allocations from the probe grid runtime.
  Problem: A local `GlobalDataVault.Create` fallback and private persistent NativeArrays would violate Vault Law and fragment memory ownership.
  Solution: Runtime now resolves every persistent buffer from `GlobalRegistry.DataVault` or the latest existing global vault, then stops ticking if no vault is available. Boot clearing is an explicit Burst job.
  Rejected Alternatives: Silent standalone vault rejected because it creates a parallel owner of lighting truth. `Allocator.Persistent` fields rejected for hot-path data.
  Scalability potential: All probe capacity and cadence can be coordinated by the central memory governor.
  Hardware Impact: Removes duplicate probe arrays and prevents hidden allocation spikes on low-end i3/MX350.

- Decision 03: GPU upload uses `GraphicsBuffer.LockBufferForWrite<CustomLightProbeDTO>` instead of Texture3D staging.
  Problem: Texture staging and managed upload arrays create CPU copy pressure and shader import churn.
  Solution: Double-buffer `GraphicsBuffer`, map with `LockBufferForWrite`, copy via `CustomLightProbeGpuUploadJob`, unlock after the copy job is completed, and bind `_H8CustomLightProbeGrid` through a delayed publication step.
  Rejected Alternatives: `Texture3D.SetPixelData`, per-frame `MaterialPropertyBlock`, global vector arrays, and same-frame upload/bind rejected because they force managed staging, size caps, or graphics synchronization hazards.
  Scalability potential: Low uploads fewer active probe records at slower cadence; Ultra uploads dense L2 records and lets shaders spend the saved CPU budget.
  Hardware Impact: Avoids per-frame managed allocation and reduces upload synchronization risk; exact us pending Frame Debugger/profiler.

- Decision 04: Replace real dynamic radiosity with nearest-8 directional SH injection.
  Problem: Real-time radiosity or raytraced bounce from flora/explosions is incompatible with 60 FPS VR on constrained mobile silicon.
  Solution: `InjectDynamicLightJob` maps light AUP to grid-local space and adds an 8-probe directional SH boost with finite clamps and quality-scaled gain.
  Rejected Alternatives: Ray marching, secondary light baking, and per-object realtime GI rejected as too expensive and non-deterministic.
  Scalability potential: Low reduces L1/L2 weight and bounce gain; Ultra keeps L2 directional terms and richer shader-side response.
  Hardware Impact: O(8 * lights) scalar writes versus O(probes * rays) or scene-graph light evaluation.

- Decision 05: Route biome tint through Core signals and packed tuning, not a World runtime dependency.
  Problem: Task 12 asks for biome/atmosphere tint, but directly referencing `BiomeTransitionManagerRuntime` or `CurrentAtmosphereDTO` from Lighting would break the compile wall.
  Solution: Read `BiomeGradientSignal` from the Core signal contract, resolve a tint from Vault ambient profiles or deterministic hash fallback, pack it into `InteriorGITuningDTO.PackedBiomeTint`, and apply it inside occlusion/propagation jobs.
  Rejected Alternatives: Adding `Hecton8.World` to `Hecton8.Lighting.asmdef` rejected. Calling shader globals back from the probe solver rejected because the owner route would be ambiguous.
  Scalability potential: One packed scalar works across Low/Middle/High/Ultra; designers can override via `ambient_lighting_profiles.csv` without C# recompile.
  Hardware Impact: Adds one RGB multiply per probe after propagation; avoids assembly dependency and managed lookup cost.

- Decision 06: Purge managed Unity SH writes even outside the immediate Lighting folder only where they fed project ambient presentation.
  Problem: `RenderSettings.ambientProbe` writes in celestial/prologue presentation would keep Unity's managed SH route alive after the custom grid was added.
  Solution: Replace those managed SH probe writes with shader scalar/color globals while leaving unrelated domain logic untouched.
  Rejected Alternatives: Leaving a parallel Unity ambient-probe authority rejected because one fact must have one owner and one route.
  Scalability potential: Shader side receives cheap scalar color inputs; the custom grid owns L2 probe detail.
  Hardware Impact: Removes managed SH object writes from presentation paths.

- Decision 07: Patch Lighting shaft DTO `Pack=1` and World using residue discovered in polish.
  Problem: Lighting shaft DTOs used packed sequential layouts and direct `Hecton8.World` AUP types inside the Lighting asmdef boundary.
  Solution: Convert contribution/telemetry DTOs to explicit 64-byte layouts and replace AUP distance with Core `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3` plus local double3 delta.
  Rejected Alternatives: Keeping `Pack=1` rejected due ARM64 unaligned access risk. Adding World asmdef reference rejected by compile-wall mandate.
  Scalability potential: Light shafts remain a shader fake and scale independently from the probe grid.
  Hardware Impact: Prevents unaligned ARM64 DTO reads and removes a sibling assembly compile dependency.

- Decision 08: Do not run `dotnet build` in this pass.
  Problem: The user explicitly ordered not to launch dotnet build until necessary, and the repo is being edited by many agents.
  Solution: Use static gates only: `rg` archaeology, Burst attribute regex, direct-using scan, GC/allocation smell scan, and `git diff --check` on owned files.
  Rejected Alternatives: A broad build under concurrent multi-agent churn rejected because it would violate the user's CPU/build gate and likely surface unrelated compile-wall failures.
  Scalability potential: No runtime effect.
  Hardware Impact: 0 us runtime; avoids developer-machine compile contention.

- Decision 09: Remove the obsolete half-texture upload scratch from the propagation hot path.
  Problem: After switching to direct `GraphicsBuffer<CustomLightProbeDTO>` upload, the old `InteriorGITextureVoxelDTO` scratch still consumed one write per probe per propagation iteration and one Vault buffer.
  Solution: Delete the half-voxel DTO, stop requesting buffer `0x630807`, remove `TextureUpload` from clear/propagation jobs, and prewarm/grow-only the real GPU buffers to `MaxCellCount`.
  Rejected Alternatives: Keeping the scratch for possible debug texture output rejected because it taxes every simulation pass while the direct GPU route already owns the visual truth.
  Scalability potential: Low and Ultra now both avoid an irrelevant packed texture write; GlobalQualityWeight changes active count without reallocating GPU buffers after boot prewarm.
  Hardware Impact: Saves one 8-byte write per active probe per propagation iteration, up to 262,144 bytes per 32^3 pass at four iterations, plus one Vault buffer request.

- Decision 10: Wire `UpdateProbeOcclusionJob` into the real simulation dependency chain.
  Problem: The SDF occlusion job existed but was not scheduled and originally expected a separate float SDF array that the Vault contract did not own.
  Solution: Change the job to consume `InteriorGIOcclusionCellDTO` directly, schedule it after propagation and before telemetry, and make it the single place that applies packed biome tint plus SDF darkening.
  Rejected Alternatives: Leaving occlusion embedded only inside propagation rejected because Task 07 specifically required a Burst occlusion baking job and the dead job would mislead future owners. Adding a new float SDF buffer rejected as duplicate truth.
  Scalability potential: Low runs the same scalar pass over fewer active probes; Ultra keeps full-density SDF/tint correction without a second data source.
  Hardware Impact: Adds one intentional O(active probes) correction pass but removes ambiguity and duplicate SDF storage; avoids a dead job and keeps terrain leak prevention in the dependency graph.

- Decision 11: Make ambient profile ingestion span-first.
  Problem: The CSV parser was allocation-free but indexed `NativeArray<byte>` directly; the assignment explicitly asked for cold slicing via `ReadOnlySpan<byte>`.
  Solution: Keep the Vault-owned byte scratch as the storage source, expose an unsafe NativeArray wrapper, and run the actual tokenization over `ReadOnlySpan<byte>`.
  Rejected Alternatives: `File.ReadAllBytes`, `string.Split`, `TextAsset`, and managed dictionaries rejected because they allocate and create a second data authority.
  Scalability potential: Designers keep editable CSV control while runtime hot paths consume fixed Vault DTO rows across all quality levels.
  Hardware Impact: 0 us gameplay hot path; cold parse remains allocation-free and bounded by `CsvBufferBytes`.

- Decision 12: Add shader-side authority for the custom probe grid.
  Problem: `_H8CustomLightProbeGrid` was uploaded from C# but no HLSL consumer existed, and direct material shaders still sampled Unity ambient SH through `SampleSH`/`SampleSHPixel`.
  Solution: Add `Hecton_CustomLightProbeGrid.hlsl` with a 128-byte `StructuredBuffer` DTO matching `CustomLightProbeDTO`, nearest/trilinear SH evaluation, quality-scaled fallback, and direct shader integrations across UberNoir, terrain, wreck, flora, kelp, coral, fauna, sargassum, tools, debris, item highlight, archive ocean residue, and indirect-lit materials. C# now sends runtime-world probe origin to `_H8InteriorGIProbeOrigin` and keeps AUP residue/root hash in `_H8InteriorGIProbeRootAup`.
  Rejected Alternatives: Leaving Unity `SampleSH`/`SampleSHPixel` as shader ambient was rejected because it keeps Unity's ambient SH as a visual authority. A new Texture3D probe volume was rejected because the DTO GraphicsBuffer already owns the payload.
  Scalability potential: Low and thermal states blend to fallback/nearest reads; middle/high/ultra blend toward 8-probe trilinear and L2 coefficients through the same GlobalQualityWeight curve.
  Hardware Impact: Low tier avoids 8 buffer reads; high tier spends saved CPU on shader-side per-pixel SH detail. Exact GPU cost requires Frame Debugger/GPU profiler proof.

- Decision 13: Complete the editor telemetry graph without touching runtime allocation rules.
  Problem: Task 17 required a real-time zero-GC graph of SH compute time, but the facade only exposed controls/readouts.
  Solution: Add a fixed `float[128]` editor-only graph element that reads Vault telemetry `SolverCompleteMs` and draws through UI Toolkit `Painter2D`.
  Rejected Alternatives: Runtime debug UI, per-frame managed list allocation, or plotting through IMGUI history arrays rejected for hot-path and coupling reasons.
  Scalability potential: No runtime effect; designers can see collapse cadence and solver completion spikes while tuning Low/Middle/High/Ultra curves.
  Hardware Impact: 0 us player hot path; editor-only repaint cost.

- Decision 14: Remove same-frame GPU publication and Tick-path resolution clear stalls.
  Problem: The earlier GPU route scheduled `CustomLightProbeGpuUploadJob` and immediately completed/unlocked/bound the mapped buffer, which violated the "bind subsequent frame" requirement and risked a same-frame graphics fence. Resolution changes also called the boot clear path from `Tick`, turning a quality collapse into a full synchronous probe-grid clear.
  Solution: Add a pending upload state machine: `TryStartGpuUploadIfDirty` maps and schedules one `CustomLightProbeGpuUploadJob`, stores shader constants with the pending buffer index, and returns; `TryPublishCompletedGpuUpload` only completes after `IsCompleted`, unlocks, and binds on a later frame. Add `InteriorGIProbeGridClearJob` and `ScheduleGridClear` so dynamic resolution clears travel through the normal simulation handle instead of `RunBootClearJob`.
  Rejected Alternatives: Completing upload immediately, binding the write buffer in the same frame, keeping `IJobParallelFor` per-element upload copies, or calling the boot clear job from `Tick` rejected because each reintroduces a hot-path fence or avoidable scheduler overhead.
  Scalability potential: Low quality naturally reduces upload cadence and active count while still publishing stable prior-frame lighting; Middle/High/Ultra can upload denser SH records without tearing the buffer currently bound to shaders.
  Hardware Impact: Removes one avoidable same-frame upload fence and one blocking dynamic clear path. Exact microseconds require Unity Profiler/Frame Debugger proof; expected gain is largest on UMA/mobile where CPU-GPU synchronization is most visible.

- Decision 15: Store new tuning before scheduled resolution clear.
  Problem: After deferring grid clears, `Tick` could set `_activeResolution`, schedule `InteriorGIProbeGridClearJob`, and return before `BuildTuning` updated the Vault tuning row. The following upload could therefore publish a cleared buffer with stale resolution/active-count shader constants.
  Solution: Resolve biome tint, cadence, and a fresh `InteriorGITuningDTO` before `ScheduleGridClear`; write it to the Vault tuning handle and prime `_visualUploadAccumulator` to the current cadence floor so the post-clear upload can publish current grid constants.
  Rejected Alternatives: Waiting for the next solver tick rejected because visual shaders would see one-frame-or-longer stale grid dimensions. Forcing a synchronous clear and upload rejected because it would undo Decision 14.
  Scalability potential: Thermal quality drops now atomically move resolution, active count, cadence, and shader constants through the same continuous `GlobalQualityWeight` curve.
  Hardware Impact: Prevents a stale-constant visual glitch without adding managed allocation or a new blocking wait; exact frame cost remains profiler-pending.

- Decision 16: Gate GPU upload behind completed simulation handles.
  Problem: `LateFrameTick` still allowed `TryStartGpuUploadIfDirty` while `_simulationJobActive` was true and the simulation handle was not complete. With two or four propagation iterations, the final write buffer can be `_probeFront`, so a GPU upload from `_probeFront` could race a Burst propagation write.
  Solution: Change the late-frame gate to return immediately while `_simulationJobActive && !_simulationHandle.IsCompleted`; upload can start only when no simulation is active or after the completed handle is reclaimed.
  Rejected Alternatives: Uploading previous-front data during active simulation rejected because NativeArray alias safety depends on the propagation iteration parity. Adding a second CPU staging buffer rejected because it violates the no-extra-persistent-array rule.
  Scalability potential: Low cadence naturally reduces contention; Ultra still publishes dense grids but only after the simulation dependency graph has produced a stable readable front buffer.
  Hardware Impact: Removes a potential data race and safety-handle violation. The cost is at most delaying visual publication by one late-frame tick; no managed allocation or blocking wait is added.

- Decision 17: Remove the cold boot clear fence.
  Problem: `RunBootClearJob` scheduled `InteriorGIClearStateJob` and immediately called `Complete()`. Although cold, it was still a full-grid main-thread wait and contradicted the dependency-chain discipline used elsewhere.
  Solution: Replace it with `ScheduleBootClearJob`. Boot initialization now writes tuning, prewarms GPU buffers, schedules `InteriorGIClearStateJob`, optionally chains `GenerateMockProbeGridJob`, marks `_scheduledBootClear`, and lets `LateFrameTick` reclaim the completed handle. Readback returns false while boot clear is pending.
  Rejected Alternatives: Keeping a cold synchronous clear rejected because it trains future edits to accept fences. Allocating a temporary managed boot staging array rejected by Vault Law and Zero-GC policy.
  Scalability potential: Low devices avoid a first-tick clear spike; Ultra can still boot a full 32^3 mock grid through a scheduled Burst chain before publication.
  Hardware Impact: Removes one full-grid initialization fence from the main thread. Static byte volume avoided as a synchronous wait is two `CustomLightProbeDTO[32768]` grids plus sources/occlusion/telemetry scratch; exact startup-frame impact is profiler-pending.

- Decision 18: Fence editor CSV polling during pending boot clear.
  Problem: After boot clear became asynchronous, `SlowTick` could call `EnsureNativeState` and then continue into CSV polling while the scheduled clear still owned CSV scratch/profile buffers.
  Solution: `SlowTick` now returns while `_scheduledBootClear` or any simulation handle is active, so editor reloads wait for the same owner-local dependency boundary as runtime propagation.
  Rejected Alternatives: Completing boot clear before CSV reload rejected because it would reintroduce the fence. Duplicating CSV scratch for editor reload rejected because the Vault scratch is the single owner.
  Scalability potential: No hot runtime change; editor tuning remains stable across Low/Middle/High/Ultra because profile rows are not reloaded into a buffer being cleared.
  Hardware Impact: Prevents a cold/editor data race with 0 gameplay-frame allocation.
