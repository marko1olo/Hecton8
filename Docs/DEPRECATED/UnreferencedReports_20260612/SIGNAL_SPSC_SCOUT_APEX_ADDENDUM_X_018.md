# SIGNAL SPSC APEX ADDENDUM X_018

Scope: read-only re-audit of `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs` and `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`.

No C# source was modified.

## 1. SpscSignalRingBuffer<T> byte layout

Source-level field order:

- `SpscSignalRingBuffer.cs:14` `_buffer : NativeArray<T>`
- `SpscSignalRingBuffer.cs:15` `_owner : Hecton8.Core.Memory.SystemID`
- `SpscSignalRingBuffer.cs:16` `_mask : int`
- `SpscSignalRingBuffer.cs:17` `_head : PaddedSignalIndex`
- `SpscSignalRingBuffer.cs:18` `_tail : PaddedSignalIndex`

Compiled metadata:

- `SpscSignalRingBuffer<T>` is sequential layout, not explicit layout.
- `PaddedSignalIndex` is explicit layout, `Size = 64` at `SpscSignalRingBuffer.cs:94`.
- `PaddedSignalIndex.Value` is at offset 0 at `SpscSignalRingBuffer.cs:97`.
- Pad fields occupy offsets 8, 16, 24, 32, 40, 48, 56 at `SpscSignalRingBuffer.cs:98-104`.

Current 64-bit Unity Editor assembly layout:

| Offset | Size | Field |
|---:|---:|---|
| 0 | 48 | `_buffer : NativeArray<T>` |
| 48 | 2 | `_owner : SystemID` |
| 50 | 2 | padding |
| 52 | 4 | `_mask : int` |
| 56 | 64 | `_head : PaddedSignalIndex` |
| 120 | 64 | `_tail : PaddedSignalIndex` |
| 184 | 0 | total end |

Supporting container layout used for the 48-byte `NativeArray<T>` calculation:

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | `m_Buffer : void*` |
| 8 | 4 | `m_Length : int` |
| 12 | 4 | `m_MinIndex : int` |
| 16 | 4 | `m_MaxIndex : int` |
| 20 | 4 | padding |
| 24 | 16 | `m_Safety : AtomicSafetyHandle` |
| 40 | 4 | `m_AllocatorLabel : Allocator` |
| 44 | 4 | tail padding |

`AtomicSafetyHandle` metadata fields are `IntPtr versionNode`, `int version`, `int staticSafetyId`, total 16 bytes on 64-bit. `SystemID` is a `ushort` enum in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:16`.

Cursor proof:

- Read cursor storage is `_head.Value`: parent offset 56 + nested offset 0 = byte offset 56.
- Write cursor storage is `_tail.Value`: parent offset 120 + nested offset 0 = byte offset 120.
- Cursor distance is 120 - 56 = 64 bytes.

False-sharing verdict:

- The two cursor `int` values are separated by exactly 64 bytes in the current 64-bit Unity Editor assembly layout.
- The source does not prove 64-byte alignment of the parent `SpscSignalRingBuffer<T>` base address.
- Therefore the code proves 64-byte spacing between cursor values, but it does not prove that `_head` and `_tail` each begin on separate 64-byte cache-line bases under every allocator, array embedding, generic container, or IL2CPP/player layout.

There is no disk serializer for `SpscSignalRingBuffer<T>` in the audited file. "On disk" layout is not defined by this code; the table above is in-memory struct layout.

## 2. SPSC enqueue memory ordering

`SpscSignalRingBuffer<T>` has no method named `Push`. Its producer-side method is `TryEnqueue`.

Exact producer path:

1. `SpscSignalRingBuffer.cs:64` reads `_tail.Value` with `Volatile.Read`.
2. `SpscSignalRingBuffer.cs:65` calculates `nextTail`.
3. `SpscSignalRingBuffer.cs:66` reads `_head.Value` with `Volatile.Read` for full detection.
4. `SpscSignalRingBuffer.cs:69` writes payload into `_buffer[tail]`.
5. `SpscSignalRingBuffer.cs:70` publishes `nextTail` with `Interlocked.Exchange(ref _tail.Value, nextTail)`.

Exact barrier/instruction inventory inside `SpscSignalRingBuffer<T>`:

- `Thread.MemoryBarrier`: no calls.
- `Volatile.Write`: no calls.
- `Volatile.Read`: line 64, line 66, line 82, line 83.
- `Interlocked.Exchange`: line 55, line 56, line 70, line 90.
- `Interlocked.CompareExchange`: no calls.
- `Interlocked.Increment`: no calls.

Ordering implication:

- Producer payload write at line 69 is sequenced before the full-fence atomic exchange at line 70 in the C# source.
- Consumer observes `_tail.Value` through `Volatile.Read` at line 83 before reading `_buffer[head]` at line 89.
- The implementation relies on `Interlocked.Exchange` to publish the cursor after payload write and on `Volatile.Read` to acquire cursor state. It does not use an explicit `Thread.MemoryBarrier` or `Volatile.Write`.

`SignalBus<T>.Push` is separate and does not use `SpscSignalRingBuffer<T>`:

- `SignalBusRuntime.cs:574-576` `Push(in T signal)` only calls `TryPush(in signal)`.
- `SignalBusRuntime.cs:616` enqueues to `_queue.Enqueue(sanitizedSignal)`.
- `SignalBusRuntime.cs:617` writes `_latestSignal` with a plain assignment.
- `SignalBusRuntime.cs:618` calls `AdvanceLatestSignalSequence()`.
- `SignalBusRuntime.cs:1298` uses `Volatile.Read(ref _latestSignalSequence)`.
- `SignalBusRuntime.cs:1302` uses `Volatile.Write(ref _latestSignalSequence, next)`.
- `SignalBusRuntime.cs:619` increments `_acceptedSignalTotal` with `Interlocked.Increment`.

No source-level barrier surrounds `_queue.Enqueue` at `SignalBusRuntime.cs:616`; any queue synchronization is delegated to Unity `NativeQueue<T>`.

## 3. SignalBusRegistry delegate registration and clear race audit

Closed generic delegates in `SignalBus<T>`:

- `_flushDispatch = FlushPreSimulation` at `SignalBusRuntime.cs:341`.
- `_clearDispatch = ClearPostSimulation` at `SignalBusRuntime.cs:343`.
- `_telemetryDispatch = CopyTelemetryStatic` at `SignalBusRuntime.cs:345`.
- `_disposeDispatch = Dispose` at `SignalBusRuntime.cs:347`.

Registration path:

1. `SignalBusRuntime.cs:1305-1308` returns if `_registered` is already true.
2. `SignalBusRuntime.cs:1310-1311` initializes `_laneHash` if absent.
3. `SignalBusRuntime.cs:1313-1318` calls `SignalBusRegistry.Register(_disposeDispatch, _flushDispatch, _clearDispatch, _telemetryDispatch, FlushDuringSimulationPause)`.
4. `SignalBusRuntime.cs:1319` sets `_registered = true` with a plain write.

Registry write path:

1. `SignalBusRuntime.cs:87-88` drops null dispose delegates.
2. `SignalBusRuntime.cs:90-94` scans `_laneDisposeDispatch[0.._laneCount)` for duplicate dispose delegates.
3. `SignalBusRuntime.cs:96-102` handles capacity overflow; `Volatile.Write(ref _registrationOverflow, 1)` at line 98.
4. `SignalBusRuntime.cs:105` snapshots `laneIndex = _laneCount`.
5. `SignalBusRuntime.cs:106` stores dispose delegate.
6. `SignalBusRuntime.cs:107` stores telemetry delegate.
7. `SignalBusRuntime.cs:108-114` stores `new SignalLaneDispatch(flush, clear, flushDuringSimulationPause)` into `_laneDispatch[_dispatchLaneCount++]`.
8. `SignalBusRuntime.cs:116` increments `_laneCount`.

Dispatch record:

- `SignalBusRuntime.cs:227` declares `internal readonly struct SignalLaneDispatch`.
- `SignalBusRuntime.cs:229` contains `Flush`.
- `SignalBusRuntime.cs:230` contains `Clear`.
- `SignalBusRuntime.cs:231` contains `FlushDuringSimulationPause`.
- `SignalBusRuntime.cs:238-240` assigns these fields in the constructor.

Flush invocation:

1. `SignalBusRuntime.cs:148-152` public `FlushPreSimulation()` reads stress and pause state, then calls `FlushRegisteredSignalLanes`.
2. `SignalBusRuntime.cs:246` reads `_dispatchLaneCount` with `Volatile.Read`.
3. `SignalBusRuntime.cs:247-249` loops and copies `_laneDispatch[i]`.
4. `SignalBusRuntime.cs:250-255` skips null delegates and pause-disallowed lanes.
5. `SignalBusRuntime.cs:257` invokes `dispatch.Flush(systemStressMilli)`.

Clear invocation:

1. `SignalBusRuntime.cs:156-158` public `ClearPostSimulationSnapshots()` calls `ClearRegisteredSignalLaneSnapshots()`.
2. `SignalBusRuntime.cs:263` reads `_dispatchLaneCount` with `Volatile.Read`.
3. `SignalBusRuntime.cs:264-266` loops and copies `_laneDispatch[i]`.
4. `SignalBusRuntime.cs:267-268` invokes `dispatch.Clear()` if non-null.
5. Target clear delegate is `SignalBus<T>.ClearPostSimulation()` at `SignalBusRuntime.cs:1193-1197`.
6. `SignalBusRuntime.cs:1195` writes `_frameSnapshotCount = 0` with a plain write.
7. `SignalBusRuntime.cs:1196` writes `_legacyReadCursor = 0` with a plain write.

Race verdict:

- If the dispatcher guarantees phase exclusivity, no producer/consumer reads snapshots while `ClearPostSimulationSnapshots()` runs, and no lane registration occurs concurrently with registry dispatch, then the clear path is phase-safe.
- The registry itself does not enforce that exclusivity. It has no lock, no CAS, and no job fence in `Register`, `FlushRegisteredSignalLanes`, or `ClearRegisteredSignalLaneSnapshots`.
- A theoretical registration race exists if `Register()` overlaps with clear/flush: `_laneDispatch[_dispatchLaneCount++]` at `SignalBusRuntime.cs:110` is a plain compound mutation, while clear/flush read `_dispatchLaneCount` with `Volatile.Read` at lines 246 and 263.
- A theoretical snapshot-consumer race exists if consumers call snapshot readers concurrently with clear: `ClearPostSimulation()` plain-writes `_frameSnapshotCount` at line 1195, while `TryReadFrameSnapshot()` plain-reads `_frameSnapshotCount` at `SignalBusRuntime.cs:1430`.
- A theoretical cursor race exists if legacy snapshot read cursor consumers overlap with clear: `_legacyReadCursor` is reset by a plain write at line 1196.
- Therefore `ClearPostSimulationSnapshots()` is not intrinsically thread-safe. It is only correct under the external dispatcher phase contract.

## 4. Hard facts

- `SpscSignalRingBuffer<T>` is a standalone fallback primitive. It is not the active `SignalBus<T>.Push` path in the audited source.
- Active `SignalBus<T>.Push` uses Unity `NativeQueue<T>` and static closed-generic delegates through `SignalBusRegistry`.
- The SPSC cursor spacing is exactly 64 bytes in the current 64-bit Unity Editor metadata-derived layout.
- The source does not prove cache-line base alignment.
- The SPSC producer uses `Volatile.Read` plus `Interlocked.Exchange`; it does not call `Thread.MemoryBarrier` or `Volatile.Write`.
- Registry dispatch uses delegate arrays and phase discipline; it does not contain internal synchronization sufficient to make registration/clear concurrent-safe.
