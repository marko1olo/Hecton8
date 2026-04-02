# WORLD ZONE HOTPATH HARDENING — 2026-04-02

## What was wrong

The world zone/content stack still had repeated small math and hierarchy costs inside its runtime loops.

Two concrete issues stood out:

1. nearest-zone / nearest-socket checks were using full flat distance where only ordering mattered
2. `WorldZoneDirector` was asking each `WorldZoneAnchor` for activation/hold state multiple times in one pass

That meant duplicate distance work, duplicate edge-noise evaluation, and more runtime churn than needed.

## What was done

- Added squared-distance helpers:
  - `WorldContentSocket.GetFlatDistanceSquared(...)`
  - `WorldZoneAnchor.GetFlatDistanceSquared(...)`
- Switched nearest-selection paths to squared-distance comparisons in:
  - `WorldContentDirector`
  - `WorldPopulationDirector`
  - `WorldProceduralFillDirector`
  - `WorldZoneDirector`
- Added `WorldZoneAnchor.EvaluatePlayerState(...)` so `WorldZoneDirector` can evaluate:
  - flat distance
  - activation weight
  - hold weight
  - inside-activation flag
  - inside-hold flag

in one calculation instead of repeating the same work several times.

## What this means in simple terms

World zone logic now does less duplicate math every time it checks where the player is and which zone/socket is currently most relevant.

So it gets to the same answer with less wasted work.

## What this gives

- cheaper nearest-zone and nearest-socket selection
- less repeated Perlin/noise and distance work in `WorldZoneDirector`
- no intended gameplay behavior change

## What was verified

- After the pass, Unity console still showed no new first-party errors.
- The remaining console noise is still third-party/editor warning spam, plus transient MCP websocket noise.
- Manual inspection confirms the new shared evaluation path is wired into `WorldZoneDirector`.

## What remains open

- This still is not a full measured profiler session.
- The project profiler script exists, but it is still not placed in the active dev scene, so the next real step should be numbers-driven profiling.
