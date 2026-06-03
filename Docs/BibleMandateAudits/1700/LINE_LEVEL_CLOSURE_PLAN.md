# Line-Level Runtime Closure Plan

Status: ACTIVE STATIC CODE REVIEW - PROFILER AND PLAYER PROOF NOT RUN  
Date: 2026-06-02  
Owner: Agent 1700

This plan exists because the broad audit and manual hotspot passes do not close every static runtime suspect line. The previous passes identify the highest-risk systems and release blockers; this file tracks the remaining line-level work needed before any system can be upgraded beyond static/yellow evidence.

Evidence class for this file: `STATIC_SOURCE` and `STATIC_DOC`. It is not Unity import proof, compile proof, Play Mode proof, profiler proof, GC proof, Frame Debugger proof, Memory Profiler proof, player-build proof, or device proof.

## Governing Mandates

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`: text search is not proof; all claims above static source must remain `PENDING VERIFICATION`.
- `.agents-skills/ARCH_Execution_Phases.txt`: raw runtime phases, same-frame job completion, and hidden callbacks remain suspicious until owner phase is proven.
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: hot-path heap allocation is a release-blocking defect unless excluded from hot paths or proven absent.
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: persistent/native allocations must have one owner, sentinel registration, fixed lifetime, and no hot accessor growth.
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`: critical systems need 300-frame black-box rings and fault dump routes; debug claims need build guards.
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`: runtime assets must use approved async lifecycle routes; sync/raw fallback assets are release blockers unless unreachable.

## Closure Definitions

- `LINE_LEVEL_STATIC_CLASSIFIED`: every static suspect line in the system's `RUNTIME_TRIAGE.md` and `RUNTIME_PRECLASSIFICATION.md` has been method-read and classified as `LEGAL_COLD_PATH`, `LEGAL_EDITOR_OR_DEV_GUARDED`, `RUNTIME_VIOLATION`, or `FALSE_POSITIVE`.
- `BLOCKER_REGISTERED`: every classified `RUNTIME_VIOLATION` or production-policy mismatch has a row in `RELEASE_BLOCKER_REGISTER.md` or an equivalent system-local blocker.
- `RUNTIME_PROOF_PENDING`: static classification is done, but profiler/build/player/device artifacts are still missing.
- `RUNTIME_PROVEN`: requires Unity/player artifacts and is not claimed anywhere in this audit unless artifact paths are listed.

## System Queue

| System | Runtime suspects | Current closure state | Next action |
|---|---:|---|---|
| Mandate Registry Currency and Routing | 0 | `NO_RUNTIME_LINES` | Keep as route/currency audit only. |
| Project Routes, Taste, Quality, Agent Entry | 0 | `NO_RUNTIME_LINES` | Keep as route/currency audit only. |
| AI, Creatures, Sonar, Drones, Navigation | 70 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `08_ai_creatures_sonar_drones/LINE_LEVEL_CLASSIFICATION.md`; do not upgrade to green until fauna/material, ecosystem boot, sonar, drone, and audio artifacts exist. |
| Rendering, Shaders, Lighting, VFX, Water Presentation | 109 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `03_rendering_visuals/LINE_LEVEL_CLASSIFICATION.md`; do not upgrade to green until RenderGraph, GPU, material, shader, fallback asset, readback, and DataVault/TBDR artifacts exist. |
| Persistence, Streaming, Release, Platform, Modding, Testing | 126 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `10_persistence_streaming_release_platform/LINE_LEVEL_CLASSIFICATION.md`; do not upgrade to green until save/load, streaming, Addressables, envelope-only modding, legacy-route quarantine, prewarm, GC/memory, and player-build artifacts exist. |
| Audio, Narrative, PDA, Cinematics, Public Text | 149 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `09_audio_narrative_presentation/LINE_LEVEL_CLASSIFICATION.md`; close `RB-017`, `RB-018`, and cross-routed `RB-123` before any green upgrade, then collect DSP/mix/PDA/quest/capture proof. |
| Gameplay, Tools, Construction, Inventory, Combat, Economy | 193 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `07_gameplay_construction_tools_inventory_combat/LINE_LEVEL_CLASSIFICATION.md`; close dynamic economy/submarine composition, construction mock SDF, drone mock truth, scavenging host, somatic/extractor, construction preview, and vehicle proof gates before any green upgrade. |
| UI, Menus, HUD, Terminals, Localization, Settings | 241 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | See `02_ui_frontend_hud/LINE_LEVEL_CLASSIFICATION.md`; close `RB-131` with authored UI/radar/visor/cockpit assets, prewarm, language/input proof, and 300-frame profiler capture. |
| Generated Meshes, Textures, Materials, LOD, Collision | 249 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `01_generated_assets/LINE_LEVEL_CLASSIFICATION.md`; close `RB-001` runtime wreck mesh generation and `RB-015` world-shell prototype loops; collect authored wreck/vegetation/outpost/impostor/resource/foundation/drone/Sargassum/brine package, import, render, collider, profiler, and player-build proof before any green upgrade. |
| World, Terrain, Voxels, Geology, Ecosystem, Celestial | 254 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `06_world_terrain_voxels_ecosystem/LINE_LEVEL_CLASSIFICATION.md`; close `RB-001` and `RB-015`; collect vegetation/radar/scatter/SDF/voxel/material-pool/authored-package/ecosystem-composition/player-build proof before any green upgrade. |
| Runtime Architecture, Data, Bootstrap, Telemetry, Performance | 275 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `04_runtime_architecture_data_telemetry/LINE_LEVEL_CLASSIFICATION.md`; no new runtime violations found, but close DataVault/H8Memory growth, direct debug/fatal route proof, bootstrap update inertness, lazy native init prewarm, scene transition lifecycle, player rebind scan counters, and ContentAuthority ledger proof before any green upgrade. |
| Physics, Vehicles, Pressure, Water Truth, Survival Physiology | 281 | `LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING` | Use `05_physics_vehicles_water/LINE_LEVEL_CLASSIFICATION.md`; close `RB-001`, `RB-015`, `RB-122`, and `RB-130`; collect collider, vehicle/tether/buoyancy/storm/thermal, readback, black-box, profiler, player-build, and device proof before any green upgrade. |

## Review Order

1. Finish small systems first when they have cross-domain risk. This prevents repeated uncertainty in later reports.
2. Prioritize systems with direct player-facing release blockers next: audio, rendering, persistence/modding, generated assets, UI.
3. Classify every line from the raw `_scans/*_runtime_risks.txt` file, not only the shortened excerpt inside `RUNTIME_TRIAGE.md`.
4. Do not edit generated audit files unless the generator is being changed. Add manual `LINE_LEVEL_CLASSIFICATION.md` files beside each system report.
5. If a runtime violation is found and the fix is clearly local, fix it. If the fix requires scene/prefab/profiler evidence, register or strengthen a blocker instead.

## Non-Negotiable Reporting Rule

No system may be called complete, clean, release-ready, zero-GC, profiler-proven, or MX350-safe from this static closure work alone. The strongest valid status from this layer is:

`LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`
