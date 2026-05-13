# Rationale_MEMORY_DEFRAGMENTATION_OVERSEER

Status: PENDING VERIFICATION

## Decision 000 - Scope Boundary

Problem: The prompt requests native heap defragmentation, but Unity/C++ heap allocations made by independent `UnsafeUtility.Malloc` calls cannot be safely compacted from managed code by shifting arbitrary addresses. Blind pointer movement would invalidate cached `NativeArray` views and corrupt live jobs.

Solution: Implement compaction inside the owned `GlobalDataVault` arena, with `H8Memory` maintaining the block map. Only vault-owned blocks can move, and the vault pointer registry is updated immediately after `UnsafeUtility.MemMove`.

Rejected Alternatives: A process-wide Unity heap compactor was rejected because the engine does not expose relocation handles for arbitrary native allocations. A copy-to-new-malloc approach was rejected for the defrag path because it does not consolidate adjacent free space and recreates fragmentation.

Scalability potential: Low runs gap analysis at 1 s cadence to reduce OOM risk. Middle and High run at 5 s cadence. Ultra preserves saved memory headroom for richer HLOD/impostor residency and texture retention.

Hardware Impact: On i3/MX350, expected gain is lower OOM risk over long sessions and fewer emergency release spikes. CPU target remains under 1.0 ms per compaction slice; one move is capped at 5 MB.

## Decision 001 - Data Vault Arena Instead Of Raw Per-Buffer Malloc

Problem: Old `GlobalDataVault` allocated each buffer independently, so freeing/growing long-lived buffers could fragment unmanaged heap over a 10-hour session.

Solution: Allocate one 128 MB H8-tracked vault arena and suballocate buffer blocks from it. The block map tracks free/occupied slots by offset, not by arbitrary unmanaged heap address.

Rejected Alternatives: Continuing independent `H8Memory.ReallocateRaw` per buffer was rejected because it recreates heap fragmentation. A managed free list was rejected because the defrag tick must stay native and allocation-free.

Scalability potential: Low keeps the arena compact and predictable. Middle/High avoid allocator churn. Ultra can hold more high-fidelity data longer because fragmentation pressure is delayed.

Hardware Impact: i3/MX350 avoids repeated unmanaged allocator calls for vault buffers after boot. Estimated hot-path gain is mostly stability, not raw CPU; allocation cold path saves allocator overhead on buffer growth.

## Decision 002 - Pre-Simulation Cadence

Problem: Moving memory while jobs or gameplay systems hold old `NativeArray` views is fatal.

Solution: Dispatcher runs `GlobalDataVault.FrostTickDefrag()` only from `RunPreSimulationMemoryDefrag()` after pre-simulation signal flush and before simulation lanes. Existing post-frost vault defrag was removed.

Rejected Alternatives: Running inside `RunFrostTick()` was rejected because it executes after systems have already run their frost logic. Worker-job compaction was rejected because relocation must update main-thread pointer registries in a synchronized phase.

Scalability potential: Low cadence is 1 s; Middle/High/Ultra cadence is 5 s unless memory pressure forces an immediate pre-simulation pass.

Hardware Impact: i3/MX350 pays a tiny scan cost at a controlled phase. Top-tier machines keep the same deterministic phase boundary and can spend the saved stability budget on richer residency.

## Decision 003 - MemMove Only For Relocation

Problem: Adjacent free and occupied ranges can overlap when shifting an occupied block down into a preceding free gap. `MemCpy` is undefined for overlap.

Solution: All touched relocation/copy paths in `H8Memory` and `GlobalDataVault` use `UnsafeUtility.MemMove`. The compactor moves one block at a time and caps move size at 5 MB.

Rejected Alternatives: `UnsafeUtility.MemCpy` was rejected as unsafe for overlap. Managed copying was rejected for GC and Burst-hostile behavior.

Scalability potential: Low avoids massive stalls. Middle/High/Ultra retain deterministic compaction without changing behavior by tier.

Hardware Impact: MemMove may be marginally slower than MemCpy on non-overlap, but correctness is mandatory. The 5 MB cap keeps i3/MX350 worst-case bounded.

## Decision 004 - Massive Move Signal, Not Hard Freeze

Problem: A 50 MB+ move cannot be hidden inside a normal frame on target hardware.

Solution: The compactor refuses to move blocks above 5 MB during normal cadence. If a 50 MB+ occupied block blocks compaction, it emits `SignalBus<SystemPauseSignal>` and telemetry so UI/loading systems can mask a planned freeze.

Rejected Alternatives: Performing the 50 MB move immediately was rejected because it violates frame-time dictatorship. Direct UI calls were rejected because core memory must stay decoupled.

Scalability potential: Low sees a loading mask instead of a frame spike. Ultra can still use the same signal and spend more memory on visual overkill until a transition window.

Hardware Impact: Prevents multi-millisecond copy spikes on i3/MX350. Exact copy cost is not claimed without profiler evidence.

## Decision 005 - H8Memory Ownership For Arena Slab

Problem: `HectonArenaAllocator` owned a persistent unmanaged slab via direct `UnsafeUtility.Malloc`/`Free`, bypassing `H8Memory`.

Solution: Route the slab through `H8Memory.AllocateRaw`/`FreeRaw` with `SystemID.H8Memory`, keeping the existing `NativeMemorySentinel` registration for legacy diagnostics.

Rejected Alternatives: Deleting the sentinel path was rejected because other diagnostics depend on it. Leaving the raw malloc was rejected by task 4.

Scalability potential: Low gains better memory accounting for OOM prevention. High/Ultra gain clearer telemetry for choosing what visual residency to keep.

Hardware Impact: No hot-path cost. Cold allocation gains central ownership and leak tracking.

## Decision 006 - Black Box

Problem: The memory compactor is critical infrastructure; without a recent state buffer, a NaN/invalid state would be unverifiable after crash.

Solution: `GlobalDataVault` owns a fixed 300-entry `NativeArray<MemoryDefragTelemetryEntry>` circular buffer. Every defrag tick writes block count, free totals, largest free block, moved bytes, pending massive move bytes, ratio, and flags. Invalid telemetry dumps the buffer to `Docs/AgentLogs/Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin`.

Rejected Alternatives: Managed log strings and `List<T>` were rejected for GC. Per-frame disk logging was rejected for I/O cost.

Scalability potential: Low gets crash evidence without frame allocation. High/Ultra get the same deterministic trace with no visual cost.

Hardware Impact: Ring write is estimated under 1 us per eligible tick on i3/MX350. Dump path is fault-only.

## Decision 007 - Root Compile Wall

Problem: Full root compile fails before this slice can be proven green due unrelated missing namespaces/types and interface drift in other domains.

Solution: Validate owned files with Unity MCP where possible, refresh Unity, read Console, run `dotnet build` to capture the dependency wall, and mark task 19 as dependency-blocked for full compile.

Rejected Alternatives: Editing audio/world/save/physics domains from the memory agent was rejected as architectural sabotage during a parallel batch.

Scalability potential: No runtime impact. This protects integration hygiene.

Hardware Impact: No runtime impact.

## Decision 014 - Pin-Gated Relocation

Problem: Source readback proved several systems cache `NativeArray` views returned by `GlobalDataVault`. Moving those blocks blindly would update the vault registry while existing structs still point at the old address.

Solution: Mark every block exposed through `GetBuffer()`, `TryGetBuffer()`, or `CreateAlias()` with `BlockFlagExternalView`. The compactor contains the real `UnsafeUtility.MemMove` path, but it moves only unpinned vault blocks and marks pinned candidates in telemetry. H8 descriptors advertise `Relocatable` only for occupied blocks that have not been externally exposed.

Rejected Alternatives: Blind movement was rejected as a use-after-move crash vector. Telemetry-only defrag was rejected because it violates the native compaction task. A broad public API signature change was rejected during the batch because interface mutation would break parallel agents; this pass uses internal pinning instead.

Scalability potential: Low/Middle devices get safe compaction when internal relocatable vault blocks exist, while exposed gameplay buffers remain stable. High/Ultra keep the same safety rule and can use preserved memory headroom for richer residency instead of unsafe heap tricks.

Hardware Impact: i3/MX350 avoids stale-pointer crashes and disk dump spikes. Actual copy cost remains capped to one unpinned block and 5 MB per eligible tick; measured profiler proof is absent.

## Decision 015 - Concurrent Drift Compile Repair

Problem: A concurrent source drift added unused Burst/job scaffolding and the wrong dump owner path into `GlobalDataVault`, producing Unity Console errors for missing `Hecton8.Core.Signals`, `BurstCompile`, `FloatMode`, and `IJob` dependencies.

Solution: Removed the unreachable Burst job scaffolding from this asmdef, restored the indexed gap scan, kept the useful unaligned-block counter, restored `Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin`, and revalidated the owned memory scripts through Unity MCP.

Rejected Alternatives: Adding Burst/package references from this memory slice was rejected as asmdef creep. Ignoring the console errors was rejected because owned-file compile break is a hard failure.

Scalability potential: No behavior loss. Gap scan remains allocation-free and bounded by vault block count. Future Burst migration belongs in an asmdef that already references Burst.

Hardware Impact: Removes compile break and avoids any job scheduling/Complete overhead inside the defrag slice. Runtime proof remains pending behind unrelated `HectonUnderwaterVisuals.cs` syntax errors.

## Decision 008 - Restore Actual Movement

Problem: Follow-up readback found `GlobalDataVault.FrostTickDefrag()` no longer called `TryMoveOneBlock()`. That reduced the system to telemetry-only behavior and violated the compaction objective.

Solution: Restore the move attempt inside the fragmented/unlocked branch, measure the slice with the 1 ms watchdog, and record the black box after the move attempt. Massive move telemetry remains a refusal path for blocks above 5 MB.

Rejected Alternatives: Keeping telemetry-only defrag was rejected because it does not compact memory. Moving multiple blocks per tick was rejected because it violates the time-slicing requirement.

Scalability potential: Low regains actual incremental compaction at 1 s cadence. Middle/High/Ultra retain 5 s cadence and deterministic pre-simulation movement.

Hardware Impact: i3/MX350 cost is bounded to one moved block and max 5 MB per eligible tick. No measured microseconds; profiler proof absent.

## Decision 009 - Reallocation Index Recovery

Problem: `TryReallocateBlock()` allocated a new block before freeing the old one. If the new free block was inserted before the old block, `existingMeta.BlockIndex` could point at the wrong block after the insert.

Solution: Locate the old and new occupied blocks by `BufferKey` and `OffsetBytes` after list mutation. Roll back the new allocation if the old block cannot be found.

Rejected Alternatives: Trusting stored indices was rejected because `NativeList` insertion/removal shifts block indices. Managed handle objects were rejected for GC and ownership complexity.

Scalability potential: Same behavior by tier; correctness fix prevents rare arena corruption that would be worse on long low-memory sessions.

Hardware Impact: Reallocation is cold. Added indexed search is bounded by vault block capacity and does not affect per-frame movement.

## Decision 010 - Suballocator Root Flag

Problem: The H8Memory allocation record for the vault arena root and the vault sub-block descriptor at offset zero could share the same base pointer and offset. That made descriptor state ambiguous during root free and sub-block updates.

Solution: Add `H8AllocationFlags.SubAllocatorRoot`. `H8Memory` still records the allocation owner, but skips automatic block descriptor registration for roots whose subregions are registered separately.

Rejected Alternatives: Leaving duplicate descriptors was rejected because free-state updates could hit the wrong descriptor. Removing H8 ownership of the root was rejected because task 4 requires H8-tracked native memory.

Scalability potential: Cleaner telemetry on all tiers; High/Ultra can rely on block map data without root/sub-block noise.

Hardware Impact: No hot-path cost. Cold registration skips one descriptor write.

## Decision 011 - Phi-VOD Dump Discipline

Problem: `TryGetBuffer()` was writing the Phi-VOD dump on ordinary missing-buffer lookups. Optional query misses are not memory corruption and disk I/O there is hostile to frame pacing.

Solution: Keep Phi-VOD dumping for arena/view fault paths, but do not dump on normal absent metadata.

Rejected Alternatives: Removing Phi-VOD entirely was rejected because it may belong to another active agent. Keeping dump-on-miss was rejected as unnecessary runtime I/O.

Scalability potential: Low tier avoids surprise disk writes from optional queries. Other tiers keep fault evidence without normal-path overhead.

Hardware Impact: Removes a potential one-time disk spike from normal missing-buffer checks.

## Decision 012 - Source Truth Over Stale Status

Problem: Disk re-read showed the status/rationale claimed movement had been restored, but the current `GlobalDataVault.cs` still had telemetry-only `FrostTickDefrag()` and ordinary missing-buffer queries could still reach Phi-VOD dump I/O. The validator also misread repeated zero-argument helper calls as duplicate method definitions.

Solution: Treat source as authoritative, restore real one-block compaction through `TryMoveOneBlock()` and `MoveOccupiedBlockIntoFreeGap()`, validate the arena block map before and after movement, keep Phi-VOD dumps for fault paths only, inline H8 initialization checks, and replace the late-frame helper method with a property to avoid the MCP parser false-positive pattern.

Rejected Alternatives: Trusting stale status text was rejected because it would ship a non-moving defragger. Leaving the zero-argument helper calls was rejected because it kept blocking owned-file validation signal. Moving several blocks per tick was rejected because it violates the 5 MB/one-block time-slicing requirement.

Scalability potential: Low tier receives actual incremental compaction again at the aggressive cadence. Middle/High/Ultra keep deterministic pre-simulation relocation and better memory-residency headroom for visual overkill.

Hardware Impact: On i3/MX350, the normal-path cost remains an indexed scan plus at most one 5 MB `MemMove`; full block-map validation is O(block count) and allocation-free. Removing normal-miss dump I/O avoids disk spikes on optional lookup paths.

## Decision 013 - Verification Wall Handling

Problem: Unity MCP became unavailable after script refresh, returning `no_unity_session`, and editor readiness timed out after 60 seconds. Full `dotnet build` still fails on unrelated domains before root proof can be obtained.

Solution: Run local static checks, `git diff --check`, grep for banned/correct patterns, and filter `dotnet build` output for owned memory/dispatcher/arena files. Record editor validation as pending instead of pretending the latest pass compiled in Unity.

Rejected Alternatives: Editing unrelated gameplay/world/audio/mining files to make the memory report green was rejected as cross-domain interference. Reporting older MCP validation as current proof was rejected because source changed after that validation.

Scalability potential: No runtime change. This keeps integration state factual for the next agent or CTO review.

Hardware Impact: No runtime impact.

## Decision 016 - H8Memory Descriptor Owner Repair

Problem: `H8Memory.RemoveRecordAt()` removes an allocation record by swapping the last record into the freed slot. The moved record's `AllocationIndex` was updated, but its `BlockDescriptor.OwnerKey` still pointed at the old record index, corrupting the native memory map used by compaction telemetry.

Solution: Added `UpdateBlockDescriptorOwnerKey()` and call it immediately after record-swap compaction. This keeps the allocation record array and the native block descriptor S.O.A. synchronized without allocating or scanning managed collections.

Rejected Alternatives: Rebuilding the whole descriptor list on every free was rejected because it turns a cold O(1) record swap into unnecessary O(n) churn. Leaving stale owner keys was rejected because postmortem dumps would lie about block ownership.

Scalability potential: Low tier gets more trustworthy OOM diagnostics during long sessions. Middle/High/Ultra keep the same zero-GC path while retaining accurate ownership for future visual-residency decisions.

Hardware Impact: On i3/MX350 the added reverse descriptor scan runs only on free/unregister and exits on first matching base pointer. No frame-path cost is added to defrag scanning.

## Decision 017 - Source Drift Re-Reconciliation

Problem: A later source read showed `GlobalDataVault` had drifted back toward a synchronous `IJob` gap wrapper, a high-tier defrag bypass, and the wrong `Dump_AGENT_HOMEOSTASIS_METABOLISM.bin` crash path. That created compile risk, skipped compaction on expensive machines, and wrote black-box evidence under another agent's name.

Solution: Removed the gap-job wrapper and retained the indexed native scan, removed the high-tier bypass so all tiers preserve memory health, restored `Dump_MEMORY_DEFRAGMENTATION_OVERSEER.bin`, and kept the actual `TryMoveOneBlock()` -> `MoveOccupiedBlockIntoFreeGap()` -> `UnsafeUtility.MemMove` path.

Rejected Alternatives: Keeping a `Run()` job wrapper was rejected because it adds dependency surface without parallelism. Skipping defrag on high-end hardware was rejected because Ultra should spend saved stability on visual overkill, not accept silent fragmentation. Mislabeling dump files was rejected because black-box evidence must identify the owning system.

Scalability potential: Low uses the same cheap indexed scan and one-block cap. Middle/High/Ultra keep compaction active and can retain larger visual caches without letting the vault degrade.

Hardware Impact: i3/MX350 remains bounded to an indexed scan plus at most one 5 MB `MemMove` per eligible tick. Top-tier devices no longer bypass memory health, preserving longer-session stability for visual residency.

## OMEGA POLISH CHANGES

Problem: Final polish required proving the owned implementation did not smuggle in honest but expensive math, string churn, managed iteration, or cross-domain coupling after all 19 tasks were checked/blocked.

Solution: Re-read the prompt and polish mandate by CLI, removed the high-tier bypass, removed the synchronous job wrapper, kept bitmask flags for defrag telemetry, kept `MemMove` as the only relocation/copy primitive in owned memory files, and ran a targeted `Select-String` anti-bloat scan against owned files.

Rejected Alternatives: A root-wide cleanup was rejected as cross-domain interference during parallel agent work. Adding Burst references to justify the old job wrapper was rejected as asmdef creep.

Scalability potential: Low remains toaster-safe through 5 MB slices and 1 second pressure cadence. Middle/High/Ultra retain compaction and can spend memory headroom on richer residency.

Hardware Impact: Exact microseconds remain unmeasured because Unity editor validation is blocked by session instability and global compile walls. Estimated runtime effect is no added steady-frame allocation, no managed collection scan, and no high-tier fragmentation bypass.

## Decision 018 - Concurrent Source Drift Blocker

Problem: `GlobalDataVault.cs` is being edited by at least one parallel workstream. Multiple repairs restored the `TryMoveOneBlock()` / `MoveOccupiedBlockIntoFreeGap()` `MemMove` path, removed high-tier bypass logic, and corrected the dump owner path, but subsequent disk readbacks showed the file rewritten back to a telemetry-only defrag path and another agent's dump owner constant.

Solution: Stop claiming source-level completion from stale logs. Record the conflict as `[BLOCKED BY CONCURRENT SOURCE DRIFT]`, keep `H8Memory` descriptor-owner repair, and leave integration truth to the latest disk readback until a single owner can merge the competing `GlobalDataVault` variants.

Rejected Alternatives: Repeatedly overwriting another active agent's edits was rejected because it creates an edit war in a shared core file. Reporting the repaired-but-overwritten variant as current was rejected as a fake report.

Scalability potential: None until the conflict is merged. The desired final state remains one safe moving compactor with either an accepted Burst/indexed gap scan, pin-gated relocation, 5 MB move cap, and the memory-overseer dump path.

Hardware Impact: Current live source does not provide the intended OOM-risk reduction because the move path is absent in the latest readback. Build filtering reports no owned compile lines, but behavioral proof is blocked.
