# AGENT_02_AI_LOG

Date: 2026-04-26
Status: PENDING VERIFICATION

## Scope

- `Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/BoidFishInstanced.shader`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`

## Structural Changes

- Reordered compute shader pragma order so `CSMain` is kernel index `0` by source definition.
- Replaced stale float-state remnants in `CSMain` with packed `uint StateFlags`.
- Hardened compute dispatch binding in `SargassumMicroFaunaBoids.cs` with explicit manual kernel indices.
- Removed runtime dependence on `FindKernel`. Validation now checks `HasKernel` and `GetKernelThreadGroupSizes` against the fixed index map before dispatch.
- Kept fail-closed behavior: invalid or missing kernels disable compute dispatch and preserve static rendering fallback instead of throwing.
- Synced the instanced render shader boid buffer layout to `uint stateFlags` so GPU readback/render stride remains 32 bytes.
- Added explicit GPU layout verification in `SargassumMicroFaunaBoids.cs`:
  - `UnsafeUtility.SizeOf<BoidData>() == 32`
  - `UnsafeUtility.AlignOf<BoidData>() == 4`
  - `Marshal.OffsetOf(StateFlags) == 28`
  - `UnsafeUtility.SizeOf<SimulationFrameConstants>() == 496`
- Forced explicit `Pack = 4` on all ancillary compute-shared structs mirrored into HLSL:
  - `GrazingAnchorData`
  - `MassiveThreatData`
  - `FormationBeaconData`
  - `FormationObstacleData`
  - `LeviathanNodeData`
- Extended fail-closed validation so ancillary GPU buffer stride drift disables compute dispatch before kernel submission.
- Added `Assets/_Project/Scripts/World/BoidStructValidator.cs` with `[InitializeOnLoadMethod]`:
  - `UnsafeUtility.SizeOf<SargassumMicroFaunaBoids.BoidData>() == 32`
  - `Marshal.SizeOf<SargassumMicroFaunaBoids.BoidData>() == 32`
  - `Marshal.OffsetOf(Position) == 0`
  - `Marshal.OffsetOf(Velocity) == 12`
  - `Marshal.OffsetOf(Panic) == 24`
  - `Marshal.OffsetOf(StateFlags) == 28`
  - any mismatch logs an editor error and throws before play-mode validation continues
- Moved foveated tier evaluation out of the main-thread cadence branch and into a Burst `IJob` with front/back `NativeArray` decision buffers.
- Preserved the double-buffer contract:
  - frame `N` consumes the front decision
  - frame `N` schedules the back decision
  - swap occurs only after job completion
- Standardized cognition runtime world-state packing in `PredatorCognitionDomain`:
  - `bit 0 = active`
  - `bit 1 = hunting`
  - `bit 2 = fleeing`
- Replaced float drive storage in `CognitionCore` with packed lanes:
  - `QuantizedDrives` bits `0-7 = hunger`
  - `QuantizedDrives` bits `8-15 = aggression`
  - `QuantizedDrives` bits `16-23 = fear`
  - `QuantizedDrives` bits `24-31 = threat`
  - `QuantizedFatigue` low byte at offset `48` preserves the fatigue lane without expanding the 64-byte core
- Replaced native cognition output storage with `PackedCognitionOutput`:
  - packed score bytes for `hunger`, `aggression`, `fear`
  - `uint StateMask`
  - `uint OutputFlags`
- Added explicit watchdog guard to `FaunaBrain` slow-tick `while` loop.
- Added panic contagion to `SargassumMicroFaunaBoids.compute` during the existing 3x3x3 spatial-hash neighborhood pass:
  - neighbors above the panic threshold bleed `10%` of their panic into the current boid
  - contagion uses `max()` accumulation, not additive fan-in, to avoid runaway school-wide saturation
  - no extra whole-swarm pass was introduced
- Replaced predator weighted-additive action scoring with polynomial utility curves in `PredatorCognitionDomain`:
  - hunt utility uses `pow(hunger, 2.5) * pow(1 - fear, 3.0)` as the core drive term
  - patrol and flee decisions are also evaluated through Burst-safe polynomial helpers
  - state selection remains arg-max over utility scores, not a linear `if/else` branch ladder
- Added a world-budgeted active-count gate in `SargassumMicroFaunaBoids`:
  - GPU buffers stay fixed-capacity at authored `boidCount`
  - dispatch/render count now uses `_activeBoidCount`
  - `_activeBoidCount` is recomputed on the existing `SystemDispatcher` slow-tick cadence from `WorldProceduralScatterDirector` spawn-budget and fauna-activation scales
- Threat decay in `HectonMapMagicVegetationBridge.ThreatPropagationJob` now uses exponential retention:
  - `retention = exp(-decayRate * dt)`
  - attractor retention boosts reduce the decay rate instead of linearly lerping retention after the fact

## Foveated AI Tiers

- Tier 0: `< 50m`
  - Full compute path.
  - Spatial grid build, PBD solve, full flocking, specialty steering, threat response.
- Tier 1: `50m - 200m`
  - Simplified compute path.
  - Reduced cadence from Burst-authored foveated scheduler.
  - No spatial-grid pass, no PBD pass, no grazing anchors, no canopy affinity, no formation logic, no leviathan body-follow logic, no parasite latch logic, no camera avoidance logic.
  - Position/velocity integration, containment, panic, and basic movement only.
  - Emotional contagion is intentionally skipped here because Tier 1 no longer traverses the spatial hash.
- Tier 2: `> 200m`
  - Sleep tier.
  - No steady-state compute dispatch.
  - One transition-time `CSMain` pass writes `Velocity = 0` into the boid buffer when the swarm first enters sleep.
  - Data retained for rendering/static representation path only.
  - Render path also forces effective boid velocity scale to `0` as a far-field safety net.

## Threat Grid Audit

- Verified existing 3D threat voxel consumers use flat `NativeArray<byte>`.
- Verified strict flattening formula remains:
  - `x + y*W + z*W*H`
- Verified `ThreatPropagationJob` remains O(N) over `_ecosystemThreatGridCellCount`:
  - one `IJobParallelFor` execution per cell
  - bounded local neighbor sampling only
  - no nested whole-grid scans inside `Execute`
- Updated decay law inside `ThreatPropagationJob` from linear retention to exponential retention while preserving the same O(N) work shape.
- Verified `ThreatVoxelizationJob` writes flat voxel bytes directly into `Output[index]` and decomposes the same flat index back to `x/y/z`.
- Verified in:
  - `PredatorCognitionDomain.FlattenThreatVoxelIndex`
  - `HectonMapMagicVegetationBridge.FlattenThreatVoxelIndex`
  - `HectonMapMagicVegetationBridge.ThreatVoxelizationJob.Execute`

## Kernel Map

- `CSMain = 0`
- `ClearLatchStats = 1`
- `ClearSpatialGrid = 2`
- `BuildSpatialGrid = 3`
- `ClearPBDCorrections = 4`
- `KernelPBDSolve = 5`

## Regression Model

- CPU:
  - Tier 1 and Tier 2 reduce dispatch cost by skipping grid/PBD/specialty logic outside near-field.
  - Foveated tier choice no longer burns main-thread cadence logic each tick.
- GC:
  - No new managed allocations added to hot paths.
- Memory:
  - Boid GPU stride preserved at 32 bytes.
  - Added three single-slot persistent native buffers for the foveated Burst scheduler.
  - Cognition drive/state storage is denser than the prior float layout.
- Cadence:
  - Existing double-buffer GPU read/write path preserved.
  - Foveated scheduler is now also double-buffered.
- Correctness:
  - Compile blocker fixed in compute shader.
  - Kernel binding no longer depends on runtime name lookup.
  - Boid compute dispatch now hard-fails on struct stride/offset mismatch.
  - Cognition score/state export now moves through packed lanes instead of raw floats.
  - Threat voxel indexing verified unchanged.

## Failure Modes

- If Unity strips or fails to compile a compute kernel, dispatch is disabled and the system falls back to static rendering.
- If any future change reorders compute shader pragmas without updating the manual index constants, validation will fail closed on startup.
- If `BoidData`, ancillary GPU buffers, or `SimulationFrameConstants` drift from the HLSL layout, compute dispatch is disabled before any kernel dispatch.
- If the foveated Burst job stops completing, the system reuses the last completed front decision rather than recomputing on the main thread.
- Legacy `FaunaBrain.AIState` still exists as a compatibility layer for managed state machine consumers. Packed `StateFlags` is now the canonical low-level world-state channel.
