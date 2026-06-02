# HECTON-8 Bible / Mandate / Codebase Audit 1700

Status: STATIC AUDIT INDEX - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Output root: Docs/BibleMandateAudits/1700/

## Audit Basis

Selected mandates:
- OK .agents-skills\OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OK .agents-skills\OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OK .agents-skills\OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OK .agents-skills\ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OK .agents-skills\ARCH_Execution_Phases.txt
- OK .agents-skills\REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- OK .agents-skills\PHYS_Physics_Integrity_Determinism_ForceMode.txt
- OK .agents-skills\DBG_Telemetry_Crash_Reporting_PostMortem.txt

Runtime boundary: this audit uses static file scans only. Unity import, Console, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, build, and device proof are not implied.

## System Reports

| System | Verdict | Bibles | Mandates | Evidence Files | Runtime Suspects | Editor/Tool Suspects | Report |
|---|---:|---:|---:|---:|---:|---:|---|
| Mandate Registry Currency and Routing | GREEN_STATIC_ALIGNMENT_PENDING_RUNTIME_PROOF | 4 | 80 | 4 | 0 | 22 | 00_mandate_registry\REPORT.md |
| Project Routes, Taste, Quality, Agent Entry | GREEN_STATIC_ALIGNMENT_PENDING_RUNTIME_PROOF | 6 | 12 | 1458 | 0 | 70 | 00_project_routes\REPORT.md |
| Generated Meshes, Textures, Materials, LOD, Collision | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 10 | 6 | 2038 | 249 | 626 | 01_generated_assets\REPORT.md |
| UI, Menus, HUD, Terminals, Localization, Settings | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 7 | 6 | 207 | 241 | 128 | 02_ui_frontend_hud\REPORT.md |
| Rendering, Shaders, Lighting, VFX, Water Presentation | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 10 | 19 | 183 | 109 | 658 | 03_rendering_visuals\REPORT.md |
| Runtime Architecture, Data, Bootstrap, Telemetry, Performance | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 8 | 10 | 387 | 275 | 123 | 04_runtime_architecture_data_telemetry\REPORT.md |
| Physics, Vehicles, Pressure, Water Truth, Survival Physiology | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 7 | 12 | 266 | 281 | 490 | 05_physics_vehicles_water\REPORT.md |
| World, Terrain, Voxels, Geology, Ecosystem, Celestial | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 8 | 8 | 317 | 254 | 539 | 06_world_terrain_voxels_ecosystem\REPORT.md |
| Gameplay, Tools, Construction, Inventory, Combat, Economy | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 8 | 6 | 316 | 193 | 74 | 07_gameplay_construction_tools_inventory_combat\REPORT.md |
| AI, Creatures, Sonar, Drones, Navigation | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 7 | 9 | 185 | 70 | 137 | 08_ai_creatures_sonar_drones\REPORT.md |
| Audio, Narrative, PDA, Cinematics, Public Text | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 7 | 6 | 161 | 149 | 107 | 09_audio_narrative_presentation\REPORT.md |
| Persistence, Streaming, Release, Platform, Modding, Testing | YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED | 8 | 13 | 630 | 126 | 1336 | 10_persistence_streaming_release_platform\REPORT.md |

## Route Integrity

- PROJECT_BIBLES.md route targets: no missing root markdown files detected by static route parse.

## Highest Priority Next Work

- YELLOW: 01_generated_assets - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 02_ui_frontend_hud - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 03_rendering_visuals - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 04_runtime_architecture_data_telemetry - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 05_physics_vehicles_water - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 06_world_terrain_voxels_ecosystem - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 07_gameplay_construction_tools_inventory_combat - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 08_ai_creatures_sonar_drones - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 09_audio_narrative_presentation - classify runtime suspects as cold-path, guarded debug, or runtime violation.
- YELLOW: 10_persistence_streaming_release_platform - classify runtime suspects as cold-path, guarded debug, or runtime violation.

## Dynamic Summary Reports

- `WHAT_EXISTS_WHAT_MISSING.md` - compact per-system exists/missing/proof status.
- `MANDATE_CURRENCY_MATRIX.md` - every `.agents-skills/*.txt` mandate mapped to expected bible routes and audit groups.
- `MANDATE_CURRENCY_SUMMARY.md` - mandate status and repeated currency flags summary.
- `RISK_CATEGORY_MATRIX.md` - static runtime suspect categories by system.
- `RUNTIME_PRECLASSIFICATION_MATRIX.md` - heuristic first-pass legality/risk classes by system.
- `HOTSPOT_REVIEW.md` - files with the highest concentration of unresolved `REVIEW_*` risk lines.
- MANUAL_REVIEW_PASS_1.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_2.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_3.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_4.md - human static review notes for runtime asset fallbacks, GPU scatter/readback, construction mock SDF, water/gas owner phases, and offline QA classification.
- MANUAL_REVIEW_PASS_5.md - human static review notes for world fallback material pools, drone mock routes, Sargassum/brine runtime asset factories, thermal/cable effect roots, and UI runtime assembly.
- MANUAL_REVIEW_PASS_6.md - human static review notes for raw Unity phases, non-editor scene lookup, GPU readback waits, direct debug logging, and Temp/TempJob payload classification.
- MANUAL_REVIEW_PASS_7.md - human static review notes for top native lifetime hotspots, wreck runtime mesh method structure, radar/path/flow staging, UI font/menu cold paths, H8Memory tracking growth, audio ring shape, and scavenging host route.
- MANUAL_FINDINGS_MATRIX.md - manual static verdict matrix by domain.
- RELEASE_BLOCKER_REGISTER.md - static release blockers and proof/fix gates.
- Per-system `MANUAL_REVIEW.md` files - human static review notes for system-local hotspot classification when present.
- Per-system `RUNTIME_TRIAGE.md` files - first-pass categorized review queues for runtime suspects.
- Per-system `RUNTIME_PRECLASSIFICATION.md` files - generated suspect lines grouped by preliminary class.
- `_scans/` - full raw evidence and risk lists for follow-up review.

## Rerun Command

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Audit\Run-BibleMandateAudit1700.ps1
```
