# Rationale 1416 - MODULAR_EQUIPMENT_AND_TOOL_RUNTIME_PURGER

Date: 2026-05-28
Status: PATCHED_STATIC_REAUDITED_IMPLICIT_ALIAS_REMOVED_BUILD_BLOCKED_BY_CONTENTION

## Decision 001 - Static Archaeology Before Mutation
Problem: The batch claims 28 forbidden native aliases in `ModularEquipmentEngine.cs`, but the live source already used `VaultGenerationHandle<T>` for persistent ownership.
Solution: Treat the remaining 28 direct `NativeArray<T>` declarations in `EquipmentVaultViews` as the hit list because they are in the manager file and carry physical views. Replace them with `EquipmentVaultView<T>` stack wrappers.
Rejected Alternatives: Reintroducing direct local `NativeArray<T>` fields was rejected. Ignoring the 28 view fields was rejected because the batch wording demands zero direct manager native fields.
Scalability potential: Low/Middle/High/Ultra all use identical DataVault ownership; presentation fidelity remains controlled by continuous `GlobalQualityWeight`.
Hardware Impact: Low-end i3/MX350 gains are stability-oriented, not measured frame-time wins; direct pointer alias exposure is reduced.

## Decision 002 - Use Existing BufferID Ranges
Problem: The prompt suggests a new contiguous range, but the project already owns equipment ranges `71300-71318` and upgrade matrix ranges `71380+`.
Solution: Reuse existing route cards and add no new global BufferID churn. Flashlight telemetry remains `(BufferID)71317/71318`, immediately after `ShinobuActiveEquipmentWearDrainRates = 71316`.
Rejected Alternatives: Creating `1416000+` route constants was rejected because it would duplicate established project IDs and increase cross-system collision risk without functional gain.
Scalability potential: Stable routes avoid migration work in low memory devices and preserve tooling assumptions on high-end profiles.
Hardware Impact: No runtime cost; lower integration risk.

## Decision 003 - Fixed-Order Write Locks
Problem: Mutation must not resolve mutable physical views without DataVault writer discipline, and exceptions must not freeze write locks.
Solution: Add `TryAcquireEquipmentViewsWriteLock` with fixed acquisition order over 28 handles and reverse-order release through `ReleaseEquipmentWriteLocks`. Every mutation call-site is enclosed in `try/finally`.
Rejected Alternatives: Per-method ad hoc lock order was rejected because deadlock proof becomes local and fragile. Holding locks as class fields was rejected because the prompt forbids cross-frame cached views.
Scalability potential: Low devices pay metadata lock checks only during mutation cadence. High/Ultra keep visual overkill in shader/presentation paths, not gameplay truth expansion.
Hardware Impact: Estimated low-end overhead is O(28) DataVault lock checks per equipment mutation phase; no profiler capture, so no fake microsecond claim.

## Decision 004 - Read-Only Presentation
Problem: A full write lock in `LateFrameTick` would be harmful because presentation only reads failure state for shader globals.
Solution: Convert late-frame failure presentation to `TryReadOnlyHandle` active state/counter reads and avoid write locks.
Rejected Alternatives: Keeping full write lock in presentation was rejected as needless contention. Copying data to managed presentation buffers was rejected as GC and stale-state risk.
Scalability potential: Low avoids per-frame write contention; Middle/High/Ultra can spend quality scalar on shader intensity without mutating gameplay buffers.
Hardware Impact: Removes 28 lock checks from the per-frame presentation path.

## Decision 005 - Build Gate Is Binding
Problem: Final compilation check is desirable but user explicitly forbids build spam and the machine reported CPU at 100% with active `dotnet` pid 40436.
Solution: Do not launch `dotnet build`; record `BLOCKED_BY_CONTENTION`; rely on static checks (`rg`, brace balance, diff check, lock/release counts).
Rejected Alternatives: Running build under 100% CPU was rejected as violation of batch coordination.
Scalability potential: Preserves host resources for parallel agents.
Hardware Impact: Compiler CPU consumed by this pass: 0.

## Decision 006 - Scheduled Job Lock Lifetime Re-Audit
Problem: The first patch let `Tick` schedule Burst work using locked DataVault views, then `CompleteActiveEquipmentJob` tried to reacquire all 28 locks. That was logically wrong: if the locks are already held for a pending job, reacquire can fail or double-block, and `LateFrameTick(forceComplete: true)` can serialize job work on the main thread.
Solution: Capture the exact integration lock count at schedule time, complete naturally in the dispatcher late-frame swap window, resolve completion views through `TryResolveCapturedEquipmentIntegrationViews`, and release the captured locks in a `finally` after `TryFinalizeCompleted`/forced teardown completion. Shutdown and vault rebind remain allowed to force-complete because they are structural teardown paths.
Rejected Alternatives: Reacquiring locks at completion was rejected because it contradicts the lock owner. Keeping `LateFrameTick` forced was rejected because it can stall MX350/i3 main thread. Releasing locks immediately after scheduling was rejected because Burst jobs would keep raw native pointers after the vault could relocate them.
Scalability potential: Low devices avoid forced per-frame main-thread completion. Middle/High/Ultra keep the same truth buffers while visual density remains controlled by continuous `GlobalQualityWeight`.
Hardware Impact: Expected low-end gain is stall avoidance, not measured; build/profiler proof is still blocked by host CPU contention.

## Decision 007 - Release Failure Mask
Problem: `GlobalDataVault.ReleaseWriteLock` returns `bool`; ignoring 28 return values makes a false proof of release.
Solution: Convert count-based release into a 28-bit release mask. Failed releases are retained in `_equipmentPendingReleaseMask` and retried before any future acquisition. A release-failure fault flag (`1u << 6`) is written to telemetry when possible.
Rejected Alternatives: Blind release calls were rejected. Throwing on release failure was rejected because gameplay hot paths must fail closed, not crash.
Scalability potential: The retry mask has constant cost and no managed allocation on all device classes.
Hardware Impact: One `uint` mask and one vault reference; no heap allocation.

## Decision 008 - Explicit Job Alias Escape
Problem: `EquipmentVaultView<T>` had an implicit conversion to `NativeArray<T>`, which made job lifetime aliasing easy to hide in call sites.
Solution: Upgrade matrix job scheduling and the editor-only hardware CSV parser now call `.AsNativeArray()` explicitly. The implicit `NativeArray<T>` conversion was removed from `EquipmentVaultView<T>`.
Rejected Alternatives: Leaving implicit job arguments was rejected because audit evidence would be ambiguous. Keeping the implicit conversion for convenience was rejected after static scan found the only non-explicit consumer.
Scalability potential: No runtime cost; improves review correctness across low/middle/high/ultra targets.
Hardware Impact: No measurable CPU change.

## Decision 009 - Explicit Mock Harness Without Runtime Claim
Problem: Task 16 required a mock equipment stress harness, but CPU remained at 100% with active `dotnet` pid 55080, so running build or Editor tests would violate the throttle rule.
Solution: Add an EditMode test file with two static contract tests and one `[Explicit]` 1024-frame mock harness that creates a `GlobalDataVault`, registers it, initializes `ModularEquipmentEngine`, runs mock generation/Tick/LateFrameTick, and cleans up in `finally`.
Rejected Alternatives: Marking the harness as executed was rejected because no runtime artifact exists. Auto-running it under current CPU contention was rejected. Hiding it inside production code was rejected because it would pollute runtime surfaces.
Scalability potential: The harness protects the same continuous `GlobalQualityWeight` equipment path without adding runtime branches. Low/Middle/High/Ultra behavior is unchanged until tests are explicitly run.
Hardware Impact: Runtime build impact is 0 because the harness is Editor test code. Verification impact is pending; no profiler or GCMonitor sample exists.
