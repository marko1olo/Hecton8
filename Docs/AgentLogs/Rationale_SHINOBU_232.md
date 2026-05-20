# Rationale_SHINOBU_232

Status: LOOP 12 VAULT_READY_AND_PROFILE_RELOAD VERIFIED - SCOPED BUILD STILL BLOCKED BY EXTERNAL DEPENDENCY WALL

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
Solution: GenerateMockCausticLightingJob provides synthetic sun direction and CalculateCausticParametersJob opportunistically reads ShinobuOceanWeatherState, ShinobuOceanWaveParameters, and ShinobuOceanSurfaceSwell from the Vault when present.
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
Solution: Burst jobs use FloatMode.Deterministic and write only SHINOBU caustic presentation BufferIDs. Static scan confirms no StateRingBuffer, Merkle, or Lockstep references in the new caustics code.
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
Solution: Replace both direct calls with `job.Run()`. Leave CPU upload as double-buffered `GraphicsBuffer.LockBufferForWrite` + `UnsafeUtility.MemCpy`, store the active buffer, and bind it only from `HectonDeferredCausticsFeature.RecordRenderGraph` through the command buffer. Move gizmo camera fallback fully into editor-only code without `Camera.main` or `transform.position`. Add cold `material.SetPass(0)` warmup in feature `Create()` while playing.
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
