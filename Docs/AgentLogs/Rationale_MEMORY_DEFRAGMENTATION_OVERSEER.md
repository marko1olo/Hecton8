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
