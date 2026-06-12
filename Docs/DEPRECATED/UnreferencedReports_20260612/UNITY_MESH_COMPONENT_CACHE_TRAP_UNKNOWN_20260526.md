# Unity Mesh And Component Cache Trap - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Evidence class: STATIC_SOURCE + OFFICIAL_UNITY_DOCS + CLI_COMPILE
Domain: Unity runtime hidden-allocation/API trap cleanup

## Scope

User directive: continue finding subtle Unity/project traps, verify with current docs, fix only correct low-risk surfaces, and avoid active dirty work from other agents.

Relevant mandates re-read:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`

Official Unity facts used:

- Unity 6 `Mesh.vertices` returns a copy on get and assigns a new vertex array on set.
- Unity 6 `Mesh.SetVertices` supports array, list, and `NativeArray<T>` input routes.
- Unity 6 `GameObject.GetComponentsInChildren(bool, List<T>)` fills a supplied list so a caller can avoid allocating a new list object per call.

Source URLs:

- `https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-vertices.html`
- `https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetVertices.html`
- `https://docs.unity3d.com/6000.0/Documentation/ScriptReference/GameObject.GetComponentsInChildren.html`

## Changed Files

- `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs`
- `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
- `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`
- `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs`
- `Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs`
- `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`

Build-wall repairs during guarded compile recheck:

- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.Comfort.cs`
- `Assets/_Project/Scripts/World/ScatterEvaluator.cs`
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`

Documentation validation repair:

- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`

## Fixes

1. Fallback procedural meshes
   - Replaced `mesh.vertices`, `mesh.uv`, and `mesh.triangles` setter routes with `SetVertices`, `SetUVs`, `SetTriangles`, or `SetIndices`.
   - Moved inline fallback geometry arrays to static readonly owner arrays in clean files.
   - Preserved bounds/normals/upload behavior.

2. MapMagic terrain tile cache
   - Replaced cached `TerrainTile[]` ownership with a pre-sized `List<TerrainTile>`.
   - Replaced `GetComponentsInChildren<TerrainTile>(true)` array allocation with `GetComponentsInChildren<TerrainTile>(true, _cachedTerrainTiles)`.
   - Kept the existing child-count refresh gate and hot read indexing.

## Current Static Results

Scoped scan for mesh property setters in the files changed by this pass:

```text
0 hits for .vertices/.uv/.triangles/.normals in touched files
```

Project-wide residual mesh-property scan:

```text
Fabricator.cs - dirty file, residual mesh.vertices/triangles/normals setters
DiegeticVisorHudMesh.cs - dirty file, residual runtime mesh setters
CarveDebrisComputeRenderer.cs - dirty file, residual mesh setters
PDAMapTab.cs - dirty file, residual point-cloud quad setter
PauseMenuController.cs - ColorBlock.colors struct property, not Mesh API
LocOverflowHandler.cs - TMP meshInfo.vertices field, not Mesh API
```

Project-wide `GetComponentsInChildren<T>()` array residuals after the MapMagic fix:

```text
DebrisManager.cs - dirty file, one Collider[] residual
ProceduralWreckGenerator.cs - dirty file, editor/bake collider generation residuals
H8PrefabRegistry.cs - UNITY_EDITOR estimator residual
ArmorPenetrationEditorFacade.cs - UNITY_EDITOR facade residual
PrologueSequenceRegistryBridge.cs - dirty file, existing list overload route
Other listed hits are already List<T> overloads
```

## Build State

Initial compile recheck exposed unrelated dirty-file build walls:

```text
BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_20260526.log -> exit 1, 25 errors in WreckMaterialRegistry namespace/import state
BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK_20260526.log -> exit 1, 2 errors and 15 warnings in dirty thermal/wreck/scatter/VR files
BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK2_20260526.log -> exit 0, 1 warning in dirty DestructibleOrganicManager control flow
```

Final guarded build:

```text
Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log
guard launch: attempt 9, CPU=33.3%, compilerProcessCount=0
command: dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false
result: Build succeeded; 0 Warning(s); 0 Error(s); exit 0
```

`git diff --check` passed for touched source files with only LF/CRLF working-copy warnings.

## Documentation Gates

Final documentation validation after the build/root-doc update:

```text
Tools/VerifyDocStructure.py -> pass=true; activeDocCount=701; encodingWithoutUtf8Sig=0
Tools/OOP_Doc_Scanner.py -> finalPass=true; activeFileCount=701; sourceSyncPass=true
```

The OOP scanner initially rejected one overlong unstructured paragraph in `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`.
That paragraph was reformatted into short contract bullets without changing the Data Monolith facts.

## Residual

- Runtime/profiler microseconds saved claimed: `0`; no Unity Play Mode or profiler capture was run.
- Dirty residual mesh setter files were not edited to avoid cross-agent interference.
- Root-doc promotion now points to `BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log`.
- Documentation gates are closed for this pass.
