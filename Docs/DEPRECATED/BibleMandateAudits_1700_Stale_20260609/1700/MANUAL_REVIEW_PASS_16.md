# Manual Review Pass 16 - Physics, Vehicles, Buoyancy, Storm, And Thermodynamics

Status: STATIC METHOD REVIEW - NO UNITY / PROFILER / DEVICE PROOF
Date: 2026-06-02

## Scope

- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageRuntime.cs`
- `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs`
- `Assets/_Project/Scripts/Physics/TetherVerletJobs.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs`
- `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`
- `Assets/_Project/Scripts/Physics/RuntimePhysicsBaker1609.cs`
- `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs`
- `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs`
- `Assets/_Project/Scripts/Thermodynamics/ThermodynamicsHazardGridRuntime.cs`

## Static Findings

`VehicleComponentDamageRuntime` has a strong fixed/post-fixed owner shape for runtime damage. `FixedTick()` locks DataVault damage buffers, schedules mapping/reduction/evaluation/publish jobs, and `PostFixedTick()` only finalizes through `DispatcherJobFence.TryFinalizeCompleted(ref _damageHandle)`. The `Allocator.Temp` CSV grid staging at `TryLoadCsvLayout()` is inside `#if UNITY_EDITOR`, so that specific allocation is `LEGAL_EDITOR_GUARDED`, not a player runtime defect. The remaining release proof is still not free: damage lanes call `SignalBus<CombatDamageSignal>.EnsureInitialized()` / `SignalBus<VehicleHazardSignal>.EnsureInitialized()` and warm `GlobalTelemetryBus` during enable, so production boot must prove those lanes are prewarmed before combat damage arrives. The lifecycle force-complete path is acceptable only for disable/destroy/rebind windows, not same-frame gameplay.

`HarpoonTensionSolver328` and `TetherVerletJobs` separate editor validation from runtime solving better than the raw scan implied. `TetherMemorySovereigntyValidator1303` and its direct `Debug.Log*` calls are under `#if UNITY_EDITOR`, and `HarpoonTensionSolver328.WriteTelemetryDump(...)` is also editor-only. That downgrades those exact debug and Temp payload hits to `LEGAL_EDITOR_GUARDED`. Runtime tether acceptance still needs proof because the solver owns tension state, spline vertex output, physics event mirrors, telemetry rings, and fault flags. The static method read shows Burst jobs and fixed DTO writes, but not maximum tether count stress, same-frame completion cost, DataVault growth counters, or proof that force-event output never allocates or floods a downstream physics lane.

`AsyncBuoyancyReadbackRuntime` is structurally better than a synchronous water sampler. It preallocates triple GraphicsBuffers, persistent readback NativeArrays, queues requests through DataVault, dispatches compute in visual sync, consumes readbacks through `SystemDispatcher.IsAsyncReadbackReadyNoWait(...)`, and only calls `AsyncGPUReadback.WaitAllRequests()` in `CompletePendingReadbacksForRelease()`. That is a greenish shape, not closure. The first dispatch can still allocate persistent readback arrays via `EnsureReadbackData(...)`; `EnsureGpuBuffers()` creates GraphicsBuffers in `OnEnable()` / DataVault rebind; fallback/mock readback can become gameplay truth if compute is unavailable; and whole-buffer uploads/readbacks require GPU profiler proof on compact hardware.

`BuoyancyDisplacementRuntime` SIMD gizmo label allocation (`label.text = new string(...)`) is inside `#if UNITY_EDITOR` and runs in `OnDrawGizmos()`. It is `LEGAL_EDITOR_GUARDED`; it must not be counted as runtime UI/string allocation. The actual buoyancy runtime still remains tied to prior water/physics proof: fixed buffers, SIMD alignment, sleep-state transitions, and black-box output need 300-frame vehicle/water stress evidence.

Current `RuntimePhysicsBaker1609` source is not a visual LOD0 collider violation by itself and no longer exposes `Physics.BakeMesh(...)` or runtime `MeshCollider.sharedMesh` reassignment in the reviewed file. It references a serialized low-poly `collisionProxyMesh`, exposes request data, and `CommitBakedCollider(...)` only enables a target collider whose existing `sharedMesh` already equals that proxy mesh. There were no callsites found for `RuntimePhysicsBaker1609` or `CommitBakedCollider(...)` outside the file in this pass, so the component is currently `YELLOW_PREBOUND_OFFLINE_PROXY_COLLIDER_PROOF_REQUIRED`. Release closure requires serialized `COL_*` proxy assignment proof, no runtime collider cooking/reassignment route, and dispatcher telemetry proving the component cannot become a gameplay-cadence collider repair path.

`ShinobuStormPropagationRuntime` uses continuous `GlobalQualityWeight` cadence and job staging buffers, but it auto-creates a runtime GameObject at `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` if no instance is present. That route is bootstrap recovery, not release scene composition proof. The job staging arrays are persistent and fixed-length after `EnsureVaultBuffersCold()`, which is good, but release acceptance still needs boot proof that the authored runtime exists, fallback root creation does not execute in normal release scenes, job completion does not stall, and managed dump scratch is allocated only during cold setup. The current `TryWriteTelemetryDumpSnapshotCold(...)` only validates the scratch snapshot; it does not write to disk, so black-box dump policy for storm propagation needs a separate proof/fix if this is supposed to satisfy crash reporting.

`AbyssalThermodynamicsSolver` and its reactor bridge are owner-phased and DataVault-backed. The main solver schedules jobs in `Tick()` and finalizes in `LateFrameTick()` without forcing unless lifecycle shutdown requires it. Reactor thermal buffers and visual buffers are cold-created, and reactor visual upload uses double-buffered `GraphicsBuffer.LockBufferForWrite(...)`. This is not release proof. Reactor visual upload happens after solver completion, uses global shader vectors/buffers, and black-box dumps allocate `Allocator.Temp` payloads on fault. Closure requires compact/high reactor stress showing no DataVault/H8Memory growth, no same-frame job completion stalls, fixed GraphicsBuffer counts, bounded shader upload cost, and functional dump artifacts.

`ThermodynamicsHazardGridRuntime` has the highest remaining yellow in this pass. It calls `EnsureNativeState()` from `Awake()`, `OnEnable()`, and every `Tick()` until handles exist; if DataVault appears late, acquisition can occur on a gameplay tick. It creates/rebuilds a runtime `Texture3D` and calls `SetPixelData()` / `Apply(false, false)` in `UploadVisualTextureIfDirty()`, with a quality-scaled upload stride. That can be a valid visual fake for heat distortion, but it is not automatically compact-safe. It also generates emergency mock hazard constants/sources and writes Temp black-box payloads on NaN. Release closure requires boot prewarm, proof that emergency mock hazards are not production truth, texture rebuild/upload counters, GPU/CPU profiler captures, and fault-dump proof.

## Classification Updates

- `VehicleComponentDamageRuntime.TryLoadCsvLayout()`: `LEGAL_EDITOR_GUARDED`.
- `VehicleComponentDamageRuntime` runtime damage jobs: `GREENISH_FIXED_POST_FIXED_OWNER_PHASE_WITH_STRESS_PROOF_REQUIRED`.
- `HarpoonTensionSolver328.WriteTelemetryDump(...)`: `LEGAL_EDITOR_GUARDED`.
- `TetherMemorySovereigntyValidator1303`: `LEGAL_EDITOR_VALIDATOR`.
- `HarpoonTensionSolver328` runtime solver: `YELLOW_TETHER_SOLVER_STRESS_AND_DOWNSTREAM_FORCE_PROOF_REQUIRED`.
- `AsyncBuoyancyReadbackRuntime`: `GREENISH_TRIPLE_ASYNC_READBACK_WITH_BOOT_ALLOC_AND_GPU_PROOF_REQUIRED`.
- `BuoyancyDisplacementRuntime` SIMD labels: `LEGAL_EDITOR_GIZMO`.
- `RuntimePhysicsBaker1609`: `YELLOW_PREBOUND_OFFLINE_PROXY_COLLIDER_PROOF_REQUIRED`.
- `ShinobuStormPropagationRuntime`: `YELLOW_AUTO_BOOTSTRAP_AND_FAULT_DUMP_PROOF_REQUIRED`.
- `AbyssalThermodynamicsSolver` / reactor bridge: `GREENISH_OWNER_PHASE_THERMAL_SOLVER_WITH_VISUAL_UPLOAD_PROOF_REQUIRED`.
- `ThermodynamicsHazardGridRuntime`: `YELLOW_HEAT_TEXTURE_UPLOAD_AND_MOCK_TRUTH_PROOF_REQUIRED`.

## Required Closure

- 300-frame compact and high vehicle combat stress with damage, tether, buoyancy, reactor, storm, and heat hazard systems active; capture GC Alloc, NativeMemorySentinel growth, H8Memory growth, DataVault growth, job completion windows, and black-box fault latches.
- Boot proof that all signal, telemetry, readback, storm, buoyancy, vehicle, and thermodynamics buffers are prewarmed before gameplay interaction.
- GPU proof for buoyancy readback, thermodynamics heat `Texture3D.Apply`, reactor visual buffer upload, and shader global updates on MX350/compact and high/ultra lanes.
- Build/prefab proof that storm propagation, buoyancy, and thermodynamics owner components are authored in release scenes and do not rely on runtime root creation as normal composition.
- Collision proof that `RuntimePhysicsBaker1609` uses serialized low-poly `COL_*` proxies, that target colliders are prebound before play, and that no normal-frame collider cooking or runtime collider mesh reassignment exists; never allow LOD0 visual meshes as colliders.
- Fault proof that storm, reactor, tether, vehicle damage, and thermodynamics black-box dumps actually write expected artifacts under NaN/over-budget conditions and do not allocate during healthy frames.

## Non-Closure

This pass improves classification accuracy only. It does not close `RB-107`, `RB-122`, `RB-129`, or the new `RB-130`. No profiler, Unity import, player build, or hardware device run was executed.
