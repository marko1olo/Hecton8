# X_018 Rationale

## Decision 01
Problem: X_018 prompt targets `SignalBusRegistry.cs`, but source scan found `SignalBusRegistry` declared inside `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`.
Solution: Treat `SignalBusRuntime.cs` lines containing `SignalBusRegistry` as the authoritative registry implementation while still auditing `SpscSignalRingBuffer.cs`.
Rejected Alternatives: Assume a missing standalone file; fabricate findings from docs; edit code to split registry into its own file.
Scalability potential: Low/Middle/High/Ultra unaffected; this is source archaeology only.
Hardware Impact: 0 us runtime change on i3/MX350; audit prevents future synchronization mistakes.

## Decision 02
Problem: The task demands byte-perfect layout facts, but generic `NativeArray<T>` handle size and managed static delegate/object layout cannot be proven from C# source alone.
Solution: Report proven explicit/source-level layouts separately from compiler/runtime-dependent layouts and label unverifiable fields as requiring `UnsafeUtility.SizeOf<T>()` or runtime offset proof.
Rejected Alternatives: Claim exact `SpscSignalRingBuffer<T>` total size from source despite generic/native-container implementation details.
Scalability potential: Low/Middle/High/Ultra unaffected; future agents get correct proof boundary.
Hardware Impact: 0 us runtime change; avoids false ARM64 cache-line claims.

## Decision 03
Problem: Read-only task conflicts with generic protocol requiring compile verification after task loops.
Solution: Do not launch `dotnet build` because no C# source changes are made and project rule forbids unnecessary rebuilds when system load/build contention may exist.
Rejected Alternatives: Run a rebuild only to create a proof artifact; this would consume CPU and may violate the no-unnecessary-build rule.
Scalability potential: Low/Middle/High/Ultra unaffected.
Hardware Impact: Saves local build CPU time; runtime impact 0 us.

## Decision 04
Problem: Active `SignalBus<T>` was expected to be SPSC by task wording, but source shows `NativeQueue<T>` plus `ParallelWriter` support.
Solution: Report `SpscSignalRingBuffer<T>` as a standalone fallback primitive and `SignalBus<T>` as the active NativeQueue-backed lane.
Rejected Alternatives: Force the SPSC mental model onto `SignalBus<T>` despite `_queue` at `SignalBusRuntime.cs:305`.
Scalability potential: Low uses bounded frame limits and shedding; Middle/High/Ultra may increase frame limits through continuous quality but still must not assume SPSC cursor ownership.
Hardware Impact: 0 us runtime change; avoids future MPSC/SPSC misuse that would cost stalls or lost events on i3/MX350.

## Decision 05
Problem: `PaddedSignalIndex` is 64 bytes, but parent alignment is not guaranteed by the source.
Solution: Mark nested struct size as proven and parent cache-line isolation as unproven until Unity runtime/IL2CPP offset proof exists.
Rejected Alternatives: Claim false-sharing prevention from nested size alone.
Scalability potential: Low/Middle/High/Ultra unchanged; future cache-line-critical code must request measured offset proof before relying on it.
Hardware Impact: 0 us runtime change; prevents cache-line contention being hidden by false audit language.

## Decision 06
Problem: Registry flush drains queues but owns no producer job fence.
Solution: Report that the external dispatcher phase must complete/fence producer jobs before `SignalBusRegistry.FlushPreSimulation()`.
Rejected Alternatives: Imply `SignalBusRegistry` itself guarantees writer quiescence.
Scalability potential: Low/Middle/High/Ultra require same ownership rule; quality may change capacity, not synchronization authority.
Hardware Impact: 0 us runtime change; missing fence risk would be unbounded correctness loss, not a small microsecond cost.

## Decision 07
Problem: APEX review demanded a byte-perfect SPSC map and a proof of cache-line false-sharing prevention.
Solution: Report current 64-bit Unity Editor metadata-derived offsets exactly, while refusing to claim parent cache-line base alignment that source and metadata do not prove.
Rejected Alternatives: Treat 64-byte cursor spacing as equivalent to guaranteed 64-byte cache-line ownership; treat `SignalBus<T>.Push` as SPSC despite its `NativeQueue<T>` source path.
Scalability potential: Low/Middle/High/Ultra unchanged; future hot-path code must not spend saved cycles on a synchronization assumption the layout does not prove.
Hardware Impact: 0 us runtime change on i3/MX350; audit prevents false-sharing fixes from being built on a false proof.
