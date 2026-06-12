# SIGNAL_TRYPUSH_SURFACE_CLOSURE_X_001

Date: 2026-05-24
Agent: X_001
Status: SOURCE_ONLY / BUILD BLOCKED BY CPU GUARD

## What Was Wrong

`GlobalSignals.Publish/Push/TryDequeue/*Writer` was already removed from external runtime routes, but first-party producers still used `SignalBus<T>.Push(...)` as a silent wrapper over `TryPush(...)`. That hid deterministic drop semantics at the call site and left editor smoke tests asserting the old wrapper text.

## What Was Done

- Converted 169 runtime `SignalBus<T>.Push(...)` producer calls across 87 files outside `Core/Signals`, `Editor`, and `Tests` to `SignalBus<T>.TryPush(...)`.
- Converted 121 internal Core calls in `CoreDeterminismSignals.cs` and `GlobalSignals.LegacyFacade.cs` to `TryPush(...)`.
- Updated two editor smoke-test files so static gates now assert `TryPush(...)`, not `Push(...)`.

## Proof

- `SignalBus<...>.Push` call sites in `Assets/_Project/Scripts/**/*.cs`: 0.
- External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` hits outside `Core/Signals`, `Editor`, and `Tests`: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` hits outside `ModdingAPI`, `Editor`, and `Tests`: 0.
- Signal DTO managed/string/native-container field hits in `Core/Signals` and `Core/Contracts/Signals`: 0.

## Overflow Meaning

Main-thread producers now call the bool-returning API directly. The return value is intentionally ignored at most current fire-and-forget presentation producers, but the source surface is no longer a silent `void` wrapper. `TryPush` rejects before enqueue when `_queue.Count >= _expectedCapacity`, applies finite guards, and records load-shed/corruption telemetry.

Job `ParallelWriter` producers remain a native MPSC compatibility path. They are not managed events and do not allocate managed heap by this source change, but they are still not pre-enqueue capped at the producer instruction. Their overflow is bounded at flush by `LaneOverflowFaultThreshold`, frame caps, coalescing, and deterministic drop/clear behavior.

## Build

Build not launched. Guard check after the source pass reported CPU at 100 percent. A later check showed CPU 100 percent with 0 compiler processes; the CPU threshold alone blocks `dotnet build` under `AGENTS.md`.
