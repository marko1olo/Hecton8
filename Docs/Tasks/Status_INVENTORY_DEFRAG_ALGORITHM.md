# INVENTORY_DEFRAG_ALGORITHM Status

Agent: QUARTERMASTER
Domain: S.O.A. Inventory System
Prompt: INVENTORY_DEFRAG_ALGORITHM
Status: PENDING VERIFICATION

Mandates loaded:
- DATA_Inventory_Resources_Items_SOA_Layout
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- ARCH_Global_Registry_ServiceLocator_DI_Init
- DBG_Telemetry_Crash_Reporting_PostMortem
- DATA_Save_Persistence_Binary_Delta_Checksum
- UI_Data_Streaming_ZeroGC_Optimization

## Phase 1
- [ ] Task 1. SINGLETON ERADICATION: Purge `InventorySorter.Instance`.
- [ ] Task 2. SIGNAL MIGRATION: Consume `InventoryCommandSignal(Sort)`. Emit `InventoryChangedSignal`.
- [ ] Task 3. ASMDEF ISOLATION: `Hecton8.Inventory.Algorithms` -> Contracts.
- [ ] Task 4. DEAD CODE HUNT: Eradicate ANY usage of `System.Array.Sort` or `List.Sort` in Inventory domain.

## Phase 2
- [ ] Task 5. THE TARGET: Inventory uses `NativeArray<int> ItemHashes` and `NativeArray<ushort> ItemCounts`.
- [ ] Task 6. BURST JOB: Write `InventoryDefragJob : IJob` sorting hashes/counts aligned.
- [ ] Task 7. IN-PLACE ALGORITHM: Native insertion/radix sort. No temp arrays in job.
- [ ] Task 8. CATEGORY WEIGHTING: Read `NativeArray<byte> ItemCategories`. Sort category, hash, count.

## Phase 3
- [ ] Task 9. STACK MERGING: Merge same-hash partial stacks, zero emptied slots.
- [ ] Task 10. GAP SHIFTING: Push `Hash == 0` slots to end.
- [ ] Task 11. UI SYNC: Push `InventoryChangedSignal`; UI reads spans/native data without prefab churn.

## Phase 4
- [ ] Task 12. AUP SHIFT SAFETY: Document N/A for data blobs.
- [ ] Task 13. MATH LOD: Document N/A; Burst microsecond path all tiers.
- [ ] Task 14. ZERO-GC: Static scan + compile path must show no managed sort allocations.
- [ ] Task 15. BLACKBOX DUMP: Push `InventoryDefragTimeMs` to telemetry path if present.
- [ ] Task 16. EVENT BUS: Emit `ToolAcousticSignal(UI_Click)` after successful sort if lane exists.
- [ ] Task 17. ASYNC AWAITABLE: Massive locker >1000 items sliced if architecture supports it; otherwise blocked with dependency note.
- [ ] Task 18. CROSS-DOMAIN AUDIT: Save delta compressor can read sorted arrays.
- [ ] Task 19. OMEGA COMPILE CHECK: `[BurstCompile(CompileSynchronously = true)]` present and compile checked.

## Iteration Log
- Loop 0: Prompt extracted. Domain confirmed. Mandates selected. No code touched.
