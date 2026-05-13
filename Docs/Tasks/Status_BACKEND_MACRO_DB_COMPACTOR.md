# BACKEND_MACRO_DB_COMPACTOR Status

Status: PENDING VERIFICATION
Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE / Data Archivist MMF Codec
Prompt: BACKEND_MACRO_DB_COMPACTOR
Task Count: 15

## Mandates Read
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- STRM_Async_Standard.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- STRM_Persistent_Object_Registry.txt
- OPT_HectonArenaAllocator_2_0.txt

## Loop 1: Tasks 1-5
- [ ] Task 1: Extend `IAsyncPersistenceService` | Justification: pending source read | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 2: Consume `CriticalMemoryPressureEvent` to pause compaction | Justification: pending signal contract read | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 3: ASMDEF isolation `Hecton8.Core.Database` -> Contracts | Justification: pending asmdef validation | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 4: Track total dead record bytes | Justification: pending database layout read | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 5: Trigger background compaction above tier threshold | Justification: pending async design | Alternatives Rejected: pending | Estimate: pending

## Loop 2: Tasks 6-10
- [ ] Task 6: Create `world_data_compact.tmp` double-buffer file | Justification: pending copy protocol | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 7: Copy live B-Tree nodes only | Justification: pending traversal implementation | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 8: Halt write queue during PRE_SIMULATION finalization | Justification: pending queue lock implementation | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 9: Flush, atomic swap, reopen active file | Justification: pending finalization implementation | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 10: Unlock queue with <2 ms target | Justification: pending stall minimization | Alternatives Rejected: pending | Estimate: pending

## Loop 3: Tasks 11-15
- [ ] Task 11: Expose compaction state for H-PHI / Memory Sentinel | Justification: pending contract field | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 12: Power loss guard / boot tmp cleanup | Justification: pending initialize cleanup | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 13: Low-tier MicroSD threshold 50 MB | Justification: pending hardware tier source | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 14: Zero-GC traversal | Justification: pending static scan | Alternatives Rejected: pending | Estimate: pending
- [ ] Task 15: Awaitable thread lock compile check | Justification: pending build | Alternatives Rejected: pending | Estimate: pending

## Loop 4: Re-Verification
- [ ] Re-read prompt after Tasks 1-15 | Justification: mandated anti-amnesia check | Alternatives Rejected: chat memory only | Estimate: pending
- [ ] Verify compaction is blocked during active Save/Load | Justification: prompt addendum | Alternatives Rejected: optimistic state assumption | Estimate: pending

## Loop 5: Omega Polish
- [ ] Read `<POLISH_MANDATE>` only after all core tasks are done or blocked | Justification: protocol compliance | Alternatives Rejected: early polish parsing | Estimate: pending
- [ ] Final anti-bloat static scan and report append | Justification: no chat-only report | Alternatives Rejected: chat-only report | Estimate: pending

## Verification
- Compile: PENDING
- Unity Console: PENDING
- Runtime / GCMonitor: PENDING VERIFICATION
