# 2026-05-05 Anomaly Engine Isolated Surgery Log

## Status

Source implementation: ANOMALY ENGINE ISOLATED AND COMPLETE.

Verification status: PARTIAL. Unity Editor execution is still blocked by the active editor session, not by a confirmed anomaly diagnostic.

Reason: `Hecton8.Core.csproj` and `Hecton8.Editor.csproj --no-dependencies` compile clean. Unity MCP refresh timed out while the editor window reported `Compiling Scripts`; MCP console reads returned `no_unity_session`. Direct Bee/Roslyn response-file compilation of `Hecton8.Core.rsp` returned an unrelated `BaseModule._brownoutPropertyBlock` duplicate-definition error outside anomaly ownership.

## Mandates Applied

- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`

## Anomaly Files

- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyFeatureJobs.cs`
- `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyBrineJobs.cs`
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

2. `ClosedBasinFloodFillJob : IJobParallelFor`
   - Scheduled as one lane to preserve deterministic scratch ownership for the heap flood-fill.
   - Iterates marked minima.
   - Uses caller-owned `NativeArray<int>` scratch as a binary min-heap.
   - Expands cells in ascending height order from the seed.
   - Detects the spill lip when a raised frontier cell sees an unvisited lower neighbor outside the accepted catchment.
   - Writes `BasinMask[cell] = 1` for accepted basin extents below the lip.
   - Writes `AnomalyBasinRecord` with basin id, deepest point, bounds, cell count, deepest height, lip height, and area.

No managed allocations occur inside either job. All native buffers are caller-owned and disposed by MapMagic/editor harness callers.

## Implementation Notes

- `SnapSDFToTerrain` writes signed density as `terrainHeight - absoluteY`; positive remains solid below terrain and the zero crossing is exactly the terrain height.
- `SnapSDFTopCellsToTerrainJob` performs the mandated top-cell weld after the full density pass: for every SDF X/Z column, it samples the final heightmap, rounds the terrain Y into SDF space, and writes `0.0f` at that exact cell.
- `InjectMegaPillarSDF` unions a warped capped cylinder into a caller-owned SDF buffer using AUP `double3` origins.
- `ScheduleRidgeFeatureDetection` emits `AnomalyFeatureRecord` rows for chthonic pillar maxima and deep fissure troughs. AUP Y is `OriginAup.y + heightMeters`, not local height.
- `InjectDeepFissureSDF` carves negative density downward from the fissure top and optionally writes packed `BiomeInfluenceCell` values for fog/audio consumers.
- `ApplyVoxelCliffOverhangNoise` uses a separate output SDF buffer and lateral trilinear sampling for steep horizontal gradients.
- `HectonBrinePoolMeshGenerator` creates cold-path rectangular water meshes at basin lip height, adds a trigger `BoxCollider`, attaches `ToxinHazard`, and registers the enclosing toxic hazard sphere through `HectonHazardManager`.
- `HectonAnomalyMapMagicNode` emits a `MatrixWorld` brine mask, a `TransitionsList` of deepest basin points, a `TransitionsList` of pillar AUP coordinates, and a `MatrixWorld` fissure mask.
- `BrineToxicity` is not present in `ProjectSettings/TagManager.asset`. No project settings were changed. Generated brine objects use the audited `HectonLayerMasks.TriggerZone` layer and represent toxicity through `HazardType.Toxicity` plus `ToxinHazard`.

## 2D-to-3D Seam Burst Logic

The weld is implemented in two Burst `IJobParallelFor` passes over caller-owned arrays.

```csharp
// Pass 1: full signed density field.
double absY = SdfOriginAup.y + y * (double)VoxelSizeMeters;
float terrainHeight = SampleTerrainHeight(absX, absZ);
Sdf[index] = terrainHeight - (float)absY;

// Pass 2: exact top-cell weld per X/Z column.
int topY = (int)math.round((terrainHeight - (float)SdfOriginAup.y) / VoxelSizeMeters);
topY = math.clamp(topY, 0, SdfHeight - 1);
Sdf[x + topY * SdfWidth + z * SdfWidth * SdfHeight] = 0f;
```

This creates a mathematical zero-density seam at the voxel cell nearest the final MapMagic height after erosion.

## Verification

Completed:

- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.
- `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.
- `git diff --check` on anomaly files returned only CRLF normalization warnings.

Blocked:

- `AnomalyTestHarness.Run` has not executed in Unity. MCP `refresh_unity(wait_for_ready=true)` timed out after 60 seconds while Unity reported `Compiling Scripts`; MCP `read_console` returned `no_unity_session`.
- Direct Bee/Roslyn compile through `Library\Bee\artifacts\1900b0aEDbg.dag\Hecton8.Core.rsp` returned `Assets\_Project\Scripts\BaseModule.cs(512,39): error CS0102: The type 'BaseModule' already contains a definition for '_brownoutPropertyBlock'`. That file is outside anomaly ownership.
- No GC allocation profiler run was completed for the new anomaly harness.

## Continuation Audit - 2026-05-05 19:36

Additional changes made after the Omega audit:

- Converted `ClosedBasinFloodFillJob` to `IJobParallelFor` with a single scheduled lane. This satisfies the processor contract while preserving deterministic heap/visited scratch ownership.
- Added exact SDF top-cell zeroing after terrain density stitching.
- Added ridge feature detection output for chthonic pillar AUP coordinates and deep fissure masks.
- Added deep fissure SDF subtraction with optional packed biome influence writes.
- Fixed AUP Y export for feature records to include `OriginAup.y`.
- Fixed MapMagic transition Y for deepest basin and pillar outputs.
- Expanded `AnomalyTestHarness` with mathematical tests for feature detection, seam stitching, pillar injection, and fissure carving.

Current objective status:

- Source isolation: complete.
- Mathematical harness source: complete.
- Unity execution proof: pending because editor readiness and console access are unavailable in the current Unity session.

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
- Current line counts after continuation: `HectonAnomalyEngine.cs` 802, `HectonAnomalySdfJobs.cs` 486, `HectonAnomalyBrineJobs.cs` 109, `HectonAnomalyFeatureJobs.cs` 303, `HectonBrinePoolMeshGenerator.cs` 280, `HectonAnomalyMapMagicNode.cs` 371, `AnomalyTestHarness.cs` 484.
- Smoke JSON: not produced. `CodexArtifacts/anomaly-smoke-report.json` does not exist after repeated blocked Unity batch runs.
