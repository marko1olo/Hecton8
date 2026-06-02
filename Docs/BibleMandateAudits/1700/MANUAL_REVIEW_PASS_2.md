# Manual Review Pass 2

Status: HUMAN STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

This pass reviews second-tier hotspots after `MANUAL_REVIEW_PASS_1.md`. It corrects false positives around compile-stripped logging and separates cold UI/render setup from runtime native allocation paths that still need proof or fixes.

## Static Scanner Correction

- `Assets/_Project/Scripts/Core/H8Debug.cs` uses `[Conditional("UNITY_EDITOR")]` and `[Conditional("DEVELOPMENT_BUILD")]` on `Log`, `LogWarning`, `LogError`, and `LogException`.
- Calls to `H8Debug.*` are therefore not release-player hot-path logs. They were reclassified in `Tools/Audit/Run-BibleMandateAudit1700.ps1` as `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Direct `UnityEngine.Debug.*` remains a review target unless it is inside editor/development guards, bootstrap failure handling, or explicit diagnostic-only routes.

## UI / HUD / Font Hotspots

### `Assets/_Project/Scripts/UI/FontAssetRecovery.cs`

Finding:
- `Bootstrap()` runs after scene load, but only invokes `RepairKnownAssetImports()` inside `#if UNITY_EDITOR`.
- `Awake()` destroys the transient owner immediately.
- `RecoverFontAssets()` is private and no active project reference calls it.
- Runtime-looking TMP material/atlas mutation exists in private methods, but the only material creation path is editor-only under `#if UNITY_EDITOR`.

Classification:
- Current active route: `LEGAL_EDITOR_OR_DEV_GUARDED` / effectively dormant.
- Risk: if `RecoverFontAssets()` is revived for player runtime, it becomes a UI production violation because it can mutate TMP font material, call `ForceMeshUpdate`, and repair font atlas state during gameplay.

Required proof:
- Keep production font atlas/material repair in editor/import validation.
- Production UI prefabs must ship with static font assets, assigned shared materials, and no player-runtime font recovery dependency.

### `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

Finding:
- The HUD registers through `ISlowTickable` / `ILateFrameTickable`, not raw `Update`.
- Runtime material creation for dithered backdrop, save pulse, acoustic radar, and threat chevrons occurs in `Awake`/`OnEnable` bootstrap style methods with `COLD ALLOC` comments.
- `BuildThreatChevronMesh()` creates a small Mesh at runtime during `EnsureThreatChevronRuntimeResources()`.
- The overlay mutates UI `Graphic.material` bindings and material properties on owned runtime UI materials, not standard SRP-batched world geometry.

Classification:
- Most lines: `LIKELY_LEGAL_COLD_PATH` for UI bootstrap.
- `BuildThreatChevronMesh()` remains `REVIEW_RUNTIME_MESH_MATERIAL_PATH`: acceptable only as a one-time UI bootstrap fallback, not as a normal production mesh-generation pattern.

Required proof:
- Confirm `EnsureThreatChevronRuntimeResources()` runs once per HUD lifetime and never during per-frame UI refresh.
- Prefer serialized/authored threat-chevron mesh asset for release UI, or document the one-time generated UI mesh as an explicit boot fallback with profiler/GC proof.
- Material writes must remain on owned UI materials with cached property ids and dirty-state guards.

### `Assets/_Project/Scripts/UI/SettingsPanel.cs` and `Assets/_Project/Scripts/UI/SettingsManager.cs`

Finding:
- The highest-count log lines are `H8Debug.*`, which are compile-stripped outside editor/development builds.
- Settings mutations are menu/user-action driven, not a continuous gameplay tick route.

Classification:
- Log findings are `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Remaining UI proof still needs menu interaction profiler/GC capture and localization expansion screenshots before release acceptance.

## Rendering / VFX Hotspots

### `Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs`

Finding:
- The render feature uses `RecordRenderGraph`, raster render graph passes, declared texture reads/writes, and `CoreUtils.CreateEngineMaterial` in `Create()`.
- `AddRenderPasses()` only enqueues the already-created pass.
- The feature falls back to a proxy path when compute is unavailable.

Classification:
- `LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH` for material creation.
- `REVIEW_GPU_COST_PROOF_REQUIRED` for the SSGI effect itself, because render correctness and MX350 cost require Frame Debugger/RenderGraph/GPU profiler proof.

Required proof:
- RenderGraph Viewer or Frame Debugger capture with named passes.
- MX350/compact capture proving the feature disables, proxies, or stays inside assigned GPU budget.

### `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`

Finding:
- The system uses `ISlowTickable` and `ILateFrameTickable`.
- Diagnostic logs are conditional/dev-only.
- Telemetry ring allocation is resolved through `GlobalDataVault` in setup/dependency bind paths, and telemetry writes occur into the resolved fixed ring.
- No raw per-frame Unity `Update()` route was found in the reviewed snippets.

Classification:
- Log findings are `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Telemetry allocation is `LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH`.
- The visual effect still needs profiler proof because camera shake, DoF, vignette, biome blend, and speed lines can cross the 0.1 ms suspicion threshold if stacked.

## Runtime Content / Streaming Hotspots

### `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`

Finding:
- `ContentAuthorityRuntime` uses dispatcher interfaces (`IUpdatable`, `ILateFrameTickable`, `ISlowTickable`, `IColdTickable`).
- It allocates fixed arrays and hologram proxy pool in `Awake()`.
- Addressables VFX prewarm is async and ledgered, not synchronous `Resources.Load`.
- Log helper methods are conditional/dev-only.

Classification:
- Logs: `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Hologram pool: `LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH`.
- Addressables prewarm remains `REVIEW_STREAMING_PROOF_REQUIRED` until handle ledger, release, and memory-pressure proof are captured.

Required proof:
- Addressables handle ledger proof.
- Memory profiler proof for prewarm/residency.
- Runtime capture showing no `WaitForCompletion`, no sync load, and bounded proxy pool behavior.

## World / Terrain / Native Lifetime Hotspots

### `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs`

Finding:
- The abyssal path route allocates `NativeList<Vector3>` and several H8Memory-owned `NativeArray<T>` buffers with `Allocator.Persistent` during path scheduling.
- The buffers are tracked and released, including job-dependent disposal.
- Completion uses `DispatcherJobSwap.TryComplete`, which is better than naked same-frame `.Complete()`, but still needs owner-barrier proof.

Classification:
- `REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED`.
- This is not automatically GC garbage, but it is a real runtime native allocation path. It violates the spirit of the memory sovereignty rules unless the allocation cadence is proven rare/cold or the scratch buffers are moved into preallocated DataVault/H8Memory pools.

Required proof or fix:
- Prove path requests are slow-cadence and cannot happen repeatedly under player stress, or replace per-request allocations with preallocated owner scratch buffers.
- Add budget telemetry for allocation count/bytes and path request cadence.
- Profiler/native memory capture after a stress route with fauna/navigation pressure.

### `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`

Finding:
- Persistent state handles and GraphicsBuffers are created in `OnEnable()` cold setup.
- `RadarPendingJob` allocates H8Memory-owned persistent arrays per scan/job, then releases them after job completion.
- SDF snapshot can resize/allocate persistent memory when required SDF length grows.
- Fallback runtime material is created if no authored `radarPingMaterial` is assigned.

Classification:
- Cold handles/GPU buffers: `LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH`.
- Per-scan pending-job buffers and SDF snapshot growth: `REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED`.
- Fallback material: `LIKELY_LEGAL_COLD_PATH`, but production prefabs should assign the material to avoid fallback.

Required proof or fix:
- Prefer double-buffered, preallocated scan job buffers sized to `GroundRadarConstants.MaxPings`.
- Prove no repeated native allocation during scanner spam and no hidden allocation when SDF payload changes.
- Production prefab proof for assigned radar material.

## Updated Priority Order

1. `ProceduralWreckGenerator.cs`: remove/prove unreachable player-runtime mesh generation.
2. `VegetationNavGridSynchronizer.cs`: replace or prove runtime native path allocations.
3. `GroundPenetratingRadarRuntime.cs`: replace or prove per-scan native allocations and fallback material absence in production prefabs.
4. `SuitHUDV4CanvasOverlay.cs`: prove one-time HUD mesh/material boot path or serialize the threat chevron mesh.
5. `HectonBiolumSSGIFeature.cs`: run render graph and GPU proof on compact lane.
6. `ContentRuntimeServices.cs`: run Addressables ledger/residency proof.

## Non-Closure

This pass reduces false positives, but it does not close runtime correctness. Every system folder still needs line-level classification and runtime proof where it owns gameplay/player paths.
