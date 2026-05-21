# VISOR AR Stencil Renderer

Owner: SHINOBU_270
Domain: ECHELON 8 Presentation & UX / Visor AR (HUD)

## Authority Boundary

`HectonVisorARStencilRendererFeature` is a visual-only URP RenderGraph renderer. It owns no gameplay truth. It reads immutable UI scalar snapshots, cached player pose, and the latest `ARWaypointOverlay` target AUP snapshot, then writes visual DTOs into UI-owned DataVault buffers. It does not poll legacy `GlobalSignals` during RenderGraph preparation; if the cached player AUP snapshot is unavailable, AR target rows are cleared and telemetry records the missing-AUP flag while HUD vitals continue rendering.

The following buffers are excluded from rollback/Merkle `StateRingBuffer` hashing:

- `BufferID 73180`: `VisorHudParamsDTO`
- `BufferID 73181`: `ARWaypointOverlay.StencilTargetSourceDTO`
- `BufferID 73182`: `VisorArTargetDTO`
- `BufferID 73183`: `VisorHudDigitParamsDTO`
- `BufferID 73184`: `VisorTelemetryEntry`
- `BufferID 73185`: `VisorHudProfileDTO`
- `BufferID 73186`: CSV scratch bytes

Rollback must not serialize, hash, or reconcile these buffers. They are "Dear Lie" presentation state only.

CSV profile hydration is editor/source-data only: `Assets/_SourceData/Visor/visor_hud_profiles.csv` may be parsed through the existing native scratch lane during editor cold setup. Player/runtime builds must not load human-readable visor profile data from `StreamingAssets`; production profile truth must arrive through a baked DataMonolith or domain `.h8bin` route.

`HectonVisorARStencilRendererFeature` owns the reference lifecycle for these visual descriptors. It releases all seven generation handles through `IDataVault.ReleaseBuffer(in handle)` on renderer disposal, DataVault service replacement, and cold service rebind before tombstoning local descriptors. It must not use `ReleaseOwnerBuffers(SystemID.UI)` because UI owns neighboring presentation lanes outside SHINOBU_270.

## Render Route

1. `SuitHUDPresentationController` defaults to `StencilRenderGraph`, but runtime Canvas suppression is owned by `HectonVisorARStencilRendererFeature`, not by the presentation controller.
2. Canvas projection-source and screen overlay paths are suppressed only after the RenderGraph feature validates Game/Base camera scope, strict cached player-camera ownership (`IPlayerRuntimeContext.PlayerCamera` reference equality), DataVault handles, mask mesh, frame upload, and the AR resolve `RecordRenderGraph` path actually creates the resolve pass and assigns `resourceData.cameraColor`. `AddRenderPasses` records only a pending frame token; `MarkStencilResolveRecorded` enables suppression after the graph record proof, and an `endCameraRendering` watchdog clears suppression on the same player-camera frame if compatibility/no-graph/drop conditions prevent the resolve from being recorded. If renderer preparation fails, the feature is absent, or `RecordRenderGraph` aborts on backbuffer/invalid target resources, the suppression flag remains/reverts false so legacy HUD can fail open instead of leaving the player blind. `SuitHUDV4CanvasOverlay` scene-load binding exits while the renderer-owned stencil flag is active, so it cannot auto-add a HUD Canvas binding during a proven runtime stencil presentation.
3. `ARWaypointOverlay` remains the waypoint service and AUP collector. In stencil mode it stops creating/mutating Canvas slots, publishes waypoint state during its tick, and the renderer copies only that latest snapshot. Vegetation bridge lookup is cold/bootstrap or registry hot-swap only, not Tick/RenderGraph polling.
4. Stencil pass draws the helmet-glass mask with `Hecton8/Visor/StencilMask`. The mask shader is ColorMask 0, Cull Off, ZWrite Off, and writes only the reserved stencil lane while using the depth attachment for test/ordering, so the generated fallback mesh cannot self-cull or dirty camera color/depth. The reserved SHINOBU_270 lane is stencil bit 0 (`WriteMask=1`, `ReadMask=1`); legacy serialized `255` writer masks are coerced to lane 1 until a wider stencil-lane reservation exists.
5. Fullscreen pass `Hidden/Hecton8/VisorAR` first copies the camera color to the resolve target, then draws AR digits, scanlines, fog, and compacted brackets only where stencil equals the configured visor reference. The AR shader read mask mirrors the resolved stencil write mask. HUD params, digit params, and the compacted 16-row target buffer are uploaded through double-buffered `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`; unused target rows are cleared with `UnsafeUtility.MemClear`. The shader loops only over the uploaded active target count, and low-quality chroma uses a `smoothstep` admission weight so survival-tier devices do not pay two dead aberration texture taps while the visible effect still ramps continuously as quality rises.
6. Non-finite projection faults dump a fixed 32-byte little-endian header followed by raw `VisorTelemetryEntry` rows to `Docs/AgentLogs/Dump_SHINOBU_270.bin`; over-budget frames remain telemetry flags and do not perform render-side disk I/O.
7. `HUDCanvasInquisition` is an editor-only proof facade. It upserts SHINOBU_270 evidence under `shinobu_270_visor_ar_stencil` in the shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` instead of overwriting neighboring agents' report objects. It also marks `generatedProjectStale=true` until generated `Hecton8.Core.csproj` includes both `HectonVisorARStencilRendererFeature.cs` and `HectonVisorStencilPreviewGizmo.cs`, preventing stale external `dotnet build` proof from being treated as SHINOBU_270 source coverage.
8. `HectonVisorStencilPreviewGizmo` is editor-fenced, uses a fixed three-row `stackalloc` span for target preview, and derives camera AUP from `HectonFloatingOrigin.CurrentTotalOffsetDouble` plus local camera position in double precision; it does not allocate a Temp `NativeArray` and does not use the legacy runtime-position bridge.
9. `Assets/_Project/Art/Shaders/Variants/Hecton_VisorAR_Stencil.shadervariants` is the cold shader warmup artifact for `Hidden/Hecton8/VisorAR` and `Hecton8/Visor/StencilMask`. `Assets/_Project/Scenes/00_BOOTSTRAP.unity` serializes this collection through `BootstrapController.shaderVariantCollections`, and `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync` warms it during the presentation/bootstrap prewarm phase before gameplay scene activation. The renderer feature does not call `ShaderVariantCollection.WarmUp()`.

## Layout Contract

`VisorHudParamsDTO` is exactly 64 bytes:

- offset 0: `float4 TargetCoordinates`
- offset 16: `float4 VitalStats`
- offset 32: `float4 VisorGlitchParams`
- offset 48: `float4 QualityAndTime`

`VisorARStencilContracts.ValidateLayouts()` is the editor/runtime proof gate.
