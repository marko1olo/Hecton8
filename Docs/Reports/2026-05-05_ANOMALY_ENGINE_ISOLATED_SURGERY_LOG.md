# 2026-05-05 Anomaly Engine Isolated Surgery Log
Date: 2026-05-07
Status: PENDING VERIFICATION

## Status

Source implementation: ANOMALY ENGINE ISOLATED AND COMPLETE.

Hadal basin source objective: ANOMALY DETECTION COMPLETE.

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

2. `ClosedBasinFloodFillJob : IJob`
   - Runs after the parallel scan to preserve deterministic scratch ownership for the heap flood-fill.
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
- `BrineToxicity` is not present in `ProjectSettings/TagManager.asset`. `AGENTS.md` forbids changing Tags/Layers, so no project settings were changed. Generated brine objects now resolve `LayerMask.NameToLayer("BrineToxicity")` once and use that layer if the project defines it; the current fallback remains audited `HectonLayerMasks.TriggerZone`.
- Brine toxicity uses the existing `HazardType.Toxicity` signal path. `HazardExposureNotifier` and `English.json` now report `TOXIC INCURSION` for `HAZARD_TOXICITY_ENTER`.

## Hadal Basin Discovery - 2026-05-05

Mandatory reconnaissance:

- Read `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`. Current project status remains `PENDING VERIFICATION`.
- Inspected `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`.

Local minima Burst logic:

```csharp
public void Execute(int index)
{
    if (IsBorder(index) || !math.isfinite(Heightmap[index]))
        return;

    float center = Heightmap[index];
    bool hasHigherNeighbor = false;

    for (int dz = -1; dz <= 1; dz++)
    for (int dx = -1; dx <= 1; dx++)
    {
        if ((dx | dz) == 0)
            continue;

        int neighborIndex = index + dx + dz * Width;
        float neighbor = Heightmap[neighborIndex];
        if (!math.isfinite(neighbor) || neighbor < center)
            return;

        hasHigherNeighbor |= neighbor > center;
        if (neighbor == center && neighborIndex < index)
            return;
    }

    if (hasHigherNeighbor)
        CandidateMask[index] = 1;
}
```

Flood-fill and mesh generation facts:

- `ClosedBasinFloodFillJob : IJob` runs a deterministic flood over caller-owned `NativeArray<int>` heap and visited scratch buffers. The local-minima scan remains `ClosedBasinDetectionJob : IJobParallelFor`.
- Accepted basins require `LipHeight - DeepestHeight >= minimumDepthMeters`; the default MapMagic node depth is `50f`.
- `HectonBrinePoolMeshGenerator` resolves basin bounds in `ResolveBrinePoolBoundsJob : IJobParallelFor`, then creates capped cold-path pool objects at the basin lip height.
- Each generated pool has `BoxCollider.isTrigger = true`, `ToxinHazard`, and a matching `HectonHazardManager.Register(..., HazardType.Toxicity, ...)` sphere.
- `HectonAnomalyMapMagicNode` exports `Brine Mask` as a `MatrixWorld` copied from `BasinMask`, enabling MapMagic terrain texture swaps such as `Viscous Mud`.

Memory and GC interrogation:

- `HectonAnomalyEngine` jobs allocate no native memory internally.
- `HectonAnomalyMapMagicNode`, `HectonBrinePoolMeshGenerator`, and `AnomalyTestHarness` own all new `NativeArray` allocations, register them with `NativeMemorySentinel`, and dispose them through `finally`/`DisposeTracked(...)`.
- No LINQ, `foreach`, `string.Format`, `Update`, `Tick`, or `SlowTick` patterns were found in the touched anomaly files.
- No new `GlobalRegistry.Get<T>()`, `Awake()`, or `OnEnable()` dependency query was added.

Verification delta:

- `CodexArtifacts/anomaly-hadal-core-build.log`: sequential `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `EXIT_CODE=0`.
- `CodexArtifacts/anomaly-hadal-editor-build.log`: sequential `dotnet build Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `EXIT_CODE=0`.
- `--no-dependencies` builds are not valid after Unity clears `Temp/bin/Debug`; they reported missing metadata for package/project DLLs, not anomaly source errors.
- After dependency DLLs were restored, `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.
- After dependency DLLs were restored, `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.

Crucible rerun defects found and fixed:

- `ClosedBasinFloodFillJob : IJobParallelFor` failed the editor harness with `IndexOutOfRangeException: Index 144 is out of restricted IJobParallelFor range [0...0]`. The flood-fill reads and writes arbitrary full-buffer cells by design, so it was converted to `ClosedBasinFloodFillJob : IJob` scheduled after the parallel local-minima scan.
- Anomaly Burst jobs used `CompileSynchronously = true`, which produced Unity editor sync-compile exceptions while scripts were compiling. Removed synchronous Burst compilation from the anomaly jobs; they remain Burst-compiled.
- Current source HAS `RadiationFatigueCriticalExposureSeconds` in `HectonPlayerHealth`; prior duplicate/missing-symbol console state is stale and is not current build truth.
- Unity MCP refresh after these fixes timed out waiting for editor readiness, and subsequent menu execution/console reads timed out. `CodexArtifacts/anomaly-hadal-harness-unity.log` batchmode also exited before method entry with `return code 1`. No post-fix `ANOMALY_TEST_HARNESS_PASS` console line was captured in this session.

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

- Converted `ClosedBasinFloodFillJob` to a Burst `IJob` after Unity proved that single-lane `IJobParallelFor` still enforces per-index restricted ranges for arbitrary flood-fill reads and writes.
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

## Omega Autonomy V2 Crucible - 2026-05-05

Status: PENDING VERIFICATION

Audit scope:

- `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyFeatureJobs.cs`
- `Assets/_Project/Scripts/World/HectonAnomalySdfJobs.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyBrineJobs.cs`
- `Assets/_Project/Scripts/World/HectonBrinePoolMeshGenerator.cs`
- `Assets/_Project/Scripts/World/HectonAnomalyMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs`

Garbage found and excised:

- `HectonBrinePoolMeshGenerator` registered brine hazards from `poolObject.transform.position` even though the runtime center had already been computed. Removed the transform read. `CreatePoolObject(...)` now returns `runtimeCenter` through an `out Vector3`, and `RegisterBrineHazard(...)` consumes that value directly. Remaining `transform.position` use is a presentation placement write only.
- `_activePools` was a `List<ActiveBrinePool>` with initial capacity 32 but no hard cap. Added `MaxGeneratedBrinePools = 32`, added `PoolCapWarningHash`, and stop generation before `_activePools.Add(...)` can force a List reallocation.
- Brine mesh generation allocated a new `Mesh` plus vertex/uv/normal/index arrays per pool. Replaced it with one owned shared unit quad mesh per generator, scaled each pool transform to its basin bounds, and added `DestroySharedPoolMesh()` in `OnDestroy()`.

Memory interrogation:

- `HectonAnomalyMapMagicNode` allocates nine `Allocator.TempJob` `NativeArray` buffers. All are registered through `NativeMemorySentinel.RegisterNativeArray(...)` and all are disposed in a `finally` block through `DisposeTracked(...)`.
- `HectonBrinePoolMeshGenerator` allocates one `Allocator.TempJob` bounds array. It is registered with `NativeMemorySentinel` and disposed in a `finally` block through `DisposeTracked(...)`.
- `AnomalyTestHarness` allocates editor-only `Allocator.TempJob` arrays per assertion. Every allocation is registered and disposed in local `finally` blocks.
- SDF, basin, brine bounds, feature detection, and overhang jobs allocate no native memory internally. They operate on caller-owned `NativeArray` buffers.

GC purge:

- No LINQ usage was found in the audited anomaly files.
- No `foreach` loop was found in the audited anomaly files.
- No `string.Format(...)` was found in the audited anomaly files.
- No `Update`, `FixedUpdate`, `LateUpdate`, `Tick`, `FixedTick`, or `SlowTick` method was found in the audited anomaly files.
- Cold generation still creates GameObjects for brine pools. That path is not a frame tick path and is capped at 32 pools.

AUP and init-order check:

- No `Vector3.Distance(...)`, `math.distance(...)`, `.magnitude`, or `.sqrMagnitude` distance calculation was found in the audited anomaly files.
- No `GlobalRegistry.Get<T>()`, `Awake()`, or `OnEnable()` dependency query was found in the audited anomaly files.
- `HectonHazardManager.Register(...)` internally resolves `GlobalRegistry.Environment`, but anomaly code calls it only from cold brine generation, not `Awake()` or `OnEnable()`.

Verification:

- `rg` scan for `IJob(?!ParallelFor)` in anomaly job files returned no matches.
- `rg` scan for LINQ/foreach/string.Format/update-tick patterns in anomaly files returned no matches.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.
- `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 errors, 0 warnings`.
- `git diff --check` on the edited anomaly files returned no whitespace errors.
- Unity MCP console proof remains blocked: `read_console` returned `Unity session not ready for 'read_console' (ping not answered)`.

Status decision:

- Source-level Crucible survived.
- Runtime status remains `PENDING VERIFICATION` until Unity console and profiler data are available.

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

