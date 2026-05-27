# Unity Runtime API Trap Cleanup - UNKNOWN - 2026-05-26

Date: 2026-05-26
Agent: UNKNOWN
Evidence class: STATIC_SOURCE + OFFICIAL_UNITY_DOCS + CLI_COMPILE
Domain: Unity runtime hidden-allocation/API trap cleanup

## Scope

User directive: keep searching for subtle project traps, verify online, fix carefully, do not disturb other agents.

Relevant mandates re-read:

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`

Official Unity facts used:

- `Renderer.sharedMaterials` returns a copied materials array; `Renderer.GetSharedMaterials(List<Material>)` avoids that copy route.
- `Physics.RaycastNonAlloc` and Unity 6 physics optimization docs confirm the project rule: query result buffers must be preallocated.
- `Input.GetTouch(index)` docs state no temporary variables are allocated; old `Input.touches` docs mark the array property as temporary allocation.
- `GameObject.CompareTag` docs expose the optimized tag path and Unity 2023+ `TagHandle` overload.

## Changed Files

- `Assets/_Project/Scripts/Editor/UnityApiTrapDetector.cs`
- `Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`
- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`
- `Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs`
- `Assets/_Project/Scripts/Gameplay/DebrisManager.cs`

## Fixes

1. `UnityApiTrapDetector`
   - Added `Renderer.sharedMaterials` getter detection.
   - Added generic `GetComponents*<T>` array-overload detection.
   - Added `#if UNITY_EDITOR` preprocessor tracking so editor-only blocks inside runtime files are not counted as runtime defects.

2. `ContextualPhysicalIkRig`
   - Replaced `muscleBulgeRenderer.sharedMaterials` copied-array getter/setter path with reusable `List<Material>`, `GetSharedMaterials`, and `SetSharedMaterials`.
   - Material instance ownership remains unchanged; this only removes the Unity array-copy trap.

3. `PrologueSequenceRegistryBridge`
   - Replaced `GetComponents<MonoBehaviour>()` array allocation with reusable `List<MonoBehaviour>` and `GetComponents<MonoBehaviour>(list)`.

4. `HarvestableOutcrop`
   - Replaced child renderer/collider array discovery with reusable `List<Renderer>` and `List<Collider>` buffers.
   - Collapse toggles now iterate retained lists.

5. `OrganicDebrisProfile` inside `DebrisManager`
   - Replaced temporary `MeshFilter[]` authoring scan with reusable `List<MeshFilter>`.
   - Serialized cache arrays remain unchanged because they are the profile contract used by runtime debris spawning.

## Current Static Results

Custom static mimic of the updated detector, excluding `/Editor/` paths and `#if UNITY_EDITOR` blocks:

```text
TOTAL=0
```

Runtime grep after fixes:

- `Input.touches`: `0`
- `Physics.RaycastAll` / `SphereCastAll` / `OverlapSphere` in first-party runtime: `0`
- `FindObjectsOfType` / `FindObjectOfType` in first-party runtime: `0`
- `.tag ==` / `.tag !=` in first-party runtime: `0`
- generic `GetComponents<T>()` array overload in first-party runtime: `0` unwaived static mimic hits
- `Renderer.sharedMaterials` runtime getter: `0` unwaived static mimic hits

## Build State

Guard readings:

```text
CPU=100%, csc=1, dotnet=1 -> build blocked
CPU=96%, csc=0, dotnet=0 -> build blocked
CPU=88%, csc=0, dotnet=0 -> build blocked
CPU=77%, csc=0, dotnet=0 -> build blocked
CPU=74%, csc=0, dotnet=0 -> build blocked
CPU=62%, csc=0, dotnet=0 -> build blocked
CPU=73%, csc=0, dotnet=0 -> build blocked
CPU=21%, csc=0, dotnet=0 -> build launched
```

Build command:

```text
dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false
```

Build result:

```text
Docs/Reports/BUILD_UNKNOWN_RUNTIME_API_TRAP_CLEANUP_20260526.log
Build succeeded.
0 Warning(s)
0 Error(s)
```

`git diff --check` passed for touched files, with only LF/CRLF working-copy warnings.

Documentation structure validator after the new report/root-doc updates:

```text
VerifyDocStructure.py: pass=true, activeDocCount=693, encodingWithoutUtf8Sig=0
```

Final documentation scanner state after retry:

```text
OOP_Doc_Scanner.py: finalPass=true, activeFileCount=693, sourceSyncPass=true
```

## Residual

- Runtime/profiler microseconds saved claimed: `0`; no Unity Play Mode or profiler capture was run.
- `Shader.Find` has many cold fallback sites. I did not bulk-edit them because many are renderer feature/resource fallback paths and need a separate shader residency policy pass, not a blind string replacement.

## Source URLs

- `https://docs.unity3d.com/ScriptReference/Renderer-sharedMaterials.html`
- `https://docs.unity.cn/ja/ScriptReference/Renderer.GetSharedMaterials.html`
- `https://docs.unity3d.com/cn/2023.2/ScriptReference/Renderer.SetSharedMaterials.html`
- `https://docs.unity3d.com/ja/6000.0/Manual/physics-optimization-raycasts-queries.html`
- `https://docs.unity3d.com/ja/6000.0/ScriptReference/Input.GetTouch.html`
- `https://docs.unity3d.com/es/530/ScriptReference/Input-touches.html`
- `https://docs.unity3d.com/ja/2023.2/ScriptReference/GameObject.CompareTag.html`
