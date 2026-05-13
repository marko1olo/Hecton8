# Status: INVENTORY_SOA_BLITTER

Role: QUARTERMASTER
Domain: S.O.A. Inventory System / Bulk Container Transfer & Weight
Status: PENDING VERIFICATION

## Mandates Loaded

- DATA_Inventory_Resources_Items_SOA_Layout
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- ARCH_Global_Registry_ServiceLocator_DI_Init
- DBG_Telemetry_Crash_Reporting_PostMortem
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First

## Task Checklist

- [x] 1. Singleton eradication | DOD: `rg InventoryManager.Instance` found no runtime singleton dependency in `_Project/Scripts` | Rejected: creating/removing a non-existent singleton facade | Estimate: 0 runtime us
- [x] 2. Signal migration | DOD: bulk transfer calls `NotifyInventoryChanged()` once per source/target after the transaction; no per-item add/remove path | Rejected: looping through `TryAddItem`/`RemoveOneItem` because it emits repeated UI/event work | Estimate: saves ~450-1200 us for 50-slot transfer
- [BLOCKED BY CURRENT ASSEMBLY TOPOLOGY] 3. ASMDEF isolation | DOD: asmdef audit found inventory code currently lives under `Hecton8.Core.asmdef` and references root contracts/types; splitting now risks a circular dependency without a Contracts extraction pass | Rejected: dropping a new asmdef into `Scripts/Inventory` blindly | Estimate: 0 runtime us
- [BLOCKED BY AUTHORING DEPENDENCY] 4. Dead code hunt | DOD: runtime inventory remains native hash/count S.O.A.; `ItemData`/`List<ItemData>` are catalog/editor/mod authoring seams, not hot inventory state | Rejected: deleting `ItemData` and breaking item catalog/save/world references | Estimate: 0 runtime us, avoids integration break
- [x] 5. Inventory limits S.O.A. | DOD: `PlayerInventory` exposes `MaxWeightKg`, `MaxVolumeLiters`, `CurrentVolumeLiters`, and ref-read `CurrentWeightKg` | Rejected: implicit carry capacity as storage limit | Estimate: ~2 us validation scalar cost
- [x] 6. Pre-flight Burst job | DOD: `InventoryTransferValidationJob` sums source slice mass/volume from native arrays and writes a failure byte | Rejected: managed foreach preflight | Estimate: saves ~60-180 us at 50 slots
- [x] 7. Transactional reject | DOD: transfer fails before mutation on invalid input/source empty/target occupied/weight/volume/placement/craft lock/copy rejection; full item footprints must be contained in the source/target slices and the target slice must be empty before whole-slice MemCpy | Rejected: partial stack transfer and anchor-clipping rollback | Estimate: prevents rollback churn; ~0 allocation
- [x] 8. Unsafe MemCpy | DOD: `TryBulkCopySlice<T>` uses guarded `UnsafeMemoryCopyGuard.TryMemCpy` for source-to-target array slices | Rejected: per-slot copy loops for accepted transaction | Estimate: saves ~20-80 us at 50 slots
- [x] 9. Compaction kernel | DOD: `InventoryCompactionJob` merges compatible identical hashes on TempJob copies, builds placements from the compacted buffers, dry-runs first-fit occupancy, then mutates the live grid only after validation | Rejected: UI-side cleanup, managed LINQ grouping, and live-array compaction before repack proof | Estimate: saves ~40-150 us versus managed compaction
- [x] 10. UI batch refresh | DOD: no per-item add/remove calls in bulk path; existing `InventoryEvents.NotifyInventoryChanged()` fires once at transaction end | Rejected: per-item UI slot refresh | Estimate: saves ~500-2000 us depending UI listeners
- [x] 11. KCC weight coupling | DOD: `CurrentWeightKg` is `ref readonly`; `HectonPlayerMovement` reads it in `HandleInventoryLoadChanged` | Rejected: duplicate movement weight cache as authority | Estimate: 0-1 us
- [x] 12. Drop debris batching | DOD: `TryDropSliceToOcean` emits `DebrisSpawnSignal` with `SpeciesHash` and `Quantity` per stack, no instantiate path | Rejected: physical object spawn per item | Estimate: saves >1000 us and many allocations on 50-count drops
- [x] 13. AUP shift safety | DOD: inventory data remains entity-local native arrays; only ocean drop converts runtime position to AUP for VFX signal | Rejected: persistent AUP per slot | Estimate: 0 us in inventory steady state
- [x] 14. Zero-GC | DOD: transfer scratch uses `Allocator.TempJob` native arrays with `DisposeTempJobArray`; static scan on touched inventory files found no `foreach`, `new List`, string format, or interpolation | Rejected: managed transaction scratch | Estimate: 0 B GC
- [x] 15. Math LOD | DOD: inventory math is bounded slice scan + MemCpy; no visual LOD needed, all tiers use O(n slice)/SIMD-friendly native arrays | Rejected: per-frame optimizer or fake physics | Estimate: 50 slots stays below suspicious 0.1 ms target on i3/MX350
- [x] 16. Blackbox dump | DOD: `NativeArray<InventoryTelemetryEntry>[300]` records weight/volume/load/hash; NaN totals dump `Dump_INVENTORY_SOA_BLITTER.bin` | Rejected: chat-only crash explanation | Estimate: ~1 us on mass refresh, cold file write only on fault
- [x] 17. Audio feedback | DOD: transfer/drop over 50kg publishes `ToolAcousticSignal` with heavy-thud hash | Rejected: audio GameObject spawn | Estimate: <5 us queue write
- [x] 18. Save system sync | DOD: existing save shadow payload remains native; added `TryCopyInventoryShadowPayload` blits payload to caller-owned native bytes | Rejected: rebuilding save DTO in bulk path | Estimate: saves ~30-120 us when external storage requests raw payload
- [BLOCKED BY GLOBAL COMPILE WALL] 19. Omega compile check | DOD: `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal` fails on unrelated missing Scheduling/Layout/Audio/Vehicle/Radar contracts before inventory verification; Unity MCP reports `no_unity_session` | Rejected: pretending green compile | Estimate: 0 runtime us

## Loop Ledger

- Loop 0: Prompt extracted from CURRENT_BATCH.md. Status and rationale files created. Code audit not started.
- Loop 1: Audited existing inventory native S.O.A., signals, movement load coupling, and save shadow payload. Found no `InventoryManager.Instance`.
- Loop 2: Implemented `InventoryTransferValidationJob`, guarded slice MemCpy, MaxWeight/MaxVolume, and transactional bulk transfer.
- Loop 3: Re-extracted `INVENTORY_SOA_BLITTER` prompt from `Docs/Tasks/CURRENT_BATCH.md`; added compaction job, drop debris batching, heavy-thud signal, and ref-read KCC weight.
- Loop 4: Ran managed build. Result is global dependency wall unrelated to touched inventory files: missing Scheduling/Layout/Audio propagation/vehicle/radar contracts.
- Loop 5: Unity MCP script validation attempted for four touched scripts; all returned `no_unity_session`. Static scans completed for singleton/event spam/hot managed constructs/TempJob disposal.
- Loop 6: OMEGA polish read after all tasks were done/blocked. Replaced heavy-transfer pitch division with reciprocal multiply and re-ran anti-bloat scans. Status remains PENDING VERIFICATION due global compile wall and no Unity session.
- Loop 7: Re-read prompt/mandates/status after user requested further quality pass. Hardened slice footprint validation, target-slice emptiness, finite drop position checks, and compaction rollback safety. `git diff --check` reports only CRLF normalization warnings; Unity MCP still returns `no_unity_session`; managed build remains blocked/timed by global dependency wall.
