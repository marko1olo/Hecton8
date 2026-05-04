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

## Files Added

- `Assets/_Project/Scripts/World/HydraulicErosionJob.cs`
- `Assets/_Project/Scripts/World/ThermalSlumpingJob.cs`
- `Assets/_Project/Scripts/World/HectonHydraulicErosionMapMagicNode.cs`
- `Assets/_Project/Scripts/Editor/ErosionTestHarness.cs`

## Implementation Facts

- `HydraulicErosionJob` is a Burst `IJob` over a caller-owned `NativeArray<float>` heightmap.
- Droplets use deterministic hash RNG, momentum, sediment capacity, erosion, deposition, water evaporation, and final evaporation deposition.
- Dendritic channel enforcement is implemented by choosing the best spawn cell from multiple deterministic candidates scored by local depression plus existing wear-channel intensity.
- Local depression deposition fills the lowest cells in a 3x3 neighborhood toward a target height before fallback bilinear deposition, creating flat local sediment plains.
- `ThermalSlumpingJob` is a Burst `IJobParallelFor` that transfers material down slopes exceeding the critical talus angle.
- `HectonHydraulicErosionMapMagicNode` exposes a MapMagic height inlet and three outlets: eroded height, sediment mask, wear mask.
- The MapMagic node reserves a configurable 4-pixel default margin by spawning droplets in the core while allowing flow into the overlapped boundary.
- Sediment and wear outputs are normalized to strict `0.0..1.0` before MapMagic product storage.
- `ErosionTestHarness` generates a 512x512 fractal heightmap, runs erosion plus slumping, and writes PNG outputs under `CodexArtifacts/`.

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
- Memory: all new native containers in the node and harness use `Allocator.TempJob`, complete before disposal, and do not persist across frames.
- Cadence: the only `.Complete()` calls are annotated blocking generation/editor harness sync points. No gameplay Tick owner was added.
- Correctness: boundary bleed is implemented as a 4-pixel overlapped processing margin. True cross-tile validation requires MapMagic graph execution on adjacent chunks.

## Verification

- Pending: Unity script import/compile.
- Pending: MapMagic node graph execution.
- Pending: editor harness PNG generation.
- Pending: GCMonitor/profiler capture.
