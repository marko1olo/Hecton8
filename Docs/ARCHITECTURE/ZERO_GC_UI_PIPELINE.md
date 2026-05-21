# Zero-GC UI Pipeline

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Rule

UI hot paths must not allocate managed memory.

Rejected in hot paths:

- `.ToString()`
- string concatenation
- interpolated strings
- LINQ
- `StringBuilder` growth
- dynamic world-space Canvas creation
- per-frame TMP text assignment with new strings
- hierarchy rebuilds for numeric telemetry

## Text Route

Required route for changing numeric text:

1. Lease a fixed char span or array from `CharBufferPool`.
2. Format values through `Span<char>.TryFormat`.
3. Commit to TextMeshPro through `SetCharArray()`.
4. Return or reuse the buffer according to pool ownership.

No hot-path UI code may allocate to format health, oxygen, depth, power, inventory counts, telemetry, or debug readouts.

## Layout Route

- Stable HUD widgets use preallocated elements.
- World markers use pooled presenters or indirect render data.
- UI state reads published DTO snapshots.
- Gameplay truth never lives in a Canvas, TMP component, or `GameObject` hierarchy.

## Continuous Quality

`GlobalQualityWeight` may scale optional UI effects:

- scanline density
- hologram ray steps
- blip count
- update cadence for presentation-only widgets
- nonessential diagnostic density

It must not change UI truth, save identity, or command routing.

## Verification Boundary

Static source compliance is not GC proof. A `0 B/frame` claim requires GCMonitor or Memory Profiler artifact for the target UI route.
