# SALINITY_CORROSION_SYSTEM Rationale

STATUS: PENDING VERIFICATION - CORE COMPLETE, UNITY COMPILE BLOCKED BY EXTERNAL VISOR/UI DEPENDENCIES

## Intake

Problem: Items had no verified salinity-driven degradation path in the extracted assignment.
Solution: Treat durability as S.O.A. inventory state updated on FrostTick by a Burst-compatible kernel and broadcast typed signals for consumers.
Rejected Alternatives: MonoBehaviour item polling, classic durability manager singleton, material swaps, and string event names; all violate hot-path and registry rules.
Scalability potential: Low uses one scalar rust blend and 5s FrostTick decay; Middle adds richer shader scratches; High adds extra detail map response; Ultra can spend saved CPU on denser first-person material detail.
Hardware Impact: i3/MX350 target is one contiguous pass over fixed slots, estimated under 25 us for 40 slots and 0 B GC.

Problem: Multiple agents are modifying adjacent systems.
Solution: Use contracts, GlobalRegistry interfaces, and typed EventBus signals where existing seams exist; avoid concrete cross-domain references unless already established in code.
Rejected Alternatives: Direct calls into biome, audio, HUD, or save classes from corrosion logic.
Scalability potential: Signal-based consumers can load-shed independently by tier.
Hardware Impact: Avoids per-frame polling and cache-missing manager chains on low-end silicon.

## Decisions

Problem: `ItemDurabilityManager.Instance` and similar singleton habits create a direct dependency chain.
Solution: Removed the remaining `ToolDurabilitySystem.Instance` accessor and verified no script references to `ItemDurabilityManager.Instance` or `DurabilityManager.Instance`.
Rejected Alternatives: Keeping a singleton shim for compatibility. It preserves the architectural infection.
Scalability potential: Low/Middle/High/Ultra all use registry/signal seams; no tier pays singleton lookup tax.
Hardware Impact: Small call-chain gain, estimated 35 us saved in worst UI/tool fan-out frames.

Problem: Durability must be inventory-owned and contiguous.
Solution: Added `_itemDurability` as a persistent NativeArray aligned with `_itemHashes`, mirrored through add/remove/load/sort/condition paths.
Rejected Alternatives: Item component state, dictionary keyed by item id, or managed list of equipped gear. All add GC or pointer chasing.
Scalability potential: Low scans 40 slots every 5s; Middle/High/Ultra can use the saved CPU for shader detail, not simulation.
Hardware Impact: SOA pass is cache-linear; estimated under 25 us on i3/MX350.

Problem: Salinity corrosion must not poll biome state from inventory.
Solution: Drain `BiomeChangedSignal`, store current biome hash, map brine known hashes to salinity 1.0 and unknown hashes to cheap fallback bands.
Rejected Alternatives: Direct Data Monolith query per FrostTick or sampling world volumes from inventory.
Scalability potential: Low gets a scalar; Ultra can refine upstream biome classification without changing inventory.
Hardware Impact: One hash comparison chain per biome event, not per item per frame.

Problem: Degradation must hit only actively equipped items.
Solution: `ItemSalinityCorrosionJob` gates each slot with `CurrentInventoryMask & ItemCorrosionMath.ResolveInventoryMaterialBit(hash)` and skips hash 0/stack 0.
Rejected Alternatives: Managed equipped item lists or GameObject references.
Scalability potential: Identical Low/Middle/High/Ultra math; presentation tier handles visual overkill.
Hardware Impact: Bitwise filter is branch-cheap and zero allocation; estimated 8 us saved versus list lookup on 40 slots.

Problem: Broken equipment must stop acting active without destroying inventory state.
Solution: Job sets `BrokenItemStateMask`; SOA mirror excludes broken slots from `CurrentInventoryMask`; break hashes emit `ToolAcousticSignal`.
Rejected Alternatives: Deleting inventory item at 0 durability, disabling GameObjects, or direct audio calls.
Scalability potential: Low only loses active bit; Ultra can layer richer break VFX/audio from signals.
Hardware Impact: Avoids object mutation and instantiation; one fixed signal per broken item.

Problem: Repair Tool coupling must restore durability without hard-binding repair code into inventory.
Solution: Drain `ItemAcquiredSignal(Titanium)` and verify active `RepairTool`; restore durability/quality/byte mirror and clear rust/degraded/broken flags.
Rejected Alternatives: RepairTool reaching into private arrays or a dedicated repair manager singleton.
Scalability potential: Low/Middle use same data write; High/Ultra can add repair visuals via existing signals.
Hardware Impact: Scan item-acquired frame snapshot only; no per-frame repair polling.

Problem: Visual rust must be cheap and controllable.
Solution: Publish one global `_HectonEquipmentRust01` scalar derived as `1 - AverageEquipmentDurability01`; no material swap.
Rejected Alternatives: Renderer material replacement, per-tool material instances, or physical corrosion simulation.
Scalability potential: Low blends one detail map; Middle increases normal/scratch response; High adds procedural grime; Ultra can add animated pitting while CPU cost stays flat.
Hardware Impact: Avoids material cloning and renderer traversal; estimated 20-80 us saved in inventory/tool presentation frames.

Problem: FrostTick path must remain zero-GC.
Solution: Persistent NativeArrays hold results and broken hashes; job is Burst-compatible `IJob`; telemetry uses fixed NativeArray ring.
Rejected Alternatives: `List<uint>` broken items, LINQ filtering, managed event queues.
Scalability potential: Low uses same fixed capacity; Ultra spends saved GC budget on rendering only.
Hardware Impact: 0 B GC per FrostTick; predictable cache path.

Problem: Post-mortem evidence is mandatory.
Solution: Added 300-entry `SalinityCorrosionTelemetryEntry` ring and NaN-triggered binary dump to `Docs/AgentLogs/Dump_SALINITY_CORROSION_SYSTEM.bin`.
Rejected Alternatives: Console logs or one-frame state snapshots.
Scalability potential: Low writes 32 bytes per SlowTick; Ultra can add external consumers without changing ring layout.
Hardware Impact: 9.6 KB persistent memory; negligible CPU.

Problem: Save payload cannot store raw floats.
Solution: Quantize durability with `durability01 * 100f` to sbyte-equivalent byte and RLE it into the inventory binary payload after timestamps.
Rejected Alternatives: Raw `float[]`, JSON, or quality-only persistence.
Scalability potential: Low saves tiny RLE; Ultra has exact enough 1% durability granularity.
Hardware Impact: 128 slots compress to 2-256 bytes; decode is bounded and linear.

Problem: SaveData version was concurrently moved to v70 by RTG work.
Solution: Kept corrosion read gate at `version >= 69` and wrote current payload through the existing v70 codec order, without reverting RTG edits.
Rejected Alternatives: Forcing version back to 69 or overwriting unrelated RTG save changes.
Scalability potential: Durable across save migrations; later tiers do not care about version conflict.
Hardware Impact: No runtime frame impact; prevents broken save compatibility.

Problem: Unity compilation could not reach green due unrelated agents.
Solution: Verified the Unity log: `Hecton8.Inventory.Corrosion` and `Hecton8.Inventory.Corrosion.Contracts` compiled and copied; build fails later on `InternalFloodWaterlineRuntime`, `HectonVisorUberPostFeature`, and `VehicleSubOsCockpitRuntime`.
Rejected Alternatives: Editing visor/UI dependencies outside assigned domain or reverting other agents.
Scalability potential: None; this is integration hygiene.
Hardware Impact: None; compile wall is external.

## OMEGA POLISH CHANGES

Problem: Polish mandate forbids avoidable floating-point division in hot Burst paths.
Solution: Replaced average durability division in `ItemSalinityCorrosionJob` with `totalDurability * math.rcp(equippedCount)`.
Rejected Alternatives: Leaving `/ equippedCount` because FrostTick is slow cadence. It is still unnecessary.
Scalability potential: Low/Middle/High/Ultra all get the same cheaper average; saved cycles buy shader detail, not more simulation.
Hardware Impact: One float divide removed from each corrosion FrostTick; estimated sub-microsecond but deterministic.

Problem: Anti-bloat audit required checking expensive math and managed allocations in the corrosion assembly.
Solution: `rg` found no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, or `.ToString()` inside `Assets/_Project/Scripts/Inventory/Corrosion`.
Rejected Alternatives: Auditing only the primary job and ignoring contracts.
Scalability potential: No hidden tier-specific cost.
Hardware Impact: Confirms 0 B GC and no vector normalization/sqrt cost in corrosion asmdef.

Problem: `dotnet build Hecton8.Core.csproj` is required by mandate, but generated csproj references are stale relative to Unity asmdefs.
Solution: Ran `dotnet build`; it failed on missing generated asmdef references (`Inventory.Algorithms`, `Inventory.Corrosion`) plus broader project dependency state. Then compiled `Hecton8.Inventory.Corrosion` and `.Contracts` directly with Unity Bee response files and Roslyn; both passed exit code 0.
Rejected Alternatives: Treating stale csproj failure as a corrosion code failure or editing unrelated generated csproj files.
Scalability potential: None; verification path only.
Hardware Impact: None.

Problem: Cinematic Cheat Protocol requires visual fake first.
Solution: Corrosion presentation is a single `_HectonEquipmentRust01` scalar and shader-side detail blend; no physical corrosion simulation, no material swapping, no per-item visuals on low tier.
Rejected Alternatives: Honest corrosion simulation, per-tool material instances, or corrosion particles driven by item state.
Scalability potential: Low: scalar detail blend. Middle: stronger scratch/normal response. High: extra grime detail. Ultra: animated pitting/detail maps from the same scalar.
Hardware Impact: Saves renderer/material churn; estimated 20-80 us in first-person presentation frames.

Final Git Diff:
- `Assets/_Project/Scripts/PlayerInventory.cs`: salinity signal drain, SOA durability mirror, FrostTick corrosion, repair, shader scalar, HUD signal, blackbox, RLE save/load sync.
- `Assets/_Project/Scripts/Inventory/Corrosion/ItemSalinityCorrosionJob.cs`: Burst job with hash-zero skip, equipped bitmask filter, rcp average.
- `Assets/_Project/Scripts/Inventory/Corrosion/Contracts/InventoryCorrosionContracts.cs`: mask/result constants and hash-to-bit helper.
- `Assets/_Project/Scripts/Inventory/Corrosion/*.asmdef`: isolated corrosion runtime and contracts assemblies.
- `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`: removed singleton accessor.
- `Assets/_Project/Scripts/SaveData.cs` and `SaveBinaryPayloadCodec.cs`: durability RLE fields/codec integrated while preserving concurrent v70 RTG edits.
- `Docs/Tasks/Status_SALINITY_CORROSION_SYSTEM.md`: task state and compile blocker evidence.
- `Docs/AgentLogs/Rationale_SALINITY_CORROSION_SYSTEM.md`: decision journal and polish audit.
