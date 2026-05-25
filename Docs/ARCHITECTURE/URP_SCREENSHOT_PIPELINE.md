# URP Screenshot Pipeline

Date: 2026-05-07

Status: PENDING VERIFICATION

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## Historical 2026-05-04 Boundary

- Evidence limit: save-thumbnail/URP contract only; renderer assignment, active-camera pass, and thumbnail persistence remain unproven.

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

- `SaveThumbnailCaptureFeature` owns the URP render hook.
- It executes inside renderer at frame end.
- It requests GPU readback from render target produced by active camera.

## Frame Flow

1. Save flow calls `SaveThumbnailSystem.CaptureThumbnail(slotName, overrideCamera)`.

2. `SaveThumbnailSystem` stores one pending request bound to the resolved active player camera.

3. During that camera's next URP render, `SaveThumbnailCaptureFeature.RecordRenderGraph(...)` detects the request.

4. The pass copies the declared camera color `TextureHandle` into a dedicated thumbnail `TextureHandle` through a RenderGraph-safe raster/blit pass. This wording does not approve `Graphics.Blit`, `CommandBuffer.Blit`, or URP Compatibility Mode routing.

5. The pass requests `AsyncGPUReadback` for that RT.

6. The callback in `SaveThumbnailSystem` receives the GPU data after the GPU finishes the copy.

7. The callback encodes the raw RGBA bytes to PNG.

8. `PersistThumbnailAsync(...)` switches to a background thread and writes the `.tmp` file.

9. The old thumbnail is replaced atomically by deleting the old file and moving the new `.tmp` into place.

## Why `AsyncGPUReadback` Prevents CPU Stalls

A synchronous readback forces CPU wait until GPU finishes rendering and copies requested pixels.

That wait is a hard stall. On constrained hardware it produces visible hitching exactly when user saves.

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

// RenderGraph pass body copies the declared camera color handle into the

// thumbnail handle; no Graphics.Blit or CommandBuffer.Blit route is approved.

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
