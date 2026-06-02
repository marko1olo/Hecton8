# Status 1626 - APEX_NATIVE_ALLOCATION_AND_REPLAY_DETERMINISM_VERIFIER

Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE with deterministic replay support in physics smoke validation.
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`, `<AGENT_PROMPT id="1626">`, extracted by CLI.
Task count: 20.
Build rule: no `dotnet build` unless CPU is below 50%, no `dotnet`/`csc.exe` is running, and Burst verification is materially blocked without it.

## Mandate Set
- `CORE_Global_State_Reset_NonReload_Transitions`
- `OPT_HectonArenaAllocator_2_0`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `MATH_AUP_Determinism_Sync`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`
- `PHYS_Physics_Integrity_Determinism_ForceMode`
- `CORE_Submarine_Vehicles_Kinematics_AUP`
- `OPT_Zero_GC_Policy_AllocFree_Mandate`

## Loop 1 - Tasks 1-5
- [x] Task 01 - Static owner-map scan for native reset surfaces.
  - DOD practice: direct source scan of `H8Memory`, `GlobalDataVault`, `NativeMemorySentinel`, KCC smoke tests, ballast contracts, and active architecture docs.
  - Rejected alternative: archive reports and stale JSON reports; they are not authority for live code.
  - Microsecond estimate: reset memclear cost is cold-path only; runtime frame cost 0 us.
- [x] Task 02 - JNI surface inventory.
  - DOD practice: `rg` scan across active scripts for `AndroidJNI`, `AndroidJava*`, `DllImport`, attach/detach symbols, and FP-control markers.
  - Rejected alternative: adding a new JNI wrapper without finding the existing route; direct dependencies would increase drift.
  - Microsecond estimate: existing route is load-time only; target hot-frame cost 0 us.
- [x] Task 03 - Deterministic replay insertion point selected.
  - DOD practice: use existing KCC smoke validation partial and DataVault-owned DTO style; no new scene polling or GlobalRegistry hot path.
  - Rejected alternative: new runtime manager MonoBehaviour; it would add ownership ambiguity and scene lifecycle drift.
  - Microsecond estimate: replay validator remains offline/editor or smoke-gate batch work; target player hot-frame cost 0 us.
- [x] Task 04 - Native reset design selected.
  - DOD practice: SubsystemRegistration reset must complete jobs, dispose latest DataVault, clear native storage, dispose, then assign static fields to `default`/0/null.
  - Rejected alternative: relying on `Dispose()` side effects only; domain reload disabled leaves stale static handles.
  - Microsecond estimate: cold Play Mode transition cost only; frame cost 0 us.
- [x] Task 05 - Proof artifact format fixed.
  - DOD practice: status/rationale/log files only; no fresh JSON report because coordinator explicitly rejected useless JSON dumps.
  - Rejected alternative: machine report generation with no runtime enforcement.
  - Microsecond estimate: no runtime cost.

## Loop 2 - Tasks 6-10
- [x] Task 06 - Patch `H8Memory` SubsystemRegistration allocator reset.
  - DOD practice: `ResetForSubsystemRegistration()` now routes through `Shutdown()` and `ResetStaticValueState()`, completes owner jobs, clears tracking memory, releases alias safety, and assigns mutable statics to zero/default/null.
  - Rejected alternative: cursor-only reset; it would leave native containers and safety handles alive across no-domain-reload restarts.
  - Microsecond estimate: cold transition only, 0 us hot frame.
- [x] Task 07 - Patch `GlobalDataVault` latest-vault reset and native clear.
  - DOD practice: latest vault pointer is nulled before disposal, backing arena/maps/telemetry rings are `UnsafeUtility.MemClear`ed before release, and SubsystemRegistration clears `_latestCreated`.
  - Rejected alternative: relying on object finalization/dispose ordering; stale bootstrap vault identity is a replay contamination route.
  - Microsecond estimate: cold linear clear only, 0 us hot frame.
- [x] Task 08 - Patch sentinel reload reset and replay ingress wiring.
  - DOD practice: `NativeMemorySentinel.ResetForSubsystemReload()` clears edit-mode tracking records without masking runtime shutdown leak asserts; `InputDispatcher` now writes `ReplayFrameDTO` AUP/input frames into DataVault via cached `IPlayerRuntimeContext`.
  - Rejected alternative: hot `GlobalRegistry.Player` polling or KCC-owned DTO dependency from Core.
  - Microsecond estimate: one DataVault ring write per deterministic input tick; estimated 1-3 us on i3/MX350, 0 GC.
- [x] Task 09 - Add unmanaged replay frame and memory telemetry DTOs.
  - DOD practice: `ReplayFrameDTO` and `MemoryStateTelemetryEntry` are explicit-layout contracts in `Hecton8.Core.Contracts.Physics`, 80/64 bytes, 8-byte aligned.
  - Rejected alternative: duplicate nested KCC DTO ownership; one fact needs one owner.
  - Microsecond estimate: DTO write cost only; no managed allocation.
- [x] Task 10 - Add Burst deterministic replay validation job.
  - DOD practice: `ValidateReplayDeterminismJob` is Burst deterministic/standard, consumes flat NativeArrays, compares recorded AUP at 0.000001 m scale, and writes numeric failure code 12.
  - Rejected alternative: managed replay runner with exceptions/log strings.
  - Microsecond estimate: offline/editor batch path; no player hot-frame cost.

## Loop 3 - Tasks 11-15
- [x] Task 11 - Audit KCC deterministic math for branchless clamps and AUP epsilon route.
  - DOD practice: replay job uses `math.select`, `math.max`, quantized AUP, deterministic Burst attributes, and branchless clamp scale.
  - Rejected alternative: `?:` fallback in Burst inner loop where `math.select` is available.
  - Microsecond estimate: replay-only; branchless replacements are neutral to slightly cheaper.
- [x] Task 12 - Audit ballast deterministic math and AUP force packet route.
  - DOD practice: targeted scan found ballast/submarine Burst jobs already marked deterministic/standard and using DataVault force packet contracts; no direct physics raycast route added.
  - Rejected alternative: broad ballast rewrite outside agent domain.
  - Microsecond estimate: 0 us, audit only.
- [x] Task 13 - Integrate drift failure code 12 into telemetry/result path.
  - DOD practice: replay drift writes `ReplayDeterminismFailureDrift = 12` into unmanaged telemetry and sets KCC precision-drift result bits.
  - Rejected alternative: managed exception or formatted log on drift.
  - Microsecond estimate: one telemetry slot write on replay frame, 0 GC.
- [x] Task 14 - Add JNI attach/detach and FP-control shield.
  - DOD practice: native bridge uses `H8JniEnvironmentScope`; attached threads detach in destructor, with `H8FloatingPointControlScope` restoring ARM64 FPCR/FPSR or x86 MXCSR after JNI release.
  - Rejected alternative: C# `try/finally` around JNI only; native attachment occurs in the C++ bridge and must be owned there.
  - Microsecond estimate: cold Android asset-I/O path only, 0 us gameplay frame.
- [x] Task 15 - Static compile-risk pass without project build.
  - DOD practice: `git diff --check`, targeted symbol scans, C++/C# static audits, DTO layout tests authored, and reset-field scanner executed.
  - Rejected alternative: `dotnet build` after local changes; coordinator explicitly forbade heavy builds unless Burst verification is critically blocked.
  - Microsecond estimate: avoids host CPU burn; runtime 0 us.

## Loop 4 - Tasks 16-20
- [x] Task 16 - Add editor smoke/fuzzer proof for 0.000001 injected drift.
  - DOD practice: `ReplayDeterminismValidator1626` seeds clean and injected replay streams; `ReplayDeterminism1626EditTests` asserts clean pass and drift failure code 12.
  - Rejected alternative: binary dump proof; user explicitly rejected useless dumps.
  - Microsecond estimate: editor-only.
- [x] Task 17 - Verify no managed allocations in runtime replay validator path.
  - DOD practice: static scan of modified hot/replay paths found only value-type `new` in `InputDispatcher.WriteReplayFrameDto` and Burst job telemetry writes; no `List<T>`, `Dictionary<T>`, string formatting, or logs in the modified replay hot path.
  - Rejected alternative: runtime profiler allocation run requiring full Unity test execution under shared CPU load.
  - Microsecond estimate: 0 GC bytes by source inspection.
- [x] Task 18 - Verify DataVault route ownership and no hot GlobalRegistry polling.
  - DOD practice: replay ingress uses handles owned by `InputDispatcher` and cached `_playerContext`; no new scene search, `TryGetLatestCreated`, or direct hot GlobalRegistry polling.
  - Rejected alternative: pulling KCC or player service through a new runtime manager.
  - Microsecond estimate: 1 ring write per deterministic input tick.
- [x] Task 19 - Confirm all touched static reset fields are explicit null/0/default.
  - DOD practice: text scanner reported `H8Memory.cs STATIC_MUTABLE_FIELDS=32 RESET_MISSING=0` and `GlobalDataVault.cs STATIC_MUTABLE_FIELDS=1 RESET_MISSING=0`.
  - Rejected alternative: manual visual-only reset claim.
  - Microsecond estimate: audit-only.
- [x] Task 20 - Append final report to `Docs/AgentLogs/LOG_1626.md`.
  - DOD practice: final report appended with changed files, failure routes, cinematic cheats, and microsecond estimates.
  - Rejected alternative: chat-only report.
  - Microsecond estimate: no runtime cost.

## Current Blockers
- None in static analysis. Full `dotnet build` intentionally not launched per coordinator prohibition after local changes.

## APEX Integrator Verification Continuation - 2026-06-01
- [x] Hot dependency proof tightened.
  - DOD practice: targeted `rg` and editor static assertions cover `PublishDeterministicInputState`, `WriteReplayFrameDto`, `ValidateReplayDeterminismJob`, `PreSimulationInputTick`, and `LateFrameTick`.
  - Rejected alternative: verbal claim of clean dependency flow.
  - Microsecond estimate: no runtime change; editor/static proof only.
- [x] Phase-safety proof tightened.
  - DOD practice: new edit test asserts replay ingress is launched from `PreSimulationInputTick`, while visual interpolation remains in `LateFrameTick` and does not publish replay truth.
  - Rejected alternative: moving replay truth to presentation phase; that would mix simulation authority with visual sync.
  - Microsecond estimate: 0 us added.
- [x] Lock-flattening proof tightened.
  - DOD practice: new edit test asserts DataVault writer locks reserve one writer slot per thread, reject same-thread nested ownership, and release writer slots through `finally`.
  - Rejected alternative: relying on caller discipline only.
  - Microsecond estimate: 0 us added; test-only source proof.
- [x] Compilation throttling complied.
  - DOD practice: `Get-Process dotnet,csc` found active `dotnet` processes, including PID 25728 with high accumulated CPU time; no `dotnet build` was launched.
  - Rejected alternative: adding another compiler workload during contention.
  - Microsecond estimate: avoided build CPU saturation; runtime cost 0 us.
- [x] Replay writer gate flattened to zero-allocation CAS handoff.
  - DOD practice: `StageInputReplaySnapshot()` and `InputReplayWriterLoop()` now use `Interlocked.CompareExchange` through `TryAcquireInputReplaySnapshotGate()` and release through `ReleaseInputReplaySnapshotGate()` in `finally`; writer `Flush()` is inside the acquired gate and runtime has no `_inputReplayGate`, `Monitor.Enter`, or `lock` syntax.
  - Rejected alternative: retaining a managed monitor in the replay pre-simulation path, or checking the pointer under a gate while flushing outside it.
  - Microsecond estimate: main-thread replay stage does not block on writer flush; replay-active MMF contention skips that mirror tick and preserves DataVault replay DTO truth, 0 GC.
- [x] Replay cleanup gate reset centralized.
  - DOD practice: `ReleaseInputReplayMap()` now clears `_inputReplaySnapshotGate`, so normal stop and setup-failure cleanup both leave the CAS handoff state at zero; edit test asserts the cleanup reset.
  - Rejected alternative: resetting the gate only in the successful writer-stop path.
  - Microsecond estimate: cold cleanup only; 0 us frame cost.
- [x] Replay DTO write path stripped of `new` markers.
  - DOD practice: `WriteReplayFrameDto`, `ResolveReplayMoveAxis`, and `SanitizeReplayFloat3` use `default` plus field assignment; edit test asserts those method bodies contain no `new ` token.
  - Rejected alternative: relying on explanation that `new ReplayFrameDTO` and `new float3` are value-type constructions and do not allocate.
  - Microsecond estimate: runtime equivalent; stronger source proof for 0 GC.
- [x] Runtime hot lookup scan repeated after lock patch.
  - DOD practice: targeted scan of modified runtime replay/KCC/editor-validator files returned no `GlobalRegistry.Get<T>()`, `GetComponent`, scene find, or `GameObject.Find` hits.
  - Rejected alternative: counting editor-test assertion string literals as runtime violations.
  - Microsecond estimate: audit-only; 0 us runtime.
- [x] Core/Physics hot lookup candidates audited.
  - DOD practice: broader scan of Core/Physics lookup tokens was reduced by method context; hits in `HydrodynamicKccRuntime.Awake`, Scene overlay creation, runtime context cold sync, and editor validators are cold/editor paths, while `Tick`, `FixedTick`, `LateFrameTick`, and Burst `Execute` bodies inspected in the replay/KCC route stay lookup-clean.
  - Rejected alternative: blindly editing cold cached component binding and breaking existing ownership while chasing raw grep hits.
  - Microsecond estimate: audit-only; no runtime change.
- [ ] Unity compile/test execution pending host contention.
  - DOD practice: latest CPU load sampled at 100% with active Unity `dotnet` PIDs 15112 and 25728, so the build gate remains closed.
  - Rejected alternative: violating compilation throttling during active compiler contention.
  - Microsecond estimate: build CPU burn avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02
- [x] Replay Burst validator hot body stripped of value-type `new` markers.
  - DOD practice: `ValidateReplayDeterminismJob.Execute` and `WriteReplayTelemetry` now build unmanaged `double3`/telemetry structs through `default` plus field assignment; edit test asserts those method bodies contain no `new ` token.
  - Rejected alternative: leaving value-type constructors as technically allocation-free but noisy for source proof.
  - Microsecond estimate: runtime-equivalent; 0 GC and 0 us added.
- [x] Replay editor validator buffer ownership de-magicified.
  - DOD practice: `(BufferID)71830/71831/71832` removed; validator uses `BufferID.ShinobuInputReplayFrames`, `BufferID.ShinobuInputReplayTelemetry`, and `BufferID.ShinobuInputReplayValidationResults`.
  - Rejected alternative: local magic IDs in a proof harness that duplicate Core-owned replay buffer identity.
  - Microsecond estimate: editor-only; 0 us runtime.
- [x] Replay editor validator fail-closed job fence.
  - DOD practice: forced `DispatcherJobFence.TryComplete` return is checked; false completion returns failure before reading result buffers.
  - Rejected alternative: assuming forced completion always succeeds and reading potentially stale validation memory.
  - Microsecond estimate: editor-only branch; 0 us runtime.
- [x] Replay editor validator source proof cleaned.
  - DOD practice: validator fixture setup uses `default` plus field assignment for job, state, tuning, input vector, AUP delta, and replay frames; edit test asserts validator source contains no `new ` token.
  - Rejected alternative: accepting editor-only value constructors that keep grep proof noisy.
  - Microsecond estimate: editor-only; no player-frame effect.
- [x] Verification pass repeated after continuation.
  - DOD practice: `REPLAY_NEW_CLEAN`, `GATE_CLEAN`, `HOT_LOOKUP_CLEAN`, and full touched `git diff --check` passed; only Git CRLF warnings remain.
  - Rejected alternative: relying on previous scans after changing validator/test/source.
  - Microsecond estimate: audit-only; 0 us runtime.
- [x] Unity importer metadata repaired for new C# proof files.
  - DOD practice: `ReplayDeterminismValidator1626.cs.meta` and `ReplayDeterminism1626EditTests.cs.meta` now contain full `MonoImporter` blocks with preserved GUIDs, so Unity can import them as scripts instead of orphan text assets.
  - Rejected alternative: keeping minimal two-line meta files and assuming Unity will normalize them later.
  - Microsecond estimate: import-path correctness only; 0 us runtime.
- [x] DataVault latest-vault disposal proof added.
  - DOD practice: edit test asserts `GlobalDataVault.Dispose()` clears `_latestCreated` when disposing the current latest vault; source scan found the exact `ReferenceEquals(_latestCreated, this)` guarded null assignment.
  - Rejected alternative: relying only on SubsystemRegistration cleanup; direct disposal also has to clear stale bootstrap identity.
  - Microsecond estimate: cold disposal only; 0 us frame cost.
- [x] Assembly and math API dependency proof repeated.
  - DOD practice: asmdef scan confirmed `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Physics.KCC.Editor`, and `Hecton8.EditModeTests` reference the contracts/math/jobs/collections assemblies required by the replay DTO and validator path; Unity.Mathematics package contains `math.asulong(double)`.
  - Rejected alternative: waiting for full build to discover a missing assembly edge on a throttled host.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still pending host gate.
  - DOD practice: latest sampled CPU was 45.353008%, but `dotnet.exe` PID 15112 remained active; `csc.exe` was absent; no `dotnet build` launched because the process gate is still closed.
  - Rejected alternative: starting another compiler while an existing `dotnet` process is active.
  - Microsecond estimate: build CPU burn avoided; runtime 0 us.
- [ ] Roslyn in-memory syntax parse not counted.
  - DOD practice: local `Assets/Plugins/Roslyn` assemblies were probed, but PowerShell load hit `ReflectionTypeLoadException` and `Roslyn.Utilities.StringTable` initializer failures; this is recorded as unavailable, not as a pass.
  - Rejected alternative: reporting a failed parser attempt as successful AST proof.
  - Microsecond estimate: parser attempt was editor/workstation-only; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 Reset Polish
- [x] Replay truth buffers added to frame-state clear path.
  - DOD practice: `InputDispatcher.ClearFrameState()` now clears `_inputReplaySnapshotHandle`, `_inputReplayFrameHandle`, and `_inputReplayTelemetryHandle` together; edit test `InputClearFrameStateClearsAllReplayTruthBuffers` asserts the full set.
  - Rejected alternative: clearing only the MMF snapshot while leaving replay truth/telemetry rings stale until full handle release.
  - Microsecond estimate: reset/shutdown path only; 0 us steady frame cost.
- [x] Replay reset polish source checks repeated.
  - DOD practice: `CLEAR_FRAME_REPLAY_BUFFERS_PASS`, `REPLAY_RELEASE_POINTER_FAILCLOSED_PASS`, `REPLAY_PATH_SOURCE_CLEAN`, `GATE_CLEAN`, and focused `git diff --check` passed after the reset polish patch.
  - Rejected alternative: relying on the pre-patch verification set after editing reset code.
  - Microsecond estimate: audit-only; 0 us runtime.
- [x] Replay MMF pointer cleanup made fail-closed.
  - DOD practice: `ReleaseInputReplayMap()` now nulls `_inputReplayPointer` whenever it is non-null, while calling `ReleasePointer()` only when `_inputReplayAccessor` is still available; edit test asserts this split.
  - Rejected alternative: requiring both pointer and accessor to be non-null before clearing the pointer, which preserves stale pointer state after a partial cleanup fault.
  - Microsecond estimate: cold cleanup branch only; 0 us steady frame cost.
- [ ] Unity compile/test execution remains blocked by host gate.
  - DOD practice: latest sampled CPU was 96.123710%; `dotnet.exe` PIDs 15112 and 27360 were active; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: launching another compiler under explicit CPU/process gate violation.
  - Microsecond estimate: avoided host CPU saturation; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 Partial Init Hardening
- [x] H8Memory partial-initialize shutdown made fail-closed.
  - DOD practice: `Shutdown()` now calls `DisposeOwnerPointerLists()`, `ClearTrackingMemoryBeforeDispose()`, and `DisposeTrackingContainers()` even when `_initialized` is false, covering failures after some Persistent containers were created but before `_initialized = true`.
  - Rejected alternative: assuming `Initialize()` either fully succeeds or allocates nothing; low-memory paths can fail between container constructions.
  - Microsecond estimate: cold shutdown/SubsystemRegistration only; 0 us steady frame cost.
- [x] H8Memory tracking-container disposal centralized.
  - DOD practice: new `DisposeTrackingContainers()` owns all native tracking container disposal and is called from both partial and normal shutdown paths; edit test `H8MemoryShutdownDisposesTrackingContainersEvenAfterPartialInitialize` asserts the route.
  - Rejected alternative: duplicated disposal code in only the normal `_initialized == true` branch.
  - Microsecond estimate: cold teardown only; no runtime allocation or frame cost.
- [x] Partial-init hardening source checks repeated.
  - DOD practice: `H8MEMORY_PARTIAL_INIT_SHUTDOWN_PASS`, smart brace-depth pass, `RESET_AND_GATE_INVARIANTS_PASS`, `REPLAY_PATH_SOURCE_CLEAN`, and full touched `git diff --check` passed.
  - Rejected alternative: using a naive brace counter that counts braces inside string literals as syntax proof.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: latest sampled CPU was 13.055723%, but `dotnet.exe` PIDs 15112 and 27360 remained active; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: violating the explicit process gate just because CPU briefly dropped below 50%.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 DataVault Init Fail-Closed
- [x] GlobalDataVault native allocation exception path made fail-closed.
  - DOD practice: `GlobalDataVault.Initialize()` now wraps the Persistent map/list and arena setup region in `try/catch`; the catch calls `AbortInitialize()` and rethrows, so any native container constructor exception disposes already-created storage.
  - Rejected alternative: relying only on explicit `IsCreated` checks after H8Memory allocations, which does not cover constructor exceptions before the next check.
  - Microsecond estimate: cold exception path only; 0 us steady frame cost.
- [x] DataVault init failure source proof added.
  - DOD practice: edit test `DataVaultInitializeAbortsOnNativeAllocationException` asserts the `try`, `catch`, `AbortInitialize();`, `throw;`, and ordering around `_buffers` allocation and `_latestCreated = this`.
  - Rejected alternative: proving this only through a forced low-memory runtime harness, which would be non-deterministic on the shared host and unnecessary for this source invariant.
  - Microsecond estimate: editor source check only; 0 us runtime.
- [x] DataVault fail-closed checks repeated.
  - DOD practice: `GLOBALDATAVAULT_INIT_FAILCLOSED_PASS`, smart brace-depth pass, hot lookup grep clean, replay CAS gate scan clean, phase order scan clean, and focused `git diff --check` passed; only CRLF warnings were reported.
  - Rejected alternative: treating the new catch block as obvious and skipping post-patch source verification.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: `dotnet.exe` PIDs 15112 and 27360 remained active; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: starting another compiler while the explicit dotnet process gate is still closed.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 DataVault Factory Contract
- [x] GlobalDataVault factory no longer returns uninitialized instances.
  - DOD practice: `GlobalDataVault.Create()` now returns only after `_initialized` is true; otherwise it calls `AbortInitialize()` and throws `FatalMemoryException.ThrowVaultInitializationFailed()`.
  - Rejected alternative: returning an uninitialized object and relying on every caller to inspect internal readiness before registration or consumption.
  - Microsecond estimate: cold factory failure path only; 0 us steady frame cost.
- [x] Factory fail-closed source proof added.
  - DOD practice: edit test `DataVaultCreateFailsClosedWhenInitializeDoesNotComplete` asserts the source ordering of `Initialize`, `_initialized`, `AbortInitialize`, and fatal memory throw, plus the new `FatalMemoryException` method.
  - Rejected alternative: adding a runtime low-memory allocator failure test on the shared host, which would be unstable and outside the deterministic source proof needed here.
  - Microsecond estimate: editor source check only; 0 us runtime.
- [x] DataVault factory checks repeated.
  - DOD practice: `GLOBALDATAVAULT_CREATE_FAILCLOSED_PASS`, smart brace-depth pass, and focused `git diff --check` passed; only CRLF warnings were reported.
  - Rejected alternative: assuming the factory edit is safe without a post-edit order scan.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: active `dotnet.exe` PIDs 15112, 20288, and 23312 were observed; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: spawning another compiler during explicit process-gate contention.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 DataVault Lock Fault Rollback
- [x] DataVault writer lock acquisition now rolls back committed lock state on exception.
  - DOD practice: `TryAcquireWriteLock<T>()` tracks `writerLockCommitted`; if a fault occurs after metadata/block lock commit, the catch calls `RollbackWriterLockUnlocked(...)` before `ReleaseBlockMutationGate()`.
  - Rejected alternative: assuming native alias view construction and safety-handle attachment cannot throw after lock state is committed.
  - Microsecond estimate: one local bool write on lock acquisition; no managed allocation, no extra lock, expected 0 us measurable steady frame cost.
- [x] Writer-lock exception rollback source proof added.
  - DOD practice: edit test `DataVaultWriterLockRejectsNestedSameThreadOwnershipAndReleasesThroughFinally` now asserts `catch`, `writerLockCommitted = true`, and rollback through `RollbackWriterLockUnlocked(key, writerSlotOffsetBytes, activeLockBit, (int)systemID)`.
  - Rejected alternative: relying on broad grep for `finally` without proving the post-commit exception path.
  - Microsecond estimate: editor source check only; 0 us runtime.
- [x] APEX integrator source gates repeated.
  - DOD practice: `HOT_LOOKUP_GREP_CLEAN`, `PHASE_SAFETY_PASS`, `REPLAY_CAS_GATE_PASS`, `DATAVAULT_WRITER_LOCK_FLATTENING_PASS`, `GLOBALDATAVAULT_CREATE_FAILCLOSED_PASS`, `DATAVAULT_LOCK_EXCEPTION_ROLLBACK_PASS`, prompt hash check, smart brace-depth pass, and touched `git diff --check` passed; only CRLF warnings were reported.
  - Rejected alternative: treating lock rollback as isolated and skipping replay/phase/dependency invariants.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: active `dotnet.exe` PIDs 15112, 16700, and 23312 were observed during this loop; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: violating compilation throttling while another dotnet host remains active.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 DataVault Pin Fault Rollback
- [x] DataVault buffer pin acquisition now rolls back committed pin state on exception.
  - DOD practice: `TryLockBuffer()` tracks `pinLockCommitted` and the previous alias requester; if a fault occurs after pin commit, the catch calls `RollbackBufferPinUnlocked(...)` before `ReleaseBlockMutationGate()`.
  - Rejected alternative: treating read alias pins as lower risk; stuck pins block relocation/defrag and can stall future writers.
  - Microsecond estimate: two local assignments in the lock acquisition path; no managed allocation, no extra lock, expected 0 us measurable steady frame cost.
- [x] Buffer-pin rollback source proof added.
  - DOD practice: edit test `DataVaultBufferPinLockRollsBackOnExceptionBeforeGateRelease` asserts commit tracking, previous-owner preservation, catch rollback, and gate release ordering.
  - Rejected alternative: relying on validation-failure rollback checks without covering thrown exceptions.
  - Microsecond estimate: editor source check only; 0 us runtime.
- [x] Pin-lock and replay gates repeated.
  - DOD practice: `DATAVAULT_PIN_LOCK_EXCEPTION_ROLLBACK_PASS`, `DATAVAULT_WRITER_LOCK_ROLLBACK_STILL_PASS`, smart brace-depth pass, `HOT_LOOKUP_GREP_CLEAN`, `PHASE_SAFETY_PASS`, `REPLAY_CAS_GATE_PASS`, and focused `git diff --check` passed; only CRLF warnings were reported.
  - Rejected alternative: checking only the new pin-lock body and skipping adjacent writer/replay invariants.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: active `dotnet.exe` PIDs 15112, 16700, and 23312 were observed; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: starting another compiler under the explicit process gate.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.

## APEX Integrator Verification Continuation - 2026-06-02 Replay Canonical Bits
- [x] Replay DTO AUP and move-axis values now canonicalize IEEE 754 zero bits before hashing.
  - DOD practice: `TryResolveReplayAup()` canonicalizes finite `double3` AUP components through `CanonicalizeReplayDouble()`, and `SanitizeReplayFloat3()` canonicalizes move/velocity floats through `CanonicalizeReplayFloat()`.
  - Rejected alternative: masking sign bits inside the hash function, which would also hide real negative non-zero physics facts.
  - Microsecond estimate: replay-recording path only; six scalar canonical checks per recorded frame, 0 GC, expected 0 us measurable steady frame cost.
- [x] Replay canonicalization source proof added.
  - DOD practice: `ReplayHotPathsDoNotUseSceneOrRegistryLookups` now asserts the canonical helpers are used by replay AUP and float sanitizers and that the helper bodies collapse both signed zero forms to positive zero.
  - Rejected alternative: relying on reviewer memory that `-0.0` and `+0.0` compare equal but hash to different bit patterns.
  - Microsecond estimate: editor source check only; 0 us runtime.
- [x] Focused source gates repeated where the host shell remained stable.
  - DOD practice: `REPLAY_CANONICAL_BITS_PASS`, `TOUCHED_HOT_DEPENDENCY_SCAN_PASS`, and `PHASE_NO_PRESENTATION_DEFER_PASS` passed; smart brace-depth printed `BRACE_DEPTH_EDITED_FILES_PASS` but the Python wrapper returned exit `-1`, so it is recorded as an anomalous wrapper result rather than a compiler proof.
  - Rejected alternative: launching `dotnet build` to compensate for shell wrapper instability while active dotnet processes remain present.
  - Microsecond estimate: audit-only; 0 us runtime.
- [ ] Unity compile/test execution still blocked by active compiler host process.
  - DOD practice: active `dotnet.exe` PIDs 15112, 16700, and 21056 were observed; `csc.exe` was absent; no `dotnet build` launched.
  - Rejected alternative: starting a second compiler under explicit process-gate contention.
  - Microsecond estimate: build CPU contention avoided; runtime 0 us.
