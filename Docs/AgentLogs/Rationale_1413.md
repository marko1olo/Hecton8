# Rationale 1413

Date: 2026-05-28
Status: ACTIVE / PENDING VERIFICATION

## R0 - Startup Scope

Problem: Broad GlobalDataVault lock spans can stall defrag and sibling systems.
Solution: First produce a machine-readable lock ledger before edits. Only mutate source after the lock owner, method, span, release path, loop context, and fail-closed behavior are known.
Rejected Alternatives: Direct manual edits from grep output. Too much risk of moving a lock without proving the matching `finally` release and mutation scope.
Scalability potential: Low tier avoids stalls by skipping contested writes; Middle tier preserves cadence with narrow critical sections; High tier uses saved CPU headroom for denser stable snapshots; Ultra spends savings in VISUAL_SYNC, not longer truth locks.
Hardware Impact: Expected gain on i3/MX350 is reduced atomic contention and fewer main-thread stalls. Exact microseconds require generated ledger and optional Stopwatch harness.

## R1 - Scanner Scope

Problem: `rg` returned a multi-thousand-line lock list with truncation and no proof of span, `finally`, loop context, or nested lock risk.
Solution: Built `agent1413_lock_line_scanner.py`, a line-regex brace-depth scanner that writes `LOCK_CONTENTION_SPAN_LEDGER_1413.json`. It found 1,217 lock invocations, 326 loop-shaped lock sites, and 45 nested-lock body shapes in 31,797,219 us.
Rejected Alternatives: Roslyn compile pipeline, because it risks csc contention and violates the build-throttle spirit for a read-only static scan. PowerShell brace parser was rejected after timeout; too slow for this tree.
Scalability potential: Low tier benefits from fail-closed skipped writes instead of lock waits; Middle tier gets stable cadence; High and Ultra can spend recovered CPU in VISUAL_SYNC visual density.
Hardware Impact: Static scan did not touch runtime. The ledger identifies likely i3/MX350 contention points before any broad source edit.

## R2 - Core Vault Fail-Closed Gate

Problem: `GlobalDataVault.ClearActiveLockBitIfUnused` performed up to 32 `Thread.SpinWait(4)` attempts. Release paths also refused to enter while compaction fence was raised, which can leave already-held locks pinned if a caller does not retry release.
Solution: Removed the spin loop, added `RecordLockContentionFault(int key)` with unmanaged numeric fields, and split release gate from acquire gate. Acquires remain blocked by compaction fence; releases may enter the block mutation gate if it is free, without waiting.
Rejected Alternatives: Keep bounded spin because it is short. Rejected: any spin on lock contention violates the fail-closed doctrine and burns low-end CPU. Rejected `Debug.Log`: managed allocation and hot-path noise.
Scalability potential: Low: no wait loop during lock cleanup. Middle: contention is counted, not hidden. High: release can clear stale pins between compaction phases. Ultra: saved CPU remains available to presentation lanes.
Hardware Impact: Removes worst-case 32 * SpinWait(4) from one cleanup path. Expected i3/MX350 gain is reduced tail latency under defrag contention; runtime microseconds remain PENDING VERIFICATION.

## R3 - Destructible Organic Template Cache Lock Span

Problem: `BuildTemplateCaches` locked `OrganicTemplateDescriptors` and `OrganicLootEntries` before allocating temp scratch, copying authoring loot, building runtime descriptors, filling managed lookup arrays, and iterating flora/harvest templates. Ledger classified both top lock sites as loop-context with 100-line critical spans.
Solution: Prebuild `descriptorScratch` and `lootEntryScratch` before locking. The lock now resolves vault arrays, clears them, and copies precomputed struct rows only. Post-edit ledger shows both lock sites outside loop context with 27-line copy spans.
Rejected Alternatives: Keep cold path as-is because template build is not per-frame. Rejected: cache rebuild can still contend with compaction and other agents; cold does not justify holding a global memory pin through authoring traversal. Rejected direct `MemCpy` because managed arrays are not pinned and this cold path does not need unsafe pinning.
Scalability potential: Low: fewer long cache-build stalls during weak-device streaming/setup. Middle: deterministic setup cadence. High: saved contention budget can support denser flora descriptor sets. Ultra: visual overkill remains in vegetation presentation; vault locks stay byte-copy only.
Hardware Impact: Static span reduction from 100 to 27 source lines for two buffer locks. Exact i3/MX350 microseconds require Unity profiler or Stopwatch harness; status PENDING VERIFICATION.

## R4 - Editor Fail-Closed Proof Contract

Problem: The fail-closed path needed a repeatable proof that `TryAcquireWriteLock` does not wait or allocate when the mutation gate is already owned.
Solution: Added `GlobalDataVaultFailClosedEditTests1413` under `UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS`. It forces `_blockMutationGate`, warms the generic path, attempts 10,000 write locks, checks all fail, checks numeric locked-skip telemetry, and asserts zero thread-local allocation across the loop.
Rejected Alternatives: Run Unity Test Runner now. Rejected because current instruction forbids heavy validation unless critical, and this environment has no Unity console artifact. Reflection in runtime was rejected; the test is editor-only and excluded from builds unless the test symbol is enabled.
Scalability potential: Low/Middle/High/Ultra all require the same fail-closed truth; quality scaling is irrelevant to authority locks and must not change lock behavior.
Hardware Impact: Prevents future reintroduction of waits in a hot failure mode. Runtime proof pending.

## R5 - Build Throttle Decision

Problem: C# syntax changes normally require compile proof, but the host CPU sample returned 100% load with one `dotnet` process already active.
Solution: Did not launch `dotnet build`. Used `git diff --check`, targeted `rg`, scanner reruns, and manual C# scope review. Marked compile as `BLOCKED_BY_CONTENTION`.
Rejected Alternatives: Launch build anyway. Rejected: explicit task and AGENTS rules forbid adding compiler load above 50% CPU or during active compiler/dotnet pressure.
Scalability potential: Agent coordination matters more than local certainty. A blocked build protects other agents' work and keeps the machine responsive.
Hardware Impact: Avoided a full CPU spike on an already saturated host. Syntax status remains PENDING VERIFICATION.

## R6 - Remaining Risk

Problem: The project still contains 325 loop-shaped lock sites and 46 nested-lock body shapes after the focused edits. Several high-priority offenders remain in volumetric fog, visual pressure aging, persistent registry, storm propagation, and vehicle damage.
Solution: Final JSON lists top remaining offenders instead of claiming global completion. This batch changed the core fail-closed gate and one top cold-cache span with clear source proof.
Rejected Alternatives: Rewrite 251 files in one pass. Rejected: too much ownership risk, no compile bandwidth, and high probability of cross-domain damage.
Scalability potential: Next passes can attack the report-ranked offenders by domain owner without global API churn.
Hardware Impact: Current changes reduce specific lock hold and spin risk; project-wide contention graph remains PENDING VERIFICATION until the remaining offenders are handled and profiled.

## R7 - APEX Release-Proof Correction

Problem: The first `BuildTemplateCaches` patch still had a manual descriptor unlock branch when `OrganicLootEntriesBufferId` failed to lock. It was probably operationally safe, but it failed the stronger proof requirement that every successful lock has a `finally` release path.
Solution: Moved the second lock attempt inside the descriptor lock's `try` scope. The descriptor lock is acquired at line 5044, the protected scope starts at line 5052, the loot lock is attempted at line 5054, and both unlocks execute from the `finally` block at lines 5090-5096.
Rejected Alternatives: Keep manual unlock because it is short. Rejected: proof is weaker and future edits could add an early return above the manual release.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is correctness hardening, not quality scaling.
Hardware Impact: No measurable CPU gain expected. Prevents one release-path proof gap from becoming a stale pin during future edits.

## R8 - APEX Verification Artifact

Problem: Verbal "zero GC" and "finally safe" claims are not evidence. The prior final report also used stale file hashes after the descriptor release correction.
Solution: Added `agent1413_apex_verifier.py` and generated `LOCK_CONTENTION_APEX_VERIFICATION_1413.json`. The verifier scanned modified hot windows for reference `new`, `string.Format`, `.ToString()`, LINQ, and `foreach`; total forbidden hits were 0. It also recorded BufferID values 73018/73019, line numbers, file SHA-256 hashes, and final report hash linkage.
Rejected Alternatives: Roslyn compile/AST. Rejected because final CPU sample was 100.000000% and both `dotnet` and `csc` were active; launching compiler validation would violate the resource-throttling mandate.
Scalability potential: Verification does not scale runtime load. It protects the lock-minimization contract across all quality levels.
Hardware Impact: No runtime cost. Static verifier cost was 5,200,000 us class work on the host.

## R9 - Release API Residual Debt

Problem: `ReleaseWriteLock` and `TryUnlockBuffer` still return `false` if `_blockMutationGate` is busy. That avoids waiting, but a caller that ignores the bool in `finally` can leave stale writer pins. This is a domain-level correctness risk, not a solved issue.
Solution: Recorded the debt in APEX and final report. Did not introduce spin, sleep, or blind direct `_blocks` mutation outside the gate.
Rejected Alternatives: Add bounded spin in release. Rejected: violates fail-closed/no-wait doctrine and burns i3/MX350 CPU under contention. Directly clearing `_blocks` without the gate was rejected because it risks racing compaction/arena mutation.
Scalability potential: Proper solution should be a deferred-release record drained by the vault owner phase or an owner-phase guaranteed release lane, continuous across device tiers and independent of `GlobalQualityWeight`.
Hardware Impact: Current state has no new CPU wait, but stale-pin risk remains until a release-drain protocol is implemented and tested.

## R10 - Deferred Release Ring

Problem: Caller scan proved the release debt is broad: many systems call `ReleaseWriteLock`/`TryUnlockBuffer` in `finally` and ignore the bool. If `_blockMutationGate` is busy at that exact moment, a pin can survive with no caller retry.
Solution: Added a fixed 256-slot unmanaged `DeferredVaultReleaseRequest` ring inside `GlobalDataVault`. On release-gate contention, writer and buffer-pin releases enqueue scalar release requests. The next successful mutation-gate entrant drains requests before continuing, while holding the same gate that protects `_blocks` and metadata.
Rejected Alternatives: Mass-edit every caller. Rejected: too much cross-domain churn and still not atomic. Rejected release spin: violates fail-closed and burns low-tier CPU. Rejected direct ungated release: races compaction/arena mutation.
Scalability potential: Low tier avoids blocking waits and gets eventual release without caller retries. Middle tier pays no cost unless contention exists. High/Ultra preserve memory compaction cadence and keep visual budget available for presentation lanes. `GlobalQualityWeight` is not used because release correctness is authority truth, not scalable fidelity.
Hardware Impact: Hot normal release path adds no allocation. Contended release path scans up to 256 fixed slots only when the gate is busy; i3/MX350 cost is bounded and replaces unbounded stale-pin risk. Compiler/runtime proof is still pending.

## R11 - Deferred Release Poison-Slot Fix

Problem: A duplicate or stale deferred buffer-pin release could remain pending forever if the buffer's `LastAliasRequester` no longer matched the queued owner while another pin still held `Reserved1 > 0`.
Solution: Treat owner mismatch as a stale drained request and clear the active bit opportunistically. Added metadata-index bounds validation before queue writes.
Rejected Alternatives: Keep stale requests pending. Rejected: no deterministic future state makes an already stale owner request valid, and it can poison one of 256 slots permanently. Rejected broad queue compaction: unnecessary moving state and more branches.
Scalability potential: Low/Middle/High/Ultra unchanged. Correctness ring cleanup is not quality-scaled.
Hardware Impact: Adds two scalar validation checks on contention queue path and O(1) stale request cleanup. No normal hot-path allocation.

## R12 - APEX Evidence Tightening

Problem: `LOCK_CONTENTION_APEX_VERIFICATION_1413.json` proved Zero-GC and Data Sovereignty, but did not embed the compilation-throttle sample. That made the final CPU/build compliance proof dependent on `Status_1413.md` and chat text.
Solution: Patched `agent1413_apex_verifier.py` to copy `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json.compilationThrottle` into `compilationResourceThrottling` and record `generatedUtc`. Regenerated the APEX JSON.
Rejected Alternatives: Leave CPU sample only in Markdown. Rejected because the user requested a final JSON evidence artifact with cryptographic hash.
Scalability potential: Verification-only change. Runtime Low/Middle/High/Ultra behavior is unchanged.
Hardware Impact: No runtime impact. Static verifier rerun cost was a host-side Python read/write; no compiler process launched.

## R13 - Deferred Release Duplicate Coalescing

Problem: A caller may retry `ReleaseWriteLock` or `TryUnlockBuffer` after receiving `false` from a contended release. Without coalescing, identical pending release requests could be stored multiple times and later double-decrement `Reserved1`.
Solution: `QueueDeferredRelease` now scans the fixed 256-slot native ring for an identical pending request before claiming an empty slot. Equality requires matching buffer key, offset, active lock bit, owner system id, and release kind.
Rejected Alternatives: Edit every caller to never retry. Rejected because hundreds of call sites exist and the vault must be robust when callers ignore the bool. Rejected adding a managed set: violates Zero-GC and makes release contention allocate under stress.
Scalability potential: Low tier pays at most 256 scalar reads only under release-gate contention; Middle/High/Ultra keep normal release path unchanged. `GlobalQualityWeight` is not used because this is correctness, not scalable fidelity.
Hardware Impact: Adds bounded O(256) native scalar scan to contended release queueing. Prevents over-release without adding waits or compiler load.

## R14 - Count-Preserving Release Contract

Problem: The first duplicate coalescing rule was too broad. Buffer-pin releases cannot be coalesced by `(buffer, owner)` because the same owner may hold several legitimate pins on the same buffer, and each pin requires its own `Reserved1` decrement.
Solution: Coalesce only writer-release requests. Buffer-pin releases keep one queued request per release call. Additionally, public contended release paths now return the queue result: accepted deferred release returns `true`, queue failure returns `false`.
Rejected Alternatives: Add a count field to `DeferredVaultReleaseRequest`. Rejected for this pass because it changes ABI semantics and needs compiler/runtime proof. Rejected managed retry tracking: violates Zero-GC and adds shared managed state under contention.
Scalability potential: Low devices keep bounded queue work and avoid lock waits. Middle/High/Ultra keep correctness independent of visual quality. `GlobalQualityWeight` remains intentionally unused because release ownership is truth, not fidelity.
Hardware Impact: Writer duplicate scan is O(256) only on contended release. Buffer-pin path avoids extra scan and preserves exact release count.

## R15 - Pre-Defrag Deferred Release Drain

Problem: Some vault maintenance paths check `_activeLocks` before they enter the block mutation gate. Accepted deferred releases clear `_activeLocks` only during drain, so these maintenance paths could repeatedly skip and never reach the drain point.
Solution: Added `TryDrainDeferredReleaseRequests()`, a single-attempt, non-blocking drain helper. Called it before active-lock checks in orphan sweep, mock relocation validation, defrag tick classification, live compaction slice, arena growth, and deferred arena growth. `HasActiveBurstLocks` remains pure.
Rejected Alternatives: Make `HasActiveBurstLocks` drain. Rejected because read accessors must not mutate global state. Rejected spin/retry drain because fail-closed forbids waits.
Scalability potential: Low devices avoid defrag starvation without blocking. Middle/High/Ultra keep memory maintenance responsive. `GlobalQualityWeight` remains unused because release and defrag truth cannot vary by quality.
Hardware Impact: Adds one `CompareExchange` attempt only on maintenance paths before active-lock checks; no managed allocation and no wait loop.

## R16 - Mutation-Gate Drain Fault Containment

Problem: `TryEnterBlockMutationGate` and `TryEnterReleaseMutationGate` acquired `_blockMutationGate`, then called `DrainDeferredReleaseRequestsLocked()` before returning the acquired gate to the caller. If editor/runtime native-container safety checks throw inside drain, the gate could remain permanently held and fail-closed paths would degrade into a memory-service stall.
Solution: Wrapped the pre-return drain in `try/catch` guards that call `ReleaseBlockMutationGate()` before rethrowing. This preserves the caller contract on success and contains the gate on fault.
Rejected Alternatives: Swallow the exception and return false. Rejected because a drain fault is structural corruption or safety-check failure; hiding it would produce unverifiable memory state. Rejected spin/retry because fail-closed forbids waits.
Scalability potential: Low/Middle/High/Ultra behavior is identical; mutation-gate correctness is authority infrastructure, not visual fidelity. `GlobalQualityWeight` remains intentionally unused.
Hardware Impact: Normal path adds no loops and no allocations. Fault path releases one atomic gate before propagating the error; runtime microseconds remain PENDING COMPILER/UNITY RUNTIME VERIFICATION.

## R17 - Alias-Open Contention Dump Removal

Problem: `TryOpenAliasBuffer` used `DumpPhiVodBlackBox()` when `TryEnterBlockMutationGate()` failed. That branch is normal contention, not memory corruption. `DumpPhiVodBlackBox()` performs managed path handling and file writes, so it violates the fail-closed rule for a lock contention path.
Solution: Replaced that branch with `RecordLockContentionFault(key)` and `return false`. Left dumps on pointer/meta/type/alignment corruption branches because those are forensic evidence routes, not expected contention.
Rejected Alternatives: Remove all dumps from `TryOpenAliasBuffer`. Rejected because real corruption must keep black-box proof. Rejected async dump request on contention because even request escalation is unnecessary for a routine busy gate.
Scalability potential: Low devices avoid filesystem work on alias-open contention. Middle/High/Ultra keep identical truth behavior; no `GlobalQualityWeight` usage because alias lock correctness is not visual fidelity.
Hardware Impact: Contended alias-open path now performs only numeric fault writes and returns. Runtime microseconds remain PENDING COMPILER/UNITY RUNTIME VERIFICATION.

## R18 - External-View Publish Rollback Containment

Problem: In `TryEnsureVaultBuffer`, the new allocation path created a buffer and metadata, then attempted `MarkExternalView`. If that publish step failed because the mutation gate was busy, the cleanup removed `_buffers`, metadata, and key routing, then attempted `TryFreeBlockRollback` while the gate was already unavailable. This can strand an occupied arena block without metadata.
Solution: On external-view publish failure after a successful new allocation, keep the registered buffer/metadata route, record numeric contention, and return false. A retry can publish the view against the existing buffer. Other corruption branches still remove/rollback and dump forensic evidence.
Rejected Alternatives: Retry or spin for the view publish. Rejected because fail-closed forbids waits. Rejected rollback under known gate contention because it can fail for the same reason and is worse than preserving the buffer route.
Scalability potential: Low devices avoid orphaned memory after transient contention. Middle/High/Ultra keep deterministic buffer ownership. `GlobalQualityWeight` remains unused because ownership truth is not scalable fidelity.
Hardware Impact: Removes one rollback attempt from a known-contention branch and prevents orphan block risk. Runtime microseconds remain PENDING COMPILER/UNITY RUNTIME VERIFICATION.

## R19 - Throttled Assembly-CSharp Compiler Proof

Problem: Native/unmanaged `GlobalDataVault` edits required at least one syntax and assembly proof, but project rules forbid compiler load while CPU exceeds 50% or active `dotnet`/`csc` processes exist.
Solution: Waited for a clean gate sample, then ran exactly one minimal compile: `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`. Pre-build sample was CPU `27.641788%`, `dotnetCount=0`, `cscCount=0`. Build result: `0 Warning(s)`, `0 Error(s)`, elapsed `48.80s`.
Rejected Alternatives: Solution-wide build, restore, repeated compiler probes, or Unity Test Runner launch. Rejected because they would add unnecessary CPU pressure and broader cross-agent contention.
Scalability potential: Verification-only change. Runtime Low/Middle/High/Ultra behavior is unchanged. `GlobalQualityWeight` still does not affect vault ownership, DTO layout, release correctness, or authority routes.
Hardware Impact: Compiler proof only. Runtime/editor behavior remains `PENDING UNITY RUNTIME VERIFICATION`; no profiler microsecond claim is made.

## R20 - Evidence Hash Reconciliation

Problem: After log/rationale updates and filesystem line-ending state, the current on-disk `GlobalDataVault.cs` SHA-256 no longer matched the hash embedded in the previous APEX JSON. A stale hash invalidates the final evidence chain even if source semantics did not change.
Solution: Rechecked raw file hashes, corrected `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json.modifiedFiles`, and regenerated `LOCK_CONTENTION_APEX_VERIFICATION_1413.json`. Current `GlobalDataVault.cs` SHA-256 is `0d0599170f98d1c4dacf76e452d1a3401cd85e7a1ef8f320f04b0e0d5691d86e`; current APEX SHA-256 is `61cbc71a6a8cbafe2c75702321b43b68a11f5fea04406fbf27041bc708c54822`.
Rejected Alternatives: Report the old hash as "close enough" or leave hash drift unexplained. Rejected because the APEX protocol requires byte-exact evidence, not semantic intent.
Scalability potential: Verification-only. Runtime Low/Middle/High/Ultra behavior is unchanged.
Hardware Impact: No runtime effect. The post-APEX sample was CPU `88.088131%`, `dotnetCount=2`, `cscCount=0`; no extra build/test was launched.

## R21 - Cross-Agent Deferred Release Contract Reconciliation

Problem: Current `QueueDeferredRelease` contains writer-only de-duplication. That is consistent with 1413 count-preservation, and current `ArenaAllocatorSentinel1414EditTests.cs:90` now asserts `StringAssert.Contains("if (kind == DeferredReleaseKindWriter)", queue)`. The remaining issue is documentation drift and the tokenless retry edge case.
Solution: Keep writer-only coalescing. Update the 1413 report/verifier evidence to match the active code and test contract. Record the remaining API limitation explicitly: with no per-acquire token, a caller that retries `TryUnlockBuffer` after accepted `true` can enqueue multiple buffer-pin releases.
Rejected Alternatives: Coalesce all buffer-pin releases. Rejected because `TryLockBuffer` increments `Reserved1` for same-owner pins; all-kind coalescing can leak legitimate nested/same-owner pins. Adding a tokenized lock handle was rejected for this polish pass because it is a public API and caller migration problem, not a surgical proof correction.
Scalability potential: Low tier preserves buffer-pin count semantics without waits. Middle/High/Ultra keep the same unmanaged ring. `GlobalQualityWeight` remains irrelevant because memory ownership is authority truth, not visual fidelity.
Hardware Impact: No code-path change in this loop. The honest residual retry risk is now documented instead of falsely reported as solved.

Evidence: APEX regenerated at `2026-05-28T09:59:18Z` with `totalForbiddenHits=0`, `hasWriterOnlyFilter=true`, `hasSerializedScanGate=true`, and `matchesArenaAllocator1414EditorContract=true`. Final hashes: `GlobalDataVault.cs` = `b35073e0f7ad2e833767c0b3f6b3139a05942bd9b416bc77c8f373b9a3d74aac`; APEX JSON = `bd30901deb1e6e10df4ec5efe39299864f6c201a90eac81559de59cbeff29114`. Post-regeneration sample was CPU `91.810825%`, `dotnetCount=1`, `cscCount=0`; no extra build/test was launched.

## R22 - TryAllocatePublishedBuffer Evidence Closure

Problem: Parallel edits moved new-buffer allocation and rollback into `TryAllocatePublishedBuffer<T>`, but the APEX zero-GC scanner still covered only `TryEnsureVaultBuffer<T>`. That left a proof gap over the actual gate-owned allocation/cleanup method.
Solution: Added `TryAllocatePublishedBuffer<T>` to `agent1413_apex_verifier.py` hot-window scanning and regenerated APEX. Current proof: method lines 1270-1375, lineCount 106, forbiddenHitCount 0. Static rollback proof: gate acquisition at line 1283, cleanup `finally` at lines 1357-1374, `FreeBlockLocked` at line 1368, `ReleaseBlockMutationGate()` at line 1373.
Rejected Alternatives: Treat the caller `TryEnsureVaultBuffer<T>` scan as sufficient. Rejected because the rollback logic now lives in the callee and must be directly scanned. Runtime-code rewrite was also rejected because the current method already keeps cleanup under the held gate.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is evidence closure for ownership truth; `GlobalQualityWeight` must not scale allocation ownership, DTO layout, or release semantics.
Hardware Impact: No runtime code change. Verification rerun was host-side JSON/static scan only. Build remained blocked by an active dotnet process despite CPU below 50%.

Evidence: APEX regenerated at `2026-05-28T10:12:41Z`; `totalForbiddenHits=0`; runtime sample CPU `48.971125%`, `dotnetCount=1`, `cscCount=0`. Final hashes: optimization report = `a4f45e1c3625774b9e917bdb6848b2f1c5dae7844dd016edb7c5e6342aa4b537`; APEX JSON = `79500eb84222fb46fb039e4e50ebf6ddd608c759c14dd655eb1185583ac72b8a`; verifier script = `8a5ff144a662564b16db0a6e9c8d71ae1796ec2c2befa66c1501ef26e24ccc25`; `GlobalDataVault.cs` = `b35073e0f7ad2e833767c0b3f6b3139a05942bd9b416bc77c8f373b9a3d74aac`.

## R23 - Deferred Release Invalid-Kind Guard

Problem: `DrainDeferredReleaseRequestsLocked()` routed every non-writer pending request into the buffer-pin drain. Normal private callers only write `DeferredReleaseKindWriter` or `DeferredReleaseKindBufferPin`, but a malformed native slot with an invalid non-zero kind could be interpreted as a buffer-pin release and decrement `Reserved1`.
Solution: `QueueDeferredRelease()` now rejects any kind outside the two legal constants, and drain treats invalid pending kinds as poison slots to clear without touching lock counters. This preserves fail-closed behavior while preventing a malformed request from becoming a false release.
Rejected Alternatives: Keep trusting private callers. Rejected because the ring is unmanaged mutable infrastructure and the cost of explicit kind validation is one scalar branch. Dumping black-box data for invalid kind was rejected in this hot drain path because the safe action is to discard the malformed pending release without file IO.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is memory-ownership truth and is not scaled by `GlobalQualityWeight`.
Hardware Impact: Adds one scalar kind validation at enqueue and one scalar branch in drain. No allocation, no spin, no wait. Compiler/runtime proof for this final runtime edit is blocked by CPU/compiler contention.

Evidence: APEX regenerated at `2026-05-28T11:11:32Z`; `totalForbiddenHits=0`; runtime sample CPU `100`, `dotnetCount=1`, `cscCount=1`. Final hashes: `GlobalDataVault.cs` = `3d9f0351e690de3b7e42269fbcc78a131833b1bfde15be42342e4171101a417f`; optimization report = `bdb8156cf0ba68ea9a4153029c717f283615a3b599357b9b2f980c8b460e1e3d`; APEX JSON = `e40ca9180b2a6aefe0dab7c1b5a39c3d33640a96b0ce0ca65b9ba2c25dd6c737`.

## R24 - Live Compaction Contention Dump Removal

Problem: `TryRunLiveCompactionSlice()` requested a MemorySentry background dump when live compaction skipped because active locks remained or the mutation gate was busy. Those branches represent expected contention, not memory corruption, and `RequestMemorySentryDump()` crosses into managed signaling via `AutoResetEvent.Set()`.
Solution: Removed dump requests from those two ordinary contention branches while preserving `_defragLockedSkipCount`, `LastDefragFlags`, and `RecordLockContentionFault(0)` on the busy-gate branch. The black-box dump paths remain for real structural faults elsewhere.
Rejected Alternatives: Keep the background dump request because it is asynchronous. Rejected: even async request escalation is unnecessary for expected contention and violates the scalar fail-closed path. Removing all black-box dumps was rejected because corruption evidence remains required.
Scalability potential: Low devices avoid managed signal/file-dump pressure during expected lock contention. Middle/High/Ultra keep the same memory truth behavior. `GlobalQualityWeight` is intentionally unused because defrag lock authority is not visual fidelity.
Hardware Impact: Removes two managed dump-request calls from live compaction contention exits. No spin, no retry, no allocation added. Compiler/runtime proof remains blocked by active CPU/dotnet pressure.

Evidence: APEX regenerated at `2026-05-28T11:21:30Z`; `TryRunLiveCompactionSlice` lines 4163-4277, forbiddenHitCount 0; `totalForbiddenHits=0`; runtime sample CPU `56.840784`, `dotnetCount=1`, `cscCount=0`. Final hashes: `GlobalDataVault.cs` = `e290018fb05717d72f2c1ca8c726428856d395bfc1b32aa4b352a8f39cf0460c`; verifier = `fd329eeb7f99f2b722efddf0031c640d0ff55da847004cd40fb5978b334faea6`; optimization report = `dc85ef6326e41ab874a6d16e74d8529b905a68e440677f86e770c7d794290f35`; APEX JSON = `12951f15396647d3e4e833dc779a2b3d1c17b364e111df4aa89031292e186d9f`.

## R25 - Cleanup Contention Dump Removal and Compile Proof

Problem: `ReleaseSceneOwnedBuffers`, `ReleaseBuffersByOwner`, and `TryReleaseOrphanedBuffer` escalated expected locked/external-view cleanup states to PhiVod black-box dumps. Those branches are normal contention/pin states during scene or orphan cleanup, not structural corruption.
Solution: Removed the final remaining-count dump from scene release. Replaced locked owner-release and orphan locked/external-view dumps with scalar alias-blocked telemetry. Kept dumps for missing metadata, missing occupied block, and failed block free because those are structural faults.
Rejected Alternatives: Keep dumps because cleanup is cold. Rejected: cold contention still creates false forensic noise and managed/file work. Removing all cleanup dumps was rejected because true metadata/block corruption must remain observable.
Scalability potential: Low devices avoid unnecessary dump work during cleanup under locks. Middle/High/Ultra preserve identical memory ownership semantics. `GlobalQualityWeight` is not used because cleanup authority and lock truth are not visual fidelity.
Hardware Impact: Removes three dump escalation sites from expected cleanup contention. No allocation, no spin, no retry added. A throttled compile proof was possible after CPU dropped below 50% and no compiler processes were active.

Evidence: Compile launched only after sample CPU `45.100791`, `dotnetCount=0`, `cscCount=0`. Command: `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`. Result: `0 Warning(s)`, `0 Error(s)`, elapsed `32.14s`. APEX regenerated at `2026-05-28T11:31:22Z`; `totalForbiddenHits=0`; build result in APEX: PASSED. Final hashes: `GlobalDataVault.cs` = `c4b38840a22049a9e393da2901bb37662e5536fde1245efcc7d88759b0053f33`; verifier = `c3d7f45da7acff241970af51d109d35cafbe55180a77df524fc8f1de71af7ce1`; optimization report = `82e0c6d738eb2ffb8a9a775525bdda50954d21dfa6f9d286e63513742156942b`; APEX JSON = `15f613ed4b3db81eee984059d5bc3e5719bc2028b014e5d57d6c19bf99bde21d`.

## R26 - Acquisition Telemetry and Deferred Enqueue Gate Removal

Problem: `TryAcquireWriteLock` and `TryLockBuffer` still had silent false-return branches for real contention: compaction fence, active writer, alias reader, external view, and saturated pin count. Separately, `QueueDeferredRelease` used its own `_deferredReleaseEnqueueGate`; if that serializer gate was busy, a release request returned `false` before reaching the fixed native ring, reintroducing the stale-lock risk this pass was built to remove.

Solution: Instrumented acquisition contention branches with `RecordLockContentionFault(key)` and left structural mismatches as non-contention failures. Removed `_deferredReleaseEnqueueGate` entirely. `QueueDeferredRelease` now scans writer duplicates and then claims a request slot through per-slot `Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)`. Writer de-duplication remains writer-only; buffer-pin releases stay one request per accepted release call.

Rejected Alternatives: Keeping silent false returns was rejected because fail-closed requires numeric proof of skipped writes. Adding dumps was rejected because ordinary contention is not corruption. Keeping the enqueue gate was rejected because release queuing must not have a second fail-closed point that callers commonly ignore. Coalescing buffer-pin releases was rejected because same-owner nested pins legitimately increment `Reserved1` more than once.

Scalability potential: Low devices get scalar telemetry and no wait loop under defrag/write contention. Middle devices keep bounded fixed-ring release work. High and Ultra devices preserve the same authority route and can spend recovered CPU in visual lanes. `GlobalQualityWeight` is intentionally not consumed here because lock ownership, DTO layout, and release correctness are gameplay/memory truth, not visual fidelity.

Hardware Impact: Adds only scalar `Interlocked`/field writes to acquisition contention false paths. Removes one global enqueue serializer gate from deferred release, replacing it with existing per-slot atomic state transitions. No managed allocation, no spin, no sleep, no file dump. Current compile proof is blocked: conditional build sampled CPU `83.531429`, `dotnetCount=2`, `cscCount=0`; APEX runtime sample after regeneration was CPU `91.48299`, `dotnetCount=2`, `cscCount=1`.

Evidence: APEX regenerated at `2026-05-28T11:49:39Z`; `totalForbiddenHits=0`; scanned `TryAcquireWriteLock` lines 1723-1880, `TryLockBuffer` lines 2474-2578, and `QueueDeferredRelease` lines 2008-2067 with forbiddenHitCount 0. Final hashes: `GlobalDataVault.cs` = `a3638d5f0d3bd01cd69d9494dde954cd22eec4a48b4366f7245f4c273e2d6ba7`; verifier = `b5f67eb0658f3ca8ea83837cf21574a7b2cda226cac620478f99cac7f4e4468b`; optimization report = `3e84d6995b75ffe8ca758c92bd2e0295a8c1f66001ebe8be050f78becbfbeb94`; APEX JSON = `7f57084a33c3fd152814e55498e1d504e1ca29ee9054c5260a85106d95a8b511`.

## R27 - Deferred Queue Gate Reality Reconciliation

Problem: Final byte-level self-audit contradicted the prior Loop 23 evidence. Current `GlobalDataVault.cs` hash was `97153541fbf227867a11f48b98b21b10efd9ff49ac22a0807c8ba222dc21cff1`, and source still contained `_deferredReleaseEnqueueGate` plus `Thread.SpinWait(DeferredReleaseEnqueueSpinWait)` in `QueueDeferredRelease`. That meant the intended no-wait release queue policy was not actually present in the latest bytes.

Solution: Removed `_deferredReleaseEnqueueGate`, its initialization/reset writes, the dead spin-wait constant, and the serializer try/finally around queue writes. `QueueDeferredRelease` now performs a writer-only duplicate scan and claims each fixed ring slot with `Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)`. This keeps buffer-pin release count semantics intact while removing the extra wait point.

Rejected Alternatives: Leaving the gate and reporting it as harmless was rejected because it spins and serializes release enqueue under contention. Replacing it with a blocking lock was rejected for the same reason. Coalescing buffer-pin releases was rejected because same-owner nested pins legitimately increment `Reserved1` more than once.

Scalability potential: Low devices avoid release-path spin waits. Middle devices keep bounded fixed-ring behavior. High and Ultra devices preserve the same memory authority and can spend saved CPU outside the vault. `GlobalQualityWeight` is intentionally not consumed here because lock ownership and release correctness are memory truth, not visual fidelity.

Hardware Impact: Removes one private enqueue gate, one spin-wait loop, two gate reset writes, and one dead spin constant. No managed allocation, no file IO, no public API change. Compiler proof is still blocked by resource policy: post-fix sample CPU `96.77117`, `dotnetCount=1`, `cscCount=0`; no build launched.

Evidence: APEX regenerated at `2026-05-28T12:02:37Z`; `totalForbiddenHits=0`; `hasNoEnqueueGateBusyFailPath=true`; `hasAtomicSlotClaim=true`. Text scan for `_deferredReleaseEnqueueGate`, `DeferredReleaseEnqueueSpinWait`, and `Thread.SpinWait(` returned no hits. Final hashes: `GlobalDataVault.cs` = `41bc397a1f2a2c371ff71e175f1a1dc092531aef1fdd85848b38818b39075b20`; verifier = `b5f67eb0658f3ca8ea83837cf21574a7b2cda226cac620478f99cac7f4e4468b`; optimization report = `e2851666f10999e97c88da530508758427e0690ac6c49fd846aa02cc4b299d59`; APEX JSON = `49acd08502c659191efb4bcce8f0e93e0d8a6611d3acac1ceb03f242e82e11d8`.

## R28 - Stale Writer Release Drain Hardening

Problem: Removing the enqueue serializer made the writer duplicate scan explicitly best-effort. That is acceptable only if stale duplicate writer records drain idempotently. The current drain returned `false` when a queued owner no longer matched a current nonzero `ActiveWriterSystemID`, leaving the stale request pending forever and consuming a fixed ring slot.

Solution: `DrainDeferredWriterReleaseLocked` now treats owner mismatch as a stale queued writer release: it calls `ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit)` and returns `true` to clear the request slot. It does not call `ReleaseWriterBlockLockUnlocked`, so a newer writer lock is not released by an old queued record.

Rejected Alternatives: Reintroducing a global enqueue gate was rejected because it restores the release-path wait. Strict atomic duplicate prevention was rejected for this pass because it would require a new per-buffer queued-release marker or tokenized API migration. Leaving the stale slot pending was rejected because the fixed ring can saturate under repeated retry/order drift.

Scalability potential: Low devices avoid a fixed-ring saturation failure and still avoid release-path spin waits. Middle/High/Ultra preserve identical lock authority while making stale queued writer releases self-cleaning. `GlobalQualityWeight` is not consumed because this is memory ownership truth.

Hardware Impact: One owner-mismatch branch now clears stale active-bit state and frees the request slot. No allocation, no file IO, no public API change. Compile proof passed under throttle.

Evidence: Build launched only after sample CPU `46.330838`, `dotnetCount=0`, `cscCount=0`. Command: `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`. Result: `0 Warning(s)`, `0 Error(s)`, elapsed `21.52s`. APEX regenerated at `2026-05-28T12:17:32Z`; `totalForbiddenHits=0`; `hasStaleWriterOwnerDiscard=true`; `hasNoEnqueueGateBusyFailPath=true`; `hasAtomicSlotClaim=true`. Final hashes: `GlobalDataVault.cs` = `d18348927bfcad28fb4d77850ae233fb89173b16a90f277359960dbbf0c5ca34`; verifier = `66adcb69279b87d9349e37e7b1a80182d3b2c133452a274dd5d38b6b0bc9b2fe`; optimization report = `0ab83d555b621d0bb3ed2e74f1b06ac90b3ef29031b56e5c40eaf7dd2f5f75e9`; APEX JSON = `e0064cbd703d605b8e99f878bdd5b7315c00b7d163f01e16cdfb55b81af391d8`.

## R29 - Project-Wide Lock Span Ledger Refresh

Problem: The lock span ledger was stale after Loop 25. A stale ledger undermines any claim about remaining work and hides current project-wide lock candidates.

Solution: Re-ran `agent1413_lock_line_scanner.py`, embedded the refreshed ledger into the optimization report, updated the ledger hash, and regenerated APEX. The scanner reports current candidates, not automatic bugs: filesWithLocks `270`, lockInvocationCount `1347`, insideLoopCount `351`, nestedLockCount `54`.

Rejected Alternatives: Leaving the 01:05 ledger in the final proof was rejected because it did not describe current bytes. Editing all top candidates was rejected in this loop because they span Graphics, World, Atmosphere, Physics, UI, and AI domains; touching them without local owner/context would breach the domain-boundary rule and risk cross-agent collisions.

Scalability potential: Verification-only. Low/Middle/High/Ultra runtime behavior unchanged. The ledger identifies future places where critical-section surgery can buy frame-time budget for visual lanes.

Hardware Impact: No runtime code change. Scanner cost: `35046041` microseconds host-side. No build needed after ledger-only changes.

Evidence: `LOCK_CONTENTION_SPAN_LEDGER_1413.json` generatedUtc `2026-05-28T12:24:05Z`; SHA-256 `036f990ff1c993bb1c10deaf9ff639c3c29ebfcacbf41884e6db3d7d6c5b4019`. APEX regenerated at `2026-05-28T12:25:37Z`; optimization report SHA-256 `703634e7eb84aa110a492307bc1bae62ce98beb33c36a4e35058e61f464a0578`; APEX SHA-256 `d30e531447721dc9cbd3e72db9eceaf2f8fea80ef498d8df7241a1b4aa2486a9`.

## R30 - VisualPressureAgingRuntime Critical Section Split

Problem: `VisualSyncTick` was a confirmed top lock-span offender. The old shape acquired seven vault locks and held them through buffer resolution, capacity math, `GraphicsBuffer.LockBufferForWrite`, `UnsafeUtility.MemCpy`, shader global writes, runtime DTO writes, telemetry cursor/ring reads, scratch locking, and black-box file output. That made ordinary visual sync a global memory choke point.

Solution: Split the path into separate lock windows. Upload locks now cover only `VisualPressureAgingParams` and `UberNoirInstanceDegradation` source resolution plus the required GPU copy while the source is pinned. Runtime DTO update has its own `VisualPressureAgingRuntime` lock. Shader globals run after the runtime lock is released. Fault dump staging uses `stackalloc Span<byte>` after the runtime lock, then telemetry helpers lock one cursor or ring at a time and release in `finally` before file writes.

Rejected Alternatives: Keeping file dump under `VisualPressureAgingCsvScratch` lock was rejected because file IO does not require vault ownership. Adding a persistent `NativeArray<byte>` staging field was initially attempted, then rejected because Memory Sovereignty forbids persistent native aliases in runtime managers. Copying from vault arrays after unlock was rejected because it would read relocatable native memory without a pin. Adding a managed `byte[]` was rejected because it violates Zero-GC.

Scalability potential: Low devices skip visual upload on contention and retain previous GPU buffers. Middle devices get shorter vault pin windows and stable cadence. High devices keep smooth `GlobalQualityWeight` blend and can push more aging/degradation payload. Ultra devices keep visual overkill in shader/detail density, not longer vault locks. No binary `if(isLowEnd)` switch was added.

Hardware Impact: Static ledger changed from VisualPressure top candidate at old lines `842/852/863` to no VisualPressure entry in the global top 20. Latest project ledger: lockInvocationCount `1343`, tryLockBufferCount `686`, insideLoopCount `350`, nestedLockCount `51`, scan `39675346` us. Exact runtime microseconds remain unmeasured; Unity Profiler/PlayMode not run.

Evidence: Current lock lines: params `834`, degradation `838`, upload finally `870-875`, runtime `894`, runtime finally `919-922`, shader globals start `926`, stackalloc fault staging `934`, file write call `939`. Secured BufferIDs: `VisualPressureAgingParams=71240`, `VisualPressureAgingRuntime=71241`, `VisualPressureAgingTelemetryRing=71242`, `VisualPressureAgingTelemetryCursor=71243`, `UberNoirInstanceDegradation=71247`, `UberNoirDegradationTelemetryRing=71248`, `UberNoirDegradationTelemetryCursor=71249`. APEX generatedUtc `2026-05-28T14:17:10Z`; totalForbiddenHits `0`; VisualPressure scanned windows all forbiddenHitCount `0`. Build proof is blocked: one permitted build failed before source compile with MSB4006 Unity editor project-reference cycle; forensic dump `Docs/AgentLogs/Dump_1413_Loop27_BuildFailure_20260528T134842Z.txt`; final APEX sample CPU `67.770575`, `dotnetCount=1`, `cscCount=0`. Final report hashes: optimization `361c50a9d2f0413be52e47fc001db51f523afd5728c27bef137e67d280d237c4`, APEX `c560747860d1bbdc94cd4f0e8727e201e5494411339535609f619e5ebb4b2c27`.

## R31 - Shinobu StormPropagation Dump Lock Split

Problem: `ShinobuStormPropagationRuntime.TryDumpTelemetryToDisk` held storm telemetry, cursor, and dump-scratch vault ownership through black-box dump file work: path construction, directory creation, `FileStream`, `FileInfo`, and atomic file replace/move. That made a fault-reporting path extend GlobalDataVault pin lifetime into managed IO. The refreshed ledger also identified `PersistentWorldRegistry.cs:1603/1521/1692` as higher priority, but those spans are open-addressing map probe/mutation windows where naive extraction can break key uniqueness.

Solution: Split the storm dump into a locked snapshot copy and a cold disk writer. `TryCopyTelemetryDumpSnapshot` locks only `ShinobuStormPropagationTelemetryRing=71715` and `ShinobuStormPropagationTelemetryCursor=71716`, copies the 32-byte header plus 300 x 64-byte telemetry entries into cold preallocated managed scratch, then releases both buffers in `finally`. `TryDumpTelemetryToDisk` calls the disk writer only after the copy helper returns. Removed `_dumpScratchHandle` and runtime use of `ShinobuStormPropagationDumpScratch=71720`.

Rejected Alternatives: Rejected stackalloc for 19,232 bytes because that is beyond the project hot-path stack budget. Rejected persistent `NativeArray<byte>` scratch in the runtime manager because it creates another native ownership lane outside the vault. Rejected reading telemetry arrays after unlock because vault-backed native memory can relocate. Rejected helper-only surgery in `PersistentWorldRegistry` because open-addressing correctness needs a mutation/version token before preprobe extraction is safe.

Scalability potential: Low devices now avoid holding vault locks during fault-path filesystem work and can fail closed faster under contention. Middle devices keep the same telemetry truth while reducing pin duration. High devices can spend the saved contention budget on richer storm presentation. Ultra devices keep visual overkill in storm rendering/cadence, not in longer memory locks. `GlobalQualityWeight` already scales storm update cadence via `SampleGlobalQualityWeightForTick`; this patch does not add binary `if(isLowEnd)` routing and does not scale memory truth.

Hardware Impact: Static ledger moved the old Storm dump candidate off the global top 20; the highest Storm candidate is now priority `194`, and the new dump copy candidate is priority `166`. Project ledger after the edit: lockInvocationCount `1343`, tryLockBufferCount `687`, insideLoopCount `349`, nestedLockCount `51`, scan `26033764` us. Runtime microseconds are unmeasured because Unity Profiler/PlayMode was not run.

Evidence: Storm lock lines: telemetry ring `1062`, cursor `1068`; `finally` `1115`; unlocks `1117-1118`; snapshot copy call `1051`; disk write call `1054`; cold writer start `1122`; cold scratch allocation `539`. APEX generatedUtc `2026-05-28T14:55:02Z`; `totalForbiddenHits=0`; `diskWriteAfterVaultUnlock=true`; `diskWriterContainsVaultLock=false`. Final build gate sample blocked compile: CPU `95.801902`, `dotnetCount=2`, `cscCount=1`, no build launched. Final hashes: Storm runtime `65982f6e79066e4be24f7c66d1b10bcc7cf23659a8ff451297de17173046f90d`; verifier `2864e750dc783117dab52d4443626e17805f84452a8e1ee9e7cce3dbfc395d9c`; ledger `1f9ce8f4146020a787c559a9c10e2cdd4fd82942375b964d13ef905fc3461bf5`; optimization report `7a690485cb552d2ef9a02f34243e2a32dc8e1ccb102db1042d01a76c74f0ab58`; APEX JSON `57b4e86d019930b584cf05eca33136665b762a17d452f3d3a50b265a8f4d5f4a`.

## R32 - PersistentWorldRegistry Fail-Closed Clear Guard

Problem: `VaultBackedHashMap<TKey,TValue>.Clear()` acquired the states buffer, then attempted the count buffer, but cleared every state slot before proving the count lock and count length. Under count-lock contention, the method could return `false` after mutating states, leaving `count[0]` stale/nonzero. That is a fail-closed violation: a rejected clear must not partially commit map truth.

Solution: Moved the count-lock/length failure check ahead of the state-clear loop. Current order is states lock at line `1437`, count lock at line `1444`, count guard at lines `1445-1446`, state clear loop at line `1449`, count reset at line `1452`, and both releases in `finally` lines `1455-1459`.

Rejected Alternatives: Rewriting `TrySet`, `TryAdd`, and `Remove` probe loops was rejected in this loop because their open-addressing windows maintain key uniqueness and tombstone/count invariants. Pre-probing outside the lock without a mutation/version token can create duplicate-key or stale-write races. Leaving the speculative clear was rejected because it creates a false return after mutation.

Scalability potential: Low devices avoid a stale-count world-state map after vault contention. Middle devices keep the same deterministic clear behavior. High and Ultra devices preserve memory truth while spending saved contention budget in presentation systems, not longer registry locks. `GlobalQualityWeight` is intentionally not used because clear semantics are authority truth and must not scale by quality.

Hardware Impact: Adds one scalar branch before an O(capacity) clear loop. No allocation, no wait, no file IO, no new public API. Runtime microseconds are unmeasured; static benefit is removal of one partial-commit failure mode under count-lock contention.

Evidence: APEX generatedUtc `2026-05-28T15:15:18Z`; `PersistentWorldRegistry.VaultBackedHashMap.Clear` lines `1432-1461`, forbiddenHitCount `0`; APEX `totalForbiddenHits=0`; `countGuardBeforeStateClear=true`; `releaseInsideFinally=true`; `partialClearOnCountLockFailureRemoved=true`. Secured state/count BufferIDs: `74459/74460`, `74475/74476`, `74481/74482`, `74495/74496`, `74499/74500`, `74503/74504`, `74507/74508`, `74511/74512`. Build launched only after CPU `39.251238`, `dotnetCount=0`, `cscCount=0`; command `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1 /p:BuildProjectReferences=false`; result `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:53.22`. `git diff --check` still fails on unrelated `Docs/Tasks/_1415_extracted_prompt.tmp.md` trailing whitespace. Final source/report hashes before status-log append: `PersistentWorldRegistry.cs` = `46177e908133046d00bbc32319be4f1db8f5e1c87661bd04df2dfc942bfcfdaa`; verifier = `2e9ed57cae8fee6347ba9bd830823a818aefe96e32e2018c3c8cdce56305ed46`; ledger = `f1108442a7fe5ed84754d1e2a2f4f847d9ce5f4fbf0c2c3c68a81bfd8ba10ddb`; optimization report = `3ce5a6083462820ef19ab92d5492cc8a657fcb31f5b96a8e20f72b720e5e0a1b`; APEX JSON = `56112ad7619d3922aa492200315e91d42d73321d5e786eaa8b2a5a9783b66442`.

## R33 - Deferred Release Enqueue Spin Reality Recheck

Problem: Current-byte self-audit contradicted the previous no-spin report. `GlobalDataVault.cs` still contained `_deferredReleaseEnqueueGate`, `DeferredReleaseEnqueueSpinWait`, and `Thread.SpinWait(DeferredReleaseEnqueueSpinWait)` inside `QueueDeferredRelease`. That made the reported fail-closed release queue evidence false for the bytes on disk.

Solution: Removed the private enqueue gate, spin constant, initialization/reset writes, and serializer `try/finally`. `QueueDeferredRelease` now scans pending writer releases and then claims a fixed ring slot with `Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)`. APEX verifier now records exact wait-primitive line hits for the queue and the full `GlobalDataVault.cs` file.

Rejected Alternatives: Keeping a short spin wait was rejected because release enqueue is itself the recovery path for a failed release mutation gate; it cannot create another wait point. Reintroducing a blocking serializer was rejected for the same reason. Adding managed retry tracking or a managed set was rejected because it would allocate and add shared managed state under contention.

Scalability potential: Low devices avoid release-path CPU burn when the vault is already under contention. Middle devices keep bounded fixed-ring enqueue work. High and Ultra devices preserve memory truth and can spend recovered CPU outside the vault. `GlobalQualityWeight` is intentionally not consumed because release ownership, active-lock bits, and DTO layout are authority facts, not scalable visual fidelity.

Hardware Impact: Removes one private spin-wait loop from the contended release enqueue path. Normal path remains scalar and allocation-free. Runtime microseconds are unmeasured; compile proof only verifies syntax/source compilation, not Unity profiler timing.

Evidence: `QueueDeferredRelease` lines `2030-2090`; APEX generatedUtc `2026-05-28T16:06:25Z`; APEX `totalForbiddenHits=0`; queue wait primitive lines are empty for `Thread.SpinWait`, `Thread.Sleep`, `Task.Delay`, and `.Wait(`; full `GlobalDataVault.cs` `Thread.SpinWait` lines empty; dispose-only `Thread.Sleep` remains at line `3647`, outside write-lock hot path. Build launched only after sample CPU `38`, `dotnetCount=0`, `cscCount=0`, `VBCSCompilerCount=0`; command `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1 /p:BuildProjectReferences=false`; result `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:41.24`. Hashes: `GlobalDataVault.cs` = `369f2809fecd985eb463a82bb8a5fb8aa1cfbb2bc4d598513b30076c04d78b1c`; verifier = `25a34611572bc173de42b6717b4546537cd571717401d56d7701c832d5907484`; ledger = `88f49180581af8ce113b5d3b2d2b496546d2574491757f9ebe19dc9f95c5c517`; optimization report = `7e574ff56451ce76a0d6e6034acbf81d12b3e46222455838c6c7d683c48fda7b`; APEX JSON = `b2e3124e3d673ad6249098484c1aa93e1ef75f59ca57e1ca45c0fd891afe3c07`.

## R34 - Deferred Writer Accepted-Return Recheck

Problem: Current-byte self-audit found `ReleaseWriteLock<T>` and private `ReleaseWriterBlockLock` again discarding the result of `QueueDeferredWriterRelease(...)` and returning `false` when `TryEnterReleaseMutationGate()` failed. That is a contract defect: if the deferred writer release was accepted, returning false leaves compliant callers free to retry an already transferred release.

Solution: Restored both contended writer-release branches to `return QueueDeferredWriterRelease(...)`. APEX now explicitly verifies the accepted-return shape and reports `ignoredDeferredWriterReleaseLines=[]`.

Rejected Alternatives: Leaving `return false` was rejected because it contradicts the deferred-release ownership-transfer contract. Adding spin/retry around `TryEnterReleaseMutationGate()` was rejected because fail-closed release must not wait. Adding a new tokenized public release API was rejected for this loop because current source already has a fixed-ring deferred-release contract and the byte-level regression was local.

Scalability potential: Low devices get no retry storm from accepted deferred writer releases. Middle devices keep bounded fixed-ring release work. High and Ultra devices keep the same memory authority and spend visual budget outside the vault. `GlobalQualityWeight` is intentionally not consumed because lock ownership, DTO layout, active lock bits, and release responsibility are authority truth, not visual fidelity.

Hardware Impact: Normal release path unchanged. Contended writer-release path now returns one scalar queue result instead of discarding it; no managed allocation, no file IO, no wait primitive, no public API change. Runtime microseconds are unmeasured; the measurable static effect is removal of one false-negative release result under contention.

Evidence: Final current bytes are blocked by concurrent overwrite. `GlobalDataVault.cs:1935` and `GlobalDataVault.cs:1967` still return `QueueDeferredWriterRelease(...)`, and writer-only duplicate suppression remains at line `2055`, but `_deferredReleaseEnqueueGate` and `Thread.SpinWait(8)` are present again at lines `541/810/2047-2048/2100/3566`. APEX generatedUtc `2026-05-28T16:52:00Z`; `zeroGcTextScan.totalForbiddenHits=0`; `releaseWriteLockReturnsAcceptedDeferredWriterRelease=true`; `internalWriterReleaseReturnsAcceptedDeferredWriterRelease=true`; `hasWriterOnlyFilter=true`; `hasNoEnqueueGateBusyFailPath=false`. Current source SHA-256 = `5d3cfe4c916fa9547a313920aba8ce6d7ef4275ed3e26dfc00145a3b5fc2c4f1`; optimization report = `a243cad370cb546bce1f3923d948c0cc532d13f0450cb427de97a8531cf04f54`; APEX JSON = `e7f1cfc5e920434734bc8b90846421c8458efea278df5694bcc54724d2828764`; forensic dump = `Docs/AgentLogs/Dump_1413_ConcurrentOverwrite_20260528T165050Z.txt` with SHA-256 `8efb82d41fae7d217f033e59f8adf3e0ca366da773248df4e3a9199f8043b957`.

## R35 - Non-Blocking Deferred Release Gate Reconciliation

Problem: Three source contracts conflicted. 1413 fail-closed policy rejects wait primitives in the release recovery path. 1414 editor test and 1404 validators require `_deferredReleaseEnqueueGate`, exact `if (kind == DeferredReleaseKindWriter)`, `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)`, `finally`, and `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)`. Pure gate removal passed the no-wait audit but would break those existing tests/validators.

Solution: Kept `_deferredReleaseEnqueueGate` as a non-blocking best-effort writer duplicate guard. `QueueDeferredRelease` now performs one CAS into `enqueueGateAcquired`; it never loops and never calls `Thread.SpinWait`. The exact writer-only branch remains for the tests. If the gate is acquired, writer duplicate scan runs under it and the gate is released in `finally`. If the gate is busy, enqueue continues to the fixed native ring slot CAS without waiting. `DrainDeferredWriterReleaseLocked` now clears already-unlocked ownerless duplicate writer requests when `meta.ActiveWriterSystemID == 0`, preventing a duplicate ownerless request from occupying the ring forever.

Rejected Alternatives: Deleting the gate was rejected because it breaks 1414/1404 source contracts already present in tests and validators. Keeping `while (...) Thread.SpinWait(8)` was rejected because release recovery must not burn CPU waiting on a secondary gate. Returning false when the gate is busy was rejected because it would reintroduce accepted-release ambiguity under contention.

Scalability potential: Low devices avoid release-path spin. Middle devices keep the source-level compatibility contract. High and Ultra devices preserve memory truth and spend saved CPU outside GlobalDataVault. `GlobalQualityWeight` is not consumed because lock ownership and release responsibility are authority truth, not scalable visual fidelity.

Hardware Impact: Replaces an unbounded spin loop with one CAS and one branch. Runtime microseconds remain unmeasured; Unity Profiler/PlayMode were not run. Static hot-path text scan for lines `1919-2215` returned zero hits for reference `new`, `string.Format`, `.ToString(`, LINQ `Select/Where/Any/All`, `foreach`, `GlobalRegistry.Get<`, and `GetComponent(`.

Evidence: Current `GlobalDataVault.cs` SHA-256 = `d06c63a6cfba238481e68be63c033cc0def222851957aa8dbe41cf132ce260bc`. Stable 10-second re-read kept the same no-spin shape. Relevant lines: gate field `541`, reset `810/3596`, accepted writer release returns `1949/1981`, single CAS `2061`, exact writer branch `2068`, writer-only pending check `2081`, finally release `2111-2118`, ownerless stale duplicate clear `2207-2211`. `rg "Thread\.SpinWait\(" GlobalDataVault.cs` returned no hits. `git diff` added-line dependency scan returned `PATCH_ADDED_DEP_LOOKUPS => 0`. `git diff --check -- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` returned only CRLF normalization warning. No `dotnet build` launched because latest CPU sample showed `cpu=100`.

## R36 - APEX Integrator Reconciliation

Problem: Current-byte stability failed once after Loop 32: `GlobalDataVault.cs` reverted to bad hash `F50DF9D3665DB970E0A2E718A465CB31E5EB00208835F80ED4D9EAE7B5E0C325`, with `_ = QueueDeferredWriterRelease(...)`, `writerGateAcquired`, and busy-fail enqueue gate. 1404/1414/static validators also had drifted contract text.

Solution: Re-applied the source-level contract that preserves accepted deferred writer-release ownership transfer and removes release-path waiting. `ReleaseWriteLock<T>` and `ReleaseWriterBlockLock` return `QueueDeferredWriterRelease(...)`. `QueueDeferredRelease` uses a writer-only best-effort CAS into `enqueueGateAcquired`, skips duplicate scan when the gate is busy, and continues to fixed-ring CAS enqueue without waiting. Contract tests/validators now assert this shape.

Rejected Alternatives: Blocking on `_deferredReleaseEnqueueGate` was rejected because release recovery cannot introduce a secondary wait. Returning false when the gate is busy was rejected because it recreates false-negative accepted release semantics. Unconditional gate CAS for buffer-pin releases was rejected because buffer-pin release does not need writer duplicate scanning.

Scalability potential: Low devices avoid spin and retry storms during memory contention. Middle devices keep deterministic bounded ring behavior. High and Ultra devices preserve memory truth and spend recovered frame budget outside GlobalDataVault. `GlobalQualityWeight` is intentionally not used because lock release ownership is authority truth, not visual fidelity.

Hardware Impact: Contended writer-release now does one queue attempt and returns the queue result. Enqueue gate contention no longer fails or spins. No managed allocation, no new public API, no file IO in the hot path.

Evidence: Stable 20-second re-read kept `GlobalDataVault.cs` SHA-256 `D89D1C9D0E88A46AC0A7FAB32E7FA12ED5BB7202C8937B71FFB612576F5456BB`. Current lines: accepted returns `1949/1981`, writer CAS `2064`, duplicate check `2085`, `finally` gate release `2118-2121`, ownerless duplicate drain `2214-2217`. `GDV_RELEASE_RANGE_FORBIDDEN_TOTAL=0`; `PATCH_ADDED_DEP_LOOKUPS=0`; `HOT_DEP_TOTAL=0`. `git diff --check` on touched files returned only CRLF normalization warnings. Roslyn parse-only via Windows PowerShell could not be completed because SDK Roslyn assemblies are not directly loadable into Windows PowerShell in this host. Final build gate sample: CPU `100`, compilerProcessCount `2`, active `dotnet` PID `67136`, active `csc` PID `15916`; no `dotnet build` launched.

## R37 - APEX Integrator Lock Flattening Follow-Up

Problem: Post-reconciliation audit found additional lock-flattening violations outside the `GlobalDataVault` release path. `VisualPressureAgingRuntime.ReloadCsvFromDisk` held `VisualPressureAgingTuning` together with `VisualPressureAgingCsvScratch` through file IO and CSV parsing. `ShinobuStormPropagationRuntime.PublishCompletedState` held four scalar publication locks while acquiring `ShinobuStormPropagationState`. Storm cold import/default setup also held tuning/profile/scratch locks together and ran parsing or sanitize work inside lock windows.

Solution: Visual CSV reload now uses cold managed scratch for file IO before any vault lock, reads tuning in one short window, parses the CSV outside locks, and writes tuning in a second short window. Storm publish now writes state and each scalar lane through isolated one-buffer helper locks. Storm impact CSV import reads into cold managed scratch, parses into `stackalloc Span<StormDepthImpactProfileDTO>`, then copies parsed profiles and tuning metadata in separate lock windows. Storm default rows now read under lock, sanitize/construct fallback outside lock, and write under a new short lock.

Rejected Alternatives: Keeping all scalar lanes atomically locked was rejected because presentation scalar buffers are independent visual outputs and nested scalar/state locks create a deadlock vector. Parsing directly into vault-backed profiles was rejected because CSV tokenization and float parsing do not require memory ownership. Removing scheduled job multi-buffer pins was rejected in this loop because the current job design passes live NativeArrays to scheduled jobs; a correct fix needs dispatcher-owned staging or immutable snapshots, not a local helper rewrite.

Scalability potential: Low devices skip individual visual scalar lanes on contention instead of blocking the publish path. Middle devices keep deterministic state publication and shorter editor import lock windows. High and Ultra devices preserve `GlobalQualityWeight`-driven storm cadence/noise and visual pressure aging density without turning memory locks into visual quality switches. No binary `isLowEnd` branch was added.

Hardware Impact: Runtime publish now acquires at most one new publication lock at a time instead of five nested publication locks. Editor CSV file IO and parse are outside vault locks. Runtime microseconds remain unmeasured because PlayMode/Profiler were not run.

Evidence: Visual lock lines `1746/1759` and `1772/1785`; Storm default rows `593/605`, `614/623`, `635/647`, `656/665`; Storm impact import `716/734` and `740/753`; Storm publish `911/930` and `936/949`; Span parser overload starts `ShinobuStormPropagationContracts.cs:444`. Hot-window forbidden scan total `0`; lock-window sequential model max `1`, endActive `0`; high-frequency dependency scan including `TryGetComponent` returned `HOT_DEP_WITH_TRYGET_TOTAL=0`. Hashes after source edits: VisualPressure `C63B4D3E4E7490C226077FC3235049DFCC611E8234CF2691D942D4F1CEC5311B`; Storm runtime `8E4CAA4CA28073058D316191B8189BEA4F79CF297D38E685FE7BBDAE7E8859CA`; Storm contracts `994E65E0E95622D29B2834DA12E824B53FE35A67635A2A5512EACDA3834D8F09`; GlobalDataVault `FEE907B658D3C66BA177A65760CD4A3AC25705CF2C1824B29700AB51E768487E`. Build gate blocked compilation: CPU `100`, compilerProcessCount `1`, active `VBCSCompiler` PID `53464`; no `dotnet build` launched.

## R38 - Ecosystem Cold Import Sovereignty Flattening

Problem: `ShinobuEcosystemBalancer` still had cold/editor import paths that mutated vault-backed arrays through raw `TryResolveHandle` views. The legacy profile load, ecosystem tuning CSV, swarm species CSV, and spatial-grid profile CSV paths did file IO and parsing against vault scratch or live vault arrays. This is not a scheduled-job lease problem; it is a local data-sovereignty defect with avoidable lock duration.

Solution: Moved file IO into preallocated managed scratch arrays before any vault buffer lock. Added one-buffer helpers for counters, ecosystem tuning, swarm species profiles, spatial-grid tuning, and spatial-grid profiles. Each helper locks one `BufferID`, resolves the handle, performs only a scalar read/write or bounded DTO copy, and releases in `finally`. Added `ReadOnlySpan<byte>` parser overloads so CSV tokenization and float parsing run before vault writes.

Rejected Alternatives: Locking the vault CSV scratch buffer during `FileStream.Read` was rejected because it pins DataVault memory during disk IO. Keeping raw `TryResolveHandle` writes was rejected because compaction/defrag can relocate memory while cold import writes are in progress. Rewriting the 28-lock scheduled job lease was rejected in this loop because current jobs consume live `NativeArray` views for the full `JobHandle` lifetime; unlocking early would be unsafe without snapshots or a vault job-lease API.

Scalability potential: Low devices avoid cold import stalls and memory relocation races during designer reloads. Middle devices keep deterministic import semantics. High and Ultra devices keep continuous `GlobalQualityWeight` scaling in the ecosystem jobs and spatial-grid budget math; no `isLowEnd` branch was added. File/CSV work is treated as cold authoring/import work, not gameplay truth.

Hardware Impact: Removes vault scratch pinning around 8 KB CSV and 64 byte legacy reads. Runtime hot path is unchanged. New lock windows are bounded: scalar tuning/counter writes or copies of `64` swarm species rows and `16` spatial-grid profile rows. Runtime microseconds remain unmeasured because PlayMode/Profiler were not run.

Evidence: New helper windows start at `ShinobuEcosystemBalancer.cs:1314`. Static lock scan over `1314-1571` reported `max=1`, `end=0`. Orchestration ranges `1686-1769`, `1814-1866`, and `1879-1976` reported `max=0`, `end=0`, proving file IO and parse sit outside lock helpers. `SpatialGridProfileCsv.Parse(ReadOnlySpan<byte>, Span<SpatialGridProfileDTO>, out SpatialGridTuningDTO)` starts at `ShinobuSpatialGridSolver.cs:1163`. Patch added-line scan reported zero `GlobalRegistry.Get<`, `GetComponent(`, `TryGetComponent(`, `string.Format`, `.ToString(`, `foreach`, and LINQ `Select/Where/Any/All`. High-frequency dependency scan over touched files reported `HOT_DEP_TOTAL=0`. Hashes: Ecosystem `4B1C5597B88E66F1617155A738C87BA961F302D27F25262530C02C0801C363B2`; SpatialGridSolver `FBEB7C0FA645C45E2E05CEB9FB19516564888BF24CB74BDFDA1FCBAA172AB7E9`; GlobalDataVault `FEE907B658D3C66BA177A65760CD4A3AC25705CF2C1824B29700AB51E768487E`. Final build gate: CPU `100`, compilerProcessCount `0`; no `dotnet build` launched.

## R39 - Storm Scheduled Job Vault Lease Removal

Problem: `ShinobuStormPropagationRuntime` still had the scheduled-job multi-lock lease that Loop 37 marked as unsafe. `TryLockOwnedJobBuffers` pinned weather, tuning, impact profiles, mock weather, write state, telemetry ring, and telemetry cursor for the lifetime of a scheduled `JobHandle`. That created a long-lived deadlock vector and let simulation jobs own DataVault memory beyond the immediate owner phase.

Solution: Replaced the live-vault job lease with owner-local persistent `NativeArray` staging. `TrySnapshotPropagationJobInputs` copies each vault input through one short `TryLockBuffer`/`finally TryUnlockBuffer` window before scheduling. `CalculateStormAttenuationJob` and `GenerateMockHurricaneJob` now operate only on local staging arrays. Completed state/scalars/telemetry are published in `LateFrameTick` after `DispatcherJobFence.TryFinalizeCompleted`, again one buffer at a time. Runtime use of `BufferID.ShinobuStormPropagationWriteState=71713` was removed.

Rejected Alternatives: Unlocking live `NativeArray` views immediately after scheduling was rejected because jobs would keep pointers into vault memory without ownership. Keeping the write-state vault buffer as a job output was rejected because it would still require a long write lock or unsafe unlocked writes. Adding a new global job-lease API was rejected for this local pass because the storm job output is small enough for an owner-local staging route.

Scalability potential: Low devices now fail closed on contention before scheduling and reuse the last published storm presentation instead of blocking behind a multi-buffer lease. Middle devices get bounded snapshot copy cost and no live vault pins across `JobHandle`. High and Ultra devices still spend quality budget through continuous `GlobalQualityWeight` in cadence, noise octaves, surge/fog/biolum scaling, and mock storm generation; no binary `isLowEnd` switch was added.

Hardware Impact: Replaces one long scheduled lease over seven vault buffers with sequential short locks and local arrays: weather `1`, tuning `1`, profiles `ImpactProfileCapacity`, mock `1`, write snapshot `1`, telemetry `1`, cursor `1`. Runtime microseconds remain unmeasured because PlayMode/Profiler were not run. The expected low-end gain is reduced contention duration, not lower math cost.

Evidence: Removed symbols: `TryLockOwnedJobBuffers`, `UnlockOwnedJobBuffers`, `_jobLockMask`, `JobLock*`, `TryLockJobBuffer`, `_writeStateHandle`, and `ShinobuStormPropagationWriteState` references in the runtime. Staging fields are `ShinobuStormPropagationRuntime.cs:59-65`; cold staging allocation/disposal `535-585`; schedule route `811-867`; snapshot helpers `971-1075`; late publish `869-967`; telemetry publish `1086-1139`. Lock scans: `StormSchedulePublishSnapshot 811-1148 max=1 end=0`, `StormTelemetryPublish 1086-1160 max=1 end=0`, `StormDumpSnapshotFull 1162-1239 max=1 end=0`, `StormScheduleOnly 811-887 max=0 end=0`. Hot reference/lookup scan hits `0`. Domain touched hot dependency scan hits `0`. Storm runtime SHA-256 `957F137AAF928DD3EAD8EAA6C2360CB0F725998395937663819CFC7F2891F408`. Final build gate: CPU `99.61`, compilerProcessCount `0`; no `dotnet build` launched.

## R40 - Ecosystem Job Lease Narrowing

Problem: `ShinobuEcosystemBalancer` used one 28-buffer job lock set for both frame flocking and macro biomass work. The macro job did not need boid state snapshots, render buffers, spatial-grid sort scratch, debug cells, flocking threat buffers, spatial telemetry, spatial tuning/profile/csv buffers, or flocking telemetry. Frame jobs also held ecosystem/spatial tuning and spatial profile/csv buffers across `JobHandle` lifetime even though tuning is read-only snapshot data and profile/csv buffers are cold import data.

Solution: Split the lock plan into `TryLockFrameJobBuffers` and `TryLockMacroJobBuffers`. `Tick` now reads ecosystem tuning and spatial-grid tuning through isolated one-buffer locks before taking the frame job lease, then passes sanitized value copies into jobs. `RunMacroBiomassPass` reads ecosystem tuning before the macro lease. Macro completion snapshots counters while the 6 live macro buffers are still pinned, releases those locks, then writes one telemetry row through `WriteMacroTelemetryAndFaultDump` under a single `ShinobuEcosystemTelemetryRing` lock.

Rejected Alternatives: Unlocking live entity/spatial/native arrays immediately after scheduling was rejected because jobs would retain pointers into vault memory without ownership. Full owner-local staging for all ecosystem frame arrays was rejected in this pass because the entity/snapshot/render/spatial arrays are large and require a dispatcher-owned snapshot design, not a local count edit. Keeping one 28-buffer lock set for macro was rejected because it pinned 22 irrelevant buffers for a scheduled macro job.

Scalability potential: Low devices reduce DataVault contention during cold macro passes and avoid keeping cold tuning/profile/csv buffers pinned for frame jobs. Middle devices keep deterministic scheduling with the same continuous `GlobalQualityWeight` budget math. High and Ultra devices still use `GlobalQualityWeight` for visual/simulation density and spend saved contention budget in presentation, not in longer memory locks. No binary `isLowEnd` branch was introduced.

Hardware Impact: Macro scheduled lease drops from 28 locks to 6 locks, plus one post-unlock telemetry lock. Frame scheduled lease drops from 28 locks to 24 locks by removing tuning/profile/csv scratch buffers from the long job lifetime. Runtime microseconds remain unmeasured because CPU policy blocked compilation and PlayMode/Profiler were not run.

Evidence: `TryLockFrameJobBuffers` lines `2483-2550`, 24 lock calls; `TryLockMacroJobBuffers` lines `2551-2582`, 6 lock calls; `WriteMacroTelemetryAndFaultDump` lines `2289-2327`, one lock with `finally` release. `TryReadEcosystemTuning` lines `1400-1422`, `TryReadSpatialGridTuning` lines `1472-1494`, both lock model max `1` and end `0`. Targeted hot-window forbidden scan over `Tick`, `RunMacroBiomassPass`, `FinishFrameJobCompletion`, `TryBuildMacroTelemetryEntry`, `WriteMacroTelemetryAndFaultDump`, `TryLockFrameJobBuffers`, and `TryLockMacroJobBuffers` returned total `0` for dependency lookups, wait primitives, string formatting, ToString, LINQ, and foreach. Struct `new` hits remain only value-type job/DTO initializers. Ecosystem domain hot dependency scan returned `ECOSYSTEM_HOT_DEP_TOTAL=0`. `ShinobuEcosystemBalancer.cs` SHA-256 `E68F800A68D741FC796C321014543E8BAD37AEB980862C0CC3231587877C6ACB`. Final CPU sample `100`, compiler count `0`; no `dotnet build`.

## R41 - Telemetry Ring Lease Split and Dump Lock Decoupling

Problem: Loop 37 still left avoidable presentation/forensic buffers inside scheduled job leases. `ShinobuEcosystemBalancer` held ecosystem and flocking telemetry rings for the entire frame `JobHandle` lifetime, then dumped black boxes while still inside telemetry/job locks. `ShinobuFloraFaunaSymbiosisSolver` held `ShinobuSymbiosisTelemetryRing` with 17 other live buffers and wrote/dumped before unlocking. That makes a fault path perform file IO or snapshot-copy work while DataVault buffers are pinned.

Solution: Split telemetry into two phases. Phase A snapshots counter-derived telemetry structs while live job buffers are still valid. Phase B releases job buffers, then writes telemetry rings under isolated one-buffer locks. Added owner-local persistent telemetry mirrors so dump routines read mirrors after telemetry-ring release instead of holding a vault lock through dump IO/snapshot copy. Ecosystem frame lock plan drops from 24 to 22. Symbiosis job lock plan drops from 18 to 17.

Rejected Alternatives: Unlocking live job arrays before `JobHandle` completion was rejected as a memory race. Calling forensics directly while the telemetry ring remained locked was rejected because ecosystem forensics takes a second dump snapshot lock and flocking/symbiosis dump paths perform file IO. Copying all entity/spatial arrays into owner-local staging was rejected in this loop because the large-array job route needs a broader dispatcher-owned staging design.

Scalability potential: Low devices skip or delay telemetry writes under contention instead of extending live simulation locks. Middle devices keep deterministic telemetry rows and existing black-box capacity. High and Ultra devices preserve `GlobalQualityWeight`-driven visual density and flocking cadence; no binary `isLowEnd` switch was added. The change spends saved contention budget on presentation stability, not extra authority work.

Hardware Impact: Ecosystem frame scheduled lease removes 2 telemetry locks. Symbiosis scheduled lease removes 1 telemetry lock. Fault dump IO is no longer performed while holding telemetry/job locks. Runtime microseconds remain unmeasured because PlayMode/Profiler were not run.

Evidence: `TryBuildFrameTelemetryEntries` `2455-2550` has `locks=0`; `WriteEcosystemTelemetryAndFaultDump` `2371-2417` has `locks=1 unlocks=1`; `WriteFlockingTelemetryAndFaultDump` `168-209` has `locks=1 unlocks=1`; `TryLockFrameJobBuffers` has `FRAME_LOCK_CALLS=22`, `FRAME_RING_LOCK_HITS=0`, `FRAME_UNLOCK_RING_HITS=0`. Symbiosis `TryLockJobBuffers` has `SYMBIOSIS_JOB_LOCK_CALLS=17`, `SYMBIOSIS_JOB_RING_LOCK_HITS=0`, `SYMBIOSIS_UNLOCK_RING_HITS=0`; `FinishFrameJobCompletion` `991-1019` releases in `finally` before `WriteTelemetryAndFaultDump` `1071-1113`. Targeted hot-window scans reported `TARGETED_FORBIDDEN_OR_NEW_TOTAL=0` and `SYMBIOSIS_TARGETED_FORBIDDEN_OR_NEW_TOTAL=0`. AI/Ecosystem hot dependency scan reported `ECOSYSTEM_HOT_DEP_TOTAL=0`. Hashes: Ecosystem `232D8E7100913EAD67EC0E8C240048B7908F6F01428E7080657D5A4472584F84`; Flocking `C3B8AA28DE851F5E37D92591751B976161D9EC94BDEBE305AC1E7DACAFEB2637`; Symbiosis `22A751250EEB3544F182A2EFAEB058CF3A439129F0CC76EF3B5641FA99072DBD`. Final build gate: CPU `100`, compiler process count `0`; no `dotnet build`.

## R42 - Spatial Telemetry, Symbiosis Tuning, and Acoustic Phase Split

Problem: Loop 38 still left spatial-grid telemetry cursor/ring ownership coupled to the frame pipeline, symbiosis tuning coupled to a 17-buffer job lease, and acoustic tap presentation inside the job-completion path before all simulation buffers were released. Symbiosis editor import also still used vault-backed scratch/handles for disk payload staging.

Solution: `BuildSpatialGridRangesJob` now writes one owner-local telemetry row to `_spatialGridTelemetryFrame`; the cursor and telemetry ring are advanced/written after frame job locks release. Symbiosis refreshes tuning through a short one-buffer lock before the scheduled lease, then passes tuning by value into `SymbiosisExchangeKernelJob`. CSV/legacy import uses managed scratch and one-buffer write helpers. Acoustic taps are copied into `_acousticTapPublishScratch` while job buffers are valid and published only after `UnlockJobBuffers`.

Rejected Alternatives: Keeping telemetry cursor mutation inside the spatial range job was rejected because it keeps a diagnostic ring in the live simulation lease. Reading tuning from a live `NativeArray` inside `SymbiosisExchangeKernelJob` was rejected because tuning is scalar authority input and does not require a scheduled write lock. Publishing acoustic signals before unlocking was rejected because it moves presentation work into the simulation ownership window. Unlocking the large entity/flora arrays before `JobHandle` completion was rejected because scheduled jobs would retain stale vault pointers.

Scalability potential: Low devices now fail closed on diagnostic/tuning/acoustic contention instead of extending live simulation locks. Middle devices keep deterministic telemetry and acoustic presentation with zero-GC scratch transfer. High and Ultra devices keep continuous `GlobalQualityWeight` math in ecosystem/flocking/symbiosis cadence and budgets; no binary `isLowEnd` branch was added. The change buys frame stability and presentation capacity, not more authority work.

Hardware Impact: Ecosystem frame lease drops to 20 live buffers after spatial telemetry ring/cursor removal. Symbiosis lease drops to 16 live buffers after tuning and telemetry removal. Disk IO and acoustic presentation are outside job locks. Runtime microseconds remain unmeasured because the single compile check timed out and PlayMode/Profiler were not run.

Evidence: `ShinobuEcosystemBalancer.cs`: owner-local spatial telemetry field `172`, job setup `494/530/535`, telemetry build `2440`, spatial write `2531`, cursor advance `2591`, frame lock `2636`, unlock `2761`. `ShinobuSpatialGridSolver.cs`: `TelemetryOutput` field `906`, write `985`. `ShinobuFloraFaunaSymbiosisSolver.cs`: tuning refresh `228/852`, job lock `1382`, finish `1215`, acoustic scratch build `1340`, publish `1358`. `TARGETED_HOT_FORBIDDEN_TOTAL=0`; `HOT_DEP_TOTAL=0`; `ECOSYSTEM_FRAME_LOCK_CALLS=20`; `SYMBIOSIS_JOB_LOCK_CALLS=16`. Hashes: Ecosystem `87C5511941CA4CC4400E5DD333F2D3A6C05F2E5D3BD907E3379E104B9E6EFB21`; Flocking `C3B8AA28DE851F5E37D92591751B976161D9EC94BDE305AC1E7DACAFEB2637`; SpatialGridSolver `9587450C90042986B054914BD5384CDE044BFC86D17FA1EF78C1A8730EB4328F`; Symbiosis `898C1519BADBD6D80F72B023DE8A42EBA4CC4E6F819B04D6AFE8430621DC7F33`. Compile check: pre-build CPU `33`, compiler count `0`; one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` timed out after 304 seconds. Spawned build PIDs `55948`, respawned `67624`, and child project build `32440` plus compiler server were shut down; final post-cleanup CPU `26`.

## R43 - Read-Only Job Input Lease Pruning

Problem: After Loop 39, two avoidable read-only input classes were still pinned in long scheduled-job leases. `ShinobuFloraFaunaSymbiosisSolver` held ambient fish arrays even though `SymbiosisExchangeKernelJob` only reads them. `ShinobuEcosystemBalancer` held flocking threat arrays even though they are frame-local signal reductions used as input for `BoidFlockingJob`; persistent telemetry uses `FlockingCounters64`, not the threat DTO buffer.

Solution: Added persistent owner-local snapshots for symbiosis ambient fish and ecosystem flocking threats. Symbiosis snapshots `ShinobuAmbientEntities` and `ShinobuAmbientAups` through one-buffer `TryLockBuffer` windows before the job lease, then passes those snapshots to the scheduled job. Ecosystem builds flocking threats directly into `_flockingThreatJobSnapshot` and `_flockingThreatCountJobSnapshot`; `BoidFlockingJob` reads those local arrays while only `ShinobuFlockingCounters64` remains in the frame lease for counter output. Converted `TryAppendFlockingThreat` from struct `new` initializer to `default` plus field writes.

Rejected Alternatives: Publishing local flocking threats back into DataVault was rejected because no downstream runtime owner reads those threat DTO buffers after frame reduction; only counters are consumed for telemetry. Removing live truth-array locks for flora/entity/output buffers was rejected because independent per-buffer publish would create partial-commit risk without a generation stamp or consolidated commit buffer. Unlocking live vault arrays while scheduled jobs still run was rejected as a memory race.

Scalability potential: Low devices now avoid pinning two ambient read buffers and two flocking threat buffers across scheduled jobs; if snapshot locks fail, the systems fail closed before scheduling. Middle devices keep deterministic frame-local threat reduction. High and Ultra devices still scale threat budget through continuous `GlobalQualityWeight` via `ResolveFlockingThreatBudget`, not binary hardware switches.

Hardware Impact: Symbiosis scheduled lease drops from 16 to 14 locks. Ecosystem frame lease drops from 20 to 18 locks. Added cold persistent memory: `5000` ambient entity snapshots, `5000` ambient AUP snapshots, `32` flocking threat snapshots, and `1` flocking threat count snapshot. Runtime microseconds remain unmeasured because CPU policy blocked compilation and PlayMode/Profiler were not run.

Evidence: Symbiosis fields `121-122`, snapshot call `233`, snapshot helper `858-888`, job lock `1479-1527`, unlock `1545-1562`. Ecosystem fields `173-174`, local threat allocation `1231-1237`, disposal `1273-1274`, frame flocking bind `1840-1855`, frame lock `2651-2706`, frame unlock `2772-2793`. Flocking append default path `ShinobuEcosystemBalancer.FlockingAvoidance.cs:109`. Counts: `AMBIENT_SNAPSHOT_HELPER_LOCKS=1 UNLOCKS=1`; `SYMBIOSIS_JOB_LOCK_CALLS=14 AMBIENT_HITS=0`; `ECOSYSTEM_FRAME_LOCKS=18 THREAT_HITS=0`; `ECOSYSTEM_FRAME_UNLOCKS=18 THREAT_HITS=0`; `HOT_DEP_TOTAL=0`; `TARGETED_HOT_FORBIDDEN_TOTAL=0`. Hashes: Ecosystem `FB6C3C927D49268CF9BE9BBE00857835D8B960D7F01A2C38B7ACBC3A22EBAF67`; Flocking `5B14BDC0FB2E2F76E4FBACD8CADDCD2152DA6A43587DB5D924CE6C545CF9E98E`; Symbiosis `D81C5B73AC402F6FA3590C620A1D2EFFD43128C1B37409C871DC115E14C66582`. Final compile gate: CPU `100`, compiler process count `0`; no `dotnet build` launched.

## R44 - Macro Hot Struct Cleanup and Optional Debug Lease

Problem: The self-audit still found value-type constructor tokens in macro biomass and rehydrate hot windows. They are not managed allocations, but they weaken the strict text proof required by the Zero-GC gate. The frame job lease also still pinned `ShinobuSpatialHashDebugCells` every frame even when debug grid presentation was disabled.

Solution: Replaced remaining hot value-type `new` tokens with `default` field assignment or `math.float3/math.double3/math.int3`. Moved `ShinobuSpatialHashDebugCells` to an optional final frame-lock slot keyed by the sanitized tuning flag before scheduling. `TryResolveFrameJobBuffers` now opens the debug-cell view only when that optional lock is requested.

Rejected Alternatives: Keeping value-type `new` was technically valid but rejected because the user asked for exact text-scan proof. Locking debug cells unconditionally was rejected because debug-grid output is optional presentation/debug data, not required frame truth. Removing live entity/spatial/render locks was rejected in this loop because those arrays are still direct scheduled-job inputs/outputs and require a versioned staging or commit protocol to avoid memory races.

Scalability potential: Low devices avoid one avoidable debug buffer pin on normal frames. Middle devices keep deterministic debug output when enabled. High and Ultra devices still scale spatial/flocking work continuously through `GlobalQualityWeight` and can spend recovered contention budget on presentation density; no binary hardware branch was added.

Hardware Impact: Normal ecosystem frame lease drops from `18` to `17` DataVault locks. Debug frames remain `18` with the debug lock last and first to release. Macro hot windows now have `MACRO_TARGET_FORBIDDEN_TOTAL=0` and targeted hot windows have `TARGETED_HOT_FORBIDDEN_TOTAL=0`. Runtime microseconds remain unmeasured because the single throttled compile check timed out after `604` seconds.

Evidence: `frameDebugGridRequested` is computed before the frame lock at `ShinobuEcosystemBalancer.cs:400`; `TryLockJobBuffers` receives it at `407`; `TryResolveFrameJobBuffers` takes it at `412/1695`; optional debug lock is at `2701-2705`; reverse unlock begins with debug at `2787`. Counts: `FRAME_LOCK_CALLS_BASE=17`, `FRAME_LOCK_CALLS_OPTIONAL_DEBUG=1`, `HOT_DEP_TOTAL=0`, `TARGETED_HOT_FORBIDDEN_TOTAL=0`, Ecosystem braces/preproc `581/581`, `8/8`. Hashes: Ecosystem `D19A095AC76F13B0C95C03DFC14ACACA8C395BE6E73EB523B6B19EBA3C915E12`; Flocking `5B14BDC0FB2E2F76E4FBACD8CADDCD2152DA6A43587DB5D924CE6C545CF9E98E`; Symbiosis `F8A4DAC249CD82495C5D6D3ABF3E86F7744324EDC3E248D531633BD5706B0459`. Compile gate was legal (`CPU=43`, compiler process count `0`), one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was launched, timed out after `604` seconds, and the spawned `dotnet` PID `39956` plus `VBCSCompiler` PID `61228` were stopped. Final compiler process scan returned none; final CPU sample was `100`.

## R45 - Phase-Safe Telemetry Release and Failure Ring Locking

Problem: Current-source re-audit found a contradiction in the ecosystem/symbiosis phase split. `FinishFrameJobCompletion` built telemetry under job pins, but also wrote telemetry rings and published acoustic/flocking presentation before `UnlockActiveJobBuffers`. `ClearSpatialGridRangeTable` and `ShinobuSpatialGridSolver.RecordQueryFailure` still mutated vault buffers through raw resolved views without a local lock/finally window.

Solution: Kept live job-output reads under existing job pins, copied results into local value structs/scratch, then moved telemetry writes, flocking dispersal signals, and acoustic signals after the `finally` release. Added one-buffer lock/finally protection to `ClearSpatialGridRangeTable`. Split spatial-grid failure telemetry into cursor lock and ring lock so failure diagnostics never hold two telemetry buffers simultaneously.

Rejected Alternatives: Unlocking live job arrays before reading job outputs was rejected because DataVault compaction can relocate memory after release. Keeping telemetry writes under job pins was rejected because diagnostic/presentation buffers are not simulation truth and should not extend long leases. Full owner-local staging for all ecosystem/symbiosis truth arrays was rejected in this loop because it needs a versioned commit protocol or vault job-lease API to avoid partial commits.

Scalability potential: Low devices now release simulation pins before diagnostic/presentation work, so telemetry contention does not stretch simulation ownership windows. Middle devices keep deterministic telemetry and signals. High and Ultra devices keep continuous `GlobalQualityWeight` math for budgets/cadence; no binary `isLowEnd` switch was introduced.

Hardware Impact: Removes nested telemetry/acoustic presentation from long ecosystem and symbiosis job leases. Adds two short locks on rare spatial-grid query failure and one cold clear lock. Runtime microseconds remain unmeasured because compilation/profiling were blocked by active compiler processes.

Evidence: Ecosystem release-before-write `ShinobuEcosystemBalancer.cs:2461-2534`, unlock `2513`, writes/signals `2519-2533`; symbiosis release-before-publish `ShinobuFloraFaunaSymbiosisSolver.cs:1408-1437`, unlock `1432`; spatial failure cursor/ring locks `ShinobuSpatialGridSolver.cs:653/677/680/717`; range clear lock `ShinobuEcosystemBalancer.cs:3224-3244`. `TARGETED_FORBIDDEN_HITS_TOTAL=0`; `HOT_METHOD_DEP_TOTAL=0`; lock scans: `EcosystemTelemetryWriters locks=3 unlocks=3 max=1 end=0`, `SpatialRecordQueryFailure locks=2 unlocks=2 max=1 end=0`. Hashes: Ecosystem `485B440DD1AAB9AA8C13BF4AEBD8A2415BFEC1F8C4330C0099411AB6C27FCFC4`; Flocking `193D737D43F0E012BC7BE953876FA5890BC4AC6B448BD7B0695563D64C95AE69`; Symbiosis `5619ADAC27E5DA66FEA2EB748024C1A61D3AF8C843B9A89A127F1989E3C073B5`; SpatialGridSolver `AD2C860C4DA41CFCD5F2381BF0BAE63BCDEBB67E05FC3E6CD04AD6AFD9B89E35`. Final build gate: CPU `50`, active `csc` PID `28592`, active `dotnet` PID `44644`; no `dotnet build` launched.

## R46 - Symbiosis Owner-Local Job Staging

Problem: `ShinobuFloraFaunaSymbiosisSolver` still pinned 14 base DataVault buffers plus optional anomaly for the lifetime of a scheduled `JobHandle`. This was pointer-safe but architecturally wrong: a cold symbiosis job could block defrag and sibling writers for its full execution window. Previous telemetry/acoustic release work did not remove the core scheduled array lease.

Solution: Replaced the live-vault job lease with persistent owner-local staging. `ColdTick` snapshots required inputs with one-buffer lock/finally windows, clears output-only local buffers without a vault lock, schedules jobs against local `NativeArray` storage, then publishes completed local outputs in `LateFrameTick` after fence finalization. The publish route writes `ShinobuSymbiosisCounters` last so `Frame` remains the completed-commit marker for readers. Snapshot and publish helpers use `UnsafeUtility.MemCpy`/`MemClear` to keep each single-buffer pin to raw memory transfer only.

Rejected Alternatives: Unlocking live DataVault arrays immediately after scheduling was rejected as a use-after-relocation race. Holding all locks until publish completion was rejected because it preserves the deadlock vector. Atomic all-buffer commit was rejected because current DataVault has no multi-buffer transaction API; counter-last commit is the least invasive fail-closed route. Copying output-only buffers from DataVault before scheduling was rejected because exchanges, scanner VFX, oxygen emitters, adherence, seeds, and acoustic taps are fully regenerated from the local counter state each run.

Scalability potential: Low devices no longer hold symbiosis vault ownership across jobs; contention skips the current cold tick or publish instead of blocking. Middle devices pay bounded linear memory copies. High and Ultra devices still scale work continuously through `GlobalQualityWeight`: quality drives micro-exchange dither, flora/macro stride, ambient stride, and neighbor sample count. No binary `isLowEnd` branch was added. This is a data-route refactor, not a new physical simulation.

Hardware Impact: Scheduled symbiosis job lock depth drops from 14 plus optional anomaly to 0. Snapshot/publish lock depth is 1. Copy cost remains real: flora and AUP staging are large, but the lock window is a contiguous `MemCpy`, not arbitrary Burst job lifetime. Runtime microseconds remain unmeasured because the build/profiler gate was blocked.

Evidence: `ShinobuFloraFaunaSymbiosisSolver.cs` lines: `ColdTick 225`, `LateFrameTick 390`, `EnsureLocalBuffersCold 638`, `TryBindJobBuffers 798`, `TrySnapshotJobBuffersFromVault 878`, `TryPublishJobBuffersToVault 975`, `TryPublishSnapshotToVault 999`, `TryCopyVaultBufferToSnapshot 1037`, `TryRefreshAuthorityTuning 1134`, `FinishFrameJobCompletion 1515`. Static scans: `OLD_LONG_LOCK_SYMBOLS=0`; `TARGETED_FORBIDDEN_TOTAL=0`; `HOT_DEP_TOTAL=0`; lock ranges max depth `1`, end `0`; braces/preprocessor `323/323`, `10/10`; SHA-256 `8E8D703DF2A37E7E3F97DA609A700F5EBB1DB7966CDA810126FB94BDDCF6EA3C`. Build gate blocked: `CPU_SAMPLE=51.19`, compiler process count `1`, active `dotnet` PID `57560`. Open risk remains in `ShinobuEcosystemBalancer.TryLockFrameJobBuffers` and `TryLockMacroJobBuffers`.

## R47 - Ecosystem Presentation Counter Lease Pruning

Problem: Current ecosystem frame jobs still pinned avoidable presentation/telemetry buffers for the whole scheduled `JobHandle`: `ShinobuBoidIndirectArgs`, `ShinobuEcosystemSectors`, and `ShinobuFlockingCounters64`. Indirect args are render presentation state. Flocking counters are output telemetry. Sector dehydration count changes only in the macro job but frame jobs were holding the sector table only to count flags.

Solution: Removed the scheduled indirect-args writer and now builds `BoidIndirectArgsDTO` as a value for GPU upload while render matrices/custom data are still safely pinned. `ShinobuBoidIndirectArgs` is written after frame unlock with one `TryLockBuffer`/`finally` window. Removed `ShinobuEcosystemSectors` from frame resolution/lock plans; frame counter clear preserves the latest dehydrated-sector count, and `LotkaVolterraMacroJob` writes that counter after its own sector mutations. Added a small cold owner-local `_flockingCounterJobScratch` for frame flocking counters and publish it after unlock through one short lock.

Rejected Alternatives: Moving GPU upload after releasing render matrices was rejected because the vault may relocate the matrix/custom-data buffers. Keeping sector counting in the frame job was rejected because it extends every frame lease for data only the macro job mutates. Full ecosystem owner-local staging was not attempted in this loop because it would require large truth-array staging and a versioned commit route; doing it halfway would create partial-frame state.

Scalability potential: Low devices now skip three scheduled frame pins and keep failed post-release publishes fail-closed. Middle devices keep deterministic telemetry through preserved macro-owned counters. High and Ultra keep continuous `GlobalQualityWeight` paths for flocking threat budget, neighbor samples, spatial probes, and render density; no binary hardware switch was added.

Hardware Impact: Normal ecosystem frame scheduled lease drops from 17 base pins to 14 base pins. Debug frames are 15 pins because `ShinobuSpatialHashDebugCells` remains optional. Added cold persistent native scratch cost is 8 `FlockingCounter64` rows. Runtime microseconds remain unmeasured; no build/profiler run was executed.

Evidence: `TryLockFrameJobBuffers LOCKS=15`, `UnlockLockedFrameJobBuffers UNLOCKS=15`, `INDIRECT=0`, `SECTORS=0`, `FLOCKING_COUNTERS=0`; macro remains `LOCKS=6`, `UNLOCKS=6`. `WriteFrameIndirectArgsAfterRelease` and `WriteFlockingCountersAfterRelease` each have `LOCKS=1`, `UNLOCKS=1`. `TARGETED_FORBIDDEN_TOTAL=0`, `HOT_DEP_TOTAL=0`, Ecosystem braces/preprocessor `582/582`, `8/8`, Flocking `46/46`, `0/0`. Hashes: Ecosystem `7B30C7792E4641F7E4C7989C56F8746488A1C0A9E2030FB2B56DD707F36868B3`; Flocking `DD965B313CCF4576C3A6FDE07F09A388CF1AF45961B75419CA51B5E98E4586D5`. Final build gate: CPU `31.14`, compiler count `0`; no `dotnet build` by latest static-validation mandate and because prior throttled builds timed out.

## R48 - Ecosystem Render Payload Lease Split

Problem: `ShinobuEcosystemBalancer` still pinned `ShinobuRenderMatrices` and `ShinobuRenderCustomData` for the full scheduled frame job. Those buffers are presentation payload, not gameplay truth. Keeping them in the scheduled lease made defrag and sibling writers wait on GPU-facing data even though the render job can write owner-local scratch and publish afterward.

Solution: Added `_renderMatrixJobScratch` and `_renderCustomDataJobScratch` as cold persistent `NativeArray` storage. `BuildShinobuRenderPayloadJob` now writes scratch arrays. `FinishFrameJobCompletion` releases DataVault frame locks first, then uploads GPU data from scratch and publishes the DataVault presentation payload through short sequential locks. `WriteRenderPayloadAfterRelease` writes indirect args to zero before payload copy and writes the final indirect args only after both matrix and custom-data copies succeed; a failed publish leaves DataVault draw count at zero instead of exposing mixed payload bytes.

Rejected Alternatives: Keeping GPU upload inside the frame lock was rejected because it extends simulation ownership with presentation work. Publishing matrices/custom data without zeroing indirect args first was rejected because a fail-closed break after one payload copy can expose partial presentation state to DataVault readers. Moving `ShinobuEcosystemCounters` to scratch in this loop was rejected after dependency audit: cold counter writes can legally occur while the frame job is scheduled, and a blind post-release counter publish could overwrite those writes unless a per-index merge or versioned commit route is added.

Scalability potential: Low devices skip two scheduled render-payload pins and fail closed to zero DataVault draw instances on publish contention. Middle devices get deterministic scratch-to-GPU upload after simulation settles. High and Ultra still use continuous `GlobalQualityWeight` for render density and flocking/spatial budgets; no binary `isLowEnd` branch was added. This is a cinematic-cheat-aligned data-route refactor: render payload is a cheap visual projection of simulation truth, not an extra physical simulation.

Hardware Impact: Normal ecosystem frame scheduled lease drops from 14 base pins to 12 base pins; optional debug remains one extra pin. New post-release publish uses at most one DataVault lock at a time and copies contiguous unmanaged bytes with `UnsafeUtility.MemCpy`. Extra post-release indirect zero marker adds one small buffer lock but prevents partial-payload presentation. Runtime microseconds remain unmeasured because final CPU sample was `57.36` with active `dotnet` PID `9452`; no build/profiler run was launched.

Evidence: `TryLockFrameJobBuffers LOCKS=13 UNLOCKS=0 RENDER=0 INDIRECT=0 FLOCK=0`; `UnlockLockedFrameJobBuffers LOCKS=0 UNLOCKS=13 RENDER=0 INDIRECT=0 FLOCK=0`; `TryWriteRenderMatricesAfterRelease LOCKS=1 UNLOCKS=1`; `TryWriteRenderCustomDataAfterRelease LOCKS=1 UNLOCKS=1`; `WriteFrameIndirectArgsAfterRelease LOCKS=1 UNLOCKS=1`. Targeted hot scan reports `TARGETED_FORBIDDEN_TOTAL=0`; AI/Ecosystem hot dependency scan reports `HOT_DEP_TOTAL=0`. Braces/preprocessor: Ecosystem `592/592`, `8/8`; Flocking `46/46`, `0/0`. Hashes: Ecosystem `89F76E051B3D7652DF30359871CE0AEF2BD39599BE0EEBF43CC96FD5530E2E4E`; Flocking `DD965B313CCF4576C3A6FDE07F09A388CF1AF45961B75419CA51B5E98E4586D5`.

## R49 - GlobalDataVault Mutation Guard Bridge

Problem: `TryAcquireMutationGuard(ulong)` accepted high guard bits, but the normal `TryLockBuffer`/`TryAcquireWriteLock` conflict bridge only observed low active-lock bits through `HasMutationGuardForActiveLockBit`. A high-bit guard could therefore fail to block a buffer lock whose low active bit overlapped the intended domain. Separately, mutation-guard contention returned `false` silently, `PinReadOnlyAlias` had been changed into an unpaired `TryLockBuffer` path despite no alias release API, and memory-sentry dispose contained a 250-iteration `Thread.Sleep(1)` polling loop.

Solution: Folded `lowMask | highMask` into `activeConflictMask` in `TryAcquireMutationGuard` and use it for both pre-acquire and post-acquire active-lock conflict checks. `HasMutationGuardForActiveLockBit` now checks low and high guard words. Added `RecordMutationGuardContentionFault` with a non-negative folded mask key and called it on every real contention fail path. Restored `PinReadOnlyAlias` to `TryOpenAliasBuffer` and only added `BufferID.Unknown` rejection. Replaced dispose polling with a single volatile observation.

Rejected Alternatives: Rejecting all high-bit guards was rejected because many existing systems already use high-bit guard domains and would fail closed too broadly. Leaving high-bit guards disconnected from active buffer locks was rejected because it preserves a write race. Keeping `PinReadOnlyAlias` on `TryLockBuffer` was rejected because the API has no corresponding unlock and can leak the lock bit indefinitely. Keeping `Thread.Sleep(1)` was rejected because dispose should not spend up to 250 ms in polling when the shutdown path already rechecks in-flight state after worker stop.

Scalability potential: Low devices get fail-closed contention instead of racing memory relocation or blocking on a polling loop. Middle devices keep existing guard domains with conservative high/low conflict mapping. High and Ultra devices preserve continuous `GlobalQualityWeight` behavior in consumers; this change is correctness gating, not a quality switch and not a physical simulation.

Hardware Impact: Added scalar `OR`, volatile reads, and fault counter writes only on contention. Removed up to 250 ms of dispose polling. Runtime microseconds remain unmeasured because compilation/profiling were not run.

Evidence: `GlobalDataVault.cs:2354-2361`, `2774-2842`, `2998-3002`, `3774-3777`, `5011-5018`. `TARGETED_GLOBALDATAVAULT_FORBIDDEN_TOTAL=0`; `TOUCHED_DOMAIN_LOOKUP_TOTAL=0`; whole-file wait scan for `Thread.Sleep`, `SpinWait`, `Task.Delay`, and `.Wait(` returned zero hits. Braces/preprocessor `644/644`, `2/2`; SHA-256 `D73C4463E43005857C7F465099F2A1AD6D4EA279861FC0557C598BBCE67268BE`. Build gate blocked: `CPU_SAMPLE=93`, final `CPU_SAMPLE_FINAL=100`, compiler process count `0`; no `dotnet build` launched. Open risk remains: ecosystem frame/macro scheduled mutation guards still represent multi-buffer leases and do not satisfy a global one-lock proof.

## R50 - Ecosystem Scratch Buffer Guard Pruning

Problem: `ShinobuEcosystemBalancer` still carried pure scratch buffers inside scheduled mutation guards. `ShinobuSpatialHashBucketHeads` and `ShinobuSpatialHashNext` are transient link tables for frame and macro jobs. `ShinobuSpatialGridSortScratch` is a transient sort buffer for frame spatial-grid construction. Search across `Assets/_Project/Scripts` showed no runtime consumer beyond ecosystem claim/readiness/release and the scheduled jobs. Holding DataVault guard bits for those scratch buffers made defrag and sibling writers wait on data that has no external truth value.

Solution: Added persistent owner-local `NativeArray` scratch: `_spatialHashBucketHeadJobScratch`, `_spatialHashNextJobScratch`, and `_spatialGridSortJobScratch`. Frame and macro job resolvers now bind these arrays directly after validating capacity. `FrameJobMutationGuardMask` no longer includes `ShinobuSpatialHashBucketHeads`, `ShinobuSpatialHashNext`, or `ShinobuSpatialGridSortScratch`. `MacroJobMutationGuardMask` no longer includes the two spatial hash scratch buffers. DataVault BufferID claim/readiness/release remains unchanged for compatibility and diagnostics; scheduled jobs no longer pin those buffers.

Rejected Alternatives: Deleting the DataVault BufferIDs was rejected because external tooling or legacy diagnostics can still expect those handles to exist. Moving `ShinobuSpatialGridEntries` or `ShinobuSpatialGridBucketRanges` was rejected because they are externally read query facts and editor-visible state. Moving `ShinobuEcosystemCounters` was rejected again because cold counter writes can occur while a frame job is scheduled; a blind post-release counter publish could overwrite unrelated counter indices without a merge/version protocol.

Scalability potential: Low devices avoid three scheduled DataVault guard bits on normal frame jobs and two on macro jobs. Middle devices keep deterministic scratch capacity with no runtime allocation. High and Ultra devices keep continuous `GlobalQualityWeight` math for spatial probe count, neighbor samples, update stride, and render density; no binary `isLowEnd` branch was added. This is a data ownership cleanup, not a new simulation.

Hardware Impact: Normal ecosystem frame scheduled guard shrinks from `12` base bits to `9`; debug frame shrinks from `13` to `10`; macro guard shrinks from `6` to `4`. Added persistent unmanaged memory equals `SpatialHashBucketCapacity * sizeof(int)` plus `(entityCapacity + sectorCapacity) * sizeof(int)` plus `entityCapacity * sizeof(SpatialGridEntryDTO)`. Runtime microseconds remain unmeasured because final CPU sample was `100` and an active `dotnet` process was present.

Evidence: `ShinobuEcosystemBalancer.cs:85-94`, `100-104`, `206-208`, `1279-1290`, `1326-1328`, `1751-1804`, `1806-1841`, `1905-1929`, `1953-2000`. Static scans: `FRAME_GUARD_BIT_LINES=9`, `FRAME_DEBUG_EXTRA_BITS=1`, `MACRO_GUARD_BIT_LINES=4`, `FRAME_HASH_AND_SORT_BITS=0`, `MACRO_HASH_BITS=0`, `DECLARED_FUNCTION_FORBIDDEN_TOTAL=0`, `TOUCHED_DOMAIN_LOOKUP_TOTAL=0`, braces/preprocessor `590/590`, `8/8`, SHA-256 `1DD3858FD3D8409120B3785F9FDDD0BF6A869AD24AFEA4919B3DCE2CDD3FC25C`. Build not run: `CPU_SAMPLE_FINAL=100`, compiler process count `1`, active `dotnet` PID `69052`.
