# Unity API Trap Detector Audit - UNKNOWN - 2026-05-26

Evidence class: `STATIC_SOURCE_PLUS_UNITY_DOCS`

## Verdict

`UnityApiTrapDetector` was too broad. It treated any textual `.material` or `.vertices` as a Unity runtime trap, regardless of the owning API type or whether the line was a documented cold allocation.

That created false compliance debt and could send agents into UI/TMP/DTO code that is not the `Renderer.material` material-instancing problem.

## Mandates Checked

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`

First-20-minutes impact: removes false compliance noise around UI/visor/loading-screen systems. This does not add gameplay content.

## Unity Reference Boundary

Unity documentation confirms the real traps:

- `Renderer.materials` returns a copy and automatically instantiates materials for that renderer.
- `Renderer.material` access can automatically instantiate a material instance.
- `Mesh.vertices` getter returns a copy of vertex positions.
- `UnityEngine.UI.Graphic.material` is the material set by the user, not the same API as `Renderer.material`.

Therefore a static detector must distinguish API type or it will over-report.

## Before

Local mimic of the old detector behavior over runtime scripts:

| Rule | Count |
|---|---:|
| `.material` | 68 |
| `.vertices` | 14 |
| Total | 82 |

Representative false positives:

- `chunk.materialIds`
- `characterInfo.materialReferenceIndex`
- `fontAsset.material`
- `Graphic image.material`
- `Image _savingProgressDataLamp.material`
- `RenderGraph passData.material`
- `mesh.vertices = ...` setters
- `mesh.vertices` getter with canonical `COLD ALLOC:` annotation

## Change Applied

File changed:

- `Assets/_Project/Scripts/Editor/UnityApiTrapDetector.cs`
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`

Detector changes:

- Uses exact member matching, so `materialIds` and `materialReferenceIndex` no longer match `.material`.
- Builds a simple per-file type index for known non-renderer material owners:
  - `Graphic`
  - `MaskableGraphic`
  - `Image`
  - `RawImage`
  - `TMP_Text`
  - `TextMeshProUGUI`
  - `TMP_FontAsset`
- Ignores known UI/TMP/RenderGraph material lines.
- Flags `Mesh.vertices` getter, not `mesh.vertices = ...` setter.
- Honors canonical `COLD ALLOC:` annotations for detector rules where the project mandate allows documented cold allocation.

Runtime source cleanup:

- Replaced the remaining `VehicleSubOsCockpitRuntime` `mesh.vertices` getter with a reusable `List<Vector3>` plus `mesh.GetVertices(_damageProxySourceVertices)`.
- This removes the Unity managed-array copy from the cockpit damage proxy setup path.
- The retained allocation is an explicit reusable cold list: `List<Vector3>(MaxDamageHologramPoints)`.

## After

Local mimic of the patched detector behavior over runtime scripts:

| Rule | Count |
|---|---:|
| `Input.touches` | 0 |
| `Renderer.material(s)` heuristic | 0 |
| `Mesh.vertices` hot getter heuristic | 0 |
| Total | 0 |

This means the current runtime source has no unwaived hits under this detector. It does not prove runtime GC is zero; it only proves this specific static detector is no longer reporting known false positives.

## Verification

CLI build:

- Guard before final build: CPU `14.8%`, `dotnet=0`, `csc=0`.
- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Log: `Docs/Reports/BUILD_UNKNOWN_UNITY_API_TRAP_DETECTOR_MESH_RECHECK_20260526.log`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.

Still not run:

- Unity Editor menu execution of `Hecton-8/Compliance/Scan Unity API Traps`.

Documentation gates:

- `python Tools/VerifyDocStructure.py`: `pass=true`, `activeDocCount=710`, `encodingWithoutUtf8Sig=0`.
- `python Tools/OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=710`, `sourceSyncPass=true`.

Runtime microseconds saved claimed: `0`.
