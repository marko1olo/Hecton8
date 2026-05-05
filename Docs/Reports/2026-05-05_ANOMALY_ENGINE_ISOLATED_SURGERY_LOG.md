# 2026-05-05 Anomaly Engine Isolated Surgery Log

## Status

Source implementation: COMPLETE.

Verification status: PENDING VERIFICATION.

Reason: Unity project compilation is currently blocked by pre-existing scatter backend accessibility errors outside the anomaly files. `Temp/anomaly-csc-tempout.log` reports `CS0122` on `ScatterBackendExecutionMode`, `ScatterSimulationBackendKind`, `IScatterSimulationBackend`, `ScatterSimulationConfig`, `ScatterSimulationResult`, and `ScatterSimulationCandidate`.

## Mandates Applied

- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`

## Files Added

- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`

## Closed Basin Burst Logic

The basin detector is split into two Burst jobs:

1. `ClosedBasinDetectionJob : IJobParallelFor`
   - Clears candidate, basin, and record outputs.
   - Rejects border samples.
   - Rejects non-finite heights.
   - Scans the 8-neighborhood.
   - Marks a cell as a seed only when no neighbor is lower, at least one neighbor is higher, and equal-height plateaus resolve to the lowest flat index.

2. `ClosedBasinFloodFillJob : IJob`
   - Iterates marked minima.
   - Uses caller-owned `NativeArray<int>` scratch as a binary min-heap.
   - Expands cells in ascending height order from the seed.
   - Detects the spill lip when a raised frontier cell sees an unvisited lower neighbor outside the accepted catchment.
   - Writes `BasinMask[cell] = 1` for accepted basin extents below the lip.
   - Writes `AnomalyBasinRecord` with basin id, deepest point, bounds, cell count, deepest height, lip height, and area.

No managed allocations occur inside either job. All native buffers are caller-owned and disposed by MapMagic/editor harness callers.

## Implementation Notes

- `SnapSDFToTerrain` writes signed density as `terrainHeight - absoluteY`; positive remains solid below terrain and the zero crossing is exactly the terrain height.
- `InjectMegaPillarSDF` unions a warped capped cylinder into a caller-owned SDF buffer using AUP `double3` origins.
- `ApplyVoxelCliffOverhangNoise` uses a separate output SDF buffer and lateral trilinear sampling for steep horizontal gradients.
- `HectonBrinePoolMeshGenerator` creates cold-path rectangular water meshes at basin lip height, adds a trigger `BoxCollider`, attaches `ToxinHazard`, and registers the enclosing toxic hazard sphere through `HectonHazardManager`.
- `HectonAnomalyMapMagicNode` emits a `MatrixWorld` brine mask and a `TransitionsList` of deepest basin points.

## Verification

Completed:

- Direct Roslyn compile of the new runtime/editor source set returned exit code `0`.
- Unity batch compile reached project script compilation and showed no anomaly-file compiler diagnostics.

Blocked:

- `AnomalyTestHarness.Run` could not execute because Unity compilation fails first on unrelated scatter backend access modifiers.
- MCP validation was unavailable after the Unity batch compile shut down the MCP local HTTP server.
