# SIGNAL Owner Counter TryPush Closure - X_001

Date: 2026-05-25
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor

## Scope

This pass removes the no-ref tracked `TryPush` escape hatch introduced in the previous closure. That no-ref path counted refusal on a generic lane counter, but it was still weaker than owner-local black-box evidence. The direct runtime producer edges now carry owner-local static counters.

Runtime/code files touched in this closure: 36.

## What Changed

- Converted 65 external runtime `SignalBus<T>.TryPushTracked(in payload)` calls to `SignalBus<T>.TryPushTracked(in payload, ref ownerCounter)`.
- Inserted 35 owner-local static counters named `s_x001DirectSignalPushDropCount_*`.
- Removed the generic `SignalBus<T>.DirectTrackedDropTotal` counter.
- Removed the no-ref `SignalBus<T>.TryPushTracked(in T signal)` overload.
- Kept the existing `SignalBus<T>.TryPushTracked(in T signal, ref int ownerDroppedSignalCount)` overload as the only tracked producer edge.

## Owner Counter Rationale

The lane itself already tracks accepted, load-shed, corrupted, dropped, coalesced, storm, peak-queued, and writer-budget counters. The missing piece was source-local evidence for direct wrapper producers whose callers still need a bool return. A 5000-signal storm now produces refusal evidence in the owner file, not only in generic lane telemetry.

This avoids:

- managed logs;
- string event names;
- dictionaries;
- delegates;
- heap sidecars;
- raising capacities to hide storms.

## Overflow Strategy

The actual admission and overflow policy remains inside `SignalBus<T>`:

- main-thread producers reject before enqueue when `_queue.Count >= _expectedCapacity`;
- tracked calls increment owner-local counters on rejection;
- job writers must use `TryEnqueueBounded(...)` with `ParallelWriterBudget`;
- finite guards reject non-finite payloads before enqueue;
- frame flush applies configured max/low-tier frame caps;
- storm policies coalesce where explicitly implemented, otherwise drop deterministically.

This pass only moves refusal accounting from the temporary generic direct-edge counter into owner-local counters.

## Verification

Commands and results:

- External no-ref tracked scan:
  - Pattern: `TryPushTracked(in [^,\\r\\n]+)`.
  - Result: `NO_REF_TRYPUSH_TRACKED_HITS=0`.
- Removed generic counter scan:
  - Pattern: `DirectTrackedDropTotal|_directTrackedDropTotal`.
  - Result: 0 hits.
- Owner declaration/reference scan:
  - Result: `OWNER_COUNTER_DECL_REF_MISSING=0`, `OWNER_COUNTER_DECL_UNUSED=0`.
- Owner-tracked edge count:
  - Result: `OWNER_DIRECT_TRACKED_CALLS=65`.
- Owner counter field count:
  - Result: `OWNER_DIRECT_COUNTER_FIELDS=35`.
- External runtime hot-route scan:
  - Pattern: `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, external `SignalBus<T>.TryPush`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `ThreadSafeCommandQueue.Enqueue`.
  - Result: `HOT_ROUTE_HITS=0`.
- Runtime `ISignal` DTO banned-field scan:
  - Result: `DTO_BANNED_FIELD_HITS=0`.
- Touched-file brace scan:
  - Result: `CODE_TOUCHED_FILES=36`, `BRACE_DELTA_HITS=0`.
- `git diff --check` over the touched code files:
  - Result: no whitespace errors; LF-to-CRLF warnings only.
- Build guard:
  - Final result: `FINAL_BUILD_GUARD cpu=99.4 compiler_count=2`.
  - Active compiler/build processes: `csc` PID 56252, `dotnet` PID 54420.
  - Per `AGENTS.md`, `dotnet build` was not launched.

## Runtime Claims

Verified runtime microsecond saving: 0us. No Unity profiler, Play Mode, GCMonitor, Memory Profiler, or player build was run.

Static proof:

- The only tracked push API now requires an explicit `ref int` owner counter.
- No external runtime no-ref tracked producer edge remains.
- No `SignalBus<T>.Push` or external `SignalBus<T>.TryPush` hot producer remains.
- No managed/string/native-container signal payload field was introduced.
