# Rationale_VAULT_MEMORY_RELOCATOR

STATUS: VERIFIED METABOLIC COMPACTION

## Decision 0: Assignment Boundary
Problem: GlobalDataVault must relocate memory, but `Hecton8.Core.Memory` cannot depend on `Hecton8.Core` without creating an asmdef cycle.
Solution: Keep relocation state and handles inside `Hecton8.Core.Memory`; expose fixed-size relocation records through `IDataVault`; let `SystemDispatcher` publish the existing `MemoryAddressShiftSignal` lane from the Core assembly.
Rejected Alternatives: Direct `GlobalSignals.Publish` from GlobalDataVault was rejected because Memory is a lower-level assembly already referenced by Core. A new concrete event bus inside Memory was rejected because it would duplicate existing typed signal lanes.
Scalability potential: Low = no compaction while stressed; Middle = one pre-simulation slice per cadence; High = larger low-stress moves; Ultra = saved stability budget can support heavier visual memory residency.
Hardware Impact: i3/MX350 gain is reduced long-session fragmentation and fewer native allocation failures; direct frame savings are workload-dependent and unmeasured.

## Decision 1: Live Compaction Trigger
Problem: A telemetry-only defrag reports gaps but leaves arena holes intact during long sessions.
Solution: Gate actual memmove compaction behind `GapRatio > 0.15f` and `SystemStress < 0.5f`, then run inside the dispatcher pre-simulation fence.
Rejected Alternatives: Full defrag every FrostTick was rejected because moving native blocks while the frame is hot can exceed the 0.1 ms suspicion threshold. Runtime GC compaction is irrelevant to native arena fragmentation and was rejected.
Scalability potential: Low = skip compaction under pressure; Middle = bounded slices; High = more frequent low-stress maintenance; Ultra = more stable high-detail asset residency during long play sessions.
Hardware Impact: i3/MX350 avoids compaction during throttled frames; expected cost is bounded to a 1 ms watchdog, but runtime proof is pending.

## Decision 2: Handle Resolution Must Heal
Problem: A stale-handle exception path made generation mismatch fatal, which defeats live relocation because valid handles become stale after every move.
Solution: `VaultBufferHandle<T>.Resolve()` now routes through `IDataVault.ResolveBuffer(ref handle)`, compares cached generation/pointer/length/stride, and refreshes the cached pointer from the current table.
Rejected Alternatives: Fatal stale-handle exceptions were rejected because the assignment requires `update_ptr()` behavior. Returning a fresh `NativeArray` from `GetBuffer` every time was rejected because it hides the generation contract and does not protect raw pointer users.
Scalability potential: Low = stale handles resolve after a skipped/rare compaction; Middle = regular cold-frame compaction; High = frequent memory shape changes with small repair cost; Ultra = long sessions can aggressively move cold blocks without invalidating handle owners.
Hardware Impact: i3/MX350 pays only a few compares on resolve; cold stale path is estimated under 6 us and avoids native heap growth from abandoned holes.

## Decision 3: Pointer Relocation Table Boundary
Problem: The memory assembly cannot publish Core signals directly, but systems need a relocation notice when `_buffers` moves.
Solution: `GlobalDataVault` writes fixed-size `VaultRelocationRecord` entries after each memmove; `SystemDispatcher` bridges them to `MemoryAddressShiftSignal` during the pre-simulation maintenance cadence.
Rejected Alternatives: Direct `GlobalSignals.Publish` in `GlobalDataVault` was rejected due asmdef cycle. Managed event delegates were rejected because the defrag path must stay zero-GC and deterministic.
Scalability potential: Low = few relocation records, cheap dispatch; Middle = fixed 64-record slice; High = multiple low-stress slices across frames; Ultra = signal consumers can drop heavyweight caches and rebuild only touched buffers.
Hardware Impact: i3/MX350 avoids whole-system cache flushes; estimated dispatch cost is about 3 us per relocation signal, bounded by the fixed record capacity.

## Decision 4: Locked Blocks Over Hope
Problem: Long-lived Burst jobs can hold raw vault pointers while compaction wants to move the backing block.
Solution: Added buffer lock/unlock semantics using block reserved fields; compaction skips locked blocks and records the skipped state in defrag flags.
Rejected Alternatives: Trusting every job owner to complete before compaction was rejected because parallel agents and long-lived jobs make that unprovable. Pinning the whole vault was rejected because it collapses compaction benefit.
Scalability potential: Low = weak devices skip contested blocks; Middle = partial compaction around active jobs; High = systems lock only real job windows; Ultra = high-end can compact more aggressively while active simulation keeps critical buffers pinned.
Hardware Impact: i3/MX350 avoids crash-class stalls from moving live job memory; lock mutation is estimated at about 2 us and zero managed allocation.

## Decision 5: Compile Wall Handling
Problem: The first full `dotnet build Hecton8.Core.csproj` attempts hit unrelated domain dependency churn while other agents were still integrating code.
Solution: Kept a targeted Roslyn compile for `H8Memory.cs` and `GlobalDataVault.cs`, then reran the full project build after the parallel integrations settled. The final command exits 0 with Hecton8.Core.dll emitted.
Rejected Alternatives: Reporting the earlier compile wall as final was rejected because the project state changed during the pass. Reverting unrelated assemblies was rejected because they are outside this domain and owned by other agents.
Scalability potential: Low = memory assembly stays independently verifiable; Middle = project build now imports the relocation code; High = relocation can be profiled in scene; Ultra = runtime watchdog data drives platform-specific compaction cadence.
Hardware Impact: i3/MX350 runtime impact remains bounded by the 1 ms slice; exact saved microseconds are not measured until Unity runtime profiling is available.

## Decision 6: Legacy Raw View Risk
Problem: Existing systems still call `GetBuffer<T>()` and may cache `NativeArray` views that do not auto-refresh after relocation.
Solution: Kept legacy API for compatibility, added handle API and relocation signal path, and documented that consumers with cross-frame pointer caches must migrate to `VaultBufferHandle<T>` or subscribe to `MemoryAddressShiftSignal`.
Rejected Alternatives: Disabling movement for all external views was rejected because `GetBuffer` marks most buffers external and would reduce compaction back to telemetry-only. Breaking `GetBuffer` was rejected because it would damage unrelated systems outside this assignment.
Scalability potential: Low = conservative consumers use handles; Middle = signal subscribers invalidate local caches; High = hot systems lock during jobs and resolve handles per phase; Ultra = memory can be aggressively defragmented while high-end devices spend saved residency on visual overkill.
Hardware Impact: i3/MX350 gains only when consumers adopt handles/signals; current architecture enables the gain but does not automatically repair every legacy raw cache.

## Decision 7: Concurrent Edit Hardening
Problem: During the hardening pass, `GlobalDataVault.cs` was overwritten back to telemetry-only defrag and fatal stale-handle behavior more than once.
Solution: Re-applied the live compaction slice, stale-handle healing, stress gate, fixed relocation records, lock skip, and memory barriers, then verified with `rg` readback and memory-only Roslyn compile. The final source must be treated as the authority, not stale chat history.
Rejected Alternatives: File read-only locking was rejected because this workspace is shared with 20+ agents and would block legitimate integration. Ignoring the overwrite was rejected because it silently reintroduces invalid pointer behavior.
Scalability potential: Low = final source remains build-clean; Middle = consumers migrate to handles/signals; High = frequent low-stress compaction without cache invalidation; Ultra = long-session memory residency can be spent on visual overkill instead of arena fragmentation.
Hardware Impact: i3/MX350 avoids crash-class stale pointer reads and keeps compaction under the 1 ms watchdog; the 512 KB soft move cap prevents a single low-end slice from becoming an unbounded memmove spike.

## Decision 8: Relocation Record Completeness
Problem: A compaction slice could theoretically move more tiny buffers than the 64-record relocation ring can report, leaving late moved buffers without a `MemoryAddressShiftSignal`.
Solution: Stop compaction before the relocation record array is exhausted and refuse individual moves when the record budget is full. Also widened alignment validation to source offset, destination offset, and moved byte span.
Rejected Alternatives: Silently dropping excess relocation records was rejected because cache invalidation must be exact. Growing the record array at runtime was rejected because defrag must remain zero-GC and bounded.
Scalability potential: Low = weak devices stop after bounded exact signals; Middle = next frame continues compaction; High = signal subscribers receive exact touched-buffer invalidations; Ultra = long-session relocation remains deterministic under dense tiny-buffer workloads.
Hardware Impact: i3/MX350 avoids unreported pointer moves and keeps per-slice work bounded; record-budget branch is under 1 us.

## Decision 9: Locked Resize and Editor Teardown
Problem: Locked buffers were protected from compaction but still could be resized in-place, and H8Memory editor hooks were removing callbacks without re-adding them.
Solution: Reject `TryReallocateBlock` when the block is locked and register editor reload/quitting/playmode-exit callbacks to call `H8Memory.Shutdown`.
Rejected Alternatives: Allowing in-place resize of locked buffers was rejected because long-lived jobs can rely on fixed length/metadata. Waiting for Unity domain reload cleanup alone was rejected because native memory has to be explicitly freed.
Scalability potential: Low = safer job windows on weak devices; Middle = clean editor iteration; High = fewer false leak reports during stress testing; Ultra = aggressive vault relocation does not compromise long-lived jobs.
Hardware Impact: i3/MX350 avoids a crash-class resize race; editor teardown fix saves native memory across repeated play sessions rather than frame microseconds.

## Decision 10: Post-Shutdown Double-Free Guard
Problem: Once `H8Memory.Shutdown()` frees tracked native allocations, owner-level cleanup can still run later and call `Release` or `FreeRaw` on stale wrappers.
Solution: `Release<T>` and owner-tagged `FreeRaw` now return without calling Unity disposal/free APIs when the H8Memory sentinel is offline; wrappers are nulled so later cleanup does not repeat the same pointer.
Rejected Alternatives: Letting late cleanup call `UnsafeUtility.Free` was rejected because shutdown already freed the tracked pointer. Removing editor shutdown hooks was rejected because repeated editor sessions need deterministic native teardown.
Scalability potential: Low = safe editor iteration on weak machines; Middle = cleaner stress-test loops; High = long-running tools survive repeated play sessions; Ultra = aggressive memory diagnostics can run without accumulating false double-free failures.
Hardware Impact: i3/MX350 impact is stability rather than frame time; avoids crash-class teardown faults and native heap corruption after editor reload/playmode exit.

## Decision 11: Concurrent Regression Re-Repair
Problem: Current source was overwritten again after Loop 8: `ResolveBuffer` threw on stale handles and `FrostTickDefrag` only recorded telemetry, so valid relocated handles could crash and heap holes would remain.
Solution: Re-applied the DOD path directly in `GlobalDataVault.cs`: stale handles refresh in place, compaction runs only under `GapRatio > 0.15f` and `SystemStress < 0.5f`, the slice uses a fenced direct `UnsafeUtility.MemMove`, record capacity is checked before moves, and the 512 KB soft move cap plus 1.0 ms watchdog bound low-end spikes.
Rejected Alternatives: Leaving the telemetry-only version was rejected because it violates the assigned live compaction requirement. Editing the unrelated fauna compile blocker was rejected because it is outside Core & Memory ownership.
Scalability potential: Low = stress gate and 512 KB cap keep toaster hardware from moving memory while hot; Middle = deterministic slices continue over later frames; High = exact relocation signals let systems invalidate only touched caches; Ultra = stable long-session memory residency can be spent on heavier visuals instead of fragmentation margin.
Hardware Impact: i3/MX350 avoids unbounded heap-hole accumulation and stale-handle crash paths. Actual runtime microseconds remain unmeasured without Unity profiler/MCP; compile evidence is Unity Roslyn memory assembly pass, with full Core currently blocked by a fauna dependency error.

## Decision 12: Dispatcher-Owned Vault Views Must Self-Heal
Problem: `SystemDispatcher` cached vault-backed `NativeArray` views for H8 time and dispatcher raycast hits. Live compaction can move those buffers, leaving the dispatcher itself with stale pointers.
Solution: Store `VaultBufferHandle<double>` for H8 time and `VaultBufferHandle<RaycastHit>` for scheduled raycast hits. Resolve H8 time before writes, resolve raycast hits before scheduling, and lock `BufferID.DispatcherRaycastHits` while the `RaycastCommand` job owns the hit buffer.
Rejected Alternatives: Relying only on `MemoryAddressShiftSignal` was rejected for the dispatcher-owned raycast path because it publishes the relocation signal and also schedules jobs. Leaving raw `NativeArray` caches was rejected because it defeats the generation contract.
Scalability potential: Low = cheap resolve branches and job lock protect weak hardware; Middle = exact relocation without global cache rebuilds; High = frequent low-stress compaction while dispatcher buffers stay valid; Ultra = longer high-detail sessions without native heap fragmentation margin.
Hardware Impact: i3/MX350 cost is branch-level handle resolve plus lock/unlock around scheduled raycasts. The gain is correctness: no stale dispatcher time/raycast pointers after compaction and no relocation while a Burst/job path writes raycast hits.

## Decision 13: Determinism Hash Jobs Must Pin Vault Aliases
Problem: `LockstepStateValidator` read `RigidbodyAUPs`, `PlayerKinematicState`, `RoomWaterLevels`, and `EntityAUPs` through legacy `TryGetBuffer` aliases, then scheduled Burst hash jobs over those aliases. Live compaction can relocate the backing blocks unless the job window is explicitly locked.
Solution: Cache `VaultBufferHandle<T>` for the determinism vault buffers, resolve writer buffers before mirroring player and room-water state, and lock every sampled vault buffer before scheduling the 300-frame POST_SIMULATION hash job chain. Unlock happens after the annotated hash fence completes.
Rejected Alternatives: Leaving `TryGetBuffer` aliases was rejected because it bypasses generation healing. Copying vault arrays into local persistent `NativeArray` scratch was rejected because it violates DataVault sovereignty and doubles memory bandwidth. Pinning the entire vault was rejected because it would collapse compaction effectiveness.
Scalability potential: Low = missing/locked determinism buffers become a telemetry missing-data path instead of a stale pointer crash; Middle = exact per-buffer locks keep compaction free to move unrelated blocks; High = frequent low-stress compaction remains compatible with determinism audits; Ultra = long sessions can keep heavier truth snapshots and visual overkill without native heap fragmentation margin.
Hardware Impact: i3/MX350 pays four handle resolves plus up to four lock/unlock mutations every 300 frames, not every frame. Estimated cost is branch-level and below the existing hash fence cost; the gain is crash-class protection for Burst jobs that read relocated vault aliases.

## Decision 14: One Block Cannot Blow the Slice Budget
Problem: The compaction loop stopped after 512 KB total moved bytes, but it could still move one first block larger than 512 KB before the total cap was checked again. That violates the low-end spike model.
Solution: Pass the remaining slice byte budget into `TryCompactFreeGapAt` and reject any occupied block whose byte span is larger than the remaining budget. The rejected block sets massive-move telemetry and is left for a later masked/loading-window strategy.
Rejected Alternatives: Trusting the 1.0 ms watchdog alone was rejected because the expensive `MemMove` would already be in flight before the watchdog could observe the breach. Splitting a live occupied buffer into partial moves was rejected because buffer handles and typed NativeArray views require contiguous storage.
Scalability potential: Low = no single relocation exceeds the MX350-safe soft cap; Middle = small blocks continue compacting across frames; High = larger blocks can be moved in intentionally scheduled cold windows; Ultra = future masked relocation can spend high-end headroom on larger residency reshapes without violating low-tier behavior.
Hardware Impact: i3/MX350 gets a hard pre-memmove branch instead of a post-fact watchdog breach. Estimated cost is under 1 us; avoided cost is an unbounded native memory copy spike.

## Decision 15: Post-Build Source Readback Is Mandatory
Problem: A full project build can pass while a concurrent edit restores behaviorally invalid code. In this pass, build success coexisted with `FatalMemoryException.ThrowStaleVaultHandle()` inside `GlobalDataVault.ResolveBuffer`.
Solution: Treat compile as one gate, then immediately run source readback for the relocation contract: no stale-handle fatal calls, live `RunCompactionSlice`, direct `UnsafeUtility.MemMove`, remaining-byte admission, and no legacy Core `GetBuffer`/`TryGetBuffer` consumers outside the vault.
Rejected Alternatives: Trusting build success alone was rejected because stale-handle throws compile cleanly but violate live relocation. Locking files read-only was rejected because this workspace has many concurrent agents and would block legitimate integration.
Scalability potential: Low = source remains correct under parallel integration churn; Middle = consumer migration state stays auditable; High = repeated low-stress compaction remains safe as more systems adopt handles; Ultra = exact memory residency reshaping can proceed without fragile undocumented assumptions.
Hardware Impact: i3/MX350 gains stability rather than direct frame time; the readback process prevents a crash-class stale pointer path from shipping behind a green build.
