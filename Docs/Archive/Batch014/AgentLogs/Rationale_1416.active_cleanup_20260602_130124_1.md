# Rationale 1416 - MODULAR_EQUIPMENT_AND_TOOL_RUNTIME_PURGER

Date: 2026-05-28
Status: PATCHED_STATIC_REAUDITED_LIVE_REFCOUNT_FIXED_BUILD_BLOCKED_AFTER_TIMEOUT

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

## Decision 010 - Release Against Captured Vault
Problem: `ReleaseEquipmentWriteLocks(ref views)` reread `_dataVault` at release time. That is usually identical inside a Unity main-thread phase, but it is not a formal proof under hot-swap/rebind pressure.
Solution: Add `EquipmentVaultViews.Vault` and set it at successful view resolution/acquisition. Direct `finally` releases and scheduled integration capture now release against the exact vault that produced the views.
Rejected Alternatives: Assuming `_dataVault` cannot change during a short phase was rejected because the APEX proof must survive registry rebind reasoning. Storing raw `NativeArray` owners on the class was rejected because it would reintroduce cross-frame aliases.
Scalability potential: No quality tier behavior changed. Low/Middle/High/Ultra use the same ownership route; quality scaling remains in cadence and visual/presentation paths.
Hardware Impact: One stack reference copy per acquisition; no heap allocation and no measured CPU claim.

## Decision 011 - Isolated Harness Registry Guard
Problem: The explicit mock harness registered a new `GlobalDataVault` without checking whether the Editor session already had an authoritative `DataVault` or `ModularEquipment` service. `GlobalRegistry.RegisterService` throws on slot hijack.
Solution: Guard the `[Explicit]` harness with `Assert.Ignore` when `GlobalRegistry.DataVault` or `GlobalRegistry.ModularEquipment` is already occupied. The harness now documents and enforces isolated execution.
Rejected Alternatives: Force-overriding registry services was rejected because the public GlobalRegistry override path is not part of this domain and would risk cross-agent test contamination. Unregistering somebody else's service was rejected as architectural sabotage.
Scalability potential: Runtime unchanged. Verification is safer across shared development machines.
Hardware Impact: Runtime impact 0; Editor-only branch.

## Decision 012 - Build Timeout Cleanup
Problem: After the final CPU gate improved to 28% and no compiler process was active, one throttled `dotnet build` was justified. It timed out after 124 seconds and left `dotnet` pid 10444 plus `VBCSCompiler` pid 67176 running.
Solution: Stop only the build process spawned by this verification attempt and record the result as `BLOCKED_BY_TIMEOUT`, not success.
Rejected Alternatives: Launching a second build was rejected as build spam. Reporting success from a timed-out build was rejected as false evidence.
Scalability potential: Preserves host resources for the parallel batch.
Hardware Impact: One failed compiler attempt consumed more than 124 seconds wall-clock; no build diagnostics were captured.

## Decision 013 - APEX Proof Identity Re-Audit
Problem: Cold layout validator exception text still carried stale `[SHINOBU_327]` identity even though the equipment fault dump and proof artifacts are agent 1416-owned. Static proof also needed a fresh check that the write-lock acquisition helper and test contract still agreed.
Solution: Re-audited `TryAcquireEquipmentViewsWriteLock`, `TryAcquireEquipmentWriteBuffer`, release mask, contention telemetry, and the EditMode static tests. Runtime/test contract is aligned: `TryAcquireEquipmentWriteBuffer` takes `ref int acquiredCount` and increments immediately after successful `TryAcquireWriteLock`. Changed only the cold validator exception identity to `[1416]`.
Rejected Alternatives: Leaving stale validator identity was rejected because APEX evidence paths must point at the current owner. Widening the runtime diff was rejected after the source already matched the safer ref-count acquisition contract.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; quality scaling still uses continuous `HomeostasisBrain.GlobalQualityWeight` for cadence, thermal approximation blend, upgrade visual states, and presentation intensity.
Hardware Impact: Hot path cost unchanged. Latest build gate was blocked at CPU 59% with sampled `dotnet` pid 5388; no new compiler CPU was consumed.

## Decision 014 - Equipment Telemetry Offset Proof Gap
Problem: The proof JSON listed `EquipmentTelemetryEntry` field offsets, but `EquipmentLayoutVerifier` only asserted `UnsafeUtility.SizeOf<EquipmentTelemetryEntry>() == 64`. That was not enough for the APEX offset requirement.
Solution: Add `AssertOffset<EquipmentTelemetryEntry>` calls for all 16 fields: Frame 0, TickIndex 4, BatteryDrainWattSeconds 8, GridDrawWattSeconds 12, PeakThermal01 16, ActiveToolMask 20, SignalCount 24, FaultFlags 28, LastFaultToolHashID 32, CpuMicroseconds 36, GlobalQualityWeight 40, TickIntervalSeconds 44, ThermalGridVersion 48, ThermalGridCellCount 52, SnapshotHash 56, WearDrainNormalized 60.
Rejected Alternatives: Trusting `[FieldOffset]` source text or the JSON report alone was rejected because the validator is the runtime guard against future field drift.
Scalability potential: No runtime scaling behavior changed. Low/Middle/High/Ultra still consume the same telemetry layout; continuous quality data remains at offset 40.
Hardware Impact: Hot path cost 0. The assertions run only in the cold validator path already called during initialization.

## Decision 015 - Exception-Safe Acquisition Accounting
Problem: The 28-buffer acquisition chain released partial locks on normal `false` paths, but the lock count was previously incremented by the caller after `TryAcquireEquipmentWriteBuffer` returned. If `TryAcquireWriteLock` succeeded and a later wrapper construction or validation threw, the caller could miss that acquired lock.
Solution: Move `acquiredCount++` into `TryAcquireEquipmentWriteBuffer` immediately after successful `TryAcquireWriteLock`, pass the counter by `ref`, and wrap the full acquisition chain in `try/finally`. The `finally` releases `ReleaseEquipmentWriteLocks(vault, acquiredCount)` unless all 28 buffers were acquired and handed to the caller.
Rejected Alternatives: Keeping the old `goto AcquireFailed` pattern was rejected because it only proved normal fail paths. Catch-and-swallow exceptions was rejected because structural vault exceptions should not be hidden; the lock release proof belongs in `finally`.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged on successful acquisition. Under contention or structural failure, the system fails closed and retries next frame without freezing DataVault write locks.
Hardware Impact: Successful hot path adds one boolean flag and one `try/finally` region; no managed allocation. Latest build gate was blocked at CPU 97% with sampled `dotnet` pid 2980; no new compiler CPU was consumed.

## Decision 016 - Disabled Lifecycle Must Drain Writer Locks
Problem: `OnDisable` unregistered the runtime before proving that a scheduled equipment integration job and its captured write locks were drained. A disabled object has no guaranteed future `Tick`/`LateFrameTick` to retry pending releases.
Solution: Call `DrainEquipmentIntegrationLocksForLifecycle()` before unregistering, force-complete scheduled integration only on lifecycle teardown, then immediately call `TryFlushPendingEquipmentWriteLockReleases()`.
Rejected Alternatives: Waiting for the next frame was rejected because disabled services may never receive one. Force-completing every late frame was still rejected because it can stall low-end hardware.
Scalability potential: Low devices avoid a permanent writer freeze after disable; Middle/High/Ultra keep the same continuous quality path and do not gain extra simulation scope.
Hardware Impact: Runtime hot path unchanged. Lifecycle path adds one pending-mask retry; latest build gate was CPU 96% with active `dotnet`/`VBCSCompiler`, so no compiler CPU was consumed by this pass.

## Decision 017 - ReleaseBuffer Failure Must Not Erase Ownership Handles
Problem: `GlobalDataVault.ReleaseBuffer` returns `false` when `ActiveWriterSystemID != 0`, but equipment teardown previously defaulted the `VaultGenerationHandle` regardless of release success. That loses the final route to a still-owned native buffer.
Solution: Make `ReleaseEquipmentVaultHandle` return `bool`, default the handle only after `ReleaseBuffer` succeeds, aggregate all 28 releases in `ReleaseEquipmentVaultHandles`, and clear all handles only when pending writer releases and buffer releases both succeed.
Rejected Alternatives: Ignoring `ReleaseBuffer` results was rejected as a false leak-free proof. Throwing from lifecycle release was rejected because Unity teardown must fail closed and allow a later retry.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged during normal frames. On teardown/rebind failure, the system preserves ownership evidence instead of corrupting the DataVault route graph.
Hardware Impact: Cold lifecycle release adds 28 boolean aggregations and one handle-existence scan if the vault is null; no managed allocation.

## Decision 018 - Recreate/Rebind Must Fail Closed
Problem: `EnsureEquipmentBuffer` could overwrite an existing handle with `EnsureGenerationHandle` after an unchecked release attempt, and `DisposeNativeState` could clear fault-dump pending flags after a release failure.
Solution: `EnsureEquipmentBuffer` now checks `ReleaseEquipmentVaultHandle` before replacing a handle and no longer defaults handles on null/invalid inputs. `ApplyDataVaultRebind` returns without switching vaults if old handles cannot be released. `DisposeNativeState` performs a second fault-dump flush after release attempts and clears pending flags only when handles are released.
Rejected Alternatives: Auto-switching to the new vault after partial old-vault release was rejected because it hides a leak. Clearing dump flags unconditionally was rejected because it destroys forensic state.
Scalability potential: The fix preserves one owner/one route across weak, middle, high, and ultra devices; quality scaling remains in `HomeostasisBrain.GlobalQualityWeight`, not in lifecycle policy.
Hardware Impact: Cold-only branches. Static proof shows zero reference-new candidates, zero `string.Format`, zero `.ToString()`, zero LINQ, and zero `foreach` in the modified audited methods.

## Decision 019 - Module Mutation Must Not Commit Without Vault Staging
Problem: `TryInstallModule` and `TryRemoveModule` wrote `_moduleRuleSlots`, called `RebuildCompiledState`, and returned `true` even though `RebuildCompiledState` returned `void` and silently exited on DataVault write-lock refusal. `WriteUpgradeMatrixStaging` also returned `void`, so invalid upgrade staging buffers could be ignored.
Solution: Convert `RebuildCompiledState` to `bool` and pass the candidate module rule buffer directly. Convert `WriteUpgradeMatrixStaging` to `TryWriteUpgradeMatrixStaging`, prevalidate the upgrade rule range before any staging writes, and commit `_moduleRuleSlots` only after staging and state rebuild succeed. `RegisterTool` now checks staging before assigning `_toolOwners`, `_slotUsed`, `ToolStates`, or `ToolStats`.
Rejected Alternatives: Rolling back `_moduleRuleSlots` after a failed rebuild was rejected because it still creates a transient false commit. Adding a second managed rule scratch array was rejected because the existing `_registrationRules` buffer already carries the candidate rules without GC. Treating module install as eventually consistent was rejected because upgrade matrix and tool stats are gameplay truth, not presentation.
Scalability potential: Weak devices avoid corrupted module truth after contention. Middle, high, and ultra devices keep the same continuous `HomeostasisBrain.GlobalQualityWeight` scaling; no binary quality switch or physical simulation was added.
Hardware Impact: Module mutation pays one existing 28-buffer lock group and stat compile only when the player changes modules. Hot frame cadence is unchanged. Latest build gate was CPU 100% with active `csc` pid 29640 and `dotnet` pid 23460; no compiler CPU was consumed by this pass.

## Decision 020 - RegisterTool Must Use One Module Slot Count
Problem: `RegisterTool` compiled runtime stats with `min(authoredRules, profile.ModuleSlotCount)` but wrote `ModuleSlotCount`, `_moduleRuleSlots`, and upgrade staging with the full authored count. Extra authored module entries could appear in the upgrade mask/staging while compiled stats ignored them.
Solution: Clamp the copied authored rule count once with `min(authoredRules, profile.ModuleSlotCount, ToolUpgradeSystem.MaxModuleSlots)` and use that single count for `CompileRuntimeStatsFromRules64`, `ToolState.ModuleSlotCount`, `TryWriteUpgradeMatrixStaging`, and `WriteModuleRuleMirror`.
Rejected Alternatives: Leaving staging to clamp only by `MaxModuleSlots` was rejected because profile slot limits are gameplay truth. Recomputing different counts per write target was rejected because it recreates the drift.
Scalability potential: Low, middle, high, and ultra devices now receive the same deterministic module truth. Visual overkill remains downstream in quality-scaled presentation, not in extra unauthorized module stats.
Hardware Impact: Cold/register path only. No per-frame cost and no GC. Static hot-path scan after the fix still reports 0 reference-new candidates, 0 `string.Format`, 0 `.ToString()`, 0 LINQ, and 0 `foreach` for `RegisterTool`.

## Decision 021 - Acquisition Cold Create Must Use Captured Vault
Problem: `TryAcquireEquipmentViewsWriteLock` captured `IDataVault vault = _dataVault`, but its `createIfMissing` branch called `EnsureEquipmentViews(out _, createIfMissing: true)`, which reread `_dataVault`. Under normal Unity main-thread flow this is unlikely to differ, but it weakens the formal one-owner/one-route proof during registry rebind pressure.
Solution: Route the cold-create branch through `EnsureEquipmentViews(vault, out _, createIfMissing: true)` and add a static test guard for the exact captured-vault call.
Rejected Alternatives: Relying on `_dataVault` not changing after local capture was rejected because the rest of the release proof already uses captured vault identity. Passing through the parameterless helper was rejected as a hidden dependency on mutable manager state.
Scalability potential: Low, middle, high, and ultra devices keep identical behavior. This only strengthens ownership routing; no quality switch or simulation path changed.
Hardware Impact: Cold create path only. Runtime hot path remains unchanged after buffers exist. No new allocation and no build launched because the latest build gate remains CPU 100% with active compiler processes.

## Decision 022 - Module Rebuild Battery Must Stay Inside Captured Views
Problem: `RebuildCompiledState` held the 28-buffer write-lock set, then called public `GetBatteryNormalized`, which reads through `_dataVault` instead of the captured `EquipmentVaultViews`. That was a mixed-route dependency inside a write phase and could preserve the wrong battery fraction under rebind pressure.
Solution: Preserve battery fraction directly from `views.ToolStates[slotIndex]` and `views.ToolStats[slotIndex]`, then apply the newly compiled capacity. Removed the now-unused `toolId` parameter from `RebuildCompiledState`.
Rejected Alternatives: Keeping the public accessor was rejected because public reads must be consumer routes, not internal write-phase dependencies. Falling back to `owner.ResolveModularBatteryNormalized()` on every module change was rejected because it can reset real runtime battery state.
Scalability potential: Low, middle, high, and ultra devices keep deterministic battery truth through module swaps. No binary quality switch or new physical simulation was introduced.
Hardware Impact: Module mutation removes two read-only DataVault helper calls and one slot lookup from rebuild. No measured microsecond claim; latest build gate was CPU 17% but active `VBCSCompiler` pid 14544 blocked compilation.

## Decision 023 - Second Build Attempt Must Be Reported As Timeout
Problem: After the battery rebuild signature change, static proof was not enough to rule out a C# compile break. The gate later cleared to CPU 16% with no active compiler processes, making one more throttled build justified.
Solution: Ran `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. It timed out after 304 seconds with no diagnostics. Stopped the leftover `dotnet` pid 68368, child `dotnet` pid 48280, child `dotnet` pid 11928, and `VBCSCompiler` pid 53788, then recorded the result as timeout, not success. A later post-resume check observed transient `dotnet` pid 14652 plus `csc` pid 57928 from `Hecton8.Editor.csproj`; both exited before further cleanup or any third build. A subsequent sample saw external compile-medic `dotnet` pid 15860 and `csc` pid 46524; those were not killed because they were not spawned by 1416.
Rejected Alternatives: Declaring compile success from a timed-out build was rejected. Launching a third build was rejected because two timed-out attempts are enough evidence of host/build-system blockage for this pass.
Scalability potential: Host resource protection for the 20+ agent batch takes priority over repeated compiler attempts. Runtime quality scaling remains unchanged.
Hardware Impact: The second attempt consumed 304 seconds wall-clock before timeout. Post-cleanup CPU was 44% and no compiler/dotnet processes were visible; later build gate sample was CPU 90% with external compile-medic `dotnet`/`csc` active, so 1416 did not launch another build.

## Decision 024 - Acquisition Refcount Proof Must Match Source
Problem: A post-report static read exposed a false proof: `ModularEquipmentEngine1416EditTests` required `TryAcquireEquipmentWriteBuffer` to take `ref int acquiredCount` and increment inside the helper, but the live source still incremented `acquiredCount` in `TryAcquireEquipmentViewsWriteLock` after each helper returned. If wrapper construction or validation threw after `TryAcquireWriteLock`, the outer `finally` could under-release.
Solution: Move the counter into `TryAcquireEquipmentWriteBuffer` as `ref int acquiredCount`, increment immediately after successful `TryAcquireWriteLock`, pass `ref acquiredCount` from all 28 acquisition calls, and remove helper-local `ReleaseWriteLock` on invalid buffer length so the outer `finally` is the single release route.
Rejected Alternatives: Keeping caller-side increments was rejected as a false exception-safety proof. Releasing inside the helper after incrementing was rejected because the outer `finally` would double-release by count.
Scalability potential: Low, middle, high, and ultra devices keep the same deterministic ownership route. This is stability hardening, not a quality switch or new simulation path.
Hardware Impact: Adds one integer increment inside the helper per acquired buffer, equal to the previous caller-side cost. After the external build cleared, the gate was CPU 45% with no compiler/dotnet process, so one final build was run. It exited code 1 after 128.6 seconds with no diagnostics emitted; immediate post-build CPU was 99% with no compiler/dotnet process visible. A later final sample saw external compile-medic `dotnet` pid 27364 and `csc` pid 18240 at CPU 77%; those were not killed because they were not spawned by 1416.

## Decision 025 - Live Refcount Patch Required Direct Line Proof
Problem: The prior proof was still false in the live file after context resume. Direct `Select-String` showed `TryAcquireEquipmentWriteBuffer(vault, in _toolStatesHandle, MaxTrackedTools, out views.ToolStates)` and caller-side `acquiredCount++` at the acquisition callsite, plus helper-local `ReleaseWriteLock(in handle)`.
Solution: Reapply the refcount patch to the actual current file and verify by direct line scan: `Assets/_Project/Scripts/ModularEquipmentEngine.cs:1290-1345` now passes `ref acquiredCount` on all 28 calls, and `Assets/_Project/Scripts/ModularEquipmentEngine.cs:1338-1362` increments inside the helper immediately after `TryAcquireWriteLock`.
Rejected Alternatives: Trusting the JSON report, test expectations, or a summary was rejected because they had already diverged from source. Running another build after the final patch was rejected because the latest final gate had CPU 73% plus active external `dotnet` pid 31496 running `dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false`.
Scalability potential: Low, middle, high, and ultra devices all benefit from deterministic release accounting under structural fault. No quality-tier switch or physical simulation was added.
Hardware Impact: Hot path keeps the same single integer increment cost, moved to the only point where lock ownership becomes true. Build attempt 4 consumed 364 seconds wall-clock before this final live-source patch and timed out with no diagnostics; therefore the final patch currently has static proof only.

## Decision 026 - Proof Artifact Must Carry Current Source Hash
Problem: The JSON report sidecar matched the report file, but the report's `modifiedFiles` section still carried the prior SHA-256 for `ModularEquipmentEngine.cs` after the final live-source refcount reconciliation.
Solution: Rehash the current source (`063612621D2481073E347F779D3DED0D7540E424259ABF60A74AB9CBB38C878C`), update the JSON report, and refresh `Docs/Reports/EQUIPMENT_MEMORY_OPTIMIZATION_REPORT_1416.sha256` to `625DB53A15545A86C57D77A5E7176B5095BD28DC4771BE0AE99B714662166A61`.
Rejected Alternatives: Reporting the old sidecar as final proof was rejected because the report would be internally stale even if its own hash was valid.
Scalability potential: No runtime behavior changed. This is evidence integrity only.
Hardware Impact: 0 runtime cost. Latest compiler gate was CPU 76% with active external `dotnet` pid 31496, so no build was launched.
