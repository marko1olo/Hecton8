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
| P1 | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:195` | `GlobalPhysicsStateManager` | `NativeQueue<T>` | `int` | `MaxTrackedBodies` | Owner-local changed-index bridge; culling state uses `VaultBufferBinding` | dispose/unregister at `280-281` |
| P1 | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:206` | `GlobalPhysicsStateManager` | `NativeQueue<T>` | `PhysicsCullingTargetWakeRequestSignal` | `64` | Owner-local wake bridge; mirror buffer uses Vault binding | dispose/unregister at `286-287` |
| P2 | `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:185` | `GlobalPhysicsStateManager` | `NativeParallelMultiHashMap<TKey,TValue>` | `int,int` | `MaxTrackedBodies` | Owner-local derived spatial hash; authority buffers use Vault binding | dispose/unregister at `274-275` |

## Explicitly Named Legacy Outside AI/Physics Folder

| Priority | File:Line before migration | Owner | Collection | Element | Capacity | Fix |
|---|---|---|---|---|---|---|
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1437` | `VehicleMotor` | `NativeArray<T>` | `SubmarineState` | `1` | Migrated to `VaultBufferHandle<SubmarineState>` via `BufferID.VehicleMotorSubmarineStates`, capacity `32` |
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1628` | `VehicleMotor` | `NativeArray<T>` | `CapsulecastCommand` | `1` | Migrated to `VaultBufferHandle<CapsulecastCommand>` via `BufferID.VehicleMotorSweepCommands`, per-motor subarray |
| P0 | `Assets/_Project/Scripts/Gameplay/VehicleMotor.cs:1639` | `VehicleMotor` | `NativeArray<T>` | `RaycastHit` | `8` | Migrated to `VaultBufferHandle<RaycastHit>` via `BufferID.VehicleMotorSweepResults`, per-motor subarray |

## Static Post-Edit Scan

`rg -n "new Native(Array|List|HashMap|ParallelHashMap).*Allocator\\.Persistent|new NativeQueue.*Allocator\\.Persistent|new NativeParallelMultiHashMap.*Allocator\\.Persistent" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Physics Assets/_Project/Scripts/Gameplay/VehicleMotor.cs`

Result: three remaining matches, all in `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` (`_physicsSpatialHash`, `_physicsStateChangedIndices`, `_physicsTargetWakeRequests`).

`rg -n "Pack\\s*=\\s*1|StructLayout\\(LayoutKind\\.Sequential" Assets/_Project/Scripts/Gameplay/VehicleMotor.cs Assets/_Project/Scripts/Fauna/FaunaSimulationEngine.cs Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`

Result: no matches.
