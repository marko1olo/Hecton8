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
