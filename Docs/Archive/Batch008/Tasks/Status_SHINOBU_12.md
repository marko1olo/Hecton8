Date: 2026-05-17
Agent: SHINOBU_12
Role: VERLET_TOW_AND_CABLE_ARCHITECT
Domain: Tether & Cable Physics
Status: PENDING RUNTIME VERIFICATION - SHINOBU SCOPED STATIC BUILD PASS - GLOBAL WARNINGS REMAIN
Task Count: 20

Mandates read before coding:
- PHYS_Tether_Cable_Acceleration_Constraints.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- ARCH_Execution_Phases.txt
- REND_GPU_Sovereignty.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

Phase ownership:
- PRE_SIMULATION: DataVault/mock signal intake, AUP root resolve, hardware tier gate.
- SIMULATION: Burst Verlet integration, constraint relaxation, SDF node push-out, reeling, tension/snap state.
- POST_SIMULATION: telemetry ring write, dirty range build, snap signal DTO emission.
- VISUAL_SYNC: GraphicsBuffer upload and GPU spline presentation only.

## State Machine Checklist

### Loop 1 - Tasks 01-05
- [x] Task 01: BINARY_GRAVEYARD_RECONNAISSANCE | Justification: Docs/Archive tether rationale read; required binary folders/files absent; `GenerateEmergencyMockCables()` installed for deterministic fallback | Alternatives Rejected: blocking on absent StreamingAssets binaries | Estimate: 3 us cold init / 0 us hot path
- [x] Task 02: UNITY_JOINT_ERADICATION_PASS | Justification: local tow domain grep found no `LineRenderer` or Unity joint dependencies; new cable DTO/jobs avoid Unity component chains | Alternatives Rejected: editing out-of-domain bio/world cables | Estimate: 0 us added hot path
- [x] Task 03: CS1612_ENCAPSULATION_PURGE | Justification: `VerletNodeDTO` uses public fields and `VerletCableNodeBuffer.GetNodeRef(int)` returns unsafe `ref` into NativeArray memory | Alternatives Rejected: properties/indexer wrappers around node arrays | Estimate: <1 us saved per mutation-heavy solver pass
- [x] Task 04: ARM64_PADDING_RECONSTRUCTION | Justification: `VerletNodeDTO` = 32 bytes, `VerletConstraintDTO` = 16 bytes, `GpuCableSplinePointDTO` = 16 bytes; shader now consumes `float4` points | Alternatives Rejected: 12-byte `float3` GPU upload stride | Estimate: 1-4 us upload alignment gain per active tether
- [x] Task 05: BLIND_DEPENDENCY_MOCKING | Justification: local `MockSDFSampler`, `MockWorldSampler`, and `MockSubmarineAnchor` compile without world/submarine ownership | Alternatives Rejected: direct hard dependency on terrain SDF or submarine classes | Estimate: ~0.04 us/node point SDF
- [x] Compile after Loop 1 | Result: BLOCKED BY DEPENDENCY - after `dotnet restore`, `dotnet build Hecton8.Core.csproj --no-restore` stops on missing `DispatcherJobSwap` in `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`, unrelated to cable domain

### Loop 2 - Tasks 06-10
- [x] Task 06: VERLET_INTEGRATION_KERNEL | Justification: Burst integration now applies gravity plus per-node mock flow in local solver space; DTO job provides AoS node path | Alternatives Rejected: PhysX joints and schedule-then-complete fake parallelism | Estimate: 18-35 us / 1000 nodes
- [x] Task 07: ITERATIVE_CONSTRAINT_SOLVER | Justification: DTO relaxation solver mutates `VerletNodeDTO` by inv-mass and stiffness; active tow tier counts are 3/5/8/10 | Alternatives Rejected: 2-iteration rubber-band default and unbounded 20+ iteration truth chase | Estimate: 45-90 us / 1000 nodes depending tier
- [x] Task 08: THE_DEAR_LIE_SDF_COLLISION | Justification: active and DTO integration sample `MockWorldSampler` at discrete nodes and push out by normal; old-position friction damps basalt drag | Alternatives Rejected: swept sphere CCD per segment | Estimate: ~40 us / 1000 nodes
- [x] Task 09: DYNAMIC_WINCH_REELING | Justification: active rest lengths ramp toward winch target at bounded speed; DTO job consumes `MockWinchSignal` and collapses constraints below spool minimum | Alternatives Rejected: instant rest length teleport causing spring shock | Estimate: 2-6 us / 10 constraints
- [x] Task 10: AUP_PRECISION_OFFSET_MANAGER | Justification: active solver rebases nodes into anchor-local float space every step; DTO origin-shift job mirrors the same local shift contract | Alternatives Rejected: world-space float node math miles from origin | Estimate: 3-8 us / active cable rebase
- [x] Compile after Loop 2 | Result: BLOCKED BY DEPENDENCY - `dotnet build Hecton8.Core.csproj --no-restore` stops on missing `DispatcherJobSwap` in vehicle physics, not SHINOBU_12 files

### Loop 3 - Tasks 11-15
- [x] Task 11: YIELD_STRESS_AND_SNAPPING | Justification: DTO solver deforms/snap-deletes constraints; active path adds plastic creep on overstretched Verlet rest lengths and keeps existing snap SignalBus | Alternatives Rejected: instant elastic reset and fake infinite-strength cables | Estimate: 1-4 us / active cable
- [x] Task 12: MASS_TRANSFER_SYNERGY | Justification: active peak tension now writes `CableTensionForceDTO` into `BufferID.VerletCableTensionForces` for submarine-side readback | Alternatives Rejected: SignalBus-only tension handoff with no torque buffer | Estimate: <1 us / cable
- [x] Task 13: HARDWARE_TIER_ITERATION_THROTTLING | Justification: active tier policy is Low/MX350=3, Mid=5, High=8, Ultra=10 solver iterations | Alternatives Rejected: fixed 10 iterations on toaster devices | Estimate: saves 45-60 us / 1000 nodes on MX350 vs 10 iterations
- [x] Task 14: BATCH_RENDERER_SPLINE_LINK | Justification: 16-byte `GpuCableSplinePointDTO` upload is fed by `TetherVisualGpuSplineCopyJob`; shader consumes `StructuredBuffer<float4>` and no LineRenderer | Alternatives Rejected: CPU LineRenderer rebuilds and 12-byte float3 GPU stride | Estimate: 3-8 us upload/copy / cable
- [x] Task 15: CURRENT_ADVECTION_BENDING | Justification: active integration samples per-node flow through `MockWorldSampler.SampleFlowAcceleration`, with existing abyssal flow sources feeding the sampler | Alternatives Rejected: payload-only current fake as final cable motion | Estimate: 4-10 us / 1000 nodes
- [x] Compile after Loop 3 | Result: PASS - `dotnet build Hecton8.Core.csproj --no-restore` succeeded, 0 warnings, 0 errors

### Loop 4 - Tasks 16-20
- [x] Task 16: AABB_FRUSTUM_CULLING | Justification: active visuals compute bounds before upload and skip GraphicsBuffer upload/draw when manager frustum rejects them; DTO AABB job exists | Alternatives Rejected: uploading invisible cables every VISUAL_SYNC | Estimate: saves 3-8 us / culled cable
- [x] Task 17: TELEMETRY_BLACK_BOX_RECORDER | Justification: active 300-frame ring dumps to `Docs/AgentLogs/Dump_VERLET_CABLES.bin`; DTO `VerletBlackBoxWriteJob` mirrors max tension/error/active node hash | Alternatives Rejected: crash reports without last-state buffer | Estimate: 2-5 us / frame ring write
- [x] Task 18: CABLE_PHYSICS_DASHBOARD | Justification: added `Verlet Tow Tuner` EditorWindow backed by `BufferID.VerletCableTuning` for gravity, friction, iterations, stretch, break force | Alternatives Rejected: hardcoded-only cable tuning | Estimate: editor-only / 0 us player runtime
- [x] Task 19: CSV_OVERRIDE_INGESTOR | Justification: added span parser and editor monitor for `cable_materials.csv`, writing `CableMaterialDTO` into `BufferID.VerletCableMaterials` | Alternatives Rejected: managed row objects and runtime string-split parser | Estimate: editor-only file IO; parser 0 managed row allocs
- [x] Task 20: GIZMO_TENSION_VISUALIZER | Justification: tuner SceneView hook reads vault positions/tensions and draws green/yellow/red constraint lines | Alternatives Rejected: inspector-only numeric debug | Estimate: editor-only / 0 us player runtime
- [x] Compile after Loop 4 | Result: BLOCKED BY DEPENDENCY - latest `dotnet build Hecton8.Core.csproj --no-restore` stops on unrelated Construction drone missing DTOs (`PathWaypointDTO`, `MockSdfGrid`, `DroneFleetTuningConstants`, etc.); no SHINOBU_12 compiler errors surfaced before dependency wall

### Loop 5 - Self-Audit / Polish Gate
- [x] Strict reread pass 1 | Result: layout/forbidden-component grep passed for SHINOBU_12 additions; only pre-existing Pack/List hits remain outside new DTO ABI
- [x] Strict reread pass 2 | Result: CSV parser miss found and fixed; material key column now hashes by FNV-1a span reader
- [x] Strict reread pass 3 | Result: DataVault buffer IDs and active force/tuning/GPU slices checked against owner domain
- [x] Strict reread pass 4 | Result: visibility path checked; frustum rejection now skips upload and draw when render camera exists
- [x] Strict reread pass 5 | Result: `git diff --check` clean except line-ending warnings; build blocked only by unrelated domains
- [x] POLISH_MANDATE parsed after core tasks | Result: tag not present in `CURRENT_BATCH.md`; anti-bloat audit executed as Loop 5 instead

### Loop 6 - User Ultra-Think Polish Mandate
- [x] Prompt truth recovery | Result: attribute-aware CLI extraction of `<AGENT_PROMPT id="SHINOBU_12" ...>` succeeded; `TaskCount=20`; previous exact-tag regex failure was caused by `role/chat_name` attributes, not missing prompt
- [x] Project x-ray read | Result: `Docs/PROJECT_STATE_STATIC_XRAY.md` confirms project-wide runtime proof is still pending; no Unity Play Mode/profiler/GC/runtime performance claim is made for SHINOBU_12
- [x] Pack=1 eradication | Result: removed runtime `Pack = 1` from `TetherVerletTelemetryEntry` and `TetherManagerTelemetryEntry`; scoped grep now returns no `Pack = 1` in SHINOBU_12 files
- [x] Explicit padding upgrade | Result: added explicit tail padding to `VerletCableTuningDTO`, `MockSDFSampler`, and `CableSnappedSignal`; added 80-byte `MockWorldSampler` layout and full `VerletCableLayout.Validate()` size matrix
- [x] Zero-GC/hot-path scan | Result: scoped scan found no LINQ/foreach/ToString/new NativeArray/new List in SHINOBU_12 hot solver jobs; remaining `List<TetherInstance>` fields are pre-existing cold manager pools, and `GetComponent` hits are in pre-existing voxel hook outside Verlet Tick
- [x] Isolated compile-wall recheck | Result: `dotnet restore` plus isolated-obj `dotnet build Hecton8.Core.csproj --no-restore` failed on unrelated SaveSystem/Terminal/Fauna/Somatic/Core/VFX files; `Docs/AgentLogs/Build_SHINOBU_12_ultra_20260517.log` contains no Tether/Verlet/CableDTO/TetherInstance/TetherManager/TetherVerletJobs errors
- [x] Diff hygiene | Result: `git diff --check` clean for touched SHINOBU_12 paths except CRLF normalization warnings

### Loop 7 - H-Phi Handle Sovereignty Recheck
- [x] TetherInstance DataVault handles | Result: every SHINOBU_12 runtime cable buffer now has a paired `VaultBufferHandle<T>`; `NativeArray<T>` fields are resolved views refreshed through handles on `VaultGenerationID` changes, not ownership allocations | Alternatives Rejected: direct `GetBuffer<T>` aliases persisting across vault relocation | Estimate: <1 us steady-state generation guard; relocation refresh cost only when vault generation changes
- [x] TetherManager blackbox handles | Result: manager telemetry ring/head now resolve through `VaultBufferHandle<T>` and dump to `Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin` | Alternatives Rejected: manager-level direct `GetBuffer<T>` lifetime alias | Estimate: <1 us steady-state generation guard
- [x] Editor facade handle parity | Result: `VerletTowTunerWindow` now writes tuning/material buffers through `GetBufferHandle<T>` before resolving editor-only `NativeArray` views | Alternatives Rejected: leaving editor facade on raw `GetBuffer<T>` while runtime moved to handles | Estimate: editor-only / 0 us player runtime
- [x] Forbidden API re-scan | Result: scoped grep for `GetBuffer<`, `new NativeArray`, `Pack=1`, Unity joints, `LineRenderer`, and fake `Schedule().Complete()` returns no SHINOBU_12 runtime hits; only editor file IO remains in editor tooling | Alternatives Rejected: broad out-of-domain cleanup | Estimate: 0 us runtime
- [x] Isolated compile-wall recheck after handles | Result: `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false` fails on unrelated `VoxelDeltaProcessor` missing `IDataVault`/`VaultBufferHandle` imports; filter over build log finds no `Tether`, `Verlet`, `Cable`, `TetherInstance`, `TetherManager`, `TetherVerletJobs`, or `VerletTowTuner` errors

### Loop 8 - GPU Draw Payload / SRP Scalar Purge
- [x] Scalar MPB purge | Result: tether render path no longer calls `MaterialPropertyBlock.SetFloat`, `SetInt`, or `SetColor`; per-draw color, stress, radius, point count, indirect mode, tier, salt/silt, and clock are packed into `GpuCableDrawParamsDTO` | Alternatives Rejected: continuing per-draw scalar property mutation | Estimate: saves 8-12 managed/native property calls per visible tether draw
- [x] GPU draw params ABI | Result: `GpuCableDrawParamsDTO` is 80 bytes as five 16-byte `float4` lanes; `VerletCableLayout.Validate()` now asserts the stride | Alternatives Rejected: mixed scalar CBUFFER fields and ad hoc 4-byte shader uniforms | Estimate: stable Metal/ARM-friendly GPU fetch, no claimed runtime us without profiler
- [x] Shader binding audit | Result: `Hecton_TetherLineStrip.shader` reads `_TetherPositions` as `float4` and `_TetherDrawParams[0]` as one structured payload; no scalar UnityPerMaterial tether constants remain in the hot draw path | Alternatives Rejected: LineRenderer, per-material `SetFloat`, and 12-byte spline strides | Estimate: avoids scalar property churn; visual overkill remains shader-only
- [x] Static verification after Loop 8 | Result: `dotnet restore` plus isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 warnings and 0 errors | Alternatives Rejected: claiming Unity runtime validation from static build | Estimate: compile guard only
- [x] Forbidden API re-scan after Loop 8 | Result: scoped grep for scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, and fake `Schedule().Complete()` returns no SHINOBU_12 hits; `git diff --check` only reports CRLF normalization warnings | Alternatives Rejected: broad out-of-domain cleanup | Estimate: 0 us runtime

### Loop 9 - Bend Voxel Lookup Hot-Path Purge
- [x] Hot component lookup purge | Result: removed `TryGetComponent` / `GetComponentInParent<HectonVoxelVolume>` from `TetherInstance.TryResolveBendCorner`; bend resolution now uses cached bend volumes first and published voxel SDF raymarch second | Alternatives Rejected: repeated Unity component lookup from tether LOS/bend recalculation | Estimate: removes 1-2 component hierarchy lookups per blocked bend hit; no runtime us claim without profiler
- [x] Dear Lie preserved | Result: if no published voxel SDF is available, the existing tangent/normal bend fallback remains; no swept segment CCD or Unity joint path was introduced | Alternatives Rejected: segment CCD and per-frame hierarchy search | Estimate: stable low-tier fallback, visual clipping accepted
- [x] Forbidden API re-scan after Loop 9 | Result: scoped grep for `GetComponent`, `TryGetComponent`, `FindObject*`, scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, and fake `Schedule().Complete()` returns no SHINOBU_12 hits | Alternatives Rejected: out-of-domain voxel cleanup | Estimate: 0 us outside tether
- [x] Compile after Loop 9 | Result: BLOCKED BY DEPENDENCY - isolated `dotnet build Hecton8.Core.csproj --no-restore` now stops in `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs` on missing `WakeRequestSignal`; build log contains no SHINOBU_12/Tether/Verlet/Cable errors | Alternatives Rejected: editing global physics signal ownership from SHINOBU_12 | Estimate: compile guard only

### Loop 10 - SDF LOS / Unity Physics Raycast Purge
- [x] Unity Physics LOS purge | Result: removed the remaining tether LOS/anti-slice `Physics.RaycastNonAlloc` dependency; bend and cable-integrity obstruction queries now use `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` with point/normal/volume/stamp outputs | Alternatives Rejected: reintroducing PhysX raycasts or collider hierarchy ownership in the cable domain | Estimate: no profiler claim; removes synchronous PhysX query surface from bend checks
- [x] Dead field cleanup | Result: removed obsolete `_bendObstructionMask` storage and post-purge unused local variables from `ValidateCableIntegrity` | Alternatives Rejected: leaving warning debt after the SDF migration | Estimate: 0 us direct runtime, compile hygiene only
- [x] Forbidden API re-scan after Loop 10 | Result: scoped grep for Unity Physics raycasts, component lookups, `FindObject*`, scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, fake `Schedule().Complete()`, and gameplay `Update`/`FixedUpdate` returns no SHINOBU_12 hits | Alternatives Rejected: broad out-of-domain cleanup | Estimate: 0 us outside tether
- [x] Compile after Loop 10 | Result: PASS WITH GLOBAL WARNINGS - isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings, all outside SHINOBU_12 (`PhysicsWakeSignalContracts.cs` duplicate include and `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` unassigned fields). Build-log filter for Tether/Verlet/Cable symbols is empty | Alternatives Rejected: editing global physics warning ownership | Estimate: compile guard only

### Loop 11 - Pool Capacity / Gameplay Create Guard
- [x] 50-cable pool capacity audit | Result: replaced the 4-capacity active/pooled `List<TetherInstance>` registries with 64-capacity cold allocations, matching the SHINOBU 50-cable target with headroom | Alternatives Rejected: relying on `List<T>` resize during gameplay attach | Estimate: avoids resize allocation spikes when cable count exceeds 4
- [x] Cold pool prewarm | Result: `TetherManager.Awake()` now prewarms 64 inactive `TetherInstance` children; `RentInstance()` only consumes the pool and fails closed when empty | Alternatives Rejected: lazy `new GameObject` during attach/fire | Estimate: moves object creation out of gameplay attach path; no runtime us claim without profiler
- [x] Attach cap guard | Result: `AttachTowCable` checks `MaxManagedTetherInstances` before adding to active list and returns the instance to pool on overflow | Alternatives Rejected: silent growth past the capacity contract | Estimate: prevents hidden managed resize at the cap
- [x] Compile after Loop 11 | Result: PASS WITH GLOBAL WARNINGS - isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings outside SHINOBU_12. Build-log filter for Tether/Verlet/Cable symbols is empty | Alternatives Rejected: editing global physics warning ownership | Estimate: compile guard only

### Loop 12 - Mock Current Trig Purge / DTO Fail-Closed Guard
- [x] Mock current transcendental purge | Result: `MockWorldSampler.SampleFlowAcceleration` no longer calls `math.sin`; it uses a deterministic triangle-wave fake with the prior phase scale preserved | Alternatives Rejected: per-node transcendental current oscillation in fallback/mock cable solver | Estimate: static CPU risk reduction only; no profiler-backed us claim
- [x] DTO layout fail-closed guard | Result: `TetherManager.Awake()` now calls `VerletCableLayout.Validate()` before signal init, pool prewarm, tick registration, or telemetry allocation and disables itself on stride mismatch | Alternatives Rejected: relying on self-audit text while allowing invalid runtime DTO layout to proceed | Estimate: cold init branch / 0 us hot path
- [x] Forbidden scan after Loop 12 | Result: scoped grep finds no SHINOBU_12 hits for trig/exp/log in fallback cable mock, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, component lookup, Unity Physics raycasts, scalar material setters, LINQ, hot `foreach`, string formatting, or `StartCoroutine` | Alternatives Rejected: broad out-of-domain cleanup | Estimate: 0 us runtime
- [x] Compile after Loop 12 | Result: PASS WITH GLOBAL WARNINGS - isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings outside SHINOBU_12. Build log: `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop12_20260518.log` | Alternatives Rejected: editing global physics warning ownership | Estimate: compile guard only

### Loop 13 - CS1612 NativeArray Property Purge
- [x] NativeArray property purge | Result: removed `TetherInstance.VisualSegmentPositions` property and replaced it with `internal ref NativeArray<float3> GetVisualSegmentPositionsRef()` | Alternatives Rejected: returning NativeArray by property copy while claiming CS1612 eradication | Estimate: <1 us; correctness/cache mutation guard, not profiler-backed
- [x] Origin-shift ref mutation path | Result: `TetherManager` now binds `ref NativeArray<float3> visualPoints` for visual rebase fallback, preserving mutation of the vault-backed slice without property copying | Alternatives Rejected: extra copy-to-temp method or managed staging buffer | Estimate: 0 us hot allocation
- [x] CS1612 scan after Loop 13 | Result: scoped grep for `NativeArray<T>` expression-bodied/get properties returns no SHINOBU_12 hits | Alternatives Rejected: broader public API churn | Estimate: 0 us runtime
- [x] Compile after Loop 13 | Result: PASS WITH GLOBAL WARNINGS - isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/` succeeded with 0 errors and 9 warnings outside SHINOBU_12. Build log: `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop13_20260518.log` | Alternatives Rejected: editing global physics warning ownership | Estimate: compile guard only

### Loop 14 - Blackbox H8Dump / 50-Cable Vault Capacity
- [x] Prompt task counter correction | Result: attribute-aware prompt extraction plus `Task \d{2}:` counter returns `20`; the previous XML `<TASK>` counter was rejected as a bad evidence script | Alternatives Rejected: trusting a zero task count from the wrong regex | Estimate: 0 us runtime
- [x] Blackbox dump writer purge | Result: removed per-entry `BinaryWriter` serialization from SHINOBU_12 cable and manager dump paths; added `TetherBlackBoxDumpWriter` with `.h8dump` primary output, `.bin` legacy mirror, MMF on Editor/Standalone, and `FileStream.Write(ReadOnlySpan<byte>)` fallback | Alternatives Rejected: leaving synchronous `BinaryWriter` in the fatal path | Estimate: fault-path only; no runtime us claim
- [x] Compile include guard | Result: added `TetherBlackBoxDumpWriter.cs` to `Hecton8.Core.csproj` after build caught the missing generated-project include | Alternatives Rejected: hiding the helper inside unrelated job files | Estimate: compile guard only
- [x] 50-cable DataVault slot correction | Result: raised `DataVaultMaxTetherSlots` from 8 to 64 so the vault slices match the 50-cable assignment and the 64-instance pool; telemetry slab is documented as `64 * 300 * 64 = 1,228,800` bytes | Alternatives Rejected: pool capacity 64 with only 8 vault-backed cable slots | Estimate: prevents silent telemetry/force publication loss above 8 active cables
- [x] Compile after Loop 14 | Result: BLOCKED BY DEPENDENCY - latest isolated Core build stops on external `LocalizationManager`/`LocRegistry.BabelDictionaryStage` errors; filtered build output contains no `Tether`, `Verlet`, `Cable`, `TetherBlackBoxDumpWriter`, or SHINOBU_12 errors | Alternatives Rejected: editing localization/dispatcher ownership from cable agent | Estimate: compile guard only
