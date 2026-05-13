# Rationale_CORE_DATA_VAULT_WARDEN

Status: PENDING VERIFICATION

## Initial Constraints
Problem: Native memory ownership is scattered across runtime systems; persistent buffers can leak or die with disabled systems.
Solution: Build a Core memory layer with `H8Memory` ownership tracking and `GlobalDataVault` buffer sovereignty.
Rejected Alternatives: Per-system `NativeArray` fields remain leak-prone and duplicate ownership; classic Unity singleton memory managers violate GlobalRegistry rules.
Scalability potential: Low = capped 512MB vault pool and minimal buffer growth; Middle = deterministic persistent buffers; High = larger hot buffers; Ultra = visual overkill funded by predictable memory residency.
Hardware Impact: i3/MX350 target needs allocation caps and zero hot-path managed churn to avoid shared-memory pressure and GC spikes.

## Decision 1: Separate Core Memory Assembly
Problem: Memory infrastructure must sit below system domains without pulling gameplay, world, physics, or UI dependencies into the lowest level.
Solution: Created `Hecton8.Core.Memory.asmdef` containing only memory contracts and unsafe/native allocation infrastructure. It depends on Unity native collection assemblies because the mandate itself requires `NativeArray`, `NativeParallelHashMap`, and `UnsafeUtility`.
Rejected Alternatives: Adding `H8Memory` to `Hecton8.Core` would couple allocation ownership to the entire core gameplay assembly. A classic Unity singleton would violate registry/bootstrap rules.
Scalability potential: Low = one tiny memory assembly loads cheaply; Middle = all systems share one allocation API; High = future platform-tier policies can be centralized; Ultra = vault capacity can be expanded to feed visual-overkill buffers without changing systems.
Hardware Impact: i3/MX350 avoids project-wide recompile churn and keeps memory policy in one small native-facing assembly. Estimated gain is compile/dependency hygiene, not frame time.

## Decision 2: `H8Memory` Sentinel API
Problem: Persistent native allocations are invisible after a system fails to dispose, so owner attribution dies with the system.
Solution: Added `H8Memory.Allocate<T>`, release, deferred release, raw allocate/free, copy-based raw reallocate, alias, owner leak reap, and text dump APIs. Tracking uses native arrays plus `NativeParallelHashMap<long, SystemID>`.
Rejected Alternatives: `NativeParallelHashMap<IntPtr, SystemID>` matched the prompt text but is compile-fragile with local Unity collections key constraints; `long` stores the same pointer value and satisfies the native hash key contract. Managed dictionaries were rejected for GC and shutdown-order risk.
Scalability potential: Low = 512MB cap blocks shared-memory exhaustion; Middle = owner IDs support deterministic cleanup; High = owner byte totals can become tier budgets; Ultra = overkill systems can reserve memory explicitly and still be reaped.
Hardware Impact: i3/MX350 gains bounded persistent memory and deterministic cold cleanup. Allocation tracking cost estimated at 1-5 us per cold allocation, 0 us per job execution.

## Decision 3: Global Data Vault
Problem: Systems owning their own `NativeArray` data lose state across kill-switch disable/re-enable and make crash recovery ambiguous.
Solution: Added `GlobalDataVault` with `UnsafeHashMap<int, IntPtr>` for raw buffers, metadata for length/stride/alignment, and `NativeList<int>` key tracking for disposal. Systems receive `NativeArray` views over vault-owned raw memory.
Rejected Alternatives: Managed `Dictionary<BufferID, NativeArray<T>>` would allocate and box around generic buffers. Moving memory blindly during defrag would invalidate active `NativeArray` views.
Scalability potential: Low = only critical persistent buffers use vault; Middle = more stateful systems can move to BufferID handles; High = buffer growth can follow hardware tier; Ultra = large visual-state buffers survive renderer/system toggles without regeneration.
Hardware Impact: i3/MX350 avoids repeated cold allocations on re-enable and caps the global pool. Expected saved cold spike for migrated physics AUP path is 10-40 us plus reduced leak risk.

## Decision 4: GlobalRegistry Leak Reap
Problem: Unregistering a service should not leave native memory behind when that service forgets disposal.
Solution: `GlobalRegistry` maps service slots to `SystemID` and calls `H8Memory.ReapOwnerLeaks` on unregister/replace, skipping the DataVault slot so vault teardown remains explicit through bootstrap.
Rejected Alternatives: Requiring every domain owner to implement perfect disposal was rejected. Reaping `CoreDataVault` on ordinary unregister was rejected because vault buffers intentionally outlive client systems.
Scalability potential: Low = leak prevention for critical owners; Middle = more slots can map to stable owners; High = allocator table can become a runtime budget dashboard; Ultra = crash triage can identify over-budget visual systems directly.
Hardware Impact: i3/MX350 prevents progressive OOM after repeated scene/system toggles. Runtime impact is 0 us until cold unregister.

## Decision 5: AUP Allocation Lock
Problem: Pointer mutation during AUP pre-shift can tear views while physics/world positions are being rebased.
Solution: `SystemDispatcher.RequestAupPreShiftPause` locks vault growth/creation and unlocks after the pre-shift frame barrier resolves. `GlobalDataVault.GetBuffer` returns default rather than reallocating while locked.
Rejected Alternatives: Allowing allocation during shift and trusting caller timing was rejected. Global pause of all native memory allocation was rejected because non-vault systems may still be cleaning up safely.
Scalability potential: Low = no pointer relocation during shift; Middle = future vault buffers inherit the lock for free; High = multi-frame shift locks can be extended; Ultra = large visual buffers can remain stable during cinematic world shifts.
Hardware Impact: i3/MX350 avoids rare but severe pointer invalidation crashes. Cost is a branch and registry lookup only on shift frames.

## Decision 6: Physics AUP Vault Ownership
Problem: The physics audit required Rigidbody AUP arrays to come from the vault, not a physics-owned persistent field.
Solution: `GlobalPhysicsStateManager` now asks `GlobalRegistry.DataVault.GetBuffer<float3>(BufferID.RigidbodyAUPs, MaxTrackedBodies, SystemID.GlobalPhysicsStateManager)` and drops only the view on dispose. If bootstrap ordering fails, it falls back to `H8Memory.Allocate` with explicit owner tracking.
Rejected Alternatives: Hard-failing without vault would create a brittle bootstrap dependency. Keeping the direct array ignored the cross-domain audit.
Scalability potential: Low = one authoritative Rigidbody AUP buffer; Middle = culling/awake arrays can be moved next; High = physics state survives kill-switch cycling; Ultra = high-end devices can retain larger historical physics buffers.
Hardware Impact: i3/MX350 avoids one persistent AUP reallocation on physics re-enable and centralizes leak cleanup. Estimated saved cold time: 10-40 us.

## Decision 7: Defrag Scope
Problem: Prompt requested optional `MemRealloc` defrag every 5 seconds, but active `NativeArray` views cannot be transparently repointed.
Solution: Hooked `FrostTickDefrag` from the dispatcher, but kept current implementation non-moving until relocation-safe handles exist. Growth uses copy/free via `H8Memory.ReallocateRaw`, with alignment from `UnsafeUtility.AlignOf<T>()`.
Rejected Alternatives: Blind `UnsafeUtility.MemRealloc` or move/free of live buffers was rejected as pointer tearing. Per-frame defrag was rejected as frame-time waste.
Scalability potential: Low = no surprise frame spikes; Middle = cold evaluation hook exists; High = future relocation handles can enable compaction; Ultra = high-end visual buffers can be resized in cold windows.
Hardware Impact: i3/MX350 cost is effectively 0 us today. Moving defrag would risk crashes and hidden stalls.

## Decision 8: Verification Result
Problem: Generated `.csproj` metadata initially lagged asmdef import and produced false missing-reference errors.
Solution: Verified Unity imported `Hecton8.Core.Memory.dll`, ran a focused memory-source compile, then reran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` after project references regenerated. Final full core build passed with 0 warnings and 0 errors.
Rejected Alternatives: Reporting green before regeneration was rejected. Editing unrelated domains during the transient compile wall was rejected.
Scalability potential: Low = build metadata now sees the memory asmdef; Middle = integrator can validate through normal core build; High = future memory domains can reference the same lowest-level assembly; Ultra = full batch orchestration should still serialize generated project refresh.
Hardware Impact: None runtime. Compile verification only.

## Decision 9: Fatal Allocation Dump Hook
Problem: A dump API alone does not satisfy the crash-forensics requirement; some bootstrap-level path must call it when Unity reports a fatal condition.
Solution: `GameBootstrapper` installs a one-shot `Application.logMessageReceived` hook after `H8Memory.Initialize`. Exceptions, asserts, and fatal/crash/NaN errors dump `H8Memory` allocation state to `Docs/AgentLogs/Dump_CORE_DATA_VAULT_WARDEN.txt`.
Rejected Alternatives: Adding UnityEngine dependencies inside `Hecton8.Core.Memory` was rejected to keep the memory assembly low-level. Dumping on every `LogType.Error` was rejected to avoid noisy cold-path writes for non-fatal editor errors.
Scalability potential: Low = one readable allocation table on failure; Middle = integrator can correlate owner IDs with systems; High = crash pipeline can upload the text artifact; Ultra = high-end visual systems can be audited by byte ownership after overkill buffer growth.
Hardware Impact: i3/MX350 normal-frame cost is 0 us. Crash-only file IO cost is irrelevant to frame time and prevents "unknown owner" postmortems.

## OMEGA POLISH CHANGES
Problem: The polish mandate required anti-bloat review after task closure, including string formatting, managed iteration, sqrt/normalize, and final build proof.
Solution: Removed the crash-dump `record.Pointer.ToInt64().ToString("X")` path and wrote the pointer integer directly. Ran focused scans over `Assets/_Project/Scripts/Core/Memory/*.cs`; result: 0 hits for `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, and `math.normalize`. Ran focused memory compile and full `Hecton8.Core.csproj` build.
Rejected Alternatives: Keeping `.ToString("X")` because it is crash-only was rejected; mandate bans it and decimal pointer values are sufficient for owner triage. Moving all 709 persistent arrays in one pass was rejected as unsafe ownership churn.
Scalability potential: Low = dump path stays cold and allocation-free during normal frames; Middle = memory layer stays clean for future domain migrations; High = owner budgets can feed platform-tier allocation policy; Ultra = saved memory discipline buys larger visual-state buffers without leaks.
Hardware Impact: i3/MX350 hot path remains 0 B GC and no sqrt/normalize cost. Microseconds saved by pointer-dump cleanup are crash-only/negligible; the practical gain is policy compliance.
Cinematic Cheats used: copy/free raw reallocation instead of risky live defrag; non-moving FrostTick evaluation instead of pointer relocation; low-tier 512MB cap to trade unbounded realism for predictable visual budget.
Final Git Diff: Added untracked `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, `GlobalDataVault.cs`, `Hecton8.Core.Memory.asmdef`, and `.meta` files. Modified `GameBootstrapper.cs`, `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`, `SystemDispatcher.cs`, `GlobalPhysicsStateManager.cs`, and `Hecton8.Core.asmdef`. Git diff stat over modified tracked files currently reports 6 files changed, 2993 insertions, 149 deletions, but that includes pre-existing concurrent-agent edits in dirty files; the exact CORE_DATA_VAULT_WARDEN symbols are `H8Memory`, `GlobalDataVault`, `IDataVault`, `SystemID`, `BufferID`, `DataVault`, `RigidbodyAUPs`, `Dump_CORE_DATA_VAULT_WARDEN`, and `[FATAL LEAK PREVENTED]`.

## Decision 10: No-Build Hardening Pass
Problem: Static review found two concrete failure modes: once `_records` reached capacity, `H8Memory` could allocate memory that was never tracked; and a failed `GlobalDataVault` map insertion after raw allocation could leak the just-allocated pointer.
Solution: `H8Memory` now grows native tracking tables before allocation up to 65,536 records, rejects allocation if tracking cannot expand, guards null `UnsafeUtility.Malloc`, uses overflow-safe cap comparison, bounds-checks owner byte counters, and writes integer owner/allocator values in dumps. `GlobalDataVault` now rejects `BufferID.Unknown`, refuses inconsistent pointer/metadata pairs, and rolls back/free raw memory if buffer or metadata insertion fails.
Rejected Alternatives: Silent untracked allocation was rejected because it defeats the Sentinel. Fixed-size 4,096 tracking was rejected as not scalable enough for the remaining migration debt. Managed fallback lists were rejected for GC and domain teardown risk.
Scalability potential: Low = 4,096 records remain cheap; Middle = growth handles broader migration; High = 65,536 tracked native allocations before hard failure; Ultra = high-end visual-overkill buffers can still be owner-audited without losing Sentinel coverage.
Hardware Impact: i3/MX350 pays 0 us hot path; growth is cold allocation only when the tracking table saturates. Prevented leak/OOM risk is materially more important than the one-time table copy.
Verification: No `dotnet build` was launched per user instruction. Static scans over `Core/Memory` found 0 hits for managed `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, and `math.normalize`. `git diff --check` reported only existing LF/CRLF warnings, no whitespace errors.

## Decision 11: Replacement-Aware Vault Growth
Problem: `H8Memory.ReallocateRaw` used `AllocateRaw(newBytes)` while the old buffer was still counted. A valid grow from 300MB to 400MB under a 512MB cap could fail because the temporary accounting looked like 700MB.
Solution: `ReallocateRaw` now computes cap pressure against the final footprint: `_totalBytes - oldBytes + newBytes`. It allocates the replacement raw block directly, copies the retained bytes, clears only the extended range when requested, unregisters/frees the old pointer, then registers the new pointer.
Rejected Alternatives: Temporarily raising the 512MB cap was rejected because it breaks the low-tier shared-memory guarantee. Keeping old+new accounting was rejected because it blocks legitimate vault growth and forces unnecessary fallback allocations.
Scalability potential: Low = small buffers unchanged; Middle = large vault buffers can grow in place logically without false cap failure; High = final-footprint accounting supports tiered buffer budgets; Ultra = visual-overkill buffers can resize without punching through the cap.
Hardware Impact: i3/MX350 gains predictable cap behavior. Normal frame cost remains 0 us; resize remains cold copy/free work only.
Verification: No `dotnet build` launched. Static scans over `Core/Memory` remain clean for forbidden managed iteration/string formatting/sqrt/normalize. `git diff --check` reported only line-ending warnings.

## Decision 12: Registry Owner Collision Guard
Problem: Fallback owner mapping in `GlobalRegistry.ResolveMemoryOwner` cast service slots through `byte`, which is unnecessary and can collide if registry slots grow past 255.
Solution: Replaced byte-cast fallback with direct integer owner calculation and `SystemID.External` overflow fallback. Also tightened `GlobalDataVault.TryGetBuffer` to reject `BufferID.Unknown`.
Rejected Alternatives: Leaving the byte cast was rejected because future service growth would make leak reaping ambiguous. Broad registry refactor was rejected as unrelated churn.
Scalability potential: Low = current slots unchanged; Middle = more services can receive distinct owner IDs; High = leak reaping scales with registry expansion; Ultra = postmortem allocation dumps stay attributable even as visual/AI domains multiply.
Hardware Impact: 0 us hot path difference; this is correctness insurance for cold unregister and crash dump attribution.
Verification: No `dotnet build` launched. Source inspection and `git diff --check` only.
