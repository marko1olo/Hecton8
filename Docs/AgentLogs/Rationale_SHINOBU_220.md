# Rationale_SHINOBU_220

Status: POLISH LOOP ACTIVE / INTENT BUS HARDENED / AUP CONVERSION HARDENED / GPU_DOUBLE_BUFFER_HARDENED / SHADER_GLOBAL_FAIL_CLOSED / ZERO_ACTIVE_VISUAL_BYPASS / ZERO_ACTIVE_KCC_JOB_BYPASS / KCC_DISPATCHER_FRAME_FENCE / VERIFY_WORDING_CORRECTED / VAULT_HOTSWAP_RELEASE_HARDENED / PRE_SIM_FENCE_HARDENED / TEARDOWN_DRAIN_HARDENED / EVERY_FRAME_TELEMETRY_HARDENED / TELEMETRY_LENGTH_GUARDS_HARDENED / BULKHEAD_SHADOW_STATE_PURGED / BLACKBOX_DUMP_DECOUPLED_FROM_SHADER / UNITY IMPORT HUNG

## Initial Decision 2026-05-20
Problem: Emergency habitat bulkhead closure is currently represented by CPU-side `BaseAirlock` transform motion, while the batch requires mathematical CSR/KCC blocking and GPU-only visual closure.
Solution: Add a vault-backed explicit-layout bulkhead state lane, Burst jobs for closure/edge sealing/KCC plane blocking, and a shader StructuredBuffer reader for the visual "Dear Lie."
Rejected Alternatives: Keeping `emergencyBulkheadDoorMesh.localPosition` movement is cheap per object but still object-state animation and scales badly across many base doors; adding colliders would churn broadphase and violate the prompt.
Scalability potential: Low uses 5 Hz authority integration with shader interpolation; Middle keeps 10-20 Hz; High/Ultra can run denser visual distortion in UberNoir without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain is removal of per-door transform writes, Animator risk, and collider toggles; exact microseconds pending static implementation and profiler proof.

## Decision 2026-05-20 - Data Authority Lane
Problem: Emergency bulkhead truth had no unmanaged CSR/KCC lane, so any lock state would be trapped in managed MonoBehaviour state.
Solution: Added `BulkheadStateDTO` as exact 32-byte explicit layout and registered `Shinobu220Bulkhead*` BufferIDs in `H8Memory` with Construction ownership.
Rejected Alternatives: Local persistent `NativeArray` ownership was rejected because Global Authority Boundaries require cross-domain state to live in DataVault. Managed dictionaries were rejected because KCC/rollback cannot memcpy them.
Scalability potential: Low uses 256-state fixed lane with 5 Hz authority; Middle raises cadence smoothly; High/Ultra keep the same state truth and spend saved CPU on shader distortion only.
Hardware Impact: Low-end i3/MX350 expected gain is 5-20 us per active door cluster by removing Transform/Animator state as the gameplay authority and avoiding broadphase churn.

## Decision 2026-05-20 - BaseAirlock Publish-Only Boundary
Problem: `BaseAirlock` moved `emergencyBulkheadDoorMesh.localPosition` and played a clang as if the object was the emergency door.
Solution: Removed the mesh transform door fields and replaced them with edge hash plus mathematical plane dimensions. `BaseAirlock` now publishes lock intent to `BulkheadContainmentRuntime`.
Rejected Alternatives: Keeping a hidden Transform as visual truth was rejected because it keeps the physical-door model alive. Adding a disabled collider fallback was rejected because it invites broadphase toggles.
Scalability potential: Low has zero per-frame door Transform writes; Middle keeps pressure whistle logic; High/Ultra render closure through UberNoir with continuous quality.
Hardware Impact: Estimated 2-8 us saved per door per frame on weak CPUs from eliminating localPosition writes and hierarchy dirtiness; more if prefabs previously had animated child graphs.

## Decision 2026-05-20 - KCC Plane Result Instead Of Direct KCC Patch
Problem: The player controller is a separate authority domain, and direct edits risk colliding with Agent 113 work.
Solution: `EvaluateDoorCollisionsJob` runs in PreSimulation and writes `BulkheadCollisionResultDTO` to `Shinobu220BulkheadCollisionResults`; `PlayerKinematicsRuntime` consumes that data-only result and projects position/velocity out of the closed plane. Plane math subtracts AUP doubles before float projection.
Rejected Alternatives: Unity colliders were rejected because closure would require broadphase updates. Direct Construction object references in KCC were rejected; the bridge is DataVault-only.
Scalability potential: Low tests only active planes at low cadence; Middle/High increase cadence with `HomeostasisBrain.GlobalQualityWeight`; Ultra can widen debug/telemetry without changing solver semantics.
Hardware Impact: Expected gain is 10-40 us per moving door avoided versus BoxCollider/MeshCollider broadphase updates; plane query cost is linear over active bulkheads and fixed-capacity.

## Decision 2026-05-20 - Dear Lie Shader Closure
Problem: The visual door still needs to read as heavy steel without CPU geometry or a physical door object.
Solution: Added `_GlobalBulkheadStates` StructuredBuffer and `_GlobalBulkheadParams` to UberNoir. Vertex deformation uses `ClosureProgress` to slide/split vertices and distort normals.
Rejected Alternatives: CPU mesh deformation, Animator clips, and instantiated door meshes were rejected because they duplicate gameplay truth and dirty transforms.
Scalability potential: Low uses simple vertical displacement; Middle adds center split; High/Ultra increase ripple/normal distortion through continuous quality weight.
Hardware Impact: Moves visual cost to the GPU vertex path; expected CPU saving remains 2-20 us per active bulkhead while GPU cost scales with doorway vertex count and quality.

## Decision 2026-05-20 - Black Box And Tooling
Problem: The system needs crash explainability, tuning, CSV profiles, and an architectural scan without allocating in hot paths.
Solution: Added `BulkheadTelemetryEntry[300]`, `Dump_SHINOBU_220.bin` writer, UI Toolkit tuner, span-based CSV profile parser, and `DoorPhysicsInquisition` report route.
Rejected Alternatives: Runtime string CSV parsing and log-only telemetry were rejected; both produce GC or fail black-box requirements.
Scalability potential: Low writes compact telemetry and skips expensive visuals; Middle/High expose live tuning; Ultra can use the same telemetry to justify visual overkill.
Hardware Impact: Hot-path telemetry is fixed-size native writes. Dump and CSV file allocations are cold/editor or fault-path only; after Loop 20, empty routes write a direct telemetry row and active routes schedule a small telemetry job every Simulation frame.

## Decision 2026-05-20 - Contract Bridge Instead Of Gameplay Construction Import (SUPERSEDED)
Superseded By: `BulkheadContainmentIntentBus` unmanaged DataVault ingress in the later hardening pass below.
Problem: `BaseAirlock` and the KCC bridge had direct Construction namespace coupling. That is a compile-wall risk if Gameplay becomes its own assembly and violates the mandate's sibling-runtime isolation intent.
Solution: Moved KCC collision payloads into `Hecton8.Core.Contracts` and added `IBulkheadContainmentPublisher`/`BulkheadContainmentBridge` for the cold BaseAirlock lockdown publish. `BulkheadContainmentRuntime` registers the publisher on enable and unregisters on disable.
Rejected Alternatives: A new SignalBus lane was rejected because direct signal dispatch is generated/hardcoded in `GlobalSignals.cs`; adding one safely would touch a much broader Core surface. Keeping the Gameplay `using Hecton8.Construction` was rejected because it left an obvious dependency smell.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is compile-boundary hardening only. Runtime cost is one cold managed interface call when airlock lockdown changes, not a frame loop.
Hardware Impact: No measurable frame gain. The gain is iteration-time protection and KCC Burst isolation because hot KCC code now reads only a Core contract DTO.

## Decision 2026-05-20 - Vault Generation Handles
Problem: The runtime still resolved owner-local buffers via direct `GetBuffer` calls across phases, which can pin stale views and contradict the Vault generation-handle addendum in the binary ledger.
Solution: Store only `VaultGenerationHandle<T>` descriptors for SHINOBU_220 owner buffers and resolve transient `NativeArray<T>` views inside the phase using them. Gizmo also resolves handles instead of owner `GetBuffer`.
Rejected Alternatives: Persistent `NativeArray` fields and legacy `VaultBufferHandle<T>` were rejected because DataVault is the owner and relocation must be possible. Raw pointer caching was rejected because jobs receive pointers only at schedule time.
Scalability potential: Same data route from weak devices to high-end machines; the handle layer allows future DataVault defrag without changing bulkhead math.
Hardware Impact: Removes stale-pointer risk. Runtime cost is a metadata resolve per phase, not per element; hot Burst loops still operate over raw pointers.

## Decision 2026-05-20 - Catastrophic Integrity Lane
Problem: Catastrophic damage originally read the conductivity scalar as fake parent integrity. That made room collapse semantics dishonest and could never represent independent structural failure.
Solution: Added `Shinobu220BulkheadModuleIntegrity` (72013). BaseAirlock publishes normalized `CurrentIntegrity / MaxIntegrity`; `ApplyCatastrophicDoorDamageJob` consumes that lane, sets `SiblingNodeHash = 0`, marks `Destroyed|Jammed|CatastrophicDamage`, and clamps visual closure to `0.73`.
Rejected Alternatives: Spawning broken door debris or rigidbody wreckage was rejected as physics work. Reusing conductivity was rejected as shadow-state corruption.
Scalability potential: Low uses the same scalar, middle/high can add richer shader mangling from the flags without adding CPU simulation.
Hardware Impact: One extra float lane read in the damage job. Saved path remains collider/debris-free; expected gain is avoiding tens of microseconds of physics/object churn during collapse events.

## Decision 2026-05-20 - Split KCC And CSR Thresholds
Problem: The first pass sealed CSR at `ClosureProgress >= 0.5`, but the assignment only gives the 0.5 threshold to KCC solidity. CSR isolation must wait until the bulkhead is materially sealed.
Solution: KCC collision remains active at `ClosureProgress > 0.5`; `ApplyBulkheadLockJob` now drops conductivity/fluid flow only at `>= 0.95`. Damage runs before lock so destroyed bulkheads reopen CSR flow in the same authority tick.
Rejected Alternatives: Single shared threshold was rejected because it collapses gameplay feel and logistics truth into one scalar boundary.
Scalability potential: Low tier still integrates at 5 Hz but absolute closure time is preserved through accumulated dt; high/ultra get smoother shader perception without changing thresholds.
Hardware Impact: No added allocations or object work. The fix prevents false flood/power isolation while preserving the cheap mathematical route.

## Decision 2026-05-20 - Unmanaged Intent Bus Hardening
Problem: The cold `IBulkheadContainmentPublisher` bridge removed direct Gameplay->Construction imports, but still left a managed interface registration path that looked like ordinary OOP service wiring.
Solution: Replaced it with `BulkheadContainmentIntentDTO[256]` and `BulkheadContainmentIntentControlDTO[1]` in the GlobalDataVault. `BaseAirlock` writes one blittable intent packet through `BulkheadContainmentIntentBus`; `BulkheadContainmentRuntime` consumes the ring in `PreSimulation` and then mutates its owner-local state, plane, CSR, and integrity lanes.
Rejected Alternatives: Editing `GlobalSignals.cs` for a one-off typed lane was rejected because generated/hardcoded dispatch tables are a wider Core compile-wall risk during a parallel batch. Keeping the interface bridge was rejected because it preserved managed object dispatch where a flat unmanaged packet is enough.
Scalability potential: Low/Middle/High/Ultra all use the same single unmanaged route. Quality still changes authority cadence and shader deformation continuously; the intent lane does not create a hardware tier branch.
Hardware Impact: No measured frame gain claimed. Cold publish removes interface dispatch and runtime object registration; hot frame cost is unchanged except for a `write == read` cursor check before PreSimulation collision work.

## Decision 2026-05-20 - Telemetry Timing Honesty
Problem: `LastCpuMicroseconds` implied measured Burst execution time, but without completing the scheduled handle in-frame the runtime can only measure enqueue/schedule cost.
Solution: Renamed the value to `LastScheduleMicroseconds` and tags telemetry rows with `ScheduleTimeOnly`. Actual Burst execution proof remains pending Unity Profiler/SystemDispatcher timing evidence.
Rejected Alternatives: Calling `JobHandle.Complete()` for timing was rejected because it would stall the dispatcher and violate dependency chaining. Leaving the CPU label was rejected as fake proof.
Scalability potential: Low tier still sheds schedule frequency through the cadence curve; high/ultra still spend saved CPU on shader detail. The metric now accurately describes what is measured.
Hardware Impact: No hot-path cost change. This prevents bad performance decisions based on mislabeled timing data.

## Decision 2026-05-20 - No Resurrection After Catastrophic Door Crush
Problem: A later BaseAirlock publish could clear the `Destroyed` bit while reasserting a lock intent, briefly turning a crushed bulkhead back into an ordinary jammed blocker before the damage job ran again.
Solution: Cold intent consumption now only asserts `Active` and writes the current lock/sibling/plane data; it does not clear `Destroyed`, `Jammed`, or `CatastrophicDamage`. Catastrophic state remains authoritative until a dedicated repair/rebuild owner exists.
Rejected Alternatives: Auto-clearing damage on any fresh BaseAirlock publish was rejected because it creates a shadow repair fact and can let KCC/CSR observe a resurrected door for one frame.
Scalability potential: All tiers keep the same deterministic damage state; high/ultra can still render richer mangling through flags without CPU debris.
Hardware Impact: No runtime cost change. The fix prevents one-frame authority drift after collapse.

## Decision 2026-05-20 - Contracts Assembly Purity
Problem: The unmanaged intent DTO file lived in `Core/Contracts`, but the first hardening pass also placed the DataVault-writing bus in the same file. `Hecton8.Core.Contracts.asmdef` cannot reference `Hecton8.Core.Memory` because `Core.Memory` already imports contracts.
Solution: Left only pure flags/DTOs in `BulkheadContainmentContracts.cs` and moved `BulkheadContainmentIntentBus` to `Assets/_Project/Scripts/Core/BulkheadContainmentIntentBus.cs`, which is compiled by the root `Hecton8.Core` asmdef. The public namespace remains `Hecton8.Core.Contracts`, so BaseAirlock call sites do not gain Construction coupling.
Rejected Alternatives: Adding a `Hecton8.Core.Memory` reference to `Hecton8.Core.Contracts.asmdef` was rejected as a circular compile wall. Moving DTOs into Construction was rejected because Gameplay/KCC need a sibling-neutral data contract.
Scalability potential: No runtime quality change. Low/Middle/High/Ultra still use one unmanaged DataVault intent route and continuous q-driven authority cadence.
Hardware Impact: Frame hot cost remains 0 us. The gain is compile isolation: contracts stay memory-agnostic and the DataVault write helper sits in an assembly that already owns the required dependencies.

## Decision 2026-05-20 - Startup Intent Retry Without Registration OOP
Problem: `BaseAirlock.OnEnable` can fire before the DataVault is registered. A one-shot publish would silently drop the initial locked/unlocked bulkhead intent, leaving the CSR/KCC lane stale until the next manual state change.
Solution: `PublishBulkheadContainmentState` now returns success and sets `_bulkheadContainmentPublishPending` on failure. `Tick` retries with a 15-tick countdown only while pending; success clears the flag and the retry path becomes inactive.
Rejected Alternatives: Reintroducing a managed runtime registration callback was rejected because it restores the object-service path removed by the intent bus. Publishing every tick forever was rejected because it turns a boot-order problem into persistent traffic.
Scalability potential: Low tier pays only during boot gaps; Middle/High/Ultra behavior is identical after the first accepted intent. The mathematical closure lane remains q-driven.
Hardware Impact: Post-success frame cost is zero beyond the existing false-branch check. During boot gaps the cost is one byte decrement per tick and one DataVault write attempt every 16 ticks per airlock.

## Decision 2026-05-20 - Explicit AUP Publish Conversion
Problem: The cold publish path still called `centerAup.ToAbsoluteDouble3()` directly from `BaseAirlock` and the static runtime publish helper. The method exists today, but the bulkhead route already owns the exact AUP arithmetic and should not depend on an incidental helper surface during a compile-wall-sensitive batch.
Solution: `BaseAirlock` now uses a local `ToBulkheadAbsoluteDouble3(in AbsoluteUniversePosition)` helper with explicit `Grid * AbsoluteUniversePosition.CellSizeMeters + Local` math. `BulkheadContainmentRuntime.TryPublishAirlockBulkheadState` now calls `BulkheadContainmentMath.ToAbsoluteDouble3(in centerAup)`, the same helper used by Burst-side plane/KCC code.
Rejected Alternatives: Leaving the instance helper call was rejected because it hides the AUP conversion dependency behind a method outside the bulkhead route. Moving the helper into Core.Contracts was rejected because it would expand contract surface during parallel integration.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is deterministic coordinate hygiene only. Shader quality and authority cadence remain driven by continuous `GlobalQualityWeight`.
Hardware Impact: Hot-frame impact is 0 us. Cold publish still performs three double multiplies/adds and writes one unmanaged intent packet; no managed allocation or object route is introduced.

## Decision 2026-05-20 - GPU Upload Double Buffer And Dirty Gate
Problem: The Dear Lie shader upload path used one `GraphicsBuffer` and uploaded the bulkhead DTO payload every VisualSync even when the state hash had not changed. `LockBufferForWrite` avoided managed staging, but the route still risked GPU/driver synchronization and wasted PCIe or unified-memory bandwidth.
Solution: `BulkheadContainmentRuntime` now owns two persistent shader buffers, `_shaderStateBufferA` and `_shaderStateBufferB`, with byte read/write slots instead of a managed array. VisualSync writes only the inactive slot, flips it into the read slot after a successful `UnsafeUtility.MemCpy`, and binds that read buffer globally. Uploads are gated by telemetry `StateHash` plus `uploadCount`; unchanged states skip the StructuredBuffer write and only update `_GlobalBulkheadParams`.
Rejected Alternatives: Keeping a single buffer was rejected because CPU writes can contend with the GPU read timeline. Using `GraphicsBuffer.SetData` was rejected because the project bandwidth discipline requires `LockBufferForWrite` with direct memcpy. A `GraphicsBuffer[]` wrapper was rejected to avoid introducing a private managed array field just to hold two stable slots.
Scalability potential: Low tier benefits most because authority cadence collapses toward 5 Hz, so most visual frames reuse the last state buffer while shader params still move continuously. Middle keeps reduced upload frequency proportional to actual state changes. High/Ultra still get full visual deformation when state changes, but do not waste bandwidth on identical DTO payloads.
Hardware Impact: Unchanged VisualSync frames avoid `32 bytes * uploadCount` payload traffic. At 256 rows this is 8192 bytes per skipped frame, which matters on MX350/Quest unified-memory pressure. Exact frame-time delta remains pending Unity profiler/Frame Debugger proof.

## Decision 2026-05-20 - Shader Global Fail-Closed Gate
Problem: After a valid upload, `_GlobalBulkheadParams.y` could remain active if shader upload was later disabled, Vault generation-handle resolution failed, a read buffer was missing, or `EnsureGraphicsBuffers()` released buffers before a failed recreate. That leaves a visual-only stale bulkhead route even though the owner Vault truth is unavailable.
Solution: Added `_shaderGlobalsActive` and `DisableShaderGlobals()`. VisualSync calls it on disabled upload, Vault failures, invalid resolved arrays, or missing read buffer; shutdown and `ReleaseGraphicsBuffers()` call it before freeing buffers. The function writes `_GlobalBulkheadParams = Vector4.zero`, clears the valid-read flag, and marks upload dirty for the next successful activation.
Rejected Alternatives: Clearing the StructuredBuffer binding itself was rejected because `Shader.SetGlobalBuffer` null semantics are Unity-version-sensitive and unnecessary; the shader already guards on `_GlobalBulkheadParams.y`. Leaving stale params until the next successful VisualSync was rejected because allocation failure and disabled upload are exactly the frames where no later overwrite is guaranteed.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged while valid. Under thermal or editor-driven upload disable, the shader collapses to the cheapest no-op path through the existing `enabled <= 0` branch with no binary quality tier branch.
Hardware Impact: Enabled hot path adds one bool store after `SetGlobalVector`; disabled/failure path emits one global vector write only on active-to-inactive transition. It prevents stale GPU vertex deformation and avoids per-frame zero-global spam.

## Decision 2026-05-20 - Zero-Active Visual Bypass
Problem: `VisualSyncTick` still resolved Vault state and created double `GraphicsBuffer` lanes before discovering there were no active bulkhead owner rows. The previous `uploadCount = 1` clamp was correct for buffer indexing but wrong for empty scenes because it kept the shader route enabled with no mathematical door truth.
Solution: Added an `_activeCount <= 0` guard immediately after the upload-enabled check. Empty scenes call `DisableShaderGlobals()` and return before Vault resolution or graphics buffer creation. Valid active scenes keep the existing double-buffered upload and state-hash gate.
Rejected Alternatives: Uploading row zero as a sentinel was rejected because it wastes GPU branch work and can keep a visual proof artifact active without an owner fact. Scanning the `BulkheadStateDTO` array in VisualSync to find active rows was rejected because it moves owner truth discovery onto the main/render phase; `_activeCount` is already the cold owner row bound.
Scalability potential: Low tier gets the largest benefit because no active base bulkheads means no shader-buffer allocation or vertex branch at all. Middle/High/Ultra behavior is unchanged once a bulkhead owner row exists, and continuous q still drives cadence and deformation.
Hardware Impact: Avoids allocating two `GraphicsBuffer` objects of `256 * 32` bytes each in empty scenes and removes the per-vertex bulkhead enabled path until a real owner row exists. Runtime proof remains pending Unity profiler/Frame Debugger.

## Decision 2026-05-20 - Zero-Active KCC Job Bypass
Problem: `PreSimulationTick` still scheduled `EvaluateDoorCollisionsJob` with `Count = 0` just to clear the single KCC collision result row when no bulkhead owner rows existed. That preserved a dispatcher job and active-job registration for an inactive mathematical route.
Solution: Reset the pre-simulation handle/scheduled flag at phase entry, then after unmanaged intent ingestion compute the bounded active count once. If it is zero, clear `Shinobu220BulkheadCollisionResults[0]` and return before override or collision jobs are scheduled.
Rejected Alternatives: Keeping the zero-count Burst job was rejected because it spends scheduler overhead to do one scalar clear. Moving the clear into KCC was rejected because bulkhead containment owns the collision proof artifact and KCC must only consume it.
Scalability potential: Low tier and empty-start scenes pay no bulkhead PreSimulation schedule until a real owner row exists. Middle/High/Ultra behavior is unchanged after activation; continuous quality still controls authority cadence and shader deformation.
Hardware Impact: Saves one `IJob` schedule and one `H8Memory.RegisterActiveJob` call per empty PreSimulation tick. Exact microseconds remain profiler-pending, but this removes avoidable main-thread scheduler traffic on weak CPUs.

## Decision 2026-05-20 - KCC Stale Frame Fence
Problem: `PlayerKinematicsRuntime.TryApplyBulkheadCollisionResult` accepted any blocked collision row if the depth was finite and positive. If PreSimulation failed to refresh the result, KCC could reuse a stale closed-door proof beyond the frame it was generated for.
Solution: Add a one-frame freshness fence using `result.Frame`. This was immediately hardened in the next decision to source the current frame from `SystemDispatcher.CurrentFrameId`, not `Time.frameCount`. Rows with frame zero, future frames, or age greater than one dispatcher frame are ignored.
Rejected Alternatives: Clearing stale rows from KCC was rejected because containment owns the proof artifact; KCC must not mutate the bulkhead lane. Leaving the row unbounded was rejected because stale collision is worse than a one-frame missed block.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for fresh rows. On weak devices under phase skip or temporary Vault failure, stale data collapses to no-op consumption instead of blocking movement indefinitely.
Hardware Impact: Adds one `uint` read and three integer comparisons on the KCC consumption path. It prevents persistent false-positive KCC correction without allocating memory or touching physics colliders.

## Decision 2026-05-20 - KCC Dispatcher Frame Fence
Problem: The first stale-row fence used `Time.frameCount` in a new SHINOBU_220 critical correction path. Even though surrounding legacy KCC code still uses Unity frame counters, this route must not add more local Unity-time dependency to rollback-sensitive bulkhead blocking.
Solution: Compare `BulkheadCollisionResultDTO.Frame` against `SystemDispatcher.CurrentFrameId` and reject dispatcher frame zero, future result rows, or rows older than one dispatcher frame.
Rejected Alternatives: Keeping the Unity frame check was rejected because the bulkhead row is produced by dispatcher-phase PreSimulation, so its freshness proof should use the same dispatcher frame spine. Reading or mutating the collision row from KCC was rejected because containment owns the proof artifact.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for fresh rows; phase-skipped or stale rows collapse to a cheap no-op regardless of quality weight.
Hardware Impact: Hot cost remains one static `uint` read and integer comparisons. No allocations, no collider work, no cross-domain Construction reference.

## Decision 2026-05-20 - Static Scan Wording Correction
Problem: The verification wording said forbidden-pattern scans had no hits in owned files, but the editor-only `DoorPhysicsInquisition` scanner intentionally contains `BoxCollider`, `MeshCollider`, and `Animator` string literals as signatures to detect illegal door-object code. That wording was imprecise and could be read as hiding a scanner hit.
Solution: Re-run the scan on runtime/core/shader files separately, record that it has zero hits, and explicitly document the editor scanner literals as intentional cold/editor detection signatures.
Rejected Alternatives: Removing the scanner string literals was rejected because it would blind Task 19's inquisition report. Calling the editor literals runtime dependencies was rejected because they are not executed in gameplay and do not introduce colliders, Animator paths, or Transform door motion.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this is evidence hygiene only.
Hardware Impact: Hot cost remains 0 us. The editor scan remains cold tooling and does not allocate in gameplay.

## Decision 2026-05-20 - Vault Hot-Swap Release Discipline
Problem: The generation-handle migration removed persistent NativeArray ownership, but the runtime still needed an explicit cold lifecycle path for DataVault replacement and teardown. Without release calls, hot reload or registry service replacement could leave stale generation descriptors and Vault ref counts alive after the owner route is inert. Releasing immediately is also unsafe because scheduled Burst jobs hold raw pointers into the old Vault until their handles finish.
Solution: `BulkheadContainmentRuntime` now registers as a `GlobalRegistry` hot-swap listener, handles the DataVault slot through the ref-forwarding callback, disables shader globals immediately, releases graphics buffers immediately, stores the pending Vault, and refuses to resolve the stale Vault while a rebind is pending. `TryFlushPendingDataVaultRebind()` releases all 16 SHINOBU_220 Vault generation handles only after `_preSimulationHandle.IsCompleted` and `_simulationHandle.IsCompleted`; it then reacquires from the current Vault through the normal `EnsureVaultState()` path. The editor gizmo normal conversion was also made explicit from `float3` to `Vector3` to remove a compile-risk dependency on implicit conversion operators.
Rejected Alternatives: Polling `GlobalRegistry.DataVault` per phase was rejected because registry polling belongs to boot/rebind paths, not hot execution. Clearing handles without `IDataVault.ReleaseBuffer` was rejected because it hides ownership leaks. Calling `JobHandle.Complete()` before release was rejected because it would stall the dispatcher. Immediate release on registry callback was rejected because it can free memory still referenced by scheduled jobs.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. Under editor hot reload or Vault replacement, the shader route fails closed with `_GlobalBulkheadParams = 0` and no stale visual proof. Once reacquired, continuous `GlobalQualityWeight` again controls cadence and deformation.
Hardware Impact: Hot-frame cost remains 0 us outside a pending rebind. Cold replacement pays two job-handle completion checks, up to 16 release calls, and two graphics-buffer releases at most, avoiding persistent native memory pressure on weak devices while preventing use-after-free of old Vault memory.

## Decision 2026-05-20 - PreSimulation Fence Ownership
Problem: The pre-simulation collision job was scheduled from a void dispatcher phase. It was only combined into the returned Simulation handle when the slower authority cadence fired. On cadence-skip frames, the job could be active without being returned to the central Simulation fence, which weakens KCC freshness and makes deferred Vault release rely on a handle the dispatcher did not necessarily complete.
Solution: `ScheduleSimulation()` now creates a `dependency` from `_preSimulationHandle` before cadence checks. Every early exit returns that dependency, so the central dispatcher owns the pre-simulation job even when no authority closure update is due. Pending DataVault rebind reset preserves scheduled flags until that Simulation phase can collect the handle, then clears them only after the deferred Vault release is flushed. The full closure/CSR/telemetry chain still appends to the same dependency when cadence fires.
Rejected Alternatives: Completing the pre-simulation handle locally was rejected because it would stall the main thread and violate the dispatcher-owned fence model. Moving KCC collision into the slower authority job was rejected because KCC blocking must be refreshed on PreSimulation cadence, not only on closure authority cadence.
Scalability potential: Low/Middle/High/Ultra behavior remains continuous. Low quality still skips expensive authority updates by cadence, but the cheap collision proof is fenced correctly; high/ultra still run smoother closure updates without changing KCC semantics.
Hardware Impact: Adds one conditional `JobHandle.CombineDependencies` only on frames with a scheduled pre-simulation job. This buys deterministic ownership and prevents unsafe old-Vault release without adding colliders or blocking waits.

## Decision 2026-05-20 - Teardown Drain And Idempotent Shutdown
Problem: After the deferred Vault release hardening, `OnDisable` unregistered dispatcher phases before requesting a DataVault rebind. If a SHINOBU_220 job was still active, `TryFlushPendingDataVaultRebind()` correctly refused to free raw-pointer memory, but no later phase was guaranteed to run and clear the pending Vault release. The system was use-after-free safe but could retain Vault references during shutdown.
Solution: Added `ShutdownRuntime(forceCompletePendingJobs: true)` and routed both `OnDisable` and `Application.quitting` through it. Shutdown is idempotent, unregisters callbacks once, drains only scheduled SHINOBU_220 handles through `DispatcherJobFence.TryComplete(..., forceComplete: true)`, then calls the same pending rebind/release path. `Application.quitting` no longer calls `OnDisable()` manually.
Rejected Alternatives: Keeping deferred release with no remaining phase pump was rejected as a cold native-memory retention risk. Immediate `ReleaseBuffer` without draining handles was rejected because Burst jobs may still own raw pointers. Manually invoking `OnDisable()` from the quit callback was rejected because lifecycle recursion hides ordering and can double-run partial cleanup.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. The drain is cold teardown only; runtime cadence and shader deformation still scale continuously from 5 Hz authority on weak devices to high-frequency visual overkill on strong machines.
Hardware Impact: Frame-loop cost is 0 us. Teardown may wait on at most the active SHINOBU_220 pre-simulation and simulation handles; that is an explicit shutdown boundary, not a gameplay-frame wait.

## Decision 2026-05-20 - Every-Frame Black Box Telemetry
Problem: The first telemetry implementation wrote the 300-frame ring only when the authority closure cadence fired. At low `GlobalQualityWeight`, authority can collapse toward 5 Hz, which means the black box could miss multiple dispatcher frames immediately before a fatal NaN or shutdown fault.
Solution: `ScheduleSimulation()` now records telemetry on every Simulation phase. The expensive closure, catastrophic damage, and CSR lock jobs remain behind the continuous cadence gate. On cadence-skip frames, only states/collision/telemetry/cursor buffers resolve and `RecordBulkheadTelemetryJob` writes a black-box row. If no bulkheads are active, the runtime writes a direct zero telemetry row and schedules no no-op job.
Rejected Alternatives: Keeping telemetry at authority cadence was rejected because it weakens crash autopsy proof. Scheduling the whole closure chain every frame was rejected because it violates continuous scalability. Resolving CSR/conductivity/fluid/integrity just to write telemetry was rejected because it increases failure surface and Vault metadata traffic on cadence-skip frames.
Scalability potential: Low tier preserves 5 Hz closure authority while retaining 60 Hz black-box observability; middle/high/ultra keep smoother authority cadence and the same every-frame forensic ring. The visual Dear Lie remains shader-driven by q.
Hardware Impact: Empty scenes pay one direct NativeArray row write and no scheduled job. Active scenes pay one small `IJob` schedule per Simulation frame for telemetry; the closure/CSR chain still sheds work continuously based on `GlobalQualityWeight`.

## Decision 2026-05-20 - Telemetry Length And Pending-Producer Guards
Problem: Every-frame telemetry introduced more frequent writes to the 300-frame ring, but the first hardening pass still assumed fixed-size Vault lanes were nonzero and that the empty-route direct write could never overlap a pending PreSimulation producer. A hot-swap, partial Vault failure, or future call-site drift could turn that into a modulo-zero fault or a data race on the collision proof row.
Solution: `ScheduleSimulation()`, `ScheduleTelemetryJob()`, and `VisualSyncTick()` now reject empty state/collision/telemetry/cursor lanes before unsafe pointer extraction or cursor modulo. If active count resolves to zero while `_preSimulationScheduled` is still true, the runtime schedules a zero-count telemetry job chained behind the pre-sim dependency instead of writing the ring directly. `RecordBulkheadTelemetryJob` also exits on invalid pointer/count inputs before reading `Cursor[0]` or `CollisionResult[0]`.
Rejected Alternatives: Trusting boot-time fixed-capacity requests was rejected because DataVault hot-swap can invalidate assumptions. Forcing all empty routes through a no-op telemetry job was rejected because normal inactive scenes should remain scheduler-inert. Completing the pre-sim handle before direct write was rejected because it would block the frame loop.
Scalability potential: Low tier keeps the cheap direct empty-route row and low closure cadence. Middle/high/ultra behavior is unchanged; the only extra path is a rare dependency-correct telemetry job when a pending producer must be fenced.
Hardware Impact: Normal active path pays integer length checks before scheduling telemetry. Normal empty path remains one direct row and no job. Rare pending-producer empty path pays one tiny IJob to avoid a race; no object, collider, or physics route is introduced.

## Decision 2026-05-20 - BaseAirlock Bulkhead Shadow State Purge
Problem: `BaseAirlock` still kept `_bulkheadClosureIntent01`, a local float shadow of closure intent used only to gate the pressure whistle. It was not gameplay-authoritative, but the name and scalar preserved object-door semantics beside the Vault-owned `BulkheadStateDTO` truth.
Solution: Removed `_bulkheadClosureIntent01` and `SetBulkheadClosureIntent`. Audio whistle gating now uses `_emergencyLockedDown` only; closure progress remains owned exclusively by the containment Vault lane and shader upload route.
Rejected Alternatives: Keeping the audio-only float was rejected because shadow state is exactly how standard door objects creep back in. Reading `BulkheadStateDTO` from Gameplay for audio was rejected because it would add cross-owner polling and a DataVault read to a cosmetic path.
Scalability potential: Low tier avoids one more local scalar branch and preserves the no-object model. Middle/high/ultra still get the same shader Dear Lie and continuous q-driven closure cadence from the owner runtime.
Hardware Impact: Removes two cold writes and one scalar read in whistle gating. The measurable gain is negligible; the authority gain is that closure progress exists in one owner route only.

## Decision 2026-05-20 - Black-Box Dump Before Shader Fail-Closed Branches
Problem: `RecordBulkheadTelemetryJob` can mark `DumpRequested`, but the previous dump call lived after successful shader buffer resolution and upload. If shader upload was disabled or failed closed because the visual route was inactive, the black-box file would not be written even though the solver requested forensic output.
Solution: Added `DumpBlackBoxIfRequested(IDataVault)` and call it at the start of `VisualSyncTick()` after Vault rebind handling, before `uploadShaderBuffer`, active-count, and graphics-buffer checks. The helper resolves only telemetry and cursor lanes, validates nonzero lengths, reads the latest ring row, and emits the dump when `DumpRequested` is set.
Rejected Alternatives: Keeping dump behind the shader upload path was rejected because crash evidence must not depend on visual feature enablement. Completing the telemetry job immediately in Simulation was rejected because it would stall the frame. Leaving both old and new dump checks was rejected because it could double-write the same flagged row.
Scalability potential: Low tier can disable or fail-close shader upload and still preserve fatal telemetry. Middle/high/ultra keep the same Dear Lie visual path; forensic output is now orthogonal to visual quality.
Hardware Impact: Adds one telemetry/cursor descriptor resolve and latest-row flag check per VisualSync. Fault-path file I/O remains gated by `DumpRequested`; no collider, object, or physics route is introduced.
