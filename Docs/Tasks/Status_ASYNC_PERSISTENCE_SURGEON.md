# ASYNC_PERSISTENCE_SURGEON Status

Status: PENDING VERIFICATION
Domain: CORE & MEMORY INFRASTRUCTURE / Data Archivist Persistence
Prompt: Background LZ4 Saving
Task Count: 19

## Mandates Read
- DATA_Save_Persistence_Binary_Delta_Checksum
- STRM_Async_Standard
- STRM_ModuleDTO_LZ4_Dictionary
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- DBG_Telemetry_Crash_Reporting_PostMortem
- ARCH_Global_Registry_ServiceLocator_DI_Init
- OPT_Performance_Budgets_FrameTime_VRAM_Limits

## Checklist
- [ ] 1. SINGLETON ERADICATION: Purge SaveManager.Instance. Register IAsyncPersistenceService.
- [ ] 2. SIGNAL MIGRATION: Consume SaveRequestSignal. Emit SaveCompletedSignal.
- [ ] 3. ASMDEF ISOLATION: Hecton8.Core.Persistence depends on Contracts.
- [ ] 4. DEAD CODE HUNT: Eradicate File.WriteAllBytes from the main thread.
- [ ] 5. THE MEMORY ARENA: Allocate persistent 10MB NativeArray<byte> _saveStagingBuffer.
- [ ] 6. PRE_SIMULATION SNAPSHOT: Pause simulation for 1 frame via SystemDispatcher and blit subsystem DTOs.
- [ ] 7. RESUME: Unpause simulation immediately; main-thread impact target < 5ms.
- [ ] 8. AWAITABLE BACKGROUND: Launch Awaitable.BackgroundThreadAsync().
- [ ] 9. LZ4 BURST: Invoke Burst-compiled LZ4 compression job on staging buffer.
- [ ] 10. FILE IO: Write compressed bytes to .tmp via FileStream.WriteAsync.
- [ ] 11. ATOMIC RENAME: Rename .tmp to .sav and backup old .sav to .bak.
- [ ] 12. CONCURRENT SAVE LOCK: Reject SaveRequestSignal while saving.
- [ ] 13. CORRUPTION RECOVERY: On load failure/checksum mismatch, load .bak and emit HUD recovery notification.
- [ ] 14. ZERO-GC: Snapshot/compression process allocates 0 managed bytes in hot path.
- [ ] 15. MATH LOD: N/A for IO; document tier behavior.
- [ ] 16. BLACKBOX DUMP: Dump SaveDurationMs and CompressedSizeBytes to telemetry.
- [ ] 17. UI SPINNER: Emit SaveStatusSignal(InProgress) until Awaitable finishes.
- [ ] 18. VRAM ABORT: If VRAM > 1800MB, force GC after save completes.
- [ ] 19. OMEGA COMPILE CHECK: Verify background thread does not touch Unity API.

## Loop Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md. Domain and mandates verified. Code untouched.
