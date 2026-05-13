# Rationale: INVENTORY_SOA_BLITTER

Status: PENDING VERIFICATION

## Decision 0: Batch Identity And File Ownership

Problem: User identity is QUARTERMASTER, while the XML tag id and prompt-specific log target are INVENTORY_SOA_BLITTER.
Solution: Use INVENTORY_SOA_BLITTER for Status/Rationale/LOG file names because the extracted prompt explicitly says `Log to Status_INVENTORY_SOA_BLITTER.md`, and AGENTS.md treats the XML tag id as the operational prompt id.
Rejected Alternatives: Using `Status_QUARTERMASTER.md` would ignore the explicit batch directive and split evidence across two identifiers.
Scalability potential: Low/Middle/High/Ultra are unaffected by file naming; deterministic evidence storage prevents integration time waste across all tiers.
Hardware Impact: 0 us runtime impact on i3/MX350. Editor-only bookkeeping.

## Decision 1: Mandate Scope

Problem: Inventory transfer touches native memory, zero-GC hot paths, GlobalRegistry migration, UI batching, telemetry, and audio/debris signals.
Solution: Load six mandates before coding: inventory S.O.A., zero GC, native memory/jobs, GlobalRegistry, post-mortem telemetry, and visual-fake protocol.
Rejected Alternatives: Reading every mandate would increase latency without adding task-specific authority; reading only inventory would miss memory safety and signal constraints.
Scalability potential: Low tier gets allocation-free transfer. Middle/High/Ultra can spend saved frame time on richer UI/audio feedback without changing transfer math.
Hardware Impact: Expected runtime target is 0 B GC per bulk transfer and fewer UI rebuilds; exact microsecond savings pending code audit and verification.

## Decision 2: Runtime S.O.A. Preservation

Problem: The prompt demanded `NativeArray<int>` hash and `NativeArray<ushort>` count inventory runtime, but the codebase already uses `NativeArray<uint> _itemHashes` plus `NativeArray<ushort> _stackCounts` with parallel metadata arrays.
Solution: Keep the existing runtime S.O.A. owner and extend it with bulk transfer validation/copy paths. `ItemData` remains a cold authoring/catalog seam.
Rejected Alternatives: Deleting `ItemData` or `List<ItemData>` would break catalog, modding, save, world registry, and editor validation. Replacing `uint` hashes with `int` would churn ABI without runtime gain.
Scalability potential: Low tier gets direct native arrays and no managed transfer loop. Middle/High/Ultra keep richer metadata for visuals, decay, radiation, genetics, and save fidelity without per-item object state.
Hardware Impact: Expected 50-slot transfer improvement is roughly 500-2000 us from avoided per-item UI/event churn plus 0 B GC on i3/MX350.

## Decision 3: ASMDEF Blocked Instead Of Blind Split

Problem: `Hecton8.Inventory` code is physically under `Assets/_Project/Scripts` and compiles through `Hecton8.Core.asmdef`; it references root services, save, world, audio, gameplay, and movement types.
Solution: Mark ASMDEF isolation blocked by current assembly topology. Do not add a new asmdef until contracts are extracted into a stable inventory contracts assembly.
Rejected Alternatives: Creating `Scripts/Inventory/Hecton8.Inventory.asmdef` now would strand `InventorySoAUtility` away from `PlayerInventory` and create circular root dependencies.
Scalability potential: Runtime tiers unchanged. Clean future assembly split would reduce editor compile blast radius, not frame time.
Hardware Impact: 0 us runtime impact. Avoided likely compile break in all tiers.

## Decision 4: Transactional Bulk Transfer Shape

Problem: Base-to-player transfer needed one atomic operation with weight/volume rejection and zero managed churn, while the grid still owns 2D item placement.
Solution: Validate source slice, target slice, weight, and volume first; place target anchors; guarded-MemCpy every S.O.A. slice; clear source only after copy succeeds; notify source and target once.
Rejected Alternatives: Calling `TryAddItem` and `RemoveOneItem` per stack was rejected because it emits repeated notifications and recomputes state repeatedly. Copying only hashes/counts was rejected because condition/state/genetics/quality/save metadata would desync.
Scalability potential: Low tier runs minimal native slice scan. Middle/High/Ultra keep high-fidelity item state, audio feedback, and debris batching without changing the transaction contract.
Hardware Impact: Expected 50-slot transfer cost stays under 0.1 ms on i3/MX350 except cold explicit command sync; avoids GC spikes.

## Decision 5: Compaction Safety

Problem: The mandate says merge identical hashes, but naive hash-only merging can corrupt non-stackable or state-divergent records.
Solution: `InventoryCompactionJob` merges only compatible records: same hash, state flags, genetics, quality, and stackable max count greater than one. It shifts empties, then the grid is repacked first-fit from preallocated placement buffers.
Rejected Alternatives: Hash-only `ushort` addition was rejected because two damaged or genetically different items with the same hash would collapse into a false item. Managed grouping was rejected for allocation risk.
Scalability potential: Low tier gets compact storage and fewer UI slots. High/Ultra can display richer stack effects because metadata remains deterministic.
Hardware Impact: Estimated 40-150 us saved versus managed grouping for 50 slots; no managed allocation.

## Decision 6: Black Box And Fault Handling

Problem: Inventory mass/volume drives movement and survival; NaN propagation would make overburdening and save state unverifiable.
Solution: Add a fixed 300-frame `NativeArray<InventoryTelemetryEntry>` black-box ring and dump it to `Docs/AgentLogs/Dump_INVENTORY_SOA_BLITTER.bin` on non-finite derived totals.
Rejected Alternatives: Relying on `Debug.Log` or chat reports was rejected because it violates the black-box mandate and loses pre-fault frame history.
Scalability potential: Low/Middle/High/Ultra all get identical fault evidence; high-tier visual overkill is unaffected because telemetry is fixed-size.
Hardware Impact: About 1 us on mass refresh on low silicon; cold disk write only on fault.

## Decision 7: Verification Boundary

Problem: `dotnet build Hecton8.Core.csproj` fails before inventory verification because unrelated assemblies/contracts are missing from the generated project.
Solution: Record `GLOBAL COMPILE WALL`, run static scans, attempt Unity MCP validation, and keep status `PENDING VERIFICATION`.
Rejected Alternatives: Reporting a green compile or editing unrelated Scheduling/Layout/Audio/Vehicle/Radar systems was rejected as false reporting and domain sabotage.
Scalability potential: No runtime impact. Honest verification state prevents integrator time loss.
Hardware Impact: 0 us runtime impact.

## OMEGA POLISH CHANGES

Problem: The final anti-bloat scan found one avoidable floating-point division in the heavy-transfer audio pitch calculation.
Solution: Replaced `HeavyBulkTransferAudioThresholdKg / max(...)` with `math.rcp(max(...))` multiplied by the threshold. Re-ran static scans for unconditional `math.sqrt`, `math.normalize`, managed `foreach`, `new List`, string formatting, interpolated strings, singleton access, and per-item event spam in touched inventory files.
Rejected Alternatives: Leaving the division was unnecessary; replacing inventory validation with LUTs or visual fakes was rejected because this is deterministic data accounting, not a visual simulation.
Scalability potential: Low tier keeps the cheapest scalar path. Middle/High/Ultra do not need divergent math; saved frame time can be spent by UI/audio/VFX consumers after the single signal.
Hardware Impact: Pitch reciprocal change is sub-microsecond per heavy transfer; combined transfer path is estimated to save 500-2000 us for 50 slots on i3/MX350 by avoiding per-item add/remove/UI churn.

Problem: The implementation touched `Core/GlobalSignals.cs` and `HectonPlayerMovement.cs`, which are outside the narrow inventory folder.
Solution: The edits are cross-domain interface seams only: `DebrisSpawnSignal.Quantity` enables batched drop VFX, and `HectonPlayerMovement` reads `CurrentWeightKg` by `ref readonly` without owning inventory state.
Rejected Alternatives: Duplicating a debris signal inside inventory or polling movement through a new singleton was rejected because it violates the EventBus/GlobalRegistry decoupling rule.
Scalability potential: Low tier drops one stack signal per stack, not one prefab per item. High/Ultra can interpret the same quantity field into richer BRG/VFX batches.
Hardware Impact: Expected drop savings exceed 1000 us plus avoided allocations for 50-count drops.

Problem: Final compile verification is blocked.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal` twice. Both fail on unrelated missing Scheduling/Layout/Audio propagation/Vehicle/Radar/BinaryBlittableSafe contracts. Unity MCP validation returned `no_unity_session` for all touched scripts.
Rejected Alternatives: Editing those systems from the inventory prompt was rejected as domain sabotage. Reporting `VERIFIED MASTER GRADE` was rejected because objective build data is red.
Scalability potential: Verification state only.
Hardware Impact: 0 us runtime impact.

Problem: Whole-slice Unsafe MemCpy can corrupt a 2D inventory grid if a source item footprint extends outside the selected slice, if a target slice contains unrelated anchors where the source has empty slots, or if compaction mutates live S.O.A. buffers before first-fit repack is proven.
Solution: Added strict footprint containment and self-contained occupied-cell checks before source mutation; added target-slice emptiness checks before placement and MemCpy; rejected non-finite drop runtime positions before AUP conversion; changed post-transfer compaction to copy S.O.A. arrays into Sentinel-registered TempJob buffers, run the Burst compaction job there, build placements from compacted buffers, dry-run first-fit occupancy, and only then apply to the live grid. Durability, unit volume, and radiation metadata are preserved through placement snapshots.
Rejected Alternatives: Relying on `ClearBulkTransferSlice` as rollback was rejected because it can remove item footprints outside the selected slice. Running compaction directly on `_itemHashes`/`_stackCounts` was rejected because a first-fit failure after `_grid.Clear()` would destroy the inventory. Copying only non-empty offsets was rejected because the prompt explicitly calls for guarded whole-slice MemCpy.
Scalability potential: Low tier gets deterministic no-rollback-corruption transfers with bounded O(slice) validation. Middle/High/Ultra keep the same fast data path while VFX/UI consumers can spend the saved event budget on richer batched presentation.
Hardware Impact: Adds a small cold-command O(slice) validation pass, estimated 2-8 us for 50 cells on i3/MX350. Prevents catastrophic rollback work and preserves the 500-2000 us savings from one-shot UI/event refresh.

Final Git Diff: scoped implementation paths are `Assets/_Project/Scripts/Inventory/InventorySoAUtility.cs`, `Assets/_Project/Scripts/PlayerInventory.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/HectonPlayerMovement.cs`, `Docs/Tasks/Status_INVENTORY_SOA_BLITTER.md`, and this rationale file. Note: `GlobalSignals.cs` and `HectonPlayerMovement.cs` already carried large pre-existing working-tree edits; this pass only depends on the `DebrisSpawnSignal.Quantity` field and the `CurrentWeightKg` read seam.
