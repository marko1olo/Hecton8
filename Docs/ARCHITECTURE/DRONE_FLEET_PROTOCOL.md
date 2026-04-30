# DRONE FLEET PROTOCOL

Status: PENDING VERIFICATION

## Scope

Runtime owners:
- `DroneFleetManager.cs`: native drone state pool, Burst cognition scheduling, task arbitration, fleet snapshot publisher, OS overclock latch, suicide-weld latch, Logic-Leech hijack latch, indirect rendering submission.
- `DroneCognitionJob.cs`: Burst-compatible movement, battery drain, task scoring, atomic task claims, emergency scalar application, and boid separation.
- `RepairDroneHub.cs`: powered dock, logistics intake, integer drone-slot lease owner. It no longer spawns per-drone GameObjects for sorties.
- `RepairDroneEntity.cs`: retired source-name marker plus shared torch-audio event structs. It is not a `MonoBehaviour` and cannot be spawned as a drone body.
- `BaseLogisticsNetwork.cs`: two-phase storage reservation and nearest supply endpoint resolver.
- `ThreadSafeCommandQueue.cs`: main-thread structural command drain for `CommitStorageReservation`.
- `FloraInteractionManager.cs`: parasite target resolution and plasma-cut bridge into `DestructibleOrganicManager`.
- `HectonSubmarineOS.cs`: publishes emergency level snapshots consumed by the fleet.

Mandates followed:
- `AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `PHYS_Destructible_Organic_Entropy.txt`

## Headless Runtime

Drone sorties are represented by native slots. No per-drone GameObject or `MonoBehaviour` exists in the runtime sortie path:
- `NativeArray<HeadlessDroneState>[512]` front buffer
- `NativeArray<HeadlessDroneState>[512]` back buffer
- `NativeArray<float4x4>[512]` render matrix buffer
- `NativeParallelMultiHashMap<int, HeadlessDroneTask>[512]` hub-keyed task fanout
- `NativeParallelMultiHashMap<int,int>[512]` spatial hash for 2 m boid separation
- `NativeArray<int>[512]` task claim owners

Scheduling model:
1. `RepairDroneHub.SlowTick()` queues launch/abort/release requests into managed fixed-capacity arrays.
2. `DroneFleetManager.HeadlessFleetDriver.Tick()` schedules `DroneCognitionJob`.
3. `LateFrameTick()` completes the job in the dispatcher swap window, swaps front/back buffers, applies managed-side repair/storage/organic/voxel commits, then applies queued hub requests.
4. SRP render callback uploads `NativeArray<float4x4>` and calls `Graphics.RenderMeshIndirect`.

The job never reads and writes the same drone state buffer in one pass.

## Task Arbitration

`DroneCognitionJob` evaluates tasks stored in `NativeParallelMultiHashMap<int, HeadlessDroneTask>`.

Score:

```csharp
Score = (Criticality / max(distanceSq, 0.5625f)) * saturate(BatteryPercent * 0.01f);
```

Atomic claim:

```csharp
int priorOwner = Interlocked.CompareExchange(ref claimPtr[taskIndex], droneId, 0);
bool claimed = priorOwner == 0 || priorOwner == droneId;
```

Before scheduling, `DroneFleetManager.ClearHeadlessTaskClaims()` clears the claim-owner array and seeds it with active drones that already hold a valid `TargetTaskIndex`. New idle drones can only claim still-unowned task indices.

Emergency rule:
- when OS level is `Evacuate`, parasite tasks are skipped by the job.
- speed multiplier = `3x`.
- battery drain multiplier = `5x`.

Legacy hub assignment still exists as a compatibility front door for launch decisions, but active headless claims are included when rebuilding claim counts.

## Supply Cycle

Launch load:
1. hub resolves `Nanite_Solder`, then falls back to `Data_TitaniumScrap`.
2. hub checks accessible stock through `BaseLogisticsNetwork.CountAccessibleItem`.
3. hub queues a headless drone launch.
4. hub commits launch stock through `BaseLogisticsNetwork.TryReserveResources` and `CommitReserved`.

Field resupply:
1. a drone with `SolderUnits <= 0` switches to `ResupplyTravel`.
2. hub resolves the nearest connected `StorageCrate` or `Fabricator` through `BaseLogisticsNetwork.TryResolveNearestSupplyEndpoint`.
3. when docked, hub calls `TryAcquireDroneResupply`.
4. `BaseLogisticsNetwork.TryReserveResources` reserves one unit.
5. `BaseLogisticsNetwork.CommitReservedViaCommandQueue` registers touched crates and enqueues `EntityCommandType.CommitStorageReservation`.
6. `ThreadSafeCommandQueue.DrainMainThread` calls `StorageCrate.CommitReservation`.
7. no supply leaves the drone in `Stasis`.

## Parasite Defense

Parasite tasks are high-priority fleet tasks:
- source: `FloraInteractionManager.TryResolveNearestModuleParasite`
- criticality: `4 + infection*6 + airRisk*1.5`, plus cascade and emergency modifiers
- execution: `FloraInteractionManager.TryApplyDroneParasiteCut`
- organic damage channel: `DestructibleOrganicManager.TryApplyToolHit(... PlasmaCut)`

Direct native organic health writes are not used because `DestructibleOrganicManager` owns those lanes.

## Logic-Leech Hijack

External fauna code can call:

```csharp
DroneFleetManager.ReportLogicLeechContact(contactPosition, radiusMeters);
```

The nearest drone inside the radius flips to `HeadlessDroneFactionBit.Hostile`.
Hostile drones stop repairing and apply:
- `BaseModule.ApplyDamage`
- `HectonVoxelVolume.ApplyPlasmaCutDda`

Player damage is not wired here because no existing Logic-Leech/player damage contract exists in this task scope.

## Boid Separation

`DroneCognitionJob` samples neighboring drone indices through the native spatial hash:
- cell size: `2 m`
- sample area: 3x3x3 cells
- separation: inverse-square push
- alignment: average neighbor velocity, weight `0.25`
- cohesion: `0.8` open water, `0.1` tight voxel corridor
- player repulsion: 2.5 m radius, stronger than drone separation

Corridor state is sampled on the main thread with `VoxelDynamicNavGridRuntime.TrySampleHybridNavigation` before scheduling.

## Suicide Weld

Trigger:

```csharp
DroneFleetManager.RequestFleetSacrifice();
```

Eligibility:
- target is breached, or
- target is flooded and integrity is at or below 20% recoverable integrity

Effect:
- repair target to recoverable integrity
- clear flooded state through `ForceDrainComplete`
- mark the drone `Sacrificed`
- mark the native slot permanently destroyed
- increment fleet destroyed count

## Verification Boundaries

This document proves owner mapping and intended data flow only.

Not proven without Unity runtime logs:
- project compile-green state
- MCP console 0 errors
- GCMonitor 0 B/frame
- render shader support for `_DroneMatrices` or `_InstanceMatrices`
