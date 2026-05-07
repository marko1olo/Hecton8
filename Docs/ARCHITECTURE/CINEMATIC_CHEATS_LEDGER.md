# CINEMATIC CHEATS LEDGER
Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-backed ledger of deliberate performance cheats that replace physical simulation with controlled presentation.

## Rule

A cinematic cheat is acceptable only when the gameplay contract does not require full physical truth and the replacement is deterministic, bounded, and visible in source.

## May 7 Entries

| Cheat | Source evidence | Expensive path avoided | Runtime-proof state |
|---|---|---|---|
| Local rain shelter gate | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs` uses cached `BuoyancyObject.IsInDryZone` for local screen-space rain exposure. | Upward shelter physics raycast and TempJob raycast buffers for a visual-only rain effect. | Source verified; profiler/GCMonitor proof still required. |
| AUP thunder distance | `HectonSurfaceWeatherDirector.ResolveAupThunderDistanceMeters(...)` computes thunder delay from `AbsoluteUniversePosition.DistanceSq(...)`. | Camera-relative `Vector3` distance drift for large-world weather logic. | Source verified; runtime event replay still required. |
| Acoustic radar shader presentation | `SuitHUDV4CanvasOverlay` feeds a compact radar texture/material overlay from `SpatialAudioManager` radar payloads. | Per-blip world objects or full UI hierarchy mutation for passive acoustic feedback. | Source verified; HUD GC and canvas rebuild proof still required. |
| Cave-mouth visual blending | Terrain/cave reports and current cave data structures document cave-mouth surface blending as vertex/color/SDF presentation data. | Expensive runtime concave geometry correction at every cave entrance. | Documentation/source map verified; scene visual proof still required. |
| Base-airlock pressure whistle | `Assets/_Project/Scripts/Gameplay/BaseAirlock.cs` samples `ResolveExternalPressureDeltaKPa()` on a frame-mask cadence and raises a procedural audio ping. | Continuous pressure-flow simulation or per-frame leak acoustics for a warning-tone presentation. | Source-present; runtime audio/profiler proof still required. |
| Player slide-blocked speed scalar | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` resolves blocked speed with squared velocity/displacement math and one final `math.sqrt`. | Repeated vector magnitude work during wall-slide/ground-sweep telemetry. | Source-present; movement/profiler proof still required. |
| Fauna biolum fade approximation | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` now computes biolum presentation response with a bounded rational approximation instead of `Mathf.Exp`. | True exponential response in a visual-only presentation fade. | Source-present; visual/profiler proof still required. |

## Non-Claims

- This ledger is not a runtime benchmark.
- This ledger does not certify `0 B/frame`.
- Any entry touching physics, memory, or rendering requires runtime proof before readiness language is allowed.
