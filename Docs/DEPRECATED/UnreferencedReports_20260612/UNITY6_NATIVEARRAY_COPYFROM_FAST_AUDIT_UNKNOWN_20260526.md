# Unity 6 NativeArray CopyFromFast Audit - UNKNOWN - 2026-05-26

Evidence class: `STATIC_SOURCE_PLUS_LOCAL_UNITY_IL`

Project editor: `Unity 6000.4.1f1`, from `ProjectSettings/ProjectVersion.txt`.

## Verdict

Do not add a project-wide `CopyFromFast` / `CopyToFast` based on `[Il2CppSetOption(Option.NullChecks, false)]` and `[Il2CppSetOption(Option.ArrayBoundsChecks, false)]`.

The Jackson Dunstan 2018 article is technically valid for the Unity version it inspected, but it is stale for the installed Unity 6 copy path. In local Unity 6000.4.1f1 assemblies, `NativeArray<T>.CopyFrom(T[])` no longer performs a managed per-element index loop. It reaches a pinned managed array plus `UnsafeUtility.MemCpy`.

There was one real project action item, but it was not `Il2CppSetOption`: `ProximityColliderSystem` now uses range copy for the active logical status range because its DataVault buffer can be larger than `_pointCount`.

## Mandates Checked

- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

Relevant law:

- Hot path GC target is `0 B/frame`.
- Native memory work must preserve single owner, explicit lifetime, and DataVault route boundaries.
- Any system over `0.1 ms` requires profiler proof before optimization claims.
- Runtime DTO/native data must remain unmanaged and layout-stable.

## External Reference Boundary

Jackson Dunstan article inspected a Unity 2018.1.0b13 IL2CPP output where `CopyFrom`/`CopyTo` emitted per-element array access checks. That is not the current installed Unity 6 behavior verified below.

Unity official IL2CPP compiler option docs state that disabling null checks or array bounds checks can improve runtime performance for some projects, but invalid null or array access can crash or silently corrupt memory. Unity also requires copying the `Il2CppSetOptionAttribute.cs` source from the Editor installation into the project before use.

Unity official `NativeArray<T>.CopyFrom` scripting API states that `CopyFrom` copies all elements from another `NativeArray<T>` or a managed array of the same length.

## Project Search Results

Runtime script search under `Assets/_Project/Scripts`:

| Pattern | Count |
|---|---:|
| `CopyFrom` | 7 |
| `CopyTo` | 149 |
| `NativeArray.Copy` | 1 |
| `GCHandle.Alloc` | 0 |
| `Il2CppSetOption` | 0 |
| `UnsafeUtility.MemCpy` | 197 |
| `UnsafeUtility.MemMove` | 8 |
| `NativeArrayUnsafeUtility.GetUnsafePtr` | 659 |
| `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` | 521 |
| `GetUnsafeBufferPointerWithoutChecks` | 576 |

Direct `NativeArray.CopyFrom` call sites:

| File | Line | Source | Classification | Recommendation |
|---|---:|---|---|---|
| `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs` | 229 | `binary.CopyFrom(managedBytes)` | Editor-only import path | Leave. No runtime gain. |
| `Assets/_Project/Scripts/ProximityColliderSystem.cs` | 294 | `positions.CopyFrom(worldPositions)` | NativeArray to NativeArray during init/reinit | Leave unless range mismatch is proven. Not managed-array issue. |
| `Assets/_Project/Scripts/ProximityColliderSystem.cs` | 612 | `NativeArray<byte>.Copy(_prevStatus, 0, prevStatus, 0, _pointCount)` | Repeated runtime managed byte array to NativeArray copy | Fixed with range copy. Do not add `CopyFromFast`; native truth ownership remains optional only with profiler proof. |
| `Assets/_Project/Scripts/SaveManager.cs` | 5058 | `persistentWorldItemBuffer.CopyFrom(persistentWorldItems)` | Cold save staging | Leave. Exact Temp allocation, not frame path. |
| `Assets/_Project/Scripts/SaveManager.cs` | 5069 | `ecosystemSectorBuffer.CopyFrom(ecosystemSectorStates)` | Cold save staging | Leave. Exact Temp allocation, not frame path. |
| `Assets/_Project/Scripts/SaveManager.cs` | 5080 | `packedQuestStateBuffer.CopyFrom(packedQuestStateWords)` | Cold save staging | Leave. Exact Temp allocation, not frame path. |

The seventh `CopyFrom` match is a comment in `ProximityColliderSystem.cs`.

## Local Unity 6 IL Proof

Assembly inspected:

`C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\Managed\UnityEngine\UnityEngine.CoreModule.dll`

Editor/development managed assembly path for `NativeArray<T>.Copy(T[], NativeArray<T>)`:

```text
Copy(T[], NativeArray<T>)
  CheckCopyLengths(source.Length, dst.Length)
  CopySafe(source, 0, dst, 0, source.Length)

CopySafe(T[], int, NativeArray<T>, int, int)
  AtomicSafetyHandle.CheckWriteAndThrow(dst.m_Safety)
  CheckCopyPtr(source)
  CheckCopyArguments(source.Length, srcIndex, dst.Length, dstIndex, length)
  GCHandle.Alloc(source, GCHandleType.Pinned)
  AddrOfPinnedObject()
  UnsafeUtility.MemCpy(dst.m_Buffer + dstIndex * sizeof(T),
                       pinnedSource + srcIndex * sizeof(T),
                       length * sizeof(T))
  GCHandle.Free()
```

Assembly inspected:

`C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\il2cpp\Managed\UnityEngine.CoreModule.dll`

IL2CPP player managed assembly strips the editor safety calls, but still uses the same pinned-array `MemCpy` route:

```text
CopyFrom(T[])
  Copy(T[], NativeArray<T>)

Copy(T[], NativeArray<T>)
  CopySafe(source, 0, dst, 0, source.Length)

CopySafe(T[], int, NativeArray<T>, int, int)
  GCHandle.Alloc(source, GCHandleType.Pinned)
  AddrOfPinnedObject()
  UnsafeUtility.MemCpy(dst.m_Buffer + dstIndex * sizeof(T),
                       pinnedSource + srcIndex * sizeof(T),
                       length * sizeof(T))
  GCHandle.Free()
```

This directly rejects the old concern that every element copy pays a managed array null/bounds check in Unity 6.

## Profit Analysis

Expected performance win from adding old-style `CopyFromFast` is negligible to negative:

- Unity 6 already performs bulk `MemCpy` for managed array to `NativeArray<T>`.
- `Il2CppSetOption` only affects IL2CPP-generated C++ checks. It does not help Burst jobs, Mono editor behavior, or existing `UnsafeUtility.MemCpy` call sites.
- The project has only one repeated runtime managed-array `CopyFrom` candidate.
- Default `ProximityColliderSystem` documented scale is `10,000` points: `_prevStatus` copy is about `10 KB` per scheduled frame.
- At `60 FPS`, that is about `600 KB/s`, before considering that the system often waits on the previous scheduled job. This is not a credible top-frame target without profiler proof.
- The main `ProximityColliderSystem` costs are more likely the point-distance job, full `_pointCount` result scan, object pool spawn/despawn work, and GameObject/Collider churn, not the `10 KB` status copy.

Expected correctness/profiling value from a custom utility:

- Low as performance optimization.
- Medium if used only to centralize telemetry through `UnsafeMemoryCopyGuard`.
- Negative if it introduces unchecked global copying with IL2CPP-only attributes.

## Real Finding: Proximity Buffer Length Boundary

`ProximityColliderSystem.EnsureProximityVaultBuffer` accepts an existing DataVault buffer when `existing.Length >= requiredCount`.

`prevStatus.CopyFrom(_prevStatus)` requires same length in editor/development managed assemblies. If the system is initialized with a larger point count and later reinitialized with a smaller point count while reusing the larger DataVault buffer, editor/development `CopyFrom` can throw `ArgumentException: source and destination length must be the same`.

The installed IL2CPP player variation strips that length check and copies only source length bytes. That creates behavior drift between editor and IL2CPP player.

Implemented minimal source change:

```csharp
NativeArray<byte>.Copy(_prevStatus, 0, prevStatus, 0, _pointCount);
```

That preserves Unity's official bulk-copy route, handles `destination.Length >= _pointCount`, and does not require `Il2CppSetOption`.

Recommended architecture change if profiler proves this path matters:

- Remove managed `_prevStatus` as a mirrored truth source.
- Keep applied collider status in one native owner buffer.
- Process job results against that native applied-status buffer.
- Update the applied-status buffer only when the spawn/despawn operation is actually accepted by `maxOperationsPerTick`.
- Preserve the current operation-cap semantics; do not let a job mark a status applied before the GameObject pool operation has happened.

This is a medium-risk refactor because `_prevStatus` currently represents "applied collider state", not merely last job output.

## Implementation Decision

Do not implement:

- `CopyFromFast`
- `CopyToFast`
- project-wide `Il2CppSetOption`
- broad replacement of `CopyFrom`

Already implemented:

1. Low-risk Proximity correctness patch: replaced `prevStatus.CopyFrom(_prevStatus)` with range `NativeArray.Copy`.

Implement only if needed later:

1. Optional guard utility only after profiler proof or repeated call-site growth:
   - `UnsafeMemoryCopyGuard.CopyFromManagedArray<T>(NativeArray<T> destination, T[] source, int count) where T : unmanaged`
   - It must do length/pointer validation once and call the existing guarded copy route.
   - It must not use `Il2CppSetOption` as a blanket safety bypass.

## Post-Patch CLI Build Verification

After applying the Proximity range-copy correction, the first CLI build passed but exposed unrelated `CS0420` warnings in `PlayerCriticalProceduralAudioRenderer.cs` where code called `Volatile.Read(ref _targetGranularMaxVoiceCount)` on an already `volatile int` field.

Correction applied:

```csharp
_targetGranularMaxVoiceCount
```

This preserves the volatile field read and removes the invalid by-ref warning surface.

Final CLI proof:

- Command: `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- Log: `Docs/Reports/BUILD_UNKNOWN_COPYFROMFAST_PATCH_RECHECK_20260526.log`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)` at lines `66-68`.

## Verification Still Missing

No Unity player build, IL2CPP generated C++ diff, or profiler capture was produced in this audit. The current verdict is based on:

- official Unity documentation,
- local Unity 6000.4.1f1 installed assemblies,
- Mono.Cecil IL inspection,
- static project source search.
- clean `.slnx` CLI compile after the source corrections.

Runtime microseconds saved claimed: `0`.
