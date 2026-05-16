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
