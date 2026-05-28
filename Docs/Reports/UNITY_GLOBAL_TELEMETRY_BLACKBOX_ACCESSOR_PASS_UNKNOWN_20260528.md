# Unity Global Telemetry Blackbox Accessor Pass - UNKNOWN - 2026-05-28

## Scope

Domain: Core global telemetry and blackbox route contracts.

Evidence class: static source proof only. Full build, Unity import, Play Mode, profiler, GC monitor, player build, and device run were not performed in this pass.

## Problem

`GlobalTelemetryBus.TryGetBlackboxRingBuffer()` looked like a read accessor but could call `EnsureBlackboxInitialized()` on the main thread.

That initializer can open DataVault-backed blackbox storage and mutate global telemetry state. Under the current global systems doctrine, a `TryGet*` route must not allocate, publish, sync, or mutate service lifetime state.

## Change

- Removed active source `TryGetBlackboxRingBuffer()`.
- Added `TryResolveBlackboxRingBufferView()` for already-open no-initialization access.
- Added `OpenOrInitializeBlackboxRingBufferView()` for the explicit owner-thread initialization path.
- Moved DTO field assignment into `PopulateBlackboxRingBufferDto()` so pointer, stride, counters, and fatal-hash behavior stay identical.

## Proof

| Check | Result |
|---|---|
| Active old API source call sites | `0` |
| `TryGetBlackboxRingBuffer` after patch | `0` |
| `TryResolveBlackboxRingBufferView` after patch | `1` |
| `OpenOrInitializeBlackboxRingBufferView` after patch | `1` |
| `TryResolveBlackboxRingBufferView` calls `EnsureBlackboxInitialized()` | `false` |
| Touched source brace delta | `0` |
| Scoped `git diff --check` | exit `0`; line-ending warning only |
| CPU guard | `100%`; build/doc heavy gates skipped |

## Architecture Verdict

This was worth doing. It removes a concrete read-accessor purity violation in a Core global route without touching cross-domain systems or the compile wall owned by another agent.

It does not claim runtime speedup. Runtime microseconds saved: `0`.

## Residuals

- `TryResolveBlackboxBuffer<T>()` remains a private pure existing-handle resolver used by mutating and read paths.
- No write-lock redesign was attempted for the raw DTO route because there were no active source callers and no runtime contention proof in this pass.
- Full build was not launched because CPU was `100%` and project rules forbid launching build under that load.
