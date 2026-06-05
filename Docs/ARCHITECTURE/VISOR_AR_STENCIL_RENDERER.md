# VISOR AR Stencil Renderer

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Owner: SHINOBU_270
Owner domain: Echelon 8 Presentation & UX / Visor AR
Domain: ECHELON 8 Presentation & UX / Visor AR (HUD)
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

## Authority Boundary

- `HectonVisorARStencilRendererFeature` is a visual-only URP RenderGraph renderer.
- It owns no gameplay truth.
- It reads immutable UI scalar snapshots, cached player pose, and the latest `ARWaypointOverlay` target AUP snapshot, then writes visual DTOs into UI-owned DataVault buffers.
- RenderGraph preparation does not poll legacy `GlobalSignals`.
- If cached player AUP snapshot is unavailable, AR target rows are cleared.
- Telemetry records missing-AUP flag.
- HUD vitals continue rendering.

The following buffers are excluded from rollback/Merkle `StateRingBuffer` hashing:

- `BufferID 73180`: `VisorHudParamsDTO`
- `BufferID 73181`: `ARWaypointOverlay.StencilTargetSourceDTO`
- `BufferID 73182`: `VisorArTargetDTO`
- `BufferID 73183`: `VisorHudDigitParamsDTO`
- `BufferID 73184`: `VisorTelemetryEntry`
- `BufferID 73185`: `VisorHudProfileDTO`
- `BufferID 73186`: CSV scratch bytes

Rollback must not serialize, hash, or reconcile these buffers. They are "Dear Lie" presentation state only.

CSV profile hydration is editor/source-data only.

`Assets/_SourceData/Visor/visor_hud_profiles.csv` may be parsed through existing native scratch lane during editor cold setup.

Player/runtime builds must not load human-readable visor profiles from `StreamingAssets`; production truth must arrive through baked DataMonolith or domain `.h8bin`.

`HectonVisorARStencilRendererFeature` owns visual-descriptor lifecycle.

- It releases all seven generation handles through `IDataVault.ReleaseBuffer(in handle)`.
- Release points: renderer disposal, DataVault replacement, cold rebind.
- Local descriptors are tombstoned after release.
- It must not use `ReleaseOwnerBuffers(SystemID.UI)`.
- UI owns neighboring lanes outside SHINOBU_270.

## 2026-06-05 Adjacent Visor Source Anchors

Evidence class: STATIC_SOURCE / STATIC_DOC only. This addendum does not change SHINOBU_270 ownership and does not prove Unity import, RenderGraph execution, GPU timing, GC, or visual quality.

- `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` is an adjacent visor spectrum/sonar owner, not SHINOBU_270 AR stencil truth. It owns bounded presentation queues for spectrum mode, sonar pulse/ping, spatial sonar snapshots, acoustic echo returns, and ping-return signals; consumes `SignalBus<AcousticPingSignal>` for active sonar geo presentation; and writes active-sonar geo telemetry under `SystemID.UI`. Stencil AR may consume only stable owner-published sensor/UI facts, not poll Spectrum internals.
- `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs` is an adjacent fullscreen visor fluid distortion renderer, not AR target, route, or HUD-vital authority. Its RenderGraph pass reads camera color/depth/opaque texture, optional lens mask, and constant buffers, then writes the camera color replacement for visor wetness/leak refraction. It is a presentation approximation boundary over cached wet-lens, hull-stress, rain, water-density, and quality fields. It must not own gameplay water truth, damage truth, objective truth, or AR waypoint truth. Required proof remains Frame Debugger/RenderGraph Viewer, profiler/GC, renderer asset binding, and compact/high readability captures.

## Render Route

- 1. `SuitHUDPresentationController` defaults to `StencilRenderGraph`, but runtime Canvas suppression is owned by `HectonVisorARStencilRendererFeature`, not by the presentation controller.
- 2.
- Canvas projection-source and screen overlay paths suppress only after all gates pass.
- Gates: Game/Base camera scope, cached player-camera ownership, DataVault handles, mask mesh, frame upload.
- AR resolve gate: `RecordRenderGraph` creates the resolve pass and assigns `resourceData.cameraColor`.
- `AddRenderPasses` records only a pending frame token.
- `MarkStencilResolveRecorded` enables suppression after graph record proof.
- `endCameraRendering` watchdog clears suppression when an authorized player-camera frame reaches end event without same-frame resolve.
- If renderer preparation fails, suppression flag remains/reverts false.
- Same fallback if feature is absent.
- Same fallback if `RecordRenderGraph` aborts on backbuffer/invalid target resources.
- Legacy HUD can fail open instead of leaving player blind.
- `SuitHUDV4CanvasOverlay` scene-load binding exits while the renderer-owned stencil flag is active, so it cannot auto-add a HUD Canvas binding during a proven runtime stencil presentation.
- 3.
- `ARWaypointOverlay` remains the waypoint service and AUP collector.
- It no longer carries `EmergencyServiceRelayDirector` or `HectonMapMagicVegetationBridge` fields, hot-swap casts, `GlobalRegistry` provider reads, or relay/anchor collection.
- Until Emergency/Vegetation owners publish a contract-backed relay/anchor snapshot, stencil waypoints consume only externally registered cached AUP rows.
- External `Transform` and stored presentation-position waypoints are visual-only in stencil mode.
- Capture to AUP occurs only at waypoint registration, stencil transition, or legacy external-waypoint cadence.
- Active stencil `Tick`/`SlowTick` reads cached AUP validity only; no `target.position` or camera `Transform.position` reads.
- The renderer subtracts camera AUP again before float projection.
- This path is not gameplay authority and must not be used for rollback or save truth.
- 4. Stencil waypoint occlusion:
  - bounded Dear Lie, not physics/HZB truth;
  - `SlowTick` marks at most `16` active waypoint rows;
  - tests use camera-relative cone and distance in AUP-local float space;
  - shader dims/brackets rows through `ShapeParams.y`;
  - complexity: `O(n)` for `n <= 16`;
  - rejected: PhysX raycasts, MeshColliders, scene renderers, synchronous GPU HZB readback.
- 5. Stencil mask pass:
  - shader: `Hecton8/Visor/StencilMask`;
  - state: ColorMask `0`, Cull Off, ZWrite Off;
  - writes only reserved stencil lane;
  - uses depth attachment for test/ordering;
  - fallback mesh cannot self-cull or dirty camera color/depth;
  - SHINOBU_270 lane: stencil bit `0`, `Ref 1`, `WriteMask 1`;
  - runtime code does not mutate stencil material properties.
- 6.
- Fullscreen pass `Hidden/Hecton8/VisorAR` copies camera color to the resolve target.
- It draws AR digits, scanlines, fog, and brackets only where stencil equals the reserved visor lane.
- The AR shader uses hard-coded `Ref 1` and `ReadMask 1`.
- HUD params, digit params, and the compacted 16-row target buffer are uploaded through double-buffered `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`; unused target rows are cleared with `UnsafeUtility.MemClear`.
- The shader loops only over the uploaded active target count.
- Chroma uses a branchless `smoothstep` admission weight: survival-tier quality collapses chroma contribution toward neutral, middle/high quality ramps continuously, and no binary quality branch selects a shader path.
- 7. Non-finite projection faults dump a 32-byte little-endian header plus raw `VisorTelemetryEntry` rows to `Docs/AgentLogs/Dump_SHINOBU_270.bin`.
- Over-budget frames remain telemetry flags and do not perform render-side disk I/O.
- 8.
- `HUDCanvasInquisition` is an editor-only proof facade.
- It upserts SHINOBU_270 evidence under `shinobu_270_visor_ar_stencil`.
- Shared report: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- It does not overwrite neighboring report objects.
- It removes stale legacy root-level SHINOBU_270 fields.
- Generated section includes generated-project evidence, fail-open proof, fixed stencil bit proof, waypoint fake proof, Vault IDs, and compile-gate status.
- It also marks `generatedProjectStale=true` until generated `Hecton8.Core.csproj` includes both `HectonVisorARStencilRendererFeature.cs` and `HectonVisorStencilPreviewGizmo.cs`, preventing stale external `dotnet build` proof from being treated as SHINOBU_270 source coverage.
- 9. `HectonVisorStencilPreviewGizmo` is editor-fenced and uses fixed three-row `stackalloc` span for target preview.
- Camera AUP derives from `HectonFloatingOrigin.CurrentTotalOffsetDouble` plus local camera position in double precision.
- It does not allocate Temp `NativeArray` or use legacy runtime-position bridge.
- 10. Shader warmup:
  - artifact: `Assets/_Project/Art/Shaders/Variants/Hecton_VisorAR_Stencil.shadervariants`;
  - shaders: `Hidden/Hecton8/VisorAR`, `Hecton8/Visor/StencilMask`;
  - bootstrap scene: `Assets/_Project/Scenes/00_BOOTSTRAP.unity`;
  - serialized field: `BootstrapController.shaderVariantCollections`;
  - warm path: `GameBootstrapper.WarmConfiguredShaderVariantCollectionsAsync`;
  - phase: presentation/bootstrap prewarm before gameplay scene activation;
  - renderer feature does not call `ShaderVariantCollection.WarmUp()`;
  - Unity import/player first-use stutter proof remains pending.

## Layout Contract

`VisorHudParamsDTO` is exactly 64 bytes:

- offset 0: `float4 TargetCoordinates`
- offset 16: `float4 VitalStats`
- offset 32: `float4 VisorGlitchParams`
- offset 48: `float4 QualityAndTime`

- `VisorARStencilContracts.ValidateLayouts()` is the editor/runtime proof gate.
- Runtime checks enforce final byte size for every SHINOBU_270 route DTO.
- Editor checks verify field offsets through `UnsafeUtility.GetFieldOffset`.
- Offset DTOs: `VisorHudParamsDTO`, `VisorArTargetDTO`, `VisorHudDigitParamsDTO`, `VisorTelemetryEntry`, `VisorHudProfileDTO`.
- Extra offset DTO: `ARWaypointOverlay.StencilTargetSourceDTO`.
