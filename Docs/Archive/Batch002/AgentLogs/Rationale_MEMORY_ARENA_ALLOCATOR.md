# Rationale_MEMORY_ARENA_ALLOCATOR

Status: PENDING VERIFICATION

## Decision 0 - Scope Lock
Problem: Prompt demands Arena 2.0, but registry mandate states arena files already exist; blind replacement would create a second allocator and violate ownership.
Solution: Upgrade the existing Core arena path in-place, preserving dispatcher and compatibility contracts.
Rejected Alternatives: New standalone allocator singleton rejected because mandates forbid duplicate transient allocator systems.
Scalability potential: Low = same 100MB reserve but optional systems drop on OOM. Middle = stable frame scratch. High = more visual scratch users. Ultra = visual overkill scratch after profiler proof.
Hardware Impact: i3/MX350 expected gain is lower native heap churn and fewer page faults; exact microseconds remain pending profiler capture.

## Decision 1 - OOM Contract
Problem: Batch prompt asks for `Allocator.Temp` fallback on OOM, while Arena 2.0 mandate requires no OS fallback allocation.
Solution: Implement Try APIs that return false, increment `_oomCount`, and publish `ARENA_OOM_HASH`.
Rejected Alternatives: Temp fallback rejected because it hides capacity bugs and can create frame spikes exactly when memory pressure is already high.
Scalability potential: Low/Middle skip optional scratch work; High/Ultra increase arena budget only with evidence.
Hardware Impact: Avoids low-end native allocator stalls; worst-case avoided spike can exceed 100 us under allocation pressure.

## Decision 2 - Slabbed Double Buffer
Problem: One global bump cursor serializes worker allocation and single-buffer reset can invalidate late readers.
Solution: Split the 100MB reserve into two logical arenas, then split each arena into processor-count slabs with thread-static slab selection and CAS bump cursors.
Rejected Alternatives: Per-system arenas rejected as fragmentation by ownership; global lock rejected as worker contention.
Scalability potential: Low = few threads still use one or two slabs. Middle = processor-count slabs. High = broad worker scratch. Ultra = GPU/visual staging candidates after contract proof.
Hardware Impact: i3/MX350 benefit is fewer cross-thread cache-line fights; estimated 2-18 us saved in worker-heavy frames.

## Decision 3 - Reset Authority
Problem: `GameTickManager.Tick` reset happened too early relative to jobs that may still be owned by dispatcher/system late phases.
Solution: Remove `HectonArenaAllocator.Reset()` from game tick and route legacy reset through `SystemDispatcher.LateUpdate` via `NativeArenaAllocator.Reset() -> HectonArenaAllocator.EndFrameSwap()`.
Rejected Alternatives: Reset on every game tick rejected because multiple tick phases can destroy scratch before consumers finish.
Scalability potential: Low/Middle deterministic frame-lifetime scratch. High/Ultra safe previous-frame read lane for visual overkill jobs.
Hardware Impact: Prevents use-after-reset; microsecond gain is small, but crash prevention is primary.

## Decision 4 - NativeArenaArray Safety
Problem: Burst jobs need NativeContainer metadata and safety integration, not loose pointers.
Solution: Add `NativeArenaArray<T>` with `[NativeContainer]`, min/max write restriction fields, safety handle assignment, pointer accessors, and `AsNativeArray()` bridge.
Rejected Alternatives: Raw pointers rejected because they bypass safety and require every caller to duplicate bounds discipline.
Scalability potential: Low = direct integer/byte scratch. Middle = job-visible SoA scratch. High = culling/visibility lanes. Ultra = visual overkill staging after lifetime proof.
Hardware Impact: Saves metadata/dispose churn when replacing TempJob arrays; estimated 3-20 us in high-allocation frames.

## Decision 5 - Cross-Domain Integrations
Problem: Tasks 8 and 9 target Gameplay/Graphics memory that is not proven frame-local arena scratch.
Solution: Block forced migration and document exact candidate files/lines. KCC buffers are persistent command/result caches. BRG output pointers may be owned/freed by Unity Graphics.
Rejected Alternatives: Replacing persistent physics buffers and BRG output pointers without owner proof rejected as architectural sabotage risk.
Scalability potential: Low = keep persistent safe buffers. Middle = migrate only per-frame visibility masks. High = staged BRG candidates with explicit Graphics contract. Ultra = arena-backed visual overkill only if Unity does not free the memory.
Hardware Impact: Avoids correctness regressions; potential 5-25 us savings remain unclaimed until owner proof exists.

## Decision 6 - Audio DSP Audit
Problem: Prompt asks for convolution FFT/delay temp buffers, but audio code uses persistent/audio-kernel delay and impulse buffers.
Solution: Leave `_caveConvolutionImpulse`, `_caveConvolutionDelay`, `_sabineReverbDelay`, and related DSP rings on `Allocator.AudioKernel`/`Allocator.Persistent`.
Rejected Alternatives: Moving delay-line state to frame arena rejected because frame reset would erase reverb history and corrupt DSP output.
Scalability potential: Low = stable cheap fake convolution. Middle = AudioKernel rings. High/Ultra = richer convolution state on audio-owned persistent buffers, not frame arena.
Hardware Impact: No direct memory-allocation savings; prevents repeated cold buffer rebuild and audio artifacts on low-end silicon.

## Decision 7 - Compile Wall
Problem: First compile exposed one allocator pointer cast error plus unrelated Audio/Boid missing-symbol errors. Polish dotnet build also exposed that generated `Hecton8.Core.csproj` had not included the new `NativeArenaArray.cs`.
Solution: Fixed allocator `void*` to `byte*` cast and added `Assets\_Project\Scripts\Core\NativeArenaArray.cs` to `Hecton8.Core.csproj`. Stopped at remaining out-of-domain dependency wall and logged blockers.
Rejected Alternatives: Editing unrelated Audio/Boid/Survival/Tether/GlobalSignals/AbyssalThermal systems rejected because assignment domain is Core/Memory and those errors are not allocator logic defects.
Scalability potential: Low/Middle/High/Ultra unchanged; blocked compile prevents final runtime proof.
Hardware Impact: Allocator scripts validate cleanly, but full hardware impact cannot be measured until project compile blockers are cleared.

## OMEGA POLISH CHANGES
Problem: Polish scan found `NativeArenaArray.ThrowIndexOutOfRange` used `index.ToString()` in allocator-owned code and dotnet build did not include the new arena container file.
Solution: Replaced the exception detail with a constant string and patched `Hecton8.Core.csproj` with the `NativeArenaArray.cs` source include. Re-ran `dotnet build`.
Rejected Alternatives: Leaving Unity validation as sole evidence rejected because Polish explicitly required dotnet build. Formatting the index through `FixedString` rejected because this is an exception-only safety path and not worth new code.
Scalability potential: Low = no managed exception-string conversion in safety code. Middle = csproj includes arena type for IDE/dotnet builds. High = no new bloat. Ultra = no change; allocator remains bitmask/CAS based.
Hardware Impact: Hot-path impact is 0 us because exception path is not hot; build determinism improved by removing the missing source include.
Cinematic Cheats Used: Bitmask alignment `(offset + 15) & ~15`, cache-line minimum alignment, frame-reset bump arena instead of honest free/defrag, double-buffer pointer swap instead of per-allocation lifetime tracking.
Final Git Diff Evidence: Core allocator files, `NativeArenaArray.cs`, dispatcher tick reset removal, `Hecton8.Core.csproj` source include, EditMode proof, recon/status/rationale/log docs. Full repository diff is dirty with unrelated agents, so only listed paths are owned by MEMORY_ARENA_ALLOCATOR.

## Decision 8 - Typed Wrapper Byte Count
Problem: A post-polish code audit found `TryAllocateNativeArray<T>` and `TryAllocateNativeArenaArray<T>` calling the byte overload with `count`, which allocated element count as bytes. `NativeArenaArray<int>(64)` therefore reserved 64 bytes instead of 256 bytes, creating a real out-of-bounds write risk.
Solution: Route both typed wrappers through `TryAllocateBlock<T>` so byte count is computed as `UnsafeUtility.SizeOf<T>() * count`. Added `NativeArrayOptions` overloads and a regression assertion that `ByteCount == Count * UnsafeUtility.SizeOf<int>()`.
Rejected Alternatives: Patching only current tests rejected because the bug lived in the allocator API. Migrating scatter/BRG arrays during this discovery rejected because a corrupt allocator wrapper invalidates downstream migration evidence.
Scalability potential: Low = typed scratch no longer corrupts cheap devices under int/float workloads. Middle = direct `NativeArrayOptions.ClearMemory` migration path. High = safer visibility/culling staging migrations after lifetime proof. Ultra = broad visual-overkill scratch can use typed arena wrappers without hidden byte under-allocation.
Hardware Impact: i3/MX350 gain is primarily crash/stall avoidance, not a claimed frame-time win. Wrapper cost remains inlined and effectively 0 us; avoided memory corruption can prevent unbounded frame spikes or process termination.
Cinematic Cheats Used: Typed byte math via one generic block allocator instead of duplicated wrapper arithmetic; direct `NativeArrayOptions` bridge instead of per-call conversion glue.
Verification Impact: Unity MCP validation could not complete because the plugin session was not ready/disconnected. `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` after the patch reports no allocator errors and stops at unrelated `HectonSurvivalSystem.cs` missing `SurvivalPhysiologyScalarResult`.

## Decision 9 - Spare Slab Fallback
Problem: TLS slab assignment prevented lock contention, but a single managed thread could fill its assigned slab and trigger OOM while other slabs in the same active arena were empty. That made the 100MB arena behave like a per-thread slice under skewed workloads.
Solution: Keep the fast path unchanged by trying the assigned TLS slab first. On slab pressure only, probe the remaining slabs with the same CAS bump allocator before publishing `ARENA_OOM_HASH`. Added an editor regression test that fills one slab edge and verifies the next allocation spills to a free slab without incrementing `OomCount`.
Rejected Alternatives: A global cursor was rejected because it restores worker contention. Per-system arenas were rejected as fragmentation. Immediate OOM on local slab full was rejected because it underuses already-reserved arena memory.
Scalability potential: Low = fewer cosmetic drops on 2-4 core CPUs when one owner allocates a large scratch burst. Middle = fuller use of the reserved arena without extra allocations. High = culling/scatter staging can tolerate skewed owner pressure after lifetime proof. Ultra = broad visual scratch bursts can consume idle slabs before shedding.
Hardware Impact: i3/MX350 gains are from avoided false OOM and avoided optional-work disable; hot path cost remains one slab CAS. The fallback path costs up to `SlabCount - 1` bounded probes only when the preferred slab cannot fit the request.
Cinematic Cheats Used: Preferred TLS slab for normal frames, bounded ring probe only on pressure, no free list and no OS fallback.
Verification Impact: `validate_script` passed with zero diagnostics for updated `HectonArenaAllocator.cs` and `NativeArenaArrayEditTests.cs`. `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` reports no allocator errors and stops on unrelated `HectonBoidController`/`VoxelDeltaProcessor` compile blockers.

## Decision 10 - Owner Telemetry Semantics
Problem: Owner high-water telemetry used the slab cursor end offset as the owner value. A small owner allocation near the end of a slab could therefore be reported as a large owner budget consumer, and spare-slab fallback made the error more visible.
Solution: Track owner telemetry as real allocated bytes. Added fixed cold arrays for current-frame owner totals and last-frame owner totals. `UpdateOwnerHighWater` now adds `byteCount` to the owner frame counter and updates lifetime high-water from that per-frame total. `EndFrameSwap()` snapshots current-frame owner bytes into last-frame bytes and clears only current-frame counters.
Rejected Alternatives: Keeping slab cursor telemetry rejected because it answers the wrong question. Adding managed dictionaries rejected because owner readback must stay allocation-free. Per-allocation owner records rejected because arena allocations are too hot for variable telemetry storage.
Scalability potential: Low = accurate watchdog data for optional scratch shedding. Middle = owner budget tuning without false blame. High = visibility/culling staging owners can be measured after lifetime proof. Ultra = visual-overkill scratch owners can consume saved cycles with auditable per-frame byte totals.
Hardware Impact: i3/MX350 runtime cost is one `Interlocked.Add` on owner telemetry per successful allocation, already present as owner high-water work before this patch. The gain is diagnostic correctness: no false megabyte reports from 64B late-slab allocations.
Cinematic Cheats Used: Fixed-slot owner table, per-frame byte counters, last-frame snapshot on arena swap; no managed map, no per-allocation log.
Verification Impact: `validate_script` passed with zero diagnostics for updated `HectonArenaAllocator.cs` and `NativeArenaArrayEditTests.cs`. `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` reports no allocator errors and stops on unrelated generated-project dependencies including path policy, platform clock, Steam Deck PAL, thread policy, hardware tier, and native bridge symbols.

## Decision 11 - Telemetry Registration and Shutdown Hygiene
Problem: Owner telemetry slot registration used a single empty-slot CAS. Under concurrent first-use owner hashes, a failed CAS could drop that successful allocation from owner accounting. `Shutdown()` also freed arena memory while leaving scalar diagnostics such as last-frame high-water, OOM count, and thread-slab cursor state stale.
Solution: Retry the fixed owner telemetry table after CAS contention, then update the claimed slot with `Interlocked.Add`. Added `ResetScalarState()` and call it on both shutdown paths, including the already-null base pointer path. Added `Shutdown_ClearsArenaTelemetryState` regression coverage.
Rejected Alternatives: Managed `ConcurrentDictionary` rejected because owner telemetry must remain allocation-free. Unbounded retry rejected because a full table must degrade predictably. Leaving shutdown counters stale rejected because post-domain-reload diagnostics would report freed arena state as live memory pressure.
Scalability potential: Low = stale diagnostics do not disable optional scratch after shutdown/reload. Middle = owner budget readback stays accurate under moderate subsystem churn. High = culling/scatter staging owners can register concurrently without first-frame telemetry loss after lifetime proof. Ultra = visual-overkill scratch owners can compete for telemetry slots without managed bookkeeping.
Hardware Impact: i3/MX350 hot path for existing owners is unchanged. New-owner contention pays a bounded table retry only during cold registration; the practical gain is avoiding false load-shed or missed OOM diagnosis on weak CPUs.
Cinematic Cheats Used: Fixed-slot owner table, bounded CAS retry, scalar reset instead of rebuilding telemetry objects, no managed owner map.
Verification Impact: `validate_script` passed with zero diagnostics for updated `NativeArenaArrayEditTests.cs`. `validate_script` for `HectonArenaAllocator.cs` was attempted twice, but the Unity MCP plugin disconnected both times. `dotnet build .\Hecton8.Core.csproj --no-restore --no-dependencies /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false /m:1 -v:q` reports no allocator errors and stops on unrelated path policy, platform clock, thread policy, Steam Deck PAL, haptics, hardware tier, scatter telemetry, and native bridge symbols.
