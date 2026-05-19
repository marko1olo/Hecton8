# AI Potential Field Navigation

Date: 2026-05-15
Status: PENDING UNITY VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R31 Current Boundary Note

R31 reread confirmed this file remains a static navigation/math contract, not fauna runtime, pathfinding quality, or profiler proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md`; R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

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

The simulator also writes `Data/AI/Navigation_Tuning.h8bin` as the SHINOBU/DataVault cache artifact and `Data/AI/Navigation_Tuning.manifest.json` as the standalone ingest manifest. The cache is little-endian, 16-byte aligned, and uses a 64-byte header plus 16-byte records:

```text
Header: <4sHHIIIIIIII24s
Record: <IHHfI
```

Every record key is an ASCII-lower FNV-1a 32-bit hash of the semantic field path. `Tools/VerifyAiNavigationTuning.py` regenerates the blob and manifest, checks byte-for-byte parity, checks file alignment, rejects endian drift, verifies zero FNV collisions, validates zeroed reserved header bytes, records header/payload CRC32 values, and validates the standalone manifest source JSON hash, section offsets, toaster payload, RTX extra data, and stateless lookup contract.

Artifact guard:

```text
python Tools/AiPathSim.py --check
python Tools/AiPathSim.py --check Data/AI/Navigation_Tuning.json
python Tools/VerifyAiNavigationTuning.py
```

The guard reloads the exported JSON, reconstructs the selected weights, replays the full candidate search, and rejects stale or weakened data if reach, SDF clearance, jitter, idle drift, path trace samples, stored raw/smoothed metrics, source constants, source contract file references, or the 100-predator performance model regress. Source drift is checked against every matching live constant in `Assets/_Project/Scripts/HectonFluidEngine.cs`, including `AbyssalFlowTextureResolution`, `AbyssalFlowTextureWorldSizeMeters`, `VectorNoiseResolution`, `SurfaceStormLayerDepthMeters`, `StormSurfaceTurbulenceStrength`, `AbyssalFlowThermoclineDepthMeters`, and `MaxAbyssalHeatSourceCount`. All `sourceFiles` entries must be relative project paths that exist on disk.

The simulator scenario constants are derived from the flow texture cell size, world size, surface storm layer, turbulence scalar, vector-noise resolution, and heat-source capacity. The tuning file now carries this derivation explicitly in `mathAudit.sourceScenarioDerivation`; fixture weights are replay-selected, not hand-entered guesses. The replay-only fixture constants also have a derivation trail in `mathAudit.simulationConstants`: 10Hz `dt`, 36s search horizon, sub-cell target radius, SDF inverse-square clamp, pushout clearance, damping, scoring weights, and 100-predator performance assumptions are named and validated instead of living as anonymous Python literals.

## Scalability

Low/MX350:
- 10Hz steering.
- One analytical flow sample.
- One nearest SDF/clearance sample or local proxy.
- Stronger EWMA smoothing and lower flow boost.
- SHINOBU runtime can consume the stripped `toasterData` binary records with no strings and no GPU readback.

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
- `rtxOverkillData` exposes harmonic flow bands, richer SDF gradient samples, visual banking curvature samples, and wake-ribbon samples for presentation-only overkill after frame/VRAM guards pass.

Tier switching uses hysteresis from `Data/AI/Navigation_Tuning.json`: 5m distance band and 3s dwell time. Runtime must satisfy both before changing steering tier so predators do not flip between Low/Middle/High/Ultra within the same engagement.

## Failure Modes

- Non-finite flow: discard flow and use target/SDF steering.
- Negative SDF clearance: use strongest positive SDF gradient, suppress attack thrust, and mark black box flags.
- Steering jitter over 3-frame window: increase smoothing alpha toward Low tier and cap flow boost.
- Sustained frame pressure: reduce steering cadence to 5Hz and cluster targets in 16m cells.

## Verification Boundary

Python simulator evidence is not Unity runtime proof. Unity Console, Play Mode, Burst profiler, GCMonitor, and scene wiring remain PENDING VERIFICATION.
