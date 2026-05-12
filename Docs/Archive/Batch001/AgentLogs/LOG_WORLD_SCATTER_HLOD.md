# WORLD_SCATTER_HLOD Final Report - 2026-05-11

Status: PENDING VERIFICATION
Agent: FOVEATED_CULLING_MASTER
Domain: Echelon 2 World Generation & Terrain - BRG Scatter Director

## What Was Wrong
- Scatter culling could spend height sampling, transforms, normal work, and draw submission on candidates that were far, peripheral, sub-pixel, hidden behind terrain, or otherwise invisible.
- The indirect path still carried managed upload risk for draw args and lacked the requested density export, atlas data, foveated cache ownership, and explicit compaction barrier.
- Compile health could not be proven clean because the project has unrelated existing failures outside the scatter domain.

## What Was Done
- Implemented squared-distance early rejects at the top of `GenerateScatterInstances`, then retained squared full-distance, squared normal-Y slope, dithered far radius, peripheral-dot, 4x4 projected-pixel, frustum, Hi-Z, and cave rejection before visibility cache write.
- Added foveated visibility cadence: outside the center mask, instances update every fourth frame unless the camera/field moved enough to force a full refresh. Compaction is now the only visible-index append owner.
- Added Hi-Z occlusion against a previous-frame depth pyramid using projected 8-corner bounds rects and a precomputed species bounds LUT.
- Kept rendering on `Graphics.RenderMeshIndirect`; removed managed indirect-args array upload by writing args through `GraphicsBuffer.LockBufferForWrite`.
- Added a 64-bin GPU-authored sargassum density buffer export via `TryGetSargassumDragDensityBuffer`.
- Added per-instance atlas scale/offset and sine-parabola vertex sway modulated by Abyssal flow magnitude.
- Kept mod matrix staging on `NativeArrayOptions.UninitializedMemory` and retained CPU upload of `_HectonScatterMinNormalYSq`.

## Cinematic Cheats Used
- Exact distance became `dot(diff, diff)`.
- Slope angle/trig became the constant normal-Y gate `0.8660254f` and squared comparison.
- Hard far clipping became deterministic blue-noise-style evaporation.
- Physical plant motion became vertex sine-parabola sway.
- CPU vegetation drag rebuild became a 1D GPU density buffer.
- Runtime mesh bounds reads became a fixed 16-entry bounds LUT.

## HLSL Evidence - Early Squared-Distance Reject
```hlsl
float2 planarCameraDelta = worldXZ - _HectonScatterCameraPosition.xz;
float planarDistanceSq = dot(planarCameraDelta, planarCameraDelta);
if (!PassesDitheredRadiusCull(planarDistanceSq, baseHash ^ 0x31415927u, uint2(cellX, cellZ)))
{
    _HectonScatterVisibilityCache[instanceIndex] = 0u;
    return;
}
```

## HLSL Evidence - Foveated Mask
```hlsl
uint ResolveFoveatedUpdateMask(float2 uv, uint instanceIndex)
{
    float2 centerDelta = uv - 0.5;
    uint outsideFovea = dot(centerDelta, centerDelta) > _HectonScatterFoveatedParams.x ? 1u : 0u;
    uint cadenceMiss = (((uint)_HectonScatterFoveatedParams.y + instanceIndex) & 3u) != 0u ? 1u : 0u;
    uint forceFull = (uint)min(max(_HectonScatterFoveatedParams.z, 0.0), 1.0);
    return 1u ^ (outsideFovea & cadenceMiss & (1u - forceFull));
}
```

## Scalability Matrix
- Low/MX350: four-frame quadrant updates, foveated cache reuse, >=2px tiny-instance reject, squared-distance gates, dithered radius fade, zero CPU visibility lists.
- Mid: same indirect pipeline, less severe visible-density loss, stable sine-parabola sway and atlas variation.
- High/Ultra: lower projected-pixel cutoff, denser visible scatter, longer cull radius potential, richer atlas/sway use bought with saved submission and overdraw budget.

## Exact Microseconds Saved
- Squared-distance and early reject before height/clip/normal: 115-270us per 16k candidates on MX350-class GPU, PENDING VERIFICATION.
- Foveated cache plus quadrant staggering: 300-810us GPU spike reduction in peripheral scatter fields, PENDING VERIFICATION.
- Indirect rendering and native args writes: 205-720us CPU submission/upload risk removed, PENDING VERIFICATION.
- Hi-Z and 4x4 pixel rejection: 150-650us fragment/vertex overdraw reduction in blocked or distant fields, PENDING VERIFICATION.
- Bounds LUT, uninitialized staging, density GPU export, atlas path: 35-120us CPU/GC risk removed plus material churn reduction, PENDING VERIFICATION.

## Verification
- `git diff --check` passed for modified scatter files. Only line-ending warnings were reported.
- Forbidden-symbol scan of modified scatter files found no `SetData`, `ClearMemory`, `DrawMeshInstanced`, `Object.Instantiate`, `length(`, `distance(`, `Mathf.Acos`, `Mathf.Sqrt`, `Vector3.Distance`, or `.magnitude` regressions.
- Zero-GC polish scan found only cold setup allocations or value-type `new` calls in modified scatter code.
- `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` failed outside scatter at `Assets/_Project/Scripts/ConstructionManager.cs(40,208)`: `ConstructionManager` does not implement `IOriginShiftListener.OnOriginShift(in OriginShiftEventData)`. Build also reported 48 warnings. No scatter-owned compile error was reported.

## Final Git Diff
```text
Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute | 276 ++++++++++-
Assets/_Project/Art/Shaders/Hecton_ScatterIndirectLit.shader | 29 +-
Assets/_Project/Scripts/World/GPUScatterDirector.cs | 518 ++++++++++++++++++++-
Assets/_Project/Scripts/World/ScatterGPUIBackend.cs | 11 +-
4 files changed, 834 insertions(+), 34 deletions(-)
```
