# Rationale_SHINOBU_125

Date: 2026-05-19
Status: STATIC IMPLEMENTATION PRESENT / UNITY RUNTIME PENDING / COMPILE BLOCKED BY DEPENDENCY

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

Solution: Add `using Hecton8.World` to `ScavengingLootOracle.cs` for request/yield/telemetry/resource-node queue DTOs that still operate inside the current Core/root assembly. The visual contract now converts that runtime AUP into contract-local `VisualScavengeAup48`, so `VisualScavengeSignal` does not force Core.Contracts to import World runtime.

Rejected Alternatives: Re-declaring an AUP surrogate inside scavenging was rejected because it would create a second source of truth. Moving `AbsoluteUniversePosition` was rejected as cross-domain sabotage.

Scalability potential: Same deterministic seed path across Low/Middle/High/Ultra. The correction is compile-surface ownership, not frame work.

Hardware Impact: No runtime CPU delta. It preserves the existing 48-byte AUP struct and therefore the 80/96/128-byte SHINOBU DTO layout math.

## Decision 36: Visual Signal Must Not Import World Runtime

Problem: `VisualScavengeSignal.cs` was physically moved into `Assets/_Project/Scripts/Core/Contracts/Signals`, but it still imported `Hecton8.World.AbsoluteUniversePosition`. `Hecton8.Core.Contracts.asmdef` has no reference to the root/Core World runtime type and must not gain one; the signal contract would fail assembly isolation or require a reverse dependency.

Solution: Add explicit 48-byte `VisualScavengeAup48` inside the signal contract file and make `VisualScavengeSignal.PositionAup` use that contract-local transfer DTO. `PublishLootYieldsJob` converts the runtime `AbsoluteUniversePosition` into `VisualScavengeAup48` before enqueue. The signal remains 80 bytes.

Rejected Alternatives: Adding `Hecton8.Core` or World runtime as a Core.Contracts reference was rejected because it creates a compile-wall cycle or sibling runtime leak. Moving `AbsoluteUniversePosition` was rejected as World/Core ownership surgery outside SHINOBU_125. Leaving the import was rejected because static project inclusion would pass only in a monolithic `.csproj` view and fail under Unity asmdef boundaries.

Scalability potential: Low/Middle/High/Ultra all receive the same visual fake payload. VFX consumers can reconstruct a runtime/world AUP at their boundary if needed, while the contract stays blittable and assembly-local.

Hardware Impact: 0 us runtime material delta. The Burst publish job copies six scalar AUP fields into an 80-byte signal payload it was already emitting; no allocation, no new lane, no native buffer.

## Decision 21: Gizmo Reads Must Respect Active Row Counts

Problem: The editor probability gizmo could read the first four Vault loot entries before a fallback table, CSV ingest, or editor tuning wrote valid rows. Because the buffer is `UninitializedMemory`, that was an invalid proof path even though it was editor-only.

Solution: In the gizmo path, cold-complete `EnsureEmergencyLootTableJob(default)` before reading and limit the scan to `_activeLootEntryCount`. The job is already idempotent; after the first preview table exists, the returned handle is default and the editor label reads only written rows.

Rejected Alternatives: Treating editor visualization as exempt was rejected because the gizmo is Task 19 proof of deterministic math. Clearing the entire Vault buffer was rejected because Task 15 requires zero-init bypass.

Scalability potential: Low keeps a four-row preview. Middle/High/Ultra can still expand active entries through CSV, but the gizmo does not scan unwritten capacity.

Hardware Impact: No player runtime impact. Editor-only cold sync occurs only before the preview table exists; active-row scan avoids reading up to 252 unwritten entries.

## Decision 22: Resource Nodes Must Not Force Table Results

Problem: The runtime `ResourceNode` path passed the prefab item hash as both `OreHash` and `ForcedItemHashID`. That made real depleted nodes bypass the authored CDF and deterministic weighted RNG entirely; only mocks/editor paths exercised the loot table.

Solution: `ResourceNode.TrySpawnLoot()` now queues `ForcedItemHashID = 0u` and keeps the prefab item hash as the ore/type seed context. The forced-item branch remains in `LootResolutionJob` only for explicit future callers that intentionally mark a forced result.

Rejected Alternatives: Keeping prefab item as forced result was rejected because it makes the "loot table oracle" a decorative fallback. Deleting forced result support was rejected because scripted deterministic grants may need that path later.

Scalability potential: Low/Middle/High/Ultra now share one deterministic weighted table path. Higher tiers can enrich `VisualScavengeSignal` consumers without changing item truth.

Hardware Impact: No measurable CPU change. Correctness fix restores the intended <5 us table roll instead of a constant output.

## Decision 23: Biome Scalar Has One Owner

Problem: Editor tuning multiplied sulfur/abyssal weights by `safeBiomeScalar` in the CDF and also wrote matching `ScavengingBiomeModifierDTO` rows. Runtime would apply the biome scalar twice when an active/preview biome hash existed.

Solution: CDF preview rows now receive only tool and rare-rate tuning. Biome tuning writes only the biome modifier rows, which are the single runtime owner of biome-specific yield weighting.

Rejected Alternatives: Keeping double application was rejected because it violates one fact -> one owner. Removing biome modifiers and baking biome into the CDF was rejected because Task 11 requires secondary Vault modifier rows.

Scalability potential: Low uses two active modifier rows in the editor preview. Middle/High/Ultra can expand biome rows through CSV/binary data without duplicating scalar ownership.

Hardware Impact: Hot-path row count remains unchanged. Correctness fix prevents designer tuning from producing nonlinear, hidden probability inflation.

## Decision 24: Self-Audit Must Match Runtime Selection

Problem: The 10k self-audit sampled raw CDF weights only. It ignored tool masks and active biome modifiers, so the editor readout could disagree with the actual runtime `LootResolutionJob`.

Solution: `ScavengingLootOracleSelfAuditJob` now reads `BiomeModifiers`, active modifier count, active entry count, all tool masks, and active biome hash. It computes modified integer weights before mapping thresholds.

Rejected Alternatives: Reporting base-CDF audit as "close enough" was rejected because Task 17 asks for real-time distribution readout based on simulated rolls. Running the full runtime request/yield pipeline for every audit was rejected as extra editor complexity and unnecessary signal traffic.

Scalability potential: Editor-only. Low/Middle/High/Ultra runtime behavior is unchanged; audit now proves the same math designers tune.

Hardware Impact: No player runtime cost. Editor audit adds a small active-row scan per simulated roll, bounded by active counts.

## Decision 25: CSV Parser Must Fail Closed On Integer Overflow

Problem: The byte-level CSV parser accumulated weights and parsed unsigned integers without overflow checks. Malformed designer input could wrap the CDF and produce non-monotonic loot weights.

Solution: Token parsing rejects overflowing unsigned values, and cumulative CDF addition saturates at `uint.MaxValue`.

Rejected Alternatives: Throwing exceptions was rejected for gameplay/editor robustness. Managed CSV parsing was rejected by Task 18 and Zero-GC parser requirements.

Scalability potential: Same data path all tiers. Larger high-tier tables stay deterministic under bad input instead of wrapping.

Hardware Impact: Editor/cold parser only; no player runtime impact.

## Decision 26: Source Kind Belongs To Contracts, Not Runtime Oracle

Problem: `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` was declared in the same source file as the scavenging runtime. That works in the current root assembly but becomes a compile-wall trap if Inventory and Scavenging split into separate asmdefs. A scan also shows `HarvestableOutcrop` consumes the same source-kind contract surface.

Solution: Move the source-kind surface to `Assets/_Project/Scripts/Core/Contracts/Signals/ItemAcquiredSignalSourceKinds.cs` and include that narrow file in the current CLI project surface. `ScavengingLootOracleConstants` and `PlayerInventory` still consume `ScavengingLootOracle = 13`, while the existing outcrop route keeps `HarvestableOutcrop = 14` in the same contract owner.

Rejected Alternatives: Touching the massive `GlobalSignals.cs` header was rejected because the facts are narrow. Mirroring values in runtime producers was rejected because it violates one fact -> one owner.

Scalability potential: No runtime scalability effect. This protects compile-wall routing for future assembly isolation.

Hardware Impact: No frame impact. Compile surface adds one constant-only C# file.

## Decision 27: Compile Attempt Stopped At External Walls

Problem: After the source-kind contract file and self-audit job changes, a compile check was needed, but project rules forbid builds while CPU is over 50% or `dotnet/csc` is already active.

Solution: Waited for the gate. At CPU 34.2% with no active compiler processes, ran `dotnet build Hecton8.Core.csproj --no-restore --nologo` once. The build failed with 82 external errors and 1 duplicate-source warning. Visible errors are in KineticCharacter, TerminalOS, Visor Uber/DeferredDecal, ModularEquipment, Fauna/Mesofauna, Somatic, and Ecosystem missing DTO/contracts. No visible error references `ScavengingLootOracle.cs`, `ResourceNode.cs`, `PlayerInventory.cs`, or `ItemAcquiredSignalSourceKinds.cs`.

Rejected Alternatives: Running more build loops was rejected because the owning domains must restore their contracts. Editing KineticCharacter, TerminalOS, Visor, ModularEquipment, Fauna, Somatic, or Ecosystem from SHINOBU_125 was rejected as cross-domain sabotage.

Scalability potential: No runtime scalability effect. This preserves compile-wall ownership and avoids turning the scavenging task into an integration medic pass.

Hardware Impact: One targeted no-restore build consumed about 15 seconds. No further build attempts will run without a changed dependency state and an open CPU/compiler gate.

## Decision 28: Visual Fake Payload Belongs To Contracts

Problem: `VisualScavengeSignal` was declared in `ScavengingLootOracle.cs` while using the `Hecton8.Core.Contracts.Signals` namespace. That compiles in the current root assembly, but it physically couples the visual fake signal contract to the scavenging runtime file. If assemblies split, VFX/UI consumers would need the scavenging runtime source just to know the signal payload layout.

Solution: Move `VisualScavengeSignal` into `Assets/_Project/Scripts/Core/Contracts/Signals/VisualScavengeSignal.cs`, keep its explicit 80-byte layout unchanged, add a Unity `.meta`, and include it in the current CLI compile surface. The oracle remains the producer; Core contracts owns the payload shape.

Rejected Alternatives: Touching `GlobalSignals.cs` was rejected because the payload is narrow. Leaving the contract payload inside the runtime oracle file was rejected as a future compile-wall trap. Duplicating the struct in VFX/UI was rejected because it violates one fact -> one owner.

Scalability potential: No runtime math change. Low/Middle/High/Ultra all receive the same signal payload; only the `GlobalQualityWeight` scalar changes presentation cost.

Hardware Impact: 0 us runtime delta. Compile-wall impact is positive: consumers can reference a narrow contract file instead of a runtime oracle source file.

## Decision 29: Prompt Extraction Must Match XML Attributes

Problem: A strict regex looking only for `<AGENT_PROMPT id="SHINOBU_125">` falsely reported the current batch block as missing. The actual tag carries additional attributes: `role="SCAVENGING_LOOT_TABLE_ORACLE"` and `chat_name="SHINOBU_125"`.

Solution: Treat the attribute-aware extraction regex as the current proof path: `<AGENT_PROMPT\b(?=[^>]*\bid="SHINOBU_125")[^>]*>...`. Status now records the exact active tag shape.

Rejected Alternatives: Using neighboring prompt blocks was rejected. Trusting old chat memory was rejected. Keeping the exact-tag regex was rejected because it fails on valid XML with extra attributes.

Scalability potential: No runtime impact. This protects task authority under batch-file edits and context compression.

Hardware Impact: 0 us runtime. CLI extraction cost is cold documentation/protocol work.

## Decision 30: Masked CDF Must Not Recompute Prefixes

Problem: The base loot path handled tool-gated tables by running a total scan and then a binary search whose midpoint predicate recomputed the eligible prefix from the table start. At 256 active rows, a masked table could read roughly 1200-1400 entries per roll instead of a bounded two-pass scan.

Solution: Keep direct binary search for the true raw-CDF case only: all active rows pass the tool mask and the active CDF is monotonic. When tool masks create a sparse effective CDF, perform a second linear pass using the already chosen threshold. This keeps item truth deterministic, avoids scratch buffers, and removes repeated prefix scans.

Rejected Alternatives: Allocating a temporary eligible CDF was rejected because persistent memory belongs to the Vault and per-frame scratch ownership was not assigned in the original task. Keeping prefix recomputation was rejected because it hides O(n log n) work behind the word "binary". Using floating weighted sampling was rejected because rollback requires integer deterministic thresholds.

Scalability potential: Low keeps the four-row emergency CDF and usually takes the direct binary path. Middle/High/Ultra can use larger CSV/binary tables; sparse tool requirements scale as O(2n), while fully eligible tables stay O(n + log n) without visual or gameplay divergence.

Hardware Impact: For a 256-row sparse table, estimated low-end saving is 2-8 us per harvested node by removing repeated prefix walks. No managed allocation and no new domain dependency.

## Decision 31: Incremental Yield Must Not Register Physical Dropped Items

Problem: Depletion loot was routed through the oracle, but incremental mining yield still called `PersistentWorldRegistry.TryRegisterDroppedItem()` and pushed an ad hoc source-kind 1 `ItemAcquiredSignal`. That retained a physical dropped-item path during the same resource-node mining flow and bypassed the canonical scavenging source-kind.

Solution: Route incremental yield through `ScavengingLootOracleRuntime.TryQueueResourceNodeLoot()` with a forced item hash and `emitDepletionDelta: false`. The oracle publishes the direct inventory signal and the visual fake. A new suppress-depletion request/result flag prevents incremental yield from sending a node tombstone before actual depletion.

Rejected Alternatives: Keeping world dropped-item registration was rejected because Task 08 requires direct inventory plus a visual fake. Emitting a direct `GlobalSignals.Push` from `ResourceNode` was rejected because it bypasses the oracle's source-kind, telemetry, and publish job. Sending depletion deltas for incremental yield was rejected because it would falsely destroy nodes while they still have health.

Scalability potential: Low uses one forced oracle request per incremental yield batch and one fake visual signal. Middle/High/Ultra can spend visual budget through the same `VisualScavengeSignal` scalar while item truth stays forced and deterministic.

Hardware Impact: Removes persistent dropped-item registration, hydration queue checks, spawn impulse bookkeeping, and world item persistence for incremental mining yield. Estimated saving is 20-200 us per emitted unit depending on registry pressure, with no new managed allocation.

## Decision 32: Scavenging Grants Must Not Trigger Repair-Tool Titanium Side Effects

Problem: `PlayerInventory.DrainRepairToolTitaniumSignals()` scanned all `ItemAcquiredSignal` payloads for titanium. After scavenging began publishing direct inventory grants on source-kind 13, a mined titanium grant could also trigger repair-tool durability restoration without going through a repair cost route.

Solution: Exclude `ItemAcquiredSignalSourceKinds.ScavengingLootOracle` from the repair-tool titanium drain. The scavenging grant remains consumed only by `DrainScavengingLootOracleSignals()`, preserving one source-kind route for inventory insertion and preventing a second gameplay meaning from the same signal.

Rejected Alternatives: Duplicating scavenging grants into a separate inventory API was rejected because Task 09 requires `ItemAcquiredSignal` handoff. Letting repair logic consume all titanium acquisitions was rejected because it creates a hidden side effect and no explicit resource-cost owner. Adding another source-kind constant was rejected because no new producer is needed.

Scalability potential: Low/Middle/High/Ultra all keep the same signal scan cost plus one byte compare. The saved gameplay ambiguity lets high-tier visual overkill remain presentation-only instead of changing item/repair truth.

Hardware Impact: 0 us material runtime delta beyond an existing branch in a bounded SlowTick loop. Correctness impact is the gain: no accidental durability writes from scavenging loot broadcasts.

## Decision 33: Fresh Compile Gate Stops On Deleted Foreign Source Includes

Problem: After the repair-tool source-kind patch, the CPU/compiler gate opened and a compile check was justified. `dotnet build Hecton8.Core.csproj --no-restore --nologo` failed before semantic C# compilation because the active project file still references two absent files: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.

Solution: Classify the build as `[BLOCKED BY DEPENDENCY]` and do not restore deleted World/Construction sources or edit the broad generated project file from the scavenging lane. Record the current blocker precisely; keep SHINOBU proof at static-scan level until the owning domains or integrator reconcile those project-file entries.

Rejected Alternatives: Recreating `ChemicalInfluenceGrid.cs` was rejected as World/Ecosystem ownership. Recreating `LogisticsPipeEvents.cs` was rejected because SHINOBU_107 explicitly deleted it as dead construction route surface. Removing `Compile Include` entries from `Hecton8.Core.csproj` was rejected as broad compile-surface surgery outside the current domain.

Scalability potential: No runtime scalability effect. This preserves domain boundaries and prevents the scavenging agent from silently reviving dead global routes.

Hardware Impact: One no-restore build consumed about 4.3 seconds under an open CPU gate. No frame-time or runtime memory impact.

## Decision 34: AUP Hashes Must Use Millimeter Commit Coordinates

Problem: `BuildDeterministicSeed()` and `BuildResourceNodeHash()` folded raw `math.asuint(LocalX/Y/Z)` float bits. That makes the oracle deterministic only when every client carries identical presentation-local float bits. The AUP mandate requires local offsets to be quantized before persistence, telemetry hashing, native packet emission, and deterministic comparison.

Solution: Add `QuantizeLocalMillimetersForHash()` and use it for both RNG seed mixing and resource-node depletion hash mixing. The helper sanitizes non-finite input to 0, clamps local offsets to `[0, AbsoluteUniversePosition.CellSizeMeters]`, and rounds to millimeters before folding.

Rejected Alternatives: Calling `PersistentWorldRegistry.ComputeResourceNodeTombstoneId()` from the Burst job was rejected because it drags a world persistence class into the hot RNG kernel. Keeping raw float bits was rejected because a 1 ULP presentation difference can produce different loot/depletion hashes. Hashing absolute doubles was rejected by the AUP precision rule and would reintroduce large-world jitter into gameplay truth.

Scalability potential: Low/Middle/High/Ultra all share identical item truth. Higher tiers can add richer `VisualScavengeSignal` consumers without changing deterministic hash semantics.

Hardware Impact: Adds three clamp/round operations per resolved request, still inside the <5 us interaction target. No managed allocation, no new Vault buffer, no new SignalBus lane, and no extra domain-owned persistent state.

## Decision 35: Loot Quantity Must Saturate Before Capacity Preflight

Problem: `ResourceNode` multiplied authored `lootCount` by prefab unit quantity in `uint`. A malformed or extreme authoring value could wrap before `PlayerInventory.CanAcceptItemQuantity(...)` and before `ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(...)`, producing an under-grant, capacity false-negative, or hidden zero-like request shape.

Solution: Add `MultiplyLootQuantitySaturated(int authoredLootCount, uint unitQuantity)` and use it on both cached and newly resolved loot oracle payload paths. The helper clamps each factor to at least one, multiplies in `ulong`, and saturates to `uint.MaxValue`.

Rejected Alternatives: Leaving unchecked arithmetic was rejected because authoring data is not a trust boundary. Throwing or aborting depletion was rejected because the player should not lose a node due to a high template count when a deterministic saturated quantity can preserve route safety. Moving this into the oracle job was rejected because capacity preflight already needs the correct quantity before enqueue.

Scalability potential: Low/Middle/High/Ultra use the same payload quantity truth. Higher-tier visual consumers can scale presentation from `VisualScavengeSignal`, but item quantity remains deterministic and bounded.

Hardware Impact: One cold/resource-node scalar `ulong` multiply on loot payload resolution. No managed allocation, no new native allocation, no branchy lookup, and no per-frame loop expansion.

## Decision 37: Signal Frames Must Be Oracle-Owned Simulation Frames

Problem: `ScavengingLootOracleRuntime` stamped queued requests and resolved signals with `Time.frameCount`. The frame value was metadata, not RNG truth, but it still tied oracle telemetry and SignalBus payloads to Unity presentation frame state instead of the deterministic simulation route.

Solution: Add owner-local `_simulationFrameCounter`, `PeekNextSimulationFrame()`, and `AdvanceSimulationFrame()`. Queue-time metadata uses the next oracle frame; `LootResolutionJob.Frame` uses the frame advanced exactly once per late-frame oracle drain. No RNG seed includes this counter.

Rejected Alternatives: Keeping `Time.frameCount` was rejected because rollback-facing signal metadata should not depend on Unity presentation cadence. Importing a global dispatcher frame counter was rejected because no stable contract was assigned in the SHINOBU_125 prompt, and adding another dependency would expand the compile wall.

Scalability potential: Low/Middle/High/Ultra all get the same item truth and signal frame semantics. Quality remains a presentation scalar through `VfxEmissionMultiplier`, not a timing switch.

Hardware Impact: One `uint` increment per oracle drain, no allocation, no native memory, no new signal lane. Runtime delta is below measurement noise; correctness isolation is the gain.

## Decision 38: Compile-Guard Claims Must Match Physical Asmdefs

Problem: The mandate names `Hecton8.[Domain].Runtime.asmdef`, but this repository currently has no `Hecton8.Scavenging.Runtime.asmdef`. Claiming direct compliance for a nonexistent assembly would be a false proof. The real current risk is introducing a new Core.Contracts dependency on World runtime through the visual fake payload.

Solution: Record the exact current state: SHINOBU runtime code is compiled through root `Hecton8.Core`, while the new signal payloads live under `Hecton8.Core.Contracts`. `VisualScavengeSignal` uses contract-local `VisualScavengeAup48`, so Core.Contracts does not import `Hecton8.World`. Item truth still uses the pre-existing `ItemAcquiredSignal` AUP contract in `GlobalSignals.cs`; that global contract predates SHINOBU and was not expanded by this task.

Rejected Alternatives: Creating a new scavenging asmdef was rejected because it would be broad assembly surgery under an already broken integration compile wall. Moving `AbsoluteUniversePosition` was rejected because it is a World-owned foundational type. Claiming the named scavenging asmdef exists was rejected as false reporting.

Scalability potential: No runtime scalability effect. Low/Middle/High/Ultra all keep the same SignalBus payloads; the benefit is preserving future assembly isolation when an integrator physically splits scavenging out of root Core.

Hardware Impact: 0 us runtime. Compile-wall impact is positive: VFX/UI consumers can consume the visual payload without depending on a scavenging runtime source file or a World runtime reference from Core.Contracts.

## Decision 39: Quantity Contract Must Clamp Before Capacity Truth

Problem: The oracle carried `uint Quantity`, but the canonical `ItemAcquiredSignal` contract can carry only `ushort Quantity`. The prior publish job clamped at the last moment, which let capacity preflight, visual payload, and telemetry see a larger number than the inventory owner could receive.

Solution: Add `ScavengingLootOracleConstants.ItemSignalMaxQuantity = ushort.MaxValue` and `ScavengingLootOracleRuntime.ClampItemSignalQuantity()`. `ResourceNode` now clamps before `CanAcceptItemQuantity(...)` and before enqueue. The oracle records `RequestFlagQuantityClamped` / `ResultFlagQuantityClamped`, telemetry inherits the result flag, `ItemAcquiredSignal.Flags` marks the narrowed contract payload, and `VisualScavengeSignal.Flags` mirrors the result flags.

Rejected Alternatives: Widening `ItemAcquiredSignal.Quantity` was rejected because that is a global signal ABI change outside this domain. Keeping late publish-only narrowing was rejected because it splits item truth across inventory, telemetry, and VFX. Throwing or aborting high authored quantities was rejected because deterministic clamping preserves a valid route without player-visible node loss.

Scalability potential: Low/Middle/High/Ultra all receive the same bounded item truth. High-tier VFX can still scale presentation through `VisualScavengeSignal.Quantity`, but that value is now the same clamped truth consumed by inventory.

Hardware Impact: One scalar min/max clamp in the producer path and one flag OR in the existing request DTO. No allocation, no new Vault buffer, no new SignalBus lane, and no extra hot-path loop. Runtime cost is below measurement noise; correctness is the gain.

## Decision 40: Impact Debris Must Be A Compute Fake, Not Pooled Physics

Problem: `ResourceNode.SpawnImpactDebris()` still built a runtime shard prefab, warmed an ObjectPool, spawned 3-5 GameObjects per mining hit, configured MeshRenderer/MeshFilter/BoxCollider/Rigidbody components, queued force/torque, and drove a `RuntimeDebrisShard` lifetime updater. That is physical simulation for a chip visual, and it stayed in the same harvesting route after depletion loot was already converted to direct item signals.

Solution: Replace the body with a single `DebrisSpawnSignal` using `DebrisSpawnSignal.FlagComputeShard`. Particle quantity is a continuous polynomial function of `HomeostasisBrain.GlobalQualityWeight` and tool power. Remove the runtime debris prefab builder, runtime physics material allocation, pooled shard updater, random cardinal rotation helper, and per-shard force/torque loop.

Rejected Alternatives: Keeping high-tier Rigidbody debris was rejected because the requirement is visual overkill through GPU/VFX work, not CPU physics debt. Adding a new scavenging-only debris signal was rejected because an existing `DebrisSpawnSignal` lane and compute debris renderer already own this visual fake. Binary low/high switches were rejected; the request quantity scales continuously from small low-tier bursts to dense high-tier compute debris.

Scalability potential: Low uses about 4-6 requested particles and lets the compute debris renderer apply its own low-tier cap. Middle increases requested particles smoothly. High/Ultra push larger compute-shard injections without changing item truth or spawning scene objects.

Hardware Impact: Removes O(k) ObjectPool/GameObject/Rigidbody work from mining impacts and replaces it with O(1) signal publish. Estimated low-end saving is 50-300 us per impact burst, higher when PhysX islands were awake. No managed allocation and no new persistent state.

## Decision 41: Yield Sample Delta Must Not Use Unity Wall Clock

Problem: `ResourceNode.ResolveYieldSampleDeltaSeconds()` read `Time.time` and stored `_lastYieldSampleTimeSeconds`. Incremental yield mass therefore depended on presentation wall-clock cadence between hit callbacks, while the loot oracle route is rollback-adjacent and already uses deterministic signal/telemetry frame ownership.

Solution: Remove `_lastYieldSampleTimeSeconds` and return a fixed deterministic `DefaultFirstYieldSampleSeconds` until a formal dispatcher `SimulationTickDelta` contract is assigned to this producer. The existing `MinimumYieldSampleSeconds` guard remains in the mass evaluator call as a NaN/zero denominator fence.

Rejected Alternatives: Keeping wall-clock deltas was rejected because it can diverge across frame pacing and rollback replay. Importing `SystemDispatcher` internals was rejected because no stable public contract was assigned to this domain and it would expand the root coupling. Adding a new signal or Vault time buffer was rejected because time authority is not owned by SHINOBU_125.

Scalability potential: Low/Middle/High/Ultra use identical yield mass per interaction callback. Visual scaling remains in `VisualScavengeSignal` and compute debris quantity, not item truth.

Hardware Impact: Removes one `Time.time` read, one float field, one clamp, and one state write per mining damage call. Estimated gain is below 1 us; deterministic replay hygiene is the reason for the change.

## Decision 42: Gameplay-Facing Struct Construction Must Be Field-Auditable

Problem: The gameplay-facing SHINOBU route used value-type initializer syntax for resolved yields, telemetry entries, queued requests, item/visual/depletion/HUD signals, visual AUP transfer payloads, late-frame job descriptors, Vault views, and compute debris signals. These do not allocate heap memory, but the syntax is mechanically indistinguishable from allocation in a fast scan and weakens the Zero-GC proof.

Solution: Replace those gameplay-facing initializer blocks with `default` stack locals and direct field writes. `default` zeroes explicit-layout padding without using heap allocation or `new` syntax. Keep cold/mock/editor initializers where they are outside player gameplay and already guarded by job/editor context.

Rejected Alternatives: Chasing every cold fallback/editor initializer was rejected because it increases compile-risk without player hot-path gain. Leaving hot-path initializer syntax was rejected because the mandate requires auditable Zero-GC code, not just technically heap-free IL. Raw uninitialized stack locals were rejected because explicit-layout padding could carry undefined bytes into queues/dumps.

Scalability potential: Low/Middle/High/Ultra all keep the same data truth. The change is proof hardening; visual scaling remains in `VisualScavengeSignal` and compute debris quantity.

Hardware Impact: 0 us material runtime delta expected. The benefit is review speed and preventing future accidental managed construction from hiding inside hot gameplay code.

## Decision 43: Quality Weight Must Fail Closed To Minimum Survival

Problem: Several SHINOBU paths saturated `GlobalQualityWeight` without a single finite-input owner. The resolver also used a prior default-to-1 guard shape for request quality, so a non-finite quality input could bias presentation toward ultra or leak NaN into particle-count math before casts.

Solution: Add `ScavengingLootOracleMath.SanitizeQualityWeight(float)` and route mock request quality, queued ResourceNode request quality, resolver VFX scalar, telemetry quality, and impact-debris compute quantity through it. The helper preserves the continuous 0..1 scalar for valid values and forces non-finite input to 0.0, the minimum-survival visual mode.

Rejected Alternatives: Binary low/high quality switches were rejected by the project scalability law. Defaulting non-finite input to 1.0 was rejected because a corrupt thermal/quality signal must not buy visual overkill. Duplicating local finite clamps at each call site was rejected because it creates multiple quality owners. Throw/assert behavior was rejected because gameplay presentation faults must fail closed without crashing the deterministic loot route.

Scalability potential: Low, Middle, High, and Ultra still consume the same continuous quality scalar. A corrupt value collapses only presentation cost to minimum survival; item truth, RNG, depletion hashes, and inventory handoff remain invariant.

Hardware Impact: One finite select plus saturate at quality ingress points already doing scalar math. No managed allocation, no new Vault buffer, no SignalBus lane, and no material frame-time claim. Correctness gain is preventing NaN or bad-quality escalation from reaching VFX/debris math.
