# Status_MACRO_DATABASE_ARCHITECT

Authority: `CURRENT_BATCH.md` / `MACRO_DATABASE_ARCHITECT`
Domain: Core & Memory Infrastructure / H8_MacroDB
Status: PENDING VERIFICATION

## Mandates Read
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `STRM_Persistent_Object_Registry.txt`

## Checklist
- [ ] Task 1: Singleton eradication | PENDING | DOD: register `IMacroDatabaseService`; reject `Database.Instance`; estimate TBD us.
- [ ] Task 2: Signal migration | PENDING | DOD: typed `SectorHydratedSignal`; reject string events; estimate TBD us.
- [ ] Task 3: ASMDEF isolation | PENDING | DOD: `Hecton8.Core.Database` depends only on Contracts; reject concrete cross-domain deps; estimate TBD us.
- [ ] Task 4: Dead code hunt | PENDING | DOD: scan persistent `List`/`Dictionary` global state; reject monolithic RAM world state; estimate TBD us.
- [ ] Task 5: File format | PENDING | DOD: `.h8db` header with fixed node size and root node offset; reject ad hoc serialization; estimate TBD us.
- [ ] Task 6: B-Tree node SoA | PENDING | DOD: node stores sector hashes and payload offsets as flat arrays; reject heap node graph; estimate TBD us.
- [ ] Task 7: Memory mapped files | PENDING | DOD: map `.h8db` with `MemoryMappedFile`; reject SQLite/plugins; estimate TBD us.
- [ ] Task 8: Unsafe pointer reads | PENDING | DOD: pointer traversal with `UnsafeUtility`; reject managed serialization; estimate TBD us.
- [ ] Task 9: AUP to hash | PENDING | DOD: absolute AUP sector hashing and radius generation; reject Transform/world-shift dependent queries; estimate TBD us.
- [ ] Task 10: Background query | PENDING | DOD: background hydration worker path; reject main-thread traversal stalls; estimate TBD us.
- [ ] Task 11: Native cache | PENDING | DOD: cache payload pointers in native-owned map abstraction; reject managed dictionaries in hot path; estimate TBD us.
- [ ] Task 12: Dehydration eviction | PENDING | DOD: 3km eviction hysteresis and dirty append path; reject immediate thrash eviction; estimate TBD us.
- [ ] Task 13: Defrag tool | PENDING | DOD: offline/main-menu repack path only; reject runtime stop-the-world defrag; estimate TBD us.
- [ ] Task 14: AUP shift safety | PENDING | DOD: DB keys absolute and unaffected by origin shift; reject Transform-relative DB authority; estimate TBD us.
- [ ] Task 15: Math LOD | PENDING | DOD: Low tier 1km hydration radius; High/Ultra richer residency; reject single middle-ground radius; estimate TBD us.
- [ ] Task 16: Zero-GC | PENDING | DOD: traversal/cache path has no managed allocations after init; reject LINQ/boxing/string hot path; estimate TBD us.
- [ ] Task 17: Blackbox dump | PENDING | DOD: fixed 300-frame telemetry records include cache MB and page faults; reject Debug.Log-only diagnostics; estimate TBD us.
- [ ] Task 18: Omega compile check | PENDING | DOD: compile/console validation of unsafe MMF path; reject unverified unsafe pointer code; estimate TBD us.

## Loop State
- Loop 1: Tasks 1-5 PENDING.
- Loop 2: Tasks 6-10 PENDING.
- Loop 3: Tasks 11-14 PENDING.
- Loop 4: Tasks 15-18 PENDING.
- Loop 5: Strict self-review PENDING.
