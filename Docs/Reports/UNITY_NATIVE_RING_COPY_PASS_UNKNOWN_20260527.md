# UNKNOWN Native Ring Copy Pass - 2026-05-27

Status: SOURCE PATCHED, STATIC PROOF PASSED, BUILD BOUNDARY BLOCKED BEFORE C#, DOC GATES PASSED
Agent: UNKNOWN
Evidence class: STATIC_SOURCE / STATIC_TEST_SOURCE / CLI_BUILD_BOUNDARY / DOC_VALIDATION

## Problem

`GlobalTelemetryBus` exported the black-box telemetry ring by copying each retained event through:

- one wrap-mask calculation per element
- one `NativeRingBuffer<T>` indexer read per element
- one `NativeArray<T>` write per element

This is exactly the kind of copy path where an unsafe custom `CopyFromFast` would be tempting. The safer project-local fix is to use Unity's built-in `NativeArray<T>.Copy` over one or two contiguous ring segments.

Unity documents `NativeArray<T>.Copy` as a range copy from source index to destination index. Unity also documents `GraphicsBuffer.LockBufferForWrite` as a lower-copy upload path for GPU buffers, but this pass is CPU-native telemetry memory, not GPU upload.

## Changed Files

| File | Change |
|---|---|
| `Assets/_Project/Scripts/Core/NativeRingBuffer.cs` | Added `CopyRange(..., destinationStartIndex)` and changed range copy to one or two `NativeArray<T>.Copy` calls. |
| `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs` | Replaced snapshot-copy per-element loops with `NativeRingBuffer<T>.CopyRange`. |
| `Assets/_Project/Tests/Editor/NativeRingBufferEditTests.cs` | Added edit tests for chronological wrapped copy and destination-offset copy. |
| `Assets/_Project/Tests/Editor/NativeRingBufferEditTests.cs.meta` | Added Unity meta for the new test source file. |

## Why This Direction

Rejected custom unsafe copy:

- `[Il2CppSetOption]` would only affect IL2CPP-generated checks around managed C# code; it does not prove a better route than Unity's own native-container copy primitive.
- An unsafe pointer copy would need extra proof for safety handles, wrap segmentation, and editor/player behavior.
- `NativeArray<T>.Copy` keeps safety contracts and removes the per-element ring-index loop in the telemetry export path.

## Static Proof

Targeted residual scan:

```text
rg -n "_snapshotBuffer\[|_ringBuffer\[|CapacityMask|for \(int i = 0; i < (copyCount|totalCount)" \
  Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs \
  Assets/_Project/Scripts/Core/NativeRingBuffer.cs

NO_HITS
```

Copy route locations:

```text
NativeRingBuffer.cs:137 NativeArray<T>.Copy(_buffer, sourceIndex, destination, destinationStartIndex, firstCopyCount);
NativeRingBuffer.cs:141 NativeArray<T>.Copy(_buffer, 0, destination, destinationStartIndex + firstCopyCount, remainingCount);
GlobalTelemetryBus.cs:848 _ringBuffer.CopyRange(..., _snapshotCopiedCount);
GlobalTelemetryBus.cs:859 _ringBuffer.CopyRange(startIndex, totalCount, _snapshotBuffer);
```

Brace proof:

```text
NativeRingBuffer.cs brace_balance 0 lines 187
GlobalTelemetryBus.cs brace_balance 0 lines 1207
NativeRingBufferEditTests.cs brace_balance 0 lines 59
NativeRingBufferEditTests.cs.meta brace_balance 0 lines 2
```

`git diff --check` on the touched source/test files passed with line-ending warnings only.

## Build Boundary

Build was attempted after the AGENTS CPU/compiler gate allowed it:

```text
attempt=1 cpu=43 compilerProcessCount=0
launched=True
command=dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false
```

Result:

- Exit code: `1`.
- Warnings: `0`.
- Errors: `62` in MSBuild summary.
- Failure class: `MSB3202`, missing Unity-generated `.csproj` files.
- Root `.csproj` count at verification time: `0`.
- Build did not reach C# compilation.

Raw proof: `BUILD_UNKNOWN_NATIVE_RING_COPY_RECHECK_20260527.log`.

## Documentation Validation

```text
VerifyDocStructure.py pass=true activeDocCount=667 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=667 sourceSyncPass=true wordReductionPercent=50.87418392343683
```

## Residuals

- No runtime/profiler microseconds are claimed.
- New edit tests were added but not executed because the Unity project-file boundary blocks CLI compile/test discovery in this checkout.
- Dirty GPU/readback files still contain `GetData`, `SetData`, `GC.Collect`, and one `AsyncGPUReadbackRequest.WaitForCompletion()` site; this pass did not touch them because they are already under concurrent modification.
- Clean `GlobalDataVault.TryGetLatestCreated()` hits were rechecked: two are `#if UNITY_EDITOR` gizmos, one is `SignalWardenRuntime.EnsureInitializedForCrashDumpRoute`, which is the explicitly allowed crash-dump fallback lane.

## External API Proof

- Unity `NativeArray<T>.Copy`: `https://docs.unity.cn/2020.1/Documentation/ScriptReference/Unity.Collections.NativeArray_1`
- Unity `GraphicsBuffer.LockBufferForWrite`: `https://docs.unity.cn/6000.2/Documentation/ScriptReference/GraphicsBuffer.UsageFlags.LockBufferForWrite.html`
