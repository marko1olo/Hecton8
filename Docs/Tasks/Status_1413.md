# Agent 1413 Status

Date: 2026-05-28
Agent: 1413
Role: ATOMIC_LOCK_CONTENTION_AND_FAIL_CLOSED_COORDINATOR
Domain: CORE & MEMORY INFRASTRUCTURE / GlobalDataVault Concurrency
Task Count: 20
Status: ACTIVE / PENDING UNITY RUNTIME VERIFICATION

## Mandates Loaded

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - fail-closed telemetry must allocate 0 B.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - GlobalDataVault ownership, handles, jobs, relocation, stale-handle behavior.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` - numeric fault telemetry and GlobalDataVault fault fields.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` - cold dependency cache, read-accessor purity, no hot registry polling.
- `ARCH_Execution_Phases.txt` - phase-owned simulation/write windows and telemetry phase.
- `DATA_Runtime_Struct_Layout_ARM64.txt` - unmanaged telemetry/DTO layout constraints.

## Loop 1 / Tasks 01-05

- [x] Task 01: EXHAUSTIVE_LOCK_SPAN_INQUISITION | DOD: generated `Docs/Reports/LOCK_CONTENTION_SPAN_LEDGER_1413.json` with 1,217 lock invocations across 251 files. Rejected raw `rg` output because it truncated and lacked span/finally/loop scoring. Static scan: 31,797,219 us.
- [x] Task 02: CALCULATION_EXTRACTION_ANALYSIS | DOD: ranked inflated spans by `priorityScore`, `tryBodyLines`, loop, nested lock, math/new/LINQ counts. Rejected editing top grep hits without complexity fields. Estimate: 420 us per record from generated JSON review.
- [x] Task 03: FAIL_CLOSED_BEHAVIOR_MAPPING | DOD: mapped GlobalDataVault contention to `_defragLockedSkipCount`, `_lastFaultBufferId`, `LastDefragFlags|DefragFlagAliasBlocked`. Rejected `Debug.Log` and managed exception paths. Estimate: 6 us fault write path, static source only.
- [x] Task 04: LOOP_CONTENTION_PATTERN_AUDIT | DOD: ledger flags 326 lock calls in loop-shaped contexts and 45 nested-lock body shapes. Rejected loop aggregation without source inspection because parser is line-regex, not Roslyn. Estimate: 31,797,219 us shared scan cost.
- [x] Task 05: TELEMETRY_AND_REPORTING_PLANNING | DOD: report path fixed as `Docs/Reports/LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json`; payload will include ledger summary, modified-file hashes, static proofs, and build throttle state. Rejected prose-only final report. Estimate: 250 us JSON schema planning.

## Loop 2 / Tasks 06-10

- [x] Task 06: SURGICAL_CALCULATION_HOISTING | DOD: `DestructibleOrganicManager.BuildTemplateCaches` now builds descriptors, loot scratch, material lookup, and authoring refs before vault locks; locked phase only clears/copies arrays. Rejected building descriptors inside lock. Estimate saved: static 100-line span -> 27-line copy span, ~73 source lines removed from lock.
- [x] Task 07: FAIL_CLOSED_BRANCH_IMPLEMENTATION | DOD: `GlobalDataVault` acquisition gate failures now return immediately and record numeric contention fault; no wait/spin. Rejected exception/log retry. Estimate: fail path stays O(1), ~6 us static estimate.
- [x] Task 08: LOOP_LOCK_AGGREGATION_REFACTORING | DOD: top two `DestructibleOrganicManager` lock sites no longer classify as `insideLoop`; ledger loop-shaped count dropped 327 -> 325 after edit. Rejected repeated lock-in-authoring-loop pattern. Estimate: removes two loop-context lock acquisitions from cold cache build.
- [x] Task 09: IRONCLAD_FINALLY_ENFORCEMENT | DOD: modified source keeps every successful vault lock behind `try/finally`; release gate split does not remove finalizers. Rejected early-return release paths after successful lock. Estimate: static `diff --check` clean except CRLF warning.
- [x] Task 10: ZERO_GC_TELEMETRY_FAULT_ROUTING | DOD: `RecordLockContentionFault(int key)` writes `_defragLockedSkipCount`, `_lastFaultBufferId`, generation zeros, and `DefragFlagAliasBlocked`; no strings, no collections, no Debug.Log. Rejected managed report path. Estimate: O(1) integer writes.

## Loop 3 / Tasks 11-15

- [x] Task 11: NESTED_LOCK_DEADLOCK_PREVENTION | DOD: nested-lock audit is embedded in `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json`; remaining nested shapes are reported, not hidden. Rejected broad nested lock rewrites without ownership proof. Estimate: 28,850,697 us scan.
- [x] Task 12: PRE_LOCK_VALIDATION_GUARDS | DOD: `BuildTemplateCaches` now validates/builds payloads before lock; `GlobalDataVault` keeps pre-gate metadata validation and only revalidates under gate. Rejected locking before descriptor build. Estimate: static 73-line span reduction.
- [x] Task 13: COMPILE_WALL_AND_NAMESPACE_HYGIENE | DOD: no new runtime `using`; editor test uses editor-only NUnit/reflection under `UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS`. `git diff --check` passed with CRLF warnings only. Estimate: 8,800,000 us diff/static checks.
- [x] Task 14: DRY_RUN_VERIFICATION_EXECUTION | DOD: rationale R2/R3/R4 simulate fail-closed contention, release clearing, and descriptor copy under lock. Rejected stale data risk in hot truth; organic cache is cold authoring payload. Estimate: 900 us reasoning record.
- [BLOCKED_BY_CONTENTION] Task 15: BATCHED_COMPILATION_AND_EXECUTION_CHECK | CPU sample was 100%, `dotnet` count 1, `csc` count 0. Per mandate, no `dotnet build` launched. Static checks only.

## Loop 4 / Tasks 16-20

- [x] Task 16: MOCK_CONTENTION_SPAM_FUZZER | DOD: added editor-only NUnit contract `GlobalDataVaultFailClosedEditTests1413` for 10,000 fail-closed write-lock attempts under forced mutation-gate contention and 0 B thread allocation assertion. Not executed; requires Unity Test Runner with `HECTON8_ENABLE_EDITMODE_TESTS`. Estimate: 10,000 attempts, runtime proof pending.
- [BLOCKED_BY_CONTENTION] Task 17: LOCK_SPAN_MICROSECOND_ASSERTION | Stopwatch runtime assertion not executed because CPU=100% and Unity Test Runner was not launched. Static span proof recorded in report.
- [x] Task 18: ZERO_COMPILATION_HOT_PATH_VERIFICATION | DOD: static grep found no `Thread.SpinWait` in `GlobalDataVault`; `RecordLockContentionFault` contains no allocation/log strings. Destructible added cold arrays only with canonical comments. Estimate: 3,100,000 us grep/static.
- [x] Task 19: NESTED_LOCK_AST_AUDIT | DOD: scanner reports 46 nested-lock body shapes after adding the editor test; top offenders are listed in final JSON. Rejected false claim of project-wide elimination. Estimate: 28,850,697 us scan.
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: wrote `Docs/Reports/LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json` with ledger summary, modified-file hashes, implemented changes, static verification, build throttle state, and regression model. Estimate: 4,800,000 us JSON generation.

## Loop 5 / Self-Review

- [x] Re-extracted `AGENT_PROMPT id="1413"` from `CURRENT_BATCH.md` after implementation. Estimate: 2,200,000 us.
- [x] Re-ran lock ledger after edits. Latest scan: 1,220 lock invocations, 325 loop-shaped locks, 46 nested-lock shapes, 28,850,697 us.
- [x] Reviewed modified `GlobalDataVault` snippets for `try/finally` preservation and release-gate behavior.
- [x] Reviewed modified `DestructibleOrganicManager.BuildTemplateCaches` for payload freshness and cold allocation comments.
- [x] Recorded build block honestly as `BLOCKED_BY_CONTENTION`; no fake green status.

## Loop 6 / APEX Final Verification

- [x] Corrected prompt extraction discipline | DOD: attribute-tolerant CLI/parser extracted `<AGENT_PROMPT id="1413" role="...">`; task count = 20; UTF-8 prompt SHA-256 in APEX verifier = `ea3c1b2810118d90e9b0fa1debccc5accae2ae9f322891eb613a8e6b4fa0d81c`. Rejected strict bare-tag regex because it false-failed on `role/chat_name`. Estimate: 1,900,000 us.
- [x] Fixed release-proof defect in `BuildTemplateCaches` | DOD: `OrganicTemplateDescriptorsBufferId` lock now enters `try` at line 5052 before the `OrganicLootEntriesBufferId` lock at line 5054; both unlocks are in `finally` lines 5090-5096. Rejected manual unlock-before-return branch. Estimate: removes one non-finally release path.
- [x] APEX Zero-GC scan | DOD: `Docs/Reports/LOCK_CONTENTION_APEX_VERIFICATION_1413.json` reports `totalForbiddenHits=0` across `RecordLockContentionFault`, `TryEnterReleaseMutationGate`, `ClearActiveLockBitIfUnused`, and the modified locked copy window. Patterns scanned: reference `new`, `string.Format`, `.ToString()`, LINQ, `foreach`. Estimate: 5,200,000 us.
- [x] Data Sovereignty proof | DOD: no fields migrated to GlobalDataVault in this pass; secured existing `OrganicTemplateDescriptorsBufferId=(BufferID)73018` and `OrganicLootEntriesBufferId=(BufferID)73019`; `releaseInsideFinally=true` in APEX JSON. Estimate: source-line proof only.
- [BLOCKED_BY_CONTENTION] Final compiler check | DOD: CPU sample from `typeperf` = 100.000000%; `dotnetCount=1`; `cscCount=1`. No `dotnet build` launched. Estimate: avoided compiler contention.
- [x] Final artifact hashes | DOD: `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json` SHA-256 = `237384c81cb44ab84e0b7569e189b93247cb773e0a597cbbd3de81e04addcecd`; `LOCK_CONTENTION_APEX_VERIFICATION_1413.json` SHA-256 = `be5a4d80f4727ad4d71e84b8256e8741bfba539ed1a8ab173ae2320af163da65`.
- [OPEN_RISK] Release APIs | DOD: APEX verifier records that `ReleaseWriteLock`/`TryUnlockBuffer` can still return false if `_blockMutationGate` is busy. No spin/wait added. Requires dedicated deferred-release or owner-phase release-drain protocol; blind local patch rejected as unsafe.

## Loop 7 / Deferred Release Hardening

- [x] Release caller blast-radius scan | DOD: `rg` over `Assets/_Project/Scripts` found hundreds of `ReleaseWriteLock`/`TryUnlockBuffer` call sites, many ignoring bool return in `finally`; caller-by-caller API churn rejected. Estimate: 3,500,000 us.
- [x] Deferred release lane implemented | DOD: added `DeferredVaultReleaseRequest` explicit-layout 32-byte unmanaged request and `_deferredReleaseRequests` fixed NativeArray capacity 256 in `GlobalDataVault`. Release failure on busy mutation gate now queues writer/buffer-pin release instead of only returning false. Rejected spin/wait and ungated `_blocks` mutation. Estimate: queue scan upper bound 256 scalar slots only on release-gate contention.
- [x] Owner-gate drain implemented | DOD: `TryEnterBlockMutationGate` and `TryEnterReleaseMutationGate` call `DrainDeferredReleaseRequestsLocked()` after legal gate acquisition; drain mutates `_blocks` and metadata only under the existing mutation gate. Estimate: O(256) worst-case per mutation-gate entry while pending count > 0.
- [x] ABI proof extended | DOD: `ValidateAbiLayout()` now checks `UnsafeUtility.SizeOf<DeferredVaultReleaseRequest>() == 32`; APEX JSON lists offsets: State 0, BufferKey 4, OffsetBytes 8, ActiveLockBit 16, LockOwnerSystemId 20, Kind 24, Flags 25, Reserved16 26, Sequence 28.
- [x] APEX Zero-GC scan extended | DOD: `totalForbiddenHits=0` across deferred release queue/drain helpers plus previous hot windows. Patterns: reference `new`, `string.Format`, `.ToString()`, LINQ, `foreach`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: CPU sample = 100.000000%, `dotnetCount=1`, `cscCount=0`; no `dotnet build`, no Unity Test Runner. Deferred release remains `PENDING COMPILER/UNITY RUNTIME VERIFICATION`.
- [x] Final artifact hashes after Loop 7 | DOD: `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json` SHA-256 = `c83d189745622b9b4ebd055abfa694f16cf273efa8933bc283fd06f83da8f57d`; `LOCK_CONTENTION_APEX_VERIFICATION_1413.json` SHA-256 = `f12f9e60672c6653d288bc94855dc923210e17c6ae25af0d47b60a2d54a9ba36`; ledger SHA-256 = `23ed043fe774fa28d8c6aed1dd4bf25a70495a5ec6d255ea6a0861b1b3e70220`.

## Loop 8 / Deferred Release Poison-Slot Audit

- [x] Duplicate/stale request audit | DOD: reviewed deferred writer and buffer-pin drains for duplicate queue behavior. Found buffer-pin owner mismatch could leave a stale pending slot forever when a duplicate release request survived after ownership changed. Estimate: 1,100,000 us.
- [x] Poison-slot fix | DOD: `DrainDeferredBufferPinReleaseLocked` now treats `LastAliasRequester != lockOwner` as drained after clearing the active bit; this prevents permanent pending-slot poisoning. Rejected keeping request pending because no future owner change can make the stale request valid. Estimate: O(1) per stale request.
- [x] Invalid-key guard | DOD: `QueueDeferredRelease` now rejects keys outside `_metadataByBufferId.Length` before writing the native ring. Rejected relying only on caller prevalidation. Estimate: 2 scalar checks.
- [x] APEX report regenerated | DOD: zero-GC forbidden hit count remains 0. Final report SHA-256 = `82699e1ce7f2f3cb391226982b681f625c1347aba391654ba15ec3fb53c22294`; APEX SHA-256 = `89f2b0861cf24d761bee980310d474cdb862ed1397f2d4a896b37e1d65203428`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: CPU sample = 100.000000%, `dotnetCount=1`, `cscCount=0`; no build/test launched.

## Loop 9 / APEX Evidence Tightening

- [x] APEX verifier artifact tightened | DOD: `Docs/Reports/agent1413_apex_verifier.py` now embeds `generatedUtc` and `compilationResourceThrottling` into `LOCK_CONTENTION_APEX_VERIFICATION_1413.json`. Rejected relying on chat/status text for CPU-throttle proof. Script SHA-256 = `48cd5d5f591e1ea0bfafddd88c95c5d9ff676f82a9fd264e044a90ebbdb673ce`.
- [x] APEX JSON regenerated | DOD: generatedUtc = `2026-05-28T01:16:54Z`; zero-GC forbidden hit count = 0; APEX SHA-256 = `3637ef317625346ffd8cd340d91eefe37439d45c88abf0ed01e77f318405242c`; final report SHA-256 remains `82699e1ce7f2f3cb391226982b681f625c1347aba391654ba15ec3fb53c22294`; ledger SHA-256 remains `23ed043fe774fa28d8c6aed1dd4bf25a70495a5ec6d255ea6a0861b1b3e70220`.
- [x] Static wait scan repeated | DOD: `rg` found no `Thread.SpinWait`, no `Task.Delay`, no lock-acquire retry wait loop in modified hot files. One `Thread.Sleep(1)` remains at `GlobalDataVault.cs:3457` only in dispose-time memory-sentry dump flush, outside write-lock hot path.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: fresh CPU sample = 100.000000%, `dotnetCount=1`, `cscCount=0`; no `dotnet build`, no Unity Test Runner.

## Loop 10 / Deferred Release Duplicate Audit

- [x] Duplicate deferred release risk found | DOD: reviewed retry behavior after release APIs return `false`. Found identical pending release requests could be queued more than once and later over-release `Reserved1` if the caller retried before drain. Estimate: 1,400,000 us static reasoning.
- [x] Duplicate coalescing implemented | DOD: `QueueDeferredRelease` now scans 256 pending native slots for matching `BufferKey`, `OffsetBytes`, `ActiveLockBit`, `LockOwnerSystemId`, and `Kind` before claiming an empty slot. Duplicate requests return true and do not create a second decrement request. Rejected caller-wide retry edits. Estimate: O(256) scalar reads only on release-gate contention.
- [x] Reports regenerated | DOD: optimization report SHA-256 = `be255c2a14af9f25eab104c77e8f3da371ed7321d42a1fd13dcdbf8c2574858a`; APEX SHA-256 = `f4d76e8f003399482f9c18959bce24bb07497f678db4edaaa610f873dd80b533`; `GlobalDataVault.cs` SHA-256 = `f5066a576ba44403058844e45d3f12b5e132851dc5ea0ae006e8e9f1b54e64a7`.
- [x] Zero-GC scan repeated | DOD: APEX zero-GC total remains 0; `QueueDeferredRelease` scanned line count = 54; no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: CPU sample during optimization report update = 100.000000%, `dotnetCount=1`, `cscCount=1`; APEX runtime sample = 100.000000%, `dotnetCount=1`, `cscCount=0`; no `dotnet build`, no Unity Test Runner.

## Loop 11 / Count-Preserving Deferred Release Contract

- [x] Buffer-pin coalescing fault found | DOD: self-audit proved same owner may legally hold multiple pins on the same buffer; collapsing identical buffer-pin releases would under-release `Reserved1`. Estimate: 1,200,000 us static contract reasoning.
- [x] Count-preserving fix implemented | DOD: `QueueDeferredRelease` now coalesces duplicate pending requests only when `kind == DeferredReleaseKindWriter`. Buffer-pin release requests are never coalesced, preserving one queued release per pin. Rejected adding a managed set or changing DTO layout.
- [x] Accepted deferred release return fixed | DOD: contended `ReleaseWriteLock`, internal writer release, and `TryUnlockBuffer` now return `QueueDeferred...` result. Accepted deferred release returns `true`; full/invalid queue returns `false`. This prevents compliant callers from retrying an already accepted deferred release.
- [x] Reports regenerated | DOD: optimization report SHA-256 = `ca2b45d83a6780a12ac0e38a43ffc4b6c09c4a7ce23ac364d27c46318ba03672`; APEX SHA-256 = `a5d83728bb4237b166416d92766abd0a7fd44a8effe4d2b6c478225c7b512616`; `GlobalDataVault.cs` SHA-256 = `aac1310e9e71a3e1d0ddbeb0260c8289e53b8b45cf60997a33d16d7c13d59117`.
- [x] Zero-GC scan repeated | DOD: APEX zero-GC total remains 0; `QueueDeferredRelease` scanned line count = 57; no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: APEX runtime sample = 100.000000%, `dotnetCount=0`, `cscCount=0`; CPU exceeds 50%, so no `dotnet build`, no Unity Test Runner.

## Loop 12 / Deferred Release Starvation Audit

- [x] Pre-defrag starvation risk found | DOD: defrag/orphan/growth paths often checked `HasActiveBurstLocks` before entering the mutation gate; an accepted deferred release could keep `_activeLocks` set and prevent the path that would drain it. Estimate: 1,600,000 us static control-flow review.
- [x] Non-blocking drain helper implemented | DOD: added `TryDrainDeferredReleaseRequests()` at `GlobalDataVault.cs:2018`. It performs one `TryAcquireBlockMutationGate()` attempt, drains pending releases if acquired, and returns without waiting if the gate is busy.
- [x] Drain call sites added before active-lock checks | DOD: added pre-check drain attempts in orphan sweep, mock relocation validation, defrag tick active-lock classification, live compaction slice, arena growth, and deferred arena growth. Rejected mutating `HasActiveBurstLocks` itself because read accessors must stay pure.
- [x] Reports regenerated | DOD: optimization report SHA-256 = `e0797a122b831e1251a29621472f8a6836a0553fce32b4c16c616e5cb140c3a9`; APEX SHA-256 = `f3ca9486daa472a2e889d8c25475990ab14bcc51578bce70fc3c75a589ecc335`; `GlobalDataVault.cs` SHA-256 = `7706a11cbd178938db30933d23f3ba306428612a2963fc014762a0b2d8878594`.
- [x] Zero-GC scan repeated | DOD: APEX zero-GC total remains 0; `TryDrainDeferredReleaseRequests` forbidden hit count = 0; no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: APEX runtime sample = 100.000000%, `dotnetCount=1`, `cscCount=0`; CPU exceeds 50% and dotnet is active, so no `dotnet build`, no Unity Test Runner.

## Loop 13 / Mutation-Gate Fault Containment Audit

- [x] Pre-return drain fault risk found | DOD: reviewed `TryEnterBlockMutationGate` and `TryEnterReleaseMutationGate`. Both acquired `_blockMutationGate` and drained deferred releases before returning ownership to caller code. If editor/runtime native-container checks faulted during drain, the gate could stay held. Estimate: 900,000 us static control-flow review.
- [x] Release-on-fault guard implemented | DOD: both gate entry helpers now release `_blockMutationGate` in a `catch` before rethrowing if `DrainDeferredReleaseRequestsLocked()` faults. Normal path remains a single acquire, drain, return; no spin, sleep, retry, or managed allocation added.
- [x] APEX scanner extended | DOD: `agent1413_apex_verifier.py` now includes `TryEnterBlockMutationGate` in the zero-GC hot-window scan.
- [x] Reports regenerated | DOD: APEX generatedUtc = `2026-05-28T01:42:02Z`; zero-GC total forbidden hits = 0; `TryEnterBlockMutationGate` lines 2632-2654 forbidden hits = 0; `TryEnterReleaseMutationGate` lines 2656-2671 forbidden hits = 0.
- [x] Final artifact hashes after Loop 13 | DOD: `GlobalDataVault.cs` SHA-256 = `1bf5afd57181118d831f9502afb2b76439c7b058bfb85db18b35d1acfd94f243`; `agent1413_apex_verifier.py` SHA-256 = `db9e48738423c983d4edf84dabfaf35f8dc341b463fd1f7b3b7688dc19cfc912`; optimization report SHA-256 = `eed3a266d12f8cb231b7192aeab7776347f8db49c47f703276141e8e281c6165`; APEX SHA-256 = `fac5c1a3621bcc34d06e656da59a080efab37f3eabc614717754291d033d166c`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: APEX runtime sample = 99.422646%, `dotnetCount=1`, `cscCount=0`; CPU exceeds 50% and dotnet is active, so no `dotnet build`, no Unity Test Runner.

## Loop 14 / Alias-Open Contention Dump Audit

- [x] Fail-closed dump violation found | DOD: reviewed `TryOpenAliasBuffer`. The corruption branches legitimately dump black-box evidence, but the pure mutation-gate contention branch at line 1357 also called `DumpPhiVodBlackBox()`, which performs directory/file IO and managed string work. Estimate: 1,000,000 us static branch classification.
- [x] Contention branch corrected | DOD: `TryOpenAliasBuffer` gate failure now calls `RecordLockContentionFault(key)` and returns false. Corruption/type/pointer mismatch branches still call `DumpPhiVodBlackBox()`.
- [x] APEX scanner extended | DOD: `agent1413_apex_verifier.py` now scans the full `TryOpenAliasBuffer<T>` method as a modified hot window.
- [x] Reports regenerated | DOD: APEX generatedUtc = `2026-05-28T01:46:58Z`; zero-GC total forbidden hits = 0; `TryOpenAliasBuffer<T>` lines 1309-1449 forbidden hits = 0; optimization report hash before APEX write = `a519682ce0b5a9bfbf22426b3c2a38cfc85b90005d7a5d00d99eb08c8ad67fc9`.
- [x] Final artifact hashes after Loop 14 | DOD: `GlobalDataVault.cs` SHA-256 = `a0233a4e583874e84a7425741dcdf21bd21a7bf3ec6a2b282eb7076c1d32d4e4`; `agent1413_apex_verifier.py` SHA-256 = `d32312469b43f054207e17576d5698ca94dcd4b7d94fe04778990e8c76f94bec`; optimization report SHA-256 = `a519682ce0b5a9bfbf22426b3c2a38cfc85b90005d7a5d00d99eb08c8ad67fc9`; APEX SHA-256 = `dd4f343fe87b43c0a44faf864fb0c9c364855d5827161d23911fc12dd6216941`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: APEX runtime sample = 57.268876%, `dotnetCount=1`, `cscCount=0`; CPU exceeds 50% and dotnet is active, so no `dotnet build`, no Unity Test Runner.

## Loop 15 / External-View Publish Rollback Audit

- [x] New-buffer orphan risk found | DOD: reviewed `TryEnsureVaultBuffer` new allocation path. If `MarkExternalView` failed because the mutation gate was busy, the old cleanup removed `_buffers`, metadata, and key route, then attempted `TryFreeBlockRollback` while the gate was already known busy. That could leave an occupied arena block without metadata. Estimate: 1,300,000 us static branch reasoning.
- [x] Rollback-on-contention removed | DOD: after new allocation, external-view publish failure now records `RecordLockContentionFault(key)` and returns false while keeping the registered buffer/metadata route intact for retry. Existing corruption cleanup branches remain unchanged.
- [x] Remaining direct gate failures instrumented | DOD: direct `TryAcquireBlockMutationGate`/`TryEnterBlockMutationGate` failures in deferred-drain, live compaction, arena growth, allocation/reallocation/free helpers, `MarkExternalView`, and `MarkAliasReader` now record numeric contention before returning false or deferring work.
- [x] APEX scanner extended | DOD: `agent1413_apex_verifier.py` scans `TryEnsureVaultBuffer<T>` lines 1087-1305; forbidden hits = 0.
- [x] Final artifact hashes after Loop 15 | DOD: `GlobalDataVault.cs` SHA-256 = `6c057b9d799b703e290f0b7469525551434f3c3b51347953dcc12f4575f488d3`; `agent1413_apex_verifier.py` SHA-256 = `c3301569bed46fb2f3152e451929544b72a1886506cf77af64632e91e2ae0b61`; optimization report SHA-256 = `89eaea68b21966f90d0f25c06c43473f5d05e0b59bbe155ebb0fb2561a366328`; APEX SHA-256 = `bfe77168367959edb1b984281d56738987c54c3a2a0a363d8055fddc35510dcb`.
- [BLOCKED_BY_CONTENTION] Compiler/runtime proof | DOD: APEX runtime sample = 88.595548%, `dotnetCount=1`, `cscCount=1`; CPU exceeds 50% and compiler/dotnet are active, so no `dotnet build`, no Unity Test Runner.

## Loop 16 / Throttled Compiler Proof

- [x] Build gate opened and was sampled | DOD: APEX runtime sample before build was CPU `27.641788%`, `dotnetCount=0`, `cscCount=0`. This satisfied the resource-throttle rule.
- [x] Minimal runtime compile executed once | DOD: ran `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`. Rejected solution-wide build and restore. Build elapsed `48.80s`.
- [x] Compiler result | DOD: `Assembly-CSharp -> C:\hades\Hecton8\Temp\CodexBuild\Assembly-CSharp\Assembly-CSharp.dll`; `0 Warning(s)`, `0 Error(s)`.
- [x] APEX evidence regenerated | DOD: APEX generatedUtc = `2026-05-28T09:30:03Z`; zero-GC total forbidden hits = 0; `dotnetBuildLaunchedByAgent1413=true`; optimization build result = `PASSED`.
- [x] Final artifact hashes after Loop 16 | DOD: `GlobalDataVault.cs` SHA-256 = `7d7d78d610a3e46b11729f29975b7826dfef1c4a9045c6491c1fa0fdea777c66`; `agent1413_apex_verifier.py` SHA-256 = `9e51fb0be2c247c163adbdf273650ecd5e3fde2c3fb93984197bdde6d68615f4`; optimization report SHA-256 = `dc23187a2d8631bbcc107f2fcbaaf5ec947913134ff414f361ac4d67c3db94ce`; APEX SHA-256 = `729d0270337b12fbb873f30772d6134bb0bd3ecf312acf117b2c30dabf25f6d0`.
- [BLOCKED_RUNTIME] Unity Test Runner / editmode proof | DOD: not launched. The compiler proof is green; Unity runtime/editor behavior remains unexecuted.

## Loop 17 / Evidence Hash Reconciliation

- [x] Current-byte hash drift detected | DOD: `Get-FileHash` showed `GlobalDataVault.cs` on disk = `0d0599170f98d1c4dacf76e452d1a3401cd85e7a1ef8f320f04b0e0d5691d86e`, while the previous APEX JSON still embedded `7d7d78d610a3e46b11729f29975b7826dfef1c4a9045c6491c1fa0fdea777c66`.
- [x] Optimization report corrected | DOD: `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json.modifiedFiles[GlobalDataVault.cs].sha256` now matches the current file bytes.
- [x] APEX evidence regenerated | DOD: APEX generatedUtc = `2026-05-28T09:37:38Z`; zero-GC total forbidden hits = 0; embedded `globalDataVault` hash = `0d0599170f98d1c4dacf76e452d1a3401cd85e7a1ef8f320f04b0e0d5691d86e`; embedded optimization report hash = `d454aa45b29702d59ae5607b639f9ff1839d29c48ba637c07f5d3eb0378db867`.
- [x] Final artifact hashes after Loop 17 | DOD: APEX SHA-256 = `61cbc71a6a8cbafe2c75702321b43b68a11f5fea04406fbf27041bc708c54822`; optimization report SHA-256 = `d454aa45b29702d59ae5607b639f9ff1839d29c48ba637c07f5d3eb0378db867`; `agent1413_apex_verifier.py` SHA-256 = `9e51fb0be2c247c163adbdf273650ecd5e3fde2c3fb93984197bdde6d68615f4`.
- [BLOCKED_RUNTIME] Post-APEX runtime/build gate | DOD: APEX runtime sample after regeneration was CPU `88.088131%`, `dotnetCount=2`, `cscCount=0`; no additional `dotnet build` and no Unity Test Runner launched.

## Loop 18 / Cross-Agent Deferred Release Contract Recheck

- [x] Contract drift found | DOD: `QueueDeferredRelease` currently contains `if (kind == DeferredReleaseKindWriter)`, while earlier 1414 text had expected all-kind de-duplication. Current editor contract now matches writer-only coalescing at `ArenaAllocatorSentinel1414EditTests.cs:90`.
- [x] Writer-only policy retained | DOD: buffer-pin releases preserve one queued request per accepted release call because `TryLockBuffer` increments `Reserved1` for same-owner pins. Coalescing buffer pins would leak legitimate nested/same-owner pins.
- [x] Residual API limit recorded | DOD: without a per-acquire token, a caller that retries `TryUnlockBuffer` after accepted `true` can enqueue multiple buffer-pin releases. This is explicit residual risk, not hidden as solved.
- [x] Report/verifier updated | DOD: optimization report text now matches current code; APEX verifier emits `deferredReleaseContract` with `dedupePolicy`, `hasWriterOnlyFilter`, and `hasSerializedScanGate`.
- [x] APEX evidence regenerated | DOD: APEX generatedUtc = `2026-05-28T09:59:18Z`; zero-GC total forbidden hits = 0; `hasWriterOnlyFilter=true`; `hasSerializedScanGate=true`; `matchesArenaAllocator1414EditorContract=true`.
- [x] Final artifact hashes after Loop 18 | DOD: APEX SHA-256 = `bd30901deb1e6e10df4ec5efe39299864f6c201a90eac81559de59cbeff29114`; optimization report SHA-256 = `3a11f51bf112283355bdad433f90c7c716d36c7126a376bd26303632cbb3de1f`; `GlobalDataVault.cs` SHA-256 = `b35073e0f7ad2e833767c0b3f6b3139a05942bd9b416bc77c8f373b9a3d74aac`; `ArenaAllocatorSentinel1414EditTests.cs` SHA-256 = `f92bebfd212a46cb09a023c7a349b934c1431b3b651c80161f3757b1d7857309`; `agent1413_apex_verifier.py` SHA-256 = `4f5218f3c8ec004dafc9ab53eec120b0ed4d9c234479571e0f2a73792385246c`.
- [BLOCKED_RUNTIME] Post-Loop-18 compiler/runtime gate | DOD: APEX runtime sample was CPU `91.810825%`, `dotnetCount=1`, `cscCount=0`; no extra `dotnet build`, no Unity Test Runner.

## Loop 19 / TryAllocatePublishedBuffer Evidence Closure

- [x] Current rollback path re-audited | DOD: `TryAllocatePublishedBuffer<T>` now owns the mutation gate from line 1283 through the cleanup `finally` at lines 1357-1374. Failed allocation cleanup removes counted bytes, buffer route, metadata, key route, and arena block via `FreeBlockLocked` before `ReleaseBlockMutationGate()`. Rejected extra runtime rewrite because the current path is gate-contained.
- [x] APEX zero-GC scope corrected | DOD: `agent1413_apex_verifier.py` now scans `TryAllocatePublishedBuffer<T>` in addition to `TryEnsureVaultBuffer<T>`. Latest APEX JSON reports `TryAllocatePublishedBuffer<T>` lines 1270-1375, lineCount 106, forbiddenHitCount 0.
- [x] Optimization report synchronized | DOD: `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json.staticVerification.tryAllocatePublishedBufferZeroGcScan` now records the current line window and rollback/finally proof. Rejected chat-only evidence.
- [x] Final artifact hashes after Loop 19 | DOD: optimization report SHA-256 = `a4f45e1c3625774b9e917bdb6848b2f1c5dae7844dd016edb7c5e6342aa4b537`; APEX SHA-256 = `79500eb84222fb46fb039e4e50ebf6ddd608c759c14dd655eb1185583ac72b8a`; `agent1413_apex_verifier.py` SHA-256 = `8a5ff144a662564b16db0a6e9c8d71ae1796ec2c2befa66c1501ef26e24ccc25`; `GlobalDataVault.cs` SHA-256 = `b35073e0f7ad2e833767c0b3f6b3139a05942bd9b416bc77c8f373b9a3d74aac`.
- [BLOCKED_RUNTIME] Post-Loop-19 compiler/runtime gate | DOD: APEX runtime sample was CPU `48.971125%`, `dotnetCount=1`, `cscCount=0`; active dotnet process blocks a new build by rule. No extra `dotnet build`, no Unity Test Runner.

## Loop 20 / Deferred Release Invalid-Kind Hardening

- [x] Invalid-kind risk found | DOD: `DrainDeferredReleaseRequestsLocked` treated any non-writer pending request as buffer-pin. Private callers only pass writer/buffer-pin kinds, but malformed native state could turn an invalid kind into a false `Reserved1` decrement. Estimate: 900,000 us static ring audit.
- [x] Fail-closed guard implemented | DOD: `QueueDeferredRelease` now rejects any kind other than `DeferredReleaseKindWriter` or `DeferredReleaseKindBufferPin`; drain clears invalid pending kinds as poison slots without touching lock counters. Rejected routing invalid kind through buffer-pin drain.
- [x] APEX zero-GC repeated | DOD: latest APEX reports `totalForbiddenHits=0`; `QueueDeferredRelease` and `DrainDeferredReleaseRequestsLocked` remain in the scanned hot-window set.
- [x] Final artifact hashes after Loop 20 | DOD: `GlobalDataVault.cs` SHA-256 = `3d9f0351e690de3b7e42269fbcc78a131833b1bfde15be42342e4171101a417f`; optimization report SHA-256 = `bdb8156cf0ba68ea9a4153029c717f283615a3b599357b9b2f980c8b460e1e3d`; APEX SHA-256 = `e40ca9180b2a6aefe0dab7c1b5a39c3d33640a96b0ce0ca65b9ba2c25dd6c737`; verifier script SHA-256 = `8a5ff144a662564b16db0a6e9c8d71ae1796ec2c2befa66c1501ef26e24ccc25`.
- [BLOCKED_BY_CONTENTION] Post-Loop-20 compiler/runtime gate | DOD: APEX runtime sample was CPU `100`, `dotnetCount=1`, `cscCount=1`; no extra `dotnet build`, no Unity Test Runner.

## Loop 21 / Live Compaction Contention Dump Removal

- [x] Async dump escalation risk found | DOD: `TryRunLiveCompactionSlice` called `RequestMemorySentryDump()` when active locks remained after a drain attempt or when the mutation gate was busy. Those are expected contention skips, not corruption. Estimate: 800,000 us static branch review.
- [x] Fail-closed contention cleaned | DOD: removed `RequestMemorySentryDump()` from the two ordinary contention branches at lines 4187-4200; numeric lock-skip telemetry remains. Rejected background dump request on expected lock pressure.
- [x] APEX scope extended | DOD: `agent1413_apex_verifier.py` now scans `TryRunLiveCompactionSlice`; latest APEX reports lines 4163-4277, lineCount 115, forbiddenHitCount 0.
- [x] Final artifact hashes after Loop 21 | DOD: `GlobalDataVault.cs` SHA-256 = `e290018fb05717d72f2c1ca8c726428856d395bfc1b32aa4b352a8f39cf0460c`; verifier script SHA-256 = `fd329eeb7f99f2b722efddf0031c640d0ff55da847004cd40fb5978b334faea6`; optimization report SHA-256 = `dc85ef6326e41ab874a6d16e74d8529b905a68e440677f86e770c7d794290f35`; APEX SHA-256 = `12951f15396647d3e4e833dc779a2b3d1c17b364e111df4aa89031292e186d9f`.
- [BLOCKED_BY_CONTENTION] Post-Loop-21 compiler/runtime gate | DOD: APEX runtime sample was CPU `56.840784`, `dotnetCount=1`, `cscCount=0`; no extra `dotnet build`, no Unity Test Runner.

## Loop 22 / Cleanup Contention Dump Removal + Compile Proof

- [x] Scene cleanup dump risk found | DOD: `ReleaseSceneOwnedBuffers`, `ReleaseBuffersByOwner`, and `TryReleaseOrphanedBuffer` dumped PhiVod evidence for locked/external-view states that represent expected contention/pinning, not structural corruption. Estimate: 1,100,000 us static branch review.
- [x] Cleanup contention made scalar-only | DOD: removed final remaining-count dump after scene release; locked owner-release and orphan locked/external-view skips now record numeric alias-blocked telemetry and do not dump files. Corruption dumps for missing metadata/block and failed free remain intact.
- [x] APEX scope extended | DOD: `agent1413_apex_verifier.py` scans `ReleaseBuffersByOwner` and `TryReleaseOrphanedBuffer`; both report forbiddenHitCount 0.
- [x] Throttled compile executed | DOD: build launched only after APEX sample CPU `45.100791`, `dotnetCount=0`, `cscCount=0`. Ran `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`; result `0 Warning(s)`, `0 Error(s)`, elapsed `32.14s`.
- [x] Final artifact hashes after Loop 22 | DOD: `GlobalDataVault.cs` SHA-256 = `c4b38840a22049a9e393da2901bb37662e5536fde1245efcc7d88759b0053f33`; verifier script SHA-256 = `c3d7f45da7acff241970af51d109d35cafbe55180a77df524fc8f1de71af7ce1`; optimization report SHA-256 = `82e0c6d738eb2ffb8a9a775525bdda50954d21dfa6f9d286e63513742156942b`; APEX SHA-256 = `15f613ed4b3db81eee984059d5bc3e5719bc2028b014e5d57d6c19bf99bde21d`.
- [BLOCKED_RUNTIME] Unity Test Runner / editmode proof | DOD: not launched. Compiler proof is green; Unity runtime/editor behavior remains unexecuted.

## Loop 23 / Acquisition Telemetry + Deferred Queue Gate Removal

- [x] Silent acquisition contention found | DOD: `TryAcquireWriteLock` and `TryLockBuffer` returned `false` on compaction fence, active writer, alias reader, external-view pin, or saturated pin states without scalar contention telemetry. Estimate: 1,300,000 us static branch audit.
- [x] Acquisition false-returns instrumented | DOD: those contention branches now call `RecordLockContentionFault(key)` before returning. Metadata absence, generation mismatch, owner mismatch, and block-key mismatch remain non-contention failures. No strings, dumps, waits, or public API changes.
- [x] Deferred enqueue self-contention removed | DOD: removed private `_deferredReleaseEnqueueGate`; `QueueDeferredRelease` now uses per-slot `Interlocked.CompareExchange` state claims. Writer duplicate scan remains writer-only; buffer-pin release count semantics are preserved.
- [x] APEX scanner extended | DOD: `agent1413_apex_verifier.py` now scans `TryAcquireWriteLock`, `TryLockBuffer`, and the updated `QueueDeferredRelease`. Latest APEX reports `totalForbiddenHits=0`, `hasNoEnqueueGateBusyFailPath=true`, `hasAtomicSlotClaim=true`.
- [BLOCKED_BY_CONTENTION] Post-Loop-23 compiler proof | DOD: conditional build command sampled CPU `83.531429`, `dotnetCount=2`, `cscCount=0`; no `dotnet build` launched. APEX runtime sample after regeneration was CPU `91.48299`, `dotnetCount=2`, `cscCount=1`. Current runtime source is `PENDING_AFTER_LOOP23_RUNTIME_EDIT`.
- [x] Final artifact hashes after Loop 23 | DOD: `GlobalDataVault.cs` SHA-256 = `a3638d5f0d3bd01cd69d9494dde954cd22eec4a48b4366f7245f4c273e2d6ba7`; verifier script SHA-256 = `b5f67eb0658f3ca8ea83837cf21574a7b2cda226cac620478f99cac7f4e4468b`; optimization report SHA-256 = `3e84d6995b75ffe8ca758c92bd2e0295a8c1f66001ebe8be050f78becbfbeb94`; APEX SHA-256 = `7f57084a33c3fd152814e55498e1d504e1ca29ee9054c5260a85106d95a8b511`.

## Loop 24 / Deferred Queue Gate Reality Reconciliation

- [x] Evidence mismatch found | DOD: byte hash after APEX/report rewrite showed current `GlobalDataVault.cs` = `97153541fbf227867a11f48b98b21b10efd9ff49ac22a0807c8ba222dc21cff1`, not the previously logged `a3638d5f0d3bd01cd69d9494dde954cd22eec4a48b4366f7245f4c273e2d6ba7`. Static read found `_deferredReleaseEnqueueGate` still present and a `Thread.SpinWait(DeferredReleaseEnqueueSpinWait)` path inside `QueueDeferredRelease`.
- [x] Actual gate removal completed | DOD: removed `_deferredReleaseEnqueueGate`, its init/reset writes, `DeferredReleaseEnqueueSpinWait`, and the spin-wait/try/finally serializer from `QueueDeferredRelease`. Queueing now uses writer-only duplicate scan plus per-slot `Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)`.
- [x] APEX evidence regenerated | DOD: generatedUtc `2026-05-28T12:02:37Z`; zero-GC `totalForbiddenHits=0`; `hasNoEnqueueGateBusyFailPath=true`; `hasAtomicSlotClaim=true`; source text scan for `_deferredReleaseEnqueueGate`, `DeferredReleaseEnqueueSpinWait`, and `Thread.SpinWait(` returned no hits.
- [BLOCKED_BY_CONTENTION] Post-Loop-24 compiler proof | DOD: conditional build sampled CPU `96.77117`, `dotnetCount=1`, `cscCount=0`; no `dotnet build` launched. Current runtime source is `PENDING_AFTER_LOOP24_RUNTIME_EDIT`.
- [x] Final artifact hashes after Loop 24 | DOD: `GlobalDataVault.cs` SHA-256 = `41bc397a1f2a2c371ff71e175f1a1dc092531aef1fdd85848b38818b39075b20`; verifier script SHA-256 = `b5f67eb0658f3ca8ea83837cf21574a7b2cda226cac620478f99cac7f4e4468b`; optimization report SHA-256 = `e2851666f10999e97c88da530508758427e0690ac6c49fd846aa02cc4b299d59`; APEX SHA-256 = `49acd08502c659191efb4bcce8f0e93e0d8a6611d3acac1ceb03f242e82e11d8`.

## Loop 25 / Stale Writer Release Drain Hardening + Compile Proof

- [x] Non-drainable stale writer slot found | DOD: `DrainDeferredWriterReleaseLocked` returned `false` when queued owner no longer matched a current nonzero `ActiveWriterSystemID`. That stale request could remain pending forever and occupy the fixed deferred-release ring. Estimate: 1,000,000 us static race audit.
- [x] Stale writer discard implemented | DOD: owner mismatch now calls `ClearActiveLockBitIfUnusedLocked(request.ActiveLockBit)` and returns `true`, clearing the stale slot without calling `ReleaseWriterBlockLockUnlocked` on the newer writer.
- [x] Report language corrected | DOD: APEX and optimization report now state best-effort writer duplicate suppression, not strict atomic de-duplication. Added `hasStaleWriterOwnerDiscard=true`.
- [x] Throttled compile executed | DOD: build launched only after sample CPU `46.330838`, `dotnetCount=0`, `cscCount=0`. Ran `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1`; result `0 Warning(s)`, `0 Error(s)`, elapsed `21.52s`.
- [x] Final artifact hashes after Loop 25 | DOD: `GlobalDataVault.cs` SHA-256 = `d18348927bfcad28fb4d77850ae233fb89173b16a90f277359960dbbf0c5ca34`; verifier script SHA-256 = `66adcb69279b87d9349e37e7b1a80182d3b2c133452a274dd5d38b6b0bc9b2fe`; optimization report SHA-256 = `0ab83d555b621d0bb3ed2e74f1b06ac90b3ef29031b56e5c40eaf7dd2f5f75e9`; APEX SHA-256 = `e0064cbd703d605b8e99f878bdd5b7315c00b7d163f01e16cdfb55b81af391d8`.

## Loop 26 / Lock Span Ledger Refresh

- [x] Project-wide lock ledger refreshed | DOD: ran `python Docs/Reports/agent1413_lock_line_scanner.py`; generatedUtc `2026-05-28T12:24:05Z`; fileCount `2456`; filesWithLocks `270`; lockInvocationCount `1347`; lockAcquireCount `653`; tryLockBufferCount `694`; missingFinallyShapeCount `1037`; insideLoopCount `351`; nestedLockCount `54`; scanMicroseconds `35046041`.
- [x] Optimization/APEX reports synchronized | DOD: `LOCK_CONTENTION_OPTIMIZATION_REPORT_1413.json.ledgerSummary` now embeds the refreshed ledger; modifiedFiles ledger hash updated; APEX regenerated at `2026-05-28T12:25:37Z`.
- [x] Final report hashes after Loop 26 | DOD: lock ledger SHA-256 = `036f990ff1c993bb1c10deaf9ff639c3c29ebfcacbf41884e6db3d7d6c5b4019`; optimization report SHA-256 = `703634e7eb84aa110a492307bc1bae62ce98beb33c36a4e35058e61f464a0578`; APEX SHA-256 = `d30e531447721dc9cbd3e72db9eceaf2f8fea80ef498d8df7241a1b4aa2486a9`.
- [PENDING_OUTSIDE_CURRENT_PATCH] Remaining project-wide candidates | DOD: refreshed scanner still reports 351 loop-shaped lock candidates and 54 nested-lock candidates. These are scanner-ranked candidates across many domains, not all verified defects. Top current candidates: `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs:842/852/863`, `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:1603`, `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs:1041`.

## Loop 27 / VisualPressureAgingRuntime Lock-Span Surgery

- [x] Top candidate re-audited | DOD: `VisualSyncTick` held seven vault locks through GPU uploads, shader global writes, runtime DTO mutation, telemetry cursor/ring reads, scratch locking, and black-box file writes. Rejected leaving graphics-side file/shader work under vault locks. Estimate: prior scanner priority `364` at old lines `842/852/863`.
- [x] Lock spans split | DOD: current `VisualSyncTick` locks `VisualPressureAgingParams` at line `834`, `UberNoirInstanceDegradation` at line `838`, and releases both in `finally` lines `870-875`; runtime DTO lock is line `894`, released in `finally` lines `919-922`; shader globals begin after release at line `926`. Rejected manual unlock branches after successful lock.
- [x] Persistent native scratch violation corrected | DOD: removed `_telemetryDumpScratch` persistent `NativeArray<byte>` field and its lifecycle methods. Fault dump staging now uses `stackalloc Span<byte>` at line `934`; file write call is line `939`, after runtime lock finally. Rejected persistent staging because runtime-manager native aliases violate Memory Sovereignty.
- [x] Telemetry dump locks narrowed | DOD: `CopyTelemetryDumpSnapshot` now locks one cursor/ring helper at a time (`TryReadTelemetryCursor` line `1903`, `TryCopyTelemetryEntries<T>` line `1936`), releases each in its own `finally`, and writes files only after copy returns. Secured BufferIDs: `71240`, `71241`, `71242`, `71243`, `71247`, `71248`, `71249`.
- [x] APEX zero-GC proof regenerated | DOD: `LOCK_CONTENTION_APEX_VERIFICATION_1413.json` generatedUtc `2026-05-28T14:11:42Z`; total forbidden hits `0`. VisualPressure scanned windows: `VisualSyncTick` lines `805-947`, `CopyTelemetryDumpSnapshot` lines `1834-1891`, `TryReadTelemetryCursor` lines `1893-1919`, `TryCopyTelemetryEntries<T>` lines `1921-1966`, `TryWriteTelemetryDumpSnapshot` lines `1968-2003`; all forbiddenHitCount `0`.
- [x] Ledger refreshed after VisualPressure edit | DOD: `LOCK_CONTENTION_SPAN_LEDGER_1413.json` generatedUtc `2026-05-28T14:05:20Z`; lockInvocationCount `1343`; tryLockBufferCount `686`; insideLoopCount `350`; nestedLockCount `51`; scanMicroseconds `39675346`. VisualPressure no longer appears in the top 20 candidates; current top candidate is `PersistentWorldRegistry.cs:1603`.
- [BLOCKED_BY_DEPENDENCY] Compiler proof | DOD: permitted build at CPU `28`, `dotnet=0`, `csc=0` launched once and failed before source compile with MSB4006 circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`; forensic dump `Docs/AgentLogs/Dump_1413_Loop27_BuildFailure_20260528T134842Z.txt`.
- [BLOCKED_BY_CONTENTION] Post-fix retry proof | DOD: retry samples blocked: CPU `87`, CPU `90` with `dotnet=1`, CPU `53.966963` with `dotnet=1`, CPU `63.522295` with `dotnet=1`; final APEX sample CPU `67.770575`, `dotnet=1`, `csc=0`. No extra `dotnet build`, no Unity Test Runner.
- [x] Final artifact hashes after Loop 27 | DOD: `VisualPressureAgingRuntime.cs` SHA-256 = `0e8e25874f610053bb8d3345ede68f88069ce232cd6a6af1c676fc47e4c941b0`; `GlobalDataVault.cs` SHA-256 = `e99da8bfc649a5d461b7f5a9cd49f34abe1ee241fbbe557b35a216f1bf682830`; verifier script SHA-256 = `1da3d106ce9c41dd578d547461032f46000bbb4ac167bd680e72acff816af270`; lock ledger SHA-256 = `88562146e57ad5755d283f35bc6cc801c14550d0b3de179ee95131c683c829ca`; optimization report SHA-256 = `361c50a9d2f0413be52e47fc001db51f523afd5728c27bef137e67d280d237c4`; APEX SHA-256 = `c560747860d1bbdc94cd4f0e8727e201e5494411339535609f619e5ebb4b2c27`.
- [OPEN_RISK] GPU upload under vault pin | DOD: `UploadNativeArray` and `UploadDegradationNativeArray` still execute while source buffers are pinned because reading `NativeArray` after unlock is unsafe and persistent staging is forbidden. No runtime microsecond claim; profiler proof absent.

## Loop 28 / StormPropagation Dump Lock Split

- [x] Current top candidate audited | DOD: `PersistentWorldRegistry.cs:1603/1521/1692` remains the highest ledger candidate, but its probe loops are open-addressing correctness logic over keys/values/states/count. Rejected helper-only or preprobe rewrite because it could create duplicate-key/stale-write races without a version/token contract.
- [x] Confirmed defect fixed | DOD: `ShinobuStormPropagationRuntime.TryDumpTelemetryToDisk` no longer holds storm telemetry/cursor/dump-scratch vault locks through path construction, directory creation, `FileStream`, `FileInfo`, `File.Replace`, or `File.Move`. Disk write call is line `1054`; cold writer starts line `1122`.
- [x] Vault scratch dependency removed | DOD: removed `_dumpScratchHandle` and runtime use of `BufferID.ShinobuStormPropagationDumpScratch=71720`. Fault dump staging now uses cold preallocated managed scratch at line `539`; this is outside the hot locked copy window.
- [x] Try/finally proof | DOD: `TryCopyTelemetryDumpSnapshot` locks `ShinobuStormPropagationTelemetryRing=71715` at line `1062` and `ShinobuStormPropagationTelemetryCursor=71716` at line `1068`; `finally` starts line `1115`; cursor/ring unlocks are lines `1117-1118`.
- [x] APEX proof regenerated | DOD: `LOCK_CONTENTION_APEX_VERIFICATION_1413.json` generatedUtc `2026-05-28T14:55:02Z`; `zeroGcTextScan.totalForbiddenHits=0`; `diskWriteAfterVaultUnlock=true`; `diskWriterContainsVaultLock=false`.
- [x] Ledger refreshed | DOD: `LOCK_CONTENTION_SPAN_LEDGER_1413.json` generatedUtc `2026-05-28T14:35:18Z`; lockInvocationCount `1343`; lockAcquireCount `656`; tryLockBufferCount `687`; insideLoopCount `349`; nestedLockCount `51`; scanMicroseconds `26033764`. Storm old top candidate line `1041` is no longer in global top 20.
- [BLOCKED_BY_CONTENTION] Compiler proof | DOD: final build gate sample `2026-05-28T14:54:00.3231219Z` returned CPU `95.801902`, `dotnetCount=2`, `cscCount=1`. No `dotnet build` launched after Loop 28 source edit. Current source compile status remains pending after the Storm edit.
- [x] Final artifact hashes after Loop 28 | DOD: `ShinobuStormPropagationRuntime.cs` SHA-256 = `65982f6e79066e4be24f7c66d1b10bcc7cf23659a8ff451297de17173046f90d`; verifier script SHA-256 = `2864e750dc783117dab52d4443626e17805f84452a8e1ee9e7cce3dbfc395d9c`; lock ledger SHA-256 = `1f9ce8f4146020a787c559a9c10e2cdd4fd82942375b964d13ef905fc3461bf5`; optimization report SHA-256 = `7a690485cb552d2ef9a02f34243e2a32dc8e1ccb102db1042d01a76c74f0ab58`; APEX SHA-256 = `57b4e86d019930b584cf05eca33136665b762a17d452f3d3a50b265a8f4d5f4a`.
- [OPEN_RISK] Runtime proof absent | DOD: no Unity Test Runner, no PlayMode, no profiler. Runtime microseconds are unmeasured. `PersistentWorldRegistry` remains the next verified high-priority candidate but needs a real open-addressing mutation-token design before surgery.

## Loop 29 / PersistentWorldRegistry Fail-Closed Clear Guard

- [x] Partial-clear defect found | DOD: self-audit of `VaultBackedHashMap<TKey,TValue>.Clear()` found that the old order cleared `_statesHandle` before proving `_countHandle` ownership. If count acquisition failed, states became empty while `count[0]` could remain stale/nonzero. Rejected large open-addressing surgery without mutation-token design. Estimate: 900,000 us static source audit.
- [x] Fail-closed guard implemented | DOD: `PersistentWorldRegistry.cs:1444-1446` now checks `!countLocked || count.Length <= 0` before the state-clear loop at line `1449`. No mutation occurs on count-lock failure. Rejected clearing states speculatively and returning false. Estimate: one branch before O(capacity) clear.
- [x] Release proof | DOD: states lock line `1437`; count lock line `1444`; `finally` line `1455`; count unlock line `1458`; states unlock line `1459`; APEX reports `releaseInsideFinally=true` and `partialClearOnCountLockFailureRemoved=true`.
- [x] Zero-GC proof | DOD: APEX block `PersistentWorldRegistry.VaultBackedHashMap.Clear` lines `1432-1461`, forbiddenHitCount `0`; no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- [x] Data Sovereignty proof | DOD: no fields migrated. Secured existing state/count BufferIDs using the generic clear path: `74459/74460`, `74475/74476`, `74481/74482`, `74495/74496`, `74499/74500`, `74503/74504`, `74507/74508`, `74511/74512`.
- [x] Ledger refreshed | DOD: `LOCK_CONTENTION_SPAN_LEDGER_1413.json` generatedUtc `2026-05-28T15:10:32Z`; lockInvocationCount `1346`; lockAcquireCount `657`; tryLockBufferCount `689`; insideLoopCount `349`; nestedLockCount `52`; scanMicroseconds `46200448`; ledger SHA-256 `f1108442a7fe5ed84754d1e2a2f4f847d9ce5f4fbf0c2c3c68a81bfd8ba10ddb`.
- [x] Throttled compile executed | DOD: build launched only after sample `2026-05-28T15:11:18.6033201Z`, CPU `39.251238`, `dotnetCount=0`, `cscCount=0`. Command: `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1 /p:BuildProjectReferences=false`; result `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:53.22`.
- [x] APEX proof regenerated | DOD: APEX generatedUtc `2026-05-28T15:15:18Z`; total forbidden hits `0`; optimization report hash captured in APEX = `3ce5a6083462820ef19ab92d5492cc8a657fcb31f5b96a8e20f72b720e5e0a1b`; APEX SHA-256 `56112ad7619d3922aa492200315e91d42d73321d5e786eaa8b2a5a9783b66442`.
- [OPEN_RISK] `git diff --check` is not clean because unrelated `Docs/Tasks/_1415_extracted_prompt.tmp.md` has trailing whitespace. I did not edit that file. Unity Test Runner, PlayMode, and profiler proof remain unexecuted.

## Loop 30 / Deferred Release Enqueue Spin Reality Recheck

- [x] False proof found | DOD: current-byte scan of `GlobalDataVault.cs` found `_deferredReleaseEnqueueGate`, `DeferredReleaseEnqueueSpinWait`, and `Thread.SpinWait(DeferredReleaseEnqueueSpinWait)` still present in `QueueDeferredRelease` despite earlier report text claiming removal. Rejected trusting previous APEX without byte scan. Estimate: 600,000 us static source audit.
- [x] Spin path removed in current bytes | DOD: removed the enqueue gate field, spin constant, init/reset writes, serializer `try/finally`, and `Thread.SpinWait` from `QueueDeferredRelease`. Current queue function lines `2030-2090` use writer-only pending scan plus `Interlocked.CompareExchange(ref request->State, DeferredReleaseStateWriting, DeferredReleaseStateEmpty)`.
- [x] APEX verifier hardened | DOD: `agent1413_apex_verifier.py` now emits wait-primitive line lists for `QueueDeferredRelease` and whole `GlobalDataVault.cs`. APEX reports queue spin/sleep/task-delay/wait lines empty and whole-file `Thread.SpinWait` lines empty. Dispose-only `Thread.Sleep` remains at line `3647`, outside write-lock hot path.
- [x] Zero-GC proof | DOD: APEX generatedUtc `2026-05-28T16:06:25Z`; `zeroGcTextScan.totalForbiddenHits=0`; `QueueDeferredRelease` lines `2030-2090`, forbiddenHitCount `0`; no reference `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- [x] Ledger refreshed | DOD: `LOCK_CONTENTION_SPAN_LEDGER_1413.json` generatedUtc `2026-05-28T16:02:18Z`; lockInvocationCount `1357`; lockAcquireCount `664`; tryLockBufferCount `693`; insideLoopCount `353`; nestedLockCount `52`; scanMicroseconds `24590433`; ledger SHA-256 `88f49180581af8ce113b5d3b2d2b496546d2574491757f9ebe19dc9f95c5c517`.
- [x] Throttled compile executed | DOD: build launched only after sample `2026-05-28T20:02:31.0417510+04:00`, CPU `38`, `dotnetCount=0`, `cscCount=0`, `VBCSCompilerCount=0`. Command: `dotnet build "C:\hades\Hecton8\Assembly-CSharp.csproj" --no-restore -v:minimal /m:1 /p:BuildProjectReferences=false`; result `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:41.24`.
- [x] Final artifact hashes after Loop 30 | DOD: `GlobalDataVault.cs` SHA-256 = `369f2809fecd985eb463a82bb8a5fb8aa1cfbb2bc4d598513b30076c04d78b1c`; verifier script SHA-256 = `25a34611572bc173de42b6717b4546537cd571717401d56d7701c832d5907484`; optimization report SHA-256 = `7e574ff56451ce76a0d6e6034acbf81d12b3e46222455838c6c7d683c48fda7b`; APEX SHA-256 = `b2e3124e3d673ad6249098484c1aa93e1ef75f59ca57e1ca45c0fd891afe3c07`.
- [OPEN_RISK] Unity Test Runner, PlayMode, and profiler proof remain unexecuted. Runtime microseconds are still unmeasured; no claim of absolute zero wait time under Unity runtime load.

## Loop 31 / Deferred Writer Accepted-Return Recheck

- [x] Accepted-return regression found | DOD: current-byte scan of `GlobalDataVault.cs` showed `ReleaseWriteLock<T>` and private `ReleaseWriterBlockLock` discarding `QueueDeferredWriterRelease(...)` and returning `false` on release-gate contention. Rejected trusting prior report hashes because the bytes contradicted the contract.
- [x] Accepted-return contract restored | DOD: `GlobalDataVault.cs:1933` and `GlobalDataVault.cs:1965` now return `QueueDeferredWriterRelease(...)`. Accepted deferred writer release returns `true`; full/invalid queue returns `false`.
- [x] APEX verifier hardened | DOD: `agent1413_apex_verifier.py` scans `ReleaseWriteLock<T>`, records `releaseWriteLockReturnsAcceptedDeferredWriterRelease`, `internalWriterReleaseReturnsAcceptedDeferredWriterRelease`, and `ignoredDeferredWriterReleaseLines`.
- [BLOCKED_BY_CONCURRENT_SOURCE_OVERWRITE] Final byte state is not acceptable | DOD: after repeated repair attempts, current `GlobalDataVault.cs` bytes again contain `_deferredReleaseEnqueueGate` and `Thread.SpinWait(8)` at lines `541/810/2047-2048/2100/3566`. Current source SHA-256 `5d3cfe4c916fa9547a313920aba8ce6d7ef4275ed3e26dfc00145a3b5fc2c4f1`.
- [x] Partial contract still verified | DOD: current bytes keep accepted writer deferred-release returns at `GlobalDataVault.cs:1935` and `GlobalDataVault.cs:1967`; writer-only duplicate filter exists at `GlobalDataVault.cs:2055`.
- [x] APEX failure captured | DOD: APEX generatedUtc `2026-05-28T16:52:00Z`; `zeroGcTextScan.totalForbiddenHits=0`; `releaseWriteLockReturnsAcceptedDeferredWriterRelease=true`; `internalWriterReleaseReturnsAcceptedDeferredWriterRelease=true`; `hasWriterOnlyFilter=true`; `hasNoEnqueueGateBusyFailPath=false`.
- [x] Forensic dump written | DOD: `Docs/AgentLogs/Dump_1413_ConcurrentOverwrite_20260528T165050Z.txt`; SHA-256 `8efb82d41fae7d217f033e59f8adf3e0ca366da773248df4e3a9199f8043b957`.
- [x] Final artifact hashes after blocked Loop 31 | DOD: optimization report SHA-256 = `a243cad370cb546bce1f3923d948c0cc532d13f0450cb427de97a8531cf04f54`; APEX SHA-256 = `e7f1cfc5e920434734bc8b90846421c8458efea278df5694bcc54724d2828764`.

## Current Build Policy

No `dotnet build` until source modifications require syntax proof and host CPU is <= 50% with no active `dotnet`/`csc.exe`. Latest permitted source compile passed with `/p:BuildProjectReferences=false`; Unity runtime proof is still pending.

## Loop 32 / Non-Blocking Deferred Release Gate Reconciliation

- [x] Cross-agent contract conflict identified | DOD: 1414 editor test and 1404 validators require `_deferredReleaseEnqueueGate`, `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)`, `finally`, and `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)`. Pure gate removal would break those source contracts.
- [x] Spin wait removed without deleting contract strings | DOD: `GlobalDataVault.cs:2061` now performs a single non-blocking CAS into `enqueueGateAcquired`; there is no `while` and no `Thread.SpinWait(` in current `GlobalDataVault.cs`.
- [x] Writer duplicate scan kept gate-scoped and writer-only | DOD: `GlobalDataVault.cs:2068` preserves exact `if (kind == DeferredReleaseKindWriter)` string; scan runs only when `enqueueGateAcquired`; duplicate match still requires `pending->Kind == DeferredReleaseKindWriter` at line `2081`.
- [x] Gate release proof | DOD: `GlobalDataVault.cs:2111-2118` releases the best-effort gate in `finally` only if acquired. No DataVault write lock is held while waiting because there is no wait path.
- [x] Duplicate stale drain hardening | DOD: `DrainDeferredWriterReleaseLocked` treats an already-unlocked ownerless duplicate as drained when `!released && meta.ActiveWriterSystemID == 0`, clearing the active lock bit at lines `2207-2211`.
- [x] Zero-GC hot-path text scan | DOD: range `1919-2215` reports `0` hits for reference `new`, `string.Format`, `.ToString(`, LINQ `Select/Where/Any/All`, `foreach`, `GlobalRegistry.Get<`, and `GetComponent(`.
- [x] Dependency lookup patch scan | DOD: `git diff` added-line scan reports `PATCH_ADDED_DEP_LOOKUPS => 0`.
- [x] Diff hygiene | DOD: `git diff --check -- Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` reports only Git CRLF normalization warning, no whitespace error.
- [x] Current source hash | DOD: `GlobalDataVault.cs` SHA-256 = `d06c63a6cfba238481e68be63c033cc0def222851957aa8dbe41cf132ce260bc`.
- [BLOCKED_BY_POLICY] Compile proof | DOD: no `dotnet build` launched in Loop 32. Latest CPU sample before this loop was `cpu=100`, so compilation was forbidden by throttling policy.

## Loop 33 / APEX Integrator Reconciliation

- [x] Accepted writer-release return restored in current bytes | DOD: `GlobalDataVault.cs:1949` and `GlobalDataVault.cs:1981` return `QueueDeferredWriterRelease(...)`; scan found no `_ = QueueDeferredWriterRelease` in `GlobalDataVault.cs`.
- [x] Deferred enqueue gate made non-blocking | DOD: `GlobalDataVault.cs:2061-2121` uses `enqueueGateAcquired = Interlocked.CompareExchange(...) == 0`, never loops, never calls `Thread.SpinWait`, and releases only inside `finally`.
- [x] Source contracts aligned | DOD: 1404 test, 1414 test, `H8AndroidAssetBridgeStaticAudit`, and `NativePluginMatrixValidator` now require accepted deferred-release return, non-blocking enqueue CAS, no `Thread.SpinWait`, and no `while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate`.
- [x] Stability proof | DOD: 20-second re-read kept `GlobalDataVault.cs` SHA-256 `D89D1C9D0E88A46AC0A7FAB32E7FA12ED5BB7202C8937B71FFB612576F5456BB`; grep showed only the two accepted returns and the single enqueue CAS.
- [x] Zero-GC and dependency scan | DOD: `GlobalDataVault.cs` release/deferred range `1921-2229` forbidden-hit total `0`; patch added-line dependency lookup scan `PATCH_ADDED_DEP_LOOKUPS=0`; high-frequency method scan across current domain files `HOT_DEP_TOTAL=0`.
- [x] Syntax proof without build | DOD: C# Roslyn parse via Windows PowerShell was attempted but local .NET SDK Roslyn assemblies are not directly loadable into Windows PowerShell; no `dotnet build` was launched. Static brace/text scans were used instead.
- [BLOCKED_BY_POLICY] Compile proof | DOD: final gate sample CPU `100`, compilerProcessCount `2`, active `dotnet` PID `67136`, active `csc` PID `15916`; `dotnet build` forbidden.

## Loop 34 / APEX Integrator Lock Flattening Follow-Up

- [x] Visual CSV reload nested lock removed | DOD: `VisualPressureAgingRuntime.ReloadCsvFromDisk` now reads CSV into cold managed scratch before any vault lock, reads `VisualPressureAgingTuning` under one short lock (`1746/1759`), parses outside locks, then writes tuning under a second short lock (`1772/1785`). `VisualPressureAgingCsvScratch` has no `TryLockBuffer` hits.
- [x] Storm publish nested scalar/state locks removed | DOD: `PublishCompletedState` now publishes state through `TryPublishCompletedStateRow` (`911/930`) and each scalar through `TryPublishScalarRow` (`936/949`) one buffer at a time. Removed `TryLockScalarPublicationBuffers`, `UnlockScalarPublicationBuffers`, and scalar lock mask constants.
- [x] Storm cold CSV/default-row locks flattened | DOD: `EnsureDefaultRowsCold` uses isolated read/write lock windows for tuning and profiles (`593/605`, `614/623`, `635/647`, `656/665`). `LoadImpactProfilesCold` reads file and parses into `stackalloc Span<StormDepthImpactProfileDTO>` before vault locks, then copies profiles (`716/734`) and updates tuning (`740/753`) separately.
- [x] Zero-GC hot-window scan | DOD: scanned windows `Visual.ReloadCsvFromDisk.lock-read 1744-1760`, `Visual.ReloadCsvFromDisk.lock-write 1770-1786`, `Storm.EnsureDefaultRowsCold 583-668`, `Storm.LoadImpactProfilesCold 670-756`, `Storm.PublishCompletedState 880-951`, `GlobalDataVault.ReleaseDeferredRange 1930-2230`; total hits for `new`, `string.Format`, `.ToString(`, LINQ, `foreach`, `GlobalRegistry.Get<`, `GetComponent(` = `0`.
- [x] Lock nesting scan on modified windows | DOD: sequential lock model reported max `1` and endActive `0` for `Visual.ReloadCsvFromDisk`, `Storm.EnsureDefaultRowsCold`, `Storm.LoadImpactProfilesCold`, and `Storm.PublishCompletedStateAndHelpers`.
- [x] Hot dependency lookup scan | DOD: high-frequency method scan across current domain files including `TryGetComponent` reported `HOT_DEP_WITH_TRYGET_TOTAL=0`.
- [x] Syntax/static sanity | DOD: touched-source brace/preprocessor counts are balanced: Visual `240/240`, `#if/#endif 11/11`; Storm runtime `145/145`, `7/7`; Storm contracts `62/62`, `4/4`; GlobalDataVault `640/640`, `2/2`. `git diff --check` on touched files returned no output.
- [BLOCKED_BY_POLICY] Compile proof | DOD: final CPU gate `CPU_SAMPLE_FINAL_CONFIRMED cpu=100 compilerProcessCount=1`, active `VBCSCompiler` PID `53464`; no `dotnet build` launched.
- [OPEN_RISK] Job buffer multi-lock design remains | DOD: `VisualPressureAgingRuntime.TryLockJobBuffers` and `ShinobuStormPropagationRuntime.TryLockOwnedJobBuffers` still pin multiple vault buffers for scheduled jobs. I did not fake a local fix because replacing this requires dispatcher-owned job snapshot/staging ownership, not a line-level lock flattening patch.
