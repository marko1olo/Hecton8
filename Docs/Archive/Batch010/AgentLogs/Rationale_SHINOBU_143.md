# Rationale_SHINOBU_143

Agent: SHINOBU_143
Domain: KINETIC_TETHER_AND_GRAPPLE_PHYSICS
Status: SOURCE POLISHED - COMPILE BLOCKED BY EXTERNAL DEPENDENCY

## Decision 000 - Preflight Authority Surface

Problem: Tether/grapple physics has previous failed design pressure toward Unity joints and per-frame CPU line rendering.
Solution: Use SHINOBU_143 XML as primary task, AGENTS.md as project law, and the tether/AUP/ARM64 mandates as implementation constraints before any source edit.
Rejected Alternatives: SpringJoint/ConfigurableJoint chains create recursive PhysX substeps and unstable high-tension NaN failure. LineRenderer creates CPU mesh rebuild work and cannot be the production visual path.
Scalability potential: Low uses fewer solver iterations and cheaper visual interpolation cadence; middle keeps deterministic stability; high increases stiffness; ultra spends saved CPU on smoother GPU spline data and richer shader lanes.
Hardware Impact: Low-end i3/MX350 target is reduced solver iteration cost and zero managed allocation. Estimated gain is architecture-dependent until profiler proof; no numeric runtime saving is claimed.

## Decision 001 - Mandatory Struct Law

Problem: Hot NativeArray node traversal must not suffer defensive copies or ARM64 unaligned reads.
Solution: Primary node DTO will be explicit 64-byte layout with raw public fields, no properties, and editor/static offset validation.
Rejected Alternatives: `Pack=1`, auto-properties, and hidden encapsulation methods in hot DTOs.
Scalability potential: Same node stride supports low-to-ultra; ultra fidelity belongs in visual buffers, not bloated simulation truth.
Hardware Impact: One node per 64-byte cache line improves predictable traversal and avoids unaligned double3 fetch penalties on ARM64-class hardware. Exact microseconds pending measurement.

## Decision 002 - Harpoon Tracer Purge

Problem: `HarpoonLauncherTool` instantiated and mutated a `LineRenderer` for shot/reel feedback, violating the cable/tracer CPU mesh rebuild ban.
Solution: Replace the tracer with cold-owned `GraphicsBuffer` lanes and the existing `Hecton8/Physics/TetherLineStrip` procedural shader. Runtime writes two `GpuCableSplinePointDTO` values plus draw constants through `LockBufferForWrite`.
Rejected Alternatives: Keeping LineRenderer for "short lifetime" visuals still creates a component mesh path and managed renderer dependency. Creating per-shot GameObjects is worse.
Scalability potential: Low uses a two-point Dear Lie beam. Middle/high/ultra can reuse the same shader lane and enrich material response without new physics.
Hardware Impact: Removes LineRenderer CPU mesh rebuild and runtime component creation from harpoon feedback. Exact microseconds pending profiler; static gain is fewer renderer-side CPU mutations.

## Decision 003 - AUP Verlet Solver Surface

Problem: Existing tether solve used local `float3` Verlet state; that cannot be the authoritative route for 100km AUP precision.
Solution: Add `TetherNodeDTO` with `double3` current/previous AUP, deterministic Burst integration, and distance relaxation that subtracts double AUPs first before bounded local `float3` math.
Rejected Alternatives: Expanding the old local-float node DTO would preserve the map-edge jitter failure. PhysX joints remain forbidden.
Scalability potential: Low keeps fewer iterations and rubbery response. Middle stabilizes tow cables. High/ultra spends iteration budget up to 15 and feeds smoother GPU splines.
Hardware Impact: O(nodes + constraints * iterations). On weak i3/MX350-class silicon, dropping from 15 to 2-3 iterations avoids roughly 80% of relaxation passes for the same cable count.

## Decision 004 - Vault Mock and Zero-Init Route

Problem: CI and editor need a deterministic tether sample without scene-authored joints, GameObjects, or persistent private native allocations.
Solution: `TetherAupVaultBootstrap` requests SHINOBU_143 buffers from `GlobalDataVault` with uninitialized slabs where fully overwritten, then runs `InitializeMockTetherAupJob` for 5 cables x 30 nodes.
Rejected Alternatives: Scene prefab mock cables, managed arrays, or local persistent NativeArrays would violate Data Sovereignty and make rollback snapshots ambiguous.
Scalability potential: The same buffers cover low-through-ultra; low can display linear fake, ultra can spline over the same truth.
Hardware Impact: Avoids redundant zero-fill for node/constraint/spline/force slabs before full overwrite. Exact timing pending profiler/import.

## Decision 005 - Designer Tuning and CSV

Problem: Cable material and solver constants must change without recompiling C#.
Solution: Add a UI Toolkit tuner for solver scalars and an allocation-free core `ReadOnlySpan<byte>` CSV parser for `cable_materials.csv`.
Rejected Alternatives: IMGUI-only tuning does not satisfy the requested editor facade. `string.Split`/LINQ parser is disallowed for runtime parser core.
Scalability potential: Designers can bias low/middle/high/ultra behavior by adjusting global quality and material stiffness without code changes.
Hardware Impact: Parser core is linear over bytes and writes fixed DTOs; editor file read allocates only in editor path, not gameplay.

## Decision 006 - Black Box and Force Packet Route

Problem: Tether NaNs and surge loads must be diagnosable and must not mutate Rigidbody from Burst jobs.
Solution: Solver writes paired `TetherForcePacketDTO` endpoint packets with AUP application points; telemetry writes fixed 64-byte entries with hashes into a 300-entry ring.
Rejected Alternatives: Direct `Rigidbody.AddForce` in the solver or ad-hoc logs after failure would break determinism and forensics.
Scalability potential: Low can still emit authoritative force packets while reducing visual work. Ultra can retain richer spline/telemetry sampling on the same packets.
Hardware Impact: Force output is two 64-byte packets per active constraint. Telemetry is fixed 19.2KB for SHINOBU_143 ring.

## Decision 007 - Compile Wall Handling

Problem: Compile verification was required after source edits, but whole-project build failed before SHINOBU_143 files could be independently proven.
Solution: Ran `dotnet build .\Assembly-CSharp.csproj --no-restore` only after the CPU/compiler gate opened. Treat the result as dependency-blocked because errors are missing external DTO/contracts in Visor/Somatic/Equipment files, not in the tether/harpoon files touched here.
Rejected Alternatives: Editing `HectonVisorUberPostFeature`, `DeferredDecalPass`, `ModularEquipmentEngine`, `GlobalRegistryContracts`, or `SomaticTunerWindow` from this domain would violate owner-local authority. Re-running while `dotnet` build servers remain active violates the user's CPU/build rule.
Scalability potential: No runtime scalability change; this protects compile-wall isolation.
Hardware Impact: Avoided repeated build attempts after post-build CPU hit 100% and `dotnet` processes stayed resident.

## Decision 008 - AUP Mock Scheduling, Force Hygiene, And Exact Cable Surgeon Dump

Problem: The first AUP pass had valid kernels but left three weak seams: uninitialized force packet lanes could retain stale values when tension dropped to zero, the mock tethers were seeded but not continuously moved by a deterministic endpoint driver, and the requested `Dump_CABLE_SURGEON.bin` path was not a first-class export target.
Solution: Add `AdvanceMockTetherEndpointsJob`, `TetherAupSolverScheduler.Schedule/ScheduleMock`, pinned endpoint Vault buffers, segment tension and solver-stat Vault buffers, exact `TetherAupBlackBoxDumper` export, and a managed bridge that routes finite `TetherForcePacketDTO` packets through `PhysicsForceRouter.QueueForceAtPosition`. The manager schedules the mock AUP pass with a `JobHandle` and only completes an already-finished prior handle before scheduling another pass. The live tow endpoint force boundary now also constructs `TetherForcePacketDTO` payloads before queueing forces, so the runtime and AUP solver share the same unmanaged packet contract.
Rejected Alternatives: Completing the AUP mock solve every fixed tick would satisfy a demo but violates the dependency-chain mandate. Leaving force packets uninitialized after zero tension is rejected because a stale packet can apply a force that no current constraint produced. Writing only `.h8dump` is rejected because Task 16 names `Dump_CABLE_SURGEON.bin` explicitly.
Scalability potential: Low quality keeps two solver iterations and cheaper endpoint motion; middle/high/ultra increase stiffness and keep richer telemetry/spline lanes without adding node truth.
Hardware Impact: On i3/MX350-class hardware the mock scheduler can skip a new pass if the previous pass is still running, avoiding a fixed-step stall. Exact microseconds remain unclaimed because compile/profiler proof is externally blocked.

## Decision 009 - Constraint Fault Telemetry Propagation

Problem: A constraint solver can detect invalid distance math without producing a non-finite node. If telemetry only records node NaNs, forensic dump export can miss the exact class of cable failure that matters most under high tension.
Solution: Propagate `SolverStats[2]` fault bits into `TetherAupTelemetryEntry.Flags` inside `RecordTetherAupTelemetryJob`, so the manager's fault watcher and `Dump_CABLE_SURGEON.bin` trigger on solver constraint faults as well as recovered non-finite node positions.
Rejected Alternatives: Relying on node flags alone is too late; a constraint can be rejected safely without mutating a node, and that still needs black-box evidence. Throwing from Burst is rejected because it breaks deterministic rollback and fault containment.
Scalability potential: No quality-tier branch added. Low/middle/high/ultra all carry the same fixed 64-byte telemetry entry.
Hardware Impact: One scalar read and one integer OR in the telemetry job; below measurable significance relative to the solve pass.

## Decision 010 - Vault-Backed Cable Material Hash Table

Problem: The byte-span CSV parser generated FNV material hashes, but writing SHINOBU material rows linearly made lookup semantics weaker than the task's hash-map requirement.
Solution: Add `CableMaterialCsvParser.ParseHashTable(ReadOnlySpan<byte>, NativeArray<CableMaterialDTO>)`, which clears the Vault-owned material slab and inserts rows with deterministic open addressing by `MaterialHash`. Add `TryFindHashSlot` for allocation-free hash lookup. The editor facade keeps the legacy linear table for old consumers and writes SHINOBU rows into the FNV keyed Vault table.
Rejected Alternatives: A persistent private `NativeParallelHashMap` would violate the Vault law and require a managed owner lifetime. A managed Dictionary or string-keyed table is rejected for GC and rollback. Adding a new cross-domain container system is rejected as compile-wall expansion.
Scalability potential: Low/middle/high/ultra use the same hash table; higher tiers can add material-specific visual overkill without changing simulation truth.
Hardware Impact: Expected O(1) material lookup in a fixed 16-slot table, no managed allocation in parser core, one predictable linear-probe loop bounded by table capacity.
