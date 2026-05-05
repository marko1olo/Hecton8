# 2026-05-05 Anomaly Engine Isolated Surgery Log

## Status

Source implementation: COMPLETE.

Verification status: PENDING VERIFICATION.

Reason: Unity batch execution has not reached `Hecton8.Editor.AnomalySmokeTester.Run`; see the Omega Audit section for current blocked-run evidence.

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

## Omega Audit - 2026-05-05

Status: PENDING VERIFICATION.

Reason: Unity batch execution of `Hecton8.Editor.AnomalySmokeTester.Run` returned exit code `1` before project compilation or method entry. The logs `CodexArtifacts/anomaly-smoke-unity-final.log`, `CodexArtifacts/anomaly-smoke-unity-omega-rerun.log`, `CodexArtifacts/anomaly-smoke-unity-omega-final2.log`, and `CodexArtifacts/anomaly-smoke-unity-omega-final3.log` stop after `Successfully changed project path to: C:\hades\Hecton8` and `Application will terminate with return code 1`. Concurrent unrelated Unity batch owners were observed for hydraulic erosion, survival kinematics, fauna, save persistence, visual omega, omega autonomy, documentation authority, narrative, construction automation, and tech-art smoke tests, so the anomaly smoke JSON was not produced.

Audit data:

- Native memory: all anomaly-owned `NativeArray` allocations in `HectonBrinePoolMeshGenerator`, `HectonAnomalyMapMagicNode`, `AnomalyTestHarness`, and `AnomalySmokeTester` have `NativeMemorySentinel.RegisterNativeArray` and unregister/dispose paths. SDF and basin jobs allocate no owned native memory.
- Barriers: `JobHandle.Complete()` exists only in cold callers: MapMagic generation, brine mesh generation, editor test harness, and editor smoke tester. No anomaly `Update`, `FixedUpdate`, `Tick`, `FixedTick`, or `LateUpdate` hot path was found.
- Static leak check: anomaly domain contains no `_instance` field and no `DontDestroyOnLoad`. `MapMagicBridge` on disk resolves runtime authority through `GlobalRegistry.MapMagic`.
- String purge: no anomaly hot loop strings found. One `StringBuilder.ToString()` remains in `AnomalySmokeTester.WriteReport`, editor cold path only.

Omega targets completed:

- Decomposition: SDF stitching, pillar injection, and overhang displacement jobs were extracted to `HectonAnomalySdfJobs.cs`. `HectonAnomalyEngine.cs` is now 543 lines.
- Performance: brine pool bounds resolution moved from managed per-cell scanning into Burst `ResolveBrinePoolBoundsJob : IJobParallelFor` in `HectonAnomalyBrineJobs.cs`.
- Stability: added `AnomalySmokeTester.cs` with perfect bowl, flat plane, and dual-bowl cases. It writes `CodexArtifacts/anomaly-smoke-report.json` when Unity reaches method entry.
- Telemetry: MapMagic anomaly generation emits `GlobalTelemetryBus.PublishPerformanceWarning` signals for large cell counts, max flood caps, and zero-basin output.

Verification data:

- Targeted anomaly source compile: `CSC_EXIT_CODE=0` in `CodexArtifacts/anomaly-omega-csc-final.log`.
- Line counts: `HectonAnomalyEngine.cs` 543, `HectonAnomalySdfJobs.cs` 267, `HectonAnomalyBrineJobs.cs` 90, `HectonBrinePoolMeshGenerator.cs` 214, `HectonAnomalyMapMagicNode.cs` 239, `AnomalyTestHarness.cs` 148, `AnomalySmokeTester.cs` 303.
- Smoke JSON: not produced. `CodexArtifacts/anomaly-smoke-report.json` does not exist after repeated blocked Unity batch runs.
