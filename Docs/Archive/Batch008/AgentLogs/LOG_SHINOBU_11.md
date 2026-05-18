## 2026-05-17 SHINOBU_11 Submarine Dynamics Report

Agent: SHINOBU_11  
Domain: SUBMARINE_MASS_AND_BUOYANCY_SOLVER  
Status: Implemented. Latest current-disk Core build blocked by unrelated concurrent churn, not SHINOBU-owned files.

### What Was Wrong
- Existing submarine authority was PhysX-backed through legacy Rigidbody surfaces. That violates the SHINOBU prompt and DOD physics mandate.
- Existing flood-mass contracts used `Pack = 1`, which is hostile to ARM64 alignment.
- No SHINOBU-owned 6D Burst kinematic lane existed for mass, flood, cargo, ballast PID, cavitation, gyro stabilization, collision impulse, slosh, and black-box telemetry.
- OSHINO submarine mass/drag binaries were absent from Docs/Archive and StreamingAssets. Rationale logs had no `struct.pack` format for the requested submarine profiles.
- The generated Core project omitted an existing VFX contracts file needed by `HectonMarineSnowRenderer.cs`, causing a false compile blocker before SHINOBU code could be checked.

### What Was Done
- Added `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs`.
  - `SubmarineKinematicState`, `SubmarineKinematicControl`, `SubmarineMassProperties`, `SubmarinePidState`, `SubmarineForceAccumulator`, `SubmarineKinematicConfig`, and `SubmarineKinematicTelemetry`.
  - `MockFloodSignal`, `MockImpactSignal`, and `CavitationAcousticSignal` local unmanaged mock lanes.
  - `Submarine6DIntegratorJob` Burst `IJobParallelFor` for semi-implicit Euler 6D integration.
  - `MockFloodSignalSeederJob` to prove flood/mass-tensor handling in isolation.
- Added `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs`.
  - Dispatcher-registered fixed/post-fixed/late/slow bridge.
  - Vault buffer acquisition for state/control/PID/mass/force/telemetry/config/drag LUT.
  - NativeQueue mock lanes registered through `NativeMemorySentinel`.
  - Completion only in `PostFixedTick` through `DispatcherJobSwap.TryComplete`.
  - Cold legacy binary archaeology and fallback `GenerateEmergencyMockProfiles()`.
  - Stream-byte CSV override parser for `sub_physics_overrides.csv`.
  - 300-frame black-box dump to `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin` on fatal NaN flag.
- Added `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs`.
  - `Hecton8/Debug/Submarine Dyno-Tuner`.
  - Runtime slider writes for base mass, drag, PID P/I/D, gyro, thrust, and ballast.
  - SceneView vectors: red CoM, green CoB, blue thrust.
- Updated `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`.
  - Added submarine vault BufferIDs `SubmarineKinematicStates` through `SubmarineKinematicDragLut`.
- Updated `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`.
  - `MaxBufferId` corrected to current highest disk enum at report time.
- Updated `Assets/_Project/Scripts/Vehicles/Physics/Contracts/DynamicFloodMassContracts.cs`.
  - Removed runtime `Pack = 1`.
  - Reordered fields for 8-byte alignment and explicit Size contracts.
- Updated generated project files for verification.
  - `Hecton8.Core.csproj` includes SHINOBU runtime files and existing `VolumetricSiltContracts.cs`.
  - `Hecton8.Editor.csproj` includes `SubmarineDynoTunerWindow.cs`.

### Cinematic Cheats Used
- Hydrodynamic drag: `speedSq -> 16-sample 1D LUT -> force opposite velocity`.
- Buoyancy: single center-of-buoyancy offset plus cubic depth ease. No per-polygon displacement.
- Slosh: 1D harmonic oscillator moving flood CoM on roll velocity. No fluid particles.
- Cavitation: scalar depth/throttle/speed index that stutters thrust and emits a mock acoustic signal. No bubbles.
- Hardware tier: SystemHealth pressure toggles low-cadence PID/CoM solving, preserving frame budget over fidelity.

### Struct Layout Forensics
- `SubmarineKinematicState` Size 192:
  - 0 double3 AUP, 24 bytes.
  - 24 quaternion Rotation, 16 bytes.
  - 40 float3 LocalPosition, 12 bytes.
  - 52 float3 LinearVelocity, 12 bytes.
  - 64 float3 AngularVelocity, 12 bytes.
  - 76 float3 CenterOfMassLocal, 12 bytes.
  - 88 float3 CenterOfBuoyancyLocal, 12 bytes.
  - 100 float3 InertiaTensor, 12 bytes.
  - 112 floats TotalMassKg/BallastRatio01/GyroDisabledSeconds, 12 bytes.
  - 124 uint Flags, 128 uint TelemetryCursor, 132 uint EntityId, 136 uint ShiftFrameId.
  - 140 byte MathLod, 141 byte HardwareTier, 142 ushort pad, 144-191 explicit long padding.
- Other SHINOBU DTO sizes: Control 64, MassProperties 128, PidState 64, ForceAccumulator 128, Config 128, Telemetry 128, MockFlood 64, MockImpact 64, Cavitation 64.
- Static scan found no SHINOBU `StructLayout(... Pack = 1)`.

### H-Phi Check
- Authoritative arrays are requested from `GlobalDataVault` via `VaultBufferHandle<T>`.
- No private `NativeArray` fields were added.
- Local persistent native state is limited to mock/cavitation `NativeQueue<T>` lanes required by the prompt and registered with `NativeMemorySentinel`.
- Jobs are stateless kernels over vault views plus queue writers.

### Zero-GC Hot Path Check
- Static scan over SHINOBU-owned files found no Rigidbody, AddForce, local NativeArray fields, LINQ, foreach, `GetComponent`, `FindObjectOfType`, `FindObjectsOfType`, or `.ToString()`.
- FixedTick resolves vault views, drains typed signal snapshots, schedules jobs, and exits.
- PostFixed only completes already finished work in dispatcher swap window.
- CSV/file I/O is slow/cold path and cached by last-write ticks.

### AUP Check
- State stores `double3 Aup`.
- Hot math uses `ToLocal(aup - config.LocalOriginAup)` before casting to `float3`.
- The integrated local delta is committed back into `double3 Aup` after simulation job completion visibility.

### Blackbox
- `SubmarineKinematicTelemetry` is a 128-byte record.
- Ring length: `vehicleCapacity * 300`.
- Records AUP, linear/angular velocity, CoM, CoB, local position, flags, mass, ballast, cavitation, and state hash.
- Fatal NaN flag triggers binary dump to `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin`.

### Compile Guard
- Core build path:
  - Initial failures were generated intermediate state: missing editorconfig and `project.assets.json`.
  - After restore and project include correction, Core built once with 0 errors and 3 unrelated VFX warnings.
  - Latest current-disk rerun after concurrent churn fails in external files:
    - `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`: missing `LateFrameTick()` and `MockNarrativeTriggerSignal`.
    - `Assets/_Project/Scripts/PowerGridManager.cs`: missing `ShinobuLogisticsRouter`.
  - No SHINOBU-owned source file was named in the latest failure.
- Editor build path:
  - Fails before source compilation due missing generated metadata DLLs in `Temp/bin/Debug` for third-party/editor assemblies.

### Exact Microseconds Saved
- Measured savings: none. No profiler or Play Mode run was available.
- Estimated low-end savings:
  - Rigidbody/per-polygon displacement avoidance: 35-90 us per active submarine.
  - 1D LUT drag versus CPU hydro: 20-80 us per active submarine.
  - 1D slosh fake versus particle slosh: 5-30 us per active submarine.
  - Low-tier PID/CoM cadence drop: 2-6 us per fleet.
- These are estimates, not evidence. Runtime profiler proof remains required.

## 2026-05-17 Post-Compaction Reconciliation

### What Was Wrong
- Concurrent edits extended `BufferID` after the first SHINOBU report and left `VaultBufferContract.MaxBufferId` pointing below active vault IDs.
- Current disk truth shows `ShinobuInventoryDumpScratch = 70140` as the highest visible enum value.

### What Was Done
- Updated `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs` so `MaxBufferId = (int)BufferID.ShinobuInventoryDumpScratch`.
- Re-ran static scan over SHINOBU files: no `Pack = 1`, Rigidbody, AddForce, GetComponent/FindObject, foreach, LINQ, `.ToString()`, `new NativeArray`, or private `NativeArray` match.
- Re-ran Core build. It still fails outside SHINOBU with 86 errors in somatic, world sampler, binary manifest, seismic, and ecosystem domains. No SHINOBU-owned file was named.

### Current Compile Wall
- `VRSomaticRuntimeBootstrap.cs`: missing `SomaticKinematicsRuntime`.
- `GlobalWorldSampler.cs`: readonly mutation.
- `BinaryLayoutManifest.cs`: missing ambient/ecosystem DTOs.
- `HectonSeismicTideDirector.cs`: missing seismic jobs/signals/fields.
- `EcosystemRuntimeInstaller.cs`: missing `ShinobuEcosystemBalancer`.

## 2026-05-17 Ultra-Polish L1/NaN Pass

### What Was Wrong
- 8-byte alignment was not enough. `SubmarineKinematicState` was 160 bytes, `SubmarineMassProperties` was 96 bytes, and `SubmarinePidState` was 48 bytes. Those sizes are legal on ARM64 but bad cache-line strides for parallel NativeArray writes.
- NaN detection flagged fatal state but did not force a finite authority fallback before writing Vault and telemetry.
- Signal consumption mutated Vault arrays before the SHINOBU lane acquired its Vault locks.
- Math LOD could flip immediately from health pressure, violating the hysteresis mandate and risking VR cadence discomfort.

### What Was Done
- `SubmarineKinematicState` padded to 192 bytes, exactly 3 L1 cache lines.
- `SubmarineMassProperties` padded to 128 bytes, exactly 2 L1 cache lines.
- `SubmarinePidState` padded to 64 bytes, exactly 1 L1 cache line.
- `CavitationAcousticSignal` padded to 64 bytes.
- `DynamicFloodRoomMassSample` padded to 64 bytes and `DynamicFloodMassSolveResult` to 128 bytes; runtime `Pack=1` remains removed.
- Added safe finite/positive helpers, guarded quaternion normalization, guarded AUP double3 fallback, and finite authority fallback on fatal NaN.
- Moved Vault lock before signal mutation and changed integrator batch size to 4.
- Added `LowLodHoldSeconds` with a 2-second low-math-LOD hysteresis hold.
- Re-corrected `VaultBufferContract.MaxBufferId` to `BufferID.ShinobuInventoryDumpScratch` after another concurrent drift.

### Struct Layout Delta
- State: 0 double3 AUP, 24 quaternion, 40 local, 52 linear, 64 angular, 76 CoM, 88 CoB, 100 inertia, 112 scalar state, 124 flags/cursors/ids, 140 bytes, 141 bytes, 142 ushort pad, 144-191 explicit long padding.
- PID: 0-31 eight floats including `LowLodHoldSeconds`, 32 frame, 36 bytes/ushort flags/pad, 40 int pad, 48-63 long padding.
- Mass: 0 double3 pivot, 24/36/48/60/72 float3 centers, 84/88/92 masses, 96-127 long padding.

### Verification
- Static SHINOBU scan found no `Pack=1`, Rigidbody, AddForce, GetComponent/FindObject, foreach, LINQ, `.ToString()`, `new NativeArray`, or private `NativeArray`.
- `git diff --check` passed for touched SHINOBU files; only existing LF-to-CRLF warning remains on `DynamicFloodMassContracts.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` still fails outside SHINOBU. Latest blockers are `ShinobuEcosystemBalancer.cs`, `DroneFleetManager.cs`, `HectonIndirectVegetationRenderer.cs`, and `GlobalTelemetryBus.cs`. No SHINOBU-owned source file is named in `Docs/AgentLogs/Build_SHINOBU_11_latest.log`.

### Exact Microseconds Saved
- Measured savings: none. No profiler, Play Mode, or GCMonitor artifact was available.
- Estimated gain is now explicitly structural, not claimed: lower false-sharing risk, fewer job ranges, and less LOD thrash on weak hardware.

## 2026-05-17 Current Batch Prompt Reconciliation

### What Was Wrong
- One preflight extraction pass reported the SHINOBU_11 XML block missing from `Docs/Tasks/CURRENT_BATCH.md`.
- A fresh disk read now shows `<AGENT_PROMPT id="SHINOBU_11">` present again, so the previous note was a transient file-state observation, not durable assignment truth.

### What Was Done
- Re-extracted the full XML block.
- Verified exact task markers `Task 01:` through `Task 20:`.
- Updated status and rationale to remove the false implication that the prompt is currently absent.

### Exact Microseconds Saved
- 0 us runtime; documentation/audit correction only.

## 2026-05-17 Literal Mock Fluid Density Reconciliation

### What Was Wrong
- Task 05 named `MockFluidDensityGenerator`; code only had a constant default fluid density.
- Blackbox telemetry wrote a nonzero `EstimatedCostUs` placeholder, which was not measured and could be mistaken for profiler evidence.

### What Was Done
- Added `MockFluidDensityGenerator` in the SHINOBU contracts file.
- Wired the existing `FluidDensityChangedSignal` latest-state bridge as an optional density multiplier source without direct fluid-domain class coupling.
- Changed blackbox telemetry cost field to `0f` until profiler data exists.

### Cinematic Cheats Used
- Low tier: density = base seawater density plus clamped depth compression.
- Higher tiers: same gameplay truth plus a tiny deterministic micro-layer bias. No volumetric fluid solver.

### Exact Microseconds Saved
- Measured savings: none.
- Runtime impact is one scalar density sample per submarine per fixed tick; profiler proof remains absent.

## 2026-05-17 Current Session Forensic Re-Audit

### What Was Wrong
- The task was reissued under the ultra-polish mandate, so disk truth had to override chat memory.
- Static vehicle-physics scan still found two forbidden `Pack = 1` declarations in adjacent `DockingAutopilotService` runtime DTOs.
- Current batch has no `<POLISH_MANDATE>` tag; the user-supplied ultra mandate was used as the active polish/audit instruction after the 20-task checklist was already checked.
- Latest Core compile is blocked by unrelated `VoxelDeltaProcessor.cs` churn, not by SHINOBU submarine sources.

### What Was Done
- Re-read `Docs/Tasks/CURRENT_BATCH.md`, `Docs/Tasks/Status_SHINOBU_11.md`, `Docs/AgentLogs/Rationale_SHINOBU_11.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, the domain map, and eight selected mandates.
- Confirmed `<AGENT_PROMPT id="SHINOBU_11">` contains exactly `Task 01:` through `Task 20:`.
- Removed `Pack = 1` from `ActiveSplineData` and `DockingSplineSample`; explicit `FieldOffset` layout and `Size` are unchanged.
- Re-ran static scans over SHINOBU/vehicle physics scope: no remaining `Pack=1` in `Physics/Vehicles`, `Vehicles/Physics/Contracts`, or the Submarine Dyno-Tuner path.
- Re-ran `dotnet restore Hecton8.Core.csproj --ignore-failed-sources`: exit 0.
- Re-ran Core build: failed outside SHINOBU in `VoxelDeltaProcessor.cs` with missing `IDataVault`/`VaultBufferHandle<>` and duplicate `StructLayout` attributes.

### SELF_AUDIT
<SELF_AUDIT>
1. THE_20_TASK_CHECK: Tasks 01-20 remain [PASS] in `Docs/Tasks/Status_SHINOBU_11.md`; Task 05 now explicitly includes `MockFluidDensityGenerator`.
2. ARM64_CHECK: `SubmarineKinematicState` is 192 bytes: 0-23 `double3 Aup`, 24-39 `quaternion`, 40-111 six `float3` streams, 112-139 scalar/ids, 140 byte, 141 byte, 142-143 ushort pad, 144-191 explicit long padding. Adjacent docking DTOs no longer use `Pack=1`.
3. ZERO_GC_CHECK: SHINOBU `FixedTick`/Burst job paths contain no LINQ, `foreach`, `GetComponent`, `Find*`, `.ToString()`, or `new NativeArray`; file IO remains cold/slow/fatal-path only.
4. AUP_CHECK: AUP stays `double3`; hot math subtracts `LocalOriginAup` before casting to `float3`, then commits local delta back to `double3`.
5. DEAR_LIE_CHECK: Hydro truth is faked with 1D drag LUT, cubic depth/buoyancy ease, scalar density fallback, and 1D slosh oscillator. No per-polygon displacement or Navier-Stokes.
6. DEPENDENCY_CHECK: Runtime uses `GlobalRegistry`, `GlobalDataVault`, existing typed `SignalBus` snapshots, and local mock queues. No direct fluid/hull/audio domain class dependency was added.
</SELF_AUDIT>

### Struct Layout
- `SubmarineKinematicState`: 192 bytes, 3 cache lines.
- `SubmarineMassProperties`: 128 bytes, 2 cache lines.
- `SubmarinePidState`: 64 bytes, 1 cache line.
- `SubmarineKinematicTelemetry`: 128 bytes, 2 cache lines, 300-frame ring per vehicle.
- `DynamicFloodRoomMassSample`: 64 bytes.
- `DynamicFloodMassSolveResult`: 128 bytes.
- `ActiveSplineData`: explicit offsets preserved, size 144, no Pack=1.
- `DockingSplineSample`: explicit offsets preserved, size 56, no Pack=1.

### H-Phi Check
- Persistent submarine simulation arrays are Vault-owned through `BufferID.SubmarineKinematicStates` through `BufferID.SubmarineKinematicDragLut`.
- Local `NativeQueue` instances are mock signal lanes, prewarmed and registered with `NativeMemorySentinel`; they are not authority arrays.

### Blackbox
- `SubmarineKinematicTelemetry` writes a 300-frame circular ring per vehicle.
- Fatal NaN flag dumps to `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin`.
- `EstimatedCostUs` is now `0f` until profiler evidence exists.

### Compile Guard
- `Hecton8.Core.csproj` includes SHINOBU runtime files and editor project includes `SubmarineDynoTunerWindow.cs`.
- Latest build artifact: `Docs/AgentLogs/Build_SHINOBU_11_latest.log`.
- Current blocker: external `VoxelDeltaProcessor.cs`; no SHINOBU-owned source file named by compiler.

### Exact Microseconds Saved
- Measured savings: none. No Play Mode, Profiler, GCMonitor, or Unity Console artifact was available in this session.
- Estimated structural savings remain unmeasured: 35-90 us per active submarine versus PhysX/per-polygon authority, 20-80 us from 1D LUT drag, 5-30 us from 1D slosh, 2-6 us/fleet from low-tier solver dilation.

### Verification
- Static scan over SHINOBU-owned files found no `Pack=1`, Rigidbody, AddForce, GetComponent/FindObject, LINQ, `.ToString()`, `new NativeArray`, or private `NativeArray`.
- `git diff --check` passed for the SHINOBU files touched in this pass.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_000329.log` and failed outside SHINOBU in `VoxelDeltaProcessor.cs`.
- Filtered build-log scan found no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `DynamicFlood`, or `Physics/Vehicles` errors.

## 2026-05-17 Vault High-Water Reconciliation

### What Was Wrong
- Earlier SHINOBU notes referenced `ShinobuInventoryDumpScratch` as the shared `BufferID` high-water mark.
- Current disk truth has moved: `FloraGenomeCsvScratch = 70502` is now higher.

### What Was Done
- Re-read `H8Memory.cs`.
- Confirmed `VaultMemoryContracts.MaxBufferId` already points at `BufferID.FloraGenomeCsvScratch`.
- Made no code change; preserving current shared enum truth avoids breaking newer vault IDs.

### Exact Microseconds Saved
- 0 us runtime; audit correction only.

## 2026-05-18 Current-Disk Reissue Pass

### What Was Wrong
- The user reissued the mandate with conflicting Agent 25/Agent 11 text; the newest explicit directive was `SHINOBU_11`, so the submarine domain stayed authoritative.
- `SubmarineDynamicsRuntime.FixedTick()` still had a cold fallback path capable of resolving Vault buffers and reading `GlobalRegistry.DataVault` when `_buffersReady` was false.
- `VaultMemoryContracts.MaxBufferId` had regressed to `BufferID.VaultSharedTransformMatrices` while current `H8Memory.cs` contains higher legal ids, including `FloraGenomeCsvScratch = 70502` and SHINOBU submarine ids 587-594.
- Current Core build no longer blocks in voxel code; it now blocks in unrelated `GlobalPhysicsStateManager.cs` incomplete `Shinobu37` physics-culling work.

### What Was Done
- Re-read AGENTS, `Docs/Tasks/CURRENT_BATCH.md`, `Docs/Tasks/Status_SHINOBU_11.md`, `Docs/AgentLogs/Rationale_SHINOBU_11.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, the domain map, Unity MCP skill guidance, and task-relevant mandates.
- Changed `FixedTick()` to return when buffers are not ready; cold recovery remains in `OnEnable`/`SlowTick`.
- Restored `VaultMemoryContracts.MaxBufferId` to `BufferID.FloraGenomeCsvScratch`.
- Re-ran static scans over SHINOBU/vehicle-memory scope: no `Pack=1`, Rigidbody/AddForce, GetComponent/FindObject, LINQ, `.ToString()`, `new NativeArray`, private `NativeArray`, or `Schedule().Complete()` matches.
- Re-ran `dotnet restore Hecton8.Core.csproj --ignore-failed-sources`: exit 0.
- Re-ran Core build and wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_current.log`.

### Cinematic Cheats Used
- No new physics truth was added in this pass.
- Existing Dear Lie remains: 1D drag LUT, cubic depth/buoyancy ease, scalar density fallback, 1D slosh oscillator, cavitation scalar signal.

### SELF_AUDIT
<SELF_AUDIT>
1. THE_20_TASK_CHECK: Tasks 01-20 remain [PASS] in `Docs/Tasks/Status_SHINOBU_11.md`.
2. ARM64_CHECK: Primary DTO remains `SubmarineKinematicState` 192 bytes, 3 cache lines: 0-23 `double3 Aup`, 24-39 `quaternion`, 40-111 six `float3` streams, 112-139 scalar ids/state, 140-141 bytes, 142-143 ushort pad, 144-191 long padding.
3. ZERO_GC_CHECK: SHINOBU fixed-step no longer calls cold Vault acquisition. Static scan found no hot-path LINQ, foreach, Find/GetComponent, `.ToString()`, local `NativeArray`, or Schedule+Complete.
4. AUP_CHECK: Hot math subtracts `LocalOriginAup` from `double3 Aup`, casts only the delta to `float3`, and commits the local delta back to `double3`.
5. DEAR_LIE_CHECK: Physical hydrodynamics remain faked; no per-polygon displacement, Navier-Stokes, particle slosh, or Rigidbody authority.
6. DEPENDENCY_CHECK: SHINOBU_11 uses existing typed signals, local mock queues, Vault handles, and GlobalRegistry only in cold lifecycle/slow recovery, not in `FixedTick`.
</SELF_AUDIT>

### Struct Layout
- `SubmarineKinematicState`: 192 bytes, 3 cache lines, 8-byte aligned.
- `SubmarineMassProperties`: 128 bytes, 2 cache lines.
- `SubmarinePidState`: 64 bytes, 1 cache line.
- `SubmarineKinematicTelemetry`: 128 bytes, 300-frame ring per vehicle.
- `DynamicFloodRoomMassSample`: 64 bytes.
- `DynamicFloodMassSolveResult`: 128 bytes.

### H-Phi Check
- Persistent simulation arrays are Vault-owned through `BufferID.SubmarineKinematicStates` to `BufferID.SubmarineKinematicDragLut`.
- Local `NativeQueue` instances are bounded mock signal lanes, prewarmed and registered with `NativeMemorySentinel`; they are not authority arrays.

### Blackbox
- 300-frame telemetry ring remains active in `SubmarineKinematicTelemetry`.
- Fatal finite failure writes `Dump_SUB_KINEMATICS.bin`.
- No fake microsecond telemetry is written; `EstimatedCostUs = 0f` until profiler proof exists.

### Compile Guard
- `git diff --check` passed for touched SHINOBU/vehicle-memory files, with line-ending warnings on pre-existing vehicle files only.
- `Hecton8.Core.csproj` fails outside SHINOBU: `GlobalPhysicsStateManager.cs` references missing `WakeRequestSignal`.
- Follow-up scan shows the same external partial also references absent `QueuePhysicsWakeRequest`, `FlushPhysicsWakeRequests`, and `Shinobu37PhysicsCulling*` helpers. No SHINOBU-owned source was named.

### Exact Microseconds Saved
- Measured savings: none. No Unity Play Mode, Profiler, GCMonitor, or player-build artifact exists for this pass.
- Structural effect: removed cold dependency acquisition from a fixed-step branch; no numeric claim.

---

## 2026-05-18 Impact Corridor And AUP Polish

### What Was Wrong
- `DeferredSubmarineImpactSignal` provides local hit point plus relative speed. SHINOBU_11 consumed the point as a world normal and treated speed as impulse.
- The strongest impact magnitude could be mixed with the last weaker signal's normal.
- CSV hot-reload had no file-size cap and opened files without shared write access.
- The editor facade displayed absolute AUP as `Vector3`, which is the precision habit the runtime forbids.

### What Was Done
- Added `ForceFlagImpactNormalLocal`.
- Deferred impacts now derive local outward normal from `-LocalPoint`, convert speed to a bounded mass-scaled impulse, and transform local normal by the submarine rotation inside the Burst integrator.
- `ApplyImpactSignal` now keeps point/normal from the strongest signal in a frame.
- CSV override reads now cap at `4096` bytes and use shared read/write sequential access.
- Dyno-Tuner now displays `AUP Local Delta`, subtracting `LocalOriginAup` before float cast.
- Rationale stale nonzero microsecond wording was corrected to `0f until profiler proof`.

### Cinematic Cheats Used
- Impact response remains a scalar impulse lie, not contact manifold truth.
- Hydrodynamic drag remains speed-squared to 1D LUT.
- Slosh remains a 1D oscillator, not particles.

### Exact Microseconds Saved
- No measured profiler artifact exists. Claim is `PENDING VERIFICATION`.
- Steady-state impact change costs 0 us when no impact signal exists; impact-frame cost is unmeasured scalar math.

### Verification
- SHINOBU-owned static ban scan found no `Pack=1`, Rigidbody/AddForce, scene search, hot LINQ/foreach, `.ToString`, local NativeArray ownership, coroutine, Camera.main, or renderer material access.
- `git diff --check` was clean for the three touched SHINOBU files.
- First Core build after the patch caught one SHINOBU compile error: `math.min(byte, byte)` ambiguity. It was fixed by int casts.
- Retry Core build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_impact_retry.log`; it fails outside SHINOBU in `SubtitleManager.cs` and `GlobalPhysicsStateManager.cs`. Filtered retry log scan found no SHINOBU-owned source names.
- Unity import, Play Mode, profiler, GCMonitor, and player build were not run.

Status: PENDING VERIFICATION.

---

## 2026-05-18 Final Forensic Report

### What Was Wrong
- Initial SHINOBU lane had to be hardened beyond "new code compiles": legacy submarine DTOs still had `Pack=1`, legacy control could auto-install Rigidbody AutoLevel, mock flood could mutate production by default, and cavitation was trapped in a local queue.
- Editor verification is still blocked by editor assembly/reference issues outside runtime proof.

### What Was Done
- Added Burst/vault-backed 6D submarine kinematics in `SubmarineDynamicsContracts.cs` and `SubmarineDynamicsRuntime.cs`.
- Routed player command intent through existing `VehicleCommandSignalBus` into `SubmarineKinematicControl`.
- Removed `Pack=1` from submarine runtime DTOs and adjacent vehicle automation DTOs while preserving explicit sizes/offsets.
- Removed automatic `RequireComponent(typeof(Rigidbody))` from submarine scripts and gated legacy AutoLevel auto-install behind `enableLegacyPhysXAutoLevelInstall`.
- Added `Submarine Dyno-Tuner` editor facade for config and CoM/CoB/thrust visualization.
- Added 300-frame vault telemetry ring and `Dump_SUB_KINEMATICS.bin` fatal-state dump path.

### Cinematic Cheats Used
- Hydrodynamic drag is a 16-sample 1D LUT over `speedSq`.
- Buoyancy uses one center-of-buoyancy offset and eased depth error, not per-polygon displacement.
- Cavitation is a scalar depth/throttle/speed index bridged to `AcousticPingSignal`, not bubble simulation.
- Flood slosh is a 1D spring shifting flood CoM, not particle fluid.

### Compile Guard
- Prompt re-extracted with attribute-aware regex: `SHINOBU_11` starts at line 566 and contains exactly 20 tasks.
- Latest runtime build log: `Docs/AgentLogs/Build_SHINOBU_11_20260518_vehicle_command_bridge2.log` shows `Build succeeded`, 9 warnings, 0 errors.
- Remaining warnings are outside SHINOBU: duplicate `PhysicsWakeSignalContracts.cs` include and unassigned `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` fields.
- Editor facade build remains unverified due external editor assembly issues; no clean Play Mode/profiler artifact exists.

<SELF_AUDIT>
Task 01 [PASS] Binary archaeology and emergency mock profiles.
Task 02 [PASS] New authority is vault kinematics; default Rigidbody auto-require/auto-install removed or gated.
Task 03 [PASS] DTOs expose fields; `GetStateRef` uses direct vault pointer ref.
Task 04 [PASS] Runtime DTOs are 8-byte aligned; submarine `Pack=1` scan is clean.
Task 05 [PASS] Local mock flood/density/impact/cavitation lanes exist; random mock flood is opt-in.
Task 06 [PASS] Burst `Submarine6DIntegratorJob` integrates linear/angular 6D state.
Task 07 [PASS] CoM solve is O(1) from base/flood/cargo masses.
Task 08 [PASS] PID ballast state is vault-backed and aligned.
Task 09 [PASS] Dear-Lie hydro drag uses 1D LUT.
Task 10 [PASS] `double3` AUP subtracts local origin before `float3` hot math.
Task 11 [PASS] Cavitation stutter and acoustic bridge implemented.
Task 12 [PASS] Gyro self-righting with impact suppression implemented.
Task 13 [PASS] Hardware pressure drops PID/CoM cadence with hysteresis.
Task 14 [PASS] Inventory mass signal injects forward cargo mass.
Task 15 [PASS] Impact signals apply bounded linear/angular impulse.
Task 16 [PASS] Flood slosh is 1D harmonic CoM shift.
Task 17 [PASS] 300-frame blackbox ring and `.bin` dump path active.
Task 18 [PASS] `Submarine Dyno-Tuner` editor facade added; editor compile not verified.
Task 19 [PASS] CSV override parser is capped and shared-read slow path.
Task 20 [PASS] SceneView CoM/CoB/thrust visualizer added.

ARM64_CHECK:
`SubmarineKinematicState` size = 192 bytes, 192 % 8 = 0, 3 x 64B cache lines.
Layout: 0 `double3 Aup`; 24 `quaternion Rotation`; 40 `float3 LocalPosition`; 52 `float3 LinearVelocity`; 64 `float3 AngularVelocity`; 76 `float3 CenterOfMassLocal`; 88 `float3 CenterOfBuoyancyLocal`; 100 `float3 InertiaTensor`; 112 `float TotalMassKg`; 116 `float BallastRatio`; 120 `float GyroSuppressionSeconds`; 124 `uint Flags`; 128 `uint TelemetryCursor`; 132 `int EntityId`; 136 `uint FrameIndex`; 140 `byte MathLod`; 141 `byte HardwareTier`; 142 `ushort _pad16`; 144-191 six `long` pads.

ZERO_GC_CHECK:
Scoped SHINOBU ban scan found no hot-path `foreach`, LINQ, `.ToString()`, `new NativeArray`, private `NativeArray`, scene search, or immediate `Schedule().Complete()` in the owned runtime/editor/contract files. Fixed-step registry acquisition was removed.

AUP_CHECK:
Absolute position remains `double3 Aup`; hot physics uses `LocalOriginAup` subtraction before casting to `float3`; cavitation signals reconstruct AUP as `LocalOriginAup + LocalPosition`.

DEAR_LIE_CHECK:
The expensive physical truth faked successfully is water resistance/displacement: scalar CoB + LUT drag + 1D slosh, leaving visual overkill to consumers.

DEPENDENCY_CHECK:
Cross-domain contact uses `GlobalDataVault`, cold `GlobalRegistry`, existing `VehicleCommandSignalBus`, existing `AcousticPingSignal`, and local mock lanes. No direct sibling runtime dependency was added.

H_PHI_CHECK:
Authoritative arrays are Vault buffers. Local NativeQueues are bounded signal/mock lanes registered with `NativeMemorySentinel`; there are no private authoritative `NativeArray` stores in the SHINOBU runtime.

BLACKBOX_CHECK:
300-frame telemetry ring is active in vault-backed telemetry; fatal NaN path writes `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin`.
</SELF_AUDIT>

### Exact Microseconds Saved
- Measured savings: none. No profiler/Play Mode/GCMonitor artifact exists.
- Estimates remain qualitative only: removed default legacy PhysX AutoLevel scheduling for new SHINOBU submarines; replaced hydrodynamics with scalar/LUT math; removed production mock-flood job scheduling by default.

Status: PENDING VERIFICATION - Core runtime build passes; Unity import, Play Mode, profiler, GCMonitor, and VR comfort validation are not verified.

---

## 2026-05-18 Current Bottom Status After Ultra-Polish

- Latest SHINOBU code changes: `.h8dump` mirror added, `VehicleCommandSignal` stride fixed to 32 bytes, `SubmarinePhysicsBindingState` fixed to 40 bytes, and all `Pack=` attributes removed from `*Submarine*.cs`.
- Current scan: `rg -n "Pack\\s*=" Assets/_Project/Scripts -g "*Submarine*.cs"` returns no matches.
- Latest SHINOBU build attempt: `Docs/AgentLogs/Build_SHINOBU_11_20260518_h8dump_signal_stride_retry.log`.
- Build result: `Build FAILED` outside SHINOBU in `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs` with 59 CS0103 missing-field errors.
- Filtered build-log scan found no SHINOBU/submarine error names.
- Current blackbox contract: fatal NaN writes `Docs/AgentLogs/Dump_SHINOBU_11.h8dump` first, then attempts legacy `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin`.

Status: PENDING VERIFICATION - current Core compile is externally blocked; Unity import, Play Mode, profiler, GCMonitor, and VR comfort validation are not verified.

---

## 2026-05-18 H8Dump And Pack Policy Polish

### What Was Wrong
- Active SHINOBU memory files had been moved out of active `Docs/Tasks` and `Docs/AgentLogs` into `Docs/Archive/Batch008`; active anti-amnesia files were restored from archive before code work.
- The blackbox wrote only `Dump_SUB_KINEMATICS.bin`; current fatal-state doctrine requires `.h8dump`.
- `VehicleCommandSignal` used `Pack=4` and had no explicit 8-byte/multiple-of-8 proof.
- `*Submarine*.cs` still had `Pack=16`/`Pack=4` attributes in legacy atmosphere/structural/binding structs.

### What Was Done
- `SubmarineDynamicsRuntime` now writes `Docs/AgentLogs/Dump_SHINOBU_11.h8dump` and also attempts the legacy `Dump_SUB_KINEMATICS.bin` mirror.
- Fault-path dump I/O now catches `IOException` and `UnauthorizedAccessException` instead of throwing from `PostFixedTick` cleanup.
- `VehicleCommandSignal` is now `[StructLayout(LayoutKind.Sequential, Size = 32)]` with explicit padding.
- `SubmarineCoreDirector.SubmarinePhysicsBindingState` is now `[StructLayout(LayoutKind.Sequential, Size = 40)]` with explicit padding.
- Removed all remaining `Pack=` attributes from `*Submarine*.cs`; current `rg -n "Pack\\s*=" Assets/_Project/Scripts -g "*Submarine*.cs"` returns no matches.

### Compile Guard
- `git diff --check` for changed files reports only CRLF normalization warnings.
- `Docs/AgentLogs/Build_SHINOBU_11_20260518_h8dump_signal_stride_retry.log` fails outside SHINOBU in `UI/TerminalOS/TerminalOsRuntime.cs` with 59 CS0103 missing-field errors.
- Filtered build-log scan found no `SubmarineDynamics`, `SubmarineKinematic`, `VehicleCommandSignals`, `SubmarineCoreDirector`, `SubmarineStructuralGrid`, `SubmarineAtmosphereSystem`, `DynamicFlood`, `DockingAutopilot`, or `SHINOBU_11` errors.
- Current status supersedes older clean-build wording: `PENDING VERIFICATION`; current-disk compile is externally blocked.

<SELF_AUDIT>
TASK_MATRIX: Tasks 01-20 remain implemented; Task 04 and Task 17 were strengthened in this pass.
ARM64_CHECK: primary DTO remains `SubmarineKinematicState` size 192; command signal stride is now 32; submarine physics binding stride is now 40; no `Pack=` remains in `*Submarine*.cs`.
ZERO_GC_CHECK: no new hot-path allocation was added; new file I/O is fatal-path dump only.
AUP_CHECK: unchanged; physics subtracts `LocalOriginAup` before float math.
DEAR_LIE_CHECK: unchanged; scalar/LUT hydro, scalar cavitation, 1D slosh.
DEPENDENCY_CHECK: no new direct sibling runtime dependency; command/cavitation still use existing typed signal lanes.
BLACKBOX_CHECK: 300-frame ring now emits `.h8dump` first and `.bin` mirror second on fatal NaN.
</SELF_AUDIT>

### Exact Microseconds Saved
- Measured savings: none. No profiler artifact exists.
- Alignment work removes risk, not a measured timing claim.

Status: PENDING VERIFICATION - current Core build is blocked by external Terminal OS compile errors, not SHINOBU/submarine code; Unity import, Play Mode, profiler, GCMonitor, and VR comfort validation are still not verified.

---

## 2026-05-18 Legacy Submarine Alignment And Core Compile Proof

### What Was Wrong
- Wider submarine-domain scan found `Pack=1` in legacy submarine gameplay/fluid/structural DTOs.
- The new SHINOBU lane was aligned, but legacy submarine runtime code could still violate ARM64 layout rules.
- The old Rigidbody-based submarine context still exists and is consumed broadly through `ISubmarineRuntimeContext.HullRigidbody`; deleting it in one pass would create a cross-domain compile wall.

### What Was Done
- Removed `Pack=1` from `SubmarineAutoLevelBallastController` DTO/job structs.
- Removed `Pack=1` from `SubmarineFluidDynamics` DTO/job structs.
- Removed `Pack=1` from `SubmarineStructuralGrid` telemetry/impact structs.
- Rebuilt `SubmarineCoreDirector.SubmarineGridState` as an 8-byte sequential struct with manual padding.
- Preserved explicit `Size` and `FieldOffset` values for explicit-layout structs.
- Verified `rg -P "Pack\\s*=\\s*1(?!\\d)" Assets/_Project/Scripts -g "*Submarine*.cs"` returns no matches.

### Compile Guard
- Core build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_pack1_submarine_legacy.log`.
- Result: `Build succeeded`, 9 warnings, 0 errors.
- Remaining Core warnings: duplicate `PhysicsWakeSignalContracts.cs` source include and unassigned `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` fields.
- Editor facade build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_editor_facade.log` and failed before useful runtime proof. First blocker is `BlackboxXRayViewer.cs(110,51)`; generated editor csproj also lacks clean `Hecton8.Core.Contracts` resolution for several editor windows including `SubmarineDynoTunerWindow`.
- A temporary editor reference bridge was tested and reverted because it produced CS0433 duplicate `HectonPhysicsContract`.

### Cinematic Cheats Used
- No new physics cheat in this pass; this was memory layout hygiene.
- The authoritative SHINOBU lane still uses 1D drag LUT, scalar cavitation, and 1D slosh.

### Exact Microseconds Saved
- Measured savings: none.
- Expected benefit is removal of ARM64 unaligned-layout risk, not a claimed profiler timing.

Status: PENDING VERIFICATION - Core compile passes; Unity import, Play Mode, profiler, GCMonitor, and editor facade runtime are not verified.

---

## 2026-05-18 Vehicle Command Bridge And Legacy PhysX Gate

### What Was Wrong
- The SHINOBU Burst lane had a control DTO but did not consume the existing player vehicle command signal lane.
- `MountablePlayerTransport` auto-added the legacy `SubmarineAutoLevelBallastController`, keeping control coupled to the old Rigidbody path.
- Submarine components still auto-required Rigidbody through attributes even though the new authoritative path is kinematic.

### What Was Done
- `SubmarineDynamicsRuntime` now implements `IVehicleCommandSignalListener`.
- It registers with `VehicleCommandSignalBus`, flushes the command lane before consuming vault signals, and maps throttle/pitch/yaw/ballast into `SubmarineKinematicControl`.
- `MountablePlayerTransport` now publishes submarine commands for `SubmarineCoreDirector` without auto-adding legacy AutoLevel.
- `SubmarineCoreDirector` legacy AutoLevel auto-install is gated by `enableLegacyPhysXAutoLevelInstall`, default false.
- Removed automatic `RequireComponent(typeof(Rigidbody))` from `SubmarineCoreDirector`, `SubmarineAutoLevelBallastController`, and `SubmarineFluidDynamics`.
- `SubmarineCoreDirector.IsTransportPlatformActive` no longer requires a legacy Rigidbody.

### Compile Guard
- Core build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_vehicle_command_bridge2.log`.
- Result: `Build succeeded`, 9 warnings, 0 errors.
- Focused scan: no `RequireComponent(typeof(Rigidbody))` in submarine scripts; no `Pack=1` in `*Submarine*.cs`; SHINOBU-owned ban scan remains clean.

### Exact Microseconds Saved
- Measured savings: none.
- Expected avoided work: new SHINOBU submarines no longer auto-schedule legacy Rigidbody AutoLevel PID unless explicitly opted in.

Status: PENDING VERIFICATION - runtime/profiler proof still absent.

---

## 2026-05-18 Mock Signal And Cavitation Corridor Polish

### What Was Wrong
- `MockFloodSignalSeederJob` was scheduled every fixed tick. That makes isolated testing easy but can silently random-flood production gameplay.
- `CavitationAcousticSignal` was drained and discarded after the Burst integrator completed, so the existing audio/signal corridor never saw cavitation.

### What Was Done
- Added `enableMockSignals` inspector gate. The mock breach seeder is now opt-in; disabled means no random mock flood job is scheduled.
- Kept the Burst integrator stateless and vault-backed.
- Bridged post-fixed cavitation events into existing `AcousticPingSignal` on `ChannelMetalStress`.
- Reconstructed signal AUP from `SubmarineKinematicConfig.LocalOriginAup + CavitationAcousticSignal.LocalPosition`; no absolute AUP is cast directly to float.
- Re-ran the exact `SHINOBU_11` prompt extraction after discarding one malformed command: `Docs/Tasks/CURRENT_BATCH.md` lines 566-619, exactly 20 tasks.

### Cinematic Cheats Used
- Cavitation remains a scalar depth/throttle/speed lie, not bubble physics.
- The signal radius is a bounded intensity mapping, not acoustic propagation simulation.

### Compile Guard
- Static ban scan stayed clean for the checked SHINOBU files: no `Pack=1`, PhysX force API, scene search, LINQ/foreach, `.ToString`, local `NativeArray`, `File.OpenRead`, or immediate `Complete()` match.
- Core build wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_cavitation_bridge.log`.
- Current external blocker: `SaveBinaryStorage.cs(2423,65)` uses local variable `header` before declaration.
- Filtered cavitation-bridge log scan found no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, or `SHINOBU_11` errors.

### Exact Microseconds Saved
- Measured savings: none. Profiler/Play Mode proof is still absent.
- Fixed-step mock breach path now costs 0 us/job scheduling when `enableMockSignals` is false. This is a qualitative scheduling removal, not a measured timing claim.

Status: PENDING VERIFICATION.

---

## 2026-05-18 Cold I/O Pressure Polish

### What Was Wrong
- Legacy mass/drag binary archaeology used `File.OpenRead`.
- That path is cold boot/fallback, not fixed-step, but it can still fight external generators rewriting `.h8bin` files.

### What Was Done
- Replaced both boot-only binary profile reads with shared `FileStream` sequential scans.
- CSV override already used the same shared sequential pattern and remains capped at `4096` bytes.
- Re-ran code-only ban scan with `File.OpenRead` included; no SHINOBU matches.
- Re-ran Core build and wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_finalcheck2.log`.

### Compile Guard
- Current external blocker: `SubtitleManager.cs(737/741)` missing `SubtitleSignal` and `SignalBus`.
- Filtered finalcheck2 log scan found no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, or `SHINOBU_11` errors.

### Exact Microseconds Saved
- Measured savings: none.
- Fixed-step impact: 0 us; this is cold boot/slow-path I/O pressure hygiene.

Status: PENDING VERIFICATION.

---

## 2026-05-18 Post-Compaction Finalcheck

### What Was Wrong
- A verification command was malformed and briefly extracted `SHINOBU_01`; it was discarded immediately.
- Current Core build blocker has shifted again under concurrent work.

### What Was Done
- Re-extracted `SHINOBU_11` from `Docs/Tasks/CURRENT_BATCH.md`: lines 566-619, exactly 20 tasks.
- Verified `Hecton8.Core.csproj` includes `SubmarineDynamicsContracts.cs` and `SubmarineDynamicsRuntime.cs`.
- Verified `Hecton8.Editor.csproj` includes `SubmarineDynoTunerWindow.cs`.
- Re-ran SHINOBU-owned static ban scan: no `Pack=1`, Rigidbody/AddForce, scene search, hot LINQ/foreach, `.ToString`, local NativeArray ownership, coroutine, Camera.main, renderer material access, or Schedule+Complete matches.
- Re-ran Core build and wrote `Docs/AgentLogs/Build_SHINOBU_11_20260518_finalcheck.log`.

### Compile Guard
- Current external blocker: `LocRegistry.cs(404,55)` missing `ISignal`.
- Existing duplicate-source warning remains for `PhysicsWakeSignalContracts.cs`.
- Filtered finalcheck log scan found no `SubmarineDynamics`, `SubmarineKinematic`, `MockFluidDensity`, `Physics/Vehicles`, `DynamicFlood`, or `SHINOBU_11` errors.

### Exact Microseconds Saved
- Measured savings: none. No Unity Play Mode, Profiler, GCMonitor, or player-build artifact exists.
- Numeric claims remain estimates only; telemetry writes `EstimatedCostUs = 0f` until profiler proof exists.

Status: PENDING VERIFICATION.

---

## 2026-05-18 Bottom-Ordered Final Forensic Report

### Evidence
- `SHINOBU_11` prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md`: start line 566, exactly 20 tasks.
- Runtime Core proof: `Docs/AgentLogs/Build_SHINOBU_11_20260518_vehicle_command_bridge2.log` reports `Build succeeded`, 9 warnings, 0 errors.
- Scoped static scans: no `Pack=1` in `*Submarine*.cs`; no SHINOBU-owned hot-path matches for Rigidbody force APIs, scene search, LINQ/foreach, `.ToString`, private/native array ownership, `File.OpenRead`, or immediate `Schedule().Complete()`.
- Editor facade remains unverified: editor project is blocked by external editor/reference issues. Unity import, Play Mode, Profiler, GCMonitor, and VR comfort validation were not run.

### Final Changes
- Burst/vault 6D submarine solver: mass, flood, cargo CoM, ballast PID, gyro stabilization, impact impulse, cavitation, 1D slosh, and AUP-safe integration.
- Legacy submarine hygiene: automatic Rigidbody requirements removed; legacy AutoLevel auto-install is opt-in through `enableLegacyPhysXAutoLevelInstall`.
- Signal corridor: player vehicle commands flow through `VehicleCommandSignalBus`; cavitation bridges into existing `AcousticPingSignal`; mock flood is opt-in.
- Human bridge: `Submarine Dyno-Tuner` editor window and capped `sub_physics_overrides.csv` slow-path override.
- Blackbox: 300-frame telemetry ring and `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin` fatal dump path.

<SELF_AUDIT>
TASK_MATRIX: 01 PASS archaeology/mock profiles; 02 PASS Rigidbody authority removed from SHINOBU lane/default auto-install; 03 PASS direct DTO fields/ref access; 04 PASS ARM64 layout; 05 PASS mock flood/density/impact/cavitation; 06 PASS Burst 6D integrator; 07 PASS O(1) CoM; 08 PASS PID ballast; 09 PASS 1D LUT drag; 10 PASS AUP local-shift; 11 PASS cavitation feedback; 12 PASS gyro stabilization; 13 PASS hardware tick dilation; 14 PASS cargo mass injection; 15 PASS collision impulse; 16 PASS slosh oscillator; 17 PASS 300-frame blackbox; 18 PASS editor facade added but editor compile unverified; 19 PASS CSV override; 20 PASS CoM/CoB/thrust gizmos.
ARM64_CHECK: `SubmarineKinematicState` is 192 bytes, multiple of 8, exactly 3 cache lines. Offsets: 0 AUP double3; 24 rotation; 40 local pos; 52 linear velocity; 64 angular velocity; 76 CoM; 88 CoB; 100 inertia; 112 total mass; 116 ballast; 120 gyro suppression; 124 flags; 128 telemetry cursor; 132 entity; 136 frame; 140 math LOD; 141 hardware tier; 142 ushort pad; 144-191 six long pads.
ZERO_GC_CHECK: SHINOBU fixed tick has no LINQ/foreach/string formatting/boxing pattern in scoped scan; registry/vault acquisition is cold path, not fixed-step recovery.
AUP_CHECK: state keeps `double3 Aup`; physics subtracts `LocalOriginAup` before `float3`; acoustic AUP is reconstructed from local origin plus local position.
DEAR_LIE_CHECK: water resistance/displacement are faked with scalar CoB, 1D LUT drag, scalar cavitation, and 1D slosh instead of CPU fluid simulation.
DEPENDENCY_CHECK: cross-domain contact uses GlobalDataVault, cold GlobalRegistry, existing VehicleCommandSignalBus, existing AcousticPingSignal, and bounded local mock queues. No direct sibling runtime coupling was added.
H_PHI_CHECK: authoritative arrays are vault buffers; local NativeQueues are signal/mock lanes only.
BLACKBOX_CHECK: 300-frame telemetry ring is active; fatal NaN dump path is `Docs/AgentLogs/Dump_SUB_KINEMATICS.bin`.
</SELF_AUDIT>

### Exact Microseconds Saved
- Measured savings: none. No profiler artifact exists.
- Expected wins are architectural only: no default PhysX AutoLevel scheduling for new SHINOBU submarines; scalar/LUT hydro instead of per-polygon or fluid particles; mock flood job skipped by default.

Status: PENDING VERIFICATION - Core runtime build passes; Unity import, Play Mode, profiler, GCMonitor, and VR comfort validation are not verified.

---

## 2026-05-18 Actual Current Bottom Status

- After the older final report above, another polish pass changed code.
- Current code state: `.h8dump` mirror added, `VehicleCommandSignal` stride fixed to 32 bytes, `SubmarinePhysicsBindingState` fixed to 40 bytes, and all `Pack=` attributes removed from `*Submarine*.cs`.
- Current scan: no `Pack=` matches in `Assets/_Project/Scripts/*Submarine*.cs`.
- Current build log: `Docs/AgentLogs/Build_SHINOBU_11_20260518_h8dump_signal_stride_retry.log`.
- Current build result: failed outside SHINOBU in `UI/TerminalOS/TerminalOsRuntime.cs` with 59 CS0103 missing-field errors.
- Filtered build-log scan found no SHINOBU/submarine error names.

Status: PENDING VERIFICATION - current Core compile is externally blocked; runtime/VR/profiler verification still absent.

---

## 2026-05-18 CSV/Vault Race Polish

### What Was Wrong
- The cold designer CSV override path could resolve and write `SubmarineKinematicControls` and `SubmarineKinematicConfig` from `SlowTick` without checking whether the Burst integrator job still owned those Vault buffers.
- The fatal blackbox dump path could throw before writing if `Docs/AgentLogs` directory creation failed.

### What Was Done
- `SlowTick` now skips Vault buffer re-ensure while `_integratorPending` or `_buffersLocked`.
- `TryApplyCsvOverrides()` now refuses to run during pending integration, locks `SubmarineKinematicControls` then `SubmarineKinematicConfig`, writes under those locks, unlocks in `finally`, and catches CSV I/O/access failures.
- `DumpBlackBoxIfFaulted()` now catches directory creation I/O/access failures before attempting `.h8dump` and legacy `.bin` writes.
- Static SHINOBU-owned scans found no `Pack=`, local `NativeArray`, Rigidbody/PhysX force API, scene-search, LINQ/foreach, `.ToString`, `File.OpenRead`, `File.ReadAllBytes`, material mutation, or `Camera.main` matches.

### Cinematic Cheats
- No new truth simulation was added. The authoritative solver still uses scalar CoB, 1D LUT drag, scalar cavitation, and 1D slosh. Saved complexity remains reserved for visual/audio consumers of cavitation and telemetry.

### Compile Guard
- Current Core build log: `Docs/AgentLogs/Build_SHINOBU_11_20260518_csv_lock_retry.log`.
- Current result: failed outside SHINOBU in `WorldChunkResidencyManager.cs(4064,17)` missing `RefreshAsyncPersistenceService`.
- Filtered build-log scan found no SHINOBU/submarine error names.

### Exact Microseconds Saved
- Measured savings: none. Fixed-step impact of this pass is 0 us; it changes slow-path CSV/fatal-path dump safety only.

Status: PENDING VERIFICATION - current Core compile is externally blocked; Unity import, Play Mode, Profiler, GCMonitor, player build, and VR comfort proof remain absent.
