# SHINOBU_152 Rationale

Date: 2026-05-19
Status: POLISH PASS / SOURCE VERIFIED / COMPILE BLOCKED BY EXISTING DEPENDENCIES

## Initialization

Problem: Current mission requires replacing object-oriented vehicle component health with a flat damage grid, but source ownership and existing routes are unknown.

Solution: Extract only `SHINOBU_152` prompt, read eight relevant mandates, create status/rationale state, then inspect current code before edits.

Rejected Alternatives: starting with a new global manager or direct vehicle references would violate owner-local/global-authority rules and risk conflicts with 20+ active agents.

Scalability potential: Low uses bounded grid radius and scalar hydrodynamic penalties; Middle keeps stable component summaries; High adds richer hazard telemetry; Ultra spends saved CPU on presentation/VFX consumers without bloating gameplay truth.

Hardware Impact: Static design target is sub-0.1 ms for the solver slice on i3/MX350 by using 16-byte cells, bounded loops, Burst, and no GameObject damage fan-out. Measured gain is PENDING VERIFICATION.

## Mandatory Thinking

Toaster: deterministic blockier propagation, smaller radius, coarse telemetry fields, no physical per-part scripts.

$5000 machine: same gameplay truth, more adjacent-cell evaluation, richer hazard signals, heavier visual consumers in `VISUAL_SYNC`.

## Loop 1 Decisions - Tasks 01-05

Problem: Vehicle damage could not be trusted if legacy component-health or collision callbacks remained the primary truth path.

Solution: No exact `SubmarineEngineHealth.cs` or `BallastDamage.cs` files existed. Created a new Vault-owned `VehicleGridCellDTO` component grid and made the existing structural `OnCollisionEnter` relay opt-in, keeping AUP `CombatDamageSignal` as the default route.

Rejected Alternatives: Deleting `SubmarineStructuralGrid` collision code outright would break unrelated hull/flooding work owned by other agents. Keeping it enabled by default would violate the router mission.

Scalability potential: Low runs a 16-byte cell grid with bounded signal count and minimal mock inputs; Middle uses the same scalar outputs with moderate propagation; High increases continuous radius; Ultra spends saved CPU on VFX consumers, not gameplay truth changes.

Hardware Impact: Exact health-script deletion saved 0 us because target files were absent. Gating collision fan-out removes the default contact callback route; estimated impact-frame saving is 15-80 us on i3/MX350 scenes with hull contact noise. Measured profiler proof is pending.

Problem: Native grid cells must be ARM64-stable and mutable in place from Burst.

Solution: Defined `VehicleGridCellDTO` as explicit 16 bytes with offsets 0/4/8/12 and raw public fields. Added `VehicleDamageLayoutValidator` using `UnsafeUtility.SizeOf` and field offsets.

Rejected Alternatives: Sequential layout, properties, or managed component lists would reintroduce CS1612 copies and non-deterministic object graph state.

Scalability potential: Low gets dense contiguous cache-friendly cells; Middle/High/Ultra scale propagation depth, not object count.

Hardware Impact: 768 default cells fit in 12 KB write grid plus 12 KB read grid. Estimated cell scan cost target is under 10 us on i3/MX350 before signal propagation.

Problem: Combat systems may be absent during isolated verification.

Solution: Added `GenerateMockVehicleDamageJob` with deterministic frame-seeded math, writing into a secondary Vault buffer before copy into the main signal buffer.

Rejected Alternatives: Main-thread random impacts, UnityEngine.Random, or fabricated managed event objects would break rollback and zero-GC rules.

Scalability potential: Mock count is driven by continuous `GlobalQualityWeight`, from minimal survival signal count to richer stress at high quality.

Hardware Impact: Estimated 1-2 us for four mock signals on low-end silicon; measured Burst timing pending.

## Loop 2 Decisions - Tasks 06-10

Problem: AUP impacts must land in a submarine-local grid without losing precision in a 100 km world.

Solution: `MapImpactToGridJob` subtracts the vehicle root `double3` AUP from each impact `double3`, then casts only the localized delta to `float3` and applies inverse root rotation. The cell index is resolved from local normalized grid coordinates.

Rejected Alternatives: `Transform.InverseTransformPoint`, `Physics.Raycast`, and absolute `float3` world coordinates were rejected because they either allocate/bridge to UnityEngine or destroy AUP precision far from origin.

Scalability potential: Low uses direct-hit cells and radius 1; Middle expands radius modestly; High and Ultra increase continuous propagation radius using `GlobalQualityWeight` while keeping the same deterministic state truth.

Hardware Impact: Mapping target is 2-6 us for 128 events on i3/MX350. No measured profiler data yet; temp compile could not fully complete due unrelated repository dependency wall.

Problem: Explosive damage must feel volumetric without raycasting through hull meshes.

Solution: `PropagateDamageJob` uses bounded grid-space inverse-square falloff and atomic in-place cell integrity updates. This is a cinematic damage fake: local voxel spread, not per-triangle fracture.

Rejected Alternatives: raycast sprays, MeshCollider hierarchies, fracture mesh spawning, and part-level `Health` components.

Scalability potential: Low clamps radius to one neighbor layer; Middle/High/Ultra expand radius continuously and can feed richer VFX from the same grid.

Hardware Impact: Estimated 4-18 us depending signal count and quality radius; saved cycles buy better damage visualization downstream.

Problem: Vehicle motion must degrade from component damage without direct coupling to engine/ballast/sensor GameObjects.

Solution: `EvaluateVehicleSystemsJob` summarizes component cells into thrust, buoyancy, sensor, drag, flood, and fire scalars. `SubmarineDynamicsRuntime` reads only the published `VehicleDamageStateDTO` from the Vault and applies hydrodynamic penalties.

Rejected Alternatives: direct references to subsystem scripts, `SetActive(false)`, or disabling authored objects from the damage solver.

Scalability potential: Low applies coarse scalars; Middle keeps component-specific penalties; High/Ultra can use the same state for cockpit UI, VFX, and audio without touching gameplay truth.

Hardware Impact: Evaluation target is 5-10 us over 768 cells. Kinematics read cost is below 1 us.

Problem: Flooding and hazards need to cross domains without turning into fluid simulation or particle truth.

Solution: Outer-hull cells below integrity threshold set `Flooded`, compute depth-weighted ingress and water mass, and publish `VehicleHazardSignal` for fire/flood/destroyed cells.

Rejected Alternatives: per-breach MonoBehaviours, CPU particles as truth, or direct calls into the fluid runtime.

Scalability potential: Low gets one scalar water mass; Middle/High/Ultra receive richer hazard events for presentation and damage control.

Hardware Impact: Incremental cost is 1-4 us inside the existing evaluation pass.

Problem: Visual/UI readers must not observe torn write buffers.

Solution: `PublishVehicleDamageStateJob` performs a post-simulation `UnsafeUtility.MemCpy` from write grid/state into read grid/state. Consumers read only the read buffer.

Rejected Alternatives: direct write-buffer reads, managed snapshots, or object graph copies.

Scalability potential: Same 12 KB copy on all tiers; Ultra can add visual consumers without expanding authoritative state.

Hardware Impact: Estimated 3-5 us for default 12 KB grid plus 128-byte state copy on i3/MX350.

## Loop 3 Decisions - Tasks 11-15

Problem: Damage propagation must scale across weak, middle, high, and ultra devices without a binary quality switch.

Solution: Propagation radius and mock signal count are continuous functions of `HomeostasisBrain.GlobalQualityWeight`. The solver clamps bounds for predictability but never branches on hardware tier.

Rejected Alternatives: `if (lowEnd)` branches, disabled damage, or high/low presets.

Scalability potential: Low uses radius 1 and minimal signal stress; Middle uses moderate radius; High expands neighbor propagation; Ultra keeps gameplay truth identical and gives VFX more hazard density.

Hardware Impact: Propagation estimate is 4 us low, 18 us high for default grid/signals. Exact Burst profiler proof is blocked by repository compile wall.

Problem: Fire and breach hazards must be authoritative enough for gameplay but cheap enough for cheap silicon.

Solution: Cells carry `Burning`, `Flooded`, and `Destroyed` flags. `EvaluateVehicleSystemsJob` emits unmanaged `VehicleHazardSignal` packets from the same pass; no particles or scene object state becomes truth.

Rejected Alternatives: CPU particle systems, instantiated hazard GameObjects, or direct VFX calls.

Scalability potential: Low can consume only scalars; Middle reads hazard count; High/Ultra can spawn richer visuals/audio from the same signal lane.

Hardware Impact: Hazard routing estimate is 1-3 us inside the existing scan.

Problem: AUP mapping has to survive a vehicle pitched 90 degrees at a far-origin coordinate.

Solution: Let impact AUP be `I`, root AUP be `R`, and root rotation be `Q`. The job computes `local = inverse(Q) * (float3)(I - R)`. For a 90-degree pitch, a world-space vertical delta rotates into the vehicle's local forward/up axis according to `inverse(Q)`, and precision is preserved because `(I - R)` is evaluated in double before the cast. Absolute `float3(I)` is never formed.

Rejected Alternatives: absolute float conversion before subtraction, Unity `Transform` helpers inside the hot path, or physics queries.

Scalability potential: Same formula on every tier; only propagation work changes.

Hardware Impact: Cost is a quaternion multiply per signal; benefit is avoiding far-origin mis-bins that would otherwise corrupt damage state.

Problem: Rollback/netcode needs memcopy-friendly deterministic state.

Solution: DTOs use explicit layouts; Burst jobs use deterministic float mode; mock damage uses frame-seeded hash math, not Unity random; write/read publication is `UnsafeUtility.MemCpy`.

Rejected Alternatives: object graph snapshots, managed lists, `Time.deltaTime`, or transient component state.

Scalability potential: Low through Ultra snapshot the same byte layout: grid cell array plus one state DTO.

Hardware Impact: 12 KB default grid is cheap to snapshot and copy; object graph traversal was rejected as unbounded.

Problem: Vault buffers requested with uninitialized memory cannot expose garbage to readers.

Solution: `InitializeVehicleGridJob` fills both write and read grids after allocation; state buffers are explicitly initialized with sane scalar defaults.

Rejected Alternatives: relying on OS zero-fill or clearing the whole buffer every frame.

Scalability potential: Low avoids load-time clears; High/Ultra can increase grid dimensions within fixed max bounds.

Hardware Impact: Initialization cost is cold only. Runtime avoids recurring memset.

## Loop 4 Decisions - Tasks 16-18

Problem: "I do not know why it crashed" is forbidden for the damage router.

Solution: The state evaluator writes a 300-entry `VehicleDamageTelemetryEntry` circular buffer. If fatal NaN state is observed, runtime dumps the raw ring to `Docs/AgentLogs/Dump_VEHICLE_SURGEON.bin` using a raw `ReadOnlySpan<byte>`.

Rejected Alternatives: managed string logs, console-only reports, or profiler-only diagnostics.

Scalability potential: Low keeps the same 300 high-level state frames; Ultra can layer richer tooling over the same bytes.

Hardware Impact: Per-frame telemetry write is below 1 us estimated; dump is cold fault path only.

Problem: Designers need to tune damage without adding runtime inspector logic.

Solution: Added `Vehicle Integrity Tuner` UI Toolkit window under editor-only compilation. It edits serialized runtime tuning and reads Vault state.

Rejected Alternatives: runtime debug UI or per-component inspectors.

Scalability potential: No player-build cost. High-end editor sessions can monitor richer state without altering runtime.

Hardware Impact: 0 us player hot path.

Problem: Component layout data must be ingestible without managed CSV parsing.

Solution: Cold parser reads `vehicle_component_layouts.csv` through a Vault scratch byte buffer, walks `ReadOnlySpan<byte>`, parses ASCII ints/floats, and hashes component names with FNV-1a.

Rejected Alternatives: `File.ReadAllLines`, string split, lists, or JSON.

Scalability potential: Low uses the authored grid defaults if CSV is absent; Middle/High/Ultra can author denser component layout without runtime allocations.

Hardware Impact: 0 us hot path. Cold load cost depends on file size and is capped at 64 KB.

## Loop 5 Decisions - Tasks 19-20

Problem: Damage debugging must not create another health UI or mutate simulation state.

Solution: `OnDrawGizmosSelected` samples the read grid only and renders damaged/burning cells in local x-ray space.

Rejected Alternatives: inspectors on individual components or in-game health bars.

Scalability potential: Editor visualization can sample with stride; player builds pay 0 us.

Hardware Impact: 0 us player hot path; editor-only visualization cost is bounded by `maxGizmoCells`.

Problem: Verification cannot claim a clean build when the repository has unrelated missing/generated dependency failures.

Solution: Ran guarded `dotnet build`; direct build fails on deleted `ChemicalInfluenceGrid.cs` and `LogisticsPipeEvents.cs`. Ran a temp-project compile excluding only those two missing files and including SHINOBU files; filtered log contains no SHINOBU file errors after replacing enum-member references with numeric `BufferID` casts while keeping the enum additions in `H8Memory.cs`.

Rejected Alternatives: restoring/deleting another agent's files, editing generated csproj, or reporting a false green build.

Scalability potential: Verification method does not affect runtime.

Hardware Impact: None.

## Ultra Polish Decisions - 2026-05-19

Problem: The first implementation still preserved a dormant `OnCollisionEnter`/relay source surface in `SubmarineStructuralGrid`. Even disabled by default, that left a second route for hull/component damage and contradicted the owner-local route law.

Solution: Removed the legacy collision callback, relay component, opt-in flag, collision mass helper, and collision energy tuning fields from the structural grid source surface. Vehicle component damage now enters through AUP damage signals and the existing `IDamageSignalReceiver`/queued local impact route, not Unity contact callbacks.

Rejected Alternatives: Keeping the relay behind `enableLegacyCollisionDamage=false` was rejected because the prompt explicitly forbids `OnCollisionEnter` and because dormant callback routes become re-enabled during prefab churn. Renaming the callback was rejected as a fake fix.

Scalability potential: Low avoids contact-storm fan-out entirely; Middle/High/Ultra spend the saved CPU on shader dents, pooled sparks, telemetry, audio, or haptics through presentation lanes rather than contact callbacks.

Hardware Impact: Static estimate remains 15-80 us saved on noisy impact frames. Measured profiler proof remains pending because Unity/runtime proof was not run.

Problem: The editor facade wrote serialized runtime fields first, so a live Vault tuning DTO could be overwritten on the next fixed tick. This violated the human-control requirement.

Solution: Added editor override tuning flags and direct `VaultBufferHandle<VehicleDamageTuningDTO>.GetElementAsRef` mutation. Runtime `ResolveTuning` now preserves CSV/editor-authored scalar values, sanitizes them, and only restores serialized values when no external tuning authority exists.

Rejected Alternatives: SerializedObject-only tuning was rejected because it requires scene object mutation and does not prove a live Vault-backed data contract. A ScriptableObject config was rejected because it would add a parallel truth store.

Scalability potential: Low can use coarse armor/falloff values without recompiling; Middle/High/Ultra can hot-adjust explosion radius, fire chance, and armor scalars for richer feedback while gameplay truth stays deterministic.

Hardware Impact: 0 us player hot path; editor-only managed UI work remains outside runtime. FixedTick cost is a single 96-byte DTO read/sanitize/write.

Problem: Runtime root AUP mapping depended on a direct `Hecton8.World` conversion call and presentation `Transform.position`, which weakens the compile wall and AUP authority.

Solution: Removed the direct `Hecton8.World` import from the SHINOBU vehicle damage runtime. The router now uses a cached root pose snapshot refreshed outside the damage schedule; presentation-only fallback exists only in editor/development builds for isolated mock profiling.

Rejected Alternatives: Keeping world conversion in the hot route was rejected as sibling-domain coupling. Blindly using `Transform.position` in player builds was rejected because Transform is presentation state.

Scalability potential: Same AUP formula on every tier; quality weight changes propagation work, not coordinate authority.

Hardware Impact: Kinematic DTO read is one 192-byte cold/hot snapshot read before scheduling jobs. It removes one sibling namespace dependency from the runtime file.

Problem: The previous `H8Memory.cs` enum additions expanded a core header even after numeric `BufferID` constants were introduced for stale-prebuilt compatibility.

Solution: Removed SHINOBU_152 `ShinobuVehicleDamage*` enum entries from `H8Memory.cs` and kept owner-local numeric casts in `VehicleDamageConstants`.

Rejected Alternatives: Keeping both enum and numeric constants was rejected because it preserves unnecessary core churn and does not improve runtime safety.

Scalability potential: None at runtime; this is compile-wall containment.

Hardware Impact: Build iteration protection only; no frame-time effect.

Problem: The telemetry ring did not explicitly expose total damage processed to the editor/readout path.

Solution: Replaced telemetry tail padding with `TotalDamage01` at offset 120 and `Reserved0` at offset 124. `EvaluateVehicleSystemsJob.WriteTelemetry` copies `state.TotalDamage01`.

Rejected Alternatives: Deriving total damage in editor from structural integrity was rejected because black-box dump fields must be forensic facts, not UI reconstruction.

Scalability potential: Low uses the scalar for coarse alarms; Middle/High/Ultra can drive richer cockpit/VFX/audio diagnostics from the same fixed ring.

Hardware Impact: No size increase; `VehicleDamageTelemetryEntry` remains 128 bytes.

## Ultra Hardening Decisions - 2026-05-19

Problem: A direct `FixedTick` read of `SubmarineKinematicStates` could race the kinematic integrator if both systems were registered in the same dispatcher lane and the integrator job had been scheduled but not post-fixed yet. Vault locks prevent relocation/defrag, not writer exclusion.

Solution: Removed live `SubmarineKinematicStates` reads from the damage FixedTick path. The router now uses a cached root pose snapshot. The snapshot is refreshed during cold boot/LateFrame from `SubmarineKinematicConfig.LocalOriginAup` plus the last completed local transform pose/rotation already published by the kinematic owner. Player builds fail closed until a config-backed snapshot exists; editor/development keeps transform-only fallback for isolated mock profiling.

Rejected Alternatives: Reading `SubmarineKinematicStates` under a Vault lock was rejected because the lock is not a reader-writer fence. Forcing `JobHandle.Complete()` on the kinematic owner was rejected because it would create a gameplay sync point. Adding a new core job-query API was rejected as compile-wall expansion outside this domain.

Scalability potential: Low through Ultra run the same AUP mapping formula. Low avoids synchronization stalls; Ultra can spend saved frame budget on visual damage consumers without changing the authoritative grid truth.

Hardware Impact: Avoids a potential cross-core data race and removes a 192-byte live state read from the damage schedule path. The retained LateFrame snapshot cost is one config DTO read plus transform scalar copy outside the Burst damage chain.

Problem: Cold initialization and CSV ingest resolved Vault pointers without holding all relevant Vault buffer locks.

Solution: Cold grid initialization now uses the full damage-buffer lock group before scheduling the Burst fill and writing state/cursor defaults. CSV ingest locks CSV scratch, write grid, read grid, and tuning before reading file bytes and applying component layout rows.

Rejected Alternatives: Assuming cold paths are safe without locks was rejected because Vault relocation is orthogonal to frame hotness. Locking only the grid buffers was rejected because init also mutates tuning, state, and cursor.

Scalability potential: Low devices avoid undefined pointer relocation faults during tooling and cold boot; High/Ultra can author denser CSV layouts without changing runtime allocation behavior.

Hardware Impact: No player hot-path cost. Cold path adds a handful of lock counter increments/decrements to protect pointer stability.

Problem: The editor readout used direct Vault reads and `.ToString()` formatting on every refresh.

Solution: Added editor-only runtime snapshot methods that refuse reads while `_damagePending` or `_buffersLocked`, lock state/telemetry/tuning buffers for short reads/writes, and expose telemetry through disabled UI Toolkit numeric fields updated via primitive `SetValueWithoutNotify`.

Rejected Alternatives: Keeping static editor Vault reads was rejected because it had no pending-job awareness. String labels with per-refresh numeric formatting were rejected because Task 17 explicitly asks for a zero-GC readout facade.

Scalability potential: Player builds pay 0 us. Editor sessions get live numeric telemetry without mutating solver state or inducing C# recompiles.

Hardware Impact: 0 us player hot path; editor refresh avoids source-level `.ToString()` churn in the telemetry display.

Problem: Task 18's hot-reload convenience still let player `SlowTick` probe `vehicle_component_layouts.csv` through `File.Exists`/`FileInfo`/`FileStream`. The binary payload ledger explicitly rejects runtime file probes.

Solution: Guarded the CSV hot-reload call and loader implementation with `UNITY_EDITOR || DEVELOPMENT_BUILD`. The byte/span parser remains available for tooling and development verification, but shipping player builds consume already-hydrated Vault data and never poll the project CSV from `SlowTick`.

Rejected Alternatives: Keeping file polling in player builds was rejected because it creates managed IO churn and platform-specific stalls. Removing the parser entirely was rejected because Task 18 requires a cold human-authored CSV ingest path.

Scalability potential: Low through Ultra player builds pay 0 us for CSV reload. Editor/development keeps live layout iteration for designers without recompiling C#.

Hardware Impact: Removes player-side `File.Exists`, `FileInfo`, and CSV `FileStream` probes from slow ticks. Exact microseconds depend on storage and platform; structural target is 0 us player hot path.

Problem: The CSV parser computed FNV-1a hashes, but the built-in component constants were not the FNV-1a values for `hull`, `engine`, `ballast`, `sensors`, or `power`. A designer-authored CSV row for `engine` would not count as an engine in `EvaluateVehicleSystemsJob`. The same parser also replaced `StatusFlags`, erasing initialized `OuterHull`, `Flammable`, and critical component flags.

Solution: Canonicalized component constants to the actual lowercase FNV-1a hashes and added allocation-free alias folding for `sensor`, `sonar`, `engines`, `reactor`, and `battery`. CSV apply now ORs authored flags with existing initialized flags plus component-derived critical/flammable flags.

Rejected Alternatives: Keeping arbitrary constants was rejected because it violated Task 18's FNV-1a authoring contract. Requiring designers to supply every status flag in CSV was rejected because omission would silently disable breaches and fire.

Scalability potential: Low devices keep the same flat grid. Middle/High/Ultra can author denser component maps without breaking hydrodynamic scalar evaluation or hazard routing.

Hardware Impact: CSV path remains editor/development cold. Runtime evaluation cost is unchanged because component classification still compares `uint` hashes.

Problem: `AtomicApplyIntegrityDamage` wrote `CellFlagDestroyed` from parallel map/propagation workers after a CAS on integrity. That created a non-atomic shared `StatusFlags` write across jobs touching the same cell.

Solution: Removed parallel `StatusFlags` mutation from the atomic damage helper. The serial `EvaluateVehicleSystemsJob` finalizes destroyed, flooded, and burning flags after all integrity writers finish.

Rejected Alternatives: Atomic OR on `StatusFlags` was rejected because the flag is not the contention-critical damage value and would add more interlocked traffic to the impact storm path. Keeping the race was rejected because it could lose authored flags.

Scalability potential: Low saves interlocked flag traffic; High/Ultra still get identical gameplay truth because final flags derive from settled integrity.

Hardware Impact: Reduces cache-line invalidation in parallel impact storms. Exact microsecond gain is pending profiler proof.

Problem: Mock impact jitter and fire ignition used deterministic hash sampling but did not use `Unity.Mathematics.Random`, while the project mandate requires deterministic math RNG instead of ad hoc random sources.

Solution: Switched mock impact sampling and fire chance to `Unity.Mathematics.Random.CreateFromIndex`, seeded from frame/index/root AUP hash or vehicle hash. No `UnityEngine.Random`, heap RNG, or managed state is present.

Rejected Alternatives: Keeping pure hash sampling was cheaper but failed the explicit RNG mandate. A persistent RNG field was rejected because rollback needs seed-reconstructible stateless sampling.

Scalability potential: Low runs fewer mock signals through the existing quality curve; High/Ultra can stress with more deterministic mock impacts without desync.

Hardware Impact: Small ALU increase per sampled mock/fire candidate, traded for mandate compliance and deterministic replay clarity.

Problem: Fault black-box dumping read state and telemetry buffers without acquiring Vault locks, and `EnsureVaultBuffers` still had a tuning DTO write before entering the full damage-buffer lock group.

Solution: Removed the unguarded tuning write; locked initialization/FixedTick paths own tuning mutation. `DumpBlackBoxIfFaulted` now locks state-read and telemetry ring buffers before resolving pointers and writing the raw dump.

Rejected Alternatives: Treating fault path as exempt was rejected because crash forensics must be more reliable than normal telemetry. Locking only state was rejected because the dump writes telemetry bytes.

Scalability potential: No player hot-path cost. Fault-path locks make post-mortem output stable on all tiers.

Hardware Impact: 0 us normal frame cost. Fault dump adds two Vault lock/unlock pairs only when fatal NaN is detected.

Problem: Diff review exposed double-encoded header/comment text in the `SubmarineStructuralGrid.cs` lines touched by the collision-route purge. Leaving mojibake in serialized inspector headers is not runtime-expensive, but it creates designer-facing corruption and makes future diff review noisy.

Solution: Normalized only the touched header/comment surface to ASCII labels while preserving the behavior change: collision callback and relay code remain removed, and visual dent publication still routes through `CombatDamageSignal`.

Rejected Alternatives: Reverting the whole structural grid was rejected because unrelated agents have dirty work and the collision purge is required by SHINOBU_152. Leaving mojibake was rejected because it degrades editor usability and review clarity.

Scalability potential: No runtime scaling effect. The change protects the editor facade and authoring path from corrupted labels on all hardware tiers.

Hardware Impact: 0 us frame-time effect; source hygiene only.
