# 2026-05-05 Chthonic Pillars Surgery Log

## Status

Source status: CHTHONIC PILLARS COMPLETE.

Verification status: PENDING VERIFICATION.

Reason: Unity/MCP is not stable enough to run the harness to completion. The Editor log is blocked by unrelated `HectonPlayerHealth` and older `PlayerPDA` compiler errors, and MCP repeatedly drops its WebSocket session. No anomaly or pillar compiler diagnostics were present in the Unity Editor log after the anomaly files were listed in the compile input.

## Mandatory Reconnaissance

- Read: `C:\hades\Hecton8\.agents-skills\VOX_Voxel_World_Logic_Carving_Persistence.txt`.
- Inspected: `Assets/_Project/Scripts/HectonVoxelEngine.cs`.
- Inspected: `Assets/_Project/Scripts/VoxelSeamDirector.cs`.

Finding: the mandate states `negative=solid, positive=void`, but the active `HectonVoxelEngine` writes terrain density as `terrainH - wp.y`, so the current project convention is positive below terrain and negative above terrain. The pillar and seam jobs preserve the active engine convention to avoid breaking existing Marching Cubes zero-crossings, normals, and nav thresholds.

## Implementation

- `HectonAnomalyFeatureJobs.cs`
  - Pillar anchors now require a local maximum plus at least 3 descending ridge arms.
  - This matches the requested 3+ Voronoi-ridge junction behavior using the eroded height/ridge field available to the node.

- `HectonAnomalySdfJobs.cs`
  - `SnapSDFToTerrainJob` writes the full signed terrain density field.
  - `SnapSDFTopCellsToTerrainJob` pins the nearest top terrain cell in every XZ column to exact `0f`.
  - `InjectMegaPillarSDFJob` injects a noise-warped vertical cylinder into caller-owned SDF arrays.
  - `VoxelCliffOverhangNoiseJob` applies lateral XZ domain-warp sampling only on steep SDF gradients.

- `HectonAnomalyMapMagicNode.cs`
  - Outputs brine mask, deepest basin points, pillar AUP coordinates, and fissure mask.
  - Default brine depth threshold is now 50m.
  - Pillar detection exposes minimum prominence and minimum ridge-arm count.
  - In Play Mode, completed pillar feature records are forwarded to `HectonAnomalyResourceBinding` with a capped per-tile binding budget.

- `ResourceDistributionDirector.cs`
  - Added `TryBindChthonicPillarResourcesAtAup`.
  - It binds each pillar surface to one Deep Mantle Geode and one pressure/thermal rare ore fallback through existing pool, sector, tombstone, and spatial registration paths.

- `HectonAnomalyResourceBinding.cs`
  - Added cold-path bridge from `NativeArray<AnomalyFeatureRecord>` pillar records to `ResourceDistributionDirector`.

- `HectonAnomalyEngine.cs`
  - Basin flood-fill remains a Burst `IJobParallelFor` per directive.
  - Scratch arrays used by the single-work-item flood-fill pass are marked with `NativeDisableParallelForRestriction` because the algorithm must scan heap, visited, accepted, mask, and record buffers by arbitrary basin indices.

## Burst Seam Snap Code

The exact terrain weld is two-phase:

```csharp
Sdf[index] = terrainHeight - (float)absY;
```

Then the column pass forces the mathematically nearest terrain-roof voxel to the zero surface:

```csharp
int y = (int)math.round((terrainHeight - (float)SdfOriginAup.y) / VoxelSizeMeters);
y = math.clamp(y, 0, SdfHeight - 1);
Sdf[x + y * SdfWidth + z * SdfWidth * SdfHeight] = 0f;
```

This creates exact `0.0f` density at the stitched MapMagic height for every SDF XZ column.

## Performance Budget

The SDF modifications are flat `IJobParallelFor` passes over caller-owned arrays with batch size 64 and no per-cell managed allocation. The 0.5ms Marching Cubes budget is not proven yet because Unity PlayMode/profiler execution is blocked by unrelated compilation errors.

## Verification Evidence

Completed:

- Unity Editor log lists anomaly/pillar files in script compile input:
  - `HectonAnomalyBrineJobs.cs`
  - `HectonAnomalyEngine.cs`
  - `HectonAnomalyFeatureJobs.cs`
  - `HectonAnomalyMapMagicNode.cs`
  - `HectonAnomalyResourceBinding.cs`
  - `HectonAnomalySdfJobs.cs`
  - `ResourceDistributionDirector.cs`
- Targeted Roslyn compile passed for the anomaly implementation files against the freshly built `Hecton8.Core.dll` and Unity/package references:
  - `HectonAnomalyBrineJobs.cs`
  - `HectonAnomalyEngine.cs`
  - `HectonAnomalyFeatureJobs.cs`
  - `HectonAnomalySdfJobs.cs`
  - `HectonAnomalyMapMagicNode.cs`
  - `HectonAnomalyResourceBinding.cs`
  - `HectonBrinePoolMeshGenerator.cs`
  - Output: `CodexArtifacts/chthonic-pillars-targeted.dll`.
- External project build log `CodexArtifacts/dotnet-space-engine-terrain-build-2026-05-05.log` shows `Hecton8.Core` build succeeded, 0 warnings, 0 errors. The generated `Hecton8.Core.csproj` still omits the new anomaly files, so the targeted Roslyn compile above is the relevant anomaly evidence.
- A previous harness attempt exposed an anomaly-side `IJobParallelFor` safety exception in `ClosedBasinFloodFillJob`. The source now marks the flood-fill scratch buffers with `NativeDisableParallelForRestriction`; re-run is blocked by Unity/MCP instability.
- Diff artifact exported: `CodexArtifacts/2026-05-05_CHTHONIC_PILLARS_DIFF.patch`.

Blocked:

- Unity compile/log path is also blocked by unrelated `HectonPlayerHealth.cs(130,99)` missing `RadiationFatigueCriticalExposureSeconds` and older `PlayerPDA` compile errors in the log.
- MCP console access is unavailable; final `refresh_unity(wait_for_ready=true)` attempt timed out after 60 seconds and the Editor log tail only reports MCP WebSocket closure.
- No profiler measurement exists yet for the 0.5ms MC budget.
