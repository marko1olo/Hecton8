# Unity Crest Native PNG Trap - UNKNOWN - 2026-05-26

Status: source fixed, compile reclosed.

## Verdict

`HectonCrestOceanDepthCacheRuntimeBridge` had a real but narrow Unity API trap
in the development/editor depth-cache forensic dump path:

- `AsyncGPUReadback` produced a `NativeArray<Color32>`.
- The old route created a temporary `Texture2D`.
- It copied readback pixels into that texture with `SetPixelData`.
- It called `Texture2D.EncodeToPNG()`, producing a managed `byte[]`.
- It wrote the managed array through `File.WriteAllBytes`.

This was not a gameplay hot path. It is guarded by
`UNITY_EDITOR || DEVELOPMENT_BUILD` and by the existing
`HectonRuntimeDepthCacheCameraDisabled` route. It was still worth fixing
because Unity already exposes a native-array PNG encoder, and this code is a
project-owned Crest adapter rather than a vendor package file.

## Sources Checked

- Unity `ImageConversion`: https://docs.unity.cn/ScriptReference/ImageConversion.html
- Unity `AsyncGPUReadbackRequest.GetData`: https://docs.unity.cn/ScriptReference/Rendering.AsyncGPUReadbackRequest.GetData.html
- Unity `NativeArray<T>`: https://docs.unity.cn/6000.0/Documentation/ScriptReference/Unity.Collections.NativeArray_1.html

The relevant official boundary is simple: successful async GPU readback exposes
data as `NativeArray<T>`, and `ImageConversion` exposes
`EncodeNativeArrayToPNG`. The fix keeps that route instead of allocating a
temporary Unity texture object and a managed PNG byte array.

## Local Static Scan

Targeted scan:

```powershell
rg -n "EncodeToPNG|new Texture2D|SetPixelData|EncodeNativeArrayToPNG|NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr|AsyncGPUReadback" Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs
```

Current result in the target file:

- `AsyncGPUReadback.Request(...)` remains.
- `ImageConversion.EncodeNativeArrayToPNG(...)` is used.
- `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(...)` is used only to stream
  the encoded native bytes.
- No `Texture2D` roundtrip remains in this file.
- No `Texture2D.EncodeToPNG()` remains in this file.

## Source Change

Changed file:

- `Assets/_Project/Scripts/Plugins/Crest/HectonCrestOceanDepthCacheRuntimeBridge.cs`

Removed:

- temporary `Texture2D` allocation;
- `Texture2D.SetPixelData(...)` on the temporary texture;
- `Texture2D.Apply(...)`;
- `Texture2D.EncodeToPNG()`;
- managed `byte[]` PNG staging;
- `UnityEngine.Object.DestroyImmediate(...)` for the temporary texture.

Added:

- `NativeArray<Color32> readbackPixels = request.GetData<Color32>()`;
- `ImageConversion.EncodeNativeArrayToPNG(...)`;
- explicit `NativeArray<byte>` disposal in `finally`;
- `FileStream.Write(ReadOnlySpan<byte>)` from the encoded native buffer pointer.

## Vegetation Compile Wall Encountered

The first legal build after the Crest patch did not fail in Crest. It exposed
active vegetation native-handle migration drift:

- `BUILD_UNKNOWN_CREST_NATIVE_PNG_RECHECK_20260526.log`: failed with `126`
  errors and `0` warnings. Dominant class: removed `NativeChunkPool` direct
  fields such as `Matrices`, `Metadata`, `Types`, `SemanticTypes`,
  `BiomeLayers`, `EdgeDistances`, `FlowDirections`, and `FlowVectors`.
- `BUILD_UNKNOWN_CREST_NATIVE_PNG_VEGETATION_POOL_RECHECK2_20260526.log`:
  failed with `25` errors and `0` warnings after the first repair batch.

Repair direction was to keep the DataVault-handle migration, not restore old
persistent native aliases:

- chunk-pool readers now go through `TryReadChunkPoolView(...)`;
- chunk-pool writers go through `TryAcquireChunkPoolWriteView(...)`;
- write locks are released through `ReleaseChunkPoolWriteLocks(...)`;
- `VegetationChunkResidencyDirector.WriteJobRecordsToPool(...)` writes through
  local `NativeArray<T>` handles acquired from the locked view;
- `VegetationDensityQueryService`, `VegetationNavGridSynchronizer`, and
  `VegetationTerrainHoleSynchronizer` no longer use the old direct
  `pool.Matrices` / `pool.Metadata` / `pool.SemanticTypes` style routes.

Static stale-field scan after the repair:

```powershell
rg -n "CountValidJobRecords|pool\.(Matrices|Metadata|Types|SemanticTypes|BiomeLayers|EdgeDistances|FlowDirections|FlowVectors)\b|_surfaceAggregateFrontBuffers\.(FlowDirections|FlowVectors)\b|underwaterPool\.(Matrices|BiomeLayers|SemanticTypes|FlowVectors)\b" Assets/_Project/Scripts/World
```

Scoped world files involved in this compile wall now have no hits for those
stale routes.

## Validation

Static:

- scoped Crest scan shows `EncodeNativeArrayToPNG` and no `EncodeToPNG` in the
  target adapter file;
- scoped vegetation stale-field scan returns no hits for the old direct pool
  fields in the repaired files;
- scoped `git diff --check` passed with line-ending warnings only.

Build:

- final guarded full-solution CLI build:
  `Docs/Reports/BUILD_UNKNOWN_CREST_NATIVE_PNG_POOL_RECHECK3_20260526.log`;
- command:
  `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`;
- exit `0`;
- `Build succeeded.`;
- `0 Warning(s)`;
- `0 Error(s)`.

Runtime proof:

- Not claimed.
- No Unity Editor import, Console, PlayMode, player build, profiler,
  GCMonitor, shader-variant, scene wiring, visual, or platform gate was run.

## Residual

- The PNG path is still a forensic file-output path and still allocates native
  encoded PNG memory because PNG encoding must produce a file payload.
- No measured frame-time gain is claimed because this is not a steady gameplay
  loop.
- Vendor Crest internals were not modified.

## Hardware Impact

Measured microseconds saved: `0`.

Expected static benefit:

- one temporary `Texture2D` object removed per depth-cache forensic PNG dump;
- one managed PNG `byte[]` staging buffer removed from this route;
- one extra texture object lifecycle removed;
- data stays on the native-array route after async GPU readback.

Low tier: less editor/development diagnostic allocation pressure.
Middle tier: same diagnostic capability with lower managed churn.
High tier: same output quality; lower temporary object churn.
Ultra tier: no visual cap removed; this is evidence/diagnostic hygiene, not
visual-fidelity work.
