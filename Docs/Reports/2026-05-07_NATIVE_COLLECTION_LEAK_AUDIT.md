# Native Collection Leak Audit
Date: 2026-05-07
Status: PENDING VERIFICATION (BLOCKED BY MCP)
Scope: source-text audit of `Assets/_Project/Scripts` native collection allocation, sentinel registration, and disposal lifecycle evidence

Mandates followed:

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## Command Evidence

Editor scanner added for this pass:

- `Assets/_Project/Scripts/Editor/NativeLeakScanner.cs`
- menu: `Hecton8/Audit/Native Leak Scanner`
- output target when Unity can execute it: `CodexArtifacts/native-leak-scanner-results.json`
- source-equivalent artifact from this blocked-MCP pass: `CodexArtifacts/native-leak-scanner-results.static.json`
- MCP execution status on 2026-05-07: `PENDING VERIFICATION`; `refresh_unity` timed out after 60 seconds and latest `read_console` returned `Unity session not ready` / `ping not answered`.
- local syntax compile status: clean with Unity 6000.4.1f1 Mono `csc.exe`, `UNITY_EDITOR`, `UnityEditor.dll`, `UnityEngine.CoreModule.dll`, and Mono `4.8-api/Facades/netstandard.dll`.

Local syntax compile command:

```powershell
$mono = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\MonoBleedingEdge\bin\mono.exe'
$csc = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\MonoBleedingEdge\lib\mono\4.5\csc.exe'
$out = Join-Path $env:TEMP 'NativeLeakScanner.syntax.dll'
$unityEngine = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'
$unityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Managed\UnityEditor.dll'
$netstandard = 'C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\MonoBleedingEdge\lib\mono\4.8-api\Facades\netstandard.dll'
& $mono $csc -nologo -target:library -define:UNITY_EDITOR -out:$out -r:$unityEngine -r:$unityEditor -r:$netstandard Assets\_Project\Scripts\Editor\NativeLeakScanner.cs
```

Equivalent source-text execution used the same same-file rule:

```powershell
$root = 'Assets/_Project/Scripts'
$allocPattern = '\bnew\s+(NativeArray|NativeList|NativeQueue|NativeHashMap|NativeParallelHashMap|NativeReference)<'
$files = rg --files $root -g '*.cs'
foreach ($file in $files) {
  $text = [System.IO.File]::ReadAllText((Resolve-Path $file))
  $matches = [regex]::Matches($text, $allocPattern)
  if ($matches.Count -eq 0) { continue }
  $hasDirectDispose = [regex]::IsMatch($text, '\.Dispose\s*\(')
  $hasHelper = [regex]::IsMatch($text, '\bDisposeNative\w*\s*\(')
  $hasRegister = [regex]::IsMatch($text, 'NativeMemorySentinel\.RegisterNative')
  $hasUnregister = [regex]::IsMatch($text, 'NativeMemorySentinel\.(UnregisterNative|Unregister)')
}
```

The scanner now masks comments and string/char literals before regex matching, so text assertions such as `"new NativeArray<T>"` are not treated as allocation sites.

Final inquisition patch: `NativeLeakScanner.cs` no longer uses `Regex.Matches` or `MatchCollection` for allocation detection. Allocation counting now uses the manual token scanner at `NativeLeakScanner.cs:110-138`; disposal/helper checks use manual call scans at `NativeLeakScanner.cs:174-225`. See `Docs/Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`.

Allocation scan:

```powershell
$patterns = @('new NativeArray<','new NativeList<','new NativeQueue<','new NativeHashMap<','new NativeParallelHashMap<','UnsafeList<')
foreach ($p in $patterns) {
  [pscustomobject]@{ Pattern = $p; Count = (rg -n -F $p Assets/_Project/Scripts -g '*.cs' | Measure-Object).Count }
}
```

Sentinel/disposal scan:

```powershell
rg -n "NativeMemorySentinel\.RegisterNativeArray" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.RegisterNativeList" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.RegisterNativeQueue" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.RegisterNative.*Hash" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.UnregisterNativeArray" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.UnregisterNativeList" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "NativeMemorySentinel\.UnregisterNativeQueue" Assets/_Project/Scripts -g "*.cs" | Measure-Object
rg -n "\.Dispose\(" Assets/_Project/Scripts -g "*.cs" | Measure-Object
```

## Counts

| Pattern | Count |
|---|---:|
| `new NativeArray<` | `826` |
| `new NativeList<` | `74` |
| `new NativeQueue<` | `162` |
| `new NativeHashMap<` | `37` |
| `new NativeParallelHashMap<` | `19` |
| `UnsafeList<` | `0` |
| `NativeMemorySentinel.RegisterNativeArray` | `461` |
| `NativeMemorySentinel.RegisterNativeList` | `54` |
| `NativeMemorySentinel.RegisterNativeQueue` | `157` |
| `NativeMemorySentinel.RegisterNative*Hash*` | `74` |
| `NativeMemorySentinel.UnregisterNativeArray` | `289` |
| `NativeMemorySentinel.UnregisterNativeList` | `37` |
| `NativeMemorySentinel.UnregisterNativeQueue` | `159` |
| `.Dispose(` calls under scripts | `958` |

Additional May 7 static detector:

| Metric | Count |
|---|---:|
| `.cs` files under `Assets/_Project/Scripts` | `1192` |
| Raw files with native allocation text tokens | `208` |
| Code-aware files with native allocation tokens | `207` |
| Code-aware native allocation token hits | `1116` |
| Files with `NativeMemorySentinel.RegisterNative*` tokens | `175` |
| `NativeMemorySentinel.RegisterNative*` token hits | `742` |
| `NativeMemorySentinel.UnregisterNative*` token hits | `534` |
| `NativeMemorySentinel.Unregister*` token hits | `544` |
| `DisposeNative*` helper token hits | `621` |
| Direct `.Dispose(` token hits | `958` |
| Code-aware same-file allocation-without-direct-`.Dispose(` hits | `3` |
| Naive register-without-unregister same-file hits | `1` |

## Sentinel Usage Summary

`Assets/_Project/Scripts/Core/NativeMemorySentinel.cs` is the active ownership registry. It stores fixed-capacity `NativeAllocationRecord` entries and exposes typed register/unregister helpers for arrays, lists, queues, and hash containers.

Source evidence shows broad adoption:

- event buses register persistent `NativeQueue<T>` lanes and unregister/dispose them in reset or teardown paths.
- audio, voxel, vegetation, UI projection, map, wreck, and simulation systems register persistent `NativeArray<T>` buffers with named owners and lifetimes.
- `PhysicalHandController` finger spherecast/pose `NativeArray<T>` buffers are registered through `NativeMemorySentinel.RegisterNativeArray(...)` and released through `DisposePersistentBuffers()` / `DisposeNativeArray(...)` in `OnDestroy`.
- multiple systems use `Dispose(JobHandle)` for deferred disposal when producer jobs can still own the container.

## TempJob Lifetime Law

All TempJob allocations have a max lifetime of 4 frames.

Static scan command:

```powershell
rg -n "Allocator\.TempJob" Assets/_Project/Scripts -g "*.cs"
```

Strict interpretation:

- `Allocator.TempJob` is allowed only for same-frame or explicitly bounded short-lived job payloads.
- Any `Allocator.TempJob` payload awaited across multiple frames must prove completion and disposal before the fourth frame boundary.
- `UnsafeUtility.Malloc(..., Allocator.TempJob)` inside BRG culling callback output is Unity callback-owned memory; it must be isolated to the callback contract and never stored by first-party systems.
- Source-text review cannot prove runtime frame lifetime by itself; runtime Sentinel capture remains required before declaring leak freedom.

## Violations

No strict 4-frame TempJob lifetime violation was proven by this source-text pass.

Review items retained:

| File | Reason |
|---|---|
| `Assets/_Project/Scripts/World/HectonBatchRendererGroupUtility.cs` | Uses `UnsafeUtility.Malloc(..., Allocator.TempJob)` for `BatchCullingOutputDrawCommands`. The code states Unity owns the callback memory after return. Runtime BRG callback validation is still required. |
| `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` | Uses TempJob scatter payloads and disposes them in `finally`; async path depends on `WaitForJobHandleAsync(...)` finishing before disposal. Runtime frame-count proof is still required. |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | Uses TempJob culling scratch arrays in BRG culling callback path. Disposal sites exist, but runtime callback cadence proof is still required. |

## Naive Detector Review Items

The same-file detector is deliberately blunt. Current review items are:

| File | Static finding | Current interpretation |
|---|---|---|
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | Native allocation and sentinel register tokens without same-file `UnregisterNative*` | Partial-class/delegated lifecycle. Allocations are preceded by `DisposeNativeArray(...)`; final teardown is delegated through vegetation bridge/memory-pool paths. Runtime sentinel dump still required. |
| `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs` | Native allocation tokens without direct `.Dispose(` | Uses `DisposeNativeArray(...)`, `DisposeNativeList(...)`, and chunk payload teardown helpers. |
| `Assets/_Project/Scripts/World/VegetationPredatorFearField.cs` | Native allocation token without direct `.Dispose(` | Uses `DisposeNativeArray(...)` before persistent buffer replacement. |

Static disposition: every reviewed static hit has a documented helper/partial-class disposal path. This is not runtime leak freedom.

## Leak Audit Boundary

This pass verifies documented source patterns, not runtime leak freedom.

Hard facts:

- Native allocation tokens outnumber sentinel registration tokens because scan counts include mesh-data aliases, generic helper methods, temp smoke arrays, copy paths, and non-owning `GetVertexData<T>()`/`GetIndexData<T>()` aliases.
- Disposal tokens are present across the same source surface, but a text scan cannot prove every dynamic branch reaches disposal after every failure mode.
- `NativeMemorySentinel` exists and is used widely enough to be the required audit seam for future native-memory surgery.

Required follow-up before claiming leak-free runtime:

1. Run `NativeMemorySentinel` runtime report after scene load, after 10 minutes idle, after streaming churn, and after scene unload.
2. Diff live record count and total bytes across the four captures.
3. For every remaining live allocation after unload, map `owner` and `label` back to source and either prove expected lifetime or patch disposal.

Status: PENDING VERIFICATION (BLOCKED BY MCP)
