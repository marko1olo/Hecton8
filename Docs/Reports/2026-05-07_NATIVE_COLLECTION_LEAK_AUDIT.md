# Native Collection Leak Audit
Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-text audit of `Assets/_Project/Scripts` native collection allocation, sentinel registration, and disposal lifecycle evidence

Mandates followed:

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## Command Evidence

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
| `new NativeArray<` | `818` |
| `new NativeList<` | `70` |
| `new NativeQueue<` | `161` |
| `new NativeHashMap<` | `37` |
| `new NativeParallelHashMap<` | `19` |
| `UnsafeList<` | `0` |
| `NativeMemorySentinel.RegisterNativeArray` | `452` |
| `NativeMemorySentinel.RegisterNativeList` | `50` |
| `NativeMemorySentinel.RegisterNativeQueue` | `156` |
| `NativeMemorySentinel.RegisterNative*Hash*` | `73` |
| `NativeMemorySentinel.UnregisterNativeArray` | `279` |
| `NativeMemorySentinel.UnregisterNativeList` | `35` |
| `NativeMemorySentinel.UnregisterNativeQueue` | `158` |
| `.Dispose(` calls under scripts | `955` |

## Sentinel Usage Summary

`Assets/_Project/Scripts/Core/NativeMemorySentinel.cs` is the active ownership registry. It stores fixed-capacity `NativeAllocationRecord` entries and exposes typed register/unregister helpers for arrays, lists, queues, and hash containers.

Source evidence shows broad adoption:

- event buses register persistent `NativeQueue<T>` lanes and unregister/dispose them in reset or teardown paths.
- audio, voxel, vegetation, UI projection, map, wreck, and simulation systems register persistent `NativeArray<T>` buffers with named owners and lifetimes.
- multiple systems use `Dispose(JobHandle)` for deferred disposal when producer jobs can still own the container.

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

STATUS: PENDING VERIFICATION
