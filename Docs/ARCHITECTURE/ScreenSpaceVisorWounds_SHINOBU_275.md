# Screen-Space Visor Wounds - SHINOBU_275

Supersession note, 2026-05-22: active trauma decal work is now `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_RESOLVER`. Use `Docs/ARCHITECTURE/SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_ROUTE_CARD.md`, BufferIDs `73190..73198`, shader `Hecton_VisorTrauma.shader`, and `_GlobalVisorTrauma`. This SHINOBU_275 note is historical.

Owner: Echelon 8 Presentation & UX / Screen-Space Wounds & Decals

## Route

- `SignalBus<CombatDamageSignal>` and `SignalBus<HighSpeedImpactSignal>` are read as unmanaged frame snapshots by `DynamicDecalVaultRuntime`.
- Impact AUP `double3` is localized by subtracting the cached camera/player AUP before casting to `float3`.
- Camera/runtime-position localization uses retained read-only AUP bridge only.
- Bridge calls: `GlobalSignals.CurrentRuntimeOriginAup()` / `TryRuntimePositionToAup()`.
- It is not a damage signal lane and publishes no direct queues.
- It fails to cached player/current-origin fallback before non-finite matrix telemetry.
- `VisorDecalDTO` is stored in `GlobalDataVault` as an explicit 80-byte unmanaged record: matrix 0, `DecalTypeHash` 64, `Opacity01` 68, `BirthTime` 72, `Flags` 76.
- `DecalTypeHash` layout:
  - low nibble: wound type;
  - bits `4..7`: atlas slice;
  - bits `8..23`: packed profile/request lifetime centiseconds.
- Preserves XML-mandated `BirthTime@72` ABI plus CSV-tuned decay/profile atlas selection.
- Burst jobs generate matrices, decay opacity, compact active records, and copy them into a double-buffered `GraphicsBuffer` from dispatcher `LateFrameTick`.
- `DeferredDecalPass` binds `_GlobalVisorWounds` / `_GlobalVisorWoundCount` and runs one RenderGraph fullscreen pass using `Hecton_VisorWounds.shader`.
- GPU uploads are staged for the subsequent frame.
- `AddRenderPasses()` publishes only the prior staged buffer and captures camera context.
- `RecordRenderGraph()` excludes signal ingestion, Vault mutation, job scheduling, and upload telemetry.
- `RecordRenderGraph()` imports the published `GraphicsBuffer` as a RenderGraph `BufferHandle`, declares `UseBuffer(Read)`, declares source/depth texture reads, and binds globals inside a raster render func.
- Runtime public wound ingress fails closed unless cold storage exists from feature `Create()` or DataVault hot-swap rebind.
- Affected APIs: `TryEnqueueRuntimeImpact`, `TryEnqueueAupImpact`.
- It cannot poll `GlobalRegistry`, allocate/prewarm queues, or request Vault handles from damage producer calls.
- Public/mock wound ingress fails closed while a visual-sync job is pending, increments dropped-ingress telemetry, and does not query or enqueue into the `NativeQueue` during the scheduled dequeue window.
- Signal-snapshot ingestion resolves material-profile Vault array and tuning DTO once per visual-sync pass, then reuses immutable values for all high-speed/combat signals.
- Existing visor noir postprocessing consumes the same visual language through `Hecton_VisorGlitchACES.shader`: torn-edge serration and procedural crack masks are active in the serialized PC renderer route.
- Noir integration is pre-tonemap. URP Volume Tonemapping owns final ACES; the active Noir shader performs grade/glitch/crack shaping without a local fragment tonemap curve or `saturate(color)` HDR clamp.
- Active Noir constant generation/upload is dispatcher-owned through `HectonVisorUberPostFeature.LateFrameTick`.
- `AddRenderPasses()` only checks last valid `GraphicsBuffer` and enqueues the RenderGraph pass.
- One-row mock/parameter math is direct scalar code, not tiny `IJob.Run()`.
- Reconstruction constants use A/B `GraphicsBuffer.Target.Constant` targets and a published active buffer.
- `AddRenderPasses()` stages camera/runtime inputs and consumes the last active buffer only.
- Dispatcher `LateFrameTick()` writes changed constants into the next mapped buffer, mirrors constants into Vault, records telemetry, and owns black-box dump.
- Visor post state route:
  - scalar/vector/texture state and wound atlas state are RenderGraph pass data;
  - raster render functions bind via `RasterCommandBuffer.SetGlobal*`;
  - Loop 22 texture binding uses `SetGlobalTexture`, not `Material.SetTexture`;
  - owned visor post shaders no longer rely on `UnityPerMaterial` or material mutation for trauma constants.
- Loop 28 corrected active disk state.
- Wound atlas, crack, lens dirt, blue noise, and VR comfort textures bind with `RasterCommandBuffer.SetGlobalTexture`.
- No owned `Material.SetTexture` or stale string-name binding remains in `DeferredDecalPass` or `HectonVisorUberPostFeature`.
- `HectonVisorUberPost.shader` and `Hecton_BilateralUpsample.shader` consume dispatcher-published visual time globals (`_HectonUberVisualTime`, `_H8UberNoirVisualTime`) instead of engine `_Time`.
- Reconstruction aesthetic CSV rows load only from cold create/DataVault hot-swap lanes.
- Rows copy into a fixed 32-row cold cache.
- Render enqueue selects profiles from that snapshot.
- It does not lock profile Vault buffer or retry file IO.
- Noir color CSV rows are also copied into a fixed cold 32-row snapshot; LateFrame profile selection does not resolve the Noir profile Vault array on cache misses.
- Shared visor host state no longer calls `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` from render enqueue.
- It consumes cached `IPlayerRuntimeContext` snapshots for player camera, survival status, and movement stress.
- Wet-lens scalar remains a presentation read from cached movement owner; touched host file no longer imports `Hecton8.Gameplay`.
- Shared host no longer imports `Hecton8.Physics`, caches `HectonFluidEngine`, or subscribes to `GlobalRegistryServiceSlot.FluidRuntime` for visuals.
- Removed concrete maelstrom warp is replaced by pressure/stress screen-space surge scalar.
- Source: existing presentation inputs until a contracts-only fluid read model exists.
- The live matrix debug view is editor-owned by `ScreenSpaceDecalTunerWindow` through `SceneView.duringSceneGui`; `DynamicDecalGizmoVisualizer` is compiled only under `UNITY_EDITOR`, so player builds do not carry a scene-component proof surface.
- `ScreenSpaceDecalTunerWindow` exposes designer bridge facts for visor wound material profiles.
- Fields: source CSV path, schema id/hash, runtime Vault route, DataMonolith output caveat, validation state, row count, header hash.
- ABI summaries cover `VisorDecalDTO` and `DecalMaterialProfileDTO`; schema mismatch fails before cold Vault CSV load.
- Diagnostic `TryGetTuning`, `TryGetRuntimeState`, and `TryGetLatestTelemetry` calls return immutable owner-phase snapshots. They do not lock Vault buffers, resolve native arrays, complete jobs, allocate, or mutate global lock state.
- `TryAcquireDecalBufferRead` / `ReleaseDecalBufferRead` is also compiled only under `UNITY_EDITOR`; it is an explicit acquire/release debug lane for SceneView gizmos and is not available to player runtime callers.
- Runtime state pointer access in `ExecuteVisualSync()` is fail-closed.
- Stale/invalid one-row Vault state buffer marks existing layout/fault telemetry bit.
- It returns false instead of throwing a managed gameplay exception.
- Cold initialization seeds one-row runtime state before VISUAL_SYNC.
- It requests visual/profile Vault buffers with clear memory.
- First normal visual-sync frame does not run direct `ClearDecalsJob.Execute(i)` loop.
- Fallback uses `UnsafeUtility.MemClear` only when cold state is missing.
- Reconstruction constants Vault mirror is clear-owned at allocation.
- CSV scratch is the only reconstruction `UninitializedMemory` lane.
- Reason: cold parser scratch.
- Parser reads only explicit byte count written before parse.

## Constraints

- No `DecalProjector` GameObjects, Canvas blood overlays, material clones, or per-wound GameObject hierarchy.
- No direct Unity `Time.*` or shader `_Time` dependency in owned visor wound runtime/feature/active shader route; dispatcher frame delta drives decay and visual phase, while `TimeSliceScheduler.CurrentFrameId` drives signal dedupe/state/profile cadence.
- The touched `HectonVisorUberPostFeature` host path routes reconstruction telemetry frame and depthless-TBDR cache through the dispatcher frame source instead of `Time.frameCount`; no fluid runtime rebind cadence remains in this host.
- Legacy `HectonVisorUberPost.shader` quality gates use continuous `smoothstep`/`lerp` weights.
- Covered paths: heat haze, VR comfort mask blending, light shafts, water refraction, droplet refraction.
- No hard low-tier branch is accepted for those paths.
- Loop 29 also removes the hard low-tier VR comfort spatial edge: both comfort mask paths use `smoothstep(0.36, 0.48, edge01)` instead of `step(0.42, edge01)`.
- Loop 30 removes the mobile waterline camera-crossing cliff: `cameraSubmerged` uses `smoothstep(-softness, softness, (waterlineY
- 0.03)
- cameraPosition.y)` rather than a hard `step`.
- Loop 31 removes hard crack reveal thresholds: procedural and texture-driven crack reveals use narrow `smoothstep` bands against `damage01`.
- Loop 32 removes the radial falloff exponent-family snap: `FastRadialFalloff01()` blends low/high polynomial approximations with `smoothstep(1.85, 2.15, e)`.
- No active Noir synchronous Burst job route remains; batched visor wound work still uses Burst, while the one-record Noir CBuffer math stays owner-local to avoid a scheduler tax.
- No runtime damage ingress path may call `EnsureInitialized()`; cold initialization is confined to `TryInitializeColdStorage`, hot-swap rebind, CSV/profile/editor tuning, mock generation, and fault dump/bootstrap lanes.
- Active wounds scale continuously from 8 to 128 via `HomeostasisBrain.GlobalQualityWeight`; thermal pressure accelerates fade.
- `DecalTuningDTO.NormalRefractionIntensity` is cold/editor-tunable and feeds `_GlobalVisorWoundRefractionParams.x`.
- Glass cracks use persistent low-decay flags and are overwritten by the bounded circular ring when capacity saturates.
- Gameplay authority and rollback state are not mutated by the renderer; wounds are presentation-only signal consumers.

Proof:
- `Tools/Decal_Projector_Inquisition.py` latest SHINOBU_275 run 2026-05-21T23:42:38Z reports 0 active GameObject/URP decal violations.
- Binary payload ledger and route card match active C#/HLSL ABI.
- Offset `72`: `BirthTime`.
- Lifetime: `DecalTypeHash` bits `8..23`.
- Shader branch reads low 4 type bits; atlas sampling reads bits `4..7`.
- Black-box telemetry uses 300 `VisorWoundTelemetryEntry` records.
- Dump path: `Docs/AgentLogs/Dump_SHINOBU_275.bin` on layout/non-finite/upload faults.
- Loop 21 format: fixed 16-byte little-endian header plus 300 fixed 64-byte rows.
- Rows are written through stack spans; no `BinaryWriter`.
- Shader binding proof: `Hecton_VisorGlitchACES.shader.meta` GUID `2b2a9f18d90f4b35b8b4f9d1a8e23501`; `Hecton_VisorWounds.shader.meta` GUID `0a2df57d7a4e4d44a95b1b4c4bfb2750`.
- Shader warmup proof: bootstrap scene already references `HectonDeferredCaustics.shadervariants`; that collection now includes both visor wound shader GUIDs so first-combat compilation is not deferred into gameplay.
- NaN guard proof: active and deprecated owned wound shaders use explicit `dot -> max(0.0001) -> rsqrt` for crack/torn-edge normal vectors; no owned route keeps HLSL `normalize(`.
