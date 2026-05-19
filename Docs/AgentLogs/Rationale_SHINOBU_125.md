# Rationale_SHINOBU_125

Date: 2026-05-19
Status: IMPLEMENTED / UNITY COMPILE PENDING

## Decision 01: Retire Missing-XML Blocker

Problem: Earlier extraction found no `SHINOBU_125` XML block. The current `Docs/Tasks/CURRENT_BATCH.md` now contains a full 20-task `<AGENT_PROMPT id="SHINOBU_125">` block.

Solution: Promote the extracted XML to authoritative scope and reset the status matrix to 20 tasks. Batch protocol remains satisfied because the block was extracted cover-to-cover with CLI regex.

Rejected Alternatives: Continuing the old blocker was false against current disk state. Using chat text alone was rejected because the XML contains exact DTO layout, Burst, Vault, telemetry, and editor obligations.

Scalability potential: Enables implementation of loot math that scales independently of scene object count. Low: direct inventory signal and 0.1 VFX multiplier. Middle: normal fake particles. High: denser icon/VFX presentation. Ultra: richer VFX consumers can spend the saved PhysX budget without changing loot truth.

Hardware Impact: Process only. Runtime impact starts when `ResourceNode` stops spawning rigidbody loot.

## Decision 02: Same-Assembly Bridge Instead Of Sibling Runtime Reference

Problem: `ResourceNode` and `PlayerInventory` live under the existing `Hecton8.Core` root assembly. Putting the oracle only in `Hecton8.Gameplay.Loot.Runtime.asmdef` would require a direct Core-to-sibling gameplay reference or a broad file move, both of which expand the compile wall.

Solution: Implement the loot oracle in namespace `Hecton8.Scavenging` inside the same existing root assembly and route cross-owner facts through typed `SignalBus<T>` and `GlobalDataVault` handles. No new sibling asmdef reference is added.

Rejected Alternatives: Adding `Hecton8.Gameplay.Loot.Contracts` as a root assembly dependency was rejected as direct sibling coupling. Moving `ResourceNode` into a new asmdef was rejected as high-risk batch churn across many authoring/editor references.

Scalability potential: Low devices get no new assembly dependency or scene object path. Middle/High/Ultra can extend the visual consumer of `VisualScavengeSignal` without touching loot truth.

Hardware Impact: Compile-wall preservation is iteration-time impact, not frame-time. Runtime saves come from deleting ore chunk spawn/PhysX.

## Decision 03: Vault-Owned Flat Loot Tables

Problem: The DataMonolith payload `loot_distribution_tables.h8bin` is not proven wired by the binary ledger, and a hard dependency would crash isolated tests.

Solution: Request Vault buffers with numeric owner IDs for entries, requests, yields, biome modifiers, telemetry, audit, and CSV scratch. Fill `LootTableEntryDTO` with a deterministic emergency CDF if the baked payload is absent.

Rejected Alternatives: Managed `Dictionary<string,LootTable>` and `List<LootEntry>` fallbacks were rejected by Zero-GC and deterministic rollback mandates. `NativeArray` private persistent fields were rejected by Vault law.

Scalability potential: Low: 4-entry emergency CDF. Middle: baked flat table. High/Ultra: larger tables and biome modifiers still remain contiguous memory scans.

Hardware Impact: i3/MX350 avoids managed lookup and heap churn. Estimated 5-30 us saved versus dictionary-based loot resolution; larger win when eliminating spawned rigidbodies.

## Decision 04: Dear Lie Inventory Route

Problem: Depleted resource nodes currently spawn pooled loot prefabs, apply rigidbody impulse/torque, and despawn later. This spends CPU on a physical truth that the requested UX does not need.

Solution: Publish `ItemAcquiredSignal` plus `VisualScavengeSignal`. The item enters inventory by data signal; visuals receive AUP, item hash, quantity, and `VfxEmissionMultiplier` for fake particles/icon flight.

Rejected Alternatives: ObjectPoolManager ore chunks were rejected because they still wake transforms and rigidbodies. Direct inventory mutation inside the resolver was rejected because inventory is the owner of inventory state.

Scalability potential: Low: icon fly and 0.1 emission. Middle: modest sprite/particle count. High: richer GPU particles. Ultra: VFX layer can overdraw stylized loot trails while CPU stays constant.

Hardware Impact: Removes per-loot Rigidbody force/torque and pool spawn path. Estimated 50-500 us saved per depleted node on i3/MX350, plus reduced GC/Transform pressure.

## Decision 05: Deterministic Integer CDF

Problem: Float-weight probability and platform RNG drift can desync rollback clients.

Solution: Store cumulative integer weights in `LootTableEntryDTO.DropWeight`. Seed `Unity.Mathematics.Random` from AUP fields, `SessionID`, table version, and roll index, then map `NextUInt()` to a threshold with multiply-high integer scaling.

Rejected Alternatives: `UnityEngine.Random`, `System.Random`, modulo `% totalWeight`, and float normalized probabilities were rejected for determinism and modulo-bias reasons.

Scalability potential: Same loot math across all tiers; quality affects only visual emission. Biome modifiers can be applied as integer milli-scalars without heap allocation.

Hardware Impact: Integer CDF with small flat arrays targets <5 us per interaction on low-end silicon.

## Decision 06: Core Late-Frame Publish Fence

Problem: Writing to `SignalBus<T>` from Burst jobs without a known completion fence risks racing the SignalBus snapshot flush. Completing inside `ResourceNode.TakeDamage()` would stall the gameplay interaction path.

Solution: `ResourceNode` only queues a request. `ScavengingLootOracleRuntime` owns a Core late-frame fence, schedules fallback/table resolution/publish jobs as a batch, and completes the tiny publish chain before the next signal flush can read the queue.

Rejected Alternatives: Fire-and-forget `JobHandle` was rejected because SignalBus could read while a `NativeQueue.ParallelWriter` job is still writing. Direct main-thread `SignalBus.Push` was rejected because Task 09 requires `PublishLootYieldsJob` via `NativeQueue.ParallelWriter`.

Scalability potential: Low: at most 64 queued requests and one small late-frame batch. Middle/High/Ultra: visual signal capacity scales from 64 low-tier to 512 high-tier; loot math stays constant.

Hardware Impact: Moves the possible wait out of depletion logic. Expected batch work remains below 5 us per request; Unity profiler proof is pending.

## Decision 07: Inventory-Full Node Preservation

Problem: If inventory rejects after depletion, the player loses the node and the save layer can record a false depletion.

Solution: `ResourceNode` preflights `PlayerInventory.CanAcceptItemQuantity()` and passes `InventoryCapacityDTO` into the request. The Burst job aborts RNG on zero capacity and emits a hash-only `HUDNotificationSignal`; `ResourceNode` returns false so health/depletion roll back.

Rejected Alternatives: Resolve loot first then reject was rejected because it mutates RNG/audit state for a non-event. Spawning world loot overflow was rejected because the assignment explicitly removes physical drops.

Scalability potential: Same behavior all tiers; VFX is suppressed because no `VisualScavengeSignal` is emitted on full inventory.

Hardware Impact: Saves CDF/RNG work on full inventory and prevents extra save deltas.

## Decision 08: Compile Gate Obeyed

Problem: Project rules forbid dotnet/C# compile while CPU is under load or dotnet/csc is running. Current CPU counter returned 100%.

Solution: Do not launch `dotnet build`. Use static scans and `git diff --check` now; leave Unity/C# compile as pending until CPU is under 50% and no compiler process is active.

Rejected Alternatives: Running build under 100% CPU was rejected because it violates explicit project hardware-protection rules.

Scalability potential: No runtime effect.

Hardware Impact: Prevents workstation contention during active load; 0 us runtime.

## Decision 09: PlayerInventory Pack=1 Cleanup

Problem: Scoped static scan found `CraftReservation` using `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 12)` and two inventory blackbox entries using explicit layout with `Pack = 1`. They are not the loot DTO, but the file is in the inventory handoff path and Pack=1 is banned by the ARM64 mandate.

Solution: Convert `CraftReservation` to explicit 16-byte layout with int fields at offsets 0, 4, 8 and explicit `_pad0` at 12. Remove Pack=1 from the already-explicit 64-byte and 32-byte inventory telemetry entries.

Rejected Alternatives: Leaving Pack=1 was rejected because the mandate is categorical and future edits could add sub-4-byte fields into an unaligned DTO.

Scalability potential: Same behavior all tiers. The 16-byte stride is friendlier to ARM64/NEON cache access.

Hardware Impact: Prevents possible unaligned access expansion on i3/MX350-class and ARM64 CPUs; no measurable gameplay cost expected from one extra int in reservation scratch arrays.

## Decision 10: Reserve Scavenging SourceKind 13

Problem: The first implementation used `ItemAcquiredSignal.SourceKind = 9`. Existing manual pickups already use source-kind 9 through `InventoryPickupSignalConstants.ItemSourceManualPickup`. `PlayerInventory` draining source-kind 9 would double-consume manual pickup signals that were already handled by pickup interaction code.

Solution: Move scavenging direct-inventory acquisition to source-kind 13 and filter that value in `PlayerInventory` without importing the scavenging runtime namespace. Static scan now shows 13 only in `ScavengingLootOracle` and the inventory consumer; manual pickup remains 9, loot magnet 8, voxel carve 12, fabricator 4.

Rejected Alternatives: Reusing source-kind 9 was rejected as a correctness bug. Importing `Hecton8.Scavenging` into inventory was rejected because it creates concrete reverse coupling from inventory to the scavenging runtime surface.

Scalability potential: Low/Middle/High/Ultra all keep the same signal lane; only the VFX consumer scales presentation. The source-kind split prevents extra inventory writes on all hardware.

Hardware Impact: Correctness-first change. Runtime cost is a single byte compare already present in the loop.

## Decision 11: Stable AUP Roll Index For Resource Nodes

Problem: Runtime resource-node requests used a local `_requestSequence` as `RollIndex`. That made the same AUP/type/session/table produce different loot if two clients queued resource nodes in different local orders.

Solution: Set runtime resource-node `RollIndex = 0u`. The deterministic seed still includes AUP, `SessionID`/world seed, table version, and resource hash. `MockHarvestRequestJob` and self-audit still use nonzero roll indices because they intentionally simulate multiple independent rolls.

Rejected Alternatives: Keeping request-order entropy was rejected because it violates "specific rock at a specific location always yields the exact same items." Injecting wall-clock/frame entropy was rejected by deterministic RNG law.

Scalability potential: Same math all tiers. Visual overkill remains presentation-only and does not perturb loot truth.

Hardware Impact: No measurable CPU delta; removes a desync path.

## Decision 12: Scene-Local Cold Host And Prefab Metadata Warm-Up

Problem: The oracle host used `DontDestroyOnLoad`, and `ResourceNode` could still scan child components on the depletion branch if metadata was not cached.

Solution: Remove `DontDestroyOnLoad`; the host is scene-local, created after scene load and re-registered through `TryRegisterLateFrame`. `ResourceNode` warms loot payload metadata during `Awake` and runtime template application. Depletion uses `allowHierarchyScan: false`; cold setup may scan prefab children once.

Rejected Alternatives: Permanent cross-scene host was rejected because AGENTS forbids unrequested `DontDestroyOnLoad`. Scanning prefab hierarchy during depletion was rejected because it is avoidable O(child count) work on the interaction path.

Scalability potential: Low: no first-use hitch at node death. Middle/High/Ultra: same data truth, richer VFX only after `VisualScavengeSignal`.

Hardware Impact: Removes possible first-depletion hierarchy scan and avoids a cross-scene hidden object lifetime hazard. Estimated savings depend on prefab hierarchy size; correctness and lifetime isolation are the primary gains.

## Decision 13: Contract-Owned Scavenging Source Kind

Problem: Source-kind `13` existed as mirrored constants in the scavenging oracle and inventory consumer. That preserved the no-runtime-reference boundary, but violated the one-fact/one-owner rule.

Solution: Move the fact to `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` in the contracts signal namespace. `ScavengingLootOracleConstants` and `PlayerInventory` both consume that symbol. Inventory still does not import `Hecton8.Scavenging`.

Rejected Alternatives: Keeping a mirrored local constant in inventory was rejected because drift would silently break the signal route. Moving the constant into `GlobalSignals.cs` was rejected because it touches a massive core header for a narrow route.

Scalability potential: Same on all tiers. The VFX and inventory consumers share the source classification without changing loot math or adding a global service.

Hardware Impact: No measurable CPU delta. Correctness and compile-wall containment are the reason for the change.

## Decision 14: Explicit Signal Publish Fence Annotation

Problem: The late-frame publish chain must complete before `SignalBus<T>` snapshots can be flushed, but the raw `.Complete()` call looked indistinguishable from an arbitrary mid-frame stall.

Solution: Classify the cold fallback/audit completions as `COLD SYNC JOB` and the late-frame publish completion as `[BLOCKING_SYNC_POINT]`. The blocking point is confined to the Core late-frame signal flush fence because `SignalBus<T>.ParallelWriter` currently exposes no producer-handle registration route.

Rejected Alternatives: Importing dispatcher internals from another sibling namespace was rejected as extra coupling. Fire-and-forget publish jobs were rejected because the next signal snapshot could race a `NativeQueue.ParallelWriter`.

Scalability potential: Low keeps the batch bounded to 64 queued requests. Middle/High/Ultra may raise visual signal richness, but the truth publish still has one explicit fence.

Hardware Impact: No new work added. The annotation documents an existing bounded fence; profiler proof remains pending under the CPU build gate.

## Decision 15: Evict ResourceNode Native Depletion Lock

Problem: `ResourceNode` used a private `NativeArray<int>(1, Allocator.Persistent)` as an interlocked depletion guard. The oracle had no private persistent arrays, but the depletion producer still carried a per-node native allocation in the same route.

Solution: Replace the native lock with owner-local `int _depletionLockState` and `Interlocked.CompareExchange/Exchange`. The lock is not a cross-domain truth, not job input, not saved state, and not replay payload; a Vault buffer would be fake sovereignty for a one-word guard.

Rejected Alternatives: Keeping the NativeArray was rejected because it creates allocation/sentinel/dispose work per node. Moving one int per node into GlobalDataVault was rejected because it would inflate global routing for a local critical section.

Scalability potential: Low devices avoid thousands of native allocations in dense resource fields. Middle/High/Ultra retain the same deterministic loot truth and can spend the saved native-memory overhead on VFX consumers.

Hardware Impact: Removes one persistent native allocation and sentinel registration per `ResourceNode`. Hot depletion still uses one atomic compare/exchange; expected frame impact is sub-microsecond per node but reduces fragmentation and scene-load/disposal pressure.

## Decision 16: Active-Count Fences For Uninitialized Vault Buffers

Problem: Loot and biome modifier Vault buffers are intentionally allocated with `NativeArrayOptions.UninitializedMemory`. The prior self-audit used full buffer length as the loot table count, and the biome path scanned the full modifier buffer. That could read unwritten memory and poison deterministic results.

Solution: Track `_activeLootEntryCount` and `_activeBiomeModifierCount` in the runtime host. `LootResolutionJob` now receives `BiomeModifierCount` and scans only written modifier rows. `ScavengingLootOracleSelfAuditJob` receives `EntryCount` and audits only written loot rows. CSV ingest sets the active loot count; emergency fallback sets it to four entries.

Rejected Alternatives: Clearing all Vault buffers every boot was rejected because Task 15 explicitly asks for zero-init bypass. Leaving scans at full capacity was rejected because uninitialized memory is only legal when every read is count-gated.

Scalability potential: Low devices keep the four-entry emergency CDF and zero modifier scans. Middle can use designer CSV counts. High/Ultra can expand table size without changing memory law; only active rows are read.

Hardware Impact: Removes up to 252 stale loot-entry reads from audit and 128 stale biome rows from every modified loot resolution when no modifiers are active. Estimated hot-path saving on no-biome frames: 1-4 us avoided versus full modifier scan on weak CPUs.

## Decision 17: Compile Wall Is External To SHINOBU_125

Problem: After the CPU/compiler gate opened, `dotnet build Hecton8.Core.csproj --no-restore --nologo` was required to check the touched Core assembly. The build failed, but the reported errors were all outside this domain: `HectonVisorUberPostFeature.cs` missing Uber Noir reconstruction contracts and `SomaticTunerWindow.cs` missing VR comfort DTOs.

Solution: Record the compile wall as `[BLOCKED BY DEPENDENCY]` and do not edit Visor/Somatic code from the scavenging loot oracle lane. Static scans and diff hygiene remain the local proof for SHINOBU_125 until the owning agents restore their contracts.

Rejected Alternatives: Fixing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`, `VrComfortProfileDTO`, or `ComfortTelemetryEntry` from this agent was rejected as cross-domain sabotage. Re-running the same build without code changes was rejected as workstation waste.

Scalability potential: No runtime scalability impact. This protects compile-wall ownership: one fact, one owner, one route.

Hardware Impact: One targeted build attempt consumed about 13 seconds after the CPU gate opened. No additional build loops will be launched until the external dependency wall changes.

## Decision 18: Editor Sliders Must Mutate Vault Truth

Problem: The `Procedural Loot Tuner` exposed biome/tool/rare sliders, but the first facade only displayed UI and audit controls. That did not literally satisfy Task 17 because designers could not apply tuning into unmanaged loot truth.

Solution: Add `TryApplyEditorTuning()` under `#if UNITY_EDITOR`. It clamps slider inputs, applies a smooth polynomial rare curve, rewrites the four preview CDF rows in the Vault loot table, writes active/preview biome modifier rows for sulfur and abyssal crystal, and updates `_activeLootEntryCount` / `_activeBiomeModifierCount` so uninitialized buffer tails remain unread.

Rejected Alternatives: Decorating the UI with sliders only was rejected as fake compliance. Using managed dictionaries or serialized ScriptableObjects for tuning was rejected because the runtime source of truth is flat Vault DTOs.

Scalability potential: Low uses a narrow four-row CDF and cheap fake VFX multiplier. Middle can load CSV and use conservative sliders. High/Ultra can push rare/biome scalars higher while VFX consumers spend the saved PhysX budget on richer fake loot trails.

Hardware Impact: Player runtime cost is unchanged. Editor-only table mutation is cold/manual; hot-path modifier scans remain count-gated to two rows in the preview case instead of 128 uninitialized rows.

## Decision 19: Include The Oracle In Unity Asset And CLI Surfaces

Problem: `ScavengingLootOracle.cs` existed on disk but had no `.meta`, and the current generated `Hecton8.Core.csproj` did not include it. A prior `dotnet build` therefore could not prove the new oracle file compiled.

Solution: Add a Unity `.meta` file for `ScavengingLootOracle.cs` and include the C# file in the current CLI project surface. This is verification hygiene, not a new runtime dependency.

Rejected Alternatives: Claiming the earlier build covered SHINOBU code was rejected because the file was absent from the project file. Waiting for Unity to regenerate project files was rejected because the current CLI verifier needed to see the file now.

Scalability potential: No runtime scalability impact. Compile-surface accuracy prevents hidden runtime failures on every tier.

Hardware Impact: No frame impact. One additional C# file is visible to the compiler; compile cost is acceptable for verification and does not add a sibling assembly reference.

## Decision 20: AUP Namespace Correction

Problem: After the oracle entered the CLI compile surface, `AbsoluteUniversePosition` failed to resolve inside the new signal/DTO file. The type is declared in `Hecton8.World`, not the assumed core namespace.

Solution: Add `using Hecton8.World` to `ScavengingLootOracle.cs`. This keeps the DTO layout unchanged and resolves the unmanaged AUP type used by `VisualScavengeSignal`, requests, yields, telemetry, and resource-node queue API.

Rejected Alternatives: Re-declaring an AUP surrogate inside scavenging was rejected because it would create a second source of truth. Moving `AbsoluteUniversePosition` was rejected as cross-domain sabotage.

Scalability potential: Same deterministic seed path across Low/Middle/High/Ultra. The correction is compile-surface ownership, not frame work.

Hardware Impact: No runtime CPU delta. It preserves the existing 48-byte AUP struct and therefore the 80/96/128-byte SHINOBU DTO layout math.

## Decision 21: Gizmo Reads Must Respect Active Row Counts

Problem: The editor probability gizmo could read the first four Vault loot entries before a fallback table, CSV ingest, or editor tuning wrote valid rows. Because the buffer is `UninitializedMemory`, that was an invalid proof path even though it was editor-only.

Solution: In the gizmo path, cold-complete `EnsureEmergencyLootTableJob(default)` before reading and limit the scan to `_activeLootEntryCount`. The job is already idempotent; after the first preview table exists, the returned handle is default and the editor label reads only written rows.

Rejected Alternatives: Treating editor visualization as exempt was rejected because the gizmo is Task 19 proof of deterministic math. Clearing the entire Vault buffer was rejected because Task 15 requires zero-init bypass.

Scalability potential: Low keeps a four-row preview. Middle/High/Ultra can still expand active entries through CSV, but the gizmo does not scan unwritten capacity.

Hardware Impact: No player runtime impact. Editor-only cold sync occurs only before the preview table exists; active-row scan avoids reading up to 252 unwritten entries.
