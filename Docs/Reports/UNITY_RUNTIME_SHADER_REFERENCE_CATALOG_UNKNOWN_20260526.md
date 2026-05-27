# Unity Runtime Shader Reference Catalog - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Evidence class: STATIC_SOURCE + OFFICIAL_UNITY_DOCS + BUILD_GUARD_BLOCKED
Domain: Unity runtime shader dependency ownership

Superseded current-state note: this report records the first catalog slice. Current shader/mesh/CTS recheck is `Docs/Reports/UNITY_SHADER_MESH_CTS_RECHECK_UNKNOWN_20260526.md`; it expands the catalog to `19` shader references, makes `TryGet*` accessors pure, and reduces first-party runtime release-reachable `Shader.Find` hits to `0`.

## Scope

User directive: keep finding subtle Unity traps, verify current online docs, fix correct low-risk source, and avoid dirty files owned by other agents.

Relevant mandates re-read:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Official Unity facts used:

- `Shader.Find` can return a shader in Editor while the player build lacks it if no tracked reference or build inclusion route exists.
- Unity lists direct references, Always Included Shaders, and `Resources` references as build-inclusion routes.
- Unity documents `Resources` as acceptable for minimal bootstrapping, but warns against large asset sets because they add startup/build/memory pressure.

Source URLs:

- `https://docs.unity3d.com/ScriptReference/Shader.Find.html`
- `https://docs.unity3d.com/Manual/LoadingResourcesatRuntime.html`

## Changed Files

New ownership route:

- `Assets/_Project/Scripts/Core/RuntimeShaderReferenceCatalog.cs`
- `Assets/_Project/Resources/RuntimeShaderReferenceCatalog.asset`
- `Assets/_Project/Art/Shaders/Hecton_RuntimeFlatColor.shader`

Existing source patched:

- `Assets/_Project/Scripts/Core/ConnectionSplineBatchRenderer.cs`
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`
- `Assets/_Project/Scripts/World/ImpostorSystem.cs`
- `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs`
- `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonNoirDepthFogFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonHalfResParticlesFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonScooterVolumetricShaftsFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`
- `Assets/_Project/Scripts/UI/FakeRadarBlipController.cs`
- `Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`

Meta files were added for the new catalog, asset, and shader.

## Fixes

1. Runtime shader catalog
   - Added a small `Resources` ScriptableObject catalog with explicit shader references.
   - The catalog is cached statically and reset on `SubsystemRegistration`.
   - This avoids release dependency on string lookup for bootstrap-created runtime materials.

2. Runtime flat-color shader
   - Added `Hecton8/Runtime/FlatColor` for construction/resource ghost fallback material creation.
   - Rejected unrelated built-in shader strings as the release dependency route.

3. Release lookup boundary
   - Patched clean runtime/URP/VFX/UI owners to resolve from the catalog first.
   - Kept `Shader.Find` only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
   - Dirty runtime files were not touched.

## Static Proof

Scoped `Shader.Find` reachability after this pass:

```text
ConnectionSplineBatchRenderer.cs:450 releaseReachable=False
ConnectionSplineBatchRenderer.cs:452 releaseReachable=False
SceneRuntimeService.cs:832 releaseReachable=False
GroundPenetratingRadarRuntime.cs:1130 releaseReachable=False
ImpostorSystem.cs:1136 releaseReachable=False
ShinobuPlasmaBeamRuntime.cs:944 releaseReachable=False
HectonAbyssalSsdoFeature.cs:380 releaseReachable=False
HectonStochasticSsrFeature.cs:294 releaseReachable=False
HectonNoirDepthFogFeature.cs:294 releaseReachable=False
HectonHalfResParticlesFeature.cs:326 releaseReachable=False
HectonScooterVolumetricShaftsFeature.cs:1009 releaseReachable=False
HectonVolumetricParticulateFogFeature.cs:1686 releaseReachable=False
FakeRadarBlipController.cs:877 releaseReachable=False
ConstructionRuntimeProxyFactory.cs:151 releaseReachable=False
ConstructionRuntimeProxyFactory.cs:153 releaseReachable=False
ConstructionRuntimeProxyFactory.cs:155 releaseReachable=False
ResourceDistributionDirector.cs:2985 releaseReachable=False
ResourceDistributionDirector.cs:2987 releaseReachable=False
```

Catalog reference check:

```text
RuntimeShaderReferenceCatalog.asset contains 13 shader references.
Hecton_RuntimeFlatColor.shader exists and is referenced by the catalog.
```

Project-wide release-reachable residuals after this pass:

```text
Dirty runtime residuals:
DroneFleetManager.cs:6079
HectonVoxelEngine.cs:5632
AssetLifecycleGovernor.cs:5250,5252,5254,5256
CarveDebrisComputeRenderer.cs:1737
MarauderOutpostGenerationService.cs:1059
WreckMaterialRegistry.cs:1366

Editor-only authoring residuals remain under Assets/_Project/Scripts/Editor.
```

`git diff --check` passed for touched source/assets with LF/CRLF warnings only.

## Build State

Build recheck was attempted through the required AGENTS guard:

```text
Docs/Reports/BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_RECHECK2_20260526.log
result: guard blocked build; no legal CPU/compiler window
attempts: 60
finalCpuPercent: 71
finalCompilerProcessCount: 0
launched: False
```

This is not a compile failure. It is a blocked build launch. The latest clean full-solution CLI build remains:

```text
Docs/Reports/BUILD_UNKNOWN_MESH_COMPONENT_CACHE_TRAP_RECHECK3_20260526.log
result: Build succeeded; 0 Warning(s); 0 Error(s)
```

That clean build is before the render-feature shader pass and before this catalog pass. Do not claim current source is CLI-compile-proven until a later legal build completes.

Earlier catalog build-guard artifact:

```text
Docs/Reports/BUILD_UNKNOWN_RUNTIME_SHADER_REFERENCE_CATALOG_20260526.log
result: guard blocked build; no legal CPU/compiler window
```

## Documentation Gates

Final documentation validation after the report/root-doc update:

```text
Tools/VerifyDocStructure.py -> pass=true; activeDocCount=687; encodingWithoutUtf8Sig=0
Tools/OOP_Doc_Scanner.py -> finalPass=true; activeFileCount=687; sourceSyncPass=true
```

Pre-existing active doc encoding issues repaired during this gate:

```text
Docs/CURRENT_ENGINEERING_DISTILLATE.md -> converted to UTF-8 BOM
Docs/PROJECT_BASELINE.md -> converted to UTF-8 BOM
Docs/ARCHITECTURE/TERRAIN_RUNTIME_AUTHORITY_ROUTE.md -> converted to UTF-8 BOM and paragraph-split
```

## Residual

- Runtime/profiler microseconds saved claimed: `0`; no profiler/player proof was run.
- Unity import, Console, Play Mode, player build, shader variants, material import, scene wiring, and visual proof remain pending.
- Dirty residual runtime `Shader.Find` files remain for their active owners.
- The catalog uses `Resources` deliberately as a tiny bootstrap reference route, not as a general asset heap.
