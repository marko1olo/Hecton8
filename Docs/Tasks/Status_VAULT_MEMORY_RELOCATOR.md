# Status_VAULT_MEMORY_RELOCATOR

PROMPT: VAULT_MEMORY_RELOCATOR
DOMAIN: CORE & MEMORY INFRASTRUCTURE
TASK COUNT: 15
STATUS: PENDING VERIFICATION

## Mandates Read
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_HectonArenaAllocator_2_0.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

## Source Read
- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs
- Assets/_Project/Scripts/Core/Memory/H8Memory.cs
- Assets/_Project/Scripts/Core/GlobalSignals.cs
- Assets/_Project/Scripts/Core/SystemDispatcher.cs

## Checklist
- [ ] 1. HANDLE ARCHITECTURE | DOD: create relocatable VaultBufferHandle<T> with pointer, generation, BufferID and guarded resolution | Alternative rejected: raw NativeArray cache only, because stale raw pointers survive memmove | Estimate: 6 us resolve cold path
- [ ] 2. REGISTRY REWRITE | DOD: generation increments on move/resize and metadata/table update | Alternative rejected: pointer-only dictionary refresh, because consumers need stale-handle detection | Estimate: 2 us per moved block
- [ ] 3. ACCESSOR GUARD | DOD: Resolve checks cached generation against current generation and refreshes pointer | Alternative rejected: forced TryGetBuffer on every use, because it hides stale-handle state | Estimate: 6 us only on stale generation
- [ ] 4. SAFE WINDOW | DOD: pre-simulation compaction fence driven by SystemDispatcher | Alternative rejected: opportunistic defrag from arbitrary callers, because jobs may hold front-buffer pointers | Estimate: 0 us outside cadence
- [ ] 5. MEMMOVE IMPLEMENTATION | DOD: UnsafeUtility.MemMove relocation pass moves occupied blocks left into gaps | Alternative rejected: telemetry-only defrag, because it does not reclaim holes | Estimate: depends on moved bytes
- [ ] 6. FRAGMENTATION TRIGGER | DOD: compaction only when GapRatio > 0.15 and SystemStress < 0.5 | Alternative rejected: always compact on FrostTick, because hot hardware needs load shed | Estimate: 1 us gate
- [ ] 7. GLOBAL SHIFT | DOD: _buffers and _metadata update immediately after block move | Alternative rejected: delayed rebuild only, because local resolvers need current pointer | Estimate: 2 us per moved block
- [ ] 8. SIGNAL BROADCAST | DOD: relocation records copied to SystemDispatcher and published as MemoryAddressShiftSignal | Alternative rejected: direct Core signal dependency from Memory asmdef, because circular reference | Estimate: 3 us per signal
- [ ] 9. ALIGNMENT ENFORCEMENT | DOD: moved offsets remain 64-byte aligned, fault telemetry on violation | Alternative rejected: element alignment only, because cache-line moves are required | Estimate: 1 us audit
- [ ] 10. TIME SLICING | DOD: compaction slice bounded by 1.0 ms watchdog and continuation next frame | Alternative rejected: full-heap compaction in one FrostTick, because frame spikes are unacceptable | Estimate: watchdog branch <1 us
- [ ] 11. PINNED BLOCKS | DOD: lock/unlock buffer API and relocator skips locked blocks | Alternative rejected: trusting job owners not to cache pointers, because Burst jobs can outlive a frame | Estimate: 2 us lock mutation
- [ ] 12. ZERO-GC | DOD: no managed allocation in defrag path; move primitive is Burst job-compatible | Alternative rejected: managed List relocation queue, because hot/cold runtime defrag must remain heap-clean | Estimate: 0 B GC
- [ ] 13. BLACKBOX DUMP | DOD: TotalMovedBytes and CompactionWatchdogBreaches recorded in telemetry ring | Alternative rejected: chat/log-only reporting, because crash triage needs binary state | Estimate: 1 us ring write
- [ ] 14. TRIPLE-STRIKE REPAIR | DOD: memory barrier path added around memmove metadata publication | Alternative rejected: blind memmove without ordering, because stale readers on weak memory are plausible | Estimate: 1 us barrier
- [ ] 15. OMEGA COMPILE | DOD: dotnet build run and compile state recorded | Alternative rejected: static confidence, because this repository has many parallel edits | Estimate: build-time only

## Iterations
- Loop 0: initialized status. Code not edited yet.
