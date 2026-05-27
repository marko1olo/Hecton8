# Unity Addressables Lifecycle Trap Pass - UNKNOWN - 2026-05-26

## Verdict

Real source defect fixed in a clean owner file:
`Assets/_Project/Scripts/ItemCatalog.cs`.

The item world-prefab route had two lifecycle faults:

- `Queued` world-prefab records were checked for a valid handle before the queued state was handled.
- Failed or null-result Addressables loads marked the record failed but did not release the failed handle.

This is asset-lifecycle correctness, not a measured frame-time optimization.
Runtime microseconds saved claimed: `0`.

## External Proof Checked

- Addressables package in this project: `com.unity.addressables` `2.7.6`.
- Unity Addressables 2.7 async handles:
  https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/AddressableAssetsAsyncOperationHandle.html
- Unity Addressables 2.7 asset memory:
  https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/memory-assets.html
- Unity Resources.UnloadUnusedAssets, Unity 6.0:
  https://docs.unity3d.com/ja/current/ScriptReference/Resources.UnloadUnusedAssets.html
- Reddit/Unity community material was used only as anecdotal signal.
  Decisions in this pass use Unity docs and local source as proof.

## Static Scan Results

Command scope:

```powershell
rg -n "Resources\.(Load|LoadAll|UnloadUnusedAssets|UnloadAsset)|Addressables\.(LoadAssetAsync|LoadAssetsAsync|InstantiateAsync|Release|ReleaseInstance|UnloadSceneAsync)|\.WaitForCompletion\(" Assets/_Project/Scripts --glob "*.cs" --glob "!Editor/**" --glob "!**/Editor/**"
```

First-party runtime result after the patch:

| Surface | Current result | Decision |
|---|---:|---|
| `Resources.Load/LoadAll/UnloadUnusedAssets/UnloadAsset` | `0` in `Assets/_Project/Scripts` runtime scope | No first-party source edit needed. |
| `WaitForCompletion()` | `0` in the same runtime scope | No sync Addressables wait trap found. |
| `Addressables.LoadAssetAsync` | `1` direct string route in `AssetLifecycleGovernor` | Keep; central tracked owner. |
| `Addressables.Release` | `AssetLifecycleGovernor` plus guarded `ItemCatalog` fallback | Keep; release owner route now closed. |
| `Addressables.ReleaseInstance` | `GameBootstrapper` dirty working-tree file | Not touched in this pass. |

Third-party/vendor Crest runtime `Resources.Load` surface remains:

| File | Hit |
|---|---|
| `Assets/Crest/Crest/Scripts/Helpers/ComputeShaderHelpers.cs` | `Resources.Load<ComputeShader>(path)` |
| `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs` | `Resources.Load<ComputeShader>("FFT/FFTSpectrum")` |
| `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTCompute.cs` | `Resources.Load<ComputeShader>("FFT/FFTCompute")` |
| `Assets/Crest/Crest/Scripts/Shapes/FFT/FFTBaker.cs` | `Resources.Load<ComputeShader>("FFT/FFTBake")` |

Vendor Crest was not edited without an explicit owner task.

## Source Changes

| File | Change |
|---|---|
| `Assets/_Project/Scripts/ItemCatalog.cs` | `TryGetLoadedWorldPrefab` now preserves `Queued` as pending instead of treating its missing handle as failure. |
| `Assets/_Project/Scripts/ItemCatalog.cs` | Queued/loading pending records now persist fresh `LastAccessFrame`/AUP touch state before returning `false`. |
| `Assets/_Project/Scripts/ItemCatalog.cs` | Failed or null-result Addressables handles now route through `FailWorldPrefabLoad`. |
| `Assets/_Project/Scripts/ItemCatalog.cs` | Failed tracked handles release through `AssetLifecycleGovernor.ReleaseAddressableAsset`. |
| `Assets/_Project/Scripts/ItemCatalog.cs` | Untracked failed handles use `TryReleaseExternalAddressableFault`; direct `Addressables.Release` is only the governor-missing fallback. |

## Why This Was Correct

`QueueWorldPrefabPrewarm` constructs queued records with `LoadState = Queued`,
`DispatchRequestId`, and `DispatchAssetKey`, but without an Addressables handle.
That is the expected state before `AssetLoadDispatcher` consumes the ticket.

Checking `Handle.IsValid()` before `Queued` made a pending dispatcher ticket look like
a failed load. The corrected order preserves the dispatcher lifecycle.

Unity Addressables 2.7 states that failed operations should still release the handle.
The previous failed/null-result branch acknowledged the dispatcher request but left
the failed `AsyncOperationHandle<GameObject>` resident in the record.

The fix keeps the project route:

- tracked asset key -> `AssetLifecycleGovernor.ReleaseAddressableAsset`
- external/untracked handle -> `TryReleaseExternalAddressableFault`
- no governor available -> direct `Addressables.Release`

## Non-Fixes With Proof

- `GameBootstrapper.cs` is dirty from other agents; not edited.
- `WorldChunkResidencyManager.cs` is dirty from other agents; not edited.
- Crest `Resources.Load<ComputeShader>` remains third-party vendor surface.
- Runtime async upload setting writers in bootstrap/world streaming dirty files were not touched.
- No player build, Unity import, Play Mode, profiler, GCMonitor, or Memory Profiler proof was produced.

## Validation

| Check | Result |
|---|---|
| Scoped `git diff --check` | Passed with LF/CRLF warning only. |
| CLI build | `Docs/Reports/BUILD_UNKNOWN_ADDRESSABLES_LIFECYCLE_TRAP_20260526.log`; exit `0`; `0 Warning(s)`; `0 Error(s)` at lines `66-68`. |
| Documentation structure gate | `python Tools/VerifyDocStructure.py` passed; `activeDocCount=696`; `encodingWithoutUtf8Sig=0`. |
| OOP documentation scanner | `python Tools/OOP_Doc_Scanner.py` passed; `finalPass=true`; `activeFileCount=696`; `sourceSyncPass=true`. |
