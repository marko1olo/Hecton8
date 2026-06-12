# SIGNAL_TRYPUSH_ZERO_REMAINDER_CLOSURE_X_001

Agent: X_001  
Date: 2026-05-25  
Scope: residual first-party runtime `SignalBus<T>.TryPush(...)` producers outside `Core/Signals`, Editor, Tests, and ModdingAPI.

## Problem

The central hot route was already clean, but 75 external statement-level producers still called `SignalBus<T>.TryPush(...)` and discarded the bool. The lane itself stayed bounded and zero-GC, but producer owners had no local refusal proof when a 5000-signal burst hit capacity.

## Work Done

- Converted the remaining 75 external statement-level `SignalBus<T>.TryPush(...)` call sites to `SignalBus<T>.TryPushTracked(...)`.
- Added owner-local `private static int s_x001*SignalPushDropCount` counters for the residual producer owners.
- Converted both simple `in signal` producers and multiline object-initializer producers.
- Corrected the containment miss created by mechanical insertion: `QuestDagDebugApi.ForceCompleteNode(...)` now owns its static refusal counter instead of referencing adjacent `QuestDagResolverService`.
- Did not raise capacities, introduce managed logs, add dictionaries, or route traffic back through `GlobalSignals`.

## Overflow Policy Proof

- `SignalBus<T>.TryPush(...)` still rejects before enqueue when `_queue.Count >= _expectedCapacity`.
- `TryPushTracked(...)` wraps the same bounded path and increments a caller-owned `int` only on refusal.
- Object-initializer payloads remain struct temporaries; no managed heap sidecar, string event name, `GameObject`, or `Transform` route was added.
- Existing lane coalescing remains in the lane policy layer; this pass closes producer-edge refusal visibility, not capacity semantics.

## Verification

- External statement-level direct `SignalBus<T>.TryPush(...)`: `0`.
- `SignalBus<T>.TryPushTracked(...)` total project call sites: `422`.
- Bad tracked receiver scan: `0`.
- Owner-counter containment scan: `0`.
- Runtime hot-route scan for external `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue`: `0`.
- Runtime `ISignal` DTO scan: `287` structs scanned, `0` fields using `GameObject`, `Transform`, `string`, `FixedString*`, `NativeArray`, `NativeQueue`, `NativeList`, or `NativeHashMap`.
- Code-aware brace delta scan over tracked producer files: `0`.
- `git diff --check` on tracked producer/doc files: no whitespace errors; LF-to-CRLF warnings only.
- Build: skipped by guard, `CPU=100`, `compiler_count=2`, active `csc` PID `39508` and `dotnet` PID `32980`.

## Hardware Impact

Verified runtime microseconds saved: `0us`; no Unity profiler/GCMonitor run was performed. Static expected effect on i3/MX350-class hardware is better postmortem refusal evidence during bounded signal storms without additional heap allocation, native queue growth, or `GlobalSignals` relapse.
