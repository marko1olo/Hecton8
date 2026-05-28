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
