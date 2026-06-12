# SIGNAL_SPSC_PARANOID_REVIEW_1311

Generated: 2026-05-25
Agent: 1311 / SIGNAL_CORRIDOR_SPSC_ARCHITECT
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`
Task count re-read: 11
Evidence class: static source scan plus existing signal-contract CLI gate. No Unity import, IL2CPP, profiler, or dotnet build proof.

## 1. Runtime Managed Logic Scan

Scope: `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs`

Patterns scanned:
`new`, `string.Format`, `.ToString(`, `System.Linq`, `.Select(`, `.Where(`, `.Any(`, `.ToList(`, `foreach (`, `throw`, `catch (`, `Debug.Log`, interpolated strings, `.Concat(`.

Hits:
- `SpscSignalRingBuffer.cs:216` - `return new ParallelWriter(_buffer, _publishedTickets, _cursor, _mask, _capacity);`
  Classification: value-type construction. `ParallelWriter` is a `struct` at `SpscSignalRingBuffer.cs:290`; no managed heap allocation is implied by this source expression.

Zero-hit categories in this file:
- `string.Format`: 0
- `.ToString(`: 0
- LINQ namespace/calls: 0
- `foreach`: 0
- `throw` / `catch`: 0
- `Debug.Log`: 0
- interpolated strings: 0
- `.Concat(`: 0

Additional scope after active-path integration: `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`

Hot publish path:
- `SignalBusRuntime.cs:581-618` contains 0 `new`, 0 string concatenation, 0 `string.Format`, 0 `.ToString(`, 0 LINQ, 0 `foreach`, 0 `throw`, 0 `catch`, and 0 `Debug.Log`.
- `SignalBusRuntime.cs:607` publishes through `_ring.TryEnqueue(in sanitizedSignal)`.

Cold/runtime-classification hits in `SignalBusRuntime.cs`:
- `SignalBusRuntime.cs:43,45,47` - static delegate arrays, cold registry storage.
- `SignalBusRuntime.cs:110` - `new SignalLaneDispatch(...)`, cold registration entry.
- `SignalBusRuntime.cs:532` - `new global::Hecton8.Core.MpscSignalRingBuffer<T>(...)`, cold lane initialization.
- `SignalBusRuntime.cs:546` - `new NativeArray<int>(...)`, cold writer-budget sidecar.
- `SignalBusRuntime.cs:687` - `new ReadOnlySpan<T>(...)`, stack-only span value construction, not managed heap allocation.
- `SignalBusRuntime.cs:1371` - `new NativeQueue<T>(Allocator.Persistent)`, cold legacy writer bridge only; not the active main-thread ring path.
- `SignalBusRuntime.cs:1534-1536` - `typeof(T).FullName` / `typeof(T).Name` for cold fallback type hash. This remains a managed metadata dependency for unknown signal contracts and is not executed per push/flush after closed-generic initialization.

String concatenation:
- `SignalBusRuntime.cs` string concat scan now only reports the cold fallback label/type metadata above; previous fault-log concatenations were removed and replaced with constant messages at lines 492 and 528.

Native collection findings in this file:
- `SpscSignalRingBuffer.cs:36` - bounded payload storage `NativeArray<T> _buffer`; allocated through `H8Memory` at lines 52 and released at lines 79-80.
- `SpscSignalRingBuffer.cs:37` - one-row cursor header `NativeArray<SignalRingCursorState> _cursor`; allocated at line 53 and released at lines 81-82.
- `SpscSignalRingBuffer.cs:169` - bounded MPSC payload storage `NativeArray<T> _buffer`; allocated at line 187 and released at lines 262-263.
- `SpscSignalRingBuffer.cs:170` - bounded MPSC publication tickets `NativeArray<long> _publishedTickets`; allocated at line 188 and released at lines 264-265.
- `SpscSignalRingBuffer.cs:171` - one-row MPSC cursor header; allocated at line 189 and released at lines 266-267.
- `SpscSignalRingBuffer.cs:292-294` - writer copies of existing native handles. These are value copies, not new allocations.

## 2. ARM64 Byte Offset Map

DTO: `SignalRingCursorState`
Declaration: `SpscSignalRingBuffer.cs:11-30`
Layout: `[StructLayout(LayoutKind.Explicit, Size = 128)]`

| Offset | Size | Field | Type | Purpose |
|---:|---:|---|---|---|
| 0 | 8 | Head | long | consumer cursor |
| 8 | 8 | _headPad0 | ulong | explicit padding |
| 16 | 8 | _headPad1 | ulong | explicit padding |
| 24 | 8 | _headPad2 | ulong | explicit padding |
| 32 | 8 | _headPad3 | ulong | explicit padding |
| 40 | 8 | _headPad4 | ulong | explicit padding |
| 48 | 8 | _headPad5 | ulong | explicit padding |
| 56 | 8 | _headPad6 | ulong | explicit padding |
| 64 | 8 | Tail | long | producer cursor |
| 72 | 8 | _tailPad0 | ulong | explicit padding |
| 80 | 8 | _tailPad1 | ulong | explicit padding |
| 88 | 8 | _tailPad2 | ulong | explicit padding |
| 96 | 8 | _tailPad3 | ulong | explicit padding |
| 104 | 8 | _tailPad4 | ulong | explicit padding |
| 112 | 8 | _tailPad5 | ulong | explicit padding |
| 120 | 8 | _tailPad6 | ulong | explicit padding |

Proof:
- Size 128 is divisible by 8 and by 64.
- `Head` occupies cache line 0 (`0..63`).
- `Tail` occupies cache line 1 (`64..127`).
- All fields are 8-byte fields; no 4-byte cursor holes remain.

Non-DTO note:
- `SpscSignalRingBuffer<T>`, `MpscSignalRingBuffer<T>`, and `MpscSignalRingBuffer<T>.ParallelWriter` are native-container wrapper structs containing Unity `NativeArray<T>` handles. Their exact compiled parent offsets are Unity-configuration dependent, so this report does not make fake parent-offset claims. Cursor truth is isolated in the explicit 128-byte native header above.

## 3. Barrier And Publication Order

SPSC enqueue:
- `SpscSignalRingBuffer.cs:104-105` reads `Tail` and `Head` through `Volatile.Read`.
- `SpscSignalRingBuffer.cs:110-111` computes the slot and writes payload.
- `SpscSignalRingBuffer.cs:112` publishes `Tail` using `Interlocked.Exchange`.

SPSC dequeue:
- `SpscSignalRingBuffer.cs:124-125` reads `Head` and `Tail` through `Volatile.Read`.
- `SpscSignalRingBuffer.cs:132-133` reads payload only after observing non-empty state.
- `SpscSignalRingBuffer.cs:134` publishes `Head` using `Interlocked.Exchange`.

MPSC enqueue:
- `SpscSignalRingBuffer.cs:323-324` reads cursors through `Volatile.Read`.
- `SpscSignalRingBuffer.cs:329` reserves `Tail` with `Interlocked.CompareExchange`.
- `SpscSignalRingBuffer.cs:332-333` writes payload to the reserved slot.
- `SpscSignalRingBuffer.cs:334` publishes the per-slot ticket using `Interlocked.Exchange`.

MPSC dequeue:
- `SpscSignalRingBuffer.cs:230-231` reads cursors through `Volatile.Read`.
- `SpscSignalRingBuffer.cs:235-239` requires the exact publication ticket before reading payload.
- `SpscSignalRingBuffer.cs:241-243` reads payload, clears ticket, then publishes `Head`.

Race closure:
- MPSC `Tail` can move before the payload is written. The per-slot `long` ticket is the correctness gate. A consumer seeing advanced `Tail` but missing ticket returns `false` at line 239 instead of reading uninitialized payload.

## 4. AUP / Spatial Determinism

`SpscSignalRingBuffer.cs` spatial scan hits for `double3`, `float3`, `Vector3`, `Quaternion`, `AUP`, `AbsoluteUniverse`, `Position`, `velocity`, `force`, `collision`: 0.

Conclusion:
- This file is generic signal transport, not a coordinate math owner.
- No absolute AUP value is cast to `float` in this file.
- No position delta formula applies inside the ring primitive.

Active adjacent file note:
- `SignalBusRuntime.cs` contains pre-existing AUP aliases at lines 14-15 and signal sanitation defaults at lines 3218 and 4634. I did not edit those spatial payload routines in this pass.

## 5. Assembly And Dependency Isolation

`SpscSignalRingBuffer.cs` usings:
- lines 1-7: `System`, `System.Runtime.CompilerServices`, `System.Runtime.InteropServices`, `System.Threading`, `Unity.Collections`, `Unity.Collections.LowLevel.Unsafe`, `Unity.Mathematics`.

Direct cross-domain concrete references introduced by this patch: 0.

Core ownership references:
- `Hecton8.Core.Memory.SystemID` and `H8Memory` are used fully qualified inside the same Core infrastructure domain.

Asmdef changes: 0.

Observed existing boundary debt outside this file:
- `SignalBusRuntime.cs` imports `Hecton8.Core.Contracts`, `Hecton8.Core.Generated`, `Hecton8.Core.Memory`, UnityEngine, and AUP aliases from `Hecton8.World` at lines 5-15. This is pre-existing in the active signal hub and remains part of later integration risk, not a new dependency from this patch.

## 6. Fail-Closed Behavior

SPSC:
- uncreated/no cursor count returns `0`: lines 64-65.
- `Clear` no cursor returns: lines 92-93.
- enqueue on uncreated/no cursor/full returns `false`: lines 101-108.
- dequeue on uncreated/no cursor/empty returns `false` with `default`: lines 118-129.

MPSC:
- count with no cursor returns `0`: lines 202-203.
- dequeue on uncreated/no cursor/empty/unpublished slot returns `false`: lines 227-239.
- enqueue on uncreated/invalid/full returns `false`: lines 316-326.
- clear resets cursors and tickets: lines 252-257.

Managed exception scan in `SpscSignalRingBuffer.cs`: 0 `throw`, 0 `catch`.

Remaining fail-closed gap:
- Task 09 blackbox dump to `Dump_1311_SignalCorridor.bin` is not implemented yet. Current primitive fails closed by return code only; it does not yet emit the mandated 300-frame forensic dump.

## 7. Overengineering Review

No physical simulation, force integration, collision solver, or visual math was added here.

The MPSC CAS/ticket design is not a visual feature and cannot be replaced by a LUT or timer without corrupting transport truth. The 32-attempt CAS cap from the first primitive pass was removed; current producer loop retries until it either reserves a slot or observes full capacity. This prevents artificial drops caused only by transient CAS contention.

## 8. Active NativeQueue Red Zone

Updated after active-path integration:

- `SignalBusRuntime.cs` no longer contains `_queue`, `_queue.Count`, `_queue.Enqueue`, `_queue.TryDequeue`, `_queue.Clear`, or `PrewarmQueue`.
- Active main-thread storage is `_ring` at `SignalBusRuntime.cs:306`, allocated as `global::Hecton8.Core.MpscSignalRingBuffer<T>` at line 550.
- `TryPush` writes through `_ring.TryEnqueue(in sanitizedSignal)` at line 625.
- Registry flush/drain/drop/clear uses `CountPendingSignals`, `TryDequeuePendingSignal`, and `ClearPendingSignals` at lines 855, 893, 896, 1229, and 1234-1239.
- Migrated producer API exists: `OpenRingParallelWriter` at line 415, `RingParallelWriter` at line 441, and `TryEnqueueBounded(MpscSignalRingBuffer<T>.ParallelWriter, ...)` at lines 682-706.

Remaining NativeQueue bridge:

- line 307: `private static NativeQueue<T> _legacyQueue;`
- line 398: `OpenLegacyMpscWriter`
- line 409: `OpenParallelWriter`
- line 432: `ParallelWriter`
- line 655: `TryEnqueueBounded(NativeQueue<T>.ParallelWriter writer, ...)`
- line 1423: `_legacyQueue = new NativeQueue<T>(Allocator.Persistent)`
- line 1437: `_legacyQueue.Enqueue(default)` cold prewarm only
- line 1439: `_legacyQueue.TryDequeue(out _)` cold prewarm drain only
- line 1449: `_legacyQueue.Count` used only when the legacy writer bridge was opened
- line 1460: `_legacyQueue.TryDequeue(out signal)` used only to drain legacy bridge writes

Conclusion:
- `SpscSignalRingBuffer.cs` primitive is hardened.
- Active main-thread `SignalBus<T>` storage and flush are ring-backed.
- Full master-prompt replacement is still incomplete because public job writer compatibility remains `NativeQueue<T>.ParallelWriter` until external job structs migrate.

## 9. Independent Contract Gate

Executed:
`Tools/SignalBusContractAuditCli/bin/Debug/net10.0/SignalBusContractAuditCli.exe --project-root . --json Docs/Reports/SIGNAL_SPSC_CONTRACT_AUDIT_1311.json --markdown Docs/Reports/SIGNAL_SPSC_CONTRACT_AUDIT_1311.md --scope SignalCritical --include-hot-path-heuristics`

Result:
- files scanned: 25 C# / 71 compute
- errors: 0
- warnings: 0
- infos: 20
- local native signal queue hits: 0
- asmdef contract boundary hits: 0
- hot-path heuristic hits: 0

Relevant scanner notes:
- `SpscSignalRingBuffer.cs:37`, `171`, `294` flagged as `LOCAL_NATIVE_SIGNAL_ARRAY_REVIEW` for `_cursor`. Manual classification above: bounded one-row cursor header, not telemetry ring and not hidden private hot queue replacement.

Failed tool attempt:
- `net8.0` compiled gate did not run because this machine only has `Microsoft.NETCore.App 10.0.6`. The already-built `net10.0` gate succeeded.

## 10. Build Status

No `dotnet build` was launched in this pass. User explicitly requested rare builds, and previous CPU guard already exceeded the project threshold. Verification is static only:
- source scan
- signal-contract CLI gate
- pending `git diff --check`
