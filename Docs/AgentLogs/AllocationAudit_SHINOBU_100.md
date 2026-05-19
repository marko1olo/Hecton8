# SHINOBU_100 Native Allocation Audit

Date: 2026-05-19
Evidence class: STATIC_SOURCE
Scope: `Assets/_Project/Scripts/AI`, `Assets/_Project/Scripts/Physics`, plus explicitly named `FaunaSimulationEngine` and `VehicleMotor`.

## AI/Physics Requested Types

Static scan found no direct `new NativeArray`, `new NativeList`, `new NativeHashMap`, or `new NativeParallelHashMap` using `Allocator.Persistent` under `Assets/_Project/Scripts/AI` or `Assets/_Project/Scripts/Physics`.

## AI/Physics Persistent Queues And Adjacent Collections

| Priority | File:Line | Owner | Collection | Element | Capacity | Current route | Teardown |
|---|---|---|---|---|---|---|---|
| RESOLVED | `Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs:224` | `AcousticEchoLocationRuntime` | `NativeQueue<T>` | `EchoTap` | `64` / `MaxQueuedEchoTaps` | Replaced by `BufferID.AcousticEchoPendingTaps` Vault buffer; frame taps/result/blackbox remain Vault handles | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1025` | `SubmarineDynamicsRuntime` | `NativeQueue<T>` | `MockFloodSignal` | `64` | Replaced by `SignalBus<MockFloodSignal>` lane | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1032` | `SubmarineDynamicsRuntime` | `NativeQueue<T>` | `MockImpactSignal` | `64` | Replaced by `SignalBus<MockImpactSignal>` lane | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs:1039` | `SubmarineDynamicsRuntime` | `NativeQueue<T>` | `CavitationAcousticSignal` | `64` | Replaced by `SignalBus<CavitationAcousticSignal>` lane | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` | `GlobalPhysicsStateManager` | `NativeQueue<T>` | `int` | `MaxTrackedBodies` | Replaced by `BufferID.ShinobuPhysicsCullingChangedIndices` plus 64B `BufferID.ShinobuPhysicsCullingChangedCount` atomic counter | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` | `GlobalPhysicsStateManager` | `NativeQueue<T>` | `PhysicsCullingTargetWakeRequestSignal` | `64` | Replaced by `BufferID.ShinobuPhysicsCullingWakeRequestMirror` plus 64B `BufferID.ShinobuPhysicsCullingWakeRequestCount` counter | no owner-local queue remains |
| RESOLVED | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` | `GlobalPhysicsStateManager` | `NativeParallelMultiHashMap<TKey,TValue>` | `int,int` | `MaxTrackedBodies` | Replaced by Vault SoA: `ShinobuPhysicsCullingSpatialBucketHeads`, `SpatialNext`, and `SpatialCellHashes` | no owner-local multi-hash map remains |

## Explicitly Named Legacy Outside AI/Physics Folder

| Priority | File:Line before migration | Owner | Collection | Element | Capacity | Fix |
|---|---|---|---|---|---|---|
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1437` | `VehicleMotor` | `NativeArray<T>` | `SubmarineState` | `1` | Migrated to `VaultBufferHandle<SubmarineState>` via `BufferID.VehicleMotorSubmarineStates`, capacity `32` |
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1628` | `VehicleMotor` | `NativeArray<T>` | `CapsulecastCommand` | `1` | Migrated to `VaultBufferHandle<CapsulecastCommand>` via `BufferID.VehicleMotorSweepCommands`, per-motor subarray |
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1639` | `VehicleMotor` | `NativeArray<T>` | `RaycastHit` | `8` | Migrated to `VaultBufferHandle<RaycastHit>` via `BufferID.VehicleMotorSweepResults`, per-motor subarray |

## Static Post-Edit Scan

`rg -n "new Native(Array|List|HashMap|ParallelHashMap).*Allocator\\.Persistent|new NativeQueue.*Allocator\\.Persistent|new NativeParallelMultiHashMap.*Allocator\\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`

Result after SHINOBU_100 Loop 4: no matches in `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`. The targeted culling bridge now resolves all persistent native storage through GlobalDataVault `VaultBufferBinding<T>`.

`rg -n "Pack\\s*=\\s*1|StructLayout\\(LayoutKind\\.Sequential" Assets/_Project/Scripts/Gameplay/VehicleMotor.cs Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`

Result: no matches.

## Loop 5 ABI / Dependency Post-Edit Scan

`rg -n "GlobalRegistry\\.(DataVault|Get|TryGet)" Assets/_Project/Scripts/Core/SystemDispatcher.cs Assets/_Project/Scripts/Core/Memory Assets/_Project/Scripts/GlobalPhysicsStateManager.cs Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs Assets/_Project/Scripts/Gameplay/VehicleMotor.cs Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs`

Result: no matches.

`rg -n "StructLayout\\(LayoutKind\\.Sequential|Pack\\s*=\\s*1" Assets/_Project/Scripts/Core/Memory/H8Memory.cs Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs Assets/_Project/Scripts/Core/SystemDispatcher.cs Assets/_Project/Scripts/GlobalPhysicsStateManager.cs Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`

Result: only generic handle/view exceptions remain:

- `GlobalDataVault.cs:195` `VaultBufferHandle<T>`: generic handle, 24B, size asserted; explicit layout rejected due CLR/IL2CPP generic explicit-layout risk.
- `GlobalDataVault.cs:297` `VaultBufferSlice<T>`: generic view, 32B, size asserted; explicit layout rejected due CLR/IL2CPP generic explicit-layout risk.

External non-SHINOBU ABI debt observed by broader scan and not edited:

- `Assets/_Project/Scripts/AI/Pathfinding/PathFunnelContracts.cs`: several `Pack=1` explicit structs under AI pathfinding ownership.
- `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs`: `Pack=1` visual instance and `CompileSynchronously = false` Burst jobs under Ambient Biota ownership.

SHINOBU BufferID collision check:

- `636-641`: no source collisions outside SHINOBU enum declarations.
- `70630-70635`: no source collisions outside SHINOBU enum declarations.
- Broad enum duplicate scan still reports non-SHINOBU collisions: `70200 SaveWorldPagerWriteArena/ConstructionBuilderOccupancy`, `70550 BabelSubtitleCueState/ShinobuLogisticsCsvScratch`, and `70800-70807 AudioStem*/ShinobuActiveEquipment*`.

## Loop 6 Physics Culling Scratch Post-Edit Scan

`rg -n "NativeQueue|NativeParallelMultiHashMap|Allocator\\.Persistent|private readonly byte\\[]|private readonly int3\\[]" Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`

Result: no matches. The only remaining managed scratch in the culling partial is `_physicsFrustumPlaneScratch = new Plane[6]`, a pre-existing Unity API interop array for `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])`, not persistent native or rollback-authoritative state.

New Vault scratch buffers:

- `BufferID.ShinobuPhysicsCullingCsvScratch = 70636`, `byte`, capacity `4096`.
- `BufferID.ShinobuPhysicsCullingLegacyRadiiScratch = 70637`, `byte`, capacity `64`.

Collision check:

- `70636-70637`: no source collisions outside SHINOBU enum declarations.

Build gate:

- Not run. CPU measured `18.0%`, but active `dotnet` processes were present, so AGENTS/user rules forbid `dotnet build`.
