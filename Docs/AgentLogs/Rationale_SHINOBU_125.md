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
