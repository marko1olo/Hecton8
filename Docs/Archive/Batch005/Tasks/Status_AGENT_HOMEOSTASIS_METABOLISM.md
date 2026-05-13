# Status_AGENT_HOMEOSTASIS_METABOLISM

Prompt: AGENT_HOMEOSTASIS_METABOLISM
Domain: GlobalDataVault defragmenter and Addressables eviction policy
Task Count: 20
Status: PENDING - GLOBAL COMPILE WALL / CONCURRENT VAULT OVERWRITE

## Hygiene
- [x] Status file created | Justification: active file was missing, not stale. DOD practice: disk-backed state before code. Alternative rejected: chat-only memory. Estimate: 0 us/frame.
- [x] Rationale file created | Justification: required before marking any task done. DOD practice: decision log with rejected alternatives and hardware impact. Alternative rejected: final-only report. Estimate: 0 us/frame.
- [x] Batch prompt extraction attempted | Justification: `Docs/Tasks/CURRENT_BATCH.md` contains no `AGENT_HOMEOSTASIS_METABOLISM` tag. DOD practice: CLI extraction before coding. Alternative rejected: pretending local batch matched. Estimate: 0 us/frame.
- [x] Active directive selected | Justification: user supplied two duplicate tags; the second tag has 20 numbered tasks and the rationale requirement, so it is the active task source. DOD practice: newest explicit user directive wins when local batch lacks the tag. Alternative rejected: executing neighboring local batch agents. Estimate: 0 us/frame.

## Mandates Read
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol | Justification: covers NativeMemorySentinel registration, JobHandle fences, raw pointer ownership. Estimate: 0 us/frame.
- [x] OPT_HectonArenaAllocator_2_0 | Justification: covers H8Memory/raw arena allocation policy. Estimate: 0 us/frame.
- [x] STRM_Asset_Lifecycle_Addressables_Loading_Memory | Justification: covers Addressables lifecycle and memory release. Estimate: 0 us/frame.
- [x] STRM_World_Streaming_Residency_Chunk_Management | Justification: covers chunk residency policy. Estimate: 0 us/frame.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate | Justification: covers zero-GC requirements for runtime loops. Estimate: 0 us/frame.
- [x] OPT_Performance_Budgets_FrameTime_VRAM_Limits | Justification: covers MX350 VRAM ceiling and frame cost gates. Estimate: 0 us/frame.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem | Justification: covers black-box telemetry and dump behavior. Estimate: 0 us/frame.
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init | Justification: covers service boundary and EventBus/GlobalRegistry split. Estimate: 0 us/frame.

## Tasks
- [x] 1. VRAM TRACKER | Justification: `VRAMMonitor` SlowTick now samples the `Gfx.UsedMemory` profiler counter candidate and folds it into total VRAM. DOD: no per-frame sampler. Alternative rejected: marker `Recorder` timing API. Estimate: 1-5 us per SlowTick.
- [x] 2. MIP-MAP BIAS INJECTION | Justification: `VRAMPressureMonitor` forces mip limit >=1 at the 1.8 GB redline and publishes `ResolutionChangedSignal`. DOD: runtime-only QualitySettings change with hysteresis. Alternative rejected: persistent quality asset mutation. Estimate: 1-3 us on transition frames.
- [x] 3. TARGETED PURGE | Justification: `WorldChunkResidencyManager` drains `SectorDehydratedSignal`, skips pinned chunks, releases Addressables handles, and clears dependency cache. DOD: immediate release on sector signal. Alternative rejected: lazy-only unload. Estimate: 5-40 us on dehydration frames.
- [x] 4. FRAGMENTATION AUDIT | Justification: `GlobalDataVault` uses Burst `VaultGapAuditJob` over the block map to compute total free, largest free, gap ratio, and unaligned buffers. DOD: job scans native layout. Alternative rejected: managed array mirror. Estimate: 2-15 us per FrostTick for normal block counts.
- [x] 5. BATCHED MEMMOVE trigger | Justification: compaction triggers from `HeapFragmentationRatio > 0.20f`. DOD: ratio threshold not largest-gap folklore. Alternative rejected: old free-space-only heuristic. Estimate: 0 us/frame outside FrostTick.
- [ ] 6. BATCHED MEMMOVE execution [BLOCKED BY CONCURRENT DEPENDENCY] | Justification: a one-block `UnsafeUtility.MemMove` implementation with fence and signal was applied repeatedly, but `GlobalDataVault.cs` was concurrently overwritten back to audit-only state during verification. DOD practice used: fail-fast with three repair attempts. Alternative rejected: leaving a compile-broken dangling `TryMoveOneBlock` call. Estimate if restored: 20-300 us typical, watchdog at 0.1 ms.
- [ ] 7. POINTER UPDATE BUS [BLOCKED BY CONCURRENT DEPENDENCY] | Justification: `MemoryAddressShiftSignal` exists in `GlobalSignals`, but the current `GlobalDataVault.cs` relocation path was overwritten before stable verification, so no stable publish site remains in the vault. DOD practice used: decoupled SignalBus contract first. Alternative rejected: direct physics reference. Estimate if restored: one SignalBus enqueue on moved slices.
- [x] 8. CRITICAL MEMORY SIGNAL | Justification: residency drains `MemoryPressureSignal` severity and `SystemHealthIndexSignal` SHI-critical equivalent. DOD: decoupled signal path. Alternative rejected: dependency on absent brain code. Estimate: O(snapshot count), normally 0-2 us.
- [x] 9. ASSET REJECTION | Justification: `ShouldLoadSpeculative()` returns false while adrenaline/predictive suspension/VRAM abort is active. DOD: speculative load gate used by scheduling. Alternative rejected: allowing predictive prewarm during critical pressure. Estimate: branch only.
- [x] 10. NATIVE POOL SHRINKING | Justification: `ObjectPoolManager.TrimInactivePoolsForMemoryPressure(0.5f)` releases half inactive objects and trims queue storage. DOD: bounded release. Alternative rejected: clearing all inactive pools. Estimate: object-count dependent, critical path only.
- [x] 11. ARRAY ALIGNMENT CHECK | Justification: Burst audit counts occupied blocks whose offsets are not 64-byte aligned and marks defrag flags. DOD: cache-line audit in vault pass. Alternative rejected: separate managed audit. Estimate: included in Task 4.
- [ ] 12. BREADCRUMB CLEANUP [BLOCKED BY DEPENDENCY] | Justification: `WorldChunkResidencyManager` now invokes `IMacroDatabaseService.EvictDistant` with persistent scratch, but exact "older than 30 in-game days" cannot be enforced because `MacroDatabasePayloadHandle` carries no last-access/tombstone-day metadata. DOD: distance purge wired, age rule documented blocked. Alternative rejected: unsafe file/cache contract mutation. Estimate: 10-80 us on SlowTicks with far cached sectors.
- [ ] 13. ZERO-GC [PARTIAL / BLOCKED BY TASK 6] | Justification: audit path uses native arrays and Burst job. Relocation loop cannot be certified zero-GC because the stable memmove publish site was overwritten. DOD: static scan of current file. Alternative rejected: claiming the removed relocation path exists. Estimate: audit path 0 B/frame.
- [x] 14. AUP SHIFT SAFETY | Justification: vault still skips compaction while allocation lock/AUP shift is active, and read APIs reject during compaction fence. DOD: no move during AUP lock. Alternative rejected: relocating during origin mutation. Estimate: branch only.
- [ ] 15. MATH LOD [PARTIAL / BLOCKED BY TASK 6] | Justification: high-tier bypass helper/flag is present, but stable FrostTick integration was overwritten with the relocation call. DOD: current-file verification. Alternative rejected: marking CPU bypass done when movement is not stable. Estimate: would save FrostTick move cost on High/Ultra.
- [x] 16. BLACKBOX DUMP | Justification: metabolic blackbox remains 300 native samples and now dumps to `Dump_AGENT_HOMEOSTASIS_METABOLISM.bin`. DOD: fixed-size native ring. Alternative rejected: managed logs as crash evidence. Estimate: ring write only per FrostTick.
- [x] 17. EXECUTION PHASE | Justification: `SystemDispatcher` already invokes vault defrag from pre-simulation memory defrag lane. DOD: no per-frame Update compactor. Alternative rejected: MonoBehaviour sidecar. Estimate: existing Frost/Cold cadence.
- [ ] 18. OMEGA COMPILE CHECK [BLOCKED BY GLOBAL DEPENDENCIES] | Justification: Unity MCP session was unavailable on retry. `dotnet build Hecton8.Core.csproj` is red from unrelated missing ecology/audio/physics/database namespaces before a clean owned-script proof can complete. DOD: build evidence captured. Alternative rejected: claiming clean compile. Estimate: 0 us/frame.
- [x] 19. RECONNAISSANCE | Justification: static scan found widespread managed arrays and `new byte[]` patterns; this is logged as metabolic pathogen evidence, not mass-edited outside domain. DOD: source scan. Alternative rejected: unrelated cross-domain rewrites. Estimate: 0 us/frame.
- [x] 20. RATIONALE | Justification: decisions 2-5 appended with rejected alternatives and hardware impact. DOD: rationale before done marks. Alternative rejected: chat-only report. Estimate: 0 us/frame.

## Iteration Log
- Loop 0: Bootstrapped status. No code edits yet.
- Loop 1: Implemented VRAM tracker/mip signal/targeted purge and verified `VRAMMonitor`, `VRAMPressureMonitor`, `WorldChunkResidencyManager`, `ObjectPoolManager`, and `GlobalDataVault` through Unity MCP. `GlobalSignals` validation timed out in MCP regex, so compile proof remains open.
- Loop 2: Implemented vault Burst audit, one-block relocation signal, compaction fence, high-tier bypass, and blackbox path. `dotnet build Assembly-CSharp.csproj --no-restore` is blocked by missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- Loop 3: Added MacroDB distant-sector purge through `IMacroDatabaseService.EvictDistant`. Exact 30-day age rule is blocked by missing age metadata in the MacroDB handle contract.
- Loop 4: Ran Unity MCP validation. `GlobalDataVault`, `VRAMMonitor`, `VRAMPressureMonitor`, `WorldChunkResidencyManager`, and `ObjectPoolManager` report 0 diagnostics. `GlobalSignals` validation times out in the MCP regex engine.
- Loop 5: Requested Unity script compile and ran `dotnet build Assembly-CSharp.csproj`; both are blocked by unrelated external errors (`FaunaBrain.Ecosystem`, missing audio/physics/ecology namespaces), not by the owned metabolism files.
- Loop 6: OMEGA audit found `GlobalDataVault.cs` was concurrently overwritten during verification. Re-applied stable Burst audit and agent-specific dump path; relocation execution/pointer-publish tasks are now marked blocked rather than falsely complete.
