# Screen-Space Visor Wounds - SHINOBU_275

Owner: Echelon 8 Presentation & UX / Screen-Space Wounds & Decals

Route:
- `SignalBus<CombatDamageSignal>` and `SignalBus<HighSpeedImpactSignal>` are read as unmanaged frame snapshots by `DynamicDecalVaultRuntime`.
- Impact AUP `double3` is localized by subtracting the cached camera/player AUP before casting to `float3`.
- Camera/runtime-position localization uses the retained read-only `GlobalSignals.CurrentRuntimeOriginAup()` / `TryRuntimePositionToAup()` AUP bridge only. It is not a damage signal lane, does not publish direct queues, and fails to cached player/current-origin fallback before marking non-finite matrix telemetry.
- `VisorDecalDTO` is stored in `GlobalDataVault` as an explicit 80-byte unmanaged record: matrix 0, `DecalTypeHash` 64, `Opacity01` 68, `BirthTime` 72, `Flags` 76.
- `DecalTypeHash` uses low nibble for wound type, bits 4..7 for atlas slice, and bits 8..23 for packed profile/request lifetime centiseconds. This preserves the XML-mandated `BirthTime@72` ABI without losing CSV-tuned decay or profile atlas selection.
- Burst jobs generate matrices, decay opacity, compact active records, and copy them into a double-buffered `GraphicsBuffer` from dispatcher `LateFrameTick`.
- `DeferredDecalPass` binds `_GlobalVisorWounds` / `_GlobalVisorWoundCount` and runs one RenderGraph fullscreen pass using `Hecton_VisorWounds.shader`.
- GPU uploads are staged for the subsequent frame. `AddRenderPasses()` only publishes the prior staged buffer and captures camera context; signal ingestion, Vault mutation, job scheduling, and upload telemetry are kept out of `RecordRenderGraph()`.
- `RecordRenderGraph()` imports the published `GraphicsBuffer` as a RenderGraph `BufferHandle`, declares `UseBuffer(Read)`, declares source/depth texture reads, and binds globals inside a raster render func.
- Runtime public wound ingress (`TryEnqueueRuntimeImpact` / `TryEnqueueAupImpact`) fails closed unless cold storage is already created by feature `Create()` or DataVault hot-swap rebind. It cannot poll `GlobalRegistry`, allocate/prewarm the request queue, or request Vault handles from a damage producer call.
- Public/mock wound ingress fails closed while a visual-sync job is pending, increments dropped-ingress telemetry, and does not query or enqueue into the `NativeQueue` during the scheduled dequeue window.
- Signal-snapshot ingestion resolves the material-profile Vault array and live tuning DTO once per owner visual-sync pass, then reuses those immutable snapshot values for all high-speed and combat signals in that pass.
- Existing visor noir postprocessing consumes the same visual language through `Hecton_VisorGlitchACES.shader`: torn-edge serration and procedural crack masks are active in the serialized PC renderer route.
- Noir integration is pre-tonemap. URP Volume Tonemapping owns final ACES; the active Noir shader performs grade/glitch/crack shaping without a local fragment tonemap curve or `saturate(color)` HDR clamp.
- Active Noir constant generation/upload is dispatcher-owned through `HectonVisorUberPostFeature.LateFrameTick`; `AddRenderPasses()` only checks the last valid `GraphicsBuffer` and enqueues the RenderGraph pass. The one-row mock/parameter math is direct scalar code, not tiny `IJob.Run()`.
- Reconstruction constant publication uses A/B `GraphicsBuffer.Target.Constant` targets and a published active buffer. `AddRenderPasses()` only stages camera/runtime inputs and consumes the last active buffer; dispatcher `LateFrameTick()` writes changed constants into the next mapped buffer, mirrors constants into Vault, records telemetry, and owns any black-box dump.
- Visor post scalar/vector/texture state and wound atlas state are carried by RenderGraph pass data and bound inside raster render functions with `RasterCommandBuffer.SetGlobal*`. Loop 22 verified texture binding also uses command-buffer globals (`SetGlobalTexture`) instead of `Material.SetTexture`; the owned visor post shaders no longer rely on `UnityPerMaterial` or material mutation for trauma constants.
- Loop 28 corrected the active disk state: wound atlas, crack, lens dirt, blue noise, and VR comfort textures are all bound with `RasterCommandBuffer.SetGlobalTexture`; no owned `Material.SetTexture` call or stale string-name texture binding constant remains in `DeferredDecalPass` or `HectonVisorUberPostFeature`.
- `HectonVisorUberPost.shader` and `Hecton_BilateralUpsample.shader` consume dispatcher-published visual time globals (`_HectonUberVisualTime`, `_H8UberNoirVisualTime`) instead of engine `_Time`.
- Reconstruction aesthetic CSV rows are loaded only from cold create/DataVault hot-swap lanes, then copied into a fixed 32-row cold cache. Render enqueue selects profiles from that snapshot and does not lock the profile Vault buffer or retry file IO.
- Noir color CSV rows are also copied into a fixed cold 32-row snapshot; LateFrame profile selection does not resolve the Noir profile Vault array on cache misses.
- Shared visor host state no longer calls `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` from render enqueue. It consumes the cached `IPlayerRuntimeContext` snapshot route for player camera, survival status, and movement stress; wet-lens scalar remains a presentation read from the cached movement owner. The touched host file no longer imports `Hecton8.Gameplay`.
- The touched shared host no longer imports `Hecton8.Physics`, caches `HectonFluidEngine`, or subscribes to `GlobalRegistryServiceSlot.FluidRuntime` for Noir/wound visuals. The removed concrete maelstrom warp is replaced by a pressure/stress screen-space surge scalar derived from existing presentation inputs until a contracts-only fluid read model exists.
- The live matrix debug view is editor-owned by `ScreenSpaceDecalTunerWindow` through `SceneView.duringSceneGui`; `DynamicDecalGizmoVisualizer` is compiled only under `UNITY_EDITOR`, so player builds do not carry a scene-component proof surface.
- `ScreenSpaceDecalTunerWindow` exposes the designer bridge facts for visor wound material profiles: source CSV path, schema id/hash, runtime Vault route, current DataMonolith output caveat, last validation state, row count, header hash, and ABI summaries for `VisorDecalDTO` / `DecalMaterialProfileDTO`. Schema mismatch fails before the cold Vault CSV load.
- Diagnostic `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry` calls return immutable owner-phase snapshots. They do not lock Vault buffers, resolve native arrays, complete jobs, allocate, or mutate global lock state.
- `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead` is also compiled only under `UNITY_EDITOR`; it is an explicit acquire/release debug lane for SceneView gizmos and is not available to player runtime callers.
- Runtime state pointer access in `ExecuteVisualSync()` is fail-closed. A stale or invalid one-row Vault state buffer marks the existing layout/fault telemetry bit and returns false instead of throwing a managed gameplay exception.
- Cold initialization seeds the one-row runtime state before VISUAL_SYNC and requests visual/profile Vault buffers with clear memory. The first normal visual-sync frame does not run a direct `ClearDecalsJob.Execute(i)` loop; the fallback path uses `UnsafeUtility.MemClear` only when cold state is missing.
- Reconstruction constants Vault mirror is clear-owned at allocation. CSV scratch remains the only reconstruction `UninitializedMemory` lane because it is cold parser scratch and parsing reads only the explicit byte count written before parse.

Constraints:
- No `DecalProjector` GameObjects, Canvas blood overlays, material clones, or per-wound GameObject hierarchy.
- No direct Unity `Time.*` or shader `_Time` dependency in owned visor wound runtime/feature/active shader route; dispatcher frame delta drives decay and visual phase, while `TimeSliceScheduler.CurrentFrameId` drives signal dedupe/state/profile cadence.
- The touched `HectonVisorUberPostFeature` host path routes reconstruction telemetry frame and depthless-TBDR cache through the dispatcher frame source instead of `Time.frameCount`; no fluid runtime rebind cadence remains in this host.
- Legacy `HectonVisorUberPost.shader` quality gates for heat haze, VR comfort mask blending, light shafts, water refraction, and droplet refraction use continuous `smoothstep`/`lerp` weights; no hard low-tier branch is accepted for those paths.
- No active Noir synchronous Burst job route remains; batched visor wound work still uses Burst, while the one-record Noir CBuffer math stays owner-local to avoid a scheduler tax.
- No runtime damage ingress path may call `EnsureInitialized()`; cold initialization is confined to `TryInitializeColdStorage`, hot-swap rebind, CSV/profile/editor tuning, mock generation, and fault dump/bootstrap lanes.
- Active wounds scale continuously from 8 to 128 via `HomeostasisBrain.GlobalQualityWeight`; thermal pressure accelerates fade.
- `DecalTuningDTO.NormalRefractionIntensity` is cold/editor-tunable and feeds `_GlobalVisorWoundRefractionParams.x`.
- Glass cracks use persistent low-decay flags and are overwritten by the bounded circular ring when capacity saturates.
- Gameplay authority and rollback state are not mutated by the renderer; wounds are presentation-only signal consumers.

Proof:
- `Tools/Decal_Projector_Inquisition.py` latest SHINOBU_275 run 2026-05-21T23:25:17Z reports 0 active GameObject/URP decal violations.
- Binary payload ledger and route card are synchronized with the active C#/HLSL ABI: offset 72 is `BirthTime`; lifetime is packed inside `DecalTypeHash` bits 8..23, shader branch reads low 4 type bits, and atlas sampling reads bits 4..7.
- Black-box telemetry uses 300 `VisorWoundTelemetryEntry` records and dumps to `Docs/AgentLogs/Dump_SHINOBU_275.bin` on layout/non-finite/upload faults. Loop 21 format is a fixed 16-byte little-endian header followed by 300 fixed 64-byte telemetry rows written through stack spans; no `BinaryWriter` is used.
- Shader binding proof: `Hecton_VisorGlitchACES.shader.meta` GUID `2b2a9f18d90f4b35b8b4f9d1a8e23501`; `Hecton_VisorWounds.shader.meta` GUID `0a2df57d7a4e4d44a95b1b4c4bfb2750`.
- Shader warmup proof: bootstrap scene already references `HectonDeferredCaustics.shadervariants`; that collection now includes both visor wound shader GUIDs so first-combat compilation is not deferred into gameplay.
- NaN guard proof: active and deprecated owned wound shaders use explicit `dot -> max(0.0001) -> rsqrt` for crack/torn-edge normal vectors; no owned route keeps HLSL `normalize(`.
