# Status_SHINOBU_220

Agent: SHINOBU_220
Role: EMERGENCY_BULKHEAD_INJECTOR
Domain: ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT
Task Count: 20
Status: POLISH LOOP ACTIVE / INTENT BUS HARDENED / AUP CONVERSION HARDENED / GPU DOUBLE BUFFER HARDENED / SHADER GLOBAL FAIL-CLOSED / ZERO-ACTIVE VISUAL BYPASS / ZERO-ACTIVE KCC JOB BYPASS / KCC DISPATCHER FRAME FENCE / VERIFY_WORDING_CORRECTED / VAULT_HOTSWAP_RELEASE_HARDENED / PRE_SIM_FENCE_HARDENED / TEARDOWN_DRAIN_HARDENED / UNITY IMPORT HUNG

## Mandates Read
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- PHYS_Fluid_Incursion_Interior.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt

## Current Architecture Facts
- No `BulkheadDoor.cs` or `AirlockController.cs` exists under Construction/Gameplay.
- `BaseAirlock` no longer owns emergency bulkhead mesh movement, `localPosition` sliding, clang-on-slide logic, or a direct Construction namespace import.
- `BaseAirlock` publishes unmanaged `BulkheadContainmentIntentDTO` packets through `Hecton8.Core.Contracts.BulkheadContainmentIntentBus`; `BulkheadContainmentRuntime` consumes the DataVault ingress ring and owns the state lane.
- `BaseAirlock` retries the cold initial publish only while DataVault ingestion is unavailable; after one successful intent write, no retry branch is active.
- `BulkheadContainmentIntentDTO` remains in `Hecton8.Core.Contracts`; the DataVault-writing `BulkheadContainmentIntentBus` lives under the root `Hecton8.Core` assembly to avoid a `Core.Contracts -> Core.Memory` circular reference.
- `PlayerKinematicsRuntime` consumes only `Hecton8.Core.Contracts.BulkheadCollisionResultDTO`, not Construction runtime types.
- New DataVault route card: `Docs/Tasks/Route_SHINOBU_220_BulkheadContainment.md`.
- Static report: `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`.

## Loop 1 Tasks 01-05
- [x] Task 01 DOOR_GAMEOBJECT_INQUISITION | DOD: `rg` scan of Construction/Gameplay; no legacy `BulkheadDoor.cs`/`AirlockController.cs`; BaseAirlock was the emergency mesh path. Rejected: blanket deletion of unrelated `SealedDoor`. Estimate: 2-8 us/frame per airlock Transform path removed.
- [x] Task 02 PHYSICAL_COLLIDER_DOOR_PURGE | DOD: KCC blocking is `BulkheadCollisionResultDTO` from planes, no collider toggles. Rejected: BoxCollider/MeshCollider broadphase gate. Estimate: 10-40 us/event avoided for moving door broadphase.
- [x] Task 03 CS1612_METADATA_STATE_ANNIHILATION | DOD: `BulkheadStateDTO` uses raw fields only, no properties. Rejected: get/set wrappers on NativeArray elements. Estimate: prevents copy-back bugs; hot cost 0 us.
- [x] Task 04 ARM64_BULKHEAD_LAYOUT_VALIDATION | DOD: `BulkheadStateLayoutGuard` uses `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`; runtime checks once, then caches the result. Rejected: assumed packing and repeated Marshal reflection. Estimate: hot cost 0 us after boot.
- [x] Task 05 EMERGENCY_MOCK_BULKHEAD_DATA | DOD: `GenerateMockBulkheadsJob` creates deterministic test states/planes/CSR edges. Rejected: waiting for hand-authored base topology. Estimate: cold-only generation.

## Loop 2 Tasks 06-10
- [x] Task 06 BURST_BULKHEAD_CLOSURE_KERNEL | DOD: deterministic Burst `UpdateBulkheadClosureJob`, raw pointers, `[NoAlias]`, dt-scaled closure. Rejected: MonoBehaviour Update animation. Estimate: 1-4 us for 256 entries at low cadence.
- [x] Task 07 CSR_EDGE_LOCK_MATHEMATICS | DOD: CSR conductivity/fluid flow seal only at `ClosureProgress >= 0.95`; KCC solidity remains `> 0.5`. Rejected: early 0.5 CSR sealing that would over-isolate logistics. Estimate: 5-20 us/event avoided versus object/collider gates.
- [x] Task 08 THE_DEAR_LIE_SHADER_DOORS | DOD: UberNoir reads `_GlobalBulkheadStates` and displaces vertices procedurally. Rejected: CPU mesh deformation/Animator. Estimate: 2-20 us CPU saved per active door.
- [x] Task 09 MANUAL_OVERRIDE_INTERACTION | DOD: `ProcessDoorOverrideJob` scans `InteractionUiSignal` and toggles `AssociatedLock` using double AUP distance. Rejected: managed event lookup loops. Estimate: 1-3 us for 32 signals.
- [x] Task 10 ASYNCHRONOUS_KCC_BLOCKING | DOD: PreSimulation writes one collision result; KCC projects position/velocity from data only. Rejected: Unity collider blocking. Estimate: 3-12 us for 256 planes plus sub-1 us KCC correction.

## Loop 3 Tasks 11-15
- [x] Task 11 CONTINUOUS_SCALABILITY_REPLAY_TIERS | DOD: cadence = `lerp(5 Hz, 30 Hz, q*q)` from `HomeostasisBrain.GlobalQualityWeight`; shader also consumes continuous q. Rejected: binary low/high switch. Estimate: low tier saves 60-80% authority ticks.
- [x] Task 12 DESTRUCTIVE_IMPLOSION_OVERRIDE | DOD: `Shinobu220BulkheadModuleIntegrity` lane feeds damage job; damage runs before CSR lock so destroyed doors leak in the same authority tick. Rejected: reusing conductivity as fake integrity. Estimate: avoids debris/collider cost entirely.
- [x] Task 13 AUP_PRECISION_PLANE_MATH | DOD: player/bulkhead AUP double subtraction before float plane projection. Rejected: premature world-float casts. Estimate: correctness gain; hot cost under 1 us.
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: deterministic Burst jobs and memcpy-safe `BulkheadStateDTO` lane. Rejected: managed state snapshots. Estimate: snapshot is blind memcpy; no per-field serialization.
- [x] Task 15 TELEMETRY_BULKHEAD_RECORDER | DOD: 300-entry telemetry ring and `Dump_SHINOBU_220.bin` fault dump. Rejected: log-only crash evidence. Estimate: under 5 us at authority cadence.

## Loop 4 Tasks 16-20
- [x] Task 16 BULKHEAD_TUNER_EDITOR_WINDOW | DOD: UI Toolkit window for close/open speed, override distance, catastrophic threshold, CSV import. Rejected: runtime IMGUI/debug text. Estimate: editor-only.
- [x] Task 17 CSV_BULKHEAD_PROFILES_INGESTOR | DOD: `ReadOnlySpan<byte>` parser writes `BulkheadProfileDTO` without hot allocations. Rejected: string-split CSV. Estimate: cold/editor-only.
- [x] Task 18 LIVE_DOOR_DEBUG_GIZMO | DOD: `OnDrawGizmos` resolves generation handles and draws green/orange/red planes plus yellow normals. Rejected: direct owner `GetBuffer` polling in gizmo after the handle migration. Estimate: editor-only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: `DoorPhysicsInquisition` editor command plus JSON report. Rejected: chat-only proof. Estimate: editor-only.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: route card, layout test, rg checks, diff check, log audit appended. Rejected: untracked global route. Estimate: hot cost 0 us.

## Loop 5 Strict Self-Review
- [x] Re-read task block using CLI extraction from `CURRENT_BATCH.md`.
- [x] Re-read own code paths for old bulkhead `localPosition`, `Complete()`, and nonexistent origin symbols; no hits in owned runtime/KCC bridge paths.
- [x] Verified `git diff --check` on touched files: whitespace clean; only CRLF warnings on tracked pre-existing files.
- [x] Verified CPU gate before build: `Win32_Processor.LoadPercentage = 100`; no `dotnet`/`csc` process active; build not launched per project rule.

## Loop 6 Polish Mandate Corrections
- [x] Removed KCC/BaseAirlock direct Construction namespace dependency. DOD: `rg "using Hecton8\.Construction|BulkheadContainmentRuntime"` on those files reports no hits. Rejected: new SignalBus lane because direct dispatch tables are generated/hardcoded and would risk a broader compile wall.
- [x] Migrated owner-local buffers to `VaultGenerationHandle<T>` descriptors. DOD: no private persistent `NativeArray`, `NativeList`, or `NativeHashMap` fields in `BulkheadContainmentRuntime`. Rejected: cached `VaultBufferHandle<T>`/pointer ownership.
- [x] Added module integrity lane `72013`. DOD: BaseAirlock publishes normalized parent integrity; damage job reads that lane and sets `SiblingNodeHash = 0`, `Destroyed`, `Jammed`, `ClosureProgress = 0.73`.
- [x] Corrected CSR seal threshold. DOD: `ApplyBulkheadLockJob` now seals graph edges only at `0.95`; KCC blocking remains `0.5` as required.

## Loop 7 Anti-OOP Hardening
- [x] Replaced managed publisher interface with unmanaged intent lane. DOD: `IBulkheadContainmentPublisher` and `BulkheadContainmentBridge` have zero source hits; ingress is `BulkheadContainmentIntentDTO[256]` plus 64-byte control row in DataVault. Rejected: direct SignalBus table edit because it expands Core generated dispatch surface during a parallel batch. Estimate: frame hot cost 0 us; cold publish removes interface dispatch and runtime object registration.
- [x] Purged residual `bulkheadSlide` semantics. DOD: BaseAirlock now stores only `_bulkheadClosureIntent01`; no `bulkheadSlide` or `SetBulkheadSlideTarget` hits remain. Rejected: keeping legacy names as "just local visual state" because it preserved the physical door mental model.
- [x] Corrected telemetry wording. DOD: the runtime now records `LastScheduleMicroseconds` with `ScheduleTimeOnly` flag instead of claiming unmeasured Burst CPU execution time. Rejected: fake profiler claims without `Complete` or Unity Profiler proof.
- [x] Added intent buffer IDs `72014` and `72015`. DOD: route card and H8Memory enum both list the ingress ring/control lanes.
- [x] Preserved catastrophic destroy flags across later airlock intents. DOD: cold BaseAirlock publishes can set lock intent but cannot resurrect `Destroyed|Jammed|CatastrophicDamage` state.

## Loop 8 Compile-Wall Correction
- [x] Split pure DTO contracts from the DataVault writer. DOD: `rg "Hecton8\.Core\.Memory|IDataVault|BufferID|SystemID|VaultGenerationHandle|NativeArrayOptions" Assets/_Project/Scripts/Core/Contracts/BulkheadContainmentContracts.cs` returns no hits; `BulkheadContainmentIntentBus.cs` is under the root `Hecton8.Core` assembly, which already references both `Hecton8.Core.Contracts` and `Hecton8.Core.Memory`. Rejected: adding `Hecton8.Core.Memory` to `Hecton8.Core.Contracts.asmdef`, because `Core.Memory` already imports contracts and that would create a circular compile wall. Estimate: frame hot cost 0 us; compile-boundary risk removed.
- [x] Added bounded cold publish retry for `BaseAirlock`. DOD: failed startup publish sets `_bulkheadContainmentPublishPending`; Tick retries every 16 ticks until the unmanaged DataVault packet is accepted, then clears the branch. Rejected: per-frame permanent publish spam and managed registration callbacks. Estimate: hot cost 0 us after success; pre-vault retry cost is one branch plus one byte decrement per registered airlock tick.

## Loop 9 AUP Compile-Risk Hardening
- [x] Replaced ambiguous airlock absolute-coordinate publish calls. DOD: `BaseAirlock` now converts `AbsoluteUniversePosition` to `double3` through a local arithmetic helper, and `BulkheadContainmentRuntime` uses `BulkheadContainmentMath.ToAbsoluteDouble3(in centerAup)`. Rejected: relying on the instance helper in cross-domain cold publish code when the bulkhead route already owns the exact AUP arithmetic. Estimate: hot cost 0 us; cold publish remains one blittable Vault packet.
- [x] Re-ran runtime/core/shader forbidden-pattern scan after the AUP hardening. DOD: no hits for `Animator`, door colliders, `Instantiate`, `new GameObject`, `.Complete()`, `Pack=`, unmanaged DTO properties, managed publisher bridge, residual slide naming, or fake CPU timing labels in runtime/core/shader files. Rejected: launching Unity/dotnet while the prior Bee log already shows a foreign dependency wall.

## Loop 10 GPU Bandwidth Discipline
- [x] Replaced the single shader upload buffer with two persistent `GraphicsBuffer` lanes. DOD: `BulkheadContainmentRuntime` now owns `_shaderStateBufferA`/`_shaderStateBufferB`, writes the inactive slot with `LockBufferForWrite`, then flips read/write slots before binding `_GlobalBulkheadStates`. Rejected: one-buffer CPU write while GPU may still be reading. Estimate: avoids GPU/driver sync hazard; profiler proof pending.
- [x] Added state-hash upload gating. DOD: VisualSync compares telemetry `StateHash` and `uploadCount` against the last uploaded payload; unchanged state skips the StructuredBuffer upload and only refreshes `_GlobalBulkheadParams`. Rejected: unconditional PCIe/unified-memory upload every VisualSync. Estimate: saves up to `32 bytes * active bulkheads` per unchanged visual frame, e.g. 8 KB/frame at 256 rows.
- [x] Static scan confirms no `GraphicsBuffer[]`, no `SetData`, no private persistent `NativeArray`/`NativeList`/`NativeHashMap`, and no legacy `VaultBufferHandle` field in `BulkheadContainmentRuntime`. Rejected: managed array wrapper for double-buffer slots.

## Loop 11 Shader Fail-Closed Discipline
- [x] Added `_GlobalBulkheadParams` fail-closed disable path. DOD: VisualSync now writes `Vector4.zero` once through `DisableShaderGlobals()` when shader upload is disabled, Vault/handle resolution fails, no read buffer exists, or shutdown releases GPU buffers. Rejected: leaving the prior global buffer and `_GlobalBulkheadParams.y = 1` active after runtime teardown or allocation failure. Estimate: prevents stale visible bulkhead deformation; hot cost is one branch, zero shader writes when already disabled.
- [x] Release path now zeroes shader globals before freeing double buffers. DOD: `ReleaseGraphicsBuffers()` calls `DisableShaderGlobals()` before `GraphicsBuffer.Release()`, so `EnsureGraphicsBuffers()` cannot release an active read buffer and then fail allocation while the shader still sees the route as enabled. Rejected: relying on later VisualSync success to overwrite globals.

## Loop 12 Zero-Active Visual Bypass
- [x] Prevented empty-route shader buffer allocation. DOD: `VisualSyncTick` now exits through `DisableShaderGlobals()` before resolving the Vault or creating `GraphicsBuffer` lanes when `_activeCount <= 0`. Rejected: clamping `uploadCount` to 1 with no active owner rows, because that keeps `_GlobalBulkheadParams.y = 1` and forces every eligible doorway vertex through the buffer branch for no visible bulkhead. Estimate: saves one cold double-buffer allocation on scenes with no bulkheads and removes per-vertex enabled-branch work while inactive.

## Loop 13 Zero-Active KCC Job Bypass
- [x] Prevented empty-route KCC collision job scheduling. DOD: `PreSimulationTick` resets its scheduled handle at entry, consumes pending unmanaged intents, computes `count = min(_activeCount, states.Length)`, clears `Shinobu220BulkheadCollisionResults[0]`, and returns before override/collision job scheduling when `count <= 0`. Rejected: scheduling `EvaluateDoorCollisionsJob` with `Count = 0` just to write a default result, because an inactive bulkhead route must not register a dispatcher job or leave stale KCC proof. Estimate: saves one `IJob` schedule plus one active-job registry write per empty PreSimulation tick.

## Loop 14 KCC Stale Frame Fence
- [x] Prevented stale collision-row reuse in KCC. DOD: `PlayerKinematicsRuntime.TryApplyBulkheadCollisionResult` rejects bulkhead results with `Frame == 0`, future frames, or age greater than one frame. Superseded by Loop 15 to source the current frame from `SystemDispatcher.CurrentFrameId` instead of `Time.frameCount`. Rejected: trusting the latest collision row forever, because a failed PreSimulation refresh could leave a closed-door proof active after the route should be inert. Estimate: one `uint` read and integer comparisons on the KCC consumption path; prevents indefinite stale blocking.

## Loop 15 KCC Dispatcher Frame Fence
- [x] Removed the new `Time.frameCount` dependency from the SHINOBU_220 KCC freshness gate. DOD: `TryApplyBulkheadCollisionResult` now compares `BulkheadCollisionResultDTO.Frame` to `SystemDispatcher.CurrentFrameId` and rejects frame-zero dispatcher state, future rows, or rows older than one dispatcher frame. Rejected: Unity frame count in this new critical correction path because the mandate requires central simulation-frame proof, not local Unity time. Estimate: hot cost unchanged; one static property read plus integer comparisons.

## Loop 16 Verification Wording Correction
- [x] Corrected static-check wording after re-running forbidden-pattern scans. DOD: runtime/core/shader file list has zero hits for door-object/collider/managed-publisher/SetData/GraphicsBuffer-array patterns; `BulkheadContainmentEditor.cs` intentionally contains `BoxCollider`, `MeshCollider`, and `Animator` string literals as signatures for the inquisition scanner. Rejected: pretending the editor scanner literals are runtime dependencies or erasing the scanner's detection signatures. Estimate: hot cost 0 us.

## Loop 17 Vault Hot-Swap Release Hardening
- [x] Added DataVault hot-swap listener wiring to `BulkheadContainmentRuntime`. DOD: the runtime implements `IGlobalRegistryHotSwapListener` and `IGlobalRegistryHotSwapRefListener`, registers on enable, unregisters on disable, and rebinds only the DataVault slot. Rejected: polling `GlobalRegistry.DataVault` inside phase ticks. Estimate: hot cost 0 us; cold registry event only.
- [x] Added deterministic cold release of all SHINOBU_220 Vault generation handles after tracked jobs are no longer running. DOD: `ReleaseVaultHandles()` releases 16 handles: states, AUPs, planes, CSR edges, conductivity, fluid flow, module integrity, tuning, telemetry ring/cursor, collision result, profiles, CSV scratch, shader upload, intent ring, and intent control. Rejected: clearing handles without calling `IDataVault.ReleaseBuffer`, because it can leak ref counts across hot reload or service replacement.
- [x] Rebind path fails visually inert and defers unsafe memory release. DOD: `RequestDataVaultRebind()` disables shader globals, releases graphics buffers, resets active counts, and stores the pending Vault; `TryFlushPendingDataVaultRebind()` releases Vault handles only after `_preSimulationHandle.IsCompleted` and `_simulationHandle.IsCompleted`, without calling `Complete()`. Rejected: freeing old Vault buffers while Burst jobs may still own raw pointers.
- [x] Corrected editor gizmo conversion risk. DOD: bulkhead normal now converts `float3` to `Vector3` explicitly instead of relying on an implicit Unity.Mathematics conversion. Rejected: leaving compile behavior dependent on package conversion operators.

## Loop 18 PreSimulation Fence Hardening
- [x] Routed the pre-simulation collision job into the dispatcher fence on every Simulation phase. DOD: `ScheduleSimulation()` now combines `_preSimulationHandle` into the returned dependency before cadence checks, Vault resolution exits, invalid-buffer exits, and zero-count exits. Rejected: scheduling from a void PreSimulation phase and only joining the handle when the slower authority cadence fired. Estimate: hot cost is one conditional combine only when a PreSimulation job exists; prevents unowned job completion and unsafe Vault release.
- [x] Rebind flushing now depends on dispatcher-owned handles. DOD: pending DataVault release waits for both tracked handles to report completion, and pending rebind reset preserves scheduled flags until the central Simulation phase can collect `_preSimulationHandle` even on cadence-skip frames.

## Loop 19 Teardown Drain Hardening
- [x] Replaced manual lifecycle recursion with `ShutdownRuntime(forceCompletePendingJobs: true)`. DOD: `Application.quitting` no longer invokes `OnDisable()` directly; both Unity disable and quitting route through one idempotent shutdown path. Rejected: manually calling Unity lifecycle methods, because it hides shutdown ordering and can double-execute partial release logic. Estimate: hot cost 0 us.
- [x] Added cold teardown fence drain before Vault release. DOD: `DrainScheduledJobsForTeardown()` uses `DispatcherJobFence.TryComplete(..., forceComplete: true)` only in shutdown/quitting, then `RequestDataVaultRebind(null)` releases the 16 Vault generation handles after handles are drained. Rejected: leaving deferred release pending after all dispatcher phases are unregistered, because that is memory-safe but can retain Vault references. Estimate: frame-loop cost 0 us; cold teardown may wait on at most the active pre-sim/sim SHINOBU jobs.

## Verification
- Compile: attempted through Unity batchmode, not `dotnet build`. Unity started script compilation, first Bee pass reported `Tundra requires additional run`, then the second `bee_backend` pass stopped writing `Docs/AgentLogs/Unity_SHINOBU_220_import.log` and held `Unity`/`bee_backend` processes for more than 25 minutes. Those processes were stopped. `Library/Bee/tundra.log.json` records foreign compile blockers in `AupPrecisionContracts.cs`, `HadalStructureForgeWindow.cs`, `HabitatDamageBakePipeline.cs`, `InteriorClutterForge.cs`, and `WreckageForgeWindow.cs`; targeted search found no `BulkheadContainment`, `BaseAirlock`, `PlayerKinematicsRuntime`, or `SHINOBU_220` compile-error row.
- Generated `.csproj` files still do not include the newly-created `BulkheadContainment*.cs` sources, so standalone `dotnet build` remains a false signal until Unity successfully regenerates project files.
- Unity Console: not available in this CLI session.
- Static checks passed after Loop 19 for runtime/core/shader files: no `Animator`, `MeshCollider`, `BoxCollider`, `Instantiate`, `new GameObject`, `.Complete()`, `Pack=`, unmanaged DTO properties, managed bulkhead publisher bridge, residual bulkhead slide naming, fake CPU timing labels, `GraphicsBuffer[]`, `SetData`, private persistent native collection fields, legacy `VaultBufferHandle` fields, direct Gameplay->Construction import, stale-active shader-global path in normal disable/release gates, empty-route visual upload before first active bulkhead, empty-route KCC collision job scheduling before first active bulkhead, unbounded KCC stale collision-row consumption, new `Time.frameCount` dependency in the bulkhead KCC gate, or implicit `Vector3 normal = plane.Normal` gizmo conversion. `BulkheadContainmentEditor.cs` intentionally retains `BoxCollider`, `MeshCollider`, and `Animator` string literals as scanner signatures for `DoorPhysicsInquisition`.
- `CONSTRUCTION_OPTIMIZATION_REPORT.json` parses through `ConvertFrom-Json`; `git diff --check` passes on owned touched files with CRLF warnings only.
- Build gate final checks: latest `typeperf "\Processor(_Total)\% Processor Time" -sc 1` reports CPU 100%, and `Get-Process dotnet,csc,bee_backend,Unity` returned no active rows. Rebuild was not launched because the CPU gate rejects it, the last Bee log already proves unrelated dependency blockers, and standalone dotnet remains a false signal until Unity regenerates project files.
- GCMonitor/profiler: measured runtime proof absent; microseconds above are engineering estimates, not profiler captures.
