# URP Screenshot Pipeline
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this pipeline as current runtime truth.
- This document is a save-thumbnail/URP contract, not proof that the renderer feature is assigned, the active camera renders the pass, or thumbnail persistence is runtime-validated.
- Re-open `SaveThumbnailSystem`, `SaveThumbnailCaptureFeature`, renderer assets, and current console evidence before surgery.

## Purpose

`SaveThumbnailSystem` must capture save-slot thumbnails without breaking the URP camera stack, without forcing a synchronous GPU readback, and without blocking the main thread on image encoding or disk I/O.

## Why `Camera.Render()` Is Forbidden

`Camera.Render()` is a built-in pipeline era escape hatch. In Unity 6 URP with RenderGraph enabled, forcing a manual render from gameplay code is wrong for four reasons:

1. It bypasses the active URP frame graph.
2. It ignores renderer-feature ordering and camera-stack state.
3. It risks double-rendering or rendering from an incomplete camera state.
4. It encourages synchronous CPU-side readback patterns such as `ReadPixels`, which stall the frame on weak hardware.

For HECTON-8 this is unacceptable. MX350-class hardware cannot afford an out-of-band render path every time the user saves.

## Current Ownership

The screenshot path is split across two owners:

1. `SaveThumbnailSystem`
2. `SaveThumbnailCaptureFeature`

`SaveThumbnailSystem` owns request orchestration, cache invalidation, PNG persistence, and disk lifetime.

`SaveThumbnailCaptureFeature` owns the URP render hook. It executes inside the renderer at the end of the frame and requests the GPU readback from the render target that was produced by the active camera.

## Frame Flow

1. Save flow calls `SaveThumbnailSystem.CaptureThumbnail(slotName, overrideCamera)`.
2. `SaveThumbnailSystem` stores one pending request bound to the resolved active player camera.
3. During that camera's next URP render, `SaveThumbnailCaptureFeature.RecordRenderGraph(...)` detects the request.
4. The pass blits the current camera color target into a dedicated thumbnail RT.
5. The pass requests `AsyncGPUReadback` for that RT.
6. The callback in `SaveThumbnailSystem` receives the GPU data after the GPU finishes the copy.
7. The callback encodes the raw RGBA bytes to PNG.
8. `PersistThumbnailAsync(...)` switches to a background thread and writes the `.tmp` file.
9. The old thumbnail is replaced atomically by deleting the old file and moving the new `.tmp` into place.

## Why `AsyncGPUReadback` Prevents CPU Stalls

A synchronous readback forces the CPU to wait until the GPU has finished rendering the frame and copied the requested pixels. That wait is a hard stall. On constrained hardware it produces visible hitching exactly when the user is saving.

`AsyncGPUReadback` avoids that by decoupling request submission from result consumption:

1. The render pass asks the GPU to copy the thumbnail target.
2. The frame continues.
3. The callback runs only when the GPU has finished the copy.
4. No main-thread polling loop or blocking `ReadPixels` path is required.

This does not make the transfer free. It removes the forced CPU wait from the save hot path.

## Callback Logic

Current callback ownership lives in `SaveThumbnailSystem.HandleReadbackCompleted(AsyncGPUReadbackRequest request)`.

```csharp
private static void HandleReadbackCompleted(AsyncGPUReadbackRequest request)
{
    if (!_hasInflightRequest)
        return;

    CaptureRequest inflightRequest = _inflightRequest;
    _inflightRequest = default;
    _hasInflightRequest = false;

    if (request.hasError)
    {
        Debug.LogError($"[SaveThumbnailSystem] AsyncGPUReadback failed for '{inflightRequest.SlotName}'.");
        return;
    }

    NativeArray<byte> encodedPng = ImageConversion.EncodeNativeArrayToPNG<byte>(
        request.GetData<byte>(),
        GraphicsFormat.R8G8B8A8_SRGB,
        (uint)Width,
        (uint)Height,
        0u);

    NativeArray<byte> persistentPng = new NativeArray<byte>(
        encodedPng.Length,
        Allocator.Persistent,
        NativeArrayOptions.UninitializedMemory);

    NativeArray<byte>.Copy(encodedPng, persistentPng, encodedPng.Length);
    encodedPng.Dispose();
    _ = PersistThumbnailAsync(inflightRequest.SlotName, persistentPng);
}
```

## RenderGraph Hook

The renderer feature performs the readback request from inside the URP frame:

```csharp
Blitter.BlitCameraTexture(cmd, data.source, data.destination, 0f, true);
cmd.RequestAsyncReadback(
    data.destinationHandle.rt,
    0,
    GraphicsFormat.R8G8B8A8_SRGB,
    SaveThumbnailSystem.ReadbackCompletedCallback);
```

## Main-Thread Rules

Forbidden in the save-thumbnail path:

1. `Camera.Render()`
2. `Texture2D.ReadPixels(...)`
3. `Texture2D.EncodeToJPG()` on the main thread
4. `File.WriteAllBytesAsync(...)` used as fake async while still building image data on the main thread

Required in the save-thumbnail path:

1. Render inside URP through a renderer feature / render pass
2. Pull frame data through `AsyncGPUReadback`
3. Keep native image bytes alive until background-thread persistence completes
4. Replace thumbnails atomically through temp-file promotion

## Regression Risks

1. If the renderer asset loses `SaveThumbnailCaptureFeature`, captures silently stop.
2. If the request camera never renders, the thumbnail remains pending.
3. If future code swaps the thumbnail RT format, the PNG encode format must be updated to match.
4. If multiple save requests are queued before the camera renders, only the latest pending request is retained by the current single-request design.

Status: PENDING VERIFICATION
