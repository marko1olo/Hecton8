# SHINOBU_225 Rationale

Date: 2026-05-20
Agent: SHINOBU_225
Role: LASER_CUTTER_DOD_REWRITE
Status: PENDING VERIFICATION

## Decision 000: Scope And Mandate Selection

Problem: Laser cutter task crosses tools, physics queries, AUP precision, VFX staging, DataVault/global authority, and telemetry. Editing without narrowing the mandate set creates refactor spread.

Solution: Read and apply eight task-relevant mandates: tools/raycast/heat, ARM64 layout, zero-GC, native memory/jobs, visual fake first, AUP precision, execution phases, and signal lane segregation. Treat `SHINOBU_225` XML block as the only batch directive.

Rejected Alternatives: Reading the full mandate registry would pollute context and violate the 2-8 mandate rule. Relying on memory would miss current R45 global authority boundaries.

Scalability potential: Low/MX350 gets bounded request counts and shader fakes. Middle keeps full deterministic cutter truth at normal cadence. High increases debug/telemetry density only after budget proof. Ultra spends saved CPU on richer GPU sparks/deformation, not more simulation truth.

Hardware Impact: Expected low-end gain comes from removing synchronous physics stalls and prefab burst churn. Exact i3/MX350 gain is PENDING PROFILER; static estimate range is 40-3000 us depending on old call site.

## Decision 001: Product Route Relevance

Problem: A laser cutter rewrite could become isolated tech work with no first-20-minutes relevance.

Solution: Scope it as removal of an equipment-route blocker: cutter interaction against salvage/module surfaces must not stall the Copper Wire/tool route through sync physics or prefab spawn spikes.

Rejected Alternatives: Building broad global systems unrelated to the route. New global authority surfaces remain blocked unless existing source already provides lanes/buffers or the work is owner-local.

Scalability potential: Same gameplay truth across devices, presentation weight scales by `GlobalQualityWeight`.

Hardware Impact: Prevents low-end frame hitch during repeated cutter impacts; exact microseconds PENDING PROFILER.

## Decision 002: Existing Raycast Backend Boundary

Problem: The batch prompt names synchronous cutter `Physics.Raycast` as the threat, but source archaeology shows `LaserCutter.TryGetCutHit` already routes through `IInteractionSignalService.TryRaycastPrimary`, and `EquipmentInteractionHandler` schedules `RaycastCommand.ScheduleBatch` later instead of blocking the cutter frame.

Solution: Keep the existing equipment interaction raycast owner as the live route and add SHINOBU-owned cutter DTO/jobs/tooling around it for deterministic profiling, staged hit evaluation, telemetry, and future dispatcher binding. Do not create a second gameplay raycast dependency in `LaserCutter`.

Rejected Alternatives: Replacing `TryRaycastPrimary` with a parallel static scheduler would duplicate raycasts, double physics query load, and create a race with the interaction owner. Forcing direct `Physics.RaycastNonAlloc` would violate the mandate and regress the already-deferred route.

Scalability potential: Low devices continue using one deferred interaction ray per cutter frame. Middle/high/ultra can run SHINOBU mock and visual-overkill evaluation buffers for richer presentation without changing gameplay truth.

Hardware Impact: Avoids adding an estimated 40-120 us duplicate physics query on i3/MX350-class hardware while preserving the no-blocking raycast route.

## Decision 003: Sparks As Signals, Not ParticleSystem Mutation

Problem: `LaserCutter.UpdateSparks` still moves a serialized `ParticleSystem`, changes emission rate, and calls `Play/Stop` during active cutting. Even without `Instantiate`, this is component-state churn in the tool hot path and does not guarantee GPU-only visual staging.

Solution: Replace the hot spark operation with a `DebrisSpawnSignal`/tool spark signal path using absolute hit AUP and continuous `GlobalQualityWeight` intensity/count scaling. Remove cutter-adjacent `ParticleSystem` references in `LaserCutter`, `SealedDoor`, and `SargassumCutResponder` so focused cutter scans show zero particle-system code.

Rejected Alternatives: Pooling or rate-tuning the existing `ParticleSystem` keeps CPU component writes in the cutter loop. Spawning prefab spark bursts is explicitly forbidden. Keeping inert serialized fields was rejected after static scan proved they still leave cutter-focused ParticleSystem evidence.

Scalability potential: Low quality collapses spark quantity to zero GPU requests. Middle/high raises density smoothly. Ultra spends saved CPU on up to 500 GPU spark/debris requests and shader glow, not extra physical particles.

Hardware Impact: Expected low-end gain is 20-150 us during cutter impact frames by removing transform/emission/Play/Stop calls; exact profiler proof remains pending.

## Decision 004: Owner-Local Buffer IDs

Problem: New cutter DOD buffers need DataVault residency, but extending the global `BufferID` enum is a cross-domain authority change that can collide with parallel agents.

Solution: Use a SHINOBU owner-local buffer ID block cast to `BufferID`, mirroring existing scoped systems such as ballistics and procedural wreckage. Document IDs in contracts and keep them inside the Tools domain.

Rejected Alternatives: Editing `H8Memory.cs` would enlarge global authority for a tool-local feature. Allocating persistent `NativeArray` fields outside DataVault violates the native memory mandate.

Scalability potential: Low/middle/high/ultra share fixed capacities with bounded request/result buffers; only quality scalar and staged visual counts vary.

Hardware Impact: Fixed vault buffers avoid allocator churn. Expected i3/MX350 benefit is stability rather than a single measurable microsecond win.

## Decision 005: Shader Deformation Truth Boundary

Problem: Cutter impact wants visible molten scars and hull dents, but CPU mesh mutation or runtime mesh rebuilds are over budget for an equipment hot path.

Solution: `EvaluateCutterRaycastHitsJob` writes `LaserCutDeformationStateDTO`, `LaserCutGlowDecalRequestDTO`, and `LaserCutImpactVfxDTO` only. The deformation is a shader/decal contract: center AUP, normal, radius, depth, heat, and progress. Geometry truth remains outside the cutter.

Rejected Alternatives: Runtime vertex edits and `RecalculateNormals` are main-thread hazards. Mesh scar prefabs would add object lifetime churn. A binary low/high visual switch was rejected; spark count, glow, and dent radius consume continuous `GlobalQualityWeight`.

Scalability potential: Low uses tiny dent radius and zero-to-low GPU spark quantity. Middle uses standard glow/decal density. High and Ultra increase radius/lifetime/spark density through continuous weights without changing gameplay damage truth.

Hardware Impact: Avoids estimated 300-3000 us mesh mutation spikes on i3/MX350-class devices. Actual gain remains PENDING PROFILER.

## Decision 006: Editor And Report Scope

Problem: Designers need tuning and static evidence, but runtime UI or manual grep would either cost gameplay frames or fail repeatability.

Solution: Add editor-only UI Toolkit tuner, editor gizmo, and `Cutter_Raycast_Inquisition` static report writer. Runtime remains DataVault/signal based; editor code is isolated in `Editor` folders or `UNITY_EDITOR` guards. Write a SHINOBU sidecar report and add a non-destructive appendix field to the shared construction report.

Rejected Alternatives: In-game debug UI was rejected because it adds player-route cost. IMGUI was rejected because the mandate and existing tuner pattern use UI Toolkit. Full rewrite of another agent's shared construction report was rejected; SHINOBU adds only an appendix and keeps the detailed report in a sidecar file.

Scalability potential: No runtime cost on any tier. Top-tier devices only gain optional editor visualization density during development.

Hardware Impact: Runtime impact is zero in builds. Editor-only overhead is irrelevant to i3/MX350 gameplay budgets.

## Decision 007: Request ABI Padding Correction

Problem: The first implementation used offsets 52/56/60 of `LaserCutRequestDTO` for Frame, Flags, and RequestSequence. The XML contract explicitly reserves bytes 52-63 as padding. That made the ABI technically 64 bytes but semantically wrong for blind MemCpy/layout proof.

Solution: Restore `LaserCutRequestDTO` to the exact mandated shape: `double3 RayOriginAUP@0`, `float3 RayDirection@24`, `float CuttingPower@36`, `float MaximumDistance@40`, `uint ToolHashID@44`, `uint ParentEntityID@48`, and explicit `_pad0/_pad1/_pad2` at 52/56/60. Move frame/flags/sequence/cooldown metadata into a separate 64-byte `LaserCutRequestMetaDTO` stored in owner-local `RequestMetaBuffer=71336`.

Rejected Alternatives: Keeping metadata inside padding was rejected because it violates the exact batch ABI. Shrinking metadata to a 16-byte row was rejected because a 64-byte row avoids false-sharing ambiguity and keeps request/meta lanes cache-line regular under parallel writes.

Scalability potential: Low devices get predictable cache-line fetches and no unaligned metadata reads. Middle/high/ultra keep the same truth buffer but can use the meta lane for richer telemetry/cooldown proofs without touching the request ABI.

Hardware Impact: Expected i3/MX350 gain is structural, not a measured frame win: fewer cache-line surprises and exact ARM64-aligned row loads. Profiler proof remains pending behind CPU/build guard.

## Decision 008: Hot Resolve Only, Cold Acquire Only

Problem: Live cutter staging and scheduled raycast setup could still fall back to `GlobalRegistry.DataVault` or acquire Vault handles if bootstrap was missed. That is a hidden hot-path authority lookup/allocation hazard.

Solution: Keep `EnsureInitialized()` as the cold bootstrap/acquire gate. Live `QueueLiveRequest`, `TryScheduleRaycastBatch` after bootstrap, evaluation finalization, and GPU VFX staging now resolve already-acquired handles with `allowAcquire:false`. If the Vault route is not booted, the hot path fails closed instead of polling GlobalRegistry or calling `GetGenerationHandle`.

Rejected Alternatives: Reacquiring handles opportunistically during tool fire was rejected because it hides boot failures inside gameplay. Caching private `NativeArray` aliases was rejected because Vault sovereignty requires generation handles plus method-local resolved views.

Scalability potential: Low devices avoid metadata traffic during repeated trigger frames. Middle/high/ultra keep the same route; extra visual density comes from cached quality weights, not additional authority lookups.

Hardware Impact: Expected low-end gain is removal of unpredictable metadata/registry stalls in the cutter path; exact microseconds remain PENDING PROFILER.

## Decision 009: Door VFX Decoupling

Problem: `SealedDoor` temporarily called `LaserCutterDodRuntime.StageGpuSparkSignal`, creating a Gameplay -> Tools source dependency for a generic door spark effect.

Solution: Replace that call with a local `DebrisSpawnSignal` publisher using door-local species hashes and continuous `HomeostasisBrain.GlobalQualityWeight` quantity scaling. Tools owns cutter DTOs; Gameplay door owns its prop VFX signal.

Rejected Alternatives: Keeping the Tools call was rejected as cross-domain coupling. Reintroducing ParticleSystem sparks was rejected by Task 02.

Scalability potential: Low collapses door spark quantity toward zero; middle/high/ultra increase GPU spark/debris quantity continuously without a binary quality branch.

Hardware Impact: Keeps door-cut feedback on the signal/GPU path and avoids direct tool-runtime coupling. Microsecond proof pending.

## Decision 010: Exact Spark Continuum And Live Tuning Consumption

Problem: Task 11 explicitly requires GPU spark presentation to scale from 0 on minimum quality to 500 on Ultra, and the Editor Facade must tune the actual Burst evaluation math. A fixed 8..128 constant range and hardcoded dent/glow/battery values leave part of the mandate decorative.

Solution: Set `LowSparkCount=0`, `UltraSparkCount=500`, feed tuning `LowSparkCount`, `UltraSparkCount`, `SparkIntensityScale`, `DentRadiusMinMeters`, `DentRadiusMaxMeters`, `GlowLifetimeSeconds`, and `BatteryWattsAtPowerOne` into `EvaluateCutterRaycastHitsJob`, and smooth presentation with `math.smoothstep(GlobalQualityWeight)`.

Rejected Alternatives: Keeping the old 128 cap was rejected because it fails the XML ceiling. Applying tuning only in the editor was rejected because designers would see controls that do not affect the runtime job. Binary tier thresholds were rejected because HECTON requires continuous quality shedding.

Scalability potential: Low collapses spark quantity to zero and keeps shader deformation at minimal radius. Middle raises density and lifetime gradually. High/Ultra spends saved CPU on up to 500 GPU-only spark requests, larger shader dent radius, longer glow, and richer decal presentation without changing simulation truth.

Hardware Impact: Low-end silicon avoids visual request pressure under thermal clamp; Ultra routes visual overkill to GPU lanes. Exact i3/MX350 microseconds remain PENDING PROFILER.

## Decision 011: Telemetry Tail Converted To Burst Work Estimate

Problem: The black-box ring recorded battery watts but did not expose even a deterministic proxy for Burst work intensity, leaving Task 15 weaker than the forensic mandate.

Solution: Replace the final telemetry tail reserve at byte 124 with `BurstWorkEstimateMicros`, derived inside `EvaluateCutterRaycastHitsJob` from hit state, quality curve, and spark count. The UI Toolkit tuner now displays the estimate as `Burst us`.

Rejected Alternatives: Measuring wall-clock time inside Burst was rejected because the job has no deterministic timer and such timing would break rollback consistency. Editor-only profiler markers were rejected as the sole black-box source because dumps must survive runtime fault capture.

Scalability potential: Low estimates shrink with zero spark count; Ultra estimates rise with 500-count visual pressure, giving QA a stable proxy for quality-weight stress.

Hardware Impact: No extra allocation and one uint write per telemetry row. Profiler proof remains pending.

## Decision 012: Preserve Job-Computed Spark Count At Signal Boundary

Problem: `EvaluateCutterRaycastHitsJob` computed tuned `SparkCount`, but `PublishImpactSignals` routed the DTO through the live staging helper, which recalculated quantity from cached quality and intensity. That silently discarded tuning `SparkIntensityScale` and the exact 0..500 job result.

Solution: Split signal publication into `PublishGpuSparkSignals`. Live `StageGpuSparkSignal` computes direct tool presentation quantity from the same no-acquire Vault tuning fields (`LowSparkCount`, `UltraSparkCount`, `SparkIntensityScale`) and stages an impact DTO. Post-evaluation publishing now forwards `LaserCutImpactVfxDTO.SparkCount` directly to `DebrisSpawnSignal.Quantity` and disables VFX-buffer restaging.

Rejected Alternatives: Leaving the recalculation was rejected because it makes the Burst DTO a lie. Adding another DTO just for signal publication was rejected because the existing impact VFX row already carries the required scalar.

Scalability potential: Low remains zero requests. Middle/high/ultra preserve designer-authored spark scale and the full 500-count Ultra ceiling through the actual signal boundary.

Hardware Impact: Removes one redundant VFX-buffer write per published impact and keeps signal emission deterministic from the completed Burst row. Exact microseconds remain PENDING PROFILER.
