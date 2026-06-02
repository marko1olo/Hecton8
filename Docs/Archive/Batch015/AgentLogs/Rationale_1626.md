# Rationale 1626

## Decision 01 - Scope Boundary
Problem: The prompt touches allocator reset, DataVault reset, JNI, KCC replay, and ballast physics. The repo already has many unrelated modified files from parallel agents.
Solution: Edit only core memory/reset code, deterministic replay validation code, and an existing JNI bridge if a missing balanced detach is proven by source. Use active source and active architecture docs only.
Rejected Alternatives: Reverting unrelated files or using archive reports as authority would corrupt parallel work and create false proof.
Scalability potential: Low-tier gets no new hot-frame work; middle/high/ultra tiers can spend offline smoke-test cycles on stricter replay validation.
Hardware Impact: i3/MX350 runtime frame gain is neutral; avoiding extra managers prevents recurring CPU and GC overhead.

## Decision 02 - Build Gate
Problem: Full project build is explicitly forbidden after small edits and the workstation is shared by many agents.
Solution: Use static C# audits, targeted `rg`, and diff checks until a Burst compile wall becomes a real blocker. Build only after checking CPU and `dotnet`/`csc.exe`.
Rejected Alternatives: Running `dotnet build` as a habit burns host CPU and violates the coordinator directive.
Scalability potential: Developer workstation remains usable for concurrent agents; verification still escalates if static analysis cannot prove safety.
Hardware Impact: Avoids minutes of CPU saturation on low-end host hardware; runtime impact 0 us.

## Decision 03 - Native Reset Shape
Problem: Domain reload disabled can keep static native handles, cursors, and latest-vault references alive across Play Mode restarts.
Solution: Reset path must finish H8Memory owner jobs, dispose latest DataVault, clear native backing storage where pointers are owned, dispose containers, and explicitly assign static fields to default/null/0.
Rejected Alternatives: Trusting `Dispose()` alone leaves stale `IsCreated` state and old cursors dependent on Unity reload behavior.
Scalability potential: Low-tier avoids allocator drift after repeated play sessions; high/ultra can run heavier smoke loops without stale allocator contamination.
Hardware Impact: Cold reset performs linear clearing once; frame impact remains 0 us on i3/MX350.

## Decision 04 - Replay Validator Placement
Problem: Determinism validation needs raw frame DTOs and drift telemetry without adding a new runtime authority.
Solution: Place unmanaged replay DTOs and Burst validation job inside the existing KCC smoke-test partial, using AUP double3, explicit struct layouts, and fixed-size NativeArray inputs.
Rejected Alternatives: A new MonoBehaviour or managed replay recorder would add lifecycle ambiguity and GC pressure.
Scalability potential: Low-tier can run short offline smoke loops; middle/high/ultra can increase frame count and telemetry detail while gameplay truth stays unchanged.
Hardware Impact: No player-frame cost; editor validation cost is bounded batch work.

## Decision 05 - Replay DTO Ownership
Problem: Input recording is owned by Core, while replay validation is consumed by Physics/KCC. Nesting `ReplayFrameDTO` under KCC would force Core to depend on a physics runtime class.
Solution: Move `ReplayFrameDTO` and `MemoryStateTelemetryEntry` into `Hecton8.Core.Contracts.Physics` with explicit 80/64 byte layouts and let both Core and KCC consume that contract.
Rejected Alternatives: Duplicate DTOs per domain, or direct Core -> KCC dependency. Both break one-owner routing and make replay ABI drift likely.
Scalability potential: Low-tier records the same flat ring with minimal per-frame bytes; middle/high/ultra can run longer replay windows without changing DTO authority.
Hardware Impact: i3/MX350 cost is one 80-byte ring write per deterministic input tick, estimated 1-3 us, 0 GC.

## Decision 06 - Input Replay Ingress Route
Problem: Replay frames need current input plus AUP without scene search or hot GlobalRegistry polling.
Solution: `InputDispatcher` writes `ReplayFrameDTO` through DataVault handles during deterministic input publish, using cached `_playerContext.TryGetPlayerPoseSnapshot` and `_playerContext.TryGetMovementRuntimeState`.
Rejected Alternatives: `FindObjectOfType`, direct KCC reach-in, or GlobalRegistry hot polling. Those routes add ownership ambiguity and can allocate or drift with service timing.
Scalability potential: Low-tier keeps a 512-frame ring; middle/high/ultra can enlarge capacity later through DataVault policy without changing gameplay truth.
Hardware Impact: One contiguous NativeArray write and two pure cached-interface reads per tick; estimated 1-3 us on i3/MX350, 0 GC.

## Decision 07 - JNI FP-Control Shield
Problem: Android JNI attach/detach is native-side in `HectonAndroidAssetBridge.cpp`; C# `try/finally` cannot protect FP control registers around native attach.
Solution: Use RAII scopes in C++: `H8JniEnvironmentScope` detaches attached threads, and `H8FloatingPointControlScope` restores ARM64 FPCR/FPSR or x86 MXCSR after detach.
Rejected Alternatives: Comment-only proof, or managed wrapper-only protection. Neither controls the native thread state where `AttachCurrentThread` executes.
Scalability potential: Low/middle/high/ultra devices share identical FP-control restoration on Android native asset I/O.
Hardware Impact: Cold asset bridge only; runtime physics frame cost 0 us. Attach path adds two register reads and two register writes on ARM64.

## Decision 08 - Build Suppression
Problem: Prompt demands build, but coordinator explicitly forbids `dotnet build` after small edits unless critical Burst verification is mathematically blocked.
Solution: Stop at static verification: `git diff --check`, targeted `rg`, reset-field scanner, C++ static audit, DTO layout tests authored. No build/test process launched.
Rejected Alternatives: Ignoring the coordinator and starting a heavyweight compiler pass on a shared workstation.
Scalability potential: Development cluster remains usable while still leaving deterministic proof artifacts on disk.
Hardware Impact: Avoided sustained CPU saturation; runtime impact 0 us.

## Decision 09 - Physics Audit Boundary
Problem: KCC/ballast audit found existing deterministic Burst attributes but also approximation names in visual/math LOD areas. A broad rewrite would cross domains and risk active agents.
Solution: Patch only the new replay validation loop to deterministic `math.select`/standard quantized AUP behavior; record ballast/KCC audit facts without rewriting unrelated systems.
Rejected Alternatives: Rewriting buoyancy/ballast approximations outside the prompt's integration surface without profiler proof.
Scalability potential: Low-tier keeps cheap approximations where already authored; high/ultra retain existing GlobalQualityWeight-driven overkill paths.
Hardware Impact: No new hot physics cost; replay validator remains bounded offline/editor work.

## Decision 10 - Active Replay Gate
Problem: Replay frame recording must not write stale AUP/input DTOs when the MMF replay writer is not alive.
Solution: Gate `WriteReplayFrameDto` behind `_inputReplaySignal != null`, `_inputReplayThread != null`, and `_inputReplayStopRequested == 0`, then use cached `_playerContext` only.
Rejected Alternatives: Recording into the DataVault ring whenever deterministic input publishes. That would create stale replay frames after replay writer shutdown.
Scalability potential: Low-tier avoids useless ring writes when replay is disabled; middle/high/ultra can keep longer replay windows without changing input authority.
Hardware Impact: One volatile read and two null checks on active replay path; inactive path returns before DataVault resolution.

## Decision 11 - Lock Flattening Proof
Problem: DataVault write locks must not deadlock through nested writer ownership or leaked release paths.
Solution: Use existing DataVault writer slot discipline as the invariant: `TryReserveThreadWriterSlot` rejects an already active slot on the same thread, `TryAcquireWriteLock` rolls back slots in `finally`, and `ReleaseWriteLock` releases under `finally`.
Rejected Alternatives: Caller-only comments or broad runtime lock rewrites. The vault already owns the central writer slot table; tests now assert it.
Scalability potential: Low/middle/high/ultra share the same one-writer-per-thread invariant. No quality tier changes lock truth.
Hardware Impact: No runtime change in this continuation; editor static assertion only.

## Decision 12 - Static Verification Over Build Contention
Problem: The integrator asked for proof, but the workstation already has active `dotnet` processes and the coordinator banned routine builds.
Solution: Add editor source assertions and run in-memory/text static checks: hot lookup scan, brace-depth scan, DataVault invariant scan, JNI FP restore scan, and `git diff --check`.
Rejected Alternatives: Starting `dotnet build` during compiler contention or generating JSON reports no one reads.
Scalability potential: Shared development cluster remains usable; proof stays in executable C# tests and concise logs.
Hardware Impact: Avoided another compiler pass; runtime impact 0 us.

## Decision 13 - Replay Gate CAS Handoff
Problem: The replay MMF writer previously exposed lock release for audit, but the writer checked the pointer under the gate and flushed outside the gate. That did not mathematically protect copy/flush ordering and kept a managed monitor in a pre-simulation route.
Solution: Replace `_inputReplayGate` with `_inputReplaySnapshotGate` as an `int` CAS gate. `StageInputReplaySnapshot()` acquires with `Interlocked.CompareExchange` and releases in `finally`; if the background writer is flushing, the main thread skips only the MMF mirror tick. `InputReplayWriterLoop()` holds the same gate through `accessor?.Flush()` and releases in `finally`.
Rejected Alternatives: Holding a managed `lock` through `Flush()` would serialize correctness but could stall pre-simulation on disk I/O. Leaving `Flush()` outside the gate preserved the race.
Scalability potential: Low-tier avoids main-thread stalls during replay recording; middle/high/ultra can raise replay flush cadence later while the authoritative DataVault DTO ring remains unchanged.
Hardware Impact: One CAS on active replay snapshot staging. Inactive replay still returns before DataVault buffer resolution. Replay-active contention costs a skipped MMF mirror instead of a main-thread disk flush wait; 0 GC.

## Decision 14 - Raw Lookup Hits Are Not All Hot Violations
Problem: A broad Core/Physics grep for `TryGetComponent` finds legitimate cold bindings in `Awake`, `OnEnable`, scene overlay creation, and cold runtime context sync. Treating every raw hit as a hot-loop violation would create broad churn in systems outside the current memory/replay patch.
Solution: Reduce candidates by phase and method context. The authoritative replay/KCC hot bodies remain lookup-clean; existing cold component resolution stays cached. The editor test now enforces the exact replay hot bodies rather than raw file-wide strings.
Rejected Alternatives: Rewriting cold service initialization and UI construction just to reduce grep count. That would risk ownership and prefab behavior without improving deterministic replay.
Scalability potential: Low-tier keeps cold startup cost where it belongs; middle/high/ultra get the same hot-loop purity without new service indirection.
Hardware Impact: No runtime change. The avoided alternative would add needless abstraction and possible cache misses in existing service startup.

## Decision 15 - Replay Cleanup and DTO Source Proof
Problem: The CAS replay handoff could remain marked as busy if MMF setup failed before a normal writer stop, and static reviewers could still flag value-type `new` syntax as suspicious even though it does not allocate.
Solution: Centralize `_inputReplaySnapshotGate` reset inside `ReleaseInputReplayMap()` so every cleanup route clears the gate, and rewrite replay DTO/float3 construction as `default` plus field assignment. Extend editor source assertions to check both cleanup reset and absence of `new ` tokens in the replay DTO helper bodies.
Rejected Alternatives: Resetting only in `StopInputReplayWriter()` misses setup-failure cleanup. Keeping value-type `new` is technically allocation-free, but it leaves noisy proof for reviewers and automated scans.
Scalability potential: Low-tier replay recording never stalls behind a stale CAS bit after failed setup; middle/high/ultra can raise replay mirror cadence later without changing DataVault truth ownership.
Hardware Impact: Cleanup reset is cold-path only. DTO initialization is runtime-equivalent to value-type construction, still 0 GC; source-level proof avoids unnecessary profiler/build escalation on the shared host.

## Decision 16 - Replay Validator Source Proof
Problem: The Burst replay validator and editor proof fixture still contained value-type `new` syntax. It does not allocate, but it weakens zero-GC source scans and makes proof review dependent on C# value-type semantics.
Solution: Rewrite replay validator `double3` delta, telemetry entry, editor job setup, seed state, tuning, input vector, AUP delta, and replay frame construction as `default` plus field assignment. Add source assertions for no `new ` in validator hot bodies and editor validator fixture.
Rejected Alternatives: Explaining that value-type `new` is stack/local initialization. Correct, but not as strong as source that has no ambiguous marker.
Scalability potential: Low-tier keeps the same runtime math with cleaner proof; middle/high/ultra can expand replay validation without carrying false allocation hits.
Hardware Impact: Runtime-equivalent in Burst and editor-only for fixture cleanup; 0 GC and 0 us player-frame cost.

## Decision 17 - Named Replay Buffer Ownership
Problem: `ReplayDeterminismValidator1626` used local magic `BufferID` casts for replay frames, telemetry, and results. That violates one-owner routing even inside an editor verifier.
Solution: Route replay proof buffers through named `BufferID.ShinobuInputReplayFrames`, `ShinobuInputReplayTelemetry`, and new `ShinobuInputReplayValidationResults`. Add a source test that rejects the old `(BufferID)718` range and asserts named IDs.
Rejected Alternatives: Keeping magic IDs because the validator creates an isolated vault. That still leaves buffer identity undocumented and invites future collision.
Scalability potential: Low/middle/high/ultra validation can share the same Core-owned replay buffer identity while changing only capacity/cadence policy.
Hardware Impact: Editor-only identity cleanup; 0 us runtime.

## Decision 18 - Fail-Closed Replay Job Fence
Problem: The editor verifier scheduled the Burst replay job and ignored the return value of `DispatcherJobFence.TryComplete`. A false return would let the verifier read stale result memory.
Solution: Check the forced completion return and return failure with `KccSmokeFailureAllocation` before reading result buffers if completion fails.
Rejected Alternatives: Assuming forced completion is infallible. The proof harness must fail closed because it is validating determinism, not hiding scheduler faults.
Scalability potential: Same proof route scales from short low-tier smoke runs to longer high/ultra replay batches without silently accepting incomplete jobs.
Hardware Impact: Editor-only branch; 0 us runtime.

## Decision 19 - Unity Importer Metadata Closure
Problem: New C# proof files had minimal `.meta` contents. Unity may import such assets unpredictably until it regenerates proper `MonoImporter` metadata, which weakens the proof harness on a shared branch.
Solution: Preserve the existing GUIDs and expand `ReplayDeterminismValidator1626.cs.meta` and `ReplayDeterminism1626EditTests.cs.meta` to full `MonoImporter` blocks.
Rejected Alternatives: Deleting meta files or letting Unity regenerate them would churn GUIDs and risk references in parallel work.
Scalability potential: Low/middle/high/ultra device behavior is unchanged; the editor import route becomes deterministic for the proof sources.
Hardware Impact: Editor import correctness only; 0 us runtime.

## Decision 20 - Latest Vault Self-Disposal Proof
Problem: SubsystemRegistration clears the latest DataVault, but a direct `GlobalDataVault.Dispose()` on the latest instance also must sever `_latestCreated` or later bootstrap/editor diagnostics can observe stale identity.
Solution: Keep the guarded `ReferenceEquals(_latestCreated, this)` null assignment in `Dispose()` and add an editor source assertion that rejects removal of this self-clear path.
Rejected Alternatives: Depending solely on `DisposeLatestCreatedForNativeMemoryShutdown()`. It covers the global shutdown route but not every direct disposal path.
Scalability potential: Repeated Play Mode runs on weak machines do not inherit stale vault identity; longer high/ultra validation loops keep clean bootstrap state.
Hardware Impact: Cold disposal branch only; 0 us frame cost.

## Decision 21 - Assembly Dependency Proof Before Build
Problem: The replay contract moved into `Hecton8.Core.Contracts.Physics`, and missing asmdef references would surface only at compile time if not checked directly.
Solution: Scan the actual asmdef files for `Hecton8.Core.Contracts`, `Unity.Mathematics`, `Unity.Collections`, `Unity.Jobs`, and the editor test reference to `Hecton8.Physics.KCC.Editor`; scan the installed Unity.Mathematics package for `math.asulong(double)`.
Rejected Alternatives: Launching a full build just to validate a small assembly/reference surface while the build gate is closed.
Scalability potential: Dependency ownership stays flat and portable across devices; no runtime route changes by quality tier.
Hardware Impact: Static file scan only; 0 us runtime and no compiler CPU saturation.

## Decision 22 - Replay Frame-State Reset Completeness
Problem: `InputDispatcher.ClearFrameState()` cleared the replay MMF snapshot ring but did not clear the replay frame DTO ring or replay telemetry ring. That leaves stale replay truth/telemetry visible during service reset paths before full handle release.
Solution: Clear `_inputReplayFrameHandle` and `_inputReplayTelemetryHandle` beside `_inputReplaySnapshotHandle`, and add an editor source assertion that requires all three replay buffers in `ClearFrameState()`.
Rejected Alternatives: Waiting for `DisposeDeterministicInputNativeBuffers()` to release handles. Release is necessary, but frame-state clear is also a reset boundary and must not preserve stale replay facts.
Scalability potential: Low-tier repeated Play Mode or service resets do not inherit false replay drift state; middle/high/ultra longer replay windows get the same clean reset route without a new service abstraction.
Hardware Impact: Cold reset/shutdown memclear only; 0 us steady frame cost and 0 GC.

## Decision 23 - Replay MMF Pointer Fail-Closed Cleanup
Problem: `ReleaseInputReplayMap()` only nulled `_inputReplayPointer` when `_inputReplayAccessor` was also non-null. A partial cleanup fault could leave a stale native pointer visible after the accessor path was already gone.
Solution: Split the guard: if the pointer is non-null, release through the accessor when available, then always assign `_inputReplayPointer = null`. Add a source assertion for the split guard.
Rejected Alternatives: Keeping the combined pointer/accessor condition and assuming partial initialization never happens. The cleanup path is specifically where partial initialization failures appear.
Scalability potential: Low/middle/high/ultra replay recording uses the same fail-closed teardown; no quality tier changes pointer ownership.
Hardware Impact: Cold cleanup branch only; 0 us steady frame cost and 0 GC.

## Decision 24 - H8Memory Partial Initialize Teardown
Problem: `H8Memory.Initialize()` creates multiple Persistent native containers before `_initialized = true`. If a low-memory or allocation failure occurs mid-initialize, `Shutdown()` previously enters the `_initialized == false` branch and could reset static fields without disposing already-created containers.
Solution: Centralize native tracking container disposal in `DisposeTrackingContainers()` and call it from both the partial-init branch and the normal shutdown branch, after clearing tracking memory and owner pointer lanes.
Rejected Alternatives: Assuming Persistent constructors either all succeed or none execute. That is not a valid low-end hardware assumption.
Scalability potential: Weak devices under memory pressure get deterministic cleanup instead of leaked tracking containers; middle/high/ultra keep identical normal shutdown behavior with less duplicated disposal code.
Hardware Impact: Cold shutdown/SubsystemRegistration only; 0 us steady frame cost and 0 GC.

## Decision 25 - GlobalDataVault Native Allocation Exception Teardown
Problem: `GlobalDataVault.Initialize()` creates Persistent maps/lists and H8Memory-backed arrays before final `_initialized = true`. Existing `AbortInitialize()` covered explicit `IsCreated` failures, but constructor exceptions from `UnsafeHashMap`, `NativeList`, or `NativeParallelHashMap` could bypass the abort path.
Solution: Wrap the native allocation and arena setup region in a `try/catch` that calls `AbortInitialize()` and rethrows. `AbortInitialize()` marks the vault disposable, then `Dispose()` releases created containers, clears native storage, frees the arena if present, and severs `_latestCreated`.
Rejected Alternatives: Catching only `OutOfMemoryException` or relying on `Create()` callers to dispose after an exception. Unity native container allocation failures are not guaranteed to surface through one managed exception type, and direct `Initialize()` callers must be protected.
Scalability potential: Low-tier memory pressure fails closed without leaking the DataVault root; middle/high/ultra keep identical successful initialization behavior and no new runtime branch.
Hardware Impact: Cold initialization exception path only; 0 us steady frame cost and 0 GC.

## Decision 26 - GlobalDataVault Factory Contract
Problem: `GlobalDataVault.Create()` returned a `GlobalDataVault` instance even when `Initialize()` returned without `_initialized = true`. Bootstrap and tests use `Create()` as a ready-object factory, so a silent uninitialized return can register or consume a vault without arena, metadata, or generation state.
Solution: After `Initialize()`, return only if `_initialized` is true. Otherwise call `AbortInitialize()` and throw through `FatalMemoryException.ThrowVaultInitializationFailed()`, preserving cleanup and making the bootstrap failure explicit.
Rejected Alternatives: Returning null or adding a bool-return factory. That would force broad caller churn and still leave existing callers vulnerable; the current method name and usages already imply fail-fast construction.
Scalability potential: Low-tier memory/ABI failures stop before corrupting global memory identity; middle/high/ultra keep the same successful path and do not pay a frame-time branch.
Hardware Impact: Cold factory failure path only; 0 us steady frame cost and 0 GC.

## Decision 27 - DataVault Writer Lock Exception Rollback
Problem: `TryAcquireWriteLock<T>()` released the thread writer slot in `finally`, but an exception after metadata/block lock commit and before successful view return could leave `ActiveWriterSystemID`, block lock flags, and active lock bits set.
Solution: Add `writerLockCommitted` tracking inside the mutation gate. If an exception occurs after commit, rollback uses `RollbackWriterLockUnlocked(key, writerSlotOffsetBytes, activeLockBit, systemID)` before the mutation gate is released.
Rejected Alternatives: Assuming `CreateNativeArrayView` and safety-handle attachment cannot throw. That is not strong enough for a lock primitive whose purpose is failure containment.
Scalability potential: Low-tier fault paths no longer poison the DataVault after a rare alias/view failure; middle/high/ultra retain the same successful lock path and deterministic release semantics.
Hardware Impact: Non-throwing path adds one local bool write in the lock acquisition path; no managed allocation, no extra lock, and 0 us measurable steady frame cost expected.

## Decision 28 - DataVault Buffer Pin Exception Rollback
Problem: `TryLockBuffer()` had explicit rollback for validation failures after pin commit, but no catch path for an exception after block pin metadata was written and before the method returned true.
Solution: Add `pinLockCommitted` plus previous alias owner tracking. If an exception occurs after the pin is committed, call `RollbackBufferPinUnlocked(key, lockedOffsetBytes, activeLockBit, committedPreviousAliasRequester)` before the mutation gate is released.
Rejected Alternatives: Treating buffer pins as lower risk because they are read aliases. A stuck alias pin blocks relocation/defrag and can stall future writer acquisition just like a stuck writer lock.
Scalability potential: Low-tier memory/view faults do not leave a permanent DataVault pin; middle/high/ultra keep the same non-fault path and relocation behavior.
Hardware Impact: Non-throwing path adds two local assignments in the buffer-pin lock path; no managed allocation, no extra lock, expected 0 us measurable steady frame cost.
## Decision 29 - Replay Hash Canonical Float Bits
Problem: Replay frame hashing consumed `math.asuint(float)` and `math.asulong(double)` after finite checks. IEEE 754 signed zero is finite, so `-0.0` and `+0.0` could describe the same physical AUP/move-axis state but produce different replay hashes across platform math routes.
Solution: Canonicalize replay floats and doubles before DTO write and hash: non-finite values and both zero signs collapse to positive zero, while finite non-zero values keep their exact bits. Source tests now assert the canonical helpers are used by `TryResolveReplayAup()` and `SanitizeReplayFloat3()`.
Rejected Alternatives: Changing the hash function to ignore sign bits globally. That would hide real negative non-zero velocity/AUP facts and weaken drift detection.
Scalability potential: Low-tier/mobile replay avoids false desync from signed-zero math noise; middle/high/ultra keep bit-exact non-zero physical state for stricter replay validation.
Hardware Impact: Three float helper calls and three double helper calls only while input replay recording is active; no managed allocation, no registry lookup, expected 0 us measurable steady frame cost.
