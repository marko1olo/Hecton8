# Rationale_SHINOBU_232

Status: LOOP 43 TELEMETRY_FLAG_SEMANTICS - STATIC VERIFIED / BUILD GUARD BLOCKED

## Current Supersession Ledger
- Decision 52 supersedes Decision 34 for Burst float mode: caustic pointer kernels are deterministic, not Fast.
- Decision 55 supersedes Decision 33/54 for legacy caustics service accessors: `ICausticsService` is identity-only.
- Decision 56 supersedes Decision 4/33/54 for weather and wave inputs: SHINOBU reads Core `IWeatherService.GetRuntimeSnapshot()` plus optional `ShinobuOceanSurfaceSwell`, not Atmosphere DTO rows.
- Decision 57 supersedes the old weather/wave telemetry flag names: `FlagWeatherSnapshotBound` and `FlagWaveInputBound` are current; no `WeatherVaultBound` or `WaveVaultBound` flag name remains in source.

## Decision 0 - Scope Boundary
Problem: Caustic lighting task spans rendering, voxel SDF visibility, ocean phase, and quality scaling while 20+ agents may edit neighboring systems.
Solution: Keep implementation in first-party rendering domain and use existing registry/vault/shader interfaces when present; otherwise provide owner-local fallback state without hard dependencies on unfinished agents.
Rejected Alternatives: Direct coupling to Celestial/Ocean/Voxel concrete classes because batch protocol forbids invented dependencies.
Scalability potential: Low uses one cheap monochrome Voronoi layer and shallow depth cutoff; Middle adds smoother panning; High adds dual-layer chroma; Ultra spends saved projector passes on richer distortion.
Hardware Impact: MX350/i3 avoids projector geometry re-render and light cookie passes; expected saving is from removed extra passes, not CPU claims until Unity profiling exists.

## Decision 1 - Task Count
Problem: Initial regex undercounted tasks because task lines are text labels, not XML task tags.
Solution: Treat Task 01 through Task 20 in the extracted XML as the authoritative count.
Rejected Alternatives: Counting XML phase tags or self-reflection questions as tasks.
Scalability potential: Correct loop scheduling prevents skipped quality tiers and verification.
Hardware Impact: No runtime impact.

## Decision 2 - Legacy Projector/RT Cut
Problem: Existing AnalyticalCausticsService could allocate a 512 RenderTexture and dispatch a compute caustic map, preserving the expensive projector/cookie era through a texture indirection.
Solution: Bootstrap now prefers AbyssalDeferredCausticsRuntime and the legacy service no-ops RenderTexture creation, compute dispatch, and texture publication. The active path is a fullscreen RenderGraph pass reading camera color and depth handles.
Rejected Alternatives: Keeping the 512 caustic map as an intermediate because it preserves CPU/GPU sync risk and VRAM churn.
Scalability potential: Low skips the old map entirely and culls by depth; Middle keeps one procedural layer; High/Ultra spend the saved pass on second Voronoi and chroma.
Hardware Impact: MX350/i3 avoids one caustic map allocation path and any geometry/projector-style redraw. Exact profiler value pending Unity run; engineering estimate is 220 us/frame risk removed.

## Decision 3 - 64B Vault DTO and Constant Buffer
Problem: Shader globals as many SetGlobalVector/Float calls create API lookup overhead and make ARM64 constant layout fragile.
Solution: CausticsParametersDTO is explicit 64 bytes: ProjectionVectorAndScale at 0, NoiseAnimationSpeed at 16, IntensityAndDepthFalloff at 32, QualityAndColor at 48. Runtime writes Vault NativeArray memory through UnsafeUtility.AsRef and uploads through double-buffered GraphicsBuffer.Target.Constant using LockBufferForWrite and UnsafeUtility.MemCpy.
Rejected Alternatives: Managed properties, MaterialPropertyBlock, or ad hoc Shader.SetGlobalFloat fields because they create copies and dictionary/API churn.
Scalability potential: Same DTO feeds Low through Ultra; only GlobalQualityWeight and max depth alter work.
Hardware Impact: Low-end silicon removes managed parameter churn; expected CPU saving is single-digit microseconds but more importantly avoids GC and layout faults.

## Decision 4 - Celestial/Ocean Decoupling
Problem: Celestial and final ocean contracts may be edited by other agents, but caustics require runnable data now.
Solution: GenerateMockCausticLightingJob provides synthetic sun direction. Superseded by Decision 56 for producer data: CalculateCausticParametersJob now consumes a pre-sanitized Core `WeatherRuntimeSnapshot` Gerstner wave snapshot plus optional `ShinobuOceanSurfaceSwell`, not Atmosphere weather/wave DTO Vault rows.
Rejected Alternatives: Direct reference to unfinished Celestial service or hard failure when ocean buffers are missing.
Scalability potential: Weak devices get deterministic mock/one-layer output; stronger devices consume wave phase and produce richer flow without changing public interfaces.
Hardware Impact: MX350/i3 remains one IJob and one 64B upload; no dependency stall or frame-time spike from service discovery.

## Decision 5 - Deferred Dear Lie Shader
Problem: Physical caustic tracing or projector cookies would duplicate geometry/light work.
Solution: Hecton_DeferredCaustics reconstructs world position from URP depth, projects a sun vector mathematically, and evaluates hash Voronoi directly in the fullscreen pass. PC_Renderer and PC_High_Renderer now contain active HectonDeferredCausticsFeature subassets with stable script/shader .meta GUIDs and matched renderer feature-map entries.
Rejected Alternatives: Light cookie, Unity Projector, or compute-generated caustic atlas because all create extra passes or intermediate memory.
Scalability potential: Low computes one monochrome layer with short depth; Middle blends second layer; High/Ultra adds chromatic offset and longer visible depth.
Hardware Impact: On i3/MX350 the replacement is one fullscreen branchable pass and no projector geometry redraw; estimated saving 180 us/frame versus an object-projection pass.

## Decision 6 - SDF Shadow Without Shadow Maps
Problem: Caustics inside cave roofs break credibility, but shadow maps or mask RTs violate the pass budget.
Solution: Shader samples _HectonCaveVoxelSdfTex and traces four fixed weighted samples toward the sun. GlobalQualityWeight continuously controls how much of the ray sample set matters.
Rejected Alternatives: Rendering a sunlight occlusion texture or waiting for voxel Agent 12 to expose a bespoke caustic interface.
Scalability potential: Low mostly uses first SDF sample; Middle/High/Ultra continuously increase ray confidence via weights, not binary hardware switches.
Hardware Impact: Removes a shadow/mask pass; estimated 90 us/frame avoided on low silicon, ALU cost remains bounded by fixed unrolled samples.

## Decision 7 - Continuous Quality and AUP Wrap
Problem: Caustic noise can either throttle GPUs at depth or tear at AUP sector boundaries if absolute coordinates reach the shader.
Solution: Jobs wrap CameraAUP.LocalOffset by NoiseTileSize before float conversion. GlobalQualityWeight continuously shrinks max depth and weights second/chromatic Voronoi contributions.
Rejected Alternatives: Passing absolute AUP double/large float to GPU or branching on low-end/high-end hardware classes.
Scalability potential: Low = shallow one-layer monochrome; Middle = weighted second layer; High = richer layer; Ultra = chromatic overkill, all from one scalar.
Hardware Impact: Low-end devices skip far-depth ALU by early return and reduced visible depth. Estimated 80 us/frame saved in abyss views, exact profiler pending.

## Decision 8 - Presentation Data Fence
Problem: Visual caustics must remain deterministic enough for repro but must not enter rollback/Merkle state.
Solution: Superseded by Decision 52. The key boundary remains: SHINOBU writes only caustic presentation BufferIDs, with no StateRingBuffer, Merkle, or Lockstep references. The Burst float mode is now `FloatMode.Deterministic` because the extracted Task 14 requires deterministic caustic parameter kernels despite the lane remaining presentation-only.
Rejected Alternatives: Adding visual light phase to netcode state because it would waste rollback bandwidth and cause visible rewinds.
Scalability potential: All tiers keep smooth presentation panning during rollback.
Hardware Impact: Network payload for caustics remains 0 bytes; CPU hash work remains unchanged.

## Decision 9 - Human Control and Forensics
Problem: Artists need live tuning and failures need last-frame evidence without polluting hot render code.
Solution: Runtime writes a 300-entry CausticsTelemetryEntry Vault ring and dumps Dump_SHINOBU_232.bin on nonfinite setup. Editor-only UI Toolkit window writes tuning DTO fields directly. CSV parser uses ReadOnlySpan<byte>, FNV-1a hashes, and no string.Split.
Rejected Alternatives: Managed frame logs, inspector-only serialized settings, or managed CSV string splitting.
Scalability potential: Low through Ultra can tune chroma, scale, flow, and depth without recompilation.
Hardware Impact: Runtime telemetry is one fixed ring write, estimated 2 us/frame. Editor/CSV costs are cold/editor-only and do not affect player frame time.

## Decision 10 - Renderer Asset Hook
Problem: A ScriptableRendererFeature class alone does not execute in URP; it must be present in the renderer data asset and the feature map must match the feature list.
Solution: Added stable .meta GUIDs for the new shader/scripts and installed HectonDeferredCausticsFeature into PC_Renderer.asset and PC_High_Renderer.asset after existing abyssal SSDO. Verified feature counts equal feature-map entries.
Rejected Alternatives: Runtime mutation of renderer data or adding the pass to Mobile/Quest forward renderer assets. Runtime asset mutation is opaque and fragile; Mobile/Quest are not the deferred targets for this pass.
Scalability potential: PC gets one cheap layer at low GlobalQualityWeight; PC_High spends quality weight on second layer/chroma. Mobile/Quest remain untouched until a forward/mobile-specific visual lie is requested.
Hardware Impact: No per-frame CPU cost from asset hookup. Build/profiler verification remains blocked because dotnet processes were already running and CPU sampled 100.0%, 100.0%.

## Decision 11 - Vault Generation Handles Only
Problem: Runtime still held phase views and a private NativeParallelHashMap, which made the Vault ownership story weaker than required by the ultra-polish mandate.
Solution: Remove all private NativeArray/native collection fields from AbyssalDeferredCausticsRuntime. Persist only 16-byte VaultGenerationHandle descriptors, resolve NativeArray views locally per method, and scan the fixed 32-row profile table in the Burst job.
Rejected Alternatives: Keeping a private CSV scratch NativeArray, keeping a private NativeParallelHashMap, or using legacy VaultBufferHandle pointer-bearing descriptors.
Scalability potential: Low and Middle devices avoid extra native allocations/ref-count ambiguity; High/Ultra still get profile-driven scale/depth/chroma through the same fixed table.
Hardware Impact: i3/MX350 avoids local native ownership and hash-map metadata churn. Runtime profile lookup is bounded to 32 rows, which is cheaper and more predictable than maintaining a second lookup structure.

## Decision 12 - Presentation Clock Without Unity Time
Problem: `Time.time` and `Time.frameCount` make the caustic phase hard to reproduce during dispatcher timing changes and rollback-adjacent frame inspection.
Solution: Advance a sanitized presentation clock from `Tick(deltaTime)` with a 0.25s clamp and write a monotonic `_presentationFrameIndex` into telemetry.
Rejected Alternatives: Continuing to pull UnityEngine.Time in the job setup path.
Scalability potential: All quality tiers keep the same clock source; lower tiers only reduce visible depth and ALU weight.
Hardware Impact: No material frame-time gain claimed. The gain is deterministic inspection and no dependency on Unity's global frame clock in the caustic lane.

## Decision 13 - Alias, Burst, and NaN Vaccination
Problem: Burst jobs lacked explicit `[NoAlias]`, synchronous deterministic compile flags, and safe normalization guards.
Solution: Add `[NoAlias]` to every non-overlapping NativeArray job field, set `CompileSynchronously = true`, retain deterministic float mode, replace raw normalization with guarded `SafeNormalize`, and keep scheduling through `job.Run()`.
Rejected Alternatives: Direct `IJob.Execute()` and raw `math.normalize`/HLSL `normalize` because they hide alias and NaN risk.
Scalability potential: The same code path scales continuously; low quality reduces second/chroma weights while high/ultra spend the saved projector pass on richer shader work.
Hardware Impact: Expected small CPU gain from alias proof and no direct GPU cost. Primary impact is preventing NaN propagation into the constant buffer and black-box dump path.

## Decision 14 - DataVault Hot-Swap Ref-Count Closure
Problem: DataVault replacement dropped generation handles without releasing the previous vault's references.
Solution: Release all caustic Vault handles against the previous IDataVault before rebinding to the new vault, and use the same release path during shutdown.
Rejected Alternatives: Relying on process teardown or letting stale handles vanish from C# fields.
Scalability potential: Long sessions and editor hot reloads keep memory ownership stable across all device tiers.
Hardware Impact: Cold path only. Prevents leaked Vault ref-counts and stale generation metadata after service replacement.

## Decision 15 - Editor-Only Offset Audit And Route Card
Problem: Task 04 specifically demanded an `UnsafeUtility.GetFieldOffset` proof, while runtime reflection in the caustic hot path would violate the zero-allocation and compile-wall posture.
Solution: Add `AbyssalCausticsLayoutAudit` under the `Editor` folder only. It validates `CausticsParametersDTO`, tuning, telemetry, and profile offsets with `UnsafeUtility.GetFieldOffset` and keeps the runtime validator reflection-free. Add `ABYSSAL_CAUSTICS_SHINOBU_232.md` as the concise authority route card for Vault IDs, render path, memory ownership, and compile guard.
Rejected Alternatives: Reintroducing `System.Reflection` into runtime code, trusting only `[FieldOffset]` constants, or burying the proof in chat.
Scalability potential: Low through Ultra devices keep the same runtime path; the extra proof exists only for editor validation and CI/manual review.
Hardware Impact: Runtime impact is 0 us. Editor audit prevents ARM64 CBuffer layout regressions before they reach Quest-class hardware.

## Decision 16 - Non-Owning External Generation Handles
Problem: `Tick` still resolved ocean weather, wave, and swell producer lanes through `TryGetBuffer` every frame and fetched camera AUP through the static `PlayerRuntimeContextService` route.
Solution: Cache read-only external `VaultGenerationHandle<T>` descriptors through non-allocating `TryGetGenerationHandle`, resolve them through `TryResolveHandle`, and clear them on DataVault replacement/shutdown without releasing producer-owned buffers. Camera AUP now uses the cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot` route first.
Rejected Alternatives: Calling `GetGenerationHandle` for external lanes, which could allocate/grow another domain's buffer, or keeping per-frame `TryGetBuffer` metadata lookups.
Scalability potential: All quality tiers consume the same input facts; low devices shed a small metadata lookup path while high/ultra preserve full weather/wave coupling.
Hardware Impact: Estimated 1-4 us/frame metadata churn reduction on low-end silicon when weather/wave lanes are present; 0 us claimed when producers are absent.

## Decision 17 - Scoped Build External Dependency Wall
Problem: After the Loop 8 C# boundary patch, a compile probe was warranted once the no-build guard cleared, but the project currently contains unrelated unresolved dependencies outside the SHINOBU_232 rendering lane.
Solution: Ran one scoped `dotnet build Hecton8.Core.csproj --no-restore` only after CPU sampled 12.2%, 24.6% and no dotnet/csc processes were active. The compile stopped on 77 external dependency errors before any AbyssalCaustics-owned file error appeared.
Rejected Alternatives: Editing unrelated Equipment, Logistics, Audio, Content, Fauna, Physics, Construction, Save, and World bridge code just to force the global project through the wall. That would violate domain boundary and multi-agent ownership.
Scalability potential: No rendering algorithm change; the scoped probe protects the caustic lane without inventing cross-domain references.
Hardware Impact: Runtime impact 0 us. Build result is an integration dependency wall, not a frame-time cost.

## Decision 18 - RenderGraph-Only CBuffer Binding And Job Run Repair
Problem: A Loop 9 source audit found two stale direct `job.Execute()` callsites and runtime-level `Shader.SetGlobalConstantBuffer` rebinding in `LateFrameTick`/upload. That contradicted the prior status proof and kept unnecessary global render-state churn outside the RenderGraph command context.
Solution: Replace both direct calls with `job.Run()`. Leave CPU upload as double-buffered `GraphicsBuffer.LockBufferForWrite` + `UnsafeUtility.MemCpy`, store the active buffer, and bind it only from `HectonDeferredCausticsFeature.RecordRenderGraph` through the command buffer. Move gizmo camera fallback fully into editor-only code without `Camera.main` or `transform.position`. Add a cold shader warmup hook in feature `Create()` while playing; Decision 24 supersedes the initial fallback pass poke with curated SVC-only warmup.
Rejected Alternatives: Keeping direct `Execute()` for tiny jobs, rebinding globals every LateFrame, or relying on first gameplay draw for shader compilation. Those choices weaken Burst proof, waste API-state work, and increase first-use hitch risk.
Scalability potential: Low devices avoid redundant render-state binding and retain one-layer caustics; middle/high/ultra keep the same continuous shader quality curve and spend the saved projector pass on richer Voronoi/chroma.
Hardware Impact: Estimated 1-3 us/frame API-state churn reduction from removing runtime global CBuffer rebinds; job.Run repair is proof hygiene; shader warmup reduces hitch risk but has no steady-frame claim.

## Decision 19 - CSV Profile Fields Must Reach Shader Constants
Problem: The cold CSV parser accepted profile `FlowSpeed`, `ChromaticDispersion`, and `SdfShadowStrength`, but the Burst kernel only consumed scale, intensity, and max depth. The previous route also compared FNV profile hashes directly to `WeatherStateDTO.StateMask`, so example names like `Calm` and `Hurricane` could fail to bind.
Solution: Map known profile names to canonical `WeatherState` bits while still computing FNV-1a fallback for unknown future biome/profile names. In `CalculateCausticParametersJob`, match against resolved weather masks, storm intensity, and the producer's forced storm bit. Feed matched profile flow into pan speed, chroma into `NoiseAnimationSpeed.w`, and SDF strength into `IntensityAndDepthFalloff.w`.
Rejected Alternatives: Keeping parser-only fields, adding a managed dictionary, or querying an Atmosphere service in the hot path. Those either lie to artists, allocate/own another lookup surface, or violate cached Vault input routing.
Scalability potential: Low devices can use calmer profiles with reduced flow/depth/chroma; middle/high/ultra can bind storm/hurricane profiles that spend saved projector cost on stronger shimmer and SDF attenuation without changing shader variants.
Hardware Impact: Runtime remains one bounded 32-row scan already present in the job; added scalar selects are estimated 0-2 us/frame. Cold parser mapping is 0 runtime us. The main gain is correctness of human-authored visual profiles, not a frame-time claim.

## Decision 20 - SDF Texture Fetch Budget Must Match Quality Curve
Problem: The shader described continuous quality degradation, but `ResolveSdfCavernOcclusion` still sampled the 3D SDF four times after the first cave lookup even when low quality made later weights zero. The RenderGraph composite also forced `B10G11R11_UFloatPack32`, risking hidden format conversion or alpha/precision mismatch against the active camera color target.
Solution: Add a continuous `sdfSampleBudget` that is zero at and below quality 0.30 and reaches four samples at quality 1.0. Each unrolled SDF ray sample is guarded by its budget weight, so low quality keeps only the first cheap SDF lookup. Remove the forced color format so the destination inherits the active camera color format while still disabling depth, MSAA, and mips.
Rejected Alternatives: Leaving zero-weight SDF samples in place, or forcing a fixed HDR format for every renderer target. Both waste bandwidth or create target-format risk without buying visible quality.
Scalability potential: Low devices get one SDF lookup plus depth/noise early-outs; middle quality admits one or two ray checks; high/ultra admits the full four-sample cave shadow confidence without changing shader variants.
Hardware Impact: Low-quality cave pixels avoid up to 3-4 3D texture fetches per shaded pixel. The color-format change has no fixed microsecond claim but removes an avoidable conversion/compatibility risk.

## Decision 21 - Owner Vault Ready Gate And Reachable Profile Reload
Problem: `Tick` still called `EnsureVaultState()` every frame, which meant five owner Vault lanes were re-resolved before the actual frame resolves even after boot seeding was done. The CSV parser was also callable only from code, leaving Task 17/18 weaker than a real artist-facing hot-reload bridge.
Solution: Add `_vaultStateReady` as a cold-state gate. `EnsureVaultState()` now acquires/seeds the required owner lanes once, then returns by checking pointer-free generation descriptors until a required resolve fails or DataVault replacement/release/shutdown invalidates the gate. Add a static `TryLoadLightingProfilesCsv` bridge, a UI Toolkit reload button, and a baseline `Assets/_Project/Data/Rendering/caustic_lighting_profiles.csv` file with canonical weather rows.
Rejected Alternatives: Continuing to probe all owner lanes every frame, adding private NativeArray mirrors for CSV staging, or leaving profile loading as an unreachable parser-only API. The first wastes metadata work; the second violates Vault ownership; the third fails the human-control requirement.
Scalability potential: Low devices avoid redundant owner-lane metadata churn and can load calmer profiles with reduced depth/chroma/flow. Middle/high/ultra profiles increase flow, chroma, and SDF attenuation without changing shader variants or adding projector/cookie paths.
Hardware Impact: Estimated 2-6 us/frame metadata churn reduction on i3/MX350 class CPU from skipping duplicate owner-lane resolve/acquire checks after boot. CSV reload remains explicit editor/cold file IO with 0 player-frame cost.

## Decision 22 - Global Doctrine Hot-Path Repair
Problem: The Loop 13 read-only audit found a hidden same-frame `Complete()` path through `DispatcherJobFence`, a scheduled one-DTO job/readback pattern, mutable read-looking helpers, and editor readout `.ToString()` churn. It also found that the route card understated the parameter lane as one DTO when the code uses active/pending slots.
Solution: Remove the pending `JobHandle`, `_pendingParameterJob`, `DispatcherJobFence`, and active-job registration from the caustic runtime. Parameter kernels now run synchronously via `job.Run()` and immediately publish the pending DTO slot to active without an async fence. `Tick` no longer performs owner Vault acquire/grow or producer `TryGetGenerationHandle` polling; it fails closed when `_vaultStateReady` is false and only resolves cached external handles read-only. Mutable helpers were renamed away from read-looking `Resolve`/`Read` names. The editor tuner now uses prebuilt bounded depth/quality label caches and updates labels only when quantized values change. Native safety suppression comments now state lane ownership, bounds, and aliasing assumptions.
Rejected Alternatives: Keeping the same-frame schedule/readback because it was Burst-shaped was rejected; the work is one DTO and does not justify a hidden completion fence. Per-frame Vault repair was rejected because DataVault ownership belongs in bootstrap, hot-swap, or explicit editor/cold repair windows. Literal UI Toolkit zero-allocation for arbitrary new label strings was rejected because Label consumes strings; bounded prebuilt caches remove repeated refresh formatting.
Scalability potential: Low devices now avoid job-fence and failure-state Vault polling overhead while still using one-layer/shallow-depth shader math. Middle/high/ultra retain the same visual-overkill shader path and profile-driven chroma/SDF/depth scaling, with no new variants or authority routes.
Hardware Impact: Removes hidden job completion risk and failure-state metadata churn from the frame path. No fake microsecond number is claimed without Unity profiler; static proof shows the caustic lane no longer contains `DispatcherJobFence`, `job.Schedule`, `.Complete(`, or editor readout `.ToString(`.

## Decision 23 - XR Stereo Fullscreen Pass State
Problem: The deferred caustics shader already sampled camera color with `TEXTURE2D_X`, but the fullscreen vertex/fragment path did not carry Unity's stereo eye macros. In single-pass instanced VR, `UNITY_MATRIX_I_VP`, depth sampling, and camera color sampling can resolve the wrong eye when the eye index is not initialized before fragment reconstruction.
Solution: Add Unity stereo instance plumbing to `Hecton_DeferredCaustics.shader`: `UNITY_VERTEX_INPUT_INSTANCE_ID` on `Attributes`, `UNITY_VERTEX_OUTPUT_STEREO` on `Varyings`, `UNITY_SETUP_INSTANCE_ID` and `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` in `Vert`, and `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` at the top of `Frag`.
Rejected Alternatives: Adding shader keywords or an XR-specific second pass was rejected because it increases variant/stutter risk. Leaving the mono path was rejected because this system explicitly targets Quest-class stereo VR and PCVR.
Scalability potential: Low/Mobile VR keeps the same one-layer/shallow-depth math but now samples the correct eye slice. Middle/High/Ultra keep dual-layer/chroma/SDF overkill without adding a new variant or renderer feature.
Hardware Impact: No microsecond saving is claimed. The gain is correctness and hitch discipline: one existing pass remains one existing pass, with no extra RenderGraph resources, no new material, no new C# allocations, and no shader variant expansion.

## Decision 24 - Private RenderGraph Bindings And Curated SVC Warmup
Problem: The caustics RenderGraph pass still depended on URP-owned global texture names for color/depth, and the cold warmup fallback could manually poke a material pass when no curated shader variant collection was assigned. That creates state-collision risk and weakens the shader warmup proof.
Solution: Replace the shader inputs with private `_HectonDeferredCausticsSource` and `_HectonDeferredCausticsDepth` IDs, bound explicitly by the caustics pass. Remove `DeclareDepthTexture`/scene-depth helper usage and sample the bound private depth texture directly. Add `Assets/_Project/Art/Shaders/Variants/HectonDeferredCaustics.shadervariants`, wire it into PC and PC_High renderer assets, and make `WarmupMaterialPass` call only `ShaderVariantCollection.WarmUp()` when the curated collection exists. Track `_activeConstantBufferFrameIndex` next to the active double-buffered CBuffer for frame-audit proof.
Rejected Alternatives: Rebinding `_BlitTexture`/`_CameraDepthTexture`, relying on implicit first-draw shader compilation, adding shader keywords, or touching central boot/shader systems outside the SHINOBU_232 domain. Those choices either collide with URP global state, create hitch risk, expand variants, or violate domain isolation.
Scalability potential: Low devices keep one private fullscreen pass with shallow depth, one noise layer, and SDF fetch collapse. Middle devices admit partial SDF ray confidence and second-layer weighting. High/Ultra devices use the same SVC-warmed pass for chroma, deeper visibility, and full SDF confidence without variant expansion or binary hardware switches.
Hardware Impact: No steady-frame microsecond saving is claimed for the ID rename. The measurable risk removed is render-state aliasing and first-use shader compilation hitch. On weak devices this prevents a cold gameplay stall; on high-end devices it preserves the same pass for visual-overkill caustic math.

## Decision 25 - Owner Published Render Buffer And URP API Repair
Problem: A focused RenderGraph/API audit found two direct compile hazards and two doctrine risks: stale `ScheduleMockLightingJob()` call after the same-frame job fence removal, reversed local URP `RasterCommandBuffer.SetGlobalConstantBuffer` arguments, RenderGraph reading a lifecycle singleton, and first valid late-frame upload being able to allocate the double CBuffer pair.
Solution: Replace the stale tuning refresh call with `RunMockLightingKernel()` so the current synchronous Burst path remains the only one-DTO update route. Bind the CBuffer with local package order `(GraphicsBuffer, int nameID, int offset, int size)`. Publish the active `GraphicsBuffer` and frame index into static render-snapshot fields only after owner upload, while editor tuning/profile bridges continue to require `s_publishedRuntime` set after registry ownership proof. Pre-create the double 64B GraphicsBuffers from `Awake`, `OnEnable`, and `InitializeService`; `LateFrameTick` upload now only checks `HasConstantBuffers()` and cannot allocate.
Rejected Alternatives: Reintroducing scheduled mock jobs, polling `GlobalRegistry.Caustics` in `RecordRenderGraph`, using `s_runtimeInstance` as hot render authority, or keeping `EnsureConstantBuffers()` inside late-frame upload. Those alternatives either break compile, violate cold DI boundaries, or create first-upload allocation stutter.
Scalability potential: Low devices keep the same one-pass shallow/degraded shader route without first-upload allocation risk. Middle/high/ultra keep the same owner-published CBuffer route and spend quality budget on chroma/depth/SDF confidence rather than runtime allocation or render-state recovery.
Hardware Impact: Prevents direct C# compile failures and removes a possible first-upload late-frame `GraphicsBuffer` allocation. No steady-frame microsecond gain is claimed without profiler; the material gain is frame-stutter risk removal and cleaner ownership proof.

## Decision 26 - APV Renderer Asset Debt Is Not Caustics Ownership
Problem: The shader/YAML sidecar audit found stale or missing Adaptive Probe Volume debug/resource GUIDs in `PC_Renderer.asset` and `PC_High_Renderer.asset`. The caustics feature lives in those same renderer data assets, so the risk needed triage to avoid conflating renderer debt with the SHINOBU caustics route.
Solution: Verify the APV GUIDs are confined to renderer/global settings assets and that caustics SVC GUID `232232232ca00147aa7d232232ca0014` resolves correctly in both PC renderer assets. Treat the APV references as renderer/APV debt outside SHINOBU_232 ownership until a Unity import proof or renderer-owner mandate requests that repair.
Rejected Alternatives: Blindly rewriting APV resource GUIDs to package resources from a caustics task. That would touch a debug/probe-volume subsystem outside the caustics route and could create package-version churn for other renderer owners.
Scalability potential: No caustics algorithm change. Low through ultra tiers keep the same caustics route; APV debug resource debt is quarantined for renderer ownership.
Hardware Impact: Runtime impact 0 us. The decision prevents scope creep and avoids changing unrelated renderer resources under CPU/build uncertainty.

## Decision 27 - Hidden One-DTO Job Fence Regression
Problem: A fresh Loop 18 forbidden-pattern audit found that `SchedulePendingCausticsJob()` still scheduled a single 64B DTO job, stored a `JobHandle`, registered it through `H8Memory`, and finalized it through `DispatcherJobFence`. This contradicted the prior same-frame-job removal report and violated the doctrine that tiny jobs and hidden completion paths need profiler proof.
Solution: Remove `_pendingParameterHandle`, `_pendingParameterJobActive`, `SchedulePendingCausticsJob`, `TryFinalizePendingCausticsJob`, `CompletePendingCausticsJobForBarrier`, and every callsite that depended on pending job state. Both caustic kernels now call `RunPendingCausticsKernel(job)`, which executes Burst-compatible `job.Run()` and immediately publishes pending slot 1 into active slot 0.
Rejected Alternatives: Keeping the scheduled job for theoretical parallelism was rejected because the workload is one DTO and the frame immediately needs the result for GPU upload. Reintroducing a dispatcher dependency was rejected because it creates a false async shape without a measurable batch size.
Scalability potential: Low devices avoid scheduler/fence overhead entirely while retaining the single-layer shallow caustic pass. Middle devices keep the same pass with partial SDF confidence. High and ultra devices spend the saved CPU overhead on richer shader-side chroma, depth, and SDF confidence without changing gameplay truth.
Hardware Impact: Removes hidden job scheduling and completion risk. No exact microsecond value is claimed without Unity profiler data; static proof now shows no `.Schedule(`, `.Complete(`, `DispatcherJobFence`, `JobHandle`, or `RegisterActiveJob` tokens in the caustics lane.

## Decision 28 - Compile Wall Statement Must Match Current Assembly Reality
Problem: The mandate asks for a domain runtime assembly compile-wall proof, but the actual caustics files are under the root `Hecton8.Core.asmdef`; there is no `Hecton8.Rendering.Runtime.asmdef` or caustics asmref in the current tree. Claiming direct sibling isolation as if that assembly existed would be a false report.
Solution: Record the exact current state: caustics introduces no new asmdef reference, `Hecton8.Core.asmdef` references contracts and Unity packages, and it does not directly reference gameplay/world runtime asmdefs for this lane. Optional ocean/player inputs are routed via cached Vault generation handles and `IPlayerRuntimeContext`, not per-frame scene searches.
Rejected Alternatives: Creating a new runtime asmdef late in the batch was rejected because it would move ownership boundaries across a large existing core assembly and risks a broad compile wall under active multi-agent work. Falsely reporting standalone assembly isolation was rejected.
Scalability potential: No visual algorithm change. The same continuous `GlobalQualityWeight` path remains intact from weak devices through ultra hardware.
Hardware Impact: Runtime impact 0 us. The value is compile-wall truthfulness and avoiding a risky assembly split while CPU/build guard is blocked.

## Decision 29 - Second-Pass Patch Against Actual Method Names
Problem: After writing the Loop 18 documentation, a second forbidden-pattern sweep still found the current file used `SchedulePendingCausticsKernel`, not the older `SchedulePendingCausticsJob` name. The first removal patch therefore did not cover the live scheduled path.
Solution: Patch the actual current symbols: remove `SchedulePendingCausticsKernel`, `TryFinalizePendingCausticsJob`, `CompletePendingCausticsJobForBarrier`, `_pendingParameterHandle`, `_pendingParameterJobActive`, `.Schedule()`, and `H8Memory.RegisterActiveJob`. Add `RunPendingCausticsKernel` as the single execution route. Remove the direct `HectonPlayerMovement` fallback from camera AUP resolution so the runtime stays on cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`.
Rejected Alternatives: Editing only the docs, adding a compatibility wrapper, or keeping the fallback concrete player movement route. Those would preserve hidden scheduling/coupling under a different method name.
Scalability potential: Low devices avoid one-DTO scheduler/fence overhead and concrete gameplay fallback. Middle through ultra devices keep the same shader-side caustic quality curve without changing authority routes.
Hardware Impact: Static proof now shows zero matches for `JobHandle`, `.Schedule(`, `.Complete(`, `DispatcherJobFence`, `RegisterActiveJob`, `_pendingParameter`, `Hecton8.Gameplay`, or `HectonPlayerMovement` in the caustics runtime. No exact microsecond claim without profiler.

## Decision 30 - Legacy Analytical And Projector Paths Must Be Inert
Problem: After the deferred caustics owner was in place, legacy files still contained structurally live alternatives: `AnalyticalCausticsService` owned native buffers/RenderTexture/compute state, `CausticsProjectorManager` published shader globals from slow tick, material shaders retained `_HectonCausticsMap`, and bootstrap still transferred a serialized `analyticalCausticsCompute` reference. Even if those routes were bypassed, they preserved a second fact owner and a second presentation route.
Solution: Convert `AnalyticalCausticsService` and `CausticsProjectorManager` into inert serialized-reference shims. Remove their private native/GPU ownership, scene/gameplay/physics dependencies, compute dispatch, and shader-global publishing. Remove `_HectonCausticsMap`, `_HectonProjectedCaustics*`, `_HectonCausticsRuntimeParams`, `_HectonCausticsSimulationParams*`, `_UberNoirCaustic*`, and the `H8_UBERNOIR_CAUSTICS_TEXTURED` branch from material shaders; keep old helper function signatures as zero-return stubs so dependent materials compile without reintroducing a route. Remove the matching stale IDs from `H8ShaderIDs`, remove the dead material consolidator keyword assignment, and remove analytical/secondary material caustic feature bits from `HectonUberNoirRuntimeBridge`. Rename the homeostasis kill-switch bit from `SecondaryCaustics` to `CausticsDetail` without changing its numeric bit value. Remove `analyticalCausticsCompute` storage/adoption from `GameBootstrapper` and `BootstrapController`; bootstrap now only attempts `AbyssalDeferredCausticsRuntime`. Update the `ICausticsService` comment so the shared contract no longer describes analytical projection authority.
Rejected Alternatives: Leaving disabled legacy code in place was rejected because dormant fallback systems still invite accidental reactivation through serialized scene state. Deleting the shim files was rejected because serialized scenes/prefabs may still carry those component types; inert shims absorb that data without owning runtime facts.
Scalability potential: Low devices keep one deferred procedural pass without caustic atlas texture fetches or projector slow-tick globals. Middle devices retain weighted procedural material/deferred math. High and ultra devices spend the saved route complexity on chroma, deeper visibility, and SDF confidence in the warmed deferred pass, not on duplicate render paths.
Hardware Impact: Removes a possible RenderTexture/compute/Shader.SetGlobal fallback route, one slow-tick projector publisher, the dormant material-side projected caustics ALU/global path, and a dead local shader variant. No exact microsecond number is claimed without a profiler capture; static proof shows the legacy texture, projected globals, UberNoir caustic globals, keyword, compute adoption, private buffers, direct gameplay/physics lookups, and shader-global publishing tokens are gone from the caustics fallback files. The remaining `AnalyticalCausticsService` identifier is a deliberate serialized shim identity, not runtime authority.

## Decision 31 - Dead Caustic Residue Must Not Publish Or Import
Problem: After the legacy route was made inert, residue still remained in three places: the bootstrap scene serialized the deleted compute GUID, `VisualOmegaSmokeTester` asserted symbols from the old fake-wave/publish-budget path, and `GlobalShaderDispatcher` still computed and published `_H8CausticProjectionMatrix` plus `_H8CausticRuntime` even though active shaders no longer consume those globals.
Solution: Remove the scene compute reference, retarget the smoke-test caustic gates to the deferred runtime/shader, delete the unreferenced `Hecton_CausticsGenerator.compute` asset and meta, remove the unused no-op `AssignComputeShader(ComputeShader)` shim method, rename stale caustic comments away from analytical projection authority, and remove the orphan projection/runtime global path from `GlobalShaderDispatcher`. The dispatcher now keeps `CausticRuntimeSlot` only as an internal tuning slot; it no longer performs per-frame sun lookup, quaternion/matrix construction, four float4 matrix writes, or two dead global binds for caustics.
Rejected Alternatives: Keeping the compute asset as archive was rejected because archived docs already preserve history and the live shader previously appeared in import-error reports. Keeping orphan globals was rejected because dead bindings imply a second presentation route and burn CPU/API work for no active shader. Deleting the shim types was rejected because serialized scenes or prefabs may still need component type absorption.
Scalability potential: Low devices avoid extra dispatcher math/API binding and rely only on the deferred caustics pass with quality-collapsed SDF/noise budgets. Middle tiers keep partial SDF confidence and weighted second layer. High and ultra tiers spend the budget on the warmed deferred shader path rather than a duplicate legacy compute/global route.
Hardware Impact: Expected low-end gain is small but concrete in shape: one per-frame `RenderSettings.sun` read, `Quaternion.LookRotation`, two `Matrix4x4` constructions, four Vault slot writes, and two command-buffer global binds are removed. No exact microsecond number is claimed without Unity profiler; static proof shows the orphan symbols and deleted compute GUID/name are absent from active project assets.

## Decision 32 - Mobile And Quest Renderer Assets Must Carry The Visual Fake
Problem: PC renderer assets were wired for the deferred caustics feature and curated SVC, but `Mobile_Renderer.asset` and `Quest_VR_Renderer.asset` had no `HectonDeferredCausticsFeature` block. XR shader macros and a continuous quality curve do not matter on Quest-class hardware if the renderer data never enqueues the pass.
Solution: Add the existing `HectonDeferredCausticsFeature` to Mobile and Quest after SSDO, reusing the same warmed shader and SVC GUIDs. Update the Unity `m_RendererFeatureMap` as signed little-endian 64-bit fileIDs so the hidden map matches the visible `m_RendererFeatures` list: PC 16/16, PC_High 15/15, Mobile 12/12, Quest 12/12.
Rejected Alternatives: Creating a mobile-only projector, cookie, atlas, or second shader variant was rejected because the existing route already collapses through `GlobalQualityWeight` and uses one fullscreen visual fake. Editing only `m_RendererFeatures` was rejected because Unity also relies on the feature map. Reverting the pre-existing Mobile/Quest UberPost shader GUID difference was rejected because it was present before this caustics patch and is outside SHINOBU ownership.
Scalability potential: Low and Quest use the same pass with shallow depth, one noise layer, and SDF fetch collapse below quality 0.3. Middle devices admit weighted second-layer and partial SDF confidence. High and ultra retain chroma, deeper visibility, and full SDF confidence without adding a hardware-class switch or extra variant family.
Hardware Impact: The edit adds no new C# allocation or CPU simulation route. It activates the already quality-collapsed screen-space fake on the target low/mobile renderer assets; runtime cost still requires Unity/Quest or MX350 frame capture and remains PENDING VERIFICATION.

## Decision 33 - Lorentz Findings: Helper Coupling, Legacy Facade, And BlackBox Order
Problem: The sidecar audit found three doctrine risks: the caustics runtime called `HectonOceanSurfaceMath` directly, the legacy `ICausticsService` compute accessors advertised a live compute-style route from a deferred CBuffer owner, and the telemetry dump exported physical ring order while doing path setup inside the fault method.
Solution: Replace direct helper calls with local caustic-only wave lane sanitization and scalar extraction over the existing Atmosphere DTO rows. Make legacy compute-facing accessors inert (`false`, `null`, `Vector4.zero`) and keep the real render route on the owner-published `TryGetActiveConstantBuffer(out GraphicsBuffer, out uint frameIndex)` snapshot. Seed the Vault telemetry ring to zero once, resolve/create the dump directory in lifecycle/cold setup, and serialize the ring oldest-to-newest from the cursor while recording the live cursor in the binary header.
Rejected Alternatives: Moving Atmosphere math into SHINOBU-owned code by copying the whole helper surface was rejected because caustics needs only one wave lane scalar projection. Leaving `IsComputeActive` tied to the active CBuffer was rejected because it lets legacy callers infer a compute map route that no longer exists. Rewriting a global async crash exporter was rejected in this loop because no first-party exporter route is owned by SHINOBU_232; the current export remains crash/fault-only, documented, and outside the render hot path.
Scalability potential: Low/Quest still use one shallow screen-space fake with locally sanitized wave influence and no compute facade. Middle/high/ultra retain wave-synchronized flow, chroma, depth, and SDF confidence through the same continuous `GlobalQualityWeight` curve without adding a sibling runtime dependency or variant branch.
Hardware Impact: Runtime steady-state impact is neutral to slightly lower: no direct helper call, no legacy active-state publication, no directory setup on fault. The measurable value is route correctness and deterministic postmortem ordering; Unity profiler and crash-export runtime proof remain pending.

## Decision 34 - Presentation Burst Mode And Telemetry Cursor Vaccination
Problem: SHINOBU_232 jobs still used `FloatMode.Deterministic`, which is reserved here for rollback truth, kinematics, or authoritative state integration. The caustics lane is presentation-only. The telemetry writer also normalized the cursor with `math.abs(int)`, which can fail for `int.MinValue` and produce a negative NativeArray index.
Solution: Superseded by Decision 52 for Burst mode: both caustic pointer carriers and entrypoints now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Replace cursor normalization with signed modulo plus negative correction. Remove the remaining `EnsureBlackBoxDumpPathCold()` call from `DumpBlackBox()` so the fault path cannot create directories.
Rejected Alternatives: Keeping the old Loop 34 Fast-mode interpretation was rejected after the primary XML was reread in Decision 52. Keeping `math.abs` was rejected because one corrupted cursor value could turn the BlackBox into the crash source. Calling the path resolver from dump was rejected because lifecycle already owns path setup.
Scalability potential: Low and Quest keep the cheapest one-layer visual fake with Fast Burst scalar prep. Middle/high/ultra spend saved math/compiler headroom on shader-side chroma, deeper depth, and SDF confidence without changing route or DTO layout.
Hardware Impact: Expected gain is compiler headroom rather than a proven microsecond number. The cursor fix prevents a rare but catastrophic bounds fault; the dump path fix removes cold filesystem repair from the fault route. Build and profiler proof remain blocked by project guard.

## Decision 35 - Required Owner Lanes Must Fail Closed Before Optional Inputs
Problem: `Tick` resolved the required owner lanes for tuning, telemetry, telemetry cursor, and profiles, but after a failed resolve it only set `_vaultStateReady = false` and still continued into optional producer input resolution plus parameter kernel setup. That contradicted the route card and could publish a partial 64-byte presentation payload with missing owner forensic/tuning state.
Solution: Add an immediate return after any required owner-lane resolve failure. Optional weather, wave, and swell lanes remain best-effort only after owner lanes are valid. The render frame path now fails closed until cold Vault setup, hot-swap repair, editor tuning, or explicit profile reload restores the required owned lanes.
Rejected Alternatives: Falling back to default tuning/profile rows inside the frame was rejected because it creates shadow state outside the Vault owner route. Re-acquiring owner buffers from `Tick` was rejected because the route card confines allocation/growth repair to lifecycle/cold windows.
Scalability potential: Low devices avoid a partial parameter update and keep the previous valid active CBuffer or no upload. Middle through ultra devices keep the same continuous quality curve once required owner lanes exist; optional producer data can still enrich visuals without changing ownership.
Hardware Impact: No steady-frame microsecond gain is claimed. The gain is correctness: no parameter kernel runs when the BlackBox/tuning/profile owner state is unresolved, so fault evidence and visual payload ownership stay coherent.

## Decision 36 - Sidecar Shader And Fault-Route Hardening
Problem: The sidecar audit found a fullscreen `sqrt(best)` in the Voronoi caustic helper, gameplay-time `ShaderVariantCollection.WarmUp()` inside `HectonDeferredCausticsFeature.Create()`, and uncaught file IO exceptions in `DumpBlackBox()`.
Solution: Keep squared Voronoi distance and remap line intensity from the squared metric. Remove renderer-feature warmup and route the caustics SVC through `00_BOOTSTRAP.unity -> BootstrapController.shaderVariantCollections -> GameBootstrapper.MemoryPreWarm`, where `WarmUp()` is called before scene activation. Wrap the BlackBox file write in IO/permission catches and set `FaultDumpIo` instead of throwing.
Rejected Alternatives: Keeping per-pixel square root was rejected because the line shape does not need true Euclidean distance. Renderer-feature warmup was rejected because `Create()` can execute at runtime and become a first-use hitch. Throwing from the crash dump was rejected because a fault exporter must fail closed.
Scalability potential: Low devices remove one fullscreen sqrt and avoid gameplay-time shader compilation. Middle/high/ultra keep the same visual-overkill path, but the saved ALU remains available for chroma, second layer, and SDF confidence.
Hardware Impact: The fullscreen sqrt removal is a real ALU reduction proportional to visible caustic pixels; no exact microsecond claim without frame capture. Warmup movement removes hitch risk from gameplay render setup. BlackBox IO catch is fault-route safety, not steady-frame performance.

## Decision 37 - SDF And Mobile Capability Boundaries Are Documented, Not Claimed Runtime Proof
Problem: The shader samples `_HectonCaveVoxelSdfTex` and related globals published by `HectonCaveVoxelLightingVolume`, while SHINOBU_232 only binds source/depth/CBuffer in RenderGraph. Mobile/Quest renderer assets are wired, but `SystemInfo.supportsSetConstantBuffer` is still an unproven device capability gate.
Solution: Record the SDF as a documented legacy shader-global bridge owned by `HectonCaveVoxelLightingVolume`; SHINOBU_232 reads it but does not allocate, update, or republish it. Document that unsupported constant-buffer platforms fail closed with no projector/cookie fallback and that Mobile/Quest remain static route proof until device capture exists.
Rejected Alternatives: Inventing a World texture-handle API inside SHINOBU_232 was rejected because it would create a direct cross-domain dependency. Adding a non-CBuffer projector/material fallback was rejected because it revives deleted expensive routes and violates one-owner/one-route doctrine.
Scalability potential: Low devices either run the same quality-collapsed deferred route when CBuffer support exists or skip caustics cleanly if unsupported. Middle/high/ultra retain SDF attenuation through the documented bridge until World exposes a cleaner RenderGraph route.
Hardware Impact: Documentation does not change frame time. It prevents false readiness claims and quarantines the remaining cross-domain shader-global dependency for the World owner/integrator.

## Decision 38 - Loop 26 Static Verification Closure
Problem: Loop 26 changed a shader ALU route, shader warmup ownership, and BlackBox failure handling. Leaving the status as in-progress or the log as "must confirm" would create a false forensic state.
Solution: Close the loop with targeted static scans: live deferred shader has no `sqrt(`, renderer feature has no gameplay-time warmup method/call, bootstrap scene carries the caustics SVC GUID, BlackBox catches IO/permission exceptions and sets `FaultDumpIo`, and the caustics runtime remains free of forbidden scheduled/completed jobs, hot shader globals, projector/cookie/material-property routes, and concrete gameplay dependencies. Defer the compile probe because the CPU guard sampled 100%.
Rejected Alternatives: Running `dotnet build` under a 100% CPU sample was rejected by project rule. Reporting runtime readiness from static scans was rejected because Unity import, Console, Play Mode, Frame Debugger, profiler, and Quest/MX350 device captures are absent.
Scalability potential: Low/Quest keeps the cheaper squared-distance screen-space fake and bootstrap-warmed shader route; middle/high/ultra retain chroma, dual-layer Voronoi, deeper visibility, and SDF confidence through the same `GlobalQualityWeight` curve.
Hardware Impact: Static-only. The removed fullscreen square root is a real ALU reduction shape, but no exact microsecond value is claimed without frame capture. The warmup move removes gameplay hitch risk; BlackBox IO catch affects fault handling only.

## Decision 39 - Bootstrap SVC Handoff Must Precede Static BeginBootstrap Guards
Problem: `BootstrapController` serialized the caustics SVC, but both `BootstrapController.EnsureRuntimeBootstrapOwner()` and `GameBootstrapper.GuardInitialSceneEntry()` can call `EnsureRuntimeInstance()?.BeginBootstrap()` through `RuntimeInitializeOnLoadMethod` routes. If that route starts before `BootstrapController.Awake/Start` delegates, `SetBootstrapShaderVariantCollections` rejects the later handoff because bootstrap is already running.
Solution: Add `BootstrapController.ApplySerializedShaderVariantCollections(GameBootstrapper)` and call it from `GameBootstrapper.EnsureRuntimeInstance(GameObject)` immediately after the runtime bootstrapper exists on the controller owner. `DelegateBoot` uses the same helper before `BeginBootstrap()`. This preserves the SVC warmup route even when static scene-entry guards are the first boot initiator.
Rejected Alternatives: Removing the static boot guards was rejected because they are central bootstrap recovery behavior outside SHINOBU ownership. Relying on scene `Awake` ordering was rejected because the current code already has independent runtime-initialize entry points.
Scalability potential: All tiers retain the same warmed deferred caustics shader path. Low/Quest avoid first-use shader compilation in gameplay; middle/high/ultra retain visual-overkill caustic layers without adding variants or projector fallback.
Hardware Impact: Cold path only. No steady-frame cost. The impact is hitch-risk containment: the curated caustics SVC reaches `MemoryPreWarm` before scene activation instead of depending on component order.

## Decision 40 - Bootstrap Scene Gate Must Be Exact
Problem: `BootstrapController` still used `scene.name.Contains("00_BOOTSTRAP")` in the static after-scene-load guard and component delegation path. That can admit duplicate or test scenes such as `00_BOOTSTRAP_COPY`, which would allow bootstrap/SVC handoff to start from the wrong scene shell.
Solution: Add a local `BootstrapSceneName` constant and `IsBootstrapScene(Scene)` helper in `BootstrapController`, using `string.Equals(scene.name, BootstrapSceneName, System.StringComparison.Ordinal)` after `scene.IsValid()`. Both boot gates now use the exact helper, matching the stricter `GameBootstrapper` scene gate.
Rejected Alternatives: Leaving substring matching was rejected because bootstrap scene identity is cold authority, not fuzzy user-facing search. Calling the private `GameBootstrapper.IsBootstrapScene` helper was rejected because changing its visibility would widen a central bootstrap API for a local shim need.
Scalability potential: All tiers keep the same warmed deferred caustics route; low/mobile and high/ultra differ only through `GlobalQualityWeight`, not through accidental scene-name admission.
Hardware Impact: Cold path only, 0 steady-frame us. The impact is preventing wrong-scene bootstrap and shader warmup authority leakage.

## Decision 41 - Active Runtime Bootstrapper Still Needs Controller SVC Handoff
Problem: `GameBootstrapper.EnsureRuntimeInstance(GameObject owner)` returned `ActiveInstance` before checking the owner for `BootstrapController`. If a runtime bootstrapper already existed but had not started `BeginBootstrap()`, the controller-owned caustics SVC could still miss `MemoryPreWarm`.
Solution: In the owner overload, apply `BootstrapController.ApplySerializedShaderVariantCollections(runtimeBootstrapper)` before returning an existing active instance. `SetBootstrapShaderVariantCollections` remains the final guard and refuses mutation after `_bootstrapRunInProgress` or `_isBootstrapComplete`.
Rejected Alternatives: Scene scanning from the no-owner overload was rejected because it would add a broader central bootstrap search. Moving warmup back into `HectonDeferredCausticsFeature.Create()` was rejected because renderer-feature warmup can execute during gameplay.
Scalability potential: All device tiers keep the same warmed deferred caustics shader path; no low/high branch or fallback route was added.
Hardware Impact: Cold path only, 0 steady-frame us. The value is closing a race in shader prewarm handoff, not frame-time reduction.

## Decision 42 - No-Owner Bootstrap Ensure Path Must Also Carry SVC Handoff
Problem: `BootstrapController.EnsureRuntimeBootstrapOwner()` calls `GameBootstrapper.EnsureRuntimeInstance()` without an owner argument. The no-owner overload still returned an existing `ActiveInstance` before consulting the bootstrap scene controller, so a pre-existing but not-yet-running bootstrapper could still miss the caustics SVC.
Solution: In the no-owner overload, when `ActiveInstance` exists and bootstrap has not started or completed, resolve the bootstrap controller through the existing exact-scene `TryResolveBootstrapControllerOwner` path and apply serialized SVCs before returning. The owner overload now also avoids `TryGetComponent<BootstrapController>` after bootstrap has started or completed.
Rejected Alternatives: Reintroducing renderer-feature warmup was rejected because feature creation can happen during gameplay. Adding a new public bootstrap-controller lookup was rejected because the existing private exact-scene resolver already owns this cold path.
Scalability potential: All tiers retain one curated SVC warmup route and the same `GlobalQualityWeight` shader cost curve; no binary quality switch or alternate fallback route was added.
Hardware Impact: Cold path only, 0 steady-frame us. The gain is first-use shader compilation risk reduction by closing both owner and no-owner bootstrap entry races.

## Decision 43 - Renderer Assets Must Not Carry A Dead SVC Route
Problem: After shader warmup authority moved to `00_BOOTSTRAP -> BootstrapController -> GameBootstrapper.MemoryPreWarm`, `HectonDeferredCausticsFeature` still serialized an unused `warmupVariants` field and all four renderer assets still carried the caustic SVC GUID. That preserved stale evidence of renderer-owned warmup.
Solution: Remove the renderer-feature `ShaderVariantCollection` field and delete the SVC entry from PC, PC_High, Mobile, and Quest renderer assets. Keep the caustic SVC serialized only on `00_BOOTSTRAP.unity`, where the bootstrap owner can warm it before scene activation.
Rejected Alternatives: Keeping renderer assets as dependency anchors was rejected because it contradicts one owner -> one route. Reintroducing renderer-feature `WarmUp()` was rejected because feature creation can execute during gameplay.
Scalability potential: All tiers still use the same warmed shader and continuous `GlobalQualityWeight` curve; low/Quest avoids accidental first-use compile routes, middle/high/ultra keep chroma/depth/SDF work without an extra variant owner.
Hardware Impact: Steady-frame impact is 0 us. The gain is import/authority hygiene and reduced risk of a future runtime warmup regression from a misleading serialized field.

## Decision 44 - Unsupported CBuffer Platforms Must Not Register A Dead Tick Route
Problem: `InitializeService()` registered update, late-frame, and origin-shift hooks before proving both DTO layout and `GraphicsBuffer.Target.Constant` availability. On a platform where `SystemInfo.supportsSetConstantBuffer` is false, the RenderGraph pass would fail closed correctly, but the runtime could still keep a useless update route alive after initialization failure.
Solution: Gate `_isInitialized` on both `CausticsParametersLayoutValidator.Validate()` and `EnsureConstantBuffers()`. Register update, late-frame, and origin-shift hooks only after that proof passes. Gate `Awake`/`OnEnable` CBuffer creation behind the same layout validator so a broken struct layout cannot allocate GPU buffers before failing. Add `FaultConstantBufferUnavailable` so BlackBox state distinguishes a platform/GPU CBuffer capability fault from a struct-layout fault.
Rejected Alternatives: Treating unsupported CBuffer as `FaultLayout` was rejected because layout and platform capability are different failure modes. Keeping hooks registered and relying on the RenderGraph pass to skip was rejected because fail-closed rendering should not leave dead frame work behind.
Scalability potential: Low/mobile devices with CBuffer support keep the same quality-collapsed fullscreen fake. Unsupported devices publish no projector/cookie/material fallback and do not run the caustic tick path. Middle/high/ultra behavior is unchanged.
Hardware Impact: On unsupported or broken CBuffer hardware, this removes repeated no-op service ticks and late-frame checks. On supported hardware, steady-frame cost is unchanged because registration still occurs after successful buffer proof.

## Decision 45 - Burst Jobs Should Not Carry Dead Optional Producer Arrays
Problem: `GenerateMockCausticLightingJob` and `CalculateCausticParametersJob` still declared optional producer `NativeArray` fields that the runtime never assigned. The runtime already sanitizes weather, waves, swell, profile, and tuning into `CausticsInputSnapshotDTO` before invoking the one-DTO kernels.
Solution: Remove the unassigned job array fields and consume `InputSnapshot.Tuning` when the snapshot flag is present, otherwise fall back to default tuning. Also remove the now-stale `Hecton8.Atmosphere` using from `AbyssalCausticsContracts.cs`; this file now only needs Core weather bit definitions.
Rejected Alternatives: Keeping unused `[NoAlias]` fields was rejected because alias proof should describe memory actually passed to Burst. Re-reading producer arrays inside the job was rejected because snapshot capture is the single sanitized handoff and keeps optional producer memory out of the kernel.
Scalability potential: Low through ultra tiers keep the same continuous `GlobalQualityWeight` math. The change reduces job payload and proof surface without changing the visual quality curve.
Hardware Impact: Expected gain is small but real in shape: smaller job struct copy and fewer NativeArray handles in Burst metadata. Exact microseconds are pending profiler proof.

## Decision 46 - Cold Initialization Must Not Be Reachable From Frame Callbacks
Problem: `Tick` and `LateFrameTick` called `InitializeService()` when `_isInitialized` was false. That makes dump path setup, Vault acquisition, hotswap listener registration, CSV scratch setup, and `GraphicsBuffer` creation reachable from frame callbacks if registration state is stale or initialization failed partway.
Solution: Change both callbacks to return immediately unless `_isInitialized` and `_ownsRegistrySlot` are already true. Cold lifecycle routes remain `Awake`, `OnEnable`, and bootstrap-owned `GameBootstrapper.TryEnsureDeferredCausticsRegistered() -> InitializeService()`.
Rejected Alternatives: Retrying cold repair from the frame path was rejected because it violates global authority phase discipline and can hide unbounded IO/GPU allocation inside a tick. Adding a second fallback material/projector route was rejected because unsupported platforms must fail closed.
Scalability potential: Low devices avoid surprise cold repair during frame execution. Middle/high/ultra keep the same warmed fullscreen fake once bootstrap initialization succeeds; visual cost still scales only through `GlobalQualityWeight`.
Hardware Impact: Supported initialized hardware has unchanged steady-frame cost. Broken/stale initialization state now costs one branch and return instead of a possible cold path. Exact microseconds pending Unity profiler proof.

## Decision 47 - External CBuffer Must Be Declared To RenderGraph
Problem: `HectonDeferredCausticsFeature` captured a raw external `GraphicsBuffer` in pass data and bound it inside the raster render function without declaring it to RenderGraph. Textures were declared, but the CBuffer read was invisible to graph resource scheduling.
Solution: Import the active buffer with `renderGraph.ImportBuffer`, store a `BufferHandle` in pass data, declare `builder.UseBuffer(..., AccessFlags.Read)`, and rely on the package-supported `BufferHandle -> GraphicsBuffer` conversion inside the render function before `SetGlobalConstantBuffer`.
Rejected Alternatives: Keeping the raw `GraphicsBuffer` capture was rejected because it undermines RenderGraph resource visibility. Copying parameters into material properties was rejected because it would reintroduce material/global mutation routes and lose the 64-byte CBuffer contract.
Scalability potential: The rendering fake and quality curve are unchanged. Low through ultra tiers now have a graph-visible CBuffer dependency instead of hidden external state.
Hardware Impact: Frame-time delta is expected to be neutral; the value is render scheduling correctness and future RenderGraph Viewer proof. Exact runtime impact pending Unity Frame Debugger/RenderGraph capture.

## Decision 48 - Cold CSV Profile IO Must Fail Closed
Problem: `LoadFileBytesIntoScratch` used `FileStream` in a cold editor/boot bridge but did not catch IO or permission failures. A missing, locked, or inaccessible CSV could throw through profile reload instead of preserving the last valid profile rows.
Solution: Catch `IOException` and `UnauthorizedAccessException` around the file stream and return zero bytes. The caller already treats non-positive byte count as reload failure and leaves current/default profiles in Vault.
Rejected Alternatives: Allocating managed fallback strings or creating the file during reload was rejected because profile reload is a tuning bridge, not a filesystem owner. Swallowing all exceptions was rejected because non-IO programming errors should still surface during development.
Scalability potential: All device tiers keep the same default profile route when CSV reload fails. No quality branch or shader route changes.
Hardware Impact: Steady-frame impact is 0 us; this is a cold/fault bridge hardening.

## Decision 49 - One-DTO Parameter Work Must Not Use The JobSystem
Problem: The caustics lane updated one 64-byte CBuffer DTO through `IJob` structs and `job.Run()`. Even without scheduling, that preserved a tiny JobSystem execution surface for work that is not batched and has no dispatcher-owned completion window.
Solution: Remove `IJob` and `Unity.Jobs` from the caustics lane. Preserve the XML-mandated kernel names as unmanaged pointer carriers. Cold service initialization compiles Burst `FunctionPointer` entrypoints for mock lighting and parameter synthesis; the frame path invokes those pointers and now fails closed if the pointers are not created. Pointers carry explicit lengths and `[NoAlias]` fields for parameter, telemetry, and cursor lanes.
Rejected Alternatives: Keeping `job.Run()` was rejected because it is still a tiny job wrapper without profiler proof. Scheduling the one-DTO update was rejected because it would reintroduce hidden fences. Moving to managed material/global parameter writes was rejected because it breaks the CBuffer route.
Scalability potential: Low devices avoid scheduler/direct-run wrapper debt. Middle through ultra keep the same shader quality curve and spend saved CPU discipline on presentation shader work, not on CPU light simulation.
Hardware Impact: Expected saving is small but structurally valid: no JobSystem wrapper, no job metadata, no future completion fence. Exact microseconds remain pending Unity profiler proof.

## Decision 50 - NoAlias Proof Requires The Burst CompilerServices Namespace
Problem: Loop 35 retained `[NoAlias]` on raw pointer carrier fields after removing `Unity.Jobs`, but the contracts file no longer imported `Unity.Burst.CompilerServices`. In this Unity/Burst codebase, existing alias-proof attributes depend on that namespace; leaving it absent risks a direct compile failure before Burst proof can even run.
Solution: Restore `using Unity.Burst.CompilerServices;` in `AbyssalCausticsContracts.cs` and keep the pointer-carrier alias attributes intact. Re-run static scans for JobSystem removal, DTO property/packing hygiene, forbidden projector/cookie/material/global shader routes, and `git diff --check`.
Rejected Alternatives: Removing `[NoAlias]` was rejected because it weakens the pointer alias contract and hides the exact memory isolation proof needed for the CBuffer/telemetry/cursor lanes. Reverting to `IJob` was rejected because the one-DTO update remains too small for JobSystem overhead and has no dispatcher-owned completion window.
Scalability potential: Low devices keep the no-scheduler, one-DTO pointer kernel. Middle, high, and ultra devices keep the same continuous shader quality curve; this fix changes compile correctness, not visual tier behavior.
Hardware Impact: Steady-frame impact is 0 us. The gain is compile-wall prevention and retention of Burst alias proof for future AVX2/NEON vectorization opportunities where the kernel grows beyond one DTO.

## Decision 51 - Burst Function Pointer ABI Must Not Copy Kernel Carriers By Value
Problem: After the JobSystem removal, the caustics lane compiled Burst `FunctionPointer` delegates that accepted `GenerateMockCausticLightingJob` and `CalculateCausticParametersJob` by value. That preserves a hidden ABI copy of the carrier, and the calculate carrier includes SHINOBU-owned pointers, explicit lengths, a 128-byte `CausticsInputSnapshotDTO`, AUP offset, timing, quality, frame index, and output index. The kernel is one DTO write; copying the carrier through the unmanaged delegate boundary is not justified.
Solution: Change both function-pointer delegate signatures to accept `GenerateMockCausticLightingJob*` / `CalculateCausticParametersJob*`. The runtime invokes the cold-compiled pointers with `&job`; Burst entrypoints null-check the pointer and execute the stack-local carrier through `UnsafeUtility.AsRef<T>(job)`. Decision 52 removed the direct fallback; a missing pointer now records `FaultBurstKernelUnavailable` and suppresses upload.
Rejected Alternatives: Keeping by-value delegates was rejected because it leaves unnecessary carrier copy traffic in the hot call. Returning to `IJob` or `job.Run()` was rejected because this is still one non-batched 64-byte parameter update without a dispatcher-owned completion window. Managed delegates, lambdas, or interface dispatch were rejected because they would add GC or virtual dispatch risk.
Scalability potential: Low devices get the smallest possible CPU presentation kernel call while the shader collapses to the shallow one-layer fake below quality 0.3. Middle, high, and ultra devices keep the same continuous `GlobalQualityWeight` shader curve, spending saved CPU discipline on deeper visibility, chroma, and SDF confidence rather than CPU light simulation.
Hardware Impact: Expected gain is structurally small but precise: the hot compiled route passes one native pointer instead of copying a large carrier by value across the function-pointer ABI. Exact microseconds remain pending Unity profiler proof.

## Decision 52 - Task 14 Requires Deterministic Pointer Kernels And No Direct Fallback
Problem: Re-reading the extracted SHINOBU_232 assignment showed Task 14 explicitly requires deterministic Burst caustic parameter jobs. Loop 24/34 had interpreted the lane as presentation-only and changed the kernels to `FloatMode.Fast`; Loop 37 still allowed a direct C# `job.Execute()` fallback when a cold-compiled function pointer was missing.
Solution: Set both caustic kernel carriers and both Burst function-pointer entrypoints to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Add `FaultBurstKernelUnavailable`; if a pointer is absent, the runtime suppresses the pending GPU upload, records the fault, attempts one BlackBox dump, and returns false.
Rejected Alternatives: Keeping Fast mode was rejected because the primary XML is stricter than the later presentation-only rationale. Direct unmanaged fallback was rejected because it silently exits the compiled pointer route and weakens forensic proof. Restoring `IJob`, `job.Run`, scheduling, or `.Complete()` was rejected because one 64-byte DTO update remains too small for JobSystem overhead and has no dispatcher-owned completion window.
Scalability potential: The shader quality curve is unchanged. Low devices still collapse visible work through shallow depth, one Voronoi layer, and first SDF lookup; middle/high/ultra still spend quality weight on second layer, chroma, deeper visibility, and SDF confidence.
Hardware Impact: Deterministic mode may reduce compiler headroom versus Fast, but that cost is accepted for Task 14 reproducibility. The fail-closed path has 0 steady-frame cost when pointers are created; on pointer failure it avoids publishing unproven C# fallback parameters.

## Decision 53 - XR UV Transform And Quest Depth Route Must Match Active Renderer Wiring
Problem: The deferred caustics shader declared XR fullscreen macros and called `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX`, but then sampled source/depth and reconstructed world position with raw `input.screenUV`. `Quest_VR_Renderer.asset` has `HectonDeferredCausticsFeature` active while `URP_Quest_VR.asset` did not explicitly require a depth texture.
Solution: Transform the fullscreen UV once through `UnityStereoTransformScreenSpaceTex(input.screenUV)` after stereo setup and use that transformed UV for source color, bound depth, and `ComputeWorldSpacePosition`. Set `URP_Quest_VR.asset` `m_RequireDepthTexture` to 1 so the Quest route explicitly supplies the depth texture consumed by the pass.
Rejected Alternatives: Leaving raw screen UV was rejected because single-pass instanced stereo can sample the wrong eye. Disabling Quest caustics was rejected because the renderer feature is already wired and the shader has a continuous low-cost quality collapse. Reintroducing a Quest projector/cookie/atlas fallback was rejected because it violates the visual-fake and no-extra-pass route.
Scalability potential: Quest/low keeps the same one-layer shallow fullscreen fake; middle/high/ultra keep richer chroma/depth/SDF work through `GlobalQualityWeight` without adding a binary hardware switch or XR-specific variant.
Hardware Impact: UV transform is one shader helper call already used by other project shaders; it fixes correctness, not a measurable CPU gain. Enabling depth on Quest is a bandwidth cost already implied by this active depth-reconstructing pass, and remains pending device profiler proof.

## Decision 54 - External Audit Boundaries After Noether/Faraday Findings
Problem: Subagent audits found three remaining boundary risks: World-owned cave SDF globals are consumed by the caustics shader, Atmosphere DTOs are used for weather/wave Vault type resolution, and `ICausticsService` still exposes inert legacy `RenderTexture` accessors. They also found stale non-caustic renderer package GUIDs.
Solution: Patch only SHINOBU-scoped defects immediately: deterministic fail-closed pointer kernels, XR UV transform, and Quest depth texture requirement. Keep the SDF bridge documented as a legacy shader-global input owned by `HectonCaveVoxelLightingVolume` until World exposes a RenderGraph texture or Vault-backed texture descriptor. The weather/wave DTO clause in this Loop 39 decision is superseded by Decision 56: SHINOBU now reads Core `IWeatherService.GetRuntimeSnapshot()` instead of Atmosphere DTO rows. The legacy `ICausticsService` accessor clause is superseded by Decision 55, which removed the stale RenderTexture-shaped properties after consumer scans.
Rejected Alternatives: Removing SDF sampling would regress Task 08. Replacing weather facts with SHINOBU mirror DTOs was rejected because it would create a shadow route. Reserializing non-caustic URP package GUIDs was rejected as a package/import ownership task, not SHINOBU caustic code.
Scalability potential: The current route still scales continuously across low, middle, high, and ultra tiers; the documented SDF bridge only affects optional attenuation confidence, and the pass fails closed if required CBuffer/depth resources are absent.
Hardware Impact: SHINOBU-scoped fixes add no managed allocation and no CPU simulation route. Unresolved external boundary items are static integration risks, not claimed frame-time savings.

## Decision 55 - Remove The Last Legacy Caustics RenderTexture Contract Surface
Problem: `ICausticsService` still exposed `IsComputeActive`, `CausticsMap`, and `CausticsAup`, preserving a RenderTexture-shaped API even though the active route is a CBuffer-backed RenderGraph pass. Source scan found these properties only in the interface and two inert implementers.
Solution: Remove the three properties from `ICausticsService` and delete their inert implementations from `AnalyticalCausticsService` and `AbyssalDeferredCausticsRuntime`. Keep the registry service slot as identity only; the active caustics data route remains `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer(out GraphicsBuffer, out uint frameIndex)`.
Rejected Alternatives: Keeping null/zero properties was rejected because it advertises a second caustics authority. Adding a replacement `RenderTexture` accessor was rejected because it would revive the atlas/projector-era lane. Broad registry rewrites were rejected; this patch only removes proven-unused accessors.
Scalability potential: Low through ultra tiers keep the same continuous shader route and the same fail-closed CBuffer behavior. No quality branch or gameplay truth route changes.
Hardware Impact: Steady-frame impact is 0 us. The value is compile/API hygiene: downstream code cannot accidentally reattach to a caustic texture map route.

## Decision 56 - Weather And Wave Facts Must Use The Core Snapshot Route
Problem: The caustics runtime still depended on Atmosphere DTO names for weather and wave Vault rows, even though global weather truth is exposed through the Core `IWeatherService` snapshot route. That preserved a sibling-domain DTO coupling and forced the caustics frame setup to maintain two optional external generation handles for facts it does not own.
Solution: Cache `IWeatherService` during cold registry/bootstrap and hotswap, then read `WeatherRuntimeSnapshot` through `GetRuntimeSnapshot()` when the service is initialized. Collapse `WeatherIntensity`, `GlobalWindVector`, `StateMask`, and the three `GerstnerWaveComponent` lanes into the existing `CausticsInputSnapshotDTO` before the Burst pointer kernel. Keep `BufferID.ShinobuOceanSurfaceSwell` as the only optional producer Vault lane because it is already a fixed `float4` presentation input.
Rejected Alternatives: Keeping `WeatherStateDTO` and `WaveParametersDTO` was rejected because it made SHINOBU import sibling DTOs for facts already owned by Core weather. Creating SHINOBU mirror DTOs was rejected because it would not resolve existing producer buffers and would create a shadow route. Querying `GlobalRegistry.Weather` inside the kernel was rejected because registry access is cold identity only.
Scalability potential: Low devices still collapse to shallow depth, one Voronoi layer, and first SDF lookup through `GlobalQualityWeight`. Middle, high, and ultra devices keep richer chroma, deeper projection, and SDF confidence while inheriting weather/wave presentation from one Core snapshot route instead of extra producer DTO handles.
Hardware Impact: Frame setup drops two optional external Vault handle resolves and type-hash-coupled DTO reads. It adds one cached-interface snapshot read from the weather owner. Net runtime gain is expected to be small; the primary impact is compile-wall and authority-boundary hygiene.

## Decision 57 - Telemetry Flag Names Must Match Current Authority Route
Problem: Loop 41 correctly moved weather facts to cached Core `IWeatherService`, but the telemetry bit constants still named weather and wave presence as Vault-bound facts. That made the proof artifact contradict the source route.
Solution: Rename `FlagWeatherVaultBound` to `FlagWeatherSnapshotBound` and `FlagWaveVaultBound` to `FlagWaveInputBound`. Preserve bit positions `1u << 10` and `1u << 9`, so telemetry binary shape and DTO size remain unchanged.
Rejected Alternatives: Keeping old names was rejected because proof artifacts must describe authority routes precisely. Adding alias constants was rejected because it would preserve the stale vocabulary in source and invite new callsites to use it.
Scalability potential: Low through ultra quality behavior is unchanged; the same continuous `GlobalQualityWeight` controls depth, noise layer weights, chroma, and SDF confidence. The patch changes forensic semantics only.
Hardware Impact: Steady-frame impact is 0 us. The value is reducing integration ambiguity: telemetry flags now distinguish Core weather snapshot presence from the optional wave/swell input lane without implying SHINOBU owns a weather Vault row.
