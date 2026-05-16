# LOG_SUBMARINE_BALLAST_PID_V2

## 2026-05-16 - HYDRO_MECHANIC - Dynamic CoM / Ballast PID

What was wrong:
- Submarine flood mass already existed, but the ballast PID path did not carry a double-precision AUP pivot through the Burst mass solve.
- Flood response produced scalar mass/drag behavior; high-end hardware had no heavier 6DOF drag tensor response.
- PID telemetry did not record PID error, flood inertia tensor, or system stress in the ballast black box dump.
- PID correction did not react to homeostasis stress and did not publish hull stress audio from heavy correction effort.
- Tail-heavy flooding had no bounded VFX signal at the engine vents.

What was done:
- Extended `SubmarineAutoLevelBallastController` with `SubmarineMassSolverJob`.
- Solver computes water mass as `WaterLevel * Volume * 1025.0f`, accumulates room mass in double AUP space, and outputs dynamic CoM, CoM offset, inertia tensor multiplier, total water mass, angular drag multiplier, and global pivot anchor.
- Added finite guards and `math.rcp(math.max(value, 0.01f))` on mass divisions.
- Preserved DataVault room buffers as authoritative SOA inputs, with existing H8Memory fallback only.
- Added dynamic Rigidbody inertia tensor application and restoration.
- Added high-tier flood linear/angular drag tensors through `SubmarineFluidDynamics.SetExternalFloodDragTensor`.
- Added `BubbleSpawnSignal` lane, publisher, bootstrap configuration, and explicit 80-byte payload.
- Added tail-heavy engine vent bubble signal at >20 degrees with cooldown.
- Added PID torque smoothing through `FastNlerp` before `PhysicsForceRouter.QueueTorque`.
- Added `SystemHealthIndexSignal` consumption; `SystemStress01 > 0.8` disables the D term and runs PI.
- Added `HullStressSignal` publication from PID error through `IAudioService.QueueHullStressSignal`, with procedural fallback.
- Added ballast PID telemetry fields and dump path `Docs/AgentLogs/Dump_SUBMARINE_BALLAST_PID_V2.bin`.

Cinematic cheats used:
- Flood mass uses room-volume lumped mass, not per-cell fluid simulation.
- Engine vent bubbles are discrete bounded `BubbleSpawnSignal` events, not continuous particle simulation.
- High-tier flood feel is drag/inertia tensor shaping, not real slosh CFD.
- Low tier keeps 1 Hz flood mass solve cadence and bypasses high-tier drag tensor shaping.

Microseconds saved, estimated not profiled:
- Duplicate controller avoided: 20 us/frame saved during active flood.
- DataVault SOA reuse instead of private room polling: 2 us/frame saved.
- Burst mass solve versus managed loop: 12 us/solve saved.
- Signal-driven flood recalculation versus idle polling: 8 us/frame saved while flood idle.
- Low-tier 1 Hz flood solve versus 60 Hz solve: 20-40 us/frame saved on i3/MX350 during active flooding.
- Stress PI fallback versus full PID D term: 1-2 us/solve saved when `SystemStress01 > 0.8`.
- Event-driven audio versus controller polling: 0 us/frame idle audio cost.

Validation:
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` was attempted.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` fails in unrelated files:
  - `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs(5,18)`: missing `Hecton8.AI.Perception`.
  - `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs(3,25)`: missing `Hecton8.Animation.Fauna`.
- No visible compiler error references the submarine ballast PID, fluid dynamics, global signal, or dynamic flood mass contract files.
- Final validation status: BLOCKED BY DEPENDENCY.

## 2026-05-16 - HYDRO_MECHANIC - Multiplatform / H-Phi Polish Pass

What was wrong:
- Ballast PID and flood mass structs still relied on sequential layout.
- Ballast PID state allocation still had a direct `H8Memory.Allocate` fallback after requesting DataVault buffers.
- Submarine fluid state allocation still had the same direct fallback path.
- Ballast PID signal consumers used NativeArray snapshot access instead of `ReadOnlySpan<T>` typed-lane reads.
- `BubbleSpawnSignal` existed as a lane but had no finite guard or non-critical VFX lane classification.

What was done:
- Converted ballast PID job output, telemetry entry, dynamic flood output, dynamic flood contract records, hydro kinematic packets, hydro black box, and splash event payloads to explicit fixed-size struct layouts.
- Removed direct persistent allocation fallback from `SubmarineAutoLevelBallastController.AllocateArray`.
- Removed direct persistent allocation fallback from `SubmarineFluidDynamics.AllocateNativeStateArray`.
- Added DataVault late rebind calls so ballast PID retries state binding after registry service replacement and slow tick refresh.
- Converted ballast PID flood and system-health signal consumers to `ReadOnlySpan<T>` from `SignalBus<T>.GetFrameSnapshot()`.
- Added `BubbleSpawnSignal` finite sanitization and non-critical VFX lane classification.

Cinematic cheats used:
- No new simulation layer added.
- Bubble signal remains a bounded VFX event, not a per-particle physics path.
- Flood-heavy feel remains tensor shaping, not CFD.

Microseconds saved, estimated not profiled:
- No new microsecond claim. This pass reduced crash surface, private ownership, and queue pressure rather than measured frame cost.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` was rerun.
- Current blocker is outside PHYSICS/VEHICLES:
  - `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs(6,21)`: missing `Hecton8.Input.Universal`.
  - `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs(122,47)`: missing `UniversalInputStateSignal`.
- No visible compiler error references the edited submarine ballast PID, submarine fluid dynamics, global signal, or dynamic flood contract files.
- Final validation status remains: BLOCKED BY DEPENDENCY.

## 2026-05-16 - HYDRO_MECHANIC - DataVault Handle / Fault I-O Polish Pass

What was wrong:
- Ballast PID no longer allocated fallback buffers, but it still kept persistent `NativeArray` fields as DataVault aliases.
- Fault telemetry wrote legacy autopilot, vehicle flood, and task dump files for the same crash/NaN event.
- Tail-heavy flood VFX emitted a semantic bubble marker but did not reuse the existing fluid impulse lane for high-tier silt/wake consumers.

What was done:
- Replaced persistent ballast PID `NativeArray` fields with `VaultBufferHandle<T>` fields for ballast fill, tank positions, PID output, flood mass output, telemetry, and room flood inputs.
- Added DataVault handle resolution helpers. Burst jobs receive transient NativeArray views only at schedule/write boundaries.
- Reset all vault handles when the DataVault service changes, preventing stale pointer identity after relocation or registry replacement.
- Collapsed black-box fault output to the required `Docs/AgentLogs/Dump_SUBMARINE_BALLAST_PID_V2.bin` file.
- Added high-tier-only `FluidImpulseSignal` publication from the tail-heavy engine vent event. Low math LOD skips the extra impulse.

Cinematic cheats used:
- Vent struggle remains event-driven: one bubble marker plus one fluid impulse, not continuous particle simulation.
- High-tier silt/wake is delegated to VFX consumers through a typed lane; physics only publishes bounded intent.
- Low tier keeps the same 1 Hz flood solve and skips extra VFX impulse work.

Microseconds saved, estimated not profiled:
- Two duplicate fault-time file writes removed for Steam Deck/MicroSD pressure.
- No runtime microsecond claim for handle resolution; it trades private ownership for DataVault correctness.
- Existing low-tier savings remain: 20-40 us/frame avoided by 1 Hz flood solve during active flooding.

Validation:
- `rg "private NativeArray" Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs` now returns no persistent NativeArray fields.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` was rerun.
- Current blocker is outside PHYSICS/VEHICLES:
  - `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs`: missing `_stateBuffer`, `_outputBuffer`, and `_blackBox`.
  - `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs`: `LockstepReplayBlockHeader` lacks `HashCadenceFrames`.
  - `Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs`: missing `ReleaseMotorArray` / `AllocateMotorArray`.
  - `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`: missing hardware metric fields/helpers and blackbox state.
  - `Assets/_Project/Scripts/Items/PickupItem.cs`: missing `ItemAcquiredSignal`.
  - `Assets/_Project/Scripts/Physics/TetherSignals.cs`: `TetherFiredSignal` is still on the old signal interface namespace.
- No visible compiler error references the edited submarine ballast PID, submarine fluid dynamics, or dynamic flood contract files.
- Final validation status remains: BLOCKED BY DEPENDENCY.

## 2026-05-16 - HYDRO_MECHANIC - Final H-Phi / Omega Build Pass

What was wrong:
- `SubmarineFluidDynamics` still held persistent `NativeArray<T>` fields as cached DataVault views.
- The local splash feedback path still carried legacy queue debt before this pass.
- A first pass that exposed vault buffers as properties was not acceptable because C# property indexer assignment on value types does not compile.
- Task-owned native payloads needed a final ARM64/Quest layout audit.

What was done:
- Replaced persistent submarine fluid `NativeArray<T>` fields with `VaultNativeBuffer<T>` wrappers around `VaultBufferHandle<T>` and the cached `IDataVault`.
- Kept hot compartment reads/writes on the cached vault pointer and resolved transient `NativeArray<T>` views only when scheduling Burst jobs.
- Removed the local submarine splash `NativeQueue`/legacy `FluidFeedbackEvents.PublishSplashQueued` path; splash water-entry feedback now publishes typed `FluidImpulseSignal`.
- Converted remaining task-owned fluid/PID payloads and dynamic flood contracts to explicit `Pack = 1` layouts where they cross native/binary or signal boundaries.
- Rechecked NaN guards around `math.rsqrt` and kept mass/division paths on `math.rcp(math.max(...))`.

Cinematic cheats used:
- Still no per-cell CFD and no per-particle bubble truth.
- Low tier keeps bounded lumped compartment mass and 1 Hz flood mass cadence.
- High tier gets heavier 6DOF drag/inertia feel plus typed VFX intent for silt/wake consumers.

Microseconds saved, estimated not profiled:
- No new runtime microsecond claim. This pass buys ownership correctness and compile stability.
- Two duplicate fault-time file writes remain removed from the earlier Steam Deck pass.
- Existing low-tier savings remain: 20-40 us/frame avoided by not running flood mass truth at 60 Hz during active flooding.

Validation:
- `rg` scans returned no hits for persistent `private NativeArray`, `NativeQueue`, local splash queue, `H8Memory.Allocate`, `H8Memory.Release`, `Update()`, `FixedUpdate()`, or `string.Format` in the submarine PID/fluid domain files.
- `rg` scan confirms task-owned submarine PID/fluid/contract explicit layouts use `Pack = 1`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` passed once after the domain fix with `0 Warning(s)` and `0 Error(s)`.
- Latest rerun now fails outside PHYSICS/VEHICLES in `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`: missing `NormalizeVectorOrFallback`, `IsFiniteBounds`, and `IsFiniteVector`.
- No visible compiler error references the edited submarine PID, submarine fluid dynamics, or dynamic flood contract files.
- Final validation status: BLOCKED BY EXTERNAL DEPENDENCY.

## 2026-05-16 - HYDRO_MECHANIC - Docking Automation / Final Build Pass

What was wrong:
- `DockingAutopilotService` held only a DataVault handle, but validated that handle through `ResolveBuffer(ref _activeSplineHandle)`.
- `GlobalDataVault.ResolveBuffer` hard-fails stale cached metadata after relocation, so the docking service still had a stale-pointer fault surface.
- Docking spline payloads used sequential `Pack = 1` layout; the stride was correct but not as auditable as explicit offsets for ARM64/Quest.

What was done:
- Converted `ActiveSplineData` and `DockingSplineSample` to explicit `Pack = 1` layouts with fixed offsets.
- Replaced direct `ResolveBuffer` validation with `TryGetBufferGeneration` and `TryGetBufferHandle`, then used the refreshed vault pointer.
- Added GlobalRegistry DataVault hot-swap handling to clear and reacquire the active spline handle on service replacement.
- Re-ran domain static scans and the core build.

Cinematic cheats used:
- No physical docking simulation was added.
- Docking remains cubic Bezier math with low-tier inertial smoothstep and high-tier zero-jerk Hermite when stress allows.
- Existing docking VFX/audio signal lanes remain reused; no duplicate docking signal invented.

Microseconds saved, estimated not profiled:
- No new microsecond claim.
- Rejected per-frame/private-array mirrors; the gain is relocation safety and deterministic native stride.

Validation:
- Static scans returned no hits for persistent private `NativeArray`, `NativeQueue`, `H8Memory.Allocate`, `H8Memory.Release`, `Update()`, `FixedUpdate()`, `string.Format`, stale docking `ResolveBuffer`, or direct docking `ResolvePointer` in PHYSICS/VEHICLES files.
- Struct-layout scan returned no non-`Pack = 1` task-owned layouts in the submarine/docking vehicle physics boundary.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- Unity Editor Play Mode, profiler, GCMonitor, Quest/Android IL2CPP, Metal, and Steam Deck hardware validation are not proven by this CLI build.
- Final validation status: DOTNET BUILD GREEN / UNITY RUNTIME PROFILING PENDING.

## 2026-05-16 - HYDRO_MECHANIC - DataVault Relocation / Hot-Swap Polish Pass

What was wrong:
- Fluid state had moved into `GlobalDataVault`, but scalar hot-path reads still used cached raw pointers inside the vault wrapper.
- A DataVault relocation signal could make those cached pointers stale before the next job-boundary resolve.
- Rebinding every wrapper to a fixed canonical BufferID would break the transfer/mass-properties ping-pong swaps already used by the fluid jobs.

What was done:
- Added typed `MemoryAddressShiftSignal` snapshot consumption in `SubmarineFluidDynamics.FixedTick`.
- Added in-place `VaultNativeBuffer<T>.Refresh` using `IDataVault.TryGetBufferHandle`, preserving whichever front/back buffer a wrapper currently owns after swaps.
- Added DataVault/PowerGrid GlobalRegistry hot-swap handling for the fluid owner, with DataVault replacement forcing teardown-safe reinitialization instead of stale handle reuse.
- Re-ran static scans for private `NativeArray`, `NativeQueue`, `H8Memory.Allocate`, `Update()`, `FixedUpdate()`, `string.Format`, and non-`Pack = 1` task-owned layouts.

Cinematic cheats used:
- No new physical simulation.
- Low tier still uses bounded compartment truth and existing cheap cadence.
- High tier keeps the same drag/inertia/VFX intent path; this pass prevents maintenance faults instead of spending more frame time.

Microseconds saved, estimated not profiled:
- No new microsecond claim.
- Rejected per-index handle resolving to avoid dictionary/generation validation inside the compartment loop.
- Existing low-tier savings remain: 20-40 us/frame avoided by not running flood mass truth at 60 Hz during active flooding.

Validation:
- Static scans returned no hits for persistent private `NativeArray`, `NativeQueue`, local splash queue, `H8Memory.Allocate`, `H8Memory.Release`, `Update()`, `FixedUpdate()`, or `string.Format` in the submarine PID/fluid domain files.
- Struct-layout scan returned no non-`Pack = 1` task-owned layouts in the submarine PID/fluid/domain contract files.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` was rerun.
- Current blocker is outside PHYSICS/VEHICLES:
  - `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18)`: missing `Hecton8.AI.Ecosystem`.
  - `Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs(4,18)`: missing `Hecton8.AI.Ecosystem`.
- No visible compiler error references the edited submarine PID, submarine fluid dynamics, or dynamic flood contract files.
- Final validation status: BLOCKED BY EXTERNAL DEPENDENCY.

## 2026-05-16 - HYDRO_MECHANIC - Burst Job Packing / Compile Wall Recheck
What was wrong:
- Two PID-owned Burst job containers lacked explicit packing declarations.
- A broad struct audit also surfaced private fluid Burst job containers. Those contain `NativeArray<T>` handles and are not binary payload structs.

What was done:
- Added `StructLayout(LayoutKind.Sequential, Pack = 1)` to `SubmarineAutoLevelPidJob` and `SubmarineMassSolverJob`.
- Kept task-owned payload structs on explicit fixed-size layouts.
- Rejected explicit offsets on `NativeArray<T>` job containers because that would hard-code Unity.Collections safety-handle internals.
- Removed one duplicate `ArchitectEyeVisualizer.ValidatePackedStructSizes` copy while triaging the full build; this was a compile-wall unblock, not a physics-domain feature change.

Cinematic cheats used:
- Low tier remains 1 Hz flood-mass truth plus visual interpolation.
- High/Ultra still spend saved physics work on 6DOF drag tensor response, bounded bubble events, and high-tier `FluidImpulseSignal` intent for silt/wake VFX.

Validation:
- Domain static scan returned no hits for missing `Pack = 1`, private `NativeArray`, `NativeQueue`, `H8Memory.Allocate/Release`, `Update()`, `FixedUpdate()`, `string.Format`, singleton pitch, or legacy EventBus.
- `git diff --check` reports only existing line-ending normalization warnings.
- Latest full `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` is blocked outside this domain with 17 errors in `PredatorCognitionDomain.cs` and `DroneFleetManager.cs`. No submarine PID/fluid/docking file appears in the error set.

Exact microseconds saved:
- 0 measured runtime microseconds claimed for the packing declarations.
- Compile-wall triage saved no runtime time; it only removed one external duplicate-symbol blocker before the current Fauna/Construction wall.

## 2026-05-16 - HYDRO_MECHANIC - Fluid Layout Closure / Tether Compile Wall

What was wrong:
- `CompartmentDefinition`, `BulkheadDefinition`, and `HydroKinematicDragJob` had no explicit `StructLayout` declaration.
- The structs were in the submarine fluid boundary, so the ARM64/Quest audit trail was incomplete even though payload structs were already explicit.

What was done:
- Added `StructLayout(LayoutKind.Sequential, Pack = 1)` to those three structs.
- Left Unity-serialized DTO field types unchanged to avoid corrupting inspector-authored compartment and bulkhead data.
- Re-ran domain debt scans and the core build.

Cinematic cheats used:
- No additional simulation was added.
- Low tier stays at bounded room-volume mass truth and 1 Hz PID flood solve.
- High/Ultra retain the existing 6DOF drag tensor, engine-vent bubble, and high-tier `FluidImpulseSignal` hooks.

Validation:
- Domain static scan returned no hits for missing `Pack = 1`, private `NativeArray`, `NativeQueue`, `H8Memory.Allocate/Release`, `Update()`, `FixedUpdate()`, `string.Format`, singleton pitch, or legacy EventBus.
- `git diff --check` reports only line-ending normalization warnings.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` fails outside PHYSICS/VEHICLES with 2 Tether errors: `TetherManager.cs(264,58)` missing `TetherSignals.TetherFireRequest`, and `Physics/TetherSignals.cs(167,82)` missing `TetherFireRequest`.
- No submarine PID/fluid/docking file appears in the error set.

Exact microseconds saved:
- 0 measured runtime microseconds claimed for layout attributes.
- The change removes implicit-layout audit debt only; runtime profiler evidence is still not available in this CLI session.

## 2026-05-16 - HYDRO_MECHANIC - Compartment State Vault Eviction / External UI-World Compile Wall

What was wrong:
- `_compartmentStates` was still a private managed `CompartmentState[]` mirror in `SubmarineFluidDynamics`.
- That state feeds gas partial pressure, flood volume snapshots, CoM resolution, and telemetry, so it was authoritative simulation state rather than harmless inspector authoring data.

What was done:
- Added `BufferID.SubmarineFluidCompartmentStates = 444` without renumbering existing Vault IDs.
- Replaced `_compartmentStates` with `VaultNativeBuffer<CompartmentState>`.
- Wired the new buffer through VehiclesPhysics Vault allocation, `MemoryAddressShiftSignal` relocation recognition, refresh, dispose, and clear paths.
- Preserved the existing explicit 64-byte `Pack = 1` `CompartmentState` layout.

Cinematic cheats used:
- No new simulation was added.
- Low tier remains bounded room-volume mass truth plus 1 Hz flood solve.
- High/Ultra retain 6DOF drag tensor response, engine-vent bubble signaling, and high-tier `FluidImpulseSignal` hooks.

Validation:
- Static domain scans returned no hits for missing `Pack = 1`, private `NativeArray`, `NativeQueue`, `H8Memory.Allocate/Release`, `Update()`, `FixedUpdate()`, `string.Format`, singleton pitch, or legacy EventBus.
- `git diff --check` reports only line-ending normalization warnings.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` fails outside PHYSICS/VEHICLES with 23 errors in `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs`.
- No submarine PID/fluid/docking file appears in the error set.

Exact microseconds saved:
- 0 measured runtime microseconds claimed.
- The change removes a private managed authority and stale-handle risk; runtime profiler evidence remains unavailable in this CLI session.
