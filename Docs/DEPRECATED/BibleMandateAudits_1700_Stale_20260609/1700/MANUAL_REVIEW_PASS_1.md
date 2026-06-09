# Manual Review Pass 1

Status: HUMAN STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

This pass reads selected hotspot files from `HOTSPOT_REVIEW.md`. It does not close the full audit. It separates obvious editor/cold paths from high-priority runtime ambiguity.

## Mandate Currency

- `80` mandate files were scanned from `.agents-skills`.
- `25` mandates are `GREEN_ROUTE_COVERED`.
- `55` mandates are `YELLOW_CURRENCY_REVIEW`.
- `0` mandates are red route/group gaps after routing `MANDATE_VERSION_6.0.txt` as a meta-mandate.
- The repeated yellow cause is source mandate wording, not missing root bibles: `48` files do not explicitly mention `GlobalQualityWeight`, `11` contain deprecated/legacy wording, and `1` lacks explicit proof wording.
- Interpretation: root bible routes are current enough to guide agents, but many old mandate source files need wording refresh if they are to remain direct current authority rather than historical technical mandates.

## High-Priority Runtime Ambiguity

### `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`

Finding:
- `ProceduralWreckGenerator` is a `MonoBehaviour` implementing `IProceduralGenerator`, `IUpdatable`, `ISlowTickable`, and `ILateFrameTickable`.
- It contains runtime mesh assembly paths: `BuildMergedMesh`, `BuildMergedMeshForTier`, async variants, `result = new Mesh()`, and `Mesh.ApplyAndDisposeWritableMeshData`.
- The merge path is skipped only when `wreckMaterialRegistry != null`; if that registry is null in player runtime, the system can generate render meshes during gameplay.
- `BakeSelectionRoot` / `HectonCompoundColliderAutoFitter` is editor-only because it sits behind `#if UNITY_EDITOR` and uses `Undo`, `EditorUtility`, and `PrefabUtility`.

Classification:
- Collider fitting block: `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Merged mesh generation block: `REVIEW_RUNTIME_MESH_MATERIAL_PATH`, likely a real violation unless player builds prove `wreckMaterialRegistry` is mandatory and generation is disabled outside offline/editor baking.

Required proof:
- Player-build route showing `wreckMaterialRegistry` is always assigned or generation never runs in player.
- Asset manifest proving generated wreck meshes are serialized before runtime when visual meshes are needed.
- Profiler/GC proof only after runtime mesh path is removed, gated, or proven unreachable.

### `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs`

Finding:
- Uses `Camera.main` in `Awake`.
- Has direct `Update()` loop.
- Reads `Keyboard.current`, `Mouse.current`, and legacy `Input.GetKey` / `Input.GetAxisRaw` directly every frame.
- Moves transform and camera rig directly in the component.

Classification:
- `REVIEW_CACHE_OR_INJECTION_REQUIRED` and `REVIEW_HOT_PHASE_METHOD`.
- This is acceptable only if the component is a local editor/debug flycam not present in release scenes. If it is in player runtime, it conflicts with input snapshot ownership, phase ownership, and direct per-frame control routing.

Required proof:
- Scene/build inclusion scan.
- If debug-only: compile guard, scene exclusion, or explicit development-only component route.
- If gameplay: replace with routed input snapshot + dispatcher-owned tick phase.

### `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs`

Finding:
- `CreateVoxelMesh()` creates a fallback mesh and calls `RecalculateNormals`, `RecalculateBounds`, and `UploadMeshData(true)`.
- It is reached by `EnsureResources()`, called from `OnEnable()` and `Start()`.
- The code comments mark it as one-time fallback/cold allocation.

Classification:
- `LIKELY_LEGAL_COLD_PATH` if the fallback exists only for missing authored assets and never repeats during gameplay.
- Still a bible pressure point: procedural mesh creation in runtime UI is only acceptable as a defensive fallback, not normal production path.

Required proof:
- Authored mesh/material assignment in production prefabs.
- Counter or log-free assertion that fallback mesh creation count is zero in release scenes.
- If fallback remains, guard it as development diagnostic or bootstrap-only and document ownership.

### `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`

Finding:
- High count of `Allocator.Persistent`, `NativeList`, `UnsafeHashMap`, and custom raw allocation.
- The file is a central vault owner with explicit disposal and generation-handle APIs.
- Many allocation lines appear in constructor/initialization and owner-owned cache setup, not generic gameplay code.

Classification:
- Mostly `LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH`, with `REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED`.
- The risk is not that `Allocator.Persistent` exists. The risk is undocumented growth through `EnsureGenerationHandle` or payload cache creation during hot gameplay phases.

Required proof:
- Initialization-only allocation map.
- Runtime growth counters for `EnsureGenerationHandle`.
- Disposal proof.
- Profiler/GC capture showing no hot-path NativeArray/UnsafeHashMap growth during gameplay.

## Priority Order

1. `ProceduralWreckGenerator.cs`: prove or remove player-runtime mesh generation.
2. `HectonWorldShellController1428.cs`: prove debug-only or replace direct Update/input/camera path.
3. UI material/mesh mutation hotspots: `AcousticRadarSphereRenderer.cs`, `FontAssetRecovery.cs`, `SuitHUDV4CanvasOverlay.cs`, `HectonBiolumSSGIFeature.cs`.
4. Native owner lifetime hotspots: `GlobalDataVault.cs`, `VegetationNavGridSynchronizer.cs`, `VegetationFlowFieldIntegrator.cs`, `GroundPenetratingRadarRuntime.cs`.
5. Log guard hotspots: `ContentRuntimeServices.cs`, `SettingsManager.cs`, `CameraJuiceSystem.cs`, `PersistentWorldRegistry.cs`, `ModLoader.cs`.

## Non-Closure

The audit is not complete until every `RUNTIME_PRECLASSIFICATION.md` line is either:

- proven editor/dev-only;
- proven cold/bootstrap-only;
- fixed as runtime violation;
- or marked false positive with file/method evidence.

Unity import, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, build, and hardware proof remain pending.
