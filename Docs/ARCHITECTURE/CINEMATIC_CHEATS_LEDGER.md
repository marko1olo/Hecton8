# CINEMATIC CHEATS LEDGER
Date: 2026-05-11
Status: PENDING VERIFICATION
Scope: source-backed ledger of deliberate performance cheats that replace physical simulation with controlled presentation.

## Rule

This stable ledger is the visual-realistic-fake authority. Dated reports are evidence/counter snapshots only.

A cinematic cheat is acceptable only when the gameplay contract does not require full physical truth and the replacement is deterministic, bounded, and visible in source.

2026-05-11 rule:

- visual fake is the default path for water, light, deformation, pressure, flow, ambience, cable sag, particles, and distant motion
- real simulation must prove that gameplay correctness fails without it
- any frame cost above `0.1ms` is suspicious until measured and justified
- no runtime-readiness language is allowed without profiler/GCMonitor/Unity proof

Mandate authority:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/PHYS_Fluid_Incursion_Interior.txt`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`

## May 11 Registry Corrections

| Area | Correction | Runtime-proof state |
|---|---|---|
| Performance budgets | Added visual-fake-first override and explicit MX350 bans for Bloom and FSR2/DLSS-class temporal upscalers. | Documentation-only; profiler proof still required. |
| Fluid VFX | Reframed particles as presentation, not gameplay fluid truth; APV/probe sampling is MED+ unless measured. | Documentation-only. |
| Flow/weather | Flow fields drive VFX/audio/AI hints first; physics sampling is limited to player/vehicles/hazards/near interactables. | Documentation-only. |
| Fluid incursion | Interior flooding defaults to scalar state plus leak/audio/haptic/shader fakes; real mass/CoM only for gameplay-critical vessels. | Documentation-only. |
| Abyssal lighting | Darkness volumes, baked AO, LUT haze, emissive proxies, and dithered shadow proxies precede raymarch/voxel truth. | Documentation-only. |
| Tether/cable physics | Old ConfigurableJoint/AddForce path is stale against current AGENTS.md; new production path must use custom constraint packets or visual fake. | Documentation-only. |
| Tool/hand physics | Welds, anchors, grabs, drag, recoil, and scatter now route through owned physics packets or presentation fakes; direct tool-side Unity physics calls are stale. | Documentation-only. |
| Underwater audio | HRTF/ITD/ILD becomes optional accessibility/headphone processing, not default underwater realism. | Documentation-only. |

## May 7 Entries

| Cheat | Source evidence | Expensive path avoided | Runtime-proof state |
|---|---|---|---|
| Bio-root sway LUT | `Assets/_Project/Scripts/CaveBioRootsGenerator.cs` uses a 1024-sample sine LUT plus quarter-wave cosine lookup for purely visual root sway. | Per-root, per-frame `Mathf.Sin`/`Mathf.Cos` calls for decorative cave vegetation motion. | Source verified; profiler/GCMonitor proof still required. |
| Bio-root deterministic hash | `CaveBioRootsGenerator.Hash01(...)` uses an integer avalanche hash for placement/phase seeds. | Trigonometric pseudo-random hash and any `UnityEngine.Random` dependency for stable cave dressing. | Source verified; placement replay proof still required. |
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
