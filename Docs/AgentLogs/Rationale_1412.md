# Rationale 1412 - LIVE_DATAVAULT_COMPACTION_STRESS_FUZZER

Status: CODED / CORE BUILD GREEN / FUZZER ASMDEF COMPILE PENDING

## Decision 000 - Ledger Initialization
Problem: Agent state must survive context compression and prove fresh batch hygiene.
Solution: Created dedicated status and rationale ledgers before source edits.
Rejected Alternatives: Chat-only state; unsafe because context compression can discard task proof.
Scalability potential: Low/Middle/High/Ultra unaffected; ledger is Editor/documentation only.
Hardware Impact: No runtime impact on i3/MX350; file I/O outside game execution.

## Decision 001 - Use Real Vault API, Not a Mock
Problem: A mock allocator cannot expose stale generation handles, block relocation, H8Memory raw arena growth, or `BlockFlagLocked` behavior.
Solution: Fuzzer targets a private `GlobalDataVault.Create(...)` instance and public generation/lock/pin APIs; no production vault or `GlobalRegistry` route.
Rejected Alternatives: Mock vault; invalid because it would test fabricated lock semantics. Editing `GlobalDataVault`; forbidden by assignment.
Scalability potential: Low uses short duration and fewer slots; Middle extends duration; High/Ultra increase worker count, slot count, and forced compaction pulses through continuous `GlobalQualityWeight` mapping.
Hardware Impact: Low-end i3/MX350 protected by editor run config and compile throttle; no player hot-path cost.

## Decision 002 - Editor Reflection Boundary
Problem: The requested direct compaction collision and `LockedSkipCount` proof are private inside `GlobalDataVault`.
Solution: Editor-only fuzzer reflects `TryRunLiveCompactionSlice(uint)` and `_defragLockedSkipCount` while the whole file is wrapped in `#if UNITY_EDITOR`.
Rejected Alternatives: Public `FrostTickDefrag` only; insufficient because it halts before block-scan skip paths when active locks are present. Production accessor addition; rejected public API mutation.
Scalability potential: Reflection is editor diagnostics only; runtime tiers unaffected.
Hardware Impact: Reflection cost is outside player; low-end editor runs can lower duration/worker count through `GlobalQualityWeight`.

## Decision 003 - Deterministic Pattern Proof
Problem: Relocation safety must be proven by data, not by absence of exceptions.
Solution: Each `int` slot stores `FNV-like(BufferID,index)` with avalanche mixing; verification re-resolves generation handles after compaction/growth and checks every active integer.
Rejected Alternatives: Random payloads; rejected because failures cannot be reproduced. Checksums only; rejected because byte-local corruption could hide behind a collision.
Scalability potential: Low verifies fewer slots; Ultra increases slot count and job iterations. The algorithm remains deterministic across tiers.
Hardware Impact: O(active integers) editor-only verification. No MX350 runtime cost.

## Decision 004 - Threading Model
Problem: Unbounded concurrent `NativeHashMap` structural mutation can crash the editor before reporting a managed failure.
Solution: Workers use real `Task` parallelism and race locks/pins/defrag; structural allocation/release is bounded by per-slot gates and small slot counts. Faults are captured into a managed queue and reported.
Rejected Alternatives: Completely serialized fake loop; rejected because it does not test `_activeLocks` or `_compactionFence`. Unbounded raw mutation storm; rejected because it can become an editor access violation instead of a usable proof artifact.
Scalability potential: Low 4 workers/short duration, Middle 6, High 8, Ultra longer jobs and more compaction pulses.
Hardware Impact: Low-end silicon protected by config; high-end can run 100000+ operations.

## Decision 005 - Whole-File Editor Quarantine
Problem: Fuzzer uses UnityEditor, reflection, Tasks, and destructive memory pressure. It cannot enter player builds.
Solution: First line is `#if UNITY_EDITOR`, last token is `#endif`, and no production script references `OOP_MemorySentryConcurrentRelocationFuzzer`.
Rejected Alternatives: Editor folder only; unsafe because parent runtime asmdef can still include child files. Partial method guards; higher leak risk.
Scalability potential: Not runtime. Editor Low/Middle/High/Ultra scales through `GlobalQualityWeight`.
Hardware Impact: 0 B/frame and 0 us/frame in player release by preprocessor exclusion.

## Decision 006 - LongRunning Task Workers
Problem: The test must create real scheduler pressure, not a deterministic main-thread rehearsal.
Solution: Use `Task.Factory.StartNew(... LongRunning ...)` for 4-8 workers plus a compaction loop. Each worker owns a xorshift RNG; no shared `Random` lock.
Rejected Alternatives: `ThreadPool.QueueUserWorkItem` without handles; harder to join and report. `async Task`; rejected by project allocation rules and exception ambiguity.
Scalability potential: Worker count and job iterations scale continuously from survival to overkill.
Hardware Impact: Low-end default is bounded duration/operations; no automatic run at editor load.

## Decision 007 - Burst Pin Job
Problem: A pin without active memory access does not prove scheduled jobs survive compaction pressure.
Solution: `ReadWriteStressJob` accepts the resolved `NativeArray<int>`, validates deterministic values repeatedly, rewrites the same pattern, and reports mismatch through a native failure flag.
Rejected Alternatives: Empty job; invalid because it may optimize away risk. Managed loop; does not exercise job pointer ownership.
Scalability potential: `JobInnerIterations` scales with `GlobalQualityWeight` from 64 to 1024.
Hardware Impact: Editor-only CPU stress. Player runtime unaffected.

## Decision 008 - Skip Counter Capture
Problem: `_defragLockedSkipCount` resets every `FrostTickDefrag` pass, so end-of-run read can miss a real collision.
Solution: After every forced compaction pulse, reflect the private counter and retain max observed skip count in fuzzer state.
Rejected Alternatives: `LastDefragFlags` only; ambiguous alias-block reason. Public telemetry snapshot; does not expose `LockedSkipCount`.
Scalability potential: Constant-time editor check independent of slot count.
Hardware Impact: Reflection cost only during manual fuzzer execution.

## Decision 009 - Arena Growth Resolve Probe
Problem: Testing arena growth by resizing one slot while holding that same slot gate would serialize all handle resolution and hide `_compactionFence` behavior.
Solution: Growth forces slot 0 above `DefaultArenaBytes`; a separate long-running resolver hammers active neighbor slots and records resolve attempts/misses while `H8Memory.ReallocateRaw` may relocate the arena.
Rejected Alternatives: Resolver on the growth slot; blocked by the harness lock. Relying on ordinary worker allocations; not guaranteed to exceed the 128 MiB default arena.
Scalability potential: Low/Middle use one forced growth; High/Ultra may raise `ArenaGrowthLength` continuously through `GlobalQualityWeight`.
Hardware Impact: Editor-only 129 MiB growth probe. i3/MX350 impact is bounded to manual menu execution, no player frame cost.

## Decision 010 - Dry-Run Lock Path
Problem: The test must prove a pin blocks relocation before a real crash proves the opposite.
Solution: `TryLockBuffer` sets `BlockFlagLocked`, increments `Reserved1`, and sets `_activeLocks`; `TryRunLiveCompactionSlice` rejects active burst locks before move and increments `_defragLockedSkipCount` when a locked block is encountered.
Rejected Alternatives: Checking only `FrostTickDefrag` return behavior; it has no return value and can reset skip telemetry each tick.
Scalability potential: Low/Middle/High/Ultra only change collision frequency; lock truth remains invariant.
Hardware Impact: The proof is static source analysis plus editor fuzzer telemetry; runtime cost 0 us/frame.

## Decision 011 - Dry-Run Writer Path
Problem: A write lock must protect `NativeArray` views from compaction while worker verification is reading and rewriting payload.
Solution: `TryAcquireWriteLock` stores `ActiveWriterSystemID`, sets `BlockFlagLocked`, increments `Reserved1`, and publishes the active lock bit; release occurs in `finally` after verification and writeback.
Rejected Alternatives: Read-only alias for writer checks; alias paths carry external-view flags and test a different state machine.
Scalability potential: Low verifies fewer integers; Ultra spends more saved CPU on more write-lock collisions.
Hardware Impact: Editor-only. No MX350 player impact because the fuzzer is excluded from release compilation.

## Decision 012 - Report Truth Boundary
Problem: The fuzzer code can be codified without claiming a runtime pass that has not been executed inside Unity Editor.
Solution: JSON report will distinguish static codification/build status from executed fuzzer iterations; runtime fields remain zero unless the menu fuzzer is actually run.
Rejected Alternatives: Fake success report; rejected because objective logs beat optimistic claims.
Scalability potential: Runtime report scales by actual `GlobalQualityWeight`; static report remains fixed.
Hardware Impact: Static report is file I/O only. Heavy execution is manually gated.

## Decision 013 - Build Gate Refusal
Problem: Final `dotnet build` is required only if the machine is free, but the sampled host was already saturated.
Solution: Refused build execution when CPU sampled above the allowed threshold. Latest APEX gate saw CPU 53 percent, csc 0, dotnet 1. Marked Task 15 blocked by contention and wrote a non-passing report with runtime iterations set to zero.
Rejected Alternatives: Launching a second compiler anyway; directly violates user CPU constraint and would produce noisy failure data.
Scalability potential: No runtime impact. On weak devices the gate protects the workstation; on high-end hardware it allows the final compile when CPU is actually free.
Hardware Impact: Avoided adding compiler load on i3/MX350-class contention; saved an unbounded build stall.

## Decision 014 - False Positive Probe Boundary
Problem: Assertion logic must catch deliberate corruption, but executing it requires Unity Editor/domain compilation that was blocked by the build gate.
Solution: Codified `RunFalsePositiveProbe()` and `InjectDeterministicCorruption()` so the probe flips one deterministic int and only passes when `VerifyAllActiveSlots()` raises `FatalMemoryCorruptionException`.
Rejected Alternatives: Marking the probe as passed without execution; invalid. Writing corruption into production vault; invalid domain breach.
Scalability potential: Same hash path across Low/Middle/High/Ultra; only duration and slot counts scale.
Hardware Impact: Editor-only. No frame cost and no release binary inclusion.

## Decision 015 - LatestCreated Isolation Repair
Problem: `GlobalDataVault.Create(...)` and `Initialize(...)` publish `_latestCreated`; my private fuzzer vault could temporarily hijack editor diagnostics.
Solution: Added `CreateIsolatedVault(...)` and immediately restored private static `_latestCreated` by reflection after initialization.
Rejected Alternatives: Accepting diagnostic pollution; wrong because `TryGetLatestCreated()` is a documented bootstrap/editor/diagnostic route. Editing `GlobalDataVault`; forbidden by the task.
Scalability potential: No runtime scalability change. The editor fuzzer remains bounded by continuous quality config.
Hardware Impact: Reflection cost occurs once per manual fuzzer run; 0 us/frame in player.

## Decision 016 - Homeostasis Quality Without Assembly Coupling
Problem: The default fuzzer profile should consume `HomeostasisBrain.GlobalQualityWeight`, but the editor asmdef cannot directly reference default `Assembly-CSharp` safely.
Solution: Resolve `Hecton8.Core.HomeostasisBrain.GlobalQualityWeight` by reflection for the default menu run; fallback remains 0.35 if the type/property is unavailable.
Rejected Alternatives: Adding a direct reference that may not compile from an asmdef to default assembly. Binary low-end switches; forbidden.
Scalability potential: Low/Middle/High/Ultra are continuous through `math.saturate`, smoothstep, and `math.lerp`.
Hardware Impact: One reflection scan at menu start only; no runtime cost.

## Decision 017 - Explicit Config Offsets
Problem: APEX verification requested mathematical struct offset proof; `[StructLayout.Sequential]` was weaker than necessary for an audit artifact.
Solution: Converted `MemorySentryFuzzerConfig` to explicit 64-byte layout with fields at 0,4,8,12,16,20,24,28,32,36,40,44,48,52,56,60.
Rejected Alternatives: Relying on default packing; acceptable in practice but weaker evidence.
Scalability potential: DTO layout is invariant across quality values.
Hardware Impact: No measurable cost; explicit layout removes ambiguity for ARM64/editor inspection.

## Decision 018 - Arena Growth Slot Release
Problem: The forced arena-growth buffer is intentionally larger than 128 MiB. Leaving it active lets the random Burst pin path select it, creating a multi-billion-operation editor stall.
Solution: Release the growth slot immediately after the arena relocation probe and before the normal chaos loop.
Rejected Alternatives: Keep it active for more stress; rejected because it tests CPU endurance more than DataVault lock correctness.
Scalability potential: Low/Middle/High/Ultra still force arena growth; normal chaos cost remains bounded by quality-scaled `MaxBufferLength`.
Hardware Impact: Prevents accidental i3/MX350 editor freeze while retaining relocation coverage.

## Decision 019 - Non-Vacuous Verification
Problem: A fuzzer can randomly release all slots before final verification, creating a meaningless integrity pass over zero integers.
Solution: Added `EnsureVerificationPopulation()` before verification and a hard failure when `VerifiedIntegers <= 0`.
Rejected Alternatives: Treat zero active slots as pass; invalid because no bytes were proven intact.
Scalability potential: Adds a 64-int fallback only when chaos empties the vault; negligible across tiers.
Hardware Impact: Bounded fallback cost, no runtime player impact.

## Decision 020 - Corruption Probe Target Guarantee
Problem: False-positive probe could silently skip corruption if every slot had been released by the chaos loop.
Solution: `InjectDeterministicCorruption()` now tries active slots, creates a fallback deterministic slot if needed, and throws if no target can be created.
Rejected Alternatives: Silent return; invalid because it can report a failed probe without proving the verifier catches corruption.
Scalability potential: Same deterministic hash path; only fallback target count is fixed at 64 ints.
Hardware Impact: Negligible editor-only fallback; no release binary impact.

## Decision 021 - Diagnostic Owner Separation
Problem: The editor fuzzer used `SystemID.CoreDataVault` as its allocation and job-fence owner, which could make diagnostic stress jobs indistinguishable from production vault ownership in global `H8Memory` owner-job telemetry.
Solution: Changed the fuzzer owner to `SystemID.CoreDiagnostics`; the isolated `GlobalDataVault` still owns its private payloads, but `H8Memory.RegisterActiveJob` no longer records fuzzer fences under the production vault owner.
Rejected Alternatives: Keeping `CoreDataVault`; rejected because the fuzzer is a diagnostic tool, not the production vault owner. Adding a new `SystemID`; rejected because `CoreDiagnostics` already exists and avoids public enum churn.
Scalability potential: No gameplay tier change. Low/Middle/High/Ultra fuzzer profiles still scale continuously through `GlobalQualityWeight`.
Hardware Impact: No player cost. Editor teardown telemetry is cleaner on low-end machines because diagnostic fences are separated from core-vault owner fences.

## Decision 022 - False-Positive Mismatch Specificity
Problem: The false-positive probe previously treated any `FatalMemoryCorruptionException` during verification as proof that deterministic hash corruption was caught.
Solution: Added `PatternMismatchMemoryCorruptionException` and made `VerifyPattern()` throw it only on actual deterministic payload mismatch. `RunFalsePositiveProbe()` now accepts only this exception type as expected corruption.
Rejected Alternatives: String-matching exception messages; rejected as brittle. Accepting any fatal verifier exception; rejected because failed resolve or zero verification is not proof of byte-corruption detection.
Scalability potential: Invariant across tiers; only the number of checked integers scales.
Hardware Impact: Failure-only editor allocation path. No release binary inclusion and no frame cost.

## Decision 023 - Monotonic Blackbox Sequence
Problem: The 300-entry blackbox wrote `Sequence = cursor + 1`; after ring wrap, sequence values repeated and postmortem ordering became ambiguous.
Solution: Added `TelemetrySequence` to `FuzzerState` and write `Interlocked.Increment(ref state.TelemetrySequence)` into each `FuzzerTelemetryEntry.Sequence`.
Rejected Alternatives: Reconstructing order from cursor only; rejected because dump readers need objective frame/event order after wrap.
Scalability potential: Constant-time; independent of quality tier. Low profile gets the same forensic ordering as Ultra.
Hardware Impact: One editor-only interlocked increment per telemetry entry. No player runtime cost.

## Decision 024 - Cleanup Return Enforcement
Problem: The fuzzer called `ReleaseWriteLock` and `TryUnlockBuffer` inside `finally`, but it did not check whether the cleanup API actually succeeded.
Solution: Added `ReleaseWriteLockOrRecord()` and `UnlockBufferOrRecord()` wrappers. They run only from cleanup paths and set explicit failure flags when cleanup returns false.
Rejected Alternatives: Throwing inside cleanup; rejected because it can hide the original memory-corruption signal. Ignoring return values; rejected because a stuck lock invalidates the fuzzer result.
Scalability potential: Constant-time cleanup check. No quality-tier behavior change.
Hardware Impact: No player cost. Editor-only branch after lock/pin operations.

## Decision 025 - Blackbox Allocation Gate
Problem: The critical memory fuzzer could continue if its 300-entry blackbox allocation failed, leaving no binary dumpable telemetry.
Solution: Added a hard failure if `H8Memory.Allocate<FuzzerTelemetryEntry>(300, ...)` returns an uncreated array.
Rejected Alternatives: Running without blackbox; rejected by the crash telemetry mandate.
Scalability potential: The ring remains fixed at 300 entries across Low/Middle/High/Ultra; quality changes workload, not forensic capacity.
Hardware Impact: 300 x 64 bytes = 19,200 bytes editor-only persistent native memory. No release binary impact.

## Decision 026 - Expected Corruption Cannot Mask Other Faults
Problem: The false-positive probe could pass after catching the deliberate hash mismatch even if unrelated worker failures or quarantine flags were also present.
Solution: The corruption probe now passes only when `ExpectedCorruptionCaught == 1` and `FailureFlags == 0`. Managed exception count permits exactly one expected verifier exception; extra exceptions set `FailureFlagManagedException`.
Rejected Alternatives: Treating expected corruption as an unconditional pass; rejected because it can hide independent fuzzer defects.
Scalability potential: Invariant across tiers; only operation count and checked payload size scale.
Hardware Impact: Integer comparisons only. No player runtime cost.

## Decision 027 - Harness Gates For Proof Hygiene
Problem: Subagent audit found the fuzzer could create failures by concurrent `GlobalDataVault` structural map/list mutation or concurrent compaction re-entry, which is not clean relocation proof.
Solution: Added `StructuralGate` around `EnsureGenerationHandle` and `ReleaseBuffer`, and `CompactionGate` around `FrostTickDefrag` plus reflected `TryRunLiveCompactionSlice`. Worker tasks still race write locks, pins, jobs, and defrag attempts, but structural container mutation is not the random crash source.
Rejected Alternatives: Leaving raw structural mutation concurrent; rejected because a `NativeParallelHashMap` structural crash would not prove a compaction barrier defect. Editing `GlobalDataVault`; forbidden by 1412 scope.
Scalability potential: Low/Middle/High/Ultra still scale worker count, slot count, duration, target operations, job iterations, buffer length, arena growth length, and compaction cadence via `GlobalQualityWeight`.
Hardware Impact: One monitor enter on editor-only structural and compaction calls; no player cost. Low-end editor runs trade invalid crash noise for actionable compaction evidence.

## Decision 028 - No Worker Mutation Of H8Memory Job Ledger
Problem: `H8Memory.RegisterActiveJob` mutates global owner-job `NativeParallelHashMap` and `NativeList` state without a fuzzer-local synchronization contract; calling it from 4-8 worker tasks could corrupt global diagnostics.
Solution: Removed `H8Memory.RegisterActiveJob` from the pin-job path. The fuzzer already holds `GlobalDataVault.TryLockBuffer` until `JobHandle.Complete`, which is the DataVault relocation proof route. The job failure channel now uses one persistent `NativeArray<int>` indexed by slot, allocated once before workers launch.
Rejected Alternatives: Serializing `RegisterActiveJob`; rejected because other editor systems could still share the global H8Memory ledger. Keeping the call; rejected because it tests H8Memory job-ledger concurrency, not DataVault compaction. Allocating one `TempJob` failure flag per pin operation; rejected because it adds a hot-path native allocation.
Scalability potential: Same quality-scaled job workload; less global diagnostic contention on every tier.
Hardware Impact: Removes global H8Memory map/list writes from worker threads. Low-end editor stability improves; player runtime unaffected.

## Decision 029 - Active Mask And Resolver Timeout Evidence
Problem: The public defrag route previously received `0u`, weakening proof that the `activeBurstLockMask` parameter was wired, and arena resolver timeout was ignored.
Solution: `ForceCompactionPulse` now samples `Vault.ActiveBurstLockMask`, passes it to `FrostTickDefrag`, tracks `MaskedDefragPasses`, then calls the reflected direct slice under the compaction gate. `WaitTaskNoThrow` now returns false on timeout and records `FailureFlagGrowthResolverTimeout`.
Rejected Alternatives: Chat-only caveat; rejected because the JSON report needs a metric. Treating resolver timeout as harmless; rejected because an unjoined resolver invalidates arena-growth proof.
Scalability potential: Constant-time telemetry. Quality scaling still controls collision frequency.
Hardware Impact: Negligible editor-only counters; no runtime impact.

## Decision 030 - Full Vault Entry Gate And Timeout Containment
Problem: The first structural gate pass covered allocation and release only. `TryAcquireWriteLock`, `TryReadHandle`, `TryGetGenerationHandle`, `TryLockBuffer`, `TryUnlockBuffer`, `ReleaseWriteLock`, compaction entry, and vault metric reads could still enter `GlobalDataVault` concurrently through the harness and produce non-actionable native container races. The pin-job path also allocated a native failure flag per operation, and timeout cleanup could dispose native memory while unjoined tasks still held aliases.
Solution: Expanded `StructuralGate` to all fuzzer-owned `GlobalDataVault` entry calls except cold isolated-vault initialization/dispose. Kept `CompactionGate` as the outer compaction re-entry guard. Replaced per-job failure allocation with a persistent per-slot `NativeArray<int>` and `FailureIndex`. Added `TasksCompleted`; if `Task.WaitAll` times out, the fuzzer writes the failure report/dump but intentionally skips vault/blackbox/job-failure/CTS disposal to avoid use-after-free. `WriteFailureDump` now locks `TelemetryGate` while copying the ring.
Rejected Alternatives: Leaving DataVault read/lock APIs outside the gate; rejected because the fuzzer would still test harness-induced native map/list races. Keeping per-operation `TempJob` flags; rejected by Zero-GC hot-path audit. Disposing native resources after a timed-out task join; rejected because safety beats cleanup aesthetics in a failing stress tool.
Scalability potential: Low/Middle/High/Ultra workload scaling remains continuous through `GlobalQualityWeight`. The gate only removes invalid harness races; it does not reduce lock/pin/compaction collision coverage.
Hardware Impact: Removes hot native allocation from each pin job. Adds monitor enters around editor-only vault calls. On i3/MX350 this trades invalid crash noise for deterministic failure reports; player runtime cost remains 0 because the file is editor-only.

## Decision 031 - Deferred Cleanup After Timeout
Problem: Timeout containment prevented use-after-free by skipping native disposal while tasks might still hold aliases, but that created a deliberate leak if workers later exited cleanly.
Solution: Stored the running `Task[]` in `FuzzerState` and queued exactly one long-running deferred cleanup when `TasksCompleted == 0`. The cleanup waits for the captured task array, then releases active slots, the per-slot job failure array, blackbox ring, isolated vault, and cancellation source only after workers complete.
Rejected Alternatives: Immediate disposal after timeout; rejected as unsafe because Burst/job aliases may still exist. Permanent leak on every timeout; rejected because a failing editor stress test should still clean up if late task completion makes it safe.
Scalability potential: No gameplay tier change. Low/Middle/High/Ultra stress profiles still scale through `GlobalQualityWeight`; deferred cleanup only runs on failed timeout paths.
Hardware Impact: One failure-path long-running managed task. Low-end machines avoid use-after-free and avoid permanent native leak if delayed worker completion occurs; player runtime remains unaffected.

## Decision 032 - Direct Compaction Must Receive Active Mask
Problem: `ForceCompactionPulse()` sampled `Vault.ActiveBurstLockMask` and passed it into public `FrostTickDefrag`, but the reflected private `TryRunLiveCompactionSlice(uint)` still received `0u`. That weakened the exact pin-vs-direct-compaction scenario the fuzzer is supposed to attack.
Solution: Changed the direct invocation to `direct(activeMask)`. The public and reflected compaction paths now consume the same sampled active lock mask inside the fuzzer harness.
Rejected Alternatives: Relying only on block `Reserved0/Reserved1` checks; rejected because `GlobalDataVault.TryRunLiveCompactionSlice` has explicit `HasActiveBurstLocks(activeBurstLockMask)` gates and the fuzzer must exercise them. Editing `GlobalDataVault`; rejected by assignment boundary.
Scalability potential: No player-tier change. Low/Middle/High/Ultra fuzzer profiles still scale continuously; the fix improves correctness of collision evidence at every tier.
Hardware Impact: One integer argument substitution. No additional CPU or memory cost on i3/MX350; player runtime remains excluded by `#if UNITY_EDITOR`.

## Decision 033 - Burst Safety Suppression Justification
Problem: `ReadWriteStressJob.Failure` used `[NativeDisableContainerSafetyRestriction]` without the three-paragraph proof required by the Native Memory & Job System mandate.
Solution: Added mandated `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` comments proving the per-slot single-writer invariant: `FailureIndex == SlotState.Index`, slot indices are unique, and `slot.Gate` serializes all jobs for that slot.
Rejected Alternatives: Allocating a `TempJob` failure flag per pin operation; rejected because it adds a hot fuzzer native allocation. Managed exceptions/queues inside Burst; rejected because Burst cannot own managed references.
Scalability potential: No behavior change. The proof covers all quality-scaled worker/slot counts.
Hardware Impact: Comments only. No runtime or editor execution cost.

## Decision 034 - Compilation Proof Boundary
Problem: The build gate opened, but generated project files on disk do not include `Hecton8.Core.Memory.Editor.csproj` and do not list `OOP_MemorySentryConcurrentRelocationFuzzer.cs` in `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Assembly-CSharp-Editor.csproj`, or `Assembly-CSharp.csproj`.
Solution: Ran exactly one allowed targeted build: `dotnet build Hecton8.Core.csproj --no-restore /m:1 /p:UseSharedCompilation=false` after sampling CPU 17%, csc 0, dotnet 0. Result was 0 errors, 0 warnings, 98.45 seconds. Report and status explicitly mark fuzzer asmdef compile proof as pending Unity project regeneration/import. A later no-more-build gate sampled CPU 99%, csc 0, dotnet 0, so a repeat build is forbidden.
Rejected Alternatives: Claiming the Core build compiled the fuzzer; rejected because the source file is absent from the generated project graph. Running a second Core build; rejected because it would not verify the modified fuzzer source and would violate the resource-throttling intent.
Scalability potential: No gameplay tier change. This is verification bookkeeping only.
Hardware Impact: One compiler run under the permitted gate. Further builds are deferred until Unity regenerates a project graph that actually includes the fuzzer asmdef.

## Decision 035 - Worker Hot-Path Exception Allocation Removal
Problem: Function-level scans found deterministic mismatch paths in worker methods could allocate managed exceptions through `VerifyPattern()` and pin-job failure handling.
Solution: Added `TryVerifyPattern()` returning mismatch data by `out` parameters. `WriteLockVerifySlot()` now records `FailureFlagIntegrity`, cancels, and returns. `PinAndScheduleJob()` now records `FailureFlagJobPayload`, cancels, and returns. Cold verification still calls `VerifyPatternOrThrow()` so the false-positive probe can prove the expected exception type outside the worker hot loop.
Rejected Alternatives: Treating exception allocation as harmless because the fuzzer is editor-only; rejected because the APEX mandate requested zero reference-type `new` in modified hot paths. Removing the false-positive exception type entirely; rejected because Task 16 requires proving mismatch-specific assertion behavior.
Scalability potential: No tier behavior change. Low/Middle/High/Ultra still scale worker count and job iterations continuously through `GlobalQualityWeight`.
Hardware Impact: Failure path now avoids managed exception allocation inside worker stress loops; editor-only, player runtime unaffected.

## Decision 036 - Final Evidence Closure
Problem: The JSON hash sidecar, status ledger, and log still carried stale report hashes after the latest CPU gate and hot-path scan corrections.
Solution: Updated the report with the latest no-build gate sample, recomputed SHA-256, synchronized the sidecar, status, and final log entry.
Rejected Alternatives: Leaving old `a66b...` sidecar or placeholder hashes; rejected because the report artifact would not cryptographically match the ledger.
Scalability potential: No runtime behavior change. Evidence bookkeeping only; Low/Middle/High/Ultra fuzzer profiles remain driven by `GlobalQualityWeight`.
Hardware Impact: No compiler run launched under CPU 99% load; avoided violating the build-throttle rule on weak host silicon.

## Decision 037 - Timeout And Blackbox Semantics Correction
Problem: A deeper audit found three remaining evidence defects: timeout failure allocated `TimeoutException` objects, `RecordFailure()` wrote failure bits into the telemetry `ActiveLockMask` field, and a runtime menu execution could overwrite the JSON report without refreshing the `.sha256` sidecar.
Solution: Timeout path now increments `TimeoutTaskCount` and relies on `FailureFlagTimeout`; `RecordFailure()` samples `Vault.ActiveBurstLockMask` with non-blocking `Monitor.TryEnter(..., 0, ref entered)` and releases in `finally`; `WriteReport()` writes `Docs/Reports/VAULT_COMPACTION_STRESS_REPORT_1412.json.sha256` after the JSON body.
Rejected Alternatives: Keeping managed timeout exceptions; rejected because a flag/counter already proves incomplete task count without allocation. Blocking on `StructuralGate` during failure telemetry; rejected because timeout reporting must not hang behind the same gate that may be involved in the timeout. Manual-only sidecar refresh; rejected because runtime reports must keep their own cryptographic artifact synchronized.
Scalability potential: No gameplay-tier behavior change. Low/Middle/High/Ultra fuzzer load remains continuous through `GlobalQualityWeight`; the fix improves failure evidence on every profile.
Hardware Impact: Removes failure-path managed exception allocation and avoids blocking failure telemetry. Latest build gate sampled CPU 62%, dotnet 0, csc 0, so no compiler was launched because CPU remained above the 50% threshold.

## Decision 038 - Legacy 1310 Fuzzer Quarantine
Problem: `Assets/_Project/Scripts/Editor/Memory/OOP_MemorySentryConcurrentRelocationFuzzer.cs` remained compiled by default as a legacy 1310 editor menu. It uses raw `Thread[]`, `GlobalDataVault.Create(...)`, writer-lock release outside a guaranteed `finally`, and a disposable vault after bounded joins; a failed join can turn that legacy diagnostic into an editor stability risk unrelated to the 1412 DataVault compaction proof.
Solution: Added an opt-in define guard: `#if UNITY_EDITOR && HECTON8_ENABLE_LEGACY_MEMORY_FUZZER_1310`. The active 1412 fuzzer remains in `Assets/_Project/Scripts/Core/Memory/Editor/OOP_MemorySentryConcurrentRelocationFuzzer.cs` with bounded cleanup and explicit evidence reporting.
Rejected Alternatives: Rewriting the 1310 harness into a second active fuzzer; rejected because it duplicates the 1412 domain and increases verification surface. Leaving the legacy menu compiled by default; rejected because it can create raw-thread disposal failures in the same memory domain.
Scalability potential: No gameplay-tier behavior change. The active fuzzer remains continuous through `GlobalQualityWeight`; the legacy raw-thread path is disabled unless explicitly requested.
Hardware Impact: Removes a default editor menu that can run a 100000-frame raw-thread memory stress tool on weak machines. Latest build gate sampled CPU 99%, dotnet 1, csc 0, so no compiler was launched.
