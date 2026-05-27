# Unity Shader Mesh CTS Recheck - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Evidence class: STATIC_SOURCE + OFFICIAL_UNITY_DOCS + GUARDED_BUILD_FAILED_BEFORE_CSHARP
Domain: Unity runtime API traps, shader dependency ownership, cold mesh construction

## Scope

User directive: keep searching for subtle Unity/project traps, fix only defensible issues, avoid disrupting concurrent agents, and report evidence without optimism.

Relevant rules re-read:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- AGENTS read-accessor purity and build-guard rules

Official Unity facts used:

- `Shader.Find` is not a safe player dependency route unless the shader is included through a tracked reference, `Resources`, or graphics settings.
- `Resources` is a legacy runtime loading route and must stay tiny/audited here.
- `Mesh.SetVertices`, `SetUVs`, `SetNormals`, and `SetTriangles` are the preferred route over legacy mesh property setters.

Source URLs:

- `https://docs.unity3d.com/ScriptReference/Shader.Find.html`
- `https://docs.unity3d.com/Manual/LoadingResourcesatRuntime.html`
- `https://docs.unity3d.com/ScriptReference/Mesh.SetVertices.html`

## Changed Files

Shader route:

- `Assets/_Project/Scripts/Core/RuntimeShaderReferenceCatalog.cs`
- `Assets/_Project/Resources/RuntimeShaderReferenceCatalog.asset`
- `Assets/_Project/Art/Shaders/Hecton_RuntimeCheckerboardUnlit.shader`
- `Assets/_Project/Art/Shaders/Hecton_RuntimeCheckerboardUnlit.shader.meta`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
- `Assets/_Project/Scripts/World/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`

Cancellation route:

- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

Mesh route:

- `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityAssetPostprocessor.cs`
- `Assets/_Project/Scripts/Editor/LSystemGenomeLabWindow.cs`
- `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`
- `Assets/_Project/Scripts/UI/PDAMapTab.cs`
- `Assets/_Project/Scripts/Fabricator.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`

## Fixes

1. Shader dependency ownership
   - Expanded `RuntimeShaderReferenceCatalog` from earlier `13` references to `19` serialized shader references.
   - Added `Hecton8/Runtime/CheckerboardUnlit` so `AssetLifecycleGovernor` keeps checkerboard fallback diagnostics without release `Shader.Find`.
   - Routed voxel bake ghost, drone procedural, wreck indirect, marauder outpost indirect, carve debris indirect, and checkerboard fallback through the catalog.
   - Moved remaining first-party runtime `Shader.Find` calls behind `UNITY_EDITOR || DEVELOPMENT_BUILD`.

2. Read-accessor purity
   - `RuntimeShaderReferenceCatalog.TryGet*` methods no longer call `Resources.Load` or mutate cache.
   - The single `Resources.Load<RuntimeShaderReferenceCatalog>` call now sits in `BeforeSceneLoad` cold bootstrap.
   - This remains an audited exception, not a general asset heap.

3. Cancellation allocation cleanup
   - Removed redundant one-token `CreateLinkedTokenSource` from `PrologueSequenceRegistryBridge`.
   - Removed redundant one-token linked CTS in `GameBootstrapper.RunBootstrapStateMachineAsync`.
   - Kept the scene activation linked CTS because it combines owner token, destroy token, and timeout.

4. Mesh construction API cleanup
   - Replaced cold fallback mesh property setters with `SetVertices`, `SetNormals`, `SetUVs`, `SetTriangles`, or existing `SetIndices`.
   - Runtime scan no longer reports project-owned runtime `mesh.vertices`, `mesh.triangles`, or `mesh.uv` property setters.

## Static Proof

Shader lookup scan:

```text
release_runtime_shader_find_hits=0
guarded_shader_find_hits=61
```

Residual `Resources.Load` scan:

```text
Assets/_Project/Scripts/Core/RuntimeShaderReferenceCatalog.cs:45
```

This is the audited tiny bootstrap catalog load. Other hits are editor scanner string literals.

Residual linked CTS scan:

```text
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:5363
```

This is the justified scene activation route: `ownerToken + destroyCancellationToken + CancelAfter(timeout)`.

Residual mesh property setter scan:

```text
Editor-only real mesh setter: Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs
False positives: MesofaunaAiTunerWindow vertex.uv fields; DamageBake scanner string literal
Runtime project-owned mesh property setter hits: 0
```

`git diff --check` for touched files passed with LF/CRLF warnings only.

## Build State

Guarded build was launched legally:

```text
log: Docs/Reports/BUILD_UNKNOWN_SHADER_MESH_CTS_RECHECK_20260526.log
guard launch attempt: 19
launch cpu: 36.2
launch compilerCount: 0
exitCode: 1
warnings: 0
errors: 62
```

The build failed before C# source compilation. Root `Hecton8.slnx` references ignored/generated Unity `.csproj` files that are absent from the workspace:

```text
AmplifyImpostors.Editor.csproj
Assembly-CSharp.csproj
Hecton8.Core.csproj
MapMagic.csproj
MoreMountains.Feedbacks.csproj
Unity.RenderPipelines.Universal.Runtime.csproj
...and other generated package/project files
```

Proof:

```text
.gitignore contains *.csproj and *.slnx
git ls-files '*.csproj' returns no active root Unity project files
Test-Path Hecton8.Core.csproj -> False
```

This is not a C# diagnostic for the shader/mesh/CTS edits. It is a current solution/project-file boundary failure. No current compile-green claim is valid until Unity regenerates the ignored `.csproj` files or the solution route is repaired.

## Residual

- Runtime/profiler microseconds saved claimed: `0`; no profiler/player proof was run.
- Unity import, shader import, Console, Play Mode, player build, visual proof, Memory Profiler, and GCMonitor remain pending.
- `RuntimeShaderReferenceCatalog` still uses one audited `Resources.Load` at cold bootstrap. Better long-term route is a serialized bootstrap owner or Addressables-backed manifest if scene/bootstrap wiring is available.
- Editor-only mesh property setters remain in `HectonOctahedralImpostorBaker`; not runtime architecture debt.

## Documentation Gates

```text
Tools/VerifyDocStructure.py -> pass=true; activeDocCount=691; encodingWithoutUtf8Sig=0
Tools/OOP_Doc_Scanner.py -> finalPass=true; activeFileCount=691; sourceSyncPass=true
```
