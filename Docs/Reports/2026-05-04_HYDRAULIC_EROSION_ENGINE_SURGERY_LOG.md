# Hydraulic Erosion Engine Surgery Log

Date: `2026-05-04`
Status: `PENDING VERIFICATION`
Scope: standalone Burst hydraulic erosion, thermal slumping, MapMagic generator node, editor PNG harness.

## Mandates Followed

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`

## Files Added

- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs`
- `Assets/_Project/Scripts/World/ThermalSlumpingJob.cs`
- `Assets/_Project/Scripts/World/ErosionHarnessJobs.cs`
- `Assets/_Project/Scripts/World/HydraulicErosionMetricsJob.cs`
- `Assets/_Project/Scripts/World/HectonHydraulicErosionMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs`
- `Assets/_Project/Scripts/Editor/HydraulicErosionSmokeTester.cs`

## Implementation Facts

- `HydraulicErosionJob` is a Burst `IJob` over a caller-owned `NativeArray<float>` heightmap.
- Droplets use deterministic hash RNG, momentum, sediment capacity, erosion, deposition, water evaporation, and final evaporation deposition.
- Dendritic channel enforcement is implemented by choosing the best spawn cell from multiple deterministic candidates scored by local depression plus existing wear-channel intensity.
- Local depression deposition fills the lowest cells in a 3x3 neighborhood toward a target height before fallback bilinear deposition, creating flat local sediment plains.
- `ThermalSlumpingJob` is a Burst `IJobParallelFor` that transfers material down slopes exceeding the critical talus angle.
- `HectonHydraulicErosionMapMagicNode` exposes a MapMagic height inlet and three outlets: eroded height, sediment mask, wear mask.
- The MapMagic node reserves a configurable 4-pixel default margin by spawning droplets in the core while allowing flow into the overlapped boundary.
- Sediment and wear outputs are normalized to strict `0.0..1.0` before MapMagic product storage.
- `HectonHydraulicErosionMapMagicNode` registers and unregisters every `Allocator.TempJob` native buffer with `NativeMemorySentinel`.
- `HectonHydraulicErosionMapMagicNode` publishes `GlobalTelemetryBus.PublishPerformanceWarning` markers for large droplet budgets, large cell budgets, and blocking barrier stalls above 25 ms.
- `ErosionFractalHeightmapJob` moves editor-harness fractal terrain generation into a Burst `IJobParallelFor`.
- `ErosionSmokeMetricsJob` writes a blittable metrics record covering height ranges, sediment/wear maxima, mean absolute delta, changed cells, and non-finite cells.
- `ErosionTestHarness` generates a 512x512 fractal heightmap, runs erosion plus slumping, and writes PNG plus JSON metrics outputs under `CodexArtifacts/`.

## Droplet Capacity Code

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static float CalculateSedimentCapacity(
    float heightDelta,
    float speed,
    float water,
    float capacityFactor,
    float minCapacity)
{
    float downhillSlope = math.max(-heightDelta, 0.001f);
    float velocityTerm = math.max(speed, 0.01f);
    float waterTerm = math.max(water, 0.01f);
    float rawCapacity = downhillSlope * velocityTerm * waterTerm * math.max(0f, capacityFactor);
    return math.max(rawCapacity, math.max(0f, minCapacity));
}
```

## Regression Model

- CPU: MapMagic generation can be expensive at high droplet counts. The node reduces droplets during draft generation. This is generation-time work, not gameplay Tick.
- GC: hot job logic is unmanaged/Burst. MapMagic generation and editor harness allocate managed matrices/textures outside gameplay hot paths. Profiler proof absent.
- Memory: all new native containers in the node and harness use `Allocator.TempJob`, register with `NativeMemorySentinel`, complete before disposal, unregister before disposal, and do not persist across frames.
- Cadence: the only `.Complete()` calls are annotated blocking generation/editor harness sync points. No gameplay Tick owner was added.
- Correctness: boundary bleed is implemented as a 4-pixel overlapped processing margin. True cross-tile validation requires MapMagic graph execution on adjacent chunks.

## Verification

- Passed: MCP `validate_script` standard diagnostics on `HydraulicErosionJob.cs`, `ThermalSlumpingJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, and `ErosionTestHarness.cs` returned zero errors and zero warnings.
- Blocked: Unity script refresh/import completed, but project compilation is blocked by unrelated current errors in `PlayerCriticalProceduralAudioRenderer.cs`, `HectonSurvivalSystem.cs`, `StorageCrate.cs`, and `PredatorCognitionDomain.cs`.
- Blocked: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` fails on unrelated stale/project errors before a clean assembly build can be claimed.
- Blocked: 2026-05-05 Unity MCP validation unavailable because no Unity Editor instance is connected (`instance_count: 0`).
- Blocked: 2026-05-05 local `dotnet build` exits `1`; diagnostic scan reports `Assets/_Project/Scripts/BaseModule.cs(349,34): error CS0246: BaseModuleCondensationSurface could not be found`.
- Passed: scoped source scan on erosion files found no `_instance` or `DontDestroyOnLoad`; `Run()` matches are editor harness/menu entry points only.
- Pending: MapMagic node graph execution.
- Pending: editor harness PNG/JSON generation; cannot execute until Unity Editor connection and project compilation are restored.
- Pending: GCMonitor/profiler capture.

## 2026-05-05 Omega-Autonomy Continuation

Status: `PENDING VERIFICATION`

### Additional Mandates Followed

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

### Surgery Log

- Registered MapMagic node TempJob buffers `heightA`, `heightB`, `sediment`, and `wear` with `NativeMemorySentinel` using `NativeAllocationLifetime.TempJob`.
- Registered editor harness TempJob buffers `before`, `heightA`, `heightB`, `sediment`, `wear`, and `metrics` with `NativeMemorySentinel`.
- Added `ErosionHarnessJobs` with Burst fractal heightmap generation and harness metrics jobs.
- Added `HydraulicErosionMetricsJob`, a Burst `IJobParallelFor` that scans height/sediment/wear buffers in blocks for smoke-test metrics.
- Added `HydraulicErosionSmokeTester`, an editor smoke runner with four scenarios: `dry_zero_power`, `tiny_margin_clamp`, `draft_tile`, and `thermal_stress`.
- Replaced the smoke tester scalar heightmap seed loop with `ErosionFractalHeightmapJob` so the smoke terrain generation is also Burst `IJobParallelFor` work.
- Added cold-path `GlobalTelemetryBus.PublishPerformanceWarning` calls for MapMagic droplet/cell budget violations and smoke-test failure reporting.
- Converted TempJob disposal in the MapMagic node and editor harness to unregister-before-dispose helper paths.

### Forensic Audit Data

- NativeCollections: scoped scan found all new caller-owned TempJob arrays are registered and unregistered in their owner files. Job structs still expose caller-owned `NativeArray<T>` fields; they do not allocate.
- Barriers: scoped scan found `.Complete()` at `HectonHydraulicErosionMapMagicNode.cs:273`, `ErosionTestHarness.cs:126`, and `HydraulicErosionSmokeTester.cs:216`. All are generation/editor sync points, not gameplay `Tick`, `FixedTick`, or `Update`.
- Static leaks: scoped scan found no `_instance` or `DontDestroyOnLoad` usage in the hydraulic erosion domain files.
- String churn: scoped fixed-string scan found no `$"` interpolated strings. `.ToString()` remains only in editor cold JSON/file output, not in gameplay `Tick`, `FixedTick`, or `Update`.

### Verification Delta

- Passed: scoped source audit for `NativeArray`, `NativeMemorySentinel`, `.Complete()`, `Run()`, hot-loop names, `.ToString()`, `string.Format`, `_instance`, `DontDestroyOnLoad`, `PublishPerformanceWarning`, and `Debug.Log` completed with findings limited to cold/editor paths.
- Passed: scoped fixed-string `rg -F '$"'` audit returned no interpolated-string matches.
- Blocked: MCP `validate_script` retry returned `no_unity_session`; Unity Editor diagnostics were unavailable in this session.
- Blocked: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` exits `1` before a clean domain compile can be claimed. Current blockers: `Temp/obj/Crest/project.assets.json` missing (`NETSDK1004`) and dependent `Unity.RenderPipelines.Universal.Runtime.dll` metadata missing (`CS0006`).
- Blocked: Unity batchmode harness attempt wrote `CodexArtifacts/unity-erosion-harness-2026-05-05.log`, but compilation stopped before `Hecton8.Editor.ErosionTestHarness.Run` executed. Blocking errors in that log are `Assets/_Project/Scripts/Editor/AnomalyTestHarness.cs(90,24)` and `(96,24)` ambiguous `Assert.AreEqual`, plus `Assets/_Project/Scripts/HectonSurvivalSystem.cs(1132,60)` and `(1133,61)` missing `HectonPlayerMovement.CapsuleHeight`.
- Not found: batchmode log contains no compiler errors naming `ErosionHarnessJobs.cs`, `HydraulicErosionMetricsJob.cs`, `HectonHydraulicErosionMapMagicNode.cs`, `HydraulicErosionJob.cs`, or `HydraulicErosionSmokeTester.cs`.
- Artifact: `CodexArtifacts/2026-05-05_EROSION_OMEGA_DIFF.patch` contains the tracked diffs plus no-index diffs for new erosion smoke/metrics files and their Unity `.meta` files.
- Pending: `HydraulicErosionSmokeTester` execution and JSON artifact generation; blocked until Unity session/project compile is available.
