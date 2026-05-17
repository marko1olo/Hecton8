# MEMORY_DEFRAGMENTATION_OVERSEER Rationale

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no XML prompt for `MEMORY_DEFRAGMENTATION_OVERSEER`, but the user directly assigned `Core/Memory` live unmanaged heap compaction.
Solution: Treat the direct override as one task and log the missing tag. Limit edits to `Assets/_Project/Scripts/Core/Memory/`.
Rejected Alternatives: Borrowing adjacent `VAULT_SOVEREIGNTY_ENFORCER` would contaminate this assignment with another agent's task list. Ignoring the direct task would leave no implementation.
Scalability potential: Low = bounded telemetry and compaction slices. Middle = compact small internal vault gaps. High = more free contiguous room for large visual buffers. Ultra = larger arena limit already supports overkill caches.
Hardware Impact: Missing prompt handling saves no runtime time; it prevents wrong-domain edits.

Problem: Live compaction can turn cached direct `NativeArray` views into stale raw pointers.
Solution: Implement live movement only for vault blocks that are not locked and have never been externalized through a direct view. Use generation updates and relocation records for moved internal blocks.
Rejected Alternatives: Blind `UnsafeUtility.MemMove` of every occupied block was rejected because direct `NativeArray` views cannot be repointed. Full stop-the-world compaction was rejected because this project has no global reader fence for all vault consumers.
Scalability potential: Low = no alias corruption on MX350. Middle = small safe gaps compacted during FrostTick. High = handle-owned future systems can benefit more as direct views are retired. Ultra = saved contiguous memory can feed high-end VFX/cache buffers.
Hardware Impact: Bounded to a small slice; expected MX350 cost is sub-0.1 ms for moved blocks under the byte cap, 0 us when all blocks are externally viewed and skipped.

Problem: Unbounded compaction can become a 1 ms+ frame spike on low-end silicon.
Solution: Cap live movement at 5 MB per FrostTick slice and compact only adjacent free->occupied gaps. Set `DefragFlagMassiveMovePending` when the next candidate cannot fit the slice.
Rejected Alternatives: Compacting the entire arena at once was rejected as a frame-time violation. Scheduling a Burst memmove job was rejected because active raw views still require a main-thread metadata fence.
Scalability potential: Low = predictable slice/no move under stress. Middle = gradual gap cleanup. High = more contiguous cache space. Ultra = larger arena has bounded maintenance instead of a monolithic stall.
Hardware Impact: 5 MB memmove is the hard cap; expected low-tier FrostTick cost stays bounded and is skipped completely when `SystemStress01 > 0.9`.

Problem: Core compile verification is currently stopped by a non-memory dependency.
Solution: Classify `TetherManager.cs(264,58)` / missing `TetherSignals.TetherFireRequest` as a dependency wall and continue static validation of the touched Core/Memory path.
Rejected Alternatives: Fixing TetherManager was rejected because the assigned domain is Core/Memory and the worktree has concurrent physics-agent edits. Reporting build green was rejected because the compile returned exit code 1.
Scalability potential: N/A for runtime; this prevents wrong-domain churn under parallel agents.
Hardware Impact: N/A. Compile blocker is source dependency, not runtime memory cost.

Problem: Final polish XML tags requested by protocol are absent from the current batch file.
Solution: Re-read `CURRENT_BATCH.md`, record missing `MEMORY_DEFRAGMENTATION_OVERSEER` and missing `<POLISH_MANDATE>`, then run a local anti-bloat scan against the touched file.
Rejected Alternatives: Reading another agent's polish section was rejected as prompt contamination. Claiming polish mandate execution from a missing tag was rejected as false evidence.
Scalability potential: N/A for runtime; keeps the source change narrow and auditable.
Hardware Impact: No runtime impact. Static scan prevents accidental debug or blocking-code drift.

Problem: Second-pass audit found a one-element `_gapAuditResult` `NativeArray` and synchronous `VaultGapAuditJob.Run()` inside `GlobalDataVault.AnalyzeGaps()`.
Solution: Remove `VaultGapAuditResult`, `VaultGapAuditJob`, `_gapAuditResult`, its H8 allocation/release, and its ABI check. Compute total free bytes, largest free block, and unaligned occupied count inline during the FrostTick maintenance scan.
Rejected Alternatives: Scheduling the job was rejected because a single-result scratch array still violates the data-sovereignty pass and adds scheduling/fence surface. Keeping `.Run()` was rejected because it is a sync-job API with no benefit for this O(n) memory-map scan.
Scalability potential: Low = one less tracked native allocation and no job invocation in the maintenance pass. Middle = simpler deterministic gap audit. High = freed maintenance budget can support larger contiguous visual/cache buffers. Ultra = high-tier arenas still get bounded compaction without scratch native state.
Hardware Impact: Removes 32 bytes of audit payload plus H8 tracking overhead; expected i3/MX350 FrostTick audit savings are 1-3 us, unprofiled, from removing job `.Run()` and NativeArray result traffic.

Problem: Multiplatform check required ARM64/Quest ABI safety, Metal/Mac shader neutrality, Steam Deck I/O restraint, and PC high-tier headroom.
Solution: Verify all Core/Memory binary structs use `StructLayout(Pack = 1)` with explicit size checks; confirm the memory domain has no shaders; keep black-box dumps fault-only instead of per-frame I/O; preserve high-tier arena limit and bounded compaction for large visual/cache allocations.
Rejected Alternatives: Adding visual features from a memory-domain task was rejected as domain breach. Per-frame text or binary dumping was rejected as Steam Deck MicroSD stutter risk.
Scalability potential: Low = 64-byte aligned vault blocks, stress-gated defrag, no per-frame disk writes. Middle = gradual compaction. High = 4 GB arena ceiling supports larger cache pools. Ultra = contiguous memory headroom for overkill VFX systems owned by their domains.
Hardware Impact: Runtime cost unchanged by audit; risk reduction comes from ABI checks and zero hot I/O.

Problem: The earlier compile blocker became stale after concurrent dependency work.
Solution: Re-run `dotnet build` for `Hecton8.Core.csproj`; current result is 0 warnings and 0 errors.
Rejected Alternatives: Leaving status as blocked was rejected because the current evidence is a clean compile. Editing Tether remained rejected because it is outside Core/Memory and no longer necessary.
Scalability potential: N/A.
Hardware Impact: N/A. Verification only; runtime impact is 0 us.

Problem: Restored XML requires compaction only in `PRE_SIMULATION`, with no movement during `VISUAL_SYNC` or while Burst jobs hold vault-backed buffers.
Solution: Add a `MemoryDefragPhase` gate to `IDataVault.FrostTickDefrag`, call it from `SystemDispatcher.RunPreSimulationMemoryDefrag()` with `MemoryDefragPhase.PreSimulation`, and abort vault compaction for any non-pre-sim phase before gap analysis can reach `UnsafeUtility.MemMove`.
Rejected Alternatives: Relying on the dispatcher method name was rejected because future callers could invoke `FrostTickDefrag` from visual sync or late-frame code. Moving memory after jobs are scheduled was rejected because raw Burst aliases cannot be repointed safely.
Scalability potential: Low = MX350 keeps defrag in a low-activity scheduling slot. Middle = deterministic compaction cadence remains intact. High = larger arenas still compact, but only before job admission. Ultra = visual systems can consume contiguous memory without risking STP/visual-sync pointer churn.
Hardware Impact: One enum compare per FrostTick call; estimated <1 us. The avoided failure mode is undefined pointer reuse, not a measurable ALU save.

Problem: The existing buffer lock was per-block metadata only; the dispatcher could not expose a global "any Burst lock active" veto to the defragmenter.
Solution: Add `_activeLocks` as a CAS-managed bitmask, owner-aware `TryLockBuffer`/`TryUnlockBuffer` overloads, and dispatcher raycast lock calls tagged with `SystemID.SystemDispatcher`. The vault now aborts compaction if either its local mask or caller-provided mask is nonzero.
Rejected Alternatives: Scanning every job handle was rejected because that invents scheduler ownership outside Core/Memory. A managed delegate/event callback was rejected because it violates the zero-GC/signal sovereignty constraints.
Scalability potential: Low = lock mask makes compaction skip instead of stalling jobs. Middle = more systems can opt into owner-tagged vault locks. High = many scheduled readers remain safe because the global mask is cheap to test. Ultra = memory visualization can read the mask from telemetry without adding new allocations.
Hardware Impact: Lock/unlock adds a short `Interlocked.CompareExchange` loop only on job scheduling boundaries. FrostTick abort path avoids up to 5 MB memmove when locks are live; expected MX350 saving is the whole slice budget, up to the existing sub-0.1 ms cap.

Problem: Pointer relocation must not publish partially fenced state, and moved blocks must retain the vault's 64-byte alignment contract while avoiding offset overflow.
Solution: Acquire/release `_compactionFence` with `Interlocked.Exchange`, publish moved pointer bits through `Interlocked.Exchange`, update the unsafe map only while the fence is held, reject any move with unaligned offsets/length, and reject offsets or move lengths above `uint.MaxValue`.
Rejected Alternatives: Writing `_compactionFence = 1/0` directly was rejected because the restored prompt explicitly required atomic publication. Allowing odd-sized moved blocks was rejected because the free-tail offset would break cache-line alignment for the next block.
Scalability potential: Low = Quest/ARM64 alignment remains deterministic. Middle = stale-handle generation checks still catch moved buffers. High = larger PC arena relocation remains bounded by uint offset guards. Ultra = contiguous space supports overkill visual caches without weakening ABI safety.
Hardware Impact: Two interlocked exchanges per actual compaction slice plus one pointer publish exchange per moved block. No cost when no relocation occurs.

Problem: The restored XML demands low-tier compaction aggression at 15% fragmentation and high-tier restraint at 30%.
Solution: Resolve fragmentation threshold from vault arena capacity: low/middle arenas use 0.15, high-tier 4 GB arenas use 0.30. This keeps low-memory hardware proactive and avoids unnecessary high-end maintenance churn.
Rejected Alternatives: A single fixed threshold was rejected because it treats MX350 and high-end PC memory pressure as equivalent. Querying graphics-tier services inside the vault was rejected because Core/Memory should not depend on rendering systems.
Scalability potential: Low = earlier gap cleanup on constrained RAM. Middle = same bounded behavior. High = fewer unnecessary memmoves until fragmentation is materially worse. Ultra = saved maintenance cycles remain available for visual/cache systems.
Hardware Impact: One branch per gap analysis; estimated <1 us. Low-tier benefit is reduced OOM risk; high-tier benefit is fewer 5 MB slices under mild fragmentation.

Problem: Build validation initially failed on the new Defrag namespace edge, then a stale external UI compile error appeared once memory code passed.
Solution: Keep `MemoryDefragPhase` in the `Hecton8.Core.Memory` authority namespace to avoid a brittle nested assembly dependency, rerun build, and confirm the later UI `NativeSlice.IsCreated` error cleared without changing UI code.
Rejected Alternatives: Editing generated project references was rejected because Unity regenerates them. Changing UI code during a Core/Memory task was rejected after the source no longer contained the failing line.
Scalability potential: N/A for runtime; this reduces assembly coupling around the defrag hot contract.
Hardware Impact: Runtime impact is 0 us. Verification result is `dotnet build` success with 0 warnings and 0 errors.

Problem: The dormant `Core/Memory/Defrag` assembly still declared its own `MemoryDefragPhase`, creating two phase authorities after the dispatcher stitch.
Solution: Remove the duplicate enum from `MemoryDefragContracts.cs`; keep the phase enum only in the `IDataVault`/`GlobalDataVault` authority namespace where the dispatcher call compiles without an extra assembly edge.
Rejected Alternatives: Re-introducing a `Hecton8.Core.Memory.Defrag` dependency was rejected because it already produced a compile wall in the generated project. Leaving duplicate phase enums was rejected because it invites the wrong enum at future call sites.
Scalability potential: Low/Middle/High/Ultra all benefit from one phase contract; no runtime branch or memory cost changes.
Hardware Impact: 0 us runtime. This is compile-contract cleanup.

Problem: `_activeLocks` originally derived its bit from the provided lock owner but cleared by scanning buffer owners. If a future job owner differed from the buffer owner, unlock could clear the bit while another job was still reading a vault buffer.
Solution: Derive the lock bit from `BufferID` bucket only and clear it only when no locked occupied block in the same bucket remains. The owner-aware overload stays for API provenance, but the veto bit now tracks actual buffer lock lifetime without a new allocation.
Rejected Alternatives: A new native lock-owner table was rejected because it would add persistent state and H-PHI debt for a 32-bit veto. Owner-derived bits without per-bit refcounts were rejected as unsafe under owner/buffer mismatch.
Scalability potential: Low = conservative false positives are acceptable and avoid MX350 corruption. Middle = multiple buffers in the same bucket keep compaction halted until all bucket locks are gone. High/Ultra = rare extra skipped FrostTick is preferable to stale Burst pointer exposure.
Hardware Impact: Same CAS operations as before. Potential extra skip is 0 moved bytes for that FrostTick; estimated memory safety gain is qualitative, not a microsecond save.

Problem: Final build validation after memory fixes surfaced unrelated compile walls from concurrent work.
Solution: Apply only narrow dependency repairs required to restore build evidence: remove duplicate local/vault exterior buoyancy sample ownership in `SubmarineFluidDynamics` and keep the vault-backed `VaultNativeBuffer<float3>`; add the missing `System` import for `FaunaBrain.Compatibility` so `[Flags]` resolves. Stale audio and interaction errors cleared before edit and were not touched.
Rejected Alternatives: Broad gameplay/audio refactors were rejected as domain breach. Marking memory as verified while the core project failed to compile was rejected because task 18 requires `dotnet build` exit 0.
Scalability potential: Submarine fix increases data sovereignty by removing a local sample array in favor of GlobalDataVault; Fauna import has no runtime effect.
Hardware Impact: Memory defrag runtime impact is 0 us. Submarine compile repair removes one local 8-element managed array from that system's ownership surface; exact runtime saving is not claimed.

Problem: The strict direct-free replay still found `UnsafeUtility.Free` in `NativeMemorySentinel.ReapSceneLifetimeLeaks()`, outside the H8 allocator authority.
Solution: Add `H8Memory.ReleaseSentinelReapedRaw()` and route the sentinel scene-leak reaper through it. The H8 path first checks the H8 allocation table and uses `ForceFreeRecordAt()` when the pointer is H8-tracked, so owner maps and block descriptors are retired before the free. Sentinel-only leak pointers still free through H8Memory, not through a second raw-free call site.
Rejected Alternatives: Leaving `NativeMemorySentinel` as a direct free exception was rejected because it bypasses the defrag map if a leaked pointer is also H8-tracked. Calling owner-tagged `FreeRaw()` from the sentinel was rejected because sentinel records store string owners, not `SystemID`, and guessing owner IDs would be unsafe. Moving leak reaping into gameplay systems was rejected as domain drift.
Scalability potential: Low = scene-unload leak recovery no longer creates a hidden allocator path on 8 GB devices. Middle = H8 tracked leak records retire their descriptor state consistently. High = larger visual/cache arenas retain one raw-free authority. Ultra = contiguous-memory maintenance remains observable through the H8 black-box instead of split allocator ownership.
Hardware Impact: Hot path impact is 0 us. Fault-path cost is one H8 record lookup and one free during scene-unload leak reaping; exact microseconds are unprofiled and not claimed.

Problem: The old `FrostTickDefrag(elapsedSeconds)` and `FrostTickDefrag(elapsedSeconds, stress)` overloads still self-labeled calls as `PreSimulation`. Any future caller outside the dispatcher could therefore bypass the explicit phase contract.
Solution: Make `MemoryDefragPhase.Unspecified` the zero/default enum value, shift `PreSimulation` to explicit value 1, mark the legacy overloads obsolete, and route them to `Unspecified`. The existing non-PRE_SIM gate now blocks those calls before gap analysis or `MemMove`.
Rejected Alternatives: Removing the overloads outright was rejected because parallel agents may still compile against the interface during the batch. Leaving default enum zero as `PreSimulation` was rejected because default values must fail closed. Treating `VisualSync` as the legacy route was rejected because it hides the actual problem: unspecified callers are unauthorized, not visual-sync callers.
Scalability potential: Low = no accidental MX350 compaction from a wrong cadence path. Middle = dispatcher remains the single movement authority. High = larger arenas still compact only in the pre-job window. Ultra = visual/cache systems can rely on stable pointers outside PRE_SIM.
Hardware Impact: Dispatcher path is unchanged. Legacy accidental calls do 0 moved bytes and one blocked black-box record; exact cost is unprofiled and not claimed.

Problem: Targeted build validation exposed an out-of-domain compile wall in `HectonBiolumManager`: a partial DataVault migration replaced local `NativeArray` fields with handles but left missing resolver methods and stale `_telemetryRing` references.
Solution: Add the missing resolver glue only: `EnsureVaultBuffers`, `TryResolvePredatorJobBuffers`, `TryResolvePredatorScores`, `TryResolveRippleJobBuffers`, `TryResolveRippleDistances`, `TryResolveTelemetryRing`, and `ReleaseVaultHandlesOnly`. Storage uses existing `BufferID.BiolumLegacyPredatorPositions`, `BiolumLegacyPredatorScores`, `BiolumLegacyRipplePositions`, `BiolumLegacyRippleDistances`, and `BiolumLegacyTelemetryRing` handles owned by `SystemID.Vfx`.
Rejected Alternatives: Reverting to local persistent `NativeArray` fields was rejected because it violates DataVault sovereignty. Refactoring Biolum visuals was rejected as outside this memory task. Running repeated rebuild loops was rejected per user instruction; one targeted build after repair is recorded.
Scalability potential: Low = Biolum job scratch and black-box telemetry no longer require local persistent native ownership. Middle = handles survive vault relocation by generation resolution. High = VFX buffers stay under the central vault map. Ultra = visual systems can use DataVault-owned staging without weakening memory compaction invariants.
Hardware Impact: Memory defrag hot path remains 0 us. Biolum compile repair adds no new containers; it resolves existing vault handles and writes a fixed 300-entry telemetry ring. Exact Biolum cost is unprofiled and not claimed.

Problem: Static replay found ownerless `TryLockBuffer`/`TryUnlockBuffer` calls outside Core/Memory. They still blocked compaction, but they collapsed provenance to `SystemID.Unknown`, weakening black-box evidence and future scheduling policy.
Solution: Mark ownerless compatibility overloads obsolete and update remaining call sites to use existing owner IDs: `SystemID.GameplayTools` for interaction/repair, `SystemID.GameplayLoot` for loot magnet jobs, `SystemID.GraphicsScalability` for shader-state telemetry, and `SystemID.Vfx` for biolum/hull-dent visual buffers.
Rejected Alternatives: Removing the compatibility overloads outright was rejected because parallel agents can still compile against the interface during the batch. Guessing new owner IDs was rejected; every patched owner already matched the buffer allocation authority in the local file. Treating Unity `BatchCullingOutput` raw `UnsafeUtility.Malloc` as H8 heap storage was rejected because Unity owns that TempJob callback memory after the BRG culling callback.
Scalability potential: Low = MX350 compaction telemetry now identifies which scheduler system held a lock. Middle = lock policy can become stricter without losing provenance. High = renderer/VFX locks remain visible without creating managed events. Ultra = high-tier visual buffers can stay aggressive while the memory black-box still names the lock owner.
Hardware Impact: Compaction hot path remains 0 us changed. Lock/unlock already paid the CAS cost; adding owner arguments changes provenance only. Targeted compile command was `dotnet build --no-restore`, not `dotnet rebuild`, and it exited 0 with 0 warnings and 0 errors.
