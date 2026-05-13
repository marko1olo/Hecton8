# Rationale_VAULT_MEMORY_RELOCATOR

STATUS: VERIFIED METABOLIC COMPACTION (MEMORY ASSEMBLY); PROJECT BUILD BLOCKED BY EXTERNAL DEPENDENCIES

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
Problem: Full `dotnet build Hecton8.Core.csproj` fails on missing unrelated domain assemblies and interfaces before this memory work can be fully project-validated.
Solution: Ran a targeted Roslyn compile of `H8Memory.cs` and `GlobalDataVault.cs` against Unity 6000.4 netstandard, Unity.Burst, Unity.Collections, Unity.Mathematics, UnityEngine.CoreModule, and Hecton8.Core.Contracts; then recorded the full-project dependency wall separately.
Rejected Alternatives: Reporting full build success was rejected because the command fails. Reverting unrelated missing assemblies was rejected because they are outside this domain and likely owned by other agents.
Scalability potential: Low = memory assembly is syntax/type clean for Unity import; Middle = integrator fixes external asmdefs and project build resumes; High = relocation can then be profiled in scene; Ultra = runtime watchdog data drives platform-specific compaction cadence.
Hardware Impact: i3/MX350 runtime impact remains bounded by the 1 ms slice; exact saved microseconds are not measured until Unity runtime profiling is available.

## Decision 6: Legacy Raw View Risk
Problem: Existing systems still call `GetBuffer<T>()` and may cache `NativeArray` views that do not auto-refresh after relocation.
Solution: Kept legacy API for compatibility, added handle API and relocation signal path, and documented that consumers with cross-frame pointer caches must migrate to `VaultBufferHandle<T>` or subscribe to `MemoryAddressShiftSignal`.
Rejected Alternatives: Disabling movement for all external views was rejected because `GetBuffer` marks most buffers external and would reduce compaction back to telemetry-only. Breaking `GetBuffer` was rejected because it would damage unrelated systems outside this assignment.
Scalability potential: Low = conservative consumers use handles; Middle = signal subscribers invalidate local caches; High = hot systems lock during jobs and resolve handles per phase; Ultra = memory can be aggressively defragmented while high-end devices spend saved residency on visual overkill.
Hardware Impact: i3/MX350 gains only when consumers adopt handles/signals; current architecture enables the gain but does not automatically repair every legacy raw cache.
