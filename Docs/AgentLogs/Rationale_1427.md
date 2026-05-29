# Rationale 1427

Date: 2026-05-28
Agent: 1427
Domain: ARCHITECTURAL_PURITY_AND_SCALABILITY_ENFORCER

## Decision 00 - Initial State Discipline

Problem: Agent state files were absent, while the active protocol requires disk-backed state before source edits.
Solution: Created fresh status and rationale files before scanning source. Loaded task-relevant mandates for visual fake first, zero GC, registry DI, and execution phases.
Rejected Alternatives: Proceeding from chat memory would violate anti-amnesia and produce unverifiable work.
Scalability potential: Low/Middle/High/Ultra unaffected directly; this preserves auditability of later code changes.
Hardware Impact: 0 us runtime gain; prevents uncontrolled source edits under multi-agent concurrency.

## Decision 01 - Phase 0 Target Selection

Problem: Repository scan found many expensive math and quality-tier sites, but the worktree is heavily modified by other agents and several hot C# systems own gameplay truth.
Solution: Select the clean kelp shaders as the first surgical target. They contain binary `_QUALITY_MX350/_QUALITY_HIGH` variants and sine-driven flora sway while remaining visual-only and already consuming `_H8GlobalQualityWeight`.
Rejected Alternatives: Editing AI/pathfinding/physics broad systems before ownership proof would risk authority corruption; adding a C# quality broadcast route would violate cold DI because the shader scalar already exists.
Scalability potential: Low uses reduced scalar amplitude and cheap arithmetic wave; Middle increases sway and parallax smoothly; High extends rich motion without a keyword branch; Ultra spends saved variant/trig cost on denser visible flora/material richness through the same shader path.
Hardware Impact: Pending static diff; expected GPU instruction/variant reduction on i3/MX350 with no gameplay truth change.

## Decision 02 - Kelp Visual Fake Implementation

Problem: Kelp shaders used `_QUALITY_MX350/_QUALITY_HIGH` keyword branches and `sin` for ambient sway. High quality also re-sampled the mask map after parallax, creating a binary expensive path.
Solution: Removed the shader feature keyword, replaced sine with `HectonKelpDearLieWave` cubic triangle arithmetic, changed MX350 amplitude suppression into a continuous `lerp(0.58h, 1.0h, SmoothRange(GlobalQualityWeight))`, and made parallax offset continuous without the extra mask re-sample.
Rejected Alternatives: Keeping high/low variants would preserve binary quality; a texture LUT would add asset/streaming dependency; CPU-side animation would add a hot C# route and material update risk.
Scalability potential: Low gets small cheap sway and zero parallax offset; Middle gains proportional sway and starts parallax; High gets full amplitude with the same shader path; Ultra can afford denser kelp/material detail because variant and trig waste were removed.
Hardware Impact: Estimated 20-60 us saved in dense kelp views from removed high-path mask resample plus SFU trig removal; measured proof absent until Unity/RenderDoc.

## Decision 03 - Verification Scope

Problem: The prompt demanded proof while forbidding cluster-heavy builds and rejecting JSON reports.
Solution: Added an Editor test that reads shader sources, bans binary quality keywords/trig/reintroduced mask reassign, and sweeps the continuous scalar formula for monotonic finite output. Ran `rg` static gates and `git diff --check`.
Rejected Alternatives: `dotnet build`/Unity import would violate the user's CPU constraint; Burst IL reflection is irrelevant because no Burst job was modified.
Scalability potential: Test prevents regressions back to low/high dichotomy; same source path remains valid from weak devices through ultra hardware.
Hardware Impact: 0 B/frame added; editor-only test cost outside runtime.

## Decision 04 - Retina Runtime Keyword Removal

Problem: Retina distortion used a runtime MX350 shader keyword, which made chromatic/distortion behavior a binary hardware branch.
Solution: Packed a continuous retina quality scalar into the existing globals buffer and multiplied distortion/chroma by smooth scalar windows inside the shader.
Rejected Alternatives: Keeping `_QUALITY_MX350` or creating separate materials would preserve binary variants and keyword churn.
Scalability potential: Low keeps minimal retina instability; Middle restores controlled chroma; High/Ultra reaches full distortion without changing shader variant.
Hardware Impact: 0 B/frame added; avoids global keyword mutation and variant divergence.

## Decision 05 - Scatter Material Path Flattening

Problem: GPU scatter selected high/low material variants and validated `_QUALITY_MX350/_QUALITY_HIGH` keywords at render time.
Solution: Prefer the primary material, keep legacy fallback fields only as null fallback, validate only `HECTON_GPU_INDIRECT`, and keep visual properties on continuous `_cachedQualityWeight01` lerps.
Rejected Alternatives: Renaming serialized fields would break scene/prefab data; preserving quality keyword validation would keep binary scalability.
Scalability potential: Low/Middle/High/Ultra share one material authority while SSS/bloom/caustic/cull distances scale continuously.
Hardware Impact: Estimated 3-12 us/frame avoided in scatter-heavy views from material path simplification and no quality keyword validation; static estimate.

## Decision 06 - Project Shader Math LOD Keyword Purge

Problem: `_MATH_LOD_LOW/_MATH_LOD_HIGH` remained as compile-time shader variants in CoreLit, Terrain, IndirectVegetation, AbyssalVoxelRock, Coral, and ProceduralBio.
Solution: Removed visual math LOD keywords and replaced them with `_HectonMathLodWeight`, `_H8GlobalQualityWeight`, or existing indirect vegetation quality scalar. Cheap paths now use scalar early-out or scalar amplitude, not compile-time branches.
Rejected Alternatives: Removing only C# keyword toggles would leave default shader variants wrong; computing all high and low paths then lerping would waste low-tier GPU time.
Scalability potential: Low uses dominant-axis, matcap, cheap flow/dither/wake suppression; Middle ramps detail; High/Ultra enables richer POM, flow normals, additional lights, sonar grids, and exact normalize weight.
Hardware Impact: Estimated 10-80 us GPU/frame scene-dependent; larger build/import variant reduction from deleted shader keyword variants.

## Decision 07 - Core Phase-Safe Continuous Broadcast

Problem: DistanceMath and GlobalRegistry flushed math precision by global shader keywords even though SystemDispatcher already has a visual sync flush point.
Solution: DistanceMath now pushes `_HectonMathLodWeight`/mode floats only; GlobalRegistry math precision flush writes only `_H8MathLodLowBlend`; SystemDispatcher visual sync remains the only route.
Rejected Alternatives: Direct writes from pressure detection phases would violate phase ownership; keeping keywords would reintroduce binary shader state.
Scalability potential: All devices receive one scalar route; no variant flip while quality changes under thermal pressure.
Hardware Impact: Removes keyword churn; 0 B/frame added.

## Decision 08 - Verification Without Build

Problem: A full build is forbidden and a dotnet process was already active on the host.
Solution: Used targeted source scans, modified-file diff check, and editor source gates. No `dotnet build`, MSBuild, or Unity test runner was launched.
Rejected Alternatives: Running build under active cluster load would violate user constraint and risk blocking other agents.
Scalability potential: Verification now catches reintroduction of project shader quality/math keywords.
Hardware Impact: Avoided heavy CPU compile load; restricted `git diff --check` passed for touched files, global check remains blocked by unrelated existing whitespace in task docs.

## Decision 09 - Unity QualitySettings Preset Removal

Problem: Project scripts still read `QualitySettings.GetQualityLevel()`, creating a binary Unity preset ceiling outside HomeostasisBrain.
Solution: URP shadow budget now resolves solely from `HomeostasisBrain.GlobalQualityWeight` and platform recommendation; flora wake trail resources use quantized continuous global quality; graphics-state collection quality labels no longer block warmup compatibility.
Rejected Alternatives: Mapping Unity quality names to tiers would preserve preset authority and duplicate the homeostasis scalar.
Scalability potential: Low/Middle/High/Ultra resource pressure now follows the same continuous owner instead of a Unity settings index.
Hardware Impact: 0 B/frame added; avoids binary resource refresh and shadow budget jumps under preset changes.

## Decision 10 - Flora Tick Lookup Flattening

Problem: `FloraInteractionManager.Tick()` called `ResolvePlayerState()`, and the player-root-change branch used `TryGetComponent` for movement/tool ownership. The lookup was rare but still lived under a high-frequency phase.
Solution: Player movement/tool references now refresh from `IPlayerRuntimeContext` in the Tick path, while direct scene override fallback resolves through `TryGetComponent` only in cold `OnEnable`/GlobalRegistry hot-swap cache refresh.
Rejected Alternatives: Keeping the identity-change lookup in Tick would violate hot-loop accessor policy; removing direct-scene fallback entirely would break isolated scene validation.
Scalability potential: Low/Middle/High/Ultra keep the same player interaction visuals, but root swaps no longer inject reflection-style component lookup into the frame phase.
Hardware Impact: 0 B/frame added; avoids one-off player-root swap spikes on weak CPUs and keeps steady-state Tick lookup-free.

## Decision 11 - Encounter Candidate Capacity Scalar

Problem: `EncounterDirector` selected 16 or 32 spawn candidates through a hard `SystemInfo` high-tier boolean.
Solution: Candidate capacity now lerps from base to high count by continuous hardware weight multiplied by `HomeostasisBrain.GlobalQualityWeight` and `PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight`.
Rejected Alternatives: Keeping CPU/VRAM threshold branch would preserve binary scalability; always using 32 candidates wastes weak CPU time.
Scalability potential: Low keeps cheap candidate scans, Middle ramps gradually, High/Ultra spend extra checks on better cinematic spawn placement.
Hardware Impact: 0 B/frame added; candidate search cost now degrades continuously under i3/MX350 pressure.

## Decision 12 - Tether Taut-Line Visual Fake Blend

Problem: Tether visual upload collapsed to a straight-line fake through a boolean low-tier path, causing a potential visual step when quality or tension crossed threshold.
Solution: Visual points now blend continuously between straight-line fake and Verlet curve by quality and load weight.
Rejected Alternatives: Keeping the bool fake would save the same work but create pop; computing only full Verlet visual ignores low-tier presentation budget.
Scalability potential: Low leans into cheap taut silhouettes, Middle restores curvature gradually, High/Ultra show full simulated curve.
Hardware Impact: 0 B/frame added; same buffer writes, no allocations, fewer visible discontinuities.

## Decision 13 - Audio Virtualization Continuous Contract

Problem: `IAudioVirtualizationService.SetLowTierVirtualization(bool)` exposed binary quality authority even though the implementation already had a continuous voice-budget path.
Solution: Replaced the public API with `SetVirtualizationQualityWeight(float)` and routed it through `ApplyVirtualVoiceQualityWeight`.
Rejected Alternatives: Keeping the bool wrapper would preserve accidental 0/1 external load shedding.
Scalability potential: Low/Middle/High/Ultra can now request proportional physical voice budgets.
Hardware Impact: 0 B/frame added; no new allocations or lookup routes.

## Decision 14 - Misleading Tier Semantics

Problem: `HighTierSmoothSteering` and `highTierMaskDetail` encoded hardware-tier language in code paths that are actually apex-behavior or scalar mask-detail activity.
Solution: Renamed cognition to `ApexSmoothSteering`; renamed terrain seam mask detail to `maskDetailActive` while preserving scalar `maskDetailWeight` ownership.
Rejected Alternatives: Leaving misleading names makes future agents rebuild binary hardware forks on top of correct scalar code.
Scalability potential: No behavior change; keeps Low/Middle/High/Ultra decisions attached to scalar weights, not names.
Hardware Impact: No direct runtime gain; reduces architectural drift risk.

## Decision 15 - User Quality Preset Decoupling

Problem: Settings UI still exposed a saved quality index that could drive Unity quality presets, bypassing HomeostasisBrain.
Solution: Preserve the UI/storage index for compatibility, map it through smoothstep to a continuous `0..1` user quality preference, and feed HomeostasisBrain. Labels no longer depend on `QualitySettings.names`.
Rejected Alternatives: Calling `QualitySettings.SetQualityLevel` would preserve binary preset authority and runtime resource churn.
Scalability potential: Low/Middle/High/Ultra become scalar points on one curve; hardware ceilings still clamp effective output.
Hardware Impact: 0 B/frame added; avoids Unity preset flips and shader/material refresh side effects.

## Decision 16 - Deployable SDF Drill Visual Cadence

Problem: Drill extraction kept gameplay truth active, but visual SDF carve packets were hidden behind `skipSdfVisualOnLowTier`.
Solution: Replace the bool with `sdfVisualCarveQualityFloor01` and a survival cadence multiplier. Extraction, power, and inventory still run; only visible carve packet cadence stretches smoothly at low quality.
Rejected Alternatives: Removing carve entirely at low tier makes terrain feedback binary; scaling extraction truth would alter gameplay authority.
Scalability potential: Low shows less frequent carve feedback while mining remains stable; Middle tightens cadence; High/Ultra returns to extraction cadence.
Hardware Impact: Visual carve packet traffic can drop to 25% at survival quality with zero new allocations.

## Decision 17 - Serialized Quality Ghost Removal

Problem: WorldProceduralProxy materials still serialized `_QUALITY_MX350` after shader sources no longer declared the keyword.
Solution: Cleared the dead keyword from material YAML and added an editor source gate to prevent reintroduction.
Rejected Alternatives: Relying only on shader source cleanup leaves Unity material variant state dirty.
Scalability potential: All hardware profiles bind the same material contract and vary only scalar shader constants.
Hardware Impact: Reduces import/runtime variant residue; no runtime memory or GC change.

## Decision 18 - Thermal DRS Scalar Continuity

Problem: ThermalDynamicResolutionAdapter consumed continuous quality but re-bucketed it into `HectonQualityTier` for render-scale limits, upscaler eligibility, and visual budget.
Solution: Route min scale, panic scale, policy scale, FSR eligibility, and dear-lie/overkill capacity through continuous quality weights. Tier state remains only for telemetry/legacy interop.
Rejected Alternatives: Keeping tier envelope switches would make render scale and upscaler selection jump at thresholds.
Scalability potential: Low smoothly favors dear-lie reconstruction and aggressive scale limits; Middle interpolates; High/Ultra spends headroom on overkill features and FSR.
Hardware Impact: 0 B/frame added; avoids preset threshold stalls and gives smoother scale migration on i3/MX350.

## Decision 19 - Indirect Vegetation DataVault Lock Flattening

Problem: CPU culling upload and native dirty-page upload could hold multiple DataVault write locks simultaneously.
Solution: Split writes into single-buffer phases. Matrix, instance data, flora ages, and dirty-page buffers are acquired, mutated, and released one at a time under local `try/finally`.
Rejected Alternatives: Keeping nested locks with only cleanup `finally` still leaves a deadlock vector; adding another combined lock API would increase shared dependency surface.
Scalability potential: Low/Middle/High/Ultra keep identical visual output and upload budget behavior while lock ownership remains deterministic.
Hardware Impact: Removes deadlock risk; adds only bounded extra lock transitions on upload/copy lanes, no GC.

## Decision 20 - Prologue Survival Proxy Scalar

Problem: Prologue ocean hydration used `IsLowTier` as the active permission for proxy surface handoff.
Solution: Add `SurvivalProxyPressure01` to the runtime contract, calculate it from continuous global quality and homeostasis pressure with the existing hysteresis frame window, and make the director consume the scalar threshold. The old bool is retained only as a compatibility wrapper because contract interfaces cannot be mutated mid-batch.
Rejected Alternatives: Deleting `IsLowTier` would violate interface immutability; keeping the director on `_runtime.IsLowTier` would preserve an active binary quality route.
Scalability potential: Low/survival pressure authorizes a cheaper proxy handoff; Middle holds high-resolution hydration longer; High/Ultra prefer full ocean readiness unless memory pressure forces survival behavior.
Hardware Impact: 0 B/frame added; no lookup route added; removes active binary prologue policy from the sequence director.

## Decision 21 - BIOS Startup Quality Weight

Problem: Boot policy forced `HectonQualityTier.Low` through `ShouldForceLowTier()` from VRAM or benchmark thresholds.
Solution: HardwareProfiler now produces `ResolveStartupQualityWeight01()` from immutable system score, continuous survival pressure byte, and smooth benchmark-step degradation. GameBootstrapper derives its legacy tier label from that scalar and stores the scalar score in the hardware profile.
Rejected Alternatives: Keeping a hard low-tier boot gate would create a startup cliff before HomeostasisBrain can take ownership.
Scalability potential: Low clamps early budgets smoothly; Middle scales upload/LOD budgets through existing curves; High/Ultra remain reachable by score and benchmark headroom.
Hardware Impact: Boot-only CPU path; no frame GC; avoids cold preset cliff on i3/MX350-class hardware.

## Decision 22 - LOD And Impostor Scalar Thresholds

Problem: Impostor thresholds switched on `LODQualityPreset.Low/High`, while LOD bias exposed a discrete preset as active runtime policy.
Solution: LODSystemManager exposes `QualityWeight01`, resolves LOD bias from a smooth scalar curve, and ImpostorSystem lerps threshold multipliers by that scalar.
Rejected Alternatives: Removing the serialized preset enum would break saved data; keeping the switch would preserve binary visual residency behavior.
Scalability potential: Low engages impostors earlier without a pop; Middle moves through the curve; High/Ultra hold real geometry longer and spend saved cycles on visual density.
Hardware Impact: 0 B/frame added; same fields and no allocations; removes preset branch from threshold calculation.

## Decision 23 - Instance Culling Tier Uniform Removal

Problem: InstanceCulling.compute declared `_HectonQualityTier`, and C# uploaded it, but the kernel never consumed it.
Solution: Removed the shader uniform and C# property ID/upload. Dispatch quality now falls back to 1.0 only when the continuous descriptor scalar is invalid.
Rejected Alternatives: Keeping dead uniform state would maintain binary-tier drift for no runtime effect.
Scalability potential: Low/Middle/High/Ultra cull distance already scales by `GlobalQualityWeight`; no compute variant or tier uniform remains in the active path.
Hardware Impact: Removes one unused compute uniform set per culling dispatch; no gameplay or buffer layout change.

## Decision 24 - Seismic Shader Shake Suppression Naming

Problem: Seismic shader shake pressure logic was named as low-tier disable behavior although the actual request depends on memory profile, math precision, and global quality.
Solution: Rename the state and resolver to shader-shake suppression semantics.
Rejected Alternatives: Leaving low-tier naming would invite future hard hardware-tier branches on top of scalar pressure logic.
Scalability potential: Low suppresses expensive shake when pressure is high; Middle/High/Ultra can restore it through quality and precision headroom.
Hardware Impact: No direct runtime cost change; drift prevention only.

## Decision 25 - Indirect Vegetation LateFrame Lookup Purge

Problem: Scooter headlight darkness culling resolved a missing `PlayerToolManager` from the `LateFrameTick` chain through `BootstrapState.TryGetCurrentPlayerTransform` and `TryGetComponent`.
Solution: Remove the scene fallback from the hot route. `OnEnable` and the GlobalRegistry hot-swap listener cold-cache `IPlayerRuntimeContext`; LateFrame now reads only `_cachedPlayerContext.ToolManager`.
Rejected Alternatives: Keeping a 2-second throttle still violates the hot-loop lookup rule and can spike weak CPUs during player-root churn.
Scalability potential: Low/Middle/High/Ultra keep identical darkness culling visuals; weaker devices avoid cold component search in the visual phase.
Hardware Impact: 0 B/frame added; removes rare but unbounded scene lookup work from LateFrame on i3/MX350-class CPUs.

## Decision 26 - DataVault Helper Failure Release

Problem: Lock acquisition helpers could pass a write lock to callers on success, but validation-failure exits needed explicit proof that acquired locks are released inside helper `finally`.
Solution: Add `lockAcquired`/`success` guards to flora-age, generic vault storage, and telemetry buffer acquisition helpers. Failed post-acquire validation releases inside helper `finally`; successful acquisition transfers one lock to caller code that already releases in a local `try/finally`.
Rejected Alternatives: Returning invalid buffers to callers for cleanup would spread lock ownership and make nested lock proof weaker.
Scalability potential: Low/Middle/High/Ultra preserve identical upload/culling behavior while write ownership stays deterministic.
Hardware Impact: No GC and no extra buffers; removes write-lock leak/deadlock vector with only scalar branch checks on acquisition paths.

## Decision 27 - Migratory Sargassum Job Lock Split

Problem: Migratory sargassum scheduling held island-state and flow-sample DataVault write locks together across a Burst job even though the job only reads flow samples.
Solution: Fill flow samples under one short write lock, release it, pin the flow buffer with owner-tagged `TryLockBuffer`, then acquire the island-state write lock for the job. Release island write ownership and the flow pin in nested `try/finally`.
Rejected Alternatives: Keeping two write locks across job scheduling; copying flow samples into a managed array; adding a new combined lock API.
Scalability potential: Low/Middle/High/Ultra keep identical drift visuals while vault compaction safety no longer depends on nested writer ownership.
Hardware Impact: 0 B/frame; no direct us claim, deadlock surface removed.

## Decision 28 - Field Sampler Read-Only Pinning

Problem: `WorldProceduralFieldSampler.CellSamplingJob` marks six vault arrays as `[ReadOnly]` but acquired six DataVault write locks to keep pointers stable.
Solution: Replace write-lock acquisition with owner-tagged buffer pins, resolve the handles after the pins are active, and unlock in reverse order under `try/finally`.
Rejected Alternatives: Leaving write locks on read-only inputs; using `TryReadOnlyHandle` alone for a cross-job pointer without a compaction pin.
Scalability potential: Weak devices avoid unnecessary writer contention during large sampling jobs; high/ultra paths keep the same data fidelity without write-lock deadlock risk.
Hardware Impact: 0 B/frame; lock contention risk reduced, exact frame-time gain requires profiler.

## Decision 29 - Telemetry Ring/Cursor Lock Flattening

Problem: Nav-grid and vegetation-memory telemetry wrote ring and cursor buffers while holding two DataVault write locks simultaneously.
Solution: Reserve/update the cursor under one write lock, release it, then write the telemetry ring entry under a separate write lock. Telemetry may skip an entry if the second phase fails; this is acceptable because telemetry is diagnostic, not gameplay truth.
Rejected Alternatives: Preserving nested locks for perfect telemetry atomicity; allocating a managed queue to defer telemetry.
Scalability potential: All hardware tiers keep fixed-size black-box telemetry with deterministic lock ownership and no garbage.
Hardware Impact: 0 B/frame; removes a deadlock vector in failure telemetry paths.

## Decision 30 - SurfaceNets CSV Tuning Lock Flattening

Problem: CSV tuning commit wrote tuning and chunk meshing state under nested DataVault write locks.
Solution: Commit sanitized tuning under one write lock, release it, then mark visible chunks dirty under a second write lock. The old path had no rollback, so split phases do not weaken a real transaction contract.
Rejected Alternatives: Holding tuning/state locks together; adding a transaction abstraction to DataVault mid-batch.
Scalability potential: Low/Middle/High/Ultra retain the same meshing tuning behavior while cold CSV override commits cannot deadlock the vault.
Hardware Impact: Cold path only; no runtime allocation and no build-time dependency added.

## Decision 31 - Diagnostic Cursor/Ring Writer Split

Problem: Input, audio, atmosphere, fluid, and equipment diagnostic telemetry used ring+cursor writer locks simultaneously, creating avoidable DataVault writer overlap in fault/black-box paths.
Solution: Reserve/update the cursor under one write lock, release it, then acquire the ring lock and write the entry. Audio encrypted-fragment state now writes hash and recovered bits in separate phases and publishes count only after both phases succeed.
Rejected Alternatives: Keeping perfect diagnostic atomicity under two writer locks; adding managed queues; broad-patching gameplay truth buffers without route-card proof.
Scalability potential: Low/Middle/High/Ultra keep the same fixed-size black-box telemetry. Weak CPUs avoid writer contention amplification; high/ultra keep diagnostic coverage without lock nesting.
Hardware Impact: 0 B/frame; no direct microsecond claim, deadlock surface reduced.

## Decision 32 - Voxel Pathing CSV Import Split

Problem: Editor-only voxel pathing profile import required profile table and profile-count write locks at the same time because the parser wrote both outputs.
Solution: Add a parser overload that writes profile rows and returns `written`; caller releases profile lock before acquiring the count lock.
Rejected Alternatives: Holding both locks in a cold editor import path; allocating a managed temporary profile list.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime. The import path remains zero managed row objects and now has deterministic writer ownership.
Hardware Impact: Cold/editor only; no frame-time claim.

## Decision 33 - High-Risk Multi-Buffer Writer Boundaries

Problem: Sub-agent static audit found real high-risk multi-buffer write-lock overlaps in combat target views, global physics culling, GPR publish, SpatialAudio portal work buffers, cockpit button jobs, visor upload, construction transaction buffers, hazard exposure, and proximity collider jobs.
Solution: Do not perform blind mechanical splits. Those paths hold multiple job output/truth buffers and require owner-specific migration to read pins, SoA staging, or single-owner commit phases.
Rejected Alternatives: A superficial regex refactor would likely corrupt job ownership, transaction semantics, or gameplay truth.
Scalability potential: These remain the next lock-flattening front; the correct solution is per-domain staging, not one global helper.
Hardware Impact: No change yet for those high-risk paths; documented as PENDING DOMAIN MIGRATION, not falsely claimed clean.

## Decision 34 - GPR Pending Publish Single-Writer Commit

Problem: `GroundPenetratingRadarRuntime.TryPublishRadarPendingJob` held seven DataVault writer fences while copying a completed pending-job staging set into public radar buffers.
Solution: Commit each buffer through `TryCopyPendingBufferToVault`, which acquires one writer fence, copies, and releases in `finally`. `GroundRadarCounters` is published last because counter[0] is the visible ping-count marker. Telemetry ring writing now uses a real writer fence instead of pinning the buffer and resolving a mutable owner view.
Rejected Alternatives: Keeping seven writer fences for a copy-only publish path; adding a managed transaction queue; claiming atomicity that the old path did not prove.
Scalability potential: Low/Middle/High/Ultra keep identical radar visuals and GPU upload behavior. Weak devices avoid long writer windows during scan completion; high/ultra keep dense ping output without synchronization overreach.
Hardware Impact: 0 B/frame; no direct microsecond claim without profiler, but worst-case writer-lock hold time is reduced to one buffer copy at a time.

## Decision 35 - Proximity Distance Job Read-Pin Split

Problem: `ProximityColliderSystem` held writer fences for positions, previous status, and results across an async distance job even though positions and previous status are `[ReadOnly]` job inputs.
Solution: Copy managed `_prevStatus` into the vault under one writer fence and release it before scheduling. Pin positions and previous-status with owner-tagged `TryLockBuffer`, resolve the handles for read-only job use, and hold only the job-results writer until `LateFrameTick` consumes the output.
Rejected Alternatives: Keeping three writer fences across the job; copying positions into a new temporary array; moving spawn/despawn work into the job.
Scalability potential: Low devices keep cheap broad-phase collider activation without writer contention amplification; Middle/High/Ultra keep the same pooled collider behavior and can process larger point sets without blocking unrelated vault writers.
Hardware Impact: 0 B/frame; removes two writer fences from the job lifetime and keeps the existing Burst math path intact.

## Decision 36 - AR Stencil Presentation Stack Staging

Problem: `HectonVisorARStencilRendererFeature.BuildAndUploadFrame` held five DataVault writer fences while collecting waypoint sources, projecting targets, building HUD/digit DTOs, uploading GraphicsBuffers, and writing telemetry.
Solution: Stage waypoint sources and projected targets in stack-local spans, build HUD/digit DTOs as value types, upload GPU payload from those local values, and commit vault buffers through one-writer helper phases with strict `try/finally`. Telemetry is written after payload upload under its own writer fence.
Rejected Alternatives: Keeping UI writer locks through projection/GPU upload; allocating managed temporary arrays; claiming multi-buffer atomicity that the old code did not prove.
Scalability potential: Low keeps the cheap AR overlay path with no managed garbage; Middle/High/Ultra retain full 16-target visor density while writer ownership stays deterministic.
Hardware Impact: 0 B/frame; no direct microsecond claim, but the presentation phase no longer serializes five vault buffers at once on i3/MX350-class CPUs.

## Decision 37 - Noir DTO Single-Writer Commit

Problem: `HectonVisorUberPostFeature.Noir.TryUpdateNoirConstants` held input, tuning, and constants writer fences together for three single-element DTO writes.
Solution: Replace the block with `TryWriteNoirDto<T>`, which acquires one GraphicsScalability writer, validates the single row, writes the value, and releases in `finally` before the next DTO commit.
Rejected Alternatives: Preserving the three-lock block; adding a transaction API for single-value presentation DTOs; keeping unsafe pointer writes where the NativeArray indexer is sufficient and zero-GC.
Scalability potential: Low/Middle/High/Ultra keep identical noir grading behavior and continuous quality math; weaker hardware avoids avoidable vault contention during late presentation updates.
Hardware Impact: 0 B/frame; removes two unnecessary simultaneous writer fences from the noir constants update path.

## Decision 38 - Diegetic Visor Local Simulation Snapshot

Problem: `DiegeticVisorLensRuntime.ScheduleSimulation` executed a synchronous visual condensation evaluator while holding write locks for state, tuning, physiology, environment, GPU globals, and NaN flags. `IngestCoreSignals` and telemetry also overlapped writer locks for presentation-only state transfer.
Solution: Add `TryReadVisorValue<T>` and `TryWriteVisorValue<T>`. The simulation now reads immutable value snapshots, executes `VisorCondensationEvaluator` over DTO fields, then commits each output buffer in its own `try/finally` writer phase. Signal ingestion and emergency mock data use the same local staging. Telemetry reserves cursor first, releases it, then writes the ring entry.
Rejected Alternatives: Holding six writer locks to avoid partial visual snapshot updates; adding managed queues; converting visual condensation into a Burst job without profiler proof.
Scalability potential: Low keeps the cheapest visor condensation path with no managed garbage; Middle/High/Ultra keep richer droplets/refraction while the visual phase cannot block unrelated vault writers through multi-lock ownership.
Hardware Impact: 0 B/frame; no profiler-backed microsecond claim, but visual-phase DataVault writer lifetime is reduced from six simultaneous fences to one fence per commit on i3/MX350-class hardware.

## Decision 39 - Thermodynamics Visual Upload Cadence Scalar

Problem: `ThermodynamicsHazardGridRuntime.UploadVisualTextureIfDirty` skipped heat texture uploads only when `_activeResolution == LowResolution`, which tied presentation cadence to a binary resolution state. Health pressure and telemetry symbols also still used low-tier names even though the active policy is continuous quality pressure.
Solution: Add `ResolveVisualUploadStride(float qualityWeight01)` using a smoothstep-shaped `GlobalQualityWeight` result. Visual texture upload cadence now scales from stride 4 at survival pressure to stride 1 at high/ultra quality regardless of the exact active resolution. Rename health pressure counters and telemetry flag constants to survival/quality-pressure semantics while preserving bit positions.
Rejected Alternatives: Keeping the low-resolution branch; adding another hardware tier enum; forcing upload every dirty frame on weak devices; changing telemetry layout.
Scalability potential: Low/survival pressure uploads fewer heat-grid texture revisions while the diffusion truth still runs; Middle tightens cadence smoothly; High/Ultra upload every dirty visual frame for richer heat-haze response.
Hardware Impact: 0 B/frame added. Static estimate only: weak devices avoid up to 75% of unchanged 3D heat texture uploads under pressure; high-end devices retain full visual cadence.

## Decision 40 - Dynamic Resolution Preset Compatibility Scalar

Problem: `DynamicResolutionScaler` retained a `switch (LODQualityPreset)` to select minimum render scale values 0.7/0.8/0.9. The public preset API is legacy-compatible, but the runtime policy should not branch on Low/Medium/High tiers.
Solution: Keep `SetQualityPreset(LODQualityPreset)` intact, convert the enum ordinal into a clamped continuous `0..1` quality weight, then resolve the minimum render-scale floor through `ResolveMinimumRenderScaleFromQualityWeight`. Invalid enum values select the medium scalar through `math.select`.
Rejected Alternatives: Changing the public API mid-batch; preserving the switch; routing this through Unity `QualitySettings`; hardcoding another hardware tier.
Scalability potential: Low preset maps to the survival floor, Middle maps to the center of the same curve, High maps to visual headroom. Future Ultra-style input can move through the scalar path without another switch.
Hardware Impact: 0 B/frame added. No profiler-backed microsecond claim; removes one binary render-scale floor decision from the late-frame presentation policy.

## Decision 41 - LOD Preset Scalar Ordinal Mapping

Problem: `LODSystemManager.ResolvePresetQualityWeight01` still used a `switch` over `LODQualityPreset.Low/Medium/High`, keeping a binary compatibility enum as active runtime policy.
Solution: Convert the enum ordinal into a clamped scalar and resolve the compatibility weight through one continuous polynomial. Invalid enum values fail to the legacy medium scalar via `math.select`.
Rejected Alternatives: Removing the public enum would break save/UI compatibility; keeping the switch would preserve a binary quality fork.
Scalability potential: Weak hardware keeps the survival LOD bias, middle hardware travels through the same scalar curve, and high/ultra gets longer mesh residency without a new branch.
Hardware Impact: 0 B/frame added; one active quality branch removed from the visual sync path.

## Decision 42 - Gas Dynamics Cadence Endpoint Rename

Problem: `GasDynamicsSolver` already used continuous quality to interpolate cadence, but serialized fields still used low/mid/high tier names. That naming invites future binary policy in a survival truth system.
Solution: Rename runtime fields to `survivalColdTickSeconds`, `standardColdTickSeconds`, `overkillColdTickSeconds`, and `survivalHibernationDistanceMeters` with `FormerlySerializedAs` preserving existing Unity serialized data.
Rejected Alternatives: Changing gas hibernation truth based on quality; deleting legacy serialized fallback; leaving low-tier field names in active code.
Scalability potential: Low/Middle/High/Ultra continue to interpolate cadence continuously while gas authority remains correctness-first.
Hardware Impact: 0 B/frame; no behavior change except removing misleading runtime symbols.

## Decision 43 - Flora Genome Continuous Quality Jobs

Problem: Flora genome L-system jobs quantized `GlobalQualityWeight` into `FloraGenomeHardwareTier` and switched iteration/matrix caps on Low/Middle/High/Ultra. This is a direct binary scalability fork inside Burst-side generation.
Solution: Remove `FloraGenomeHardwareTier`, pass continuous `QualityWeight01` into `IterativeLSystemExpanderJob` and `TurtleGraphicsJob`, and resolve iteration/matrix caps with scalar math. `FloraPlantSeedDTO` keeps a one-byte `QualityWeightQ8` at the same offset for compact telemetry/layout continuity.
Rejected Alternatives: Keeping the enum as a compatibility facade; changing DTO size; using managed config objects; running a build to discover syntax mistakes.
Scalability potential: Weak devices clamp toward the 512-matrix survival floor and toaster iteration cap; middle quality rises through the same polynomial; high/ultra reaches 16384 matrix visual overkill without adding branches.
Hardware Impact: 0 B/frame added; generation capacity now degrades smoothly instead of jumping across hardware buckets.

## Decision 44 - Drone Fleet Continuous Endpoint Naming

Problem: Construction drone tuning used Low/Mid/High/Ultra field names inside the active runtime DTO, and drone render/phantom budgets still carried low/high tier constants even though the math already consumed `GlobalQualityWeight`.
Solution: Rename active fields and constants to survival/standard/high-fidelity/overkill semantics while preserving `DroneFleetTuningConstants` explicit offsets and legacy CSV key aliases. Runtime steering, A* budget, phantom count, and render distance still resolve through scalar quality.
Rejected Alternatives: Removing CSV compatibility; changing DTO size; leaving tier names because the math was already scalar.
Scalability potential: Weak devices move toward survival steering and zero phantom draw count; middle hardware follows the same curve; high/ultra spends budget on more phantom swarm density and render distance without new branches.
Hardware Impact: 0 B/frame added; no profiler-backed microsecond claim.

## Decision 45 - Drone Fleet Presentation Lock Flattening

Problem: `RenderRealHeadlessFleet` acquired render-instance and culling-state DataVault write locks together for presentation scratch fill and GPU upload.
Solution: Split the upload into `TryPrepareAndUploadDroneRenderInstances` and `TryPrepareAndUploadDroneCullingStates`. Each helper acquires one writer, fills one DTO array, uploads it, and releases in `finally` before the next writer can be acquired.
Rejected Alternatives: Holding two writer locks for visual scratch; moving scratch to managed arrays; broad-changing the core drone simulation multi-buffer job locks without a route-card migration.
Scalability potential: Low/survival hardware avoids presentation lock contention while scalar budgets reduce visible drone density; high/ultra keeps GPU culling and richer phantom visuals with deterministic writer ownership.
Hardware Impact: 0 B/frame; removes a presentation-phase deadlock vector and shortens writer lifetime on i3/MX350-class CPUs.

## Decision 46 - Save Thumbnail Continuous Capture Quality

Problem: `SaveThumbnailSystem` used a hard low-quality threshold to delete/skip thumbnails and publish low-tier metadata. That turned user-facing save metadata into a binary hardware casualty.
Solution: Remove the skip branch and resolve JPG encode quality with a smooth `GlobalQualityWeight` curve from survival to visual-overkill. Rename the deferred metadata constants to quality-pressure semantics while preserving byte values.
Rejected Alternatives: Keeping `LowTierSkipped`; adding another quality enum; scaling gameplay save identity; allocating alternate thumbnail buffers.
Scalability potential: Weak devices still capture a thumbnail with cheaper encoded bytes; middle/high/ultra move through the same curve to higher JPG quality. Save identity and slot metadata route stay unchanged.
Hardware Impact: Cold save I/O only; 0 B/frame added. No profiler-backed microsecond claim.

## Decision 47 - Sargassum Facade Compute Support and Scalar Resolution

Problem: `SargassumCrestDampingController` denied the ocean damping facade whenever `AllowHighResourceComputeShaders` was false, even though facade intensity already consumes quality and the effect is presentation-only.
Solution: Gate only on compute shader/kernel support and scale facade RenderTexture dimensions during cold allocation with continuous quality. World rect stays identical, so shader sampling preserves the same semantic space.
Rejected Alternatives: Hard hardware-tier denial; full-resolution facade on survival pressure; reallocating facade RT every quality fluctuation.
Scalability potential: Weak devices use smaller facade textures and lower wave/oil strength; middle quality grows resolution; high/ultra gets full facade texture density.
Hardware Impact: 0 B/frame added. Static estimate: survival facade area can shrink toward roughly 12% of full resolution, reducing dispatch pixels and RT memory.

## Decision 48 - Presentation Compute Gates Use Capability, Not Hardware Bucket

Problem: GPUI scatter, sargassum micro-fauna compute binding, volumetric fog, volumetric light, and biolum SSGI selected compute/proxy paths through `HardwareTierDetector.AllowHighResourceComputeShaders`.
Solution: Replace those gates with actual compute support checks (`SystemInfo.supportsComputeShaders`) plus existing shader/kernel validation. Quality-sensitive cost remains in already-present scalar resolvers: proxy blend, ray steps, internal scale, sample count, placement density, and culling budgets.
Rejected Alternatives: Keeping high-resource hardware buckets; forcing compute on unsupported devices; changing gameplay-truth boid or scatter ownership in the same pass.
Scalability potential: Compute-capable weak devices keep the continuous visual route instead of a hard proxy cliff. Middle/high/ultra spend the same scalar budgets on richer fog, god rays, SSGI, and flora instancing.
Hardware Impact: 0 B/frame added. No runtime proof claimed; static result removes five binary hardware branches from presentation/visual admission paths.

## Decision 49 - Runtime Compute Admission Capability Route

Problem: Multiple runtime compute paths still treated `HardwareTierDetector.AllowHighResourceComputeShaders` as an admission authority: parasite VFX, TerminalOS blit, bilateral DRS, foam, marine snow, boids, GPUI rock/scatter, carve debris, PDA sonar, ocean-atmosphere wave sampling, fluid abyssal/advection, indirect vegetation, async buoyancy readback, and Crest FFT disable.
Solution: Replace the high-resource hardware bucket with literal `SystemInfo.supportsComputeShaders`, while preserving existing compute shader null checks, `HasKernel`, `IsSupported`, and thread-group validation. Existing quality/cadence/density budgets remain the cost governors.
Rejected Alternatives: Keeping visual/compute features off on compute-capable weak devices; forcing unsupported compute paths; broad-changing fluid/buoyancy truth algorithms in the same pass.
Scalability potential: Weak compute-capable devices stay on the same scalable route at lower budgets instead of falling off a binary cliff; middle/high/ultra keep richer GPU presentation and readback paths without new branches.
Hardware Impact: 0 B/frame added. Static effect: removes all direct project-runtime `AllowHighResourceComputeShaders` admissions outside the detector itself.

## Decision 50 - Bilateral DRS Continuous Edge Mask

Problem: Bilateral DRS used `qualityGate == 0f` to skip Sobel and publish a clear-only edge mask, creating a binary render-graph path exactly at the low-quality boundary.
Solution: Add `MinimumEdgeMaskQualityGate` and `ResolveEdgeMaskQualityGate`; even survival pressure keeps a tiny Sobel edge-mask dispatch while the existing constant buffer quality still controls edge influence.
Rejected Alternatives: Leaving clear-only Sobel bypass; running full-size edge detection at survival pressure; changing shader constants or output layout.
Scalability potential: Low uses a tiny edge mask, middle grows resolution through the same scalar, high/ultra reaches full edge mask density without route switching.
Hardware Impact: 0 B/frame added. Survival edge-mask linear resolution is clamped to ~3.125% before thread-group rounding, preserving continuity at minimal GPU cost.

## Decision 51 - Abyssal Thermal Grid Continuous Route

Problem: `AbyssalThermalManager.UsesThermalGrid` denied the whole 32^3 thermal map/readback route below a hard quality/VRAM threshold. That made thermal sampling truth depend on visual pressure instead of owning one consistent route.
Solution: Remove the quality threshold from `UsesThermalGrid`, keep route availability structural, and move cost control into `ResolveThermalMapColdTickSeconds` plus `ResolveThermalMapDiffusion01`. Idle thermal storage is not allocated until active vents or existing thermal map storage require clearing.
Rejected Alternatives: Keeping `effectiveQuality >= threshold`; shrinking DTO/readback layout; deleting thermal readback on weak devices; building to validate syntax while a host `dotnet` process was active.
Scalability potential: Weak devices keep the same thermal route but stretch cold ticks up to survival cadence and reduce diffusion blend; middle/high/ultra move continuously toward faster thermal updates and richer diffusion.
Hardware Impact: 0 B/frame added. Static estimate: survival cold map cycles can run up to 4x slower without a binary truth cutoff.

## Decision 52 - VR Ghost Hand Quality Naming

Problem: `VRSomaticProvider` already scaled ghost-hand visibility threshold by continuous quality, but the active field was named `reduceGhostHandsAtLowQuality`, preserving low-tier policy language in VR comfort code.
Solution: Rename the active field to `scaleGhostHandToleranceByQuality` and preserve both legacy serialized aliases with `FormerlySerializedAs`. The mask body still uses `Smoothstep01(_globalQualityWeight01)` and `math.lerp`.
Rejected Alternatives: Removing the authoring toggle and changing user comfort behavior; leaving the low-quality field name as future binary-policy bait.
Scalability potential: Weak devices get more tolerant ghost hand reveal distance through the same curve; middle/high/ultra converge smoothly toward authored base threshold.
Hardware Impact: 0 B/frame; no behavior-layout change, only contract hygiene.

## Decision 53 - Tether Compatibility Tier Scalar Ordinal

Problem: `TetherManager.ResolveQualityTierFromGlobalWeight` still converted continuous quality into legacy `HectonQualityTier` with four hard thresholds. Active tether math already reads continuous `GlobalQualityWeight`, but the fallback label was a future binary-policy foothold.
Solution: Replace the threshold ladder with `ResolveCompatibilityQualityTierOrdinal`, a single smooth scalar mapping from `Low` to `Ultra`, rounded only at the final compatibility enum boundary.
Rejected Alternatives: Removing the enum and breaking existing tether APIs; keeping the threshold ladder; passing the compatibility enum deeper into active solver math.
Scalability potential: Weak, middle, high, and ultra tiers all flow through the same scalar curve; the enum remains only a legacy label and no longer owns cost transitions.
Hardware Impact: 0 B/frame; no allocation and no gameplay authority route change.
