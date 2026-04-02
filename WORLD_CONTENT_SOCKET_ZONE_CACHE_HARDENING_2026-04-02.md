# WORLD CONTENT SOCKET ZONE CACHE HARDENING — 2026-04-02

## What was wrong

Several world/runtime passes were repeatedly calling `GetComponentInParent<WorldZoneAnchor>()` on the same `WorldContentSocket` objects.

That happened in:

- `WorldContentDirector`
- `WorldPopulationDirector`
- `WorldProceduralFillDirector`

On a larger socket count, this becomes needless hierarchy walking during slow runtime passes.

## What was done

- Added a lazy cached zone reference to `WorldContentSocket`.
- Added `GetZoneAnchor()` so callers can reuse the cached parent zone instead of re-querying hierarchy every time.
- Added cache invalidation on `OnTransformParentChanged()`.
- Updated the three world directors above to use `socket.GetZoneAnchor()`.

## What this means in simple terms

Each socket now remembers which zone it belongs to.

So world systems stop asking the hierarchy the same question over and over again.

## What this gives

- less repeated hierarchy traversal in world slow-pass logic
- cheaper nearest-socket and recommendation evaluation
- no gameplay behavior change

## What was verified

- Unity console returned `0 log entries` after the pass and short play-stop smoke.
- Manual code inspection confirms the new zone-cache method exists once and is used in the expected places.

## What remains open

- This is still a code-level optimization pass, not a measured profiler capture yet.
- The next useful step remains the same: place `RuntimePerformanceProfiler` into the dev scene and collect actual numbers.
