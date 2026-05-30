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

## Decision 54 - KCC Velocity Continuous Quality Pressure

Problem: `KccVelocitySignal` encoded quality pressure as a binary low-tier flag. `SomaticKinematicsRuntime` set `StateFlagLowTier` at `qualityPressureQ8 >= 128`, and `HydrodynamicKccRuntime` published `KccVelocitySignal.FlagLowTier` through `survivalPressure01 > 0.5f`.
Solution: Reuse the existing byte at offset 77 as `QualityPressureQ8`. Somatic publishes `_frameContext.QualityPressureQ8`, hydrodynamic KCC publishes a smooth Q8 survival-pressure curve, player kinematics publishes Q8 from cached `GlobalQualityWeight`, and `PhysicsDeterminismSignals` exposes the byte as an optional bridge parameter.
Rejected Alternatives: Keeping `FlagLowTier`; adding a second signal; changing the 128-byte signal size; using `GlobalRegistry` or scene lookups in producers.
Scalability potential: Weak devices expose continuous pressure without changing authority ownership; middle devices move through the same Q8 route; high and ultra devices converge to zero pressure while retaining movement authority flags.
Hardware Impact: 0 B/frame managed allocation. Existing signal padding was reused; no DTO size growth.

## Decision 55 - Somatic Kinematics DataVault Lock Flattening

Problem: `SomaticKinematicsRuntime.FixedTick` acquired state, sphere, hand history, tuning, drag LUT, signal scratch, blackbox, and blackbox cursor write buffers together and held them across `job.Run()`. CSV/tuning paths also held multiple write buffers in one method.
Solution: Add persistent local NativeArray scratch for the fixed-step job. FixedTick hydrates from read-only vault snapshots, runs the job locally, then flushes each changed buffer through one write lock at a time with `try/finally`. Tuning and CSV override writes are split into sequential single-lock sections.
Rejected Alternatives: Keeping the multi-lock job window; allocating Temp NativeArrays every fixed tick; dropping blackbox history; moving somatic authority out of DataVault in this pass.
Scalability potential: Weak devices avoid deadlock stalls and keep local deterministic kinematics; middle/high/ultra keep the same rich hand-stroke/blackbox route while lock lifetime stays bounded.
Hardware Impact: 0 B/frame managed allocation. Persistent native scratch is allocated once; per-frame DataVault write-lock max is statically bounded to 1.

## Decision 56 - Thermal DRS Survival Pressure Semantics

Problem: `ThermalDynamicResolutionAdapter` had already moved render-scale math to continuous quality, but the active pressure flag was still named `FlagLowTierEmergency` and the scalar multiplier was `lowTierWeight01`. That naming kept a binary hardware-tier foothold in a high-frequency presentation governor.
Solution: Rename the active flag to `FlagSurvivalPressureEmergency`, add explicit survival pressure fade constants, resolve `ResolveSurvivalPressureWeight01(float qualityWeight01)` from the continuous quality scalar, and publish `ResolutionScaleStateFlags.SurvivalPressureEmergency`. Preserve `ResolutionScaleStateFlags.LowTierEmergency` only as a same-bit compatibility alias so existing readers do not lose the byte contract.
Rejected Alternatives: Changing the `ResolutionScaleState.Flags` bit layout; deleting the legacy alias mid-batch; leaving active low-tier names because behavior was already scalar.
Scalability potential: Weak devices express survival pressure as a smooth fade between 0.12 and 0.44 quality, middle devices taper through the same curve, and high/ultra devices clear the pressure bit without a hard route change.
Hardware Impact: 0 B/frame. No profiler-backed microsecond claim; this prevents future binary pressure routing while preserving the existing 64-byte DRS state layout.

## Decision 57 - Homeostasis And QA Survival Emergency Bit

Problem: `HomeostasisBrain.ScalabilityDictator` and `Shinobu38QaWatchdogRuntime` still wrote active emergency bits through `LowTierEmergency` names, even though the actual owner state is `_survivalEmergencyActive` and system-health pressure.
Solution: Add `SystemBit.SurvivalPressureEmergency` on the existing bit value, keep `LowTierEmergency` only as an enum alias, and update active kill-mask writes/reads to the survival-pressure bit. Rename the QA mock-vault flag to `VaultFlagSurvivalPressureEmergency`.
Rejected Alternatives: Changing the mask bit value; deleting compatibility aliases; leaving proof harness telemetry with low-tier naming.
Scalability potential: Weak devices still enter survival pressure through the same mask bit, middle devices recover through existing hysteresis, and high/ultra only see the alias as compatibility metadata.
Hardware Impact: 0 B/frame; no mask or DTO size growth.

## Decision 58 - QA Watchdog Continuous Normal Blend

Problem: The headless QA bot used `richNormalGate > 0f ? SampleNormal : cheapNormal`, a binary quality route for obstacle avoidance proof data.
Solution: Rename the threshold to `RichNormalFadeStart01`, always resolve the rich SDF normal inside the QA job, and use the continuous gate only as blend weight against the cheap dear-lie normal.
Rejected Alternatives: Keeping the branchy low-quality collapse; deleting the rich SDF normal proof path; adding a scheduler/cadence layer to a small headless watchdog job.
Scalability potential: Weak QA runs deterministic one-route math with the cheap normal dominating the blend; middle/high/ultra get progressively richer obstacle normals without a route switch.
Hardware Impact: 0 B/frame GC. CPU cost can rise in the headless QA obstacle branch because rich normal is no longer skipped; accepted because this is a proof harness, not active player presentation.

## Decision 59 - Rendering Bridge Survival Pressure Naming

Problem: `GlobalShaderDispatcher` and `HectonUberNoirRuntimeBridge` still called their continuous pressure scalar `lowTierWeight01` and carried `ResolveLowTierWeight01`/`ResolveLowTierFloor01` helpers. That kept binary vocabulary in shader-global and Noir presentation bridges.
Solution: Rename the scalar route to `survivalPressureWeight01`, rename the floor/weight helpers, update telemetry bucket state, mock shader kernel fields, wake params, and Uber Noir feature-mask/blackbox hashing to the survival-pressure names without changing numeric values.
Rejected Alternatives: Changing shader slot layout; rewriting the Noir feature mask contract; leaving active render bridge vocabulary as low-tier residue.
Scalability potential: Weak devices still receive high survival-pressure weights for cheaper wakes/Noir features; middle devices taper continuously; high/ultra clear pressure while visual-overkill features remain scalar-driven.
Hardware Impact: 0 B/frame; no shader slot, telemetry stride, or DataVault buffer growth.

## Decision 60 - Verification Throttle

Problem: The final verification needed syntax and contract confidence, but the host had external `dotnet` activity and the task forbids compile spam.
Solution: Use prompt re-extraction, targeted residue `rg`, method-body lookup parsing, write-lock release-balance parsing, restricted `git diff --check`, and a string/comment-aware brace scan on recent touched files. Do not launch build or Unity tests.
Rejected Alternatives: Running `dotnet build` under active compiler load; trusting chat-only claims; writing JSON/bin reports.
Scalability potential: Verification adds no runtime route and preserves CPU for other agents while source gates remain in editor tests.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime change.

## Decision 61 - World Sampler Survival Sampling Pressure Naming

Problem: `GlobalWorldSampler` still wrote active result flags through `MathLodLow` vocabulary and used a literal `qualityWeight <= 0.05f` check for that flag. The expensive sampler math already scales through `ResolveExpensiveSamplingWeight`, but the active flag name and threshold invited future binary policy.
Solution: Add `SurvivalSamplingPressure` and `ForceSurvivalSamplingPressure` same-bit enum names, preserve the old enum members only as compatibility aliases, and route active flag writes through `ResolveSurvivalSamplingPressureFlag(float qualityWeight)`.
Rejected Alternatives: Deleting public enum members would break unknown consumers; computing all expensive normals at survival pressure would violate the toaster path and spend CPU with zero visual benefit.
Scalability potential: Weak devices keep the cheap sampler path while reporting survival pressure by name; middle devices fade expensive sampling through the existing scalar; high/ultra retain rich sampling without a route rename.
Hardware Impact: 0 B/frame; DTO layout unchanged. No profiler-backed microsecond claim.

## Decision 62 - Toxic Outgassing Binary Fallback Telemetry Removal

Problem: `ToxicOutgassingChemistryRuntime` published `TelemetryFlagFallbackRadial` when `qualityWeight < 0.3f` or `_activeResolution == LowResolution`, even though telemetry already carries both `GlobalQualityWeight` and `ActiveResolution`.
Solution: Remove the fallback radial telemetry bit from schedule/header publication and keep flags for mock/failure/NaN semantics only. The active simulation resolution remains continuous through `ResolveResolution(ResolveRuntimeQualityWeight01())`.
Rejected Alternatives: Keeping duplicate binary telemetry; adding a new byte to DTOs; changing gas truth or resolution ownership in the same pass.
Scalability potential: Weak, middle, high, and ultra telemetry now reports the actual scalar and resolution instead of a coarse bucket; runtime cost remains governed by the existing quality-derived resolution.
Hardware Impact: 0 B/frame; no buffer growth and no new allocation.

## Decision 63 - Sampler/Toxic Verification Without Build

Problem: The new cleanup needed source confidence, but host `dotnet` and `csc` processes were active and the compilation throttle forbids build spam.
Solution: Run targeted `rg` residue checks, method-token validation for the editor source gates, hot-loop forbidden lookup scan, write-lock scan, restricted `git diff --check`, and a string/comment-aware brace scan. Do not launch build or Unity tests.
Rejected Alternatives: Running `dotnet build`; treating broad candidate names as bugs without ownership proof; editing gameplay loot quality because it contained the word quality.
Scalability potential: Source gates now prevent fallback radial telemetry and active MathLodLow flag naming from returning.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime change.

## Decision 64 - Utility AI Continuous Quality Output And Over-Budget Faults

Problem: AI cognition used `UtilityAICognitionActionFlags.HighQuality` as a binary `quality > 0.75f` action-output marker, and reused the same active flag name for Burst microsecond over-budget telemetry. This mixed device-quality semantics with timing faults and left a hard quality threshold in a high-frequency Burst job.
Solution: Reuse `CognitionActionOutputDTO` offset 58 as `QualityWeightQ8`, encoded from the existing continuous quality scalar. Rename active timing-fault writes to `OverBudget`; keep `HighQuality` only as a same-bit compatibility alias in the contract. Add an editor source gate proving runtime jobs/vault no longer call the active `HighQuality` flag or `quality > 0.75f`.
Rejected Alternatives: Growing the 64-byte output DTO; keeping a binary visual-overkill flag; deleting compatibility aliases that unknown readers may still compile against; adding registry or scene lookups for quality access.
Scalability potential: Weak devices publish low Q8 quality while candidate budget and tick cadence remain continuous; middle devices interpolate through the same byte; high and ultra devices publish saturated Q8 without a separate route or authority change.
Hardware Impact: 0 B/frame managed allocation. Existing DTO padding was reused; the Burst job adds one `round`/`clamp` encode and removes one binary quality comparison.

## Decision 65 - Survival Pressure Naming For Active Flags

Problem: Several active writers still used low/reduced quality names even though their math already consumed continuous pressure: acoustic portal `LowTierFallback`, instance culling `LowTierDistance`, marauder descriptor `LowTierFlag`, and Apex brain `ReducedQualityNodeBudget`.
Solution: Add same-value survival-pressure names and update active runtime writers to `SurvivalBudgetFallback`, `SurvivalDistancePressure`, `SurvivalBandFlag`, and `SurvivalNodeBudgetPressure`. Keep old names as aliases only.
Rejected Alternatives: Removing public aliases; changing enum/bit values; treating the names as harmless while they still sat in active writer code.
Scalability potential: Weak devices still take the same cheap budget paths; middle devices slide through the existing scalar curves; high/ultra devices keep richer paths without a separate boolean hardware route.
Hardware Impact: 0 B/frame; no DTO, enum value, or buffer size change.

## Decision 66 - Marauder Outpost DataVault Job Lock Flattening

Problem: `MarauderOutpostGenerationService` held DataVault write locks across scheduled solve/extraction/shift jobs. Extraction was worst: six write buffers could be locked together until `LateFrameTick`, creating a deadlock vector and violating single-writer phase discipline.
Solution: Add persistent local scratch arrays for solve WFC, extraction mutable grid/matrices/cell types/interactables/counters, and shift matrices. Jobs write scratch only. `LateFrameTick` copies scratch into DataVault one buffer at a time through `TryFlushScratchBuffer<T>`, which releases inside `finally`.
Rejected Alternatives: Keeping the multi-lock job window; allocating TempJob scratch per generation phase; completing jobs immediately on the scheduling frame; rewriting the WFC job contract outside this pass.
Scalability potential: Weak devices avoid long-held vault locks during outpost generation; middle/high/ultra keep the same generated geometry and richer dimensions while vault ownership stays phase-safe.
Hardware Impact: 0 B/frame managed allocation. Persistent native scratch is allocated cold; per-frame/job-finalization DataVault write-lock max is statically bounded to 1.

## Decision 67 - Thermal DRS Compatibility Scalar Ordinal

Problem: `ThermalDynamicResolutionAdapter.ResolveQualityTierFromWeight` still used four hard quality thresholds to derive a legacy `HectonQualityTier`, and FSR admission used a graphics-memory-or-`quality >= 0.86f` cliff.
Solution: Convert the compatibility tier byte through `ResolveCompatibilityQualityTierOrdinal(float qualityWeight01)`, a single smoothed scalar ordinal from Low to Ultra. FSR admission now depends on cold capability plus the existing continuous eligibility curve.
Rejected Alternatives: Keeping threshold tiers; deleting the compatibility byte; using graphics memory as a second binary visual admission authority.
Scalability potential: Weak devices stay on the same DRS route through lower scalar eligibility; middle/high/ultra converge through the same ordinal and upscaler eligibility curve.
Hardware Impact: 0 B/frame; no DTO growth and no new allocation.

## Decision 68 - Loot And Spatial Audio Survival Endpoints

Problem: Loot magnet budgets, virtual voice budget floor, spatial SDF sampler, and SubmarineOS sonar cadence still exposed active low-tier names or quality cutoffs, even where math already scaled continuously.
Solution: Rename active endpoints to survival/standard/high-fidelity/visual-overkill names, keep old constants only as aliases, smooth loot budgets through one envelope helper, remove the spatial SDF `qualityWeight > 0.02f` enable gate, stop dropping ambient sample rate from `GlobalQualityWeight`, and reuse blackbox padding for `QualityWeightQ8`.
Rejected Alternatives: Deleting public aliases; keeping quality-driven audio sample-rate switches; publishing a survival-pressure boolean in acoustic blackbox telemetry; adding allocations for lookup tables or per-frame policy data.
Scalability potential: Weak devices still get lower voice/budget/cadence values via scalar math; middle devices interpolate; high/ultra get denser acoustic, wake, and sonar presentation without route forks.
Hardware Impact: 0 B/frame; all changes reuse existing value paths and persistent buffers.

## Decision 69 - Noir Telemetry Scalar Ownership

Problem: Noir telemetry converted `QualityAndLimits.x` into a binary active feature flag while the same telemetry row already stores `GlobalQualityWeight01`.
Solution: Remove the quality-derived feature bit and leave feature flags for actual active effects: chroma, glitch offset, and AB split. Quality remains a scalar telemetry field.
Rejected Alternatives: Keeping duplicate binary quality flag; changing telemetry stride; adding another DTO field.
Scalability potential: Weak, middle, high, and ultra diagnostics now read the actual scalar instead of a coarse bit.
Hardware Impact: 0 B/frame; no buffer or stride change.

## Decision 70 - Math LOD Runtime Config Scalars

Problem: `MathLodApproximation.PublishConfig` still converted `GlobalQualityWeight` into hard minimum-survival and visual-overkill config flags, and reserved DTO bytes carried no scalar proof of overkill pressure.
Solution: Stop writing those quality bits, reuse the existing explicit-layout offset 52 as `VisualOverkillWeight01`, and publish continuous `SurvivalPressure01` plus `VisualOverkillWeight01` from scalar helper curves.
Rejected Alternatives: Keeping `quality <= 0.1001f` / `quality >= 0.95f`; growing `MathLodConfigDTO`; deleting legacy constants that unknown readers may still compile against.
Scalability potential: Weak devices publish high survival pressure and zero overkill through one DTO route; middle devices interpolate; high/ultra publish smooth overkill weight without a separate authority bit.
Hardware Impact: 0 B/frame. Existing DTO storage was reused; no registry lookup, allocation, or buffer growth.

## Decision 71 - Ambient Biota Continuous Spawn And Shader Fake

Problem: Ambient Biota spawn signals and state flags encoded quality through hard survival/visual-overkill bits, telemetry duplicated visual-overkill as a threshold flag, and the indirect shader used a binary overkill branch into a 16-tap parallax loop.
Solution: Overlay `EntitySpawnSignal` offset 57 as `QualityWeightQ8` and offset 59 as `SurvivalPressureQ8`, publish only ecology flags from macro hydration, keep legacy flag names as aliases, rename active ambient state semantics to survival/headlight names, and replace shader `Parallax16` with one continuous triangle-wave `ParallaxCheat`.
Rejected Alternatives: Growing the 64-byte spawn signal; deleting compatibility aliases; keeping a 16-tap branch; computing full parallax at low quality.
Scalability potential: Weak devices keep cheap billboard pressure and one-tap parallax with near-zero overkill weight; middle devices ramp the same path; high/ultra spend scalar overkill on richer movement, salt/silt, and parallax without variant or branch switches.
Hardware Impact: 0 B/frame managed. The former overkill shader path loses 15 loop taps; no profiler-backed microsecond claim.

## Decision 72 - Ambient/MathLod Verification Throttle

Problem: The pass changed explicit-layout DTO overlays and shader math, but the host had external compiler processes and the task forbids build spam.
Solution: Use prompt re-extraction, targeted runtime/shader residue `rg`, method-body hot lookup parsing, write-lock acquisition counting, string/comment-aware brace scan, restricted `git diff --check`, and editor source gates. Do not launch build or Unity tests.
Rejected Alternatives: Running `dotnet build`; writing JSON/bin proof; claiming whole-repo proof from local changes.
Scalability potential: Source gates now reject regression to hard Ambient quality flags, 16-tap branch parallax, and Math LOD binary config flags.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime change.

## Decision 73 - Marauder Outpost Compatibility Tier Scalarization

Problem: `MarauderOutpostGenerationService` still resolved `OutpostGenerationQualityTier` through three hard thresholds (`0.25`, `0.55`, `0.85`). The generated dimensions were already continuous, so the enum had become a compatibility label with binary cut points.
Solution: Rename the active field to `_compatibilityQualityTier`, derive it through `ResolveCompatibilityQualityTierOrdinal(float qualityWeight01)` with `SmoothStep01` and `math.lerp`, and publish the real scalar as `OutpostGenerationSnapshot.QualityWeightQ8`.
Rejected Alternatives: Deleting `OutpostGenerationQualityTier` and breaking unknown readers; keeping the hard ladder; passing the enum back into WFC or matrix extraction.
Scalability potential: Weak devices, middle devices, high-end, and ultra all use the same scalar dimension/quality route. The enum is now only a rounded compatibility label at the boundary.
Hardware Impact: 0 B/frame. Existing explicit-layout padding was reused; no allocation, no registry lookup, no snapshot size growth.

## Decision 74 - Marauder Survival-Band Snapshot Scalar

Problem: After tier cleanup, `ResolveDescriptorFlags()` still converted continuous quality into `MarauderOutpostConstants.SurvivalBandFlag` by `survivalBandWeight > 0.5f`. That left a binary quality bit in an otherwise scalar snapshot.
Solution: Add `SurvivalBandWeightQ8` at snapshot offset 57 and encode `ResolveSurvivalBandWeight01(_generationQualityWeight01)`. Leave `Flags` for factual state such as heightmap fallback; keep `SurvivalBandFlag` only as a legacy constant alias in the job constants.
Rejected Alternatives: Keeping the survival-band bit; removing descriptor flags entirely; increasing the 64-byte snapshot stride.
Scalability potential: Weak devices expose high survival-band pressure as a byte, middle devices interpolate, and high/ultra devices fade to zero without changing the data route.
Hardware Impact: 0 B/frame. One existing padding byte was reused; no DataVault lock, DTO stride, or managed allocation change.

## Decision 75 - Outpost Scalar Verification Without Build

Problem: The outpost pass modified explicit-layout contracts and runtime snapshot publication. Compile throttle still forbids using `dotnet build` as a reflex.
Solution: Re-extract prompt, run targeted residue `rg`, method-body hot lookup scan, refined max-held DataVault writer scan, string/comment-aware brace scan, restricted `git diff --check`, and process check. Do not launch build, MSBuild, Unity runner, JSON reports, or binary dumps.
Rejected Alternatives: Running a project build for syntax-local edits; accepting a naive write-lock count that flags sequential locks as nested; leaving source gates unchanged.
Scalability potential: Source gates now reject regression to outpost tier ladders and binary survival-band descriptor bits.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime change.

## Decision 76 - Apex Brain Survival Node Budget Q8

Problem: `ApexBrainJob` still wrote survival node-budget pressure as a binary `ApexBrainFlags.SurvivalNodeBudgetPressure` bit derived from `quality >= ApexBrainConstants.MinimumQualityNodeHold`.
Solution: Reuse explicit-layout padding in `ApexBrainOutputDTO` and `ApexInfluenceNode` for `SurvivalNodeBudgetPressureQ8`. Encode the existing continuous quality curve with `EncodeSurvivalNodeBudgetPressureQ8`; keep old flag names as aliases only, with no active writer.
Rejected Alternatives: Growing DTO stride; deleting public aliases; keeping the binary helper because the node count already scaled continuously.
Scalability potential: Weak devices publish high pressure while evaluating fewer nodes; middle devices interpolate; high/ultra publish low pressure and spend scalar budget on full ambush nodes without a route fork.
Hardware Impact: 0 B/frame. One byte in existing padding reused for output and influence nodes; no registry lookup, allocation, or DataVault lock added.

## Decision 77 - Q8 Telemetry Instead Of Quality Flags

Problem: Carve debris, splashdown fluid impulse, and dispatcher blackbox telemetry converted continuous quality/pressure into hard flags (`>= 0.75`, `> 0.001`, `<= 0.25`). Those flags duplicated scalar state and preserved binary quality semantics in proof surfaces.
Solution: Carve debris blackbox now stores `QualityPressureQ8` in existing padding and hashes it. Splashdown fluid telemetry stores `_splashdownImpulseQualityPressureQ8` and folds it into the telemetry context. Dispatcher blackbox packs `GlobalQualityWeight` Q8 into the upper byte of its existing `Flags` ushort while lower bits remain factual state flags.
Rejected Alternatives: Keeping threshold flags; expanding telemetry structs; moving the data through managed reports; changing runtime gameplay truth from telemetry code.
Scalability potential: Weak devices report high pressure as a scalar, middle devices report partial pressure, and high/ultra report low pressure without binary cuts or new data lanes.
Hardware Impact: 0 B/frame. Existing fields/context bits reused; no managed allocation, no new buffer, no material or shader route.

## Decision 78 - Q8 Pass Verification Throttle

Problem: The pass touched Burst/job-facing structs and core telemetry, but compile throttling forbids reflexive build validation.
Solution: Re-extract prompt, run targeted runtime residue scans, touched-method hot lookup parsing, diff-based write-lock scan, string/comment-aware brace scan, restricted `git diff --check`, and process check. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; accepting test-only grep hits as runtime residue; adding DataVault telemetry just to prove the change.
Scalability potential: Source gates now reject regression to threshold flags across Apex, debris, splashdown, and dispatcher blackbox paths.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime change.

## Decision 79 - Thermodynamics Hazard Q8 Telemetry

Problem: `ThermodynamicsHazardGridRuntime.ScanTelemetryJob` converted existing `QualityPressureQ8` and `HealthPressureQ8` values into two binary flags. That collapsed continuous pressure into bits inside a 300-frame blackbox row.
Solution: Remove `TelemetryFlagQualityPressure` and `TelemetryFlagHealthPressureSurvival` active writers. Store `QualityPressureQ8` and `HealthPressureQ8` directly in `ThermodynamicsHazardTelemetryEntry` padding at offsets 56 and 57.
Rejected Alternatives: Keeping threshold bits; increasing the 64-byte telemetry stride; moving pressure proof into managed logs.
Scalability potential: Weak devices publish high pressure as byte values; middle devices keep partial pressure; high and ultra publish low pressure without changing the telemetry route.
Hardware Impact: 0 B/frame. Existing padding reused; no new lock, buffer, allocation, or job dependency.

## Decision 80 - World Sampler Result Flag Purge

Problem: `GlobalWorldSampler` still reported survival sampling pressure as a result flag derived from `ResolveExpensiveSamplingWeight <= 0.0001f`. No runtime consumer used the bit; it was active binary quality telemetry in the hot sampling result.
Solution: Remove `ResolveSurvivalSamplingPressureFlag` and stop OR-ing `SurvivalSamplingPressure` in `SampleDistanceOnly` and `EstimateNormal`. Keep enum aliases as ABI compatibility only.
Rejected Alternatives: Changing the 64-byte `TerrainSampleResult` layout; adding a separate result Q8 byte; deleting public enum aliases during a batch run.
Scalability potential: Sampling cost still scales through continuous `ResolveExpensiveSamplingWeight`; low, middle, high, and ultra paths no longer expose a binary result route.
Hardware Impact: 0 B/frame. Removes one helper call/branch in the distance sample path and preserves DTO size.

## Decision 81 - Visor Fluid Blackbox Scalar Bytes

Problem: Visor fluid refraction blackbox flags converted four scalar states into threshold bits: quality pressure, homeostasis fallback, thermal motion cull, and visual overkill.
Solution: Remove those active flag constants and writers. Reuse blackbox bytes 48-51 for `QualityPressureQ8`, `HomeostasisFallbackQ8`, `ThermalMotionCullQ8`, and `VisualOverkillQ8`, serialize them into the dump row, and hash fallback/cull scalar inputs directly.
Rejected Alternatives: Keeping `> 0.001f` / `> 0.5f` flag thresholds; growing `VisorRefractionTelemetryEntry`; moving proof to docs instead of blackbox data.
Scalability potential: Low hardware reports pressure/fallback/cull as continuous bytes; middle hardware reports partial values; high/ultra reports overkill pressure without a binary route.
Hardware Impact: 0 B/frame. Existing 64-byte row reused; no material instance, no new buffer, no extra DataVault lock.

## Decision 82 - Telemetry Scalar Verification Without Build

Problem: The pass changed explicit-layout telemetry rows and hot sampling/presentation paths while an external `dotnet` process was active.
Solution: Use prompt extraction, runtime residue scans, touched-method hot lookup scan, diff write-lock scan, string/comment-aware brace scan, restricted `git diff --check`, and process checks. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; claiming source proof without source gates; changing unrelated dirty visor code owned by another agent.
Scalability potential: Source gates now reject regression to binary quality bits in thermodynamics, world sampler, and visor fluid blackbox telemetry.
Hardware Impact: Compile CPU avoided; runtime change remains 0 B/frame.

## Decision 83 - Volcanic Debris Lift Q8 Telemetry

Problem: `VolcanicTelemetryFinalizeJob` and mock debris rows converted continuous debris lift weight into `TelemetryFlagDebrisCulled` when quality collapsed to zero. That made the blackbox report a quality bit instead of the scalar that actually drives debris lift cost.
Solution: Remove the debris-cull flag writer and constant. Store `DebrisLiftWeightQ8` at offset 60 of the existing 64-byte `VolcanicUpdraftTelemetryEntry`, encode it with `VolcanicUpdraftVault.EncodeUnitQ8`, and include the byte in the fault dump stream.
Rejected Alternatives: Keeping an epsilon flag; growing the telemetry row; removing debris lift entirely at low quality; writing a managed report.
Scalability potential: Weak devices show near-zero debris lift weight without a binary route; middle devices publish partial lift; high and ultra devices publish full lift while preserving the same signal and telemetry path.
Hardware Impact: 0 B/frame. Existing padding reused; no allocation, no new DataVault lock, no gameplay truth change.

## Decision 84 - Procedural Bite IK Visual-Overkill Weight

Problem: `ResolveBiteRuntimeFlags()` forced maximum-quality and visual-overkill flags every bite solve. The Burst job then ran the wrap-anchor/tentacle path as a binary branch, while hull dents and debris spawned through active `ResultFlagVisualOverkill` checks.
Solution: Add `VisualOverkillWeight01` to `ProceduralBiteJob`, pack its Q8 value into the result flags, write the same scalar into `BiteIkSolveEvent.VisualOverkillWeight01`, and scale wrap bone count, tentacle radius/length, hull dent radius/depth, and debris quantity continuously from `GlobalQualityWeight`.
Rejected Alternatives: Keeping always-on overkill; deleting tentacle wrap visuals; adding a new DTO; using a low-tier hull flag; changing the public signal contract mid-batch.
Scalability potential: Weak devices retain mandible contact and small debris while wrap bones fall to zero by scalar capacity; middle devices get partial wrap/debris; high and ultra spend the same path on more wrap bones, stronger dents, and denser shards.
Hardware Impact: 0 B/frame managed allocation. Existing job/value fields and BiteIk telemetry padding are used; low-quality solves avoid forced tentacle bone writes formerly caused by unconditional overkill flags.

## Decision 85 - Volcanic/Fauna Verification Without Build

Problem: The pass touched Burst job source, explicit-layout telemetry, and LateFrame presentation signal writers, but the host had an external `dotnet` process and compile throttling forbids build spam.
Solution: Re-extract prompt, run active-source residue scans, touched hot-method lookup parsing, lock scan, string/comment-aware brace scan, restricted `git diff --check`, process check, and editor source gates. Do not launch build, MSBuild, Unity runner, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; treating compatibility constants as active writers; leaving source gates unchanged.
Scalability potential: Source gates now reject regression to volcanic debris cull flags and bite visual-overkill flag routing.
Hardware Impact: Compile CPU avoided; 0 B/frame runtime allocation change.

## Decision 86 - Repair Tool Continuous VFX Route

Problem: `RepairTool.PublishRepairSparkSignal` used `ResolveRepairQualityCurve(quality01) > 0.0001f` to decide whether the spark signal entered the compute debris renderer. `PublishHullRepairedSignal` also converted the same scalar into `LowTierVisualOnlyFlag`.
Solution: Always publish `DebrisSpawnSignal.FlagComputeShard` for repair sparks and let `ResolveRepairSparkQuantity` scale quantity from survival to visual-overkill. Add `HullRepairedSignal.QualityWeightQ8` as an offset-62 alias and write only `CompletedFlag` from the repair tool.
Rejected Alternatives: Keeping compute shards as a binary quality switch; growing `HullRepairedSignal`; removing the legacy `QualityTier`/`LowTierVisualOnlyFlag` names during a multi-agent batch.
Scalability potential: Weak devices still get low spark counts through Q-scaled quantity; middle devices interpolate; high and ultra devices spend the same route on denser GPU shards without changing signal authority.
Hardware Impact: 0 B/frame. Existing signal bytes and scalar quantity math are reused; no managed allocation, no DataVault lock, and no new dependency lookup.

## Decision 87 - Repair Verification Without Build

Problem: Repair signal source and an explicit-layout contract changed while an external `dotnet` process was active.
Solution: Run targeted runtime residue scans, hot-method lookup parsing, direct DataVault write-lock scan, string/comment-aware brace scan, restricted `git diff --check`, and process check. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; relying on a broad grep that includes negative test strings; claiming whole-project lock proof beyond touched files.
Scalability potential: Source gates now reject regression to repair spark compute quality thresholds and hull repaired low-tier visual flags.
Hardware Impact: Compile CPU avoided; runtime allocation delta remains 0 B/frame.

## Decision 88 - Tool Diegetic Display Fallback Is Not Quality Authority

Problem: `ToolDiegeticDisplayController` turned continuous quality into two binary UI decisions: disabling the render-texture camera at `_qualityFallback01 >= 0.75f` and collapsing scanner titles at `_qualityFallback01 >= 0.66f`.
Solution: Keep `_qualityFallback01` and `_visualOverkill01` as shader/material scalars, but make render fallback depend only on actual pool/renderability failure. Scanner title compaction now follows only factual fallback state.
Rejected Alternatives: Keeping the low-quality camera-disable branch; adding a second render path; allocating tier-specific UI text buffers; deleting the fallback texture path needed for pool failure.
Scalability potential: Weak devices retain the same UI route with scalar shader pressure; middle/high/ultra increase visual overkill through the same material properties instead of switching identity.
Hardware Impact: 0 B/frame. No new allocation, no new material instance, no DataVault lock, no registry lookup. One branch no longer converts quality into route authority.

## Decision 89 - Tool Display Verification Without Build

Problem: The UI pass touched presentation phase logic while an external compiler process was already active.
Solution: Run targeted threshold residue scan, high-frequency method lookup scan, direct DataVault write-lock scan, string/comment-aware brace scan, restricted `git diff --check`, and source gate update. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running a build for syntax-local UI edits; claiming the negative test strings as runtime residue; changing render texture pooling ownership.
Scalability potential: Source gate now rejects fallback thresholds returning to the diegetic tool screen.
Hardware Impact: Compile CPU avoided; runtime allocation delta remains 0 B/frame.

## Decision 90 - Habitat Compromise Signal Quality Weight

Problem: `HabitatGraphManager.TryPublishBaseModuleCompromisedSignal` used `ResolveModuleStressDisplacementMaxMeters(globalQualityWeight) <= 0f` to choose `BaseModuleCompromisedSignal.LowTierVisualOnlyFlag`. That made a visual displacement scalar decide the signal flag route for a factual module compromise.
Solution: Always publish `BaseModuleCompromisedSignal.MaxDeformationFlag` for the compromise event and carry the continuous quality as `QualityWeightQ8` at the existing offset-45 byte. Keep `QualityTier` and `LowTierVisualOnlyFlag` as compatibility aliases only.
Rejected Alternatives: Keeping the displacement-threshold flag, deleting the legacy field/flag names during a multi-agent batch, or adding a new byte outside the 64-byte signal lane.
Scalability potential: Weak devices still reduce shader displacement through `ResolveModuleStressDisplacementMaxMeters`; middle/high/ultra raise the same scalar visual stress without changing event identity or authority.
Hardware Impact: 0 B/frame. Existing signal byte reused; no allocation, no new DataVault lock, no registry lookup.

## Decision 91 - Habitat Signal Verification Without Build

Problem: The habitat pass changed an explicit-layout signal and a module-stress hot owner path while an external `dotnet` process was active.
Solution: Run targeted residue scan, touched-method hot lookup scan, direct DataVault write-lock scan, string/comment-aware brace scan, restricted `git diff --check`, process check, and editor source gate. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; claiming lock safety without checking `FlushModuleStressShader`; treating legacy alias constants as active writers.
Scalability potential: Source gate now rejects regression to a low-tier compromised-signal flag and enforces the Q8 lane alias.
Hardware Impact: Compile CPU avoided; runtime allocation delta remains 0 B/frame.

## Decision 92 - Tether Quality Scalar Is Manager-Owned

Problem: `TetherManager` passed legacy `HectonQualityTier` into `TetherInstance.Simulate` and `UpdateVisuals`, while `TetherInstance.ResolveTetherQualityWeight` read `HomeostasisBrain.GlobalQualityWeight` from helper code. That left the hot tether path with a hidden global scalar lookup and a compatibility tier argument.
Solution: Add `CachedQualityWeight01` to `TetherManager`, pass `_cachedQualityWeight01` through fixed and late-frame calls, store it in `TetherInstance._qualityWeight01`, and make Verlet point count, iteration count, damping, and taut-line visual fake consume the float directly.
Rejected Alternatives: Keeping `HectonQualityTier` as the hot API, calling `HomeostasisBrain.GlobalQualityWeight` from each tether, or deleting tier-compatible configure overloads used by older call sites.
Scalability potential: Weak, middle, high, and ultra devices all run one scalar solver/visual route; quality changes capacity and damping continuously without changing simulation identity.
Hardware Impact: 0 B/frame. Removes repeated hot scalar reads and keeps all state in value fields; no allocation and no registry lookup.

## Decision 93 - Tether Indirect Route Is Capability-Based

Problem: `ShouldUseIndirectTetherRendering(float qualityWeight01)` switched render identity at `Smooth01(qualityWeight01) >= 0.62f`. That is a binary quality route; direct and indirect drawing should not be selected by a quality cliff.
Solution: Replace the method with `HasIndirectTetherRenderResources()`, gated by cold `SystemInfo.supportsInstancing && SystemInfo.supportsComputeShaders` plus mesh/args buffer presence. Visual density still scales through continuous `visualTier`, crystal density, and silt intensity.
Rejected Alternatives: Leaving the threshold; forcing indirect on unsupported hardware; allocating a second tier-specific renderer.
Scalability potential: Weak devices without GPU capability use direct draw by factual support state; capable devices use the same indirect route while scalar visuals determine richness.
Hardware Impact: 0 B/frame. Existing cold resources reused; no per-frame allocation and no new DataVault lock.

## Decision 94 - Tether Verification Without Build

Problem: The tether pass touched fixed-step simulation and late-frame presentation paths while an external compiler process was active.
Solution: Run residue scan, hot-method scanner over fixed/late/schedule/sim/visual helpers, direct DataVault write-lock scanner, string/comment-aware brace scan, restricted `git diff --check`, process check, and editor source gates. Do not launch build, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; treating the cold quality cache owner as a hot poll; asserting route safety without checking `UpdateVerletVisualUpload` lock release.
Scalability potential: Source gates now reject tier-fed tether visuals, direct instance quality polling, and indirect-render quality thresholds.
Hardware Impact: Compile CPU avoided; runtime allocation delta remains 0 B/frame.

## Decision 95 - World HLOD Impostor Snap Is Not Quality Authority

Problem: `TryBuildChunkImpostorPayload` selected `FlagSurvivalSnap` when `ResolveSmoothGlobalQualityWeight01() <= 0.15f`. That made a presentation route change at one quality cutoff while the HLOD shader already consumes continuous `_HectonGlobalQualityWeight`.
Solution: Remove the threshold constant and always emit `FlagDitherBlend` for active chunk impostors. Keep the existing 32-byte `OctahedralImpostorInstance` payload and existing shader scalar path; do not grow DTO stride for a visual-only cleanup.
Rejected Alternatives: Packing a new per-instance scalar into `SizeFlags.w`, toggling both flags, or preserving survival snap for weak devices. The renderer/shader ignore `FlagSurvivalSnap`, and stride changes would risk cross-domain GPU ABI drift.
Scalability potential: Weak devices still receive impostors and cheap dither; middle, high, and ultra devices get richer continuous view interpolation from `_HectonGlobalQualityWeight` without switching identity at one cutoff.
Hardware Impact: 0 B/frame and no added buffer upload. Removes one branch from payload assembly; compile CPU avoided because external `dotnet` PID 40836 was active.

## Decision 96 - Fauna SDF Hugging Is Weighted, Not Tier-Gated

Problem: `ResolveSdfPayload` skipped SDF terrain hugging when `SmoothQualityCurve(qualityWeight) <= 0.0001f`. The terrain IK job already consumes `GlobalQualityWeight` continuously for sample mode and iteration budgets; the runtime payload should not add an arbitrary epsilon route cliff.
Solution: Add `ResolveSdfHuggingWeight(float qualityWeight)` and multiply the published `sdfRange` by that weight. The job keeps the same DTO layout, same SDF snapshot buffer, and same nearest/trilinear adaptive sampling.
Rejected Alternatives: Adding a new `SdfWeight` field to the job DTO, copying SDF at zero weight, or keeping the epsilon cutoff. A new field would widen the hot job contract; copying at zero weight wastes memory bandwidth; the old cutoff made quality a binary authority.
Scalability potential: Weak devices retain MapMagic fallback and near-zero SDF influence; middle, high, and ultra devices spend the same path on stronger terrain hugging and trilinear SDF detail.
Hardware Impact: 0 B/frame and no new NativeArray/GraphicsBuffer. Static effect is a branch threshold removal plus one scalar multiply during payload assembly; build CPU avoided because external `dotnet` PID 40836 was active.

## Decision 97 - Prologue Forced Memory Is Pressure-Owned

Problem: `ReadSurvivalProxyPressurePolicy` calculated continuous survival pressure from `GlobalQualityWeight`, then also set `forcedLowMemory` from `qualityWeight01 <= ForcedMemoryQualityThreshold01`. That created two owners for the same policy and let a direct quality cutoff drive proxy forcing.
Solution: Remove `ForcedMemoryQualityThreshold01`; keep `survivalPressure01 = 1 - SmoothStep01(qualityWeight01)` as the continuous quality contribution and set `forcedLowMemory` only from the resolved `pressure01 >= ForcedMemoryPressureThreshold01`.
Rejected Alternatives: Keeping the threshold for readability, or lowering it to zero. Both preserve a separate quality route that is redundant with the pressure scalar.
Scalability potential: Weak devices still reach high proxy pressure smoothly as quality approaches survival; middle/high/ultra recover continuously through the same pressure curve instead of crossing a hidden boolean cliff.
Hardware Impact: 0 B/frame. Removes one branch and one constant; no allocation, no DataVault lock, no new dependency lookup.

## Decision 98 - Homeostasis Visual Overkill Flag Has One Owner

Problem: `BuildFlags` stamped `VisualOverkillBudgetOpen` from `GlobalQualityWeight >= VisualOverkillFlagQualityThreshold01` while `ApplyVisualOverkillPolicy` separately owned the actual `SystemBit.VisualOverkill` state with system-health hysteresis. That was state drift.
Solution: Delete `VisualOverkillFlagQualityThreshold01`; stamp `VisualOverkillBudgetOpen` only after `ApplyDictatorPressurePolicy` resolves the actual target mask and `SystemBit.VisualOverkill` is present.
Rejected Alternatives: Keeping both routes, or adding a second scalar to the signal. The signal already has a kill-mask route; adding another surface would expand authority without a consumer.
Scalability potential: Weak devices do not receive a misleading overkill flag from raw quality; high/ultra receive it only when the dictator's hysteresis admits the visual-overkill state.
Hardware Impact: 0 B/frame. One bit test after existing mask resolution; no managed allocation, no new signal lane, no DataVault lock.

## Decision 99 - Prologue Low-Tier API Is Compatibility Only

Problem: `IPrologueSequenceRuntime` still exposed `IsLowTier`, even though the active director route already consumes `SurvivalProxyPressure01`. The stale name invites future binary-quality coupling.
Solution: Add `IsSurvivalProxySurfaceActive` and implement legacy `IsLowTier` as an `[Obsolete]` alias to that property. Keep the interface member to avoid a public contract break during a multi-agent batch.
Rejected Alternatives: Deleting `IsLowTier` immediately, or leaving it undocumented. Deletion risks compile walls in unseen branches; leaving it active preserves the wrong mental model.
Scalability potential: Weak, middle, high, and ultra devices all consume pressure semantics; the compatibility alias no longer names the preferred route.
Hardware Impact: 0 B/frame. No caller changed; no allocation, no lock, no build launched.

## Decision 100 - Core Verification Without Build

Problem: Core/prologue policy changed, but the user forbids build spam and source-local checks were sufficient for the touched edits.
Solution: Run restricted residue scan, hot-method lookup scan, method-level DataVault write-lock scan, restricted `git diff --check`, and compiler process check. Do not launch `dotnet build`, MSBuild, Unity tests, JSON reports, or binary dumps.
Rejected Alternatives: Running a full build for scalar/bit-route edits; whole-repo heavy AST scan after the earlier broad scan timeout; claiming project-wide lock proof beyond touched methods.
Scalability potential: Source gates now reject regression to the removed quality thresholds and `_runtime.IsLowTier` active route.
Hardware Impact: Compile CPU avoided. Process check found no compiler processes after verification; this agent still launched zero builds.

## Decision 101 - Vehicle SubOS RT Format Is Capability-Owned

Problem: `VehicleSubOsCockpitRuntime.ResolveUiRenderTextureFormat(float qualityWeight01)` selected `ARGB32` or `RGB565` from `ResolveCheapVisualWeight`, turning GlobalQualityWeight into render target identity. That is a binary quality fork and can trigger resource churn at a visual scalar boundary.
Solution: Replace the resolver with `ResolvePanelRenderTextureFormat()`, fed only by `_supportsRgb565RenderTextureCold` cached in `CacheGraphicsCapabilitiesCold`. GlobalQualityWeight now affects RT width/height through `ResolveQualityDimension` only.
Rejected Alternatives: Always forcing `ARGB32` because it is simple; preserving the old quality cutoff; adding a tier enum. Always-ARGB32 violates low-end VRAM pressure, the cutoff violates scalar quality, and a tier enum reintroduces binary policy.
Scalability potential: Weak devices use RGB565 when the GPU supports it and smaller continuous RT dimensions; middle/high/ultra increase resolution through the same scalar without changing route ownership.
Hardware Impact: 0 B/frame. No new allocation or registry lookup; low-end VRAM can stay at 2 bytes/pixel for panel RTs instead of 4 bytes/pixel.

## Decision 102 - Vehicle SubOS External Feed Is Continuous

Problem: External feed release/status/texture selection used `_externalFeedWeight01 <= 0.0001f`, and the weight itself was zero below `ExternalFeedEnableThreshold`. A requested user-facing camera could degrade to static noise solely because quality crossed a threshold, and active external RT dimensions were not revalidated when quality changed.
Solution: Replace the threshold with `MinExternalFeedBlendWeight` and a smooth lerp to 1.0. Requested external feed now acquires the camera RT independent of quality, while `EnsureExternalRenderTextureCurrent` verifies width, height, format, and target binding after quality changes.
Rejected Alternatives: Keeping static-only external feed below the threshold; always rendering full external resolution; leaving external RT resize to lever toggles. The first preserves a binary quality route, the second wastes weak hardware, and the third leaves stale resource state.
Scalability potential: Weak devices render the external feed at minimum dimensions and RGB565 capability with low blend; middle/high/ultra scale resolution and blend continuously without a route cliff.
Hardware Impact: 0 B/frame managed allocation. Resource mutation is restricted to the existing late-frame/presentation resource path; no new DataVault lock and no hot registry lookup.

## Decision 103 - Vehicle SubOS Verification Without Build

Problem: The SubOS patch touched diegetic UI resource policy while an external `dotnet` process was active and build spam is forbidden.
Solution: Run targeted method-body scans for format and external-feed bodies, hot lookup scan for registry/component violations, restricted `git diff --check`, and process check. Add source gates to reject regression.
Rejected Alternatives: Running `dotnet build`; relying on chat proof; scanning the whole repo with a CPU-heavy parser while another compiler process was active.
Scalability potential: Source gates now lock the SubOS resource policy to capability/factual state plus continuous quality dimensions.
Hardware Impact: Compile CPU avoided. External `dotnet` PID 4592 was active; this agent launched no build, MSBuild, Unity runner, JSON report, or binary dump.

## Decision 104 - Diegetic Panel Format Is Hardware Policy

Problem: `DiegeticPanelController.ResolveColorGraphicsFormat` used `_isMx350Tier && _qualityWeight01 < 0.72f` to switch between `B5G6R5` and `R8G8B8A8`. That made GlobalQualityWeight change render target identity and contradicted the serialized MX350 policy tooltip.
Solution: Make `_isMx350Tier` the sole format owner: MX350 policy uses `GraphicsFormat.B5G6R5_UNormPack16`, non-MX350 uses `GraphicsFormat.R8G8B8A8_UNorm`. RT resolution remains the continuous quality/distance route.
Rejected Alternatives: Keeping the 0.72 threshold; forcing RGB565 on all hardware; adding a new tier enum. The first is a binary quality fork, the second suppresses high-end panel fidelity, and the third adds policy surface without a new fact owner.
Scalability potential: Weak devices keep stable low-VRAM panel format while middle/high/ultra retain RGBA8 and scale visual richness through resolution and phosphor effect.
Hardware Impact: 0 B/frame. Prevents format churn at quality changes; no allocation, no new lock, no registry lookup.

## Decision 105 - Diegetic Panel Verification Without Build

Problem: Panel format policy changed, but only a single resolver and source gate were touched while external compiler load existed.
Solution: Run method-body gate for `ResolveColorGraphicsFormat`, check `TryGetComponent` matches are confined to cold serialized-reference resolution, run direct lock scan and restricted `git diff --check`. Do not build.
Rejected Alternatives: Running `dotnet build`; claiming all `TryGetComponent` matches are hot violations without method context; sweeping unrelated UI refactors.
Scalability potential: Source gate rejects reintroducing `_qualityWeight01 < 0.72f` in the format resolver.
Hardware Impact: Compile CPU avoided; runtime allocation delta remains 0 B/frame.

## Decision 106 - Light Shaft Quality Pressure Is Q8 Telemetry, Not A Flag

Problem: `ScreenSpaceLightShaftRuntime.LateFrameTick` encoded `_qualityPressure01 > 0.001f` into `TelemetryFlagQualityPressure`. That converted a continuous quality pressure scalar into a binary blackbox bit and mixed resource/quality pressure with factual fault flags.
Solution: Remove `TelemetryFlagQualityPressure`; reuse the explicit-layout padding byte at offset 33 as `QualityPressureQ8`; write it from `RecordTelemetry` through `EncodeQualityPressureQ8`. Keep `TelemetryFlagLoadShed`, `TelemetryFlagNoCamera`, and `TelemetryFlagNaN` as factual runtime flags.
Rejected Alternatives: Keeping bit 0 reserved as a quality marker; growing the 64-byte telemetry struct; writing a managed diagnostic sidecar. Bit 0 was a quality cliff, stride growth risks native ABI drift, and sidecars violate the no-report mandate.
Scalability potential: Weak devices still report high pressure as a scalar; middle, high, and ultra report lower pressure without changing telemetry schema or fault semantics. Shader quality already consumes `_qualityPressure01` continuously.
Hardware Impact: 0 B/frame. One existing byte is reused; no allocation, no new DataVault lock, no registry lookup, no build launched.

## Decision 107 - Light Shaft Verification Without Build

Problem: The light-shaft patch touched late-frame presentation and blackbox serialization while compilation throttling remains active.
Solution: Run source method-body gates for `LateFrameTick`, `RecordTelemetry`, `DumpBlackbox`, and `EncodeQualityPressureQ8`; scan touched hot methods for registry/component lookups; inspect mutation guard release paths; run restricted `git diff --check`; check compiler processes. Do not launch build, tests, JSON reports, or binary dumps.
Rejected Alternatives: Running `dotnet build`; dumping sample telemetry; asserting lock safety without checking the handed-off guard release.
Scalability potential: Source gate now rejects regression to a binary light-shaft quality pressure flag and requires continuous Q8 blackbox state.
Hardware Impact: Compile CPU avoided. Process check found no compiler process, but source-local verification was sufficient for the padding-byte telemetry change.

## Decision 108 - Wrist HUD Math LOD Pressure Is State Scalar

Problem: `WristHologramHudRuntime.WristHudBuildJob.Execute` set `StateFlagSurvivalMath` when `mathLodPressure01 >= 0.75f`. That made continuous math-LOD pressure a binary state/telemetry flag inside a high-frequency UI job.
Solution: Remove `StateFlagSurvivalMath`; reuse the explicit-layout padding at offset 236 as `MathLodPressureQ8`; encode pressure through `EncodeUnitWeightQ8` in both `SeedInitialState` and Burst `Execute`. Keep factual flags for culling, PDA, NaN, CSV, legacy, and GPU faults.
Rejected Alternatives: Leaving the flag for blackbox readability; adding a new DTO field beyond the 248-byte state stride; pushing a tier enum. The flag is a quality cliff, stride growth risks GPU/native contract drift, and a tier enum violates scalar quality policy.
Scalability potential: Weak devices publish high math pressure as a scalar while reducing visual budget continuously; middle, high, and ultra devices report lower pressure and buy richer glyph/radar presentation without changing state identity.
Hardware Impact: 0 B/frame. One existing int-sized padding slot is reused; no allocation, no registry lookup, no DataVault lock in `Execute`.

## Decision 109 - Wrist HUD Verification Without Build

Problem: The Wrist patch touched a Burst job and an explicit-layout UI state DTO; a full build is still not justified under compilation throttling.
Solution: Run method-body gates for `Execute`, `SeedInitialState`, and `EncodeUnitWeightQ8`; run explicit-layout string gate for `Size = 248` and offset 236; scan touched hot methods for registry/component lookups; prove `SeedInitialState` releases its single write lock in `finally`; run restricted `git diff --check` and compiler process check.
Rejected Alternatives: Launching `dotnet build`; attempting runtime marshal reflection from PowerShell against Unity C# types; changing telemetry entry stride.
Scalability potential: Source gate now rejects reintroducing the `0.75` survival-math threshold and requires the scalar route.
Hardware Impact: Compile CPU avoided. One incorrect PowerShell marshal probe failed without disk mutation; the corrected static layout gate passed.

## Decision 110 - Biomass Impact Drain Uses One Short Mutation Guard

Problem: `EcosystemDirector.ApplyPendingBiomassImpacts()` and `ClearBiomassRuntimeState()` could hold a biomass sector/macro mutation guard and then acquire `_pendingBiomassImpacts` as a separate DataVault write lock. That violates the lock-flattening rule and creates fail-closed behavior on the second ownership attempt.
Solution: Add `BiomassImpactDrainMutationGuardMask`, composed from the existing macro/biomass guard plus `EcosystemPendingBiomassImpacts`. The pending drain and biomass clear paths now acquire that one short guard, resolve the pending queue directly under the guard, and release in `finally`.
Rejected Alternatives: Adding `EcosystemPendingBiomassImpacts` to long-lived `SectorSolveMutationGuardMask` or `MacroSwarmTravelMutationGuardMask` was rejected because queued biomass events must remain writable while solve/travel jobs are scheduled. Keeping a pending write lock under the macro guard was rejected because `GlobalDataVault` intentionally fails closed on nested writer ownership.
Scalability potential: Weak devices keep the queued/deferred biomass fake stable under heavy jobs; middle/high/ultra devices keep the same queue semantics while avoiding lock-order drift. Low/Middle/High/Ultra route: queue during active jobs, drain through one owner guard after jobs settle, no fidelity fork.
Hardware Impact: 0 B/frame. No new allocation, no new job, no registry lookup. Static value is removal of one nested DataVault ownership attempt in the drain/clear paths.

## Decision 111 - Pending Biomass Drain Runs After Sector Guard Release

Problem: `CompleteScheduledSolve()` drained pending biomass impacts before releasing `SectorSolveMutationGuardMask`. If the pending queue were folded into the sector guard, runtime event queue writes would be blocked during scheduled jobs; if left separate, the drain stacked ownership.
Solution: Split completion into two phases: swap sector/biomass front/back views and mark `_solveScheduled = false` inside the sector guard, release the sector guard in `finally`, then call `ApplyPendingBiomassImpacts()` and publish biomass telemetry/starvation state.
Rejected Alternatives: Draining before unlock; delaying drain to another frame; broad DataVault changes. Before-unlock stacks/widens locks, delayed drain changes gameplay/event timing, and DataVault core edits are unnecessary for a local route bug.
Scalability potential: Weak devices can queue biomass impacts while long solves are active and drain deterministically once the job has settled. Middle/high/ultra devices keep the same ordering with no extra route or DTO changes.
Hardware Impact: 0 B/frame. The phase split is scalar state only; `git diff --check` passed and an external `dotnet build .\Hecton8.slnx` was already running, so no build was launched by this agent.

## Decision 112 - UberNoir Stress Shed Is A Scalar, Not A Latch Bit

Problem: `HectonUberNoirRuntimeBridge` used a latched `bool stressShed` and `if (stressShed)` to clamp high-cost presentation. That made a continuous stress/quality route depend on a binary phase latch in LateFrame.
Solution: Replace the latch with `_stressShedWeight01`, a smooth target with bounded per-frame release. High-cost and visual-overkill weights now damp through `math.lerp` using that scalar. The feature mask now represents route availability; telemetry floats carry actual intensity.
Rejected Alternatives: Keeping the latch for hysteresis, or encoding stress shed as a feature bit. The latch was the branch route being removed; the bit was redundant with `SystemStress01` and `HighCostAllowed01`.
Scalability potential: Weak devices shed presentation gradually under stress; middle/high/ultra keep the same shader route and scale extra detail through floats rather than feature-bit cliffs.
Hardware Impact: 0 B/frame. No allocation, no registry lookup, no new DataVault lock; one LateFrame branch route removed.

## Decision 113 - Shader Dispatcher Fallback Overkill Has No 0.78 Gate

Problem: `GlobalShaderDispatcher.ResolveResolutionState()` generated fallback visual overkill with `(quality - 0.78)`, producing zero overkill across most of the scalar range when no DRS state was available.
Solution: Add `ResolveVisualOverkillWeight01(float)` with cubic-biased continuous scaling from 0 to 1 and use it for both resolution-scaler fallback and no-scaler fallback.
Rejected Alternatives: Keeping the high threshold or copying DRS state code into the dispatcher. The first is a quality cliff; the second duplicates policy ownership.
Scalability potential: Weak devices receive near-zero extra detail without a branch; middle tiers can receive small visual enrichment; high/ultra devices reach visual overkill smoothly.
Hardware Impact: 0 B/frame. Scalar math only; no CBuffer, DTO, or lock change.

## Decision 114 - Content Feature Mask Is Route Availability

Problem: `ContentTieredGroupPolicy.ResolveVisualFeatureMaskForWeight` set five feature bits through `visualWeight01 >=` thresholds. That made public content budget metadata jump at fixed quality values.
Solution: Keep the mask as stable route availability and use the previously reserved bytes in `ContentVisualFeatureBudget` for Q8 weights: visual feature, POM, silt wake, hull dents, and salt crystals. Existing capacity fields remain continuous.
Rejected Alternatives: Growing the DTO, keeping threshold bits, or removing `FeatureMask`. DTO growth risks native layout drift; threshold bits preserve cliffs; removing the mask breaks public API callers.
Scalability potential: Weak devices see routes present but weights/capacities near minimum; middle/high/ultra scale feature intensity and capacity continuously.
Hardware Impact: 0 B/frame. The 16-byte layout is preserved; no managed allocation, no new asset load, no hot registry lookup.

## Decision 115 - Rendering/Content Verification Without Build

Problem: The patch touched rendering/content policy while an external `dotnet` process was active, and compilation throttling forbids build spam.
Solution: Run targeted residue scans, method-body hot lookup scans, and restricted `git diff --check`; add editor source gates for the removed overkill/feature thresholds and stress latch route. Do not run build or tests.
Rejected Alternatives: Launching `dotnet build`; running broad CPU-heavy parsers; claiming correctness without source gates.
Scalability potential: Regression gates now reject reintroducing the 0.78 overkill cliff, `if (stressShed)`, and content `visualWeight01 >=` feature thresholds.
Hardware Impact: Compile CPU avoided. Process check observed external `dotnet` PID 53404; this agent launched no build, MSBuild, Unity runner, JSON report, or binary dump.

## Decision 116 - UberNoir Layout Gate Follows Runtime Owner

Problem: `DrsContractsEditTests` still asserted a 48-byte `UberNoirShaderTelemetryEntry`, while the runtime source declares and uses a 64-byte explicit-layout entry with fields through offset 44 plus padding through 63.
Solution: Update the editor proof artifact to assert 64 bytes. Runtime source and offsets are unchanged.
Rejected Alternatives: Shrinking the runtime telemetry entry; ignoring a known stale layout gate. Shrinking would remove existing telemetry fields/padding and risk ABI drift; ignoring it leaves a deterministic editor-test failure.
Scalability potential: Stable 64-byte telemetry keeps scalar quality/high-cost/overkill proof data available across weak, middle, high, and ultra devices without changing runtime behavior.
Hardware Impact: Editor-only proof fix. Runtime allocation delta is 0 B/frame; no build, MSBuild, Unity runner, JSON report, or binary dump was launched.

## Decision 117 - DRS Feature Flags Are Route Availability

Problem: `ThermalDynamicResolutionAdapter.ResolveVisualFeatureFlags` still converted six continuous visual feature weights into binary bits through `weights > VisualFeatureFlagEpsilon`. The shader already receives `_H8VisualFeatureWeights0/1`, so the flags were redundant cliff state.
Solution: Replace per-weight bit assembly with a stable `VisualFeatureRouteMask`. `_H8VisualFeatureFlags` now means the shader route exists; `_H8VisualFeatureWeights0/1` remain the only authority for visual intensity.
Rejected Alternatives: Keeping epsilon presence bits, removing the mask entirely, or widening shader payload. Presence bits preserve binary quality identity; removing the mask can break shader/API route checks; widening payload has no value because the weight vectors already exist.
Scalability potential: Weak devices keep all routes available at near-zero weight; middle, high, and ultra devices ramp salt, silt, dents, POM, subsurface, and raymarched fog continuously.
Hardware Impact: 0 B/frame. Removes six threshold compares and no allocation, DataVault lock, registry lookup, or DTO stride change.

## Decision 118 - UberPost Visual Overkill Uses A Response Curve

Problem: Reconstruction overkill used `Smooth01((quality01 - threshold) / (1 - threshold))`, leaving the visual-overkill contribution at zero until quality crossed a serialized threshold.
Solution: Rename the serialized scalar to `visualOverkillResponse` with `FormerlySerializedAs("visualOverkillThreshold")` and resolve compatibility overkill through `ResolveCompatibilityVisualOverkillWeight01`, a cubic quality response scaled by a continuous response factor.
Rejected Alternatives: Keeping the threshold for inspector familiarity, deleting the serialized field, or changing shader CBuffer layout. The first preserves the cliff, the second loses existing project tuning, and the third is unnecessary.
Scalability potential: Weak devices get a low but nonzero smooth response; middle devices can buy light reconstruction polish; high/ultra devices reach overkill without a route flip.
Hardware Impact: 0 B/frame. Scalar math only; no allocation, no new lock, no hot lookup, and old serialized data migrates.

## Decision 119 - Active Sonar Quality Is Q8 Telemetry

Problem: `WriteActiveSonarGeoTelemetry` stamped `Flags = 1` when `ResolveActiveSonarGeoQualityWeight() <= 0.15f`. That made the blackbox encode a binary hardware-quality state instead of the actual scalar that drove shader globals.
Solution: Reuse explicit-layout byte offset 28 as `QualityWeightQ8`, serialize it at byte 28 in dumps, and keep `Flags` as factual status with `0u` for the current healthy path.
Rejected Alternatives: Growing `ActiveSonarGeoTelemetryEntry`, retaining the flag as compatibility, or writing an external report. Growing risks native layout drift; the flag is the violation; external reports are forbidden and unnecessary.
Scalability potential: Weak devices report low active-sonar quality as a byte, middle/high/ultra report higher fidelity continuously, and analysis can reconstruct the gradient instead of one cutoff.
Hardware Impact: 0 B/frame. The 64-byte ring entry remains unchanged; write lock remains a single DataVault lock released in `finally`.

## Decision 120 - DRS/Visor Verification Without Build

Problem: The patch touched presentation and telemetry code, but the user forbids build spam and the changes are source-local scalar/offset edits.
Solution: Run targeted residue scans for removed threshold patterns, hot lookup scan with method context, active-sonar lock-scope inspection, restricted `git diff --check`, and compiler-process check. Add source gates to prevent regression.
Rejected Alternatives: Launching `dotnet build`; generating JSON/binary proof dumps; relying on verbal proof without source gates.
Scalability potential: Regression gates now enforce route-availability masks, continuous overkill response, and Q8 active-sonar quality across weak, middle, high, and ultra device policy.
Hardware Impact: Compile CPU avoided. Process check found no compiler process; this agent still launched no build, MSBuild, Unity runner, JSON report, or binary dump.

## Decision 121 - Compass Visual Overkill Cannot Mutate Navigation Truth

Problem: `DiegeticGyroCompassRuntime.ScheduleDrift` converted visual-overkill readiness into `FlagIndirectDial`, and `GyroDriftJob.ResolveNoiseValue` used that flag to add another triangle-noise octave to `CurrentHeadingDegrees`. That made global quality/presentation capability affect the compass authority path and exported snapshot flags.
Solution: Remove `FlagIndirectDial` entirely from state writes and job noise. Keep authority drift deterministic from power, anomaly, calibration, and cadence only. Reintroduce the extra richness as `ResolveVisualDialHeading`, a triangle-wave presentation wobble computed inside `ApplyPresentation` after the job completes.
Rejected Alternatives: Passing `VisualOverkillWeight01` into `GyroDriftJob`, keeping the old bool flag as telemetry, or expanding `CompassStateDTO`. The first still changes gameplay truth by quality, the second preserves the violation, and the third breaks native layout without need.
Scalability potential: Weak devices retain cheap deterministic heading; middle/high/ultra get extra dial motion as a visual fake in LateFrame only, scaled by one continuous weight.
Hardware Impact: 0 B/frame. Authority job loses one flag branch and one optional triangle octave; LateFrame adds scalar triangle math only when presentation runs.

## Decision 122 - Compass Indirect Dial Is A Route, Not A Quality Fork

Problem: `ShouldDrawIndirectDial` and `EnsureIndirectBuffersCold` gated indirect rendering and GPU buffer lifetime on `_visualOverkillWeight01 > 0.001f` and a stress threshold, while the serialized field was named `enableIndirectHighTier`.
Solution: Rename the field to `enableIndirectVisualRoute` with `FormerlySerializedAs("enableIndirectHighTier")`; make `ShouldDrawIndirectDial` depend only on authored enable, buffer readiness, mesh/material, and cold graphics capability. Visual amount comes from `ResolveVisualOverkillWeight01` and continuous stress headroom.
Rejected Alternatives: Keeping high-tier names, retaining quality/stress gates, or forcing direct mesh presentation for all tiers. Names encode the wrong policy, gates create binary route churn, and forcing direct presentation throws away a valid capability route.
Scalability potential: Weak devices without capability stay on direct transform rotation; capable low/middle devices can still render the dial route while overkill effects are near-minimal; high/ultra devices spend the scalar budget on wobble, chromatic and particle density.
Hardware Impact: 0 B/frame. No new allocation in hot paths; buffer allocation remains cold and guarded by existing resource checks.

## Decision 123 - Compass Regression Proof Is Source-Gated

Problem: The compass violation crossed simulation, presentation, and tests, so a verbal proof would not prevent the old flag from returning.
Solution: Add `DiegeticGyroCompass_VisualOverkillStaysPresentationOnlyAndContinuous` to the 1427 editor source gates. The gate checks migration, scalar response, LateFrame visual path, capability route body, and absence of the old bool flag/methods in schedule and noise bodies.
Rejected Alternatives: Running `dotnet build` while an external `dotnet` process was active, generating JSON reports, or widening native DTO tests. Build spam violates throttling, JSON reports are rejected, and DTO layout did not change.
Scalability potential: The source gate protects the weak/middle/high/ultra scalar contract from later binary quality patches.
Hardware Impact: Editor-only. Runtime cost is 0 B/frame; verification CPU avoided by static scans and restricted diff checks.

## Decision 124 - DRS Upscaler Identity Is Not Quality Authority

Problem: `ThermalDynamicResolutionAdapter.ResolveUpscalerHash(float qualityWeight01, float renderScale)` selected the upscaler route through `ResolveFsrUpscalerEligibility01(qualityWeight01)`. That made upscaler identity a binary quality fork, while render scale and continuous visual weights already carry the fidelity budget.
Solution: Change `ResolveUpscalerHash` to accept only `renderScale`. Native scale returns `UpscalerNativeHash`; scaled rendering uses the capability-cached bilateral DRS route or BilateralTAA fallback. Quality still controls scale, visual budget, sharpening, and overkill continuously, but not route identity.
Rejected Alternatives: Keeping the FSR eligibility curve, converting it to another continuous threshold, or forcing BilateralTAA on all devices. The first two still encode a route fork; the third wastes first-party compute capability on capable desktop GPUs.
Scalability potential: Weak/mobile devices stay on cheap BilateralTAA when compute route is unavailable; middle/high/ultra devices use the first-party BDRS route whenever cold capability proves it is valid, with intensity governed by continuous scale and visual weights.
Hardware Impact: 0 B/frame. Removes a quality eligibility branch family from hash resolution; no allocation, no DTO/CBuffer layout change, no DataVault access, and no hot registry lookup.

## Decision 125 - Misleading FSR Symbols Removed

Problem: The adapter used FSR-named symbols (`UpscalerFsrTaaHash`, `_coldFsrCapabilityAllowed`, `_fsrUpscalerAllowed`) for a local route hash that maps to the project's bilateral DRS path. That naming invited future integration of a vendor dependency and hid the actual ownership route.
Solution: Rename the route to `UpscalerBilateralDrsHash`, `_coldBilateralDrsRouteAllowed`, and `_bilateralDrsRouteAllowed`. Keep the hash value aligned with the first-party BDRS route and keep capability probing cold through `CacheGraphicsCapabilitySnapshotCold`.
Rejected Alternatives: Adding a direct dependency on the BDRS constants assembly, keeping FSR names for compatibility, or adding a new DTO field. Direct dependency risks assembly cycles, FSR names are false architecture, and DTO growth is unnecessary.
Scalability potential: Low-tier fallback remains predictable; middle/high/ultra devices can use the stronger route without introducing a vendor-specific route contract.
Hardware Impact: 0 B/frame. Route flag is cached; no new allocation, no new process, and no build was launched.

## Decision 126 - DRS Upscaler Verification Stayed Static

Problem: The change touches render policy, but compilation throttling forbids build spam and external compiler state may change independently.
Solution: Add a source gate that rejects the old FSR eligibility symbols and requires render-scale-only hash selection. Run runtime residue scans, phase scans, hot lookup scans, DataVault lock scans, restricted `git diff --check`, and compiler-process inspection.
Rejected Alternatives: Running `dotnet build`, generating a JSON report, or trusting chat-level claims. Build spam violates the batch protocol; JSON reports are rejected; claims without source gates do not survive later agent edits.
Scalability potential: The regression gate protects the weak/middle/high/ultra route split: capability decides route, continuous weights decide fidelity.
Hardware Impact: Compile CPU avoided. Static verification found no runtime residue of the old FSR quality fork; this agent launched no build, MSBuild, Unity runner, JSON report, or binary dump.

## Decision 127 - Bilateral DRS Telemetry Belongs To PostSimulation

Problem: `HectonBilateralDrsUpscalerRuntime.ScheduleOwnerSimulation` calculated pending upscaler params and then immediately wrote the telemetry ring/cursor from the Simulation bridge. It held no nested write lock, but the phase contract says blackbox telemetry belongs after simulation fences, not inside simulation work.
Solution: Store the produced `UpscalerTelemetryEntry` in a private value-type `_pendingTelemetryEntry` and set `_pendingTelemetryEntryValid`. `RunOwnerPostSimulation` now records telemetry first and then publishes pending params. `RunOwnerVisualSync` remains the only GPU upload phase.
Rejected Alternatives: Keeping telemetry writes in Simulation, pushing telemetry to VisualSync, or writing an external report. Simulation write blurs phase ownership; VisualSync is presentation upload, not blackbox ownership; external reports are rejected.
Scalability potential: Weak devices avoid mixed-phase stalls and keep telemetry as a sequential PostSimulation write; middle/high/ultra devices keep the same parameter quality while preserving clean simulation-to-presentation handoff.
Hardware Impact: 0 B/frame. The transfer is one unmanaged struct field copy. No allocation, no new DataVault lock, no registry lookup, and no GPU upload change.

## Decision 128 - Bilateral DRS Fail-Closed Clears Pending Telemetry

Problem: Deferring telemetry creates a one-frame field that could become stale if the runtime fails closed or the vault is replaced before PostSimulation drains it.
Solution: `FailClosedRuntimeRoute` and `ResetVaultSeedState` clear `_pendingTelemetryEntryValid` and reset `_pendingTelemetryEntry` to default. Existing shutdown calls `ResetVaultSeedState`, so the cleanup path remains centralized.
Rejected Alternatives: Clearing only `_simulationPendingPublish`, clearing in `ShutdownServiceState` only, or leaving the field live until overwritten. Those options let stale telemetry survive route failure, vault replacement, or partial shutdown.
Scalability potential: All tiers keep deterministic blackbox ownership with no stale frame bleed across route resets.
Hardware Impact: 0 B/frame. Two scalar assignments on rare fail/reset paths.

## Decision 129 - Bilateral DRS Phase Proof Is Source-Gated

Problem: Phase hygiene can regress by moving `RecordUpscalerTelemetryOneLock` back into `ScheduleOwnerSimulation`, and chat-level claims are not proof.
Solution: Add `BilateralDrsUpscaler_TelemetryWritesAfterSimulation` to the 1427 source gates. The gate checks pending telemetry fields, Simulation method absence of the writer, PostSimulation record-before-publish ordering, VisualSync GPU upload, and fail/reset cleanup.
Rejected Alternatives: Running `dotnet build`, generating JSON proof, or relying on diff review only. Build is throttled; JSON proof is rejected; diff review is not persistent.
Scalability potential: The gate protects low/middle/high/ultra phase split: Simulation calculates, PostSimulation records/publishes, VisualSync uploads.
Hardware Impact: Editor-only. Static verification passed; no build, MSBuild, Unity runner, JSON report, or binary dump was launched.
