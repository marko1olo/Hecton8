# Unity Runtime Trap Deep Pass - UNKNOWN - 2026-05-26

## Verdict

This pass found no first-party release `AsyncGPUReadbackRequest.WaitForCompletion()` trap.

First-party async readbacks reviewed are mostly using the right shape: poll `done` or `SystemDispatcher.IsAsyncReadbackReadyNoWait`, then call `GetData<T>()`. That is not the same problem as synchronous CPU/GPU readback.

Real fix applied: removed release-player `Shader.Find` fallback reachability from four clean runtime files that create visible materials from code.

## External Proof Checked

- Unity `Shader.Find`: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Shader.Find.html
- Unity shader loading and first-use stalls: https://docs.unity.cn/Manual/shader-loading.html
- Unity `AsyncGPUReadbackRequest.WaitForCompletion`: https://docs.unity.cn/ScriptReference/Rendering.AsyncGPUReadbackRequest.WaitForCompletion.html
- Unity `Texture2D.ReadPixels`: https://docs.unity3d.com/ScriptReference/Texture2D.ReadPixels.html
- Unity `GraphicsBuffer.LockBufferForWrite`: https://docs.unity3d.com/ja/2022.1/ScriptReference/GraphicsBuffer.LockBufferForWrite.html

## Static Scan Results

| Trap class | Result | Decision |
|---|---:|---|
| `AsyncGPUReadbackRequest.WaitForCompletion()` | `0` first-party/Crest runtime hits in searched script roots | No source fix needed. |
| First-party `AsyncGPUReadbackRequest.GetData<T>()` | Present after `done`/readiness checks in reviewed files | Keep; no synchronous wait was introduced. |
| First-party `ComputeBuffer.SetData()` | Only found in `Assets/_Project/Scripts/Dev/HabitatStressSmokeTester.cs` | Keep; dev smoke harness, not gameplay/render hot path. |
| `Texture2D.ReadPixels()` | No hot first-party release path found; vendor Crest/editor capture paths remain | Do not edit vendor Crest without explicit owner task. |
| `Shader.Find()` in selected clean runtime files | Release-reachable in tether/sonar/visor/dry-volume fallback material creation | Fixed. |

## Source Changes

| File | Change |
|---|---|
| `Assets/_Project/Scripts/TetherManager.cs` | Added authored `tetherRenderShader`; editor auto-loads exact shader asset; `Shader.Find` is editor/development only. |
| `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs` | Existing serialized `sonarMapShader` is now the release route; name lookup is editor/development only; `OnValidate` auto-assigns the asset. |
| `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs` | Added authored `fallbackShader`; editor auto-loads exact shader asset; release fallback no longer name-searches the shader. |
| `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs` | Added editor exact-path assignment for three dry-volume shaders; release creation no longer name-searches hidden shaders. |

## Why This Was The Correct Fix

Unity documents that `Shader.Find` can work in the Editor but fail in a player build if no asset references the shader. The selected files were not dirty from other agents and are visible first-person/vehicle UI or cable visuals.

Global `Always Included Shaders` was rejected because it hides ownership in project settings. Per-owner serialized references keep ownership local and visible.

## Non-Fixes With Proof

- GPU readback: no `WaitForCompletion()` was found. The reviewed `GetData<T>()` sites are gated by request completion and are not the sync-stall pattern.
- `ReadPixels`: the first-party Crest bridge PNG path is editor/development gated and runtime depth-cache camera is disabled. Vendor Crest capture code remains third-party surface.
- `GlobalRegistry`/scene search: broad scan found no `Camera.main` runtime polling and no first-party `FindObjectOfType`/`GameObject.Find` hits in searched runtime roots.

## Validation

| Check | Result |
|---|---|
| Touched `Shader.Find` release reachability | `false` for all six touched calls. |
| Scoped `git diff --check` | Passed; line-ending warnings only. |
| CLI build | Passed after guard allowed launch: `Docs/Reports/BUILD_UNKNOWN_RUNTIME_TRAP_DEEP_PASS_20260526.log`; exit `0`; `0 Warning(s)`; `0 Error(s)` at lines `66-68`. Earlier guard block at `2026-05-26 15:30:51` was CPU `99%` with active `dotnet=53348` and `csc=46404`. |
| Documentation structure gate | `python Tools/VerifyDocStructure.py` passed; `activeDocCount=695`; `encodingWithoutUtf8Sig=0`. |
| OOP documentation scanner | `python Tools/OOP_Doc_Scanner.py` passed; `finalPass=true`; `activeFileCount=695`; `sourceSyncPass=true`. |

## Remaining Debt

This is not a project-wide shader cleanup. Dirty files owned by other agents still include shader lookup debt, notably `DroneFleetManager.cs` and `HectonVoxelEngine.cs`. Render-feature shader assets also need a separate serialized renderer-data pass, not a blind C# edit.
