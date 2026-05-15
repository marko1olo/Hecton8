# AI Potential Field Navigation

Date: 2026-05-15
Status: PENDING UNITY VERIFICATION
Owner: AI_POTENTIAL_FIELD_NAVIGATOR
Domain: ECHELON 3 - FLORA, FAUNA & BIOTA

## Scope

This contract designs flow-aware steering for predators and other fauna that swim through `AbyssalFlowField` currents. It does not replace voxel pathfinding, funnel smoothing, predator cognition, or the fluid engine. It defines the cheap local steering layer used after cognition chooses a target.

## Source Boundaries

- Flow input: `HectonFluidEngine.TrySampleModAbyssalFlow` / analytical current snapshot. GPU flow texture stays presentation-side; no CPU readback.
- Obstacle input: voxel SDF or `VoxelDynamicNavGridRuntime` clearance/passability snapshot. Static NavMesh remains rejected.
- Target input: player, prey biomass, scent memory, or pack target already resolved by cognition.
- Output: desired steering direction plus force/speed multipliers for the fauna movement owner.

No direct dependency on concrete fluid, voxel, or player classes is required in hot paths. Runtime code should cache interfaces/snapshots outside Tick and consume plain data inside Burst jobs.

## Flow Parameters Read

Source files:

- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`

Pinned values in `Data/AI/Navigation_Tuning.json`:

- Flow texture resolution: `32`
- Flow texture world size: `100 m`
- Flow texture cell size: `3.125 m`
- Vector noise resolution: `32`
- Surface storm layer depth: `50 m`
- Storm surface turbulence strength: `0.4`
- Abyssal thermocline depth: `120 m`
- Heat source capacity: `8`

## Runtime Phase

- `PRE_SIMULATION`: validate DataVault/snapshot handles and load-shed cadence.
- `SIMULATION`: solve potential steering at 10Hz base cadence, staggered by creature id.
- `POST_SIMULATION`: swap steering outputs, write the 300-frame AI black box entry, publish broad typed signals only when state changes.
- `VISUAL_SYNC`: animation/VAT/audio consume steering output; no gameplay truth mutation.

## Black Box

Runtime port must keep `NativeArray<AiPotentialFieldTelemetryEntry>[300]` as a circular buffer and dump it to `Docs/AgentLogs/Dump_AI_POTENTIAL_FIELD_NAVIGATOR.bin` on non-finite state, negative SDF clearance, or source-parameter drift detection.

Required telemetry fields are exported in `Data/AI/Navigation_Tuning.json`: `frameIndex`, `entityId`, `positionAupCell`, `positionLocalMeters`, `velocityMetersPerSecond`, `targetDistanceMeters`, `flowAlignmentSigned`, `sdfClearanceMeters`, `stateFlags`, and `stateHash`. All float fields feeding steering or rendering must be finite-guarded before write.

## Steering Formula

```text
targetDir = normalize(target - position)
flowDir   = normalize(flow)
align     = dot(flowDir, targetDir)

targetForce = targetDir * targetAttractionWeight
flowForce   = flowDir * flowSpeed * (align >= 0 ? flowAlignmentBoostWeight : -flowResistanceWeight)
sdfForce    = wallNormal * min(maxRepulsion, sdfObstacleRepulsionWeight / max(distanceMeters, 0.25)^2)

softIntent  = clampMagnitude(targetForce + flowForce, maxAcceleration)
smoothed    = lerp(previousSteering, softIntent, ewmaSteeringAlpha)
steering    = clampMagnitude(smoothed + sdfForce, maxAcceleration)
```

EWMA is intentionally not applied to SDF repulsion. Wall response is immediate; smoothing the wall term delays avoidance and causes clipping.

Idle fauna do not fight the current:

```text
idleVelocity += flowVector * idleFlowCoupling * dt
```

This gives organic drift without simulating water as physical authority for every entity.

## Tuning Export

The simulator writes `Data/AI/Navigation_Tuning.json` with selected weights, tier profiles, jitter metrics, idle drift metrics, compact path trace samples, and a static performance model.

Artifact guard:

```text
python Tools/AiPathSim.py --check
python Tools/AiPathSim.py --check Data/AI/Navigation_Tuning.json
```

The guard reloads the exported JSON, reconstructs the selected weights, replays the full candidate search, and rejects stale or weakened data if reach, SDF clearance, jitter, idle drift, path trace samples, stored raw/smoothed metrics, source constants, source contract file references, or the 100-predator performance model regress. Source drift is checked against every matching live constant in `Assets/_Project/Scripts/HectonFluidEngine.cs`, including `AbyssalFlowTextureResolution`, `AbyssalFlowTextureWorldSizeMeters`, `VectorNoiseResolution`, `SurfaceStormLayerDepthMeters`, `StormSurfaceTurbulenceStrength`, `AbyssalFlowThermoclineDepthMeters`, and `MaxAbyssalHeatSourceCount`. All `sourceFiles` entries must be relative project paths that exist on disk.

## Scalability

Low/MX350:
- 10Hz steering.
- One analytical flow sample.
- One nearest SDF/clearance sample or local proxy.
- Stronger EWMA smoothing and lower flow boost.

Middle:
- 10Hz steering.
- Capped local obstacle loop.
- EWMA final steering.

High:
- 15Hz steering.
- Richer SDF gradient and tighter smoothing.
- Saved CPU buys better visual path curvature and animation banking.

Ultra:
- 20Hz steering.
- Optional local vortex interest for visual overkill.
- Still O(1); no global A* spam and no GPU readback.

Tier switching uses hysteresis from `Data/AI/Navigation_Tuning.json`: 5m distance band and 3s dwell time. Runtime must satisfy both before changing steering tier so predators do not flip between Low/Middle/High/Ultra within the same engagement.

## Failure Modes

- Non-finite flow: discard flow and use target/SDF steering.
- Negative SDF clearance: use strongest positive SDF gradient, suppress attack thrust, and mark black box flags.
- Steering jitter over 3-frame window: increase smoothing alpha toward Low tier and cap flow boost.
- Sustained frame pressure: reduce steering cadence to 5Hz and cluster targets in 16m cells.

## Verification Boundary

Python simulator evidence is not Unity runtime proof. Unity Console, Play Mode, Burst profiler, GCMonitor, and scene wiring remain PENDING VERIFICATION.
