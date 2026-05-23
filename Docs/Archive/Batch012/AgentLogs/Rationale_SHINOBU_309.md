# Rationale_SHINOBU_309

Status: PENDING LOOP21 CORE COMPILE GATE / UNITY IMPORT / PLAYMODE / PROFILER PROOF; PRIOR CORE COMPILE GREEN

## Decision 01 - Authority Shape

Problem: Nutrient drift needs to influence ecosystem behavior without particle actors, scene transforms, or another environment authority.
Solution: Create `Hecton8.Ecosystem.NutrientDriftRuntime` as a Vault-backed scalar-field owner with explicit public read/write snapshot APIs. Use `GlobalRegistry` only for cold service resolution and hot-swap repair; no scene search or hot polling.
Rejected Alternatives: Partial injection into player movement, atmosphere, or a non-existent `HectonFluidDynamicsRuntime` would mix authority and create merge contention.
Scalability potential: Low = 16^3 active cells and nearest-heavy interpolation; Middle = 20-24^3; High = 28-32^3; Ultra = denser visual upload/telemetry use, not bigger gameplay truth.
Hardware Impact: i3/MX350 avoids Transform iteration, ParticleSystem collision readback, and managed spawn/despawn churn. Expected replacement gain is entire-object overhead removal; scalar field cost remains bounded and cache-linear.

## Decision 02 - Vault IDs

Problem: Nutrient state needs double buffers, flow, sources, tuning, telemetry, upload, CSV, and fault lanes without colliding with neighboring ecosystem work.
Solution: Allocate contiguous SHINOBU_309 buffer IDs `70460..70473` after already-present ecosystem/flocking lanes and document them in `NutrientDriftSelfAudit`.
Rejected Alternatives: Reusing ToxicOutgassing hard-coded `70800..70823` would collide with audio lanes; runtime-created anonymous buffers would hide ownership.
Scalability potential: Same IDs serve Low through Ultra; active axis and cadence scale continuously inside fixed capacity.
Hardware Impact: Fixed handles avoid late allocation/growth and keep buffer ownership visible to DataVault diagnostics.

## Decision 03 - Mock Flow Field

Problem: The prompt references `AbyssalFlowField`; source archaeology found an existing read-only 3D abyssal flow-volume route, but the nutrient runtime must not cache the concrete World owner that implements it.
Solution: Use cached `IAbyssalFlowVolumeReadModel.TryGetAbyssalFlowVolumePayload` when available and copy/sample it into the nutrient Vault flow buffer `70462` through `CopyAbyssalFlowVolumeToNutrientFlowJob`. Keep `GenerateMockFlowFieldJob` as deterministic emergency fallback only.
Rejected Alternatives: Blocking on Agent 105 or inventing a dependency on absent code would create a compile wall. Retaining a concrete World owner field would violate the contract-route polish. Scene vector GameObjects would violate the no-particle/no-transform mandate.
Scalability potential: Low samples fewer active cells; Middle/High/Ultra use the same route with higher active cells and richer presentation.
Hardware Impact: Real flow-volume path is one trilinear Burst pass over active cells; mock path is cheaper. Estimated 35-70 us at 16^3 mock and 120-300 us at 32^3 flow-volume sampling on i3/MX350 class CPU.

## Decision 04 - Semi-Lagrangian Solver

Problem: Biomass density must drift through water predictably without simulating particles or micro-physics.
Solution: Reverse-sample the previous density field by velocity * timestep, toroidal-wrap the sample, blend nearest/trilinear interpolation by `GlobalQualityWeight`, then apply scalar decay and source injection.
Rejected Alternatives: Forward splatting needs atomics or scatter conflicts; particle transport needs transforms/collisions; NavMesh/Physics has no ownership here.
Scalability potential: Low = nearest-dominant blend and coarse axis; Middle = partial trilinear; High = full trilinear and denser axis; Ultra = visual overkill via shader sampling rather than more gameplay authority.
Hardware Impact: Linear pointer traversal, no atomics, no allocations. i3/MX350 cost target stays below 0.5 ms per FrostTick-equivalent solve for bounded grids; high-end saved cycles buy richer fog/biolume sampling.

## Decision 05 - Thermal Vent Injection

Problem: Vent nutrient injection needs AUP precision over large world coordinates without making the nutrient runtime depend on the concrete World registry owner.
Solution: Read bounded active thermal vent snapshots through cached `INutrientThermalVentReadModel`, store AUP as `double3`, subtract grid origin in double precision inside Burst, then cast only the local delta to `float3` for grid indexing.
Rejected Alternatives: Absolute float world positions lose precision at large map extents; a new vent signal is unjustified because the existing World owner already owns active vent state; retaining the concrete registry field in Ecosystem widens the compile wall.
Scalability potential: Source capacity fixed at 16; radius/injection multiplier scale continuously across Low/Middle/High/Ultra.
Hardware Impact: Bounded source loop inside each cell pass; no queue flush, no listener fan-out, no GameObject source markers.

## Decision 06 - Visual Lie

Problem: Filter-feeder attraction needs a visually/sampleable biomass field but must not own gameplay truth through render objects.
Solution: Publish normalized density to a single RFloat `Texture3D` and shader globals after the job fence completes. Upload cadence is continuous through `GlobalQualityWeight`.
Rejected Alternatives: Plankton mesh/particle clouds would return to object simulation. CPU raymarch or per-cell debug renderers would exceed the frame-time doctrine.
Scalability potential: Low = sparse upload cadence; Middle = moderate cadence; High = every frame around Frost solve; Ultra = downstream shaders can over-sample the same texture for visual richness.
Hardware Impact: CPU upload is bounded by 32^3 floats. Cheap GPUs get coarse but stable density; high-end GPUs get visual overkill without changing solver authority.

## Decision 07 - Telemetry And Black Box

Problem: The system needs crash/fault evidence and must not answer "unknown crash".
Solution: Record last 300 high-level entries in `FluidGridTelemetryEntry` ring, dump raw rows with a fixed header to `Docs/AgentLogs/Dump_SHINOBU_309.bin` on NaN or over-budget detection.
Rejected Alternatives: Managed log strings per frame allocate and lose pre-crash state; profiler-only proof is unavailable in automated batch.
Scalability potential: Telemetry capacity is fixed across all tiers; only optional editor graph usage scales.
Hardware Impact: 300 * 64 bytes resident; telemetry write is estimated 8-20 us and fault dump is off normal path.

## Decision 08 - CSV Profiles

Problem: Designers need nutrient profile ingestion without managed per-row objects or runtime CSV parsing churn.
Solution: Cold reload `nutrient_drift_profiles.csv` through Vault byte scratch `70471`, parse `ReadOnlySpan<byte>`, and store unmanaged `NutrientProfileDTO` rows in `70472`.
Rejected Alternatives: LINQ, `string.Split`, Newtonsoft, or managed profile lists would allocate and violate hot-path policy.
Scalability potential: Low through Ultra use the same compact rows; future profile count changes are capacity-bound, not heap-bound.
Hardware Impact: Runtime hot path pays 0 us for CSV; cold load is bounded by 16 KB scratch.

## Decision 09 - Snapshot Readiness Bug Fix

Problem: `ReadSnapshotReady` originally sanitized tuning before checking flags; sanitization sets initialized flags, which could accept an uninitialized Vault row.
Solution: Evaluate raw `Flags` first, then sanitize only the returned DTO.
Rejected Alternatives: Trusting `EnsureVaultState` alone leaves a false-ready edge case during hot-swap or editor reads.
Scalability potential: Same correctness across tiers.
Hardware Impact: Negligible branch cost; prevents undefined reads from uninitialized memory.

## Decision 10 - Compile Gate

Problem: Source changed, but project rule forbids launching dotnet when another dotnet/csc process is running.
Solution: Checked CPU and compiler processes repeatedly. First gate was CPU 30% with `dotnet.exe` PID 6776 running Unity `VBCSCompiler.dll`; later gate was CPU 62% with `dotnet.exe` PID 5544 active; final gate was CPU 97% with `dotnet.exe` PIDs 3104 and 12624 active. No build launched. Used static checks instead: JSON parse, brace balance, targeted scanner, `git diff --check`.
Rejected Alternatives: Running `dotnet build` with an active compiler server would violate the explicit batch rule.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoids contention with existing compiler process and respects shared-agent workspace limits.

## Decision 11 - Hot Route Purity Polish

Problem: The first pass still had helper-path impurity: flow payload resolution repaired `_vegetationBridge` through `GlobalRegistry.MapMagicVegetation` during `FrostTick`, CSV profile reload checked the filesystem from `FrostTick`, and grid origin read a concrete `HectonPlayerMovement`.
Solution: Make `TryReadAbyssalFlowPayload` consume only the cached bridge set by cold activation or hot-swap listener; move CSV ingestion to cold Vault initialization plus explicit editor reload; read player origin from `IPlayerRuntimeContext.TryGetMovementRuntimeState` and `PredictedAup`.
Rejected Alternatives: Lazy per-tick self-healing and filesystem timestamp polling were simpler but violate the registry/zero-GC hot-path doctrine.
Scalability potential: Low through Ultra use the same authority route; quality still changes active axis/interpolation/upload cadence only.
Hardware Impact: Removes managed filesystem work from gameplay cadence and narrows compile-wall coupling. i3/MX350 gains are small per tick but remove unpredictable IO spikes.

## Decision 12 - Editor Diagnostic GC Correction

Problem: The SceneView grid slice gizmo first allocated a fresh four-corner `Vector3[]` for every drawn cell, then still retained one private managed corner array.
Solution: Remove the array route entirely; draw the slice cells with `Handles.DrawSolidDisc` plus `DrawWireDisc` from scalar density samples.
Rejected Alternatives: Debug cubes, per-cell GameObjects, per-cell `new Vector3[]`, or a private managed corner cache would hide solver truth behind object churn or weaken H-Phi claims.
Scalability potential: Low draws coarse slices; Middle/High/Ultra can raise slice density without multiplying allocations.
Hardware Impact: Removes editor heap pressure/private array ownership while inspecting the field; no player runtime impact.

## Decision 13 - Shared Report And Compile Wall

Problem: The editor scanner initially wrote a single-agent JSON object and would erase neighboring report sections if run. After CPU/compiler gates opened, narrow Core compile failed before SHINOBU_309 proof because unrelated SHINOBU_306 fauna genetics DTO types are missing from `FaunaGenome64.cs`/`EcosystemDirector.cs`.
Solution: Convert `Fluid_Particle_Scanner` to upsert only `shinobu_309_plankton_nutrient_flow_drift` into `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`, document the SHINOBU_309 Vault ABI in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and mark compile as externally blocked after one guarded build attempt.
Rejected Alternatives: Overwriting the shared report is hostile to parallel agents; fixing SHINOBU_306 DTO ownership from this agent would cross the assigned domain boundary.
Scalability potential: Tooling/doc-only; runtime scalability unchanged.
Hardware Impact: No runtime impact. Build attempt used `--no-restore` and `-maxcpucount:1` after CPU sampled 25.6% with no active compiler process.

## Decision 14 - Fluid Particle Scanner AST Tightening

Problem: The first scanner pass used source-line substring matching. That could count comments/strings as code and did not meet the prompt's AST wording.
Solution: Use existing editor Roslyn dependencies to parse each scoped C# file with `CSharpSyntaxTree`, walk syntax nodes, classify `ParticleSystem`/collision/Rigidbody references from identifiers/member accesses/method declarations, and report parser failures separately from active nutrient-authority hits.
Rejected Alternatives: Adding a new package would touch project dependency surface; retaining line scanning was weaker evidence and vulnerable to false positives.
Scalability potential: Tooling-only. Runtime scalability remains Low 16^3 nearest-heavy, Middle partial trilinear, High 28-32^3, Ultra shader over-sampling from the same scalar texture.
Hardware Impact: Runtime 0 us. Editor scanner cost is bounded to 64 scoped source files in the current tree and does not run in gameplay.

## Decision 15 - Post-Wall Compile Guard

Problem: The earlier guarded Core compile stopped in SHINOBU_306 missing DTO diagnostics before SHINOBU_309 could be proven by compiler.
Solution: After the ledger showed SHINOBU_306 integration had landed, rechecked the guard: CPU 49.2%, no `dotnet`/`csc`/`VBCSCompiler` processes. Ran exactly one narrow `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1`.
Rejected Alternatives: Broad solution rebuild, restore, or building while Unity compiler processes were active.
Scalability potential: Tooling-only; runtime quality curve unchanged.
Hardware Impact: Build succeeded with 0 errors and 0 warnings in 2.24s. Runtime proof still requires Unity import, Play Mode, Burst/profiler/GCMonitor, and visual validation.

## Decision 16 - Interpolation Cost Collapse

Problem: The first solver pass made interpolation visually continuous, but still paid trilinear sampling cost at the cheap endpoint.
Solution: Use the same continuous `GlobalQualityWeight` curve with explicit endpoint collapse: below the smoothstep floor, flow and density sample only nearest; middle weights blend nearest and trilinear; full-quality endpoint uses pure trilinear without a redundant nearest read.
Rejected Alternatives: Always computing trilinear and lerping by zero preserved visuals but wasted ALU/cache bandwidth on thermally constrained hardware.
Scalability potential: Low = 1-tap flow and density sampling; Middle = transitional nearest/trilinear blend; High/Ultra = full trilinear flow/density with shader-side visual over-sampling from the same scalar texture.
Hardware Impact: Low endpoint saves 7 flow-volume reads and 8 nutrient-cell reads per active cell versus the previous always-trilinear path; high endpoint removes one redundant nearest nutrient read.

## Decision 17 - Mock Source Telemetry Precision

Problem: Blackbox telemetry marked the mock-source bit whenever there was exactly one source, which mislabels a legitimate single thermal vent as fallback data.
Solution: Pass the bounded source pointer into `RecordNutrientTelemetryJob` and set the mock bit only when a source row has `SourceFlagMock`.
Rejected Alternatives: Leaving the heuristic would corrupt forensic evidence during single-vent tests; adding a separate managed debug flag would violate the telemetry route.
Scalability potential: Same across tiers; telemetry truth does not change solver quality.
Hardware Impact: One telemetry job reads at most 16 source flags. Runtime cost is bounded and outside the per-cell advection loop.

## Decision 18 - UI Toolkit Graph Correction

Problem: The tuner window used UI Toolkit controls but rendered its telemetry graph through `IMGUIContainer`, `GUILayoutUtility`, `EditorGUI.DrawRect`, and `Handles.BeginGUI`, which made Task 16 weaker than the requested UI Toolkit facade.
Solution: Replace the graph bridge with a retained `VisualElement.generateVisualContent` callback and draw the 300-frame telemetry curve through `Painter2D`.
Rejected Alternatives: Keeping IMGUI was faster to write but adds a legacy repaint bridge and makes zero-GC/editor facade proof ambiguous.
Scalability potential: Editor-only. Low through Ultra runtime paths are unchanged; the graph can still inspect the same fixed 300-frame blackbox ring.
Hardware Impact: Runtime 0 us. Editor graph avoids IMGUI layout/draw bridge costs during the tuner repaint.

## Decision 19 - Hot Vault Preflight Collapse

Problem: `EnsureVaultState()` still ran the cold `OpenOrAcquireVaultBuffer` chain for all 14 Vault lanes before every normal FrostTick.
Solution: Once cold init succeeds, fast-path on stamped `VaultGenerationHandle` IDs. If later job buffer opens fail, mark `_initialized=false` so the next tick performs explicit cold reacquire.
Rejected Alternatives: Repeating Boot acquisition logic in hot cadence violates the Vault law and makes allocator/registry setup look like a gameplay guard.
Scalability potential: Low through Ultra keep the same fixed handles; quality still scales active axis/interpolation/upload cadence only.
Hardware Impact: Normal FrostTick removes 14 Vault open/acquire probes before scheduling. Stale-handle recovery remains bounded to the failure path.

## Decision 20 - Grid Header Lock Symmetry

Problem: `NutrientDriftGridHeaderDTO` is the proof artifact updated after job finalization, but the first lock matrix covered density upload and fault flags without locking the header lane.
Solution: Add `BufferID.ShinobuNutrientDriftGridHeader` to the scheduled solve lock/unlock set and make the unlock count symmetric.
Rejected Alternatives: Relying on owner convention without a lock leaves the proof artifact weaker than the data lanes it summarizes.
Scalability potential: Same across tiers; quality does not change header ownership or layout.
Hardware Impact: Adds one lock/unlock per scheduled solve. No per-cell cost; prevents concurrent read/write ambiguity around the proof row.

## Decision 21 - Bounded Telemetry Cursor

Problem: The 300-frame blackbox ring used a physical modulo slot but stored a monotonically increasing `int` cursor.
Solution: Store the next write cursor modulo the physical telemetry capacity and keep `_telemetryCursor` bounded in `0..299`.
Rejected Alternatives: Depending on `int` overflow being unreachable during normal endurance tests is not a mathematical closure proof.
Scalability potential: Same across tiers; telemetry capacity and DTO layout are fixed.
Hardware Impact: O(1) modulo math per scheduled solve. No per-cell cost and clearer crash-dump cursor semantics.

## Decision 22 - Script Meta Normalization

Problem: New C# assets had stable GUID metas but only the two-line header, leaving Unity free to regenerate importer metadata during import.
Solution: Add the standard `MonoImporter` block to the three new script `.meta` files while preserving GUIDs.
Rejected Alternatives: Waiting for Unity import to rewrite metas creates avoidable diff noise and weakens asset identity proof.
Scalability potential: Editor/import-only; runtime unchanged.
Hardware Impact: Runtime 0 us. Prevents metadata churn during Unity import.

## Decision 23 - Localized Shader Origin

Problem: The visual density texture publish cast `NutrientDriftTuningDTO.GridOriginAup` absolute `double3` directly to float shader globals.
Solution: Use `ResolveGridCenterLocal` so the shader origin is calculated by subtracting the current runtime origin in double precision before casting to `float3`.
Rejected Alternatives: Absolute float shader coordinates would reintroduce large-world jitter into the visual fake even though solver sources use local AUP math.
Scalability potential: Same across tiers; quality changes cadence/sampling, not coordinate authority.
Hardware Impact: O(1) per texture publish. Prevents precision loss at large world offsets.

## Decision 24 - Source Falloff Math LOD

Problem: `UpdateNutrientSourcesJob` paid one square root per source/cell even when `GlobalQualityWeight` requested the cheapest acceptable solver.
Solution: Map quality through `smoothstep(0.35,0.90)`. Low endpoint uses squared-distance falloff without `sqrt`; middle weights blend squared and exact radial weights; high/ultra keep exact radial shape.
Rejected Alternatives: Always computing exact radial falloff preserves shape but wastes ALU on weak hardware; a hard low/high switch would violate continuous scalability.
Scalability potential: Low = no `sqrt` in source injection; Middle = continuous blend; High/Ultra = exact radial injection with shader-side visual over-sampling.
Hardware Impact: Low endpoint saves up to 16 square roots per active cell at current source capacity. No DTO, Vault, save, or authority route changes.

## Decision 25 - Nutrient Editor Assembly Isolation

Problem: `NutrientDriftParticleScanner` uses Roslyn AST APIs, but the broad `Hecton8.Editor` asmdef does not declare Roslyn precompiled references.
Solution: Add a local editor-only `Hecton8.Ecosystem.NutrientDrift.Editor.asmdef` in the NutrientDrift folder with Roslyn references and `autoReferenced=false`.
Rejected Alternatives: Adding Roslyn to `Hecton8.Editor` would expand the compile wall for unrelated editor tools; reverting to text-only scanning would weaken the AST proof already requested by the status/report route.
Scalability potential: Runtime unchanged. Editor scanner remains isolated to the nutrient folder and does not change gameplay quality tiers.
Hardware Impact: Runtime 0 us. Editor compile blast radius is bounded to the nutrient editor assembly.

## Decision 26 - Mock Flow Radial LOD

Problem: `GenerateMockFlowFieldJob` still paid precise radial `sqrt` in the emergency fallback flow path even when `GlobalQualityWeight` selected the cheapest solver endpoint.
Solution: Reuse the continuous `smoothstep(0.30,0.90)` curve. Low quality uses squared-radius falloff without `sqrt`; middle quality blends squared and precise radial falloff; high/ultra keep the exact radial shape.
Rejected Alternatives: Leaving mock flow as always-exact was acceptable visually but not consistent with the low-quality ALU collapse already applied to source injection and density/flow interpolation.
Scalability potential: Low = no mock-flow radial `sqrt`; Middle = smooth visual transition; High/Ultra = exact vortex radial falloff plus shader over-sampling.
Hardware Impact: Low endpoint saves one square root per active mock-flow cell. At 16^3 this removes 4096 `sqrt` ops from the fallback pass; at 32^3 it removes 32768.

## Decision 27 - Contract Route Decoupling

Problem: The nutrient runtime still cached `PersistentWorldRegistry` and `HectonMapMagicVegetationBridge` concrete World owners, leaving a direct sibling-domain route in the Ecosystem owner.
Solution: Add `NutrientThermalVentSnapshotDTO`, `INutrientThermalVentReadModel`, and `IAbyssalFlowVolumeReadModel` in the core registry contracts. `PersistentWorldRegistry` and `HectonMapMagicVegetationBridge` implement those interfaces; `NutrientDriftRuntime` stores only interface fields resolved during activation or hot-swap.
Rejected Alternatives: Keeping concrete fields was simpler but violates the registry mandate when an owner interface can carry the read model. Moving thermal vents into a new Vault lane from this agent was rejected because `PersistentWorldRegistry` already owns the bounded vent fact and a new owner would create shadow state.
Scalability potential: Low through Ultra use the same read model and fixed DTO layout; quality still changes active axis, interpolation cost, source falloff, mock-flow falloff, and visual upload cadence only.
Hardware Impact: Runtime math cost is unchanged. The gain is compile-wall containment: nutrient drift no longer has concrete World owner fields/casts for its thermal and abyssal-flow inputs.

## Decision 28 - Evidence Consistency Pass

Problem: Earlier status/rationale evidence still described the initial concrete owner route, while Loop 18 source now resolves only Core read-model interfaces.
Solution: Normalize the proof text to `IAbyssalFlowVolumeReadModel` and `INutrientThermalVentReadModel`, leaving concrete World classes documented only as implementers behind the contract.
Rejected Alternatives: Leaving stale evidence text creates false architecture proof even when the source route is decoupled; editing runtime again without a source defect would be churn.
Scalability potential: Runtime quality behavior is unchanged: Low collapses sampling/falloff ALU; Middle blends; High/Ultra spend saved CPU on visual density presentation.
Hardware Impact: Runtime 0 us. Review cost drops because source and proof files now agree on the route boundary.

## Decision 29 - Loop 19 Build Guard Deferral

Problem: Loop 19 needs compile proof after the evidence consistency pass, but the explicit guard forbids launching build under high CPU or active compiler processes.
Solution: Re-run static gates, then sample CPU/compiler state before any build. The gate reported CPU 100% and active `dotnet.exe` PID 12344, so no `dotnet build` was launched.
Rejected Alternatives: Running `dotnet build` under current load would violate the batch rule and compete with another agent or Unity compiler process.
Scalability potential: Tooling-only; runtime quality scaling remains unchanged.
Hardware Impact: Runtime 0 us. Local machine contention avoided; pending compile proof remains explicit instead of faked.

## Decision 30 - Loop 20 Source Hygiene Audit

Problem: After contract-route decoupling, `NutrientDriftRuntime` still imports `Hecton8.World`, which could be misread as a concrete sibling-domain dependency.
Solution: Audit usage. The import is required for `AbsoluteUniversePosition` and AUP helpers already used by Core contracts; concrete World owner fields/casts are absent from nutrient runtime after Loop 18. No source mutation was made.
Rejected Alternatives: Moving AUP types out of `Hecton8.World` from this agent would be a broad core/domain migration outside SHINOBU_309. Removing the import without replacing shared AUP types would break compile.
Scalability potential: Runtime quality behavior remains unchanged. Low/Middle/High/Ultra still scale through active axis, interpolation/falloff ALU collapse, and visual upload cadence.
Hardware Impact: Runtime 0 us. The audit reduces false-positive review risk without changing execution.

## Decision 31 - Loop 20 Build Guard Deferral

Problem: Source hygiene audit needs a fresh Core compile, but the build rule also blocks when compiler processes are active even if CPU is under 50%.
Solution: Sampled CPU/process guard after static checks. CPU was 26%, but `csc.exe` PID 15232 and `dotnet.exe` PID 10876 were active, so no build was launched.
Rejected Alternatives: Launching a parallel compile under an active compiler process violates the explicit shared-workspace rule and risks IO/CPU contention.
Scalability potential: Tooling-only; no runtime quality route changes.
Hardware Impact: Runtime 0 us. Build proof remains pending instead of competing with active compiler work.

## Decision 32 - Runtime Self-Audit Hardening

Problem: `NutrientDriftSelfAudit.BuildSelfAuditXml()` still emitted a short attribute-only XML, weaker than the prompt's requested 20-task reconciliation and forensic proof block.
Solution: Expand the cold self-audit routine to emit Tasks 01-20, primary `NutrientCellDTO` byte offsets, secondary DTO sizes, Vault `70460..70473` verification, fixed capacities, continuous quality curve, H-Phi ownership, NoAlias/dependency graph, compile guard, Dear Lie complexity, zero-GC static proof, and netcode exclusion.
Rejected Alternatives: Leaving the short XML would force reviewers to reconstruct proof from scattered docs. Writing only a chat report would violate the reporting protocol.
Scalability potential: Runtime quality behavior is unchanged. Low still collapses sampling/falloff ALU and upload cadence; Middle blends; High/Ultra use full trilinear/exact radial math and shader over-sampling from the same scalar texture.
Hardware Impact: Hot path 0 us. The self-audit allocates a managed string only when explicitly called as a cold/editor diagnostic.

## Decision 33 - Loop 21 Report Restore And Build Guard Deferral

Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` was externally overwritten by a SHINOBU_326 scanner, removing the SHINOBU_309 section again. A fresh compile proof was also blocked by CPU and active Unity compiler processes.
Solution: Restore the `shinobu_309_plankton_nutrient_flow_drift` object while preserving currently present SHINOBU_326 and SHINOBU_325 report objects. Latest build guard: CPU 100%, Unity `dotnet.exe` PID 16552, so no `dotnet build` launched.
Rejected Alternatives: Overwriting the shared report with only this agent's object would repeat the original scanner defect. Building under saturated CPU and active compiler processes violates the explicit guard.
Scalability potential: Tooling-only; the runtime quality curve and Vault route are unchanged.
Hardware Impact: Runtime 0 us. Avoids local compile contention; current proof remains static plus prior guarded Core green build.
