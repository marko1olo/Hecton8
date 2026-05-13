# Rationale_BASE_DECONSTRUCTION_SYS

Status: PENDING VERIFICATION

## Intake
Problem: Base modules have no safe deconstruction path; raw `Destroy(gameObject)` risks stale graph/save/power/fluid/spatial references.
Solution: Build a signal-driven habitat deconstruction system registered behind an interface, with validation before rollback and pooled release after graph unregister.
Rejected Alternatives: Direct tool-to-manager calls and raw GameObject destruction are too coupled for parallel agents and unsafe for save graph integrity.
Scalability potential: Low uses visual warning plus optional DFS skip; Middle uses DFS validation; High and Ultra can add richer ghost/dissolve feedback while keeping rollback deterministic.
Hardware Impact: Expected low-end i3/MX350 gain comes from pooled module return, bitwise refund math, and preallocated native DFS containers; no measured profiler proof yet.

## Loop 1 - Purge And Signal Ownership
Problem: Player tools called module destruction behavior directly, which mixed tool UX, refund mutation, graph unregister, and object lifetime in one unsafe path.
Solution: Moved deconstruction authority behind `IHabitatDeconstructionSystem`; tools now publish `DeconstructRequestSignal` with AUP target/ray data and the construction owner drains requests late-frame.
Rejected Alternatives: Keeping `BaseModule.Deconstruct(PlayerInventory)` as the actual execution path was rejected because it could not preflight graph rollback, inventory capacity, and pool availability before mutation.
Scalability potential: Low/MX350 gets the same authoring path with cheaper validation; High/Ultra can listen to the same result/delete signals for richer dissolve and power-grid feedback.
Hardware Impact: Expected 35-65 us saved on low-end input frames by removing tool-to-graph scans and replacing direct calls with a bounded NativeQueue signal.

Problem: Legacy deconstruction refunded per item and despawned without graph-native checks.
Solution: `ConstructionManager` now resolves build data, preflights inventory capacity, applies `Cost >> 1`, emits `ItemAcquiredSignal`, unregisters the module, and only then returns it to pool.
Rejected Alternatives: World-drop fallback first was rejected because it hides full-inventory failure and creates spawned objects during a rollback transaction.
Scalability potential: Low uses HUD rejection and minimal debris signal; High/Ultra can add shader dissolve listeners without changing rollback semantics.
Hardware Impact: `>> 1` and batch quantity adds avoid per-unit loop work; expected gain is 10-80 us depending on cost stack count.

## Loop 2 - Rollback DFS
Problem: Removing a corridor can isolate a base island or leave dependent window modules floating with stale flood/power topology.
Solution: `HabitatGraphManager.TryValidateDeconstructionRollback` checks dependent room connections, then runs a Burst DFS over the existing CSR edge arrays while skipping the removed node.
Rejected Alternatives: Recursive managed graph traversal and delete-then-repair were rejected because they allocate, risk stack depth, and create one-frame invalid topology.
Scalability potential: Low/MX350 skips isolation DFS and flags the result; Mid/High/Ultra runs DFS and can spend saved low-tier cycles on stronger visual feedback.
Hardware Impact: Persistent `NativeList<long>` and `NativeParallelHashSet<long>` target 0 B GC/request; low-tier skip saves roughly 35-110 us on medium bases.

## Loop 3 - Failsafe And Pool Contract
Problem: A successful visual deconstruction could still fail late if the inventory was full or the object was not owned by a pool.
Solution: Inventory and pool are both preflighted before `TryBeginAuthoritativeDeconstruction`; `ObjectPoolManager.CanDespawnWithoutDestroy` rejects unpooled instances.
Rejected Alternatives: Calling `Object.Destroy` as fallback was rejected because pooled save/graph systems would retain stale AUP and entity references.
Scalability potential: Low shows rejection through HUD signal; High/Ultra can layer tool-specific feedback from the same `DeconstructResultSignal`.
Hardware Impact: Pool preflight is a marker/hash lookup, roughly 6 us; it avoids expensive destroy, GC, and later recovery work.

## Loop 4 - Cinematic Cheat Path
Problem: Deconstruction wants water displacement, structural debris, and module death feedback without running actual fluid or fracture simulation.
Solution: Reset flood/leak/drain state, zero water volume, emit a disintegrate `DebrisSpawnSignal`, and force downstream cold-tick invalidation with `ModuleDeconstructSignal` flags.
Rejected Alternatives: Runtime mesh fracture and fluid displacement were rejected as suspicious >0.1 ms work for a tool-triggered rollback.
Scalability potential: Low gets a cheap fake; High/Ultra can attach richer shader dissolve and debris listeners to the same signal lane.
Hardware Impact: Expected 80-200 us saved by avoiding physical displacement and object instantiation.

## Loop 5 - Black Box And Verification
Problem: If rollback rejects after NaN/AUP corruption, there must be enough recent state to diagnose it.
Solution: Added a fixed 300-entry native black box for deconstruction result/reason/DFS counts and dump path `Docs/AgentLogs/Dump_BASE_DECONSTRUCTION_SYS.bin` on AUP validation failure.
Rejected Alternatives: Managed logs-only telemetry was rejected because it can allocate and disappears under crash timing.
Scalability potential: Low carries compact counters only; High/Ultra can expand listeners externally without changing the core ring buffer.
Hardware Impact: Persistent native ring buffer costs fixed memory only; request write is O(1), expected below 2 us.

Problem: Compile verification is currently blocked before patched files are reached.
Solution: Ran three verification lanes: `dotnet build Hecton8.Core.csproj`, `dotnet build Assembly-CSharp.csproj`, and Unity batchmode. The first two fail on stale/missing generated asmdef project references; batchmode is blocked by the open interactive Unity lock.
Rejected Alternatives: Editing generated `.csproj` references was rejected as cross-agent metadata churn outside the deconstruction task.
Scalability potential: Not runtime applicable; this is integration health.
Hardware Impact: No runtime impact; integrator must regenerate Unity project files or compile in the already-open editor.

## OMEGA POLISH CHANGES
Problem: The polish audit required checking for honest expensive math, managed iteration, string formatting, unconditional normalization, and domain leakage.
Solution: Ran source/diff scans across touched files for `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, `math.normalize`, `Mathf.Sqrt`, new managed collections, interpolated strings, and literal divisions. New deconstruction math uses `math.rsqrt`, bit shifts, persistent native containers, and `for` loops.
Rejected Alternatives: Adding a secondary managed analysis layer or editor-only validator was rejected because the project compile path is already blocked by generated assembly references.
Scalability potential: Low/MX350 uses DFS skip and cheap signal/VFX fakes; Mid/High/Ultra keeps deterministic DFS and can attach visual-overkill listeners without touching rollback.
Hardware Impact: No additional runtime cost from the polish pass. Kept expected 0 B GC per deconstruction request in the rollback validation path.

Problem: The old BaseModule body remained textually present during the first purge pass, which would fail a dead-code audit even if preprocessor-disabled.
Solution: Removed the inactive legacy refund/despawn block entirely; `BaseModule.Deconstruct` now only enqueues through the service.
Rejected Alternatives: Keeping `#if false` burial was rejected because grep-based dead-code audits still see the old path.
Scalability potential: All tiers now have a single deconstruction authority path.
Hardware Impact: Avoids accidental reactivation of per-item world-drop loops and raw manager calls.

Cinematic Cheats Used:
- Water displacement is a reset of flood/leak/drain state and `waterVolumeM3 = 0f`, not a fluid solve.
- Deconstruction visuals are `DebrisSpawnSignal(Disintegrate)` and optional shared-material ghosting, not instantiated fracture/debris objects.
- Low-tier rollback uses a Math LOD skip flag instead of running DFS on weak hardware.

Domain Boundary Justification:
- `Core/GlobalSignals.cs`, `Core/GlobalRegistry.cs`, and `Core/GlobalRegistryContracts.cs`: required cross-domain interface/signal contract surface.
- `PlayerInventory.cs`: one preflight wrapper exposing existing inventory simulation without mutation.
- `ObjectPoolManager.cs`: one pool-ownership preflight to prevent `Destroy` fallback.
- `PlayerBuilder.cs` and `LaserCutter.cs`: tool producers converted from direct module calls to signal producers.

Final Diff Summary:
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: deconstruction request/result/delete signal lanes.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `IHabitatDeconstructionSystem` and registry slot.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: habitat deconstruction service registration/resolution.
- `Assets/_Project/Scripts/ConstructionManager.cs`: authoritative deconstruction validation, refund, VFX, pool return, black box.
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`: adjacency and DFS rollback validation.
- `Assets/_Project/Scripts/BaseModule.cs`: service enqueue, preview, pool reset, hosted content ejection.
- `Assets/_Project/Scripts/PlayerBuilder.cs` and `Assets/_Project/Scripts/LaserCutter.cs`: signal-based tool requests and preview.
- `Assets/_Project/Scripts/PlayerInventory.cs`: capacity preflight wrapper.
- `Assets/_Project/Scripts/ObjectPoolManager.cs`: pooled-despawn preflight.

## Follow-Up Hardening Pass
Problem: The DFS visited container used `NativeHashSet`, while the project memory sentinel and surrounding systems standardize on `NativeParallelHashSet`.
Solution: Switched rollback DFS visited state to `NativeParallelHashSet<long>` and registered/refreshed/unregistered it through `NativeMemorySentinel`.
Rejected Alternatives: Leaving it untracked was rejected because the Black Box mandate requires owned native memory to be visible and disposable.
Scalability potential: Low tiers still skip DFS; Mid/High/Ultra keep native deterministic DFS with tracked memory.
Hardware Impact: Same 0 B GC target; memory tracking adds no per-request allocation.

Problem: Tool UI and logs reported recovery completion immediately after enqueue, before graph/inventory/pool validation could reject.
Solution: Builder and laser cutter now report recovery queued; authoritative success remains owned by `ConstructionManager` result/delete signals.
Rejected Alternatives: Keeping optimistic completion logs was rejected as fake reporting.
Scalability potential: All tiers share the same truth model; high-tier presentation can later listen to result snapshots without touching rollback.
Hardware Impact: No hot-path runtime cost; removed dead archive string construction from laser recovery.

Problem: Inventory preflight checked refund item types independently, which could overpromise capacity for mixed non-stackable refunds.
Solution: Added `PlayerInventory.CanAcceptItemQuantityBatch` and stack-allocated grouped refund spans in `ConstructionManager` so the whole refund set is simulated in one grid pass before mutation.
Rejected Alternatives: Allocating dictionaries/lists for grouping was rejected; per-item preflight was rejected because it was not a full transaction preflight.
Scalability potential: Low/MX350 keeps cheap stackallocated grouping; High/Ultra can afford the same exact grid simulation for correctness.
Hardware Impact: 0 B GC; cold-path estimate 20-120 us depending on inventory grid and cost count.

Problem: Delete marker ordering and pool preflight had edge cases.
Solution: Capture module hash/node id before mutation, unregister graph first, publish `ModuleDeconstructSignal`, then return to pool; `ObjectPoolManager.CanDespawnWithoutDestroy` now guards null pool lookup state.
Rejected Alternatives: Emitting delete markers before graph removal and assuming pool dictionaries exist were rejected.
Scalability potential: Downstream save/power/logistics listeners receive a cleaner rollback sequence on all tiers.
Hardware Impact: Hash/node capture is O(1); null guard is negligible.
