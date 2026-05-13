# Status_CORE_DATA_VAULT_WARDEN

Prompt: CORE_DATA_VAULT_WARDEN
Role: SYSTEMS_ARCHITECT
Domain: CORE & MEMORY INFRASTRUCTURE
Status: PENDING VERIFICATION

## Mandates Selected
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] OPT_HectonArenaAllocator_2_0.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- [x] OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- [x] DATA_Save_Persistence_Binary_Delta_Checksum.txt

## Phase 1: Tasks 1-5
- [x] 1. Singleton eradication | Justification: `rg MemoryManager.Instance Assets/_Project/Scripts` returned no hits; vault is bound by `GameBootstrapper` through `GlobalRegistry.RegisterDataVault`. DOD: no singleton call site added. Alternatives Rejected: `MemoryManager.Instance` facade. Estimate: 12 us cold bootstrap bind, 0 us hot path.
- [ ] 2. Allocator migration [BLOCKED BY SCALE / INTEGRATION RISK] | Justification: source scan still reports 709 `new NativeArray<...>(..., Allocator.Persistent)` sites across `Scripts/`. Physics AUP and dispatcher critical buffers were migrated. DOD: factual scan, no blind regex rewrite. Alternatives Rejected: mass replacement without owner mapping; would corrupt disposal and job dependencies. Estimate: migrated paths save 4-20 us per cold allocation via centralized leak tracking; remaining sites pending owner-by-owner migration.
- [x] 3. ASMDEF isolation | Justification: added `Hecton8.Core.Memory.asmdef` with no project-level dependencies; Unity dependency is only `Unity.Collections`, required for `NativeArray`, `NativeParallelHashMap`, and `UnsafeUtility`. DOD: Unity produced `Library/ScriptAssemblies/Hecton8.Core.Memory.dll`. Alternatives Rejected: folding into `Hecton8.Core` and increasing dependency churn. Estimate: 0 us runtime, reduces compile/domain coupling.
- [x] 4. Dead code hunt | Justification: static scan found manual `Dispose()` calls clustered in lifecycle/cold methods, not a confirmed `Update()` hot loop in the owned edits. DOD: no new Update-time dispose introduced. Alternatives Rejected: deleting unrelated lifecycle disposals. Estimate: avoids main-thread sync stalls of 50-500 us if misused.
- [x] 5. Vault registry | Justification: `GlobalDataVault` created with `UnsafeHashMap<int, IntPtr>` and metadata map keyed by `BufferID`; registered as `GlobalRegistryServiceSlot.DataVault`. Alternatives Rejected: managed dictionary and Unity singleton. Estimate: O(1) cold lookup, sub-us expected.

## Phase 2: Tasks 6-10
- [x] 6. Stateless systems buffer retrieval | Justification: `GlobalPhysicsStateManager` requests `BufferID.RigidbodyAUPs` from `GlobalRegistry.DataVault`; fallback uses `H8Memory.Allocate` if the vault is absent. Alternatives Rejected: hard dependency on bootstrap order. Estimate: sub-us buffer retrieval after cold map lookup.
- [x] 7. Lifetime decoupling | Justification: vault-owned Rigidbody AUP memory is not disposed when physics drops the view; system disable no longer owns that backing allocation. Alternatives Rejected: per-system authoritative persistent arrays. Estimate: prevents cold reallocation spikes of roughly 10-40 us on re-enable.
- [x] 8. Vault defrag frost tick | Justification: `SystemDispatcher.RunFrostTick` calls `DataVault.FrostTickDefrag`; implementation intentionally avoids moving buffers because outstanding `NativeArray` views would become dangling pointers, while growth uses aligned copy/free reallocation. Alternatives Rejected: unsafe blind `MemRealloc` while aliases exist. Estimate: 0 us hot path, cold evaluation hook under 5 us.
- [x] 9. H8Memory API | Justification: `H8Memory.Allocate<T>(int length, SystemID owner, Allocator allocator, NativeArrayOptions options)` plus release, raw allocate/free, reallocate, alias, dump, and shutdown APIs implemented. Alternatives Rejected: wrapping only `NativeMemorySentinel`, which cannot own raw vault pointers. Estimate: 1-5 us cold tracking overhead, 0 us job hot path.
- [x] 10. Leak tracker | Justification: `NativeParallelHashMap<long, SystemID>` tracks pointer keys with native records; `long` is used instead of `IntPtr` because the local Unity collections generic constraint requires an equatable unmanaged key. Alternatives Rejected: managed `Dictionary<IntPtr,...>` and compile-fragile `IntPtr` key. Estimate: sub-us hash insert/remove on cold allocation.

## Phase 3: Tasks 11-15
- [x] 11. Auto-dispose sentinel | Justification: `GlobalRegistry.UnregisterService` and replacement path call `H8Memory.ReapOwnerLeaks` for mapped owners and log `[FATAL LEAK PREVENTED]` when memory is reaped. Alternatives Rejected: relying on every system to remember disposal. Estimate: 0 us normal hot path, cold unregister scan proportional to active allocation count.
- [x] 12. Aliasing guards | Justification: `H8Memory.CreateAlias<T>()` and vault alias retrieval return read-only aliases without copies, using safety handles under collection checks. Alternatives Rejected: duplicate `NativeArray` copies. Estimate: avoids copy cost; alias construction sub-us.
- [x] 13. Bounds checking | Justification: vault validates stride and alignment under `ENABLE_UNITY_COLLECTIONS_CHECKS`; length growth is explicit through `requiredLength`. Alternatives Rejected: trusting callers with raw pointer casts. Estimate: development-only branch, 0 us release cost.
- [x] 14. AUP shift safety | Justification: `SystemDispatcher.RequestAupPreShiftPause` locks vault allocations and unlocks after the frame barrier; vault refuses grow/create while locked. Alternatives Rejected: pointer mutation during AUP rebasing. Estimate: one registry lookup and branch per shift frame.
- [x] 15. Math LOD pool cap | Justification: `H8Memory` defaults pool cap to 512 MB for low-tier shared-memory protection. Alternatives Rejected: unbounded persistent native heap. Estimate: 0 us hot path, one addition/compare per cold allocation.

## Phase 4: Tasks 16-19
- [x] 16. Zero-GC validation pass | Justification: hot APIs use native collections, unsafe pointers, value records, and no managed owner tables; only cold fatal dump touches `StreamWriter`/directories. Alternatives Rejected: managed owner dictionaries and per-call string labels. Estimate: expected 0 B GC on allocate/release hot path after initialization; profiler proof still pending integrator runtime pass.
- [x] 17. Blackbox dump | Justification: `H8Memory.DumpAllocationTableText(path)` writes owner/pointer/byte table and `GameBootstrapper` now installs a one-shot fatal/exception/assert log hook to `Docs/AgentLogs/Dump_CORE_DATA_VAULT_WARDEN.txt`. Alternatives Rejected: binary-only dump unreadable by integrators; pulling `UnityEngine` into `Hecton8.Core.Memory`. Estimate: cold crash-only, no normal frame cost.
- [x] 18. Cross-domain physics audit | Justification: `GlobalPhysicsStateManager` now sources `_rigidbodyAUPs` from `BufferID.RigidbodyAUPs` and only releases fallback-owned memory. Alternatives Rejected: direct physics-owned AUP persistent array. Estimate: prevents one cold array allocation and ownership ambiguity.
- [x] 19. Omega compile check | Justification: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` passed with 0 warnings and 0 errors after Unity regenerated asmdef references; memory-only focused check also passed. Alternatives Rejected: relying on stale generated csproj state. Estimate: compile verification only, 0 runtime us.

## Iteration Log
- Loop 0: Prompt extracted from `Docs/Tasks/ANOTHER_BATCH.md`. Status/Rationale files created.
- Loop 1: Mandates and domain docs read; `MemoryManager.Instance` and persistent `NativeArray` blast radius measured. Result: no singleton infection, 709 allocator migration sites.
- Loop 2: Implemented `Hecton8.Core.Memory`, `H8Memory`, `SystemID`, `BufferID`, native pointer records, low-tier cap, and dump API.
- Loop 3: Implemented `GlobalDataVault`, registry slot, bootstrap registration, and shutdown sequencing through `GameBootstrapper`.
- Loop 4: Wired AUP allocation lock in `SystemDispatcher` and migrated dispatcher/physics critical buffers to `H8Memory` or vault.
- Loop 5: Re-read prompt, ran Unity/dotnet verification. Memory assembly compiled; targeted check after fatal dump hook reached unrelated `RadiationHazardGrid.CompleteDiffusionJobForTeardown` compile wall.
- Loop 6: OMEGA polish read after all tasks were checked or blocked. Removed crash-dump `.ToString`, verified no forbidden managed iteration/formatting/sqrt/normalize in `Core/Memory`, ran focused memory compile, then ran full `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` successfully. Status remains PENDING VERIFICATION by prompt mandate because Task 2 is blocked by 709 unmigrated persistent allocations outside the safe scope.
- Loop 7: User requested continued quality pass and explicitly banned `dotnet build`. Re-read status/rationale/prompt, audited memory and integration code, patched tracking-table growth before allocation, null `Malloc` guard, overflow-safe pool-cap check, owner-byte bounds guards, enum-free dump writes, `BufferID.Unknown` rejection, and vault insertion rollback on map failure. Static no-build checks passed: no forbidden memory-pattern hits and `git diff --check` only reported existing line-ending warnings.
- Loop 8: Continued no-build quality pass. Patched `H8Memory.ReallocateRaw` so vault growth tests the final post-reallocation footprint instead of old+new temporary bytes, preserving the 512MB cap without false failures. Patched `GlobalRegistry.ResolveMemoryOwner` to avoid byte-cast owner collisions and tightened vault `TryGetBuffer` against `BufferID.Unknown`. Static no-build checks passed: no forbidden memory-pattern hits and `git diff --check` only reported existing line-ending warnings.
