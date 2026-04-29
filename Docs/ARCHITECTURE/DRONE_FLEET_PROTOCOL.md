# DRONE FLEET PROTOCOL

## Scope

Runtime owners:
- `DroneFleetManager.cs`: central repair-task dispatcher, fleet snapshot publisher, emergency-overclock state, swarm avoidance input.
- `RepairDroneHub.cs`: powered dock, logistics intake, pooled launch owner.
- `RepairDroneEntity.cs`: mission execution, battery drain, route following, additive weld dispatch.
- `HectonSubmarineOS.cs`: diegetic consumer of fleet snapshot telemetry.

Relevant mandates followed:
- `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`
- `AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Fleet Arbitration

The fleet does not let every hub free-run a local target scan anymore.

`DroneFleetManager` builds a bounded repair-task max-heap each dispatch window:
- candidate source: `ConstructionManager.SpawnedModules`
- eligibility gates:
- same `PowerGrid` as the requesting hub
- damaged below dispatch threshold, or flooded, or cascade-failed, or graph-ruptured
- active-claim cap not exceeded

Claim caps:
- `NativeArray<int>` stores live claim counts by current module index.
- max simultaneous claims per target: `2`
- active counts are rebuilt from the currently spawned drone registry before each assignment pass

## Assignment Score

Distance term:

```csharp
float clampedDistance = Mathf.Max(0.75f, distanceMeters);
```

Criticality term:

```csharp
weight = 1f + ((1f - integrity01) * 4f);
if (module.IsFlooded) weight += 2f;
if (module.IsBreached) weight += 3f;
if (module.HasCascadeFailure) weight += 1.5f;
if (BaseDegradationSystem.IsModuleRuptured(module)) weight += 2.5f;
weight += (1f - module.AirReserveNormalized) * 1.5f;
if (EmergencyLevel == Evacuate) weight *= 1.35f;
```

Final score:

```csharp
Score = (1.0f / clampedDistance) * criticalityWeight;
```

Meaning:
- closer tasks rise naturally
- flooded, breached, cascade-failed, and graph-ruptured modules jump ahead
- stale-air compartments escalate even if raw integrity is not yet zero
- `EMERGENCY_LEVEL_3` in Hecton-OS biases the entire fleet harder toward life-critical repair work

## Supply Cycle

Repair work is no longer free.

Hub launch flow:
1. resolve repair supply item
2. count accessible stock on the local `PowerGrid` through `BaseLogisticsNetwork`
3. reserve units through the two-phase logistics reservation path
4. commit the reservation only after the drone is actually spawned

Current fallback:
- requested ID: `Nanite_Solder`
- fallback ID: `Data_TitaniumScrap`

Consumption model:
- `1%` integrity repaired = `0.1` solder units
- operationally: `1` discrete crate item covers `10%` restored integrity
- the drone carries a mission load and decrements that load as restored integrity accumulates
- no supply: dock slots report `STASIS`

## Navigation And Swarm Motion

Travel:
- route requests use `VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc`
- if no macro route exists, the drone falls back to direct movement

Swarm steering:
- base vector: route or target direction
- additive avoidance:
- other active repair drones
- player runtime transform from `GlobalRegistry.Player`

Corridor policy:
- if hybrid nav sample resolves `HybridNavigationMode.CaveVoxel`, cohesion weight drops to `0.1`
- otherwise cohesion stays at `0.8`

This keeps narrow corridors from turning into a self-blocking clump.

## Emergency Overclock

Source:
- `HectonSubmarineOsEvents.OnSnapshotUpdated`

When OS emergency level reaches `Evacuate`:
- thruster speed multiplier: `3x`
- battery drain multiplier: `5x`

The fleet snapshot mirrors this so diegetic UI can display it without scanning live scene objects.

## Suicide Weld

External trigger:
- `DroneFleetManager.RequestFleetSacrifice()`

Runtime behavior:
- if the assigned target is below `5%` recoverable integrity and the command is armed
- the drone force-repairs missing integrity, clears flood state, dispatches a final additive weld, reports itself destroyed, and does not return to service

This is intentionally destructive and should be treated as a last-resort command path.

## Verification Boundaries

What this document does prove:
- owner mapping
- assignment score math
- claim-cap model
- logistics reservation path
- emergency-overclock contract

What it does not prove:
- global project compile-green state
- fleet dispatch time under `0.2 ms`
- live Unity console green state

Status: `PENDING VERIFICATION`
