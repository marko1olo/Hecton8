# Rationale_SHINOBU_153

Agent: SHINOBU_153
Status: IMPLEMENTED / STATIC POLISH PASS 3 / COMPILE GATE PENDING

## Decision 00 - Domain Boundary

Problem: The assignment touches resource generation, save depletion state, GPU rendering, and editor diagnostics. Blindly editing save/render systems would cross ownership boundaries.
Solution: Keep geology authority in Echelon 2 World Generation. Use Vault buffers/snapshots and narrow interfaces for save depletion and render consumption. Route-card any new global/Vault surface.
Rejected Alternatives: Direct references to Save Archivist, Scavenging Oracle, or renderer concrete classes were rejected because 20+ agents may rewrite those systems concurrently and concrete coupling violates Global Authority boundaries.
Scalability potential: Low uses fewer cosmetic matrices and broad paging hysteresis; Middle raises cluster density; High keeps more sectors resident; Ultra spends saved storage/CPU on rich visual-only clusters while authoritative node count remains deterministic.
Hardware Impact: Replacing stored full node coordinates with seed-derived JIT generation removes disk and memory pressure; expected low-end gain is bounded by current unknown resource count, with hot-path target 0 B GC and sub-0.1 ms amortized generation/upload slices on i3/MX350.

## Decision 01 - RNG Authority

Problem: Unity RNG and floating weighted tables break deterministic replay and cross-client resource identity.
Solution: Use integer hash/LCG seeded by world seed, AUP sector hash, table version, roll index, and salt. Resource selection uses integer cumulative weights or fixed fallback rules.
Rejected Alternatives: `System.Random`, `UnityEngine.Random`, frame time, object instance IDs, and Transform position seeds were rejected by deterministic RNG mandate.
Scalability potential: Low keeps gameplay nodes stable and prunes visual-only clusters through `GlobalQualityWeight`; Ultra adds cosmetic cluster variants after the gameplay resource type is fixed.
Hardware Impact: Integer LCG/hash costs a few integer ops per node and avoids managed RNG object state; estimated save/load improvement depends on depleted-node hash count rather than world node count.

## Decision 02 - AUP And GPU Locality

Problem: Absolute `float3` coordinates for resource matrices will jitter at world edges and corrupt render placement.
Solution: Store resource authority by sector/AUP-derived hash and compose GPU matrices from camera-relative local deltas only after subtracting camera AUP. No shader-side absolute coordinate reconstruction.
Rejected Alternatives: Transform.position authority, global shader offsets, and raw absolute float uploads were rejected due to AUP precision mandates.
Scalability potential: Low renders stable local matrices in the active sector grid; Ultra extends residency and cluster fidelity without changing authority hashes.
Hardware Impact: Matrix upload stays 64 bytes per rendered instance; DTO authority data remains cache-aligned, avoiding unaligned ARM64 reads.

## Route Card - GEOLOGY_RESOURCE_NODE_VAULT

Route ID: GEOLOGY_RESOURCE_NODE_VAULT
Date: 2026-05-19
Owner: SHINOBU_153
Owner domain: Echelon 2 World Generation
Owning file/system: `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs` plus `ProceduralGeologyContracts.cs`

Problem: Procedural geology output crosses generation, render, interaction, save-depletion, and crash-telemetry boundaries.
Why owner-local data is insufficient: Render and interaction systems need read-only generated node snapshots, and save needs depleted hash reconciliation.
Why direct caller/owner interface is insufficient: Multiple consumers need stable native snapshot semantics and crash reconstruction; one direct caller is not the actual topology.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [ ] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer phase: SIMULATION / VISUAL_SYNC handoff, exact dispatcher path pending archaeology.
Consumer phase: Interaction query in simulation, render upload in VISUAL_SYNC, save depletion query on save/load.
Cadence: New sectors on paging events; render upload dirty-sector only.
Expected max events/reads per frame: one active-sector generation job and one visual-sync matrix upload when sector/terrain/depletion dirties.
GlobalQualityWeight behavior: Continuous cosmetic cluster density and sector prewarm budget; gameplay authoritative nodes remain stable.

Payload/data shape: `ResourceNodeDTO` 128-byte unmanaged explicit layout; telemetry 64-byte unmanaged explicit layout.
Managed fields present: no.
UnityEngine.Object fields present: no.
Layout proof: planned `UnsafeUtility.SizeOf` / `OffsetOf` editor assertion.
Capacity: default 2048 nodes, clamp 64-16384, active buffer IDs 71530-71547. `CsvScratch` 71541 is reserved for the CSV lane but not requested in the current runtime boot path.
Overflow/failure mode: stop writing at capacity, set telemetry overflow flag, preserve authoritative core nodes before visual-only nodes.

Telemetry fields: sector hash, generated count, depleted cull count, overflow flags, finite flags, elapsed microseconds estimate.
Black-box fields: last 300 generation summaries, active sector hash, node count, error flags.
Profiler marker: planned static marker if local profiler pattern exists.
GC proof required: static scan plus Unity GCMonitor later; local CLI is not runtime proof.

Shutdown/disposal rule: Vault owner releases buffers through existing vault route; no local persistent NativeArray ownership unless proven owner-local.
Scene unload behavior: active sector page count returns to baseline; pending jobs fenced by owner.
Stale-handle behavior: generation id mismatch rejects external read.

Rejected alternatives:
  [ ] owner-local field
  [ ] cached owner interface
  [ ] existing SignalBus lane
  [ ] existing Vault buffer
  [x] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk: one owner writes; external systems consume read-only snapshots/interfaces, not concrete geology classes.
H-Phi impact expected: native DataVault surface increases only if source archaeology proves no existing vault buffer can own the data.
Runtime proof required before acceptance: Unity compile/import, Play Mode sector paging smoke, GCMonitor 0 B, profiler timings, Frame Debugger for draw path.
Reviewer: Integrator / Save Archivist / World Streaming owner
Status: IMPLEMENTED, active procedural geology is data-only; full legacy `ResourceNode` deletion and multi-sector residency remain owner-contract blocked.

## Decision 03 - Vault ABI And ResourceNodeDTO

Problem: Existing ore generation owned persistent `NativeArray` buffers and no fixed ABI payload for resource nodes. That makes cross-system consumption brittle and violates DataVault sovereignty.
Solution: Added `ResourceNodeDTO` at 128 bytes with explicit offsets: `float4x4 LocalMatrix` 0, `uint ResourceTypeHash` 64, `float YieldRemaining` 68, `double3 SectorAUP` 72, and explicit padding 96-127. Added Vault buffer IDs 71530-71547 for nodes, SoA scan arrays, matrices, telemetry, mock terrain, rules, tuning, self-audit, candidate slots, Vault depletion cache, and sector hash grid.
Rejected Alternatives: Extending `ResourceNode` MonoBehaviour or storing metadata fields after offset 96 was rejected because the prompt mandates padding there and GameObject identity is not deterministic enough for rollback.
Scalability potential: Low reads the same authoritative core DTOs with fewer visual-only matrices; Middle/High/Ultra increase cosmetic matrix count and debug/tuning fidelity without changing core node identity.
Hardware Impact: Vault uninitialized buffers remove owner-local persistent allocations and bulk zeroing. Expected low-end gain on i3/MX350 is dominated by eliminating GameObject proxy creation and cutting visual matrix count under low `GlobalQualityWeight`.

## Decision 04 - Legacy GameObject Resource Path

Problem: `ResourceDistributionDirector` and `ResourceNode` still maintain GameObject/List-based resource workflows used by metamorphism, spatial grid, and legacy save compatibility. `ProceduralOreSpawner` also still carried proxy arrays and an `ICuttable` bridge after the first pass.
Solution: Physically removed `ProceduralOreSpawner` proxy `GameObject`, `MeshCollider`, `ICuttable`, `ActiveProxyCount`, hydration constants, and `Hecton8.Gameplay` dependency. Full legacy file deletion remains `[BLOCKED BY DEPENDENCY]`. Generated geology now exists as Vault DTO math until another owner migrates interaction/metamorphism off `ResourceNode`.
Rejected Alternatives: Deleting `ResourceNode.cs` or `ResourceDistributionDirector.cs` was rejected because compile references prove direct dependencies in world distribution, construction/scavenging interaction, and tombstone reconciliation.
Scalability potential: Low has zero ore proxy hydration; Middle/High/Ultra spend cycles on GPU matrices, not scene hierarchy. Legacy special-case resource directors remain outside this patch until their owners expose math-query interfaces.
Hardware Impact: Avoids 24-proxy cold pool plus MeshCollider/BakeMesh path in procedural ore runtime and removes a sibling gameplay compile-wall edge. On i3/MX350 this removes visible hitch risk from near-player ore hydration; exact gain depends on previous proxy churn.

## Decision 05 - Deterministic Generation And AUP Locality

Problem: The old job used `FloatMode.Fast`, absolute `float3` positions, and quality-tier branches that changed gameplay node count.
Solution: Replaced it with `GenerateResourceNodesJob` using deterministic Burst, LCG/hash sector seeding, candidate-slot depletion, camera-AUP subtraction before float cast, normal-aligned matrices, and compact render output.
Rejected Alternatives: `UnityEngine.Random`, `System.Random`, `FloatMode.Fast`, absolute float coordinates, and compact index persistence were rejected because they break replay, AUP precision, or depletion identity.
Scalability potential: Core gameplay candidates are independent of hardware tier. Low prunes visual-only clusters through a continuous curve; Middle adds sparse cluster crystals; High keeps dense clusters; Ultra reaches 5 visual matrices per core node.
Hardware Impact: Integer RNG and compact writes are cache-stable. Visual-only pruning can remove up to 5 matrix uploads per core node on weak devices, converting saved CPU/GPU time into visual density on high-end devices.

## Decision 06 - Terrain And Biome Inputs

Problem: The voxel/biome systems are not safe to depend on directly in this patch, but generation requires grounding and resource distribution.
Solution: Added `GenerateMockTerrainSDFJob` for deterministic height/normal fallback and a cold `ReadOnlySpan<byte>` CSV parser that mutates unmanaged distribution rules in Vault. Existing MapMagic payload is still consumed when valid.
Rejected Alternatives: Blocking on Voxel Engine Agent 12, managed string dictionaries, and runtime CSV parsing were rejected. Direct Biome Transition Manager dependency was deferred because no safe owner contract was present in the inspected files.
Scalability potential: Low uses cheap triangle-wave mock or quantized height samples; Middle samples distribution rules; High/Ultra can feed richer biome/depth rules through the same DTO table.
Hardware Impact: Mock terrain is 1024 samples and deterministic; it avoids runtime stalls waiting for terrain provider readiness. CSV parser is cold-only, so hot path remains zero managed allocation.

## Decision 07 - Compile Gate

Problem: Project rules forbid launching dotnet build while CPU is above 50% or another `dotnet`/`csc` compile is active.
Solution: Checked processes and CPU before build. Earlier CPU remained 89.88-100%; the polish pass then saw seven active `dotnet` processes and CPU 100%, so no build was launched. Static scans were used instead.
Rejected Alternatives: Forcing `dotnet build` under load was rejected because it violates the batch rule and creates false compile-wall noise for 20+ active agents.
Scalability potential: Not runtime-facing.
Hardware Impact: Avoided adding compiler load during system saturation. Latest guard found no `dotnet`/`csc` process but CPU measured 100%, so compile verification remains pending until CPU stays below 50% and no `dotnet`/`csc` build process is active.

## Decision 08 - Vault Depletion Cache

Problem: The first pass still used a private persistent `NativeParallelHashMap<ulong, ulong>` for session depletion words. That violates the Vault law even though persisted truth remains `ResourceDepletionDeltaSignal`.
Solution: Replaced the local native map with Vault-owned open-address arrays: `DepletionCacheKeys` 71544, `DepletionCacheMasks` 71545, and `DepletionCacheCount` 71546. The active job still receives a compact `DepletionMasks` lane; depletion writes update the Vault cache and emit the existing global signal.
Rejected Alternatives: Keeping `NativeParallelHashMap` was rejected because it is owner-local persistent memory and adds allocator fragmentation. A direct dependency on Save Archivist/Scavenging Oracle was rejected because no safe owner contract was present and would break parallel agent isolation.
Scalability potential: Low/Middle/High/Ultra all use the same deterministic core resource identities. Low pays only O(active words) mask hydration; Ultra can keep richer visual matrices without expanding save state.
Hardware Impact: Removes one persistent native allocation from geology runtime. The 4096-slot cache is 64 KiB keys + 64 KiB masks + 4 B count in Vault, with predictable linear-probe access and no managed or native container allocation.

## Decision 09 - AUP Sector Hash Grid

Problem: Task 13 required AUP-to-sector mapping, but mutating concrete world streaming residency would cross owner boundaries and risk compile-wall coupling.
Solution: Added Vault buffer `SectorHashGrid` 71547 with the 3x3 hash grid around the player sector. The active sector still schedules one async `GenerateResourceNodesJob`; future streaming ownership can consume the hash grid without depending on `ProceduralOreSpawner` internals.
Rejected Alternatives: Allocating per-sector `NativeArray` pages inside this component was rejected because it would duplicate the streaming owner's lifecycle. Directly editing `WorldChunkResidencyManager` was rejected as cross-domain sabotage without a route card.
Scalability potential: Low devices keep one active resource buffer and use the 3x3 hashes as cheap prewarm hints. Middle/High/Ultra can expand residency through a world-streaming owner without changing resource identity math.
Hardware Impact: Adds 72 B of Vault resident hash-grid data and no new scheduled job. It replaces concrete sector-page memory speculation with a bounded handoff surface.

## Decision 10 - Lifecycle Fence And CSV Identity Repair

Problem: Static reread found two non-obvious failure modes: disabling the component while a generation job is in flight could unregister the late-frame owner before the job unlocked Vault buffers, and CSV resource tokens were parsed into FNV hashes that do not match `WorldOreTypeIds` expected by radar/inventory consumers. The XML also explicitly required `Unity.Mathematics.Random`, while the user mandate required an LCG.
Solution: `OnDisable` now unregisters slow tick only, marks pending output for discard, and keeps/requests late-frame ticking until `TryCompleteFinishedSpawnJob()` can unlock Vault buffers without a blocking `Complete()`. CSV items now map known tokens/numeric ids to stable ore ids 1-4 and reject unknown items cold. The job creates a `Unity.Mathematics.Random` per deterministic slot from AUP sector hash/world seed/slot, then uses its first uint to seed the LCG stream that drives placement/type rolls.
Rejected Alternatives: Blocking `JobHandle.Complete()` in `OnDisable` was rejected because it can stall a frame during sector generation. Keeping arbitrary CSV FNV resource ids was rejected because GPR clamps filters to `WorldOreTypeIds.None..Silver` and inventory hash resolution expects the same ids. Replacing the LCG with Unity's RNG for all rolls was rejected because the primary user directive named the LCG generator as the core algorithm.
Scalability potential: Low devices avoid long-lived Vault locks when sectors are disabled during streaming churn. Middle/High/Ultra keep identical deterministic resource identity; richer CSV rule sets can be introduced without changing hot job branches.
Hardware Impact: Adds one deterministic `Unity.Mathematics.Random.CreateFromIndex`/`NextUInt` seed step per candidate, still allocation-free and Burst-safe. Preventing a leaked Vault lock avoids worst-case frame stalls and false contention under multi-agent streaming tests on i3/MX350.

## Decision 11 - Hot Registry And Blackbox Cadence

Problem: `EnsureNativeState()` still read `GlobalRegistry.DataVault` from slow/late tick paths, and the black-box ring was generation/event oriented instead of a true last-300-frame forensic trail. Writing frame telemetry naively would scan ore lanes each frame and create its own 0.1 ms risk.
Solution: `EnsureNativeState()` now uses cached `_dataVault` after cold allocation and only falls back to cold `AllocateNativeState()` if the cached vault/view path fails. The first live ore position/hash is cached after spawn commit and depletion, so `LateFrameTick()` can write O(1) telemetry every frame. Duplicate normal samples in the same frame are skipped, while event samples still write. Drop-pod distance weighting now subtracts AUPs first, clamps, casts to local `float3`, and computes `lengthsq`.
Rejected Alternatives: Keeping service-locator reads in tick was rejected by the Global Authority boundary rule. Scanning `OreTypes` every frame to find the first live node was rejected as black-box self-sabotage. Absolute double-distance gameplay weighting was rejected because the AUP mandate requires localized delta math before physics/gameplay distance calculations.
Scalability potential: Low devices get frame-level forensic visibility without O(n) telemetry scans. Middle/High/Ultra keep the same telemetry shape while richer visual clusters only alter cached counts and hashes.
Hardware Impact: Removes one registry lookup per slow/late tick and replaces per-frame first-node scans with cached scalar fields. Added telemetry cost is one 64-byte Vault write per frame, bounded and cache-local. The editor tuner now formats only on telemetry-frame changes using one reused `StringBuilder`; UI Toolkit still receives a managed string, so this is explicitly editor-only churn reduction, not runtime zero-GC proof.

## Decision 12 - Vault Owner ID

Problem: Static import review found `SystemID.WorldStreaming` in the SHINOBU_153 Vault request/lock path. That enum member does not exist in `GlobalRegistryContracts`, so Unity import would fail before runtime verification.
Solution: Use the existing owner id `SystemID.WorldResourceSpawnerRuntime` for all procedural geology Vault buffer acquisition, job locks, unlocks, and editor tuning writes.
Rejected Alternatives: Adding a new enum value to `GlobalRegistryContracts` was rejected because it edits a massive core contract for a local geology fix and violates rebuild protection. Using `SystemID.Unknown` was rejected because `GlobalDataVault` explicitly treats unknown allocation owners as fatal.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is ownership correctness for the Vault route.
Hardware Impact: No frame-time cost. Prevents a compile-wall failure and preserves clean owner telemetry for Vault lock auditing.

## Decision 13 - Tuning Row Authority

Problem: The editor facade wrote Vault tuning values, but runtime sanitization still overwrote cluster spread, normal tolerance, and visual cluster density from serialized inspector fields. That made the UI Toolkit facade partially decorative instead of authoritative.
Solution: Treat the Vault `GeologyTuningDTO` as the cold control row after initialization. Runtime now defaults invalid/uninitialized rows, clamps all tuning values, writes them back, and passes the sanitized Vault values into `GenerateResourceNodesJob`. Small control/telemetry buffers use clear-memory or explicit initialization; large resource/matrix/cache lanes keep uninitialized allocation semantics.
Rejected Alternatives: Keeping inspector fields as the source of truth was rejected because it forces recompilation/scene edits for tuning and violates the human-control mandate. Blindly trusting uninitialized one-row DTO memory was rejected because random finite bits could become density/sector parameters.
Scalability potential: Low devices can tune down visual-only density and spread without changing gameplay resource identity. Middle/High/Ultra can raise visual density through the same Vault row without code changes.
Hardware Impact: No hot allocation. The job reads already-sanitized scalar fields; the saved cost is avoiding failed balancing cycles and avoiding random uninitialized control-state faults.

## Decision 14 - Data-Only Interaction Command Route

Problem: Removing the proxy `ICuttable` path erased the old GameObject-mediated mining hook. Leaving only a private `MarkDepleted` method would force future interaction code either to reintroduce proxies or to depend on concrete `ProceduralOreSpawner`.
Solution: Added `IWorldResourceSpawnerCommandModel` beside the existing World resource read model. It exposes `TryMarkOreDepleted(int, out uint, out uint, out float3)` with primitive outputs only. `ProceduralOreSpawner` implements it and keeps depletion authority owner-local, including mask updates, signals, and visual-cluster clearing.
Rejected Alternatives: Re-adding `ICuttable`, colliders, or concrete mining references was rejected because it recreates the MonoBehaviour spawner path Task 01 removed. Putting signal DTOs into World.Contracts was rejected because that asmdef intentionally references only Collections/Mathematics.
Scalability potential: Low devices consume the same sparse index route without collider hydration. Middle/High/Ultra can layer richer VFX on returned primitive position/hash data without changing resource authority.
Hardware Impact: Avoids per-resource collider/proxy allocation. Depletion remains O(render window) only when clearing visual-only cluster siblings for the deterministic slot; normal query and command data are primitive and allocation-free.
