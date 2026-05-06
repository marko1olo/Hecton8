# 2026-05-07 Native Collection Lifecycle Audit
Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: `Assets/_Project/Scripts` static Native collection and `NativeMemorySentinel` lifecycle sweep.

Mandates followed:

- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Audit Boundary

This document is a static source audit. It is not a Unity runtime leak dump, Memory Profiler capture, or proof that every disposal path executed in Play Mode.

## Command Evidence

Native allocation file sweep:

```powershell
$files = @(Get-ChildItem -LiteralPath 'Assets/_Project/Scripts' -Recurse -File -Filter '*.cs')
$allocRegex = 'new\s+Native(Array|List|HashMap|Queue|ParallelMultiHashMap|ParallelHashMap|Reference|Stream)<'
$allocFiles = foreach ($f in $files) {
  $txt = Get-Content -LiteralPath $f.FullName -Raw
  if ($txt -match $allocRegex) { $f }
}
```

Native allocation and sentinel token count:

```powershell
$totalAllocTokens = 0
$registerTokens = 0
$unregisterTokens = 0
$disposeHelperTokens = 0
$disposeDotTokens = 0
foreach ($f in $files) {
  $txt = Get-Content -LiteralPath $f.FullName -Raw
  if ($null -eq $txt) { $txt = '' }
  $totalAllocTokens += ([regex]::Matches($txt, $allocRegex)).Count
  $registerTokens += ([regex]::Matches($txt, 'NativeMemorySentinel\.RegisterNative')).Count
  $unregisterTokens += ([regex]::Matches($txt, 'NativeMemorySentinel\.UnregisterNative')).Count
  $disposeHelperTokens += ([regex]::Matches($txt, 'DisposeNative(Array|List|HashMap|Queue|ParallelMultiHashMap|ParallelHashMap)\s*\(')).Count
  $disposeDotTokens += ([regex]::Matches($txt, '\.Dispose\s*\(')).Count
}
```

Naive same-file risk detector:

```powershell
$noDispose = foreach ($f in $allocFiles) {
  $txt = Get-Content -LiteralPath $f.FullName -Raw
  if ($txt -notmatch '\.Dispose\s*\(' -and $txt -notmatch 'using\s*\(') { $f }
}
$regNoUnreg = foreach ($f in $files) {
  $txt = Get-Content -LiteralPath $f.FullName -Raw
  if ($txt -match 'NativeMemorySentinel\.RegisterNative' -and
      $txt -notmatch 'NativeMemorySentinel\.UnregisterNative') { $f }
}
```

## Static Results

| Metric | Count |
|---|---:|
| `.cs` files under `Assets/_Project/Scripts` | `1170` |
| Files with native allocation tokens | `204` |
| Native allocation token hits | `1120` |
| Files with `NativeMemorySentinel.RegisterNative*` tokens | `170` |
| `NativeMemorySentinel.RegisterNative*` token hits | `724` |
| `NativeMemorySentinel.UnregisterNative*` token hits | `517` |
| `DisposeNative*` helper token hits | `489` |
| Direct `.Dispose(` token hits | `952` |
| Naive same-file allocation-without-`.Dispose(` hits | `4` |
| Naive register-without-unregister same-file hits | `3` |

## Sentinel API Surface

`Assets/_Project/Scripts/Core/NativeMemorySentinel.cs` exposes these current registration surfaces:

- `RegisterNativeArray<T>`
- `RegisterNativeList<T>`
- `RegisterNativeListInstance<T>`
- `RegisterNativeHashMap<TKey, TValue>`
- `RegisterNativeParallelHashMap<TKey, TValue>`
- `RegisterNativeParallelHashMapInstance<TKey, TValue>`
- `RegisterNativeParallelHashSet<TKey>`
- `RegisterNativeParallelMultiHashMap<TKey, TValue>`
- `RegisterNativeQueue<T>`

It exposes matching unregister surfaces for arrays, lists, hash maps, parallel hash maps, parallel hash sets, parallel multi-hash maps, and queues.

## Review Items From Naive Detector

| File | Static finding | Current interpretation |
|---|---|---|
| `Assets/_Project/Scripts/Audio/Editor/DSPThreadSafetySmokeTester.cs` | Native allocation token without local `.Dispose(` | False-positive class: editor smoke tester scans source text strings such as `new NativeArray<...>` and asserts disposal text exists in another file. |
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | Native allocation and sentinel register tokens without same-file `UnregisterNative*` | Partial-class/delegated lifecycle class. Allocations are preceded by `DisposeNativeArray(...)`; shared disposal helpers and final teardown are in `HectonMapMagicVegetationBridge` / `VegetationMemoryPool` paths. Requires runtime sentinel dump to close. |
| `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs` | Native allocation tokens without direct `.Dispose(` | Uses `DisposeNativeArray(...)`, `DisposeNativeList(...)`, and chunk payload teardown helpers. Naive detector misses helper-based lifecycle. |
| `Assets/_Project/Scripts/World/VegetationPredatorFearField.cs` | Native allocation token without direct `.Dispose(` | Uses `DisposeNativeArray(...)` before persistent buffer replacement. Naive detector misses helper-based lifecycle. |
| `Assets/_Project/Scripts/VisualOmegaSmokeTester.cs` | Register token without unregister token | False-positive class: smoke tester scans source text for sentinel registration tokens. It is not the allocation owner. |
| `Assets/_Project/Scripts/Editor/PersistenceUxSmokeTester.cs` | Register token without unregister token | False-positive class: editor smoke tester scans source text for image encoding/sentinel usage. It is not the allocation owner. |

## Lifecycle Rule

Every new persistent Native allocation must document its lifecycle at the allocation site:

- allocation owner
- allocator type
- capacity or length source
- register call through `NativeMemorySentinel`
- matching dispose/unregister owner
- job dependency used for disposal when a scheduled job can still read/write the memory

Allocations using shared helpers still need the allocation-site comment plus a documented helper path. A helper name alone is not enough for future audits.

## Verdict

The static audit found broad sentinel adoption and documented helper-based disposal patterns, but it does not prove all allocations are leak-free at runtime.

Required closure evidence:

- Unity Play Mode run with `NativeMemorySentinel` leak dump after scene teardown.
- Memory Profiler snapshot or equivalent runtime allocation inventory.
- Follow-up source audit for partial classes where allocation and final disposal live in different files.

STATUS: PENDING VERIFICATION
