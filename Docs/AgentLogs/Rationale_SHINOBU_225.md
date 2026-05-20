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

Scalability potential: Low clamps spark quantity to a small GPU request count. Middle/high raises density smoothly. Ultra spends saved CPU on GPU debris quantity and shader glow, not extra physical particles.

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

Scalability potential: Low uses tiny dent radius and low GPU spark quantity. Middle uses standard glow/decal density. High and Ultra increase radius/lifetime/spark density through continuous weights without changing gameplay damage truth.

Hardware Impact: Avoids estimated 300-3000 us mesh mutation spikes on i3/MX350-class devices. Actual gain remains PENDING PROFILER.

## Decision 006: Editor And Report Scope

Problem: Designers need tuning and static evidence, but runtime UI or manual grep would either cost gameplay frames or fail repeatability.

Solution: Add editor-only UI Toolkit tuner, editor gizmo, and `Cutter_Raycast_Inquisition` static report writer. Runtime remains DataVault/signal based; editor code is isolated in `Editor` folders or `UNITY_EDITOR` guards.

Rejected Alternatives: In-game debug UI was rejected because it adds player-route cost. IMGUI was rejected because the mandate and existing tuner pattern use UI Toolkit. Overwriting another agent's shared construction report was rejected; SHINOBU writes a sidecar report and final log notes the shared file state.

Scalability potential: No runtime cost on any tier. Top-tier devices only gain optional editor visualization density during development.

Hardware Impact: Runtime impact is zero in builds. Editor-only overhead is irrelevant to i3/MX350 gameplay budgets.
