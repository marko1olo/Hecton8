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
| Mandate Registry Currency and Routing | GREEN_STATIC_ALIGNMENT_PENDING_RUNTIME_PROOF | 5 | 80 | 5 | 0 | 22 | 00_mandate_registry\REPORT.md |
| Project Routes, Taste, Quality, Agent Entry | GREEN_STATIC_ALIGNMENT_PENDING_RUNTIME_PROOF | 6 | 12 | 5272 | 0 | 70 | 00_project_routes\REPORT.md |
| Generated Meshes, Textures, Materials, LOD, Collision | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 10 | 6 | 2049 | 249 | 715 | 01_generated_assets\REPORT.md |
| UI, Menus, HUD, Terminals, Localization, Settings | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 7 | 6 | 207 | 241 | 128 | 02_ui_frontend_hud\REPORT.md |
| Rendering, Shaders, Lighting, VFX, Water Presentation | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 10 | 19 | 189 | 109 | 823 | 03_rendering_visuals\REPORT.md |
| Runtime Architecture, Data, Bootstrap, Telemetry, Performance | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 8 | 10 | 388 | 275 | 123 | 04_runtime_architecture_data_telemetry\REPORT.md |
| Physics, Vehicles, Pressure, Water Truth, Survival Physiology | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 7 | 12 | 267 | 281 | 568 | 05_physics_vehicles_water\REPORT.md |
| World, Terrain, Voxels, Geology, Ecosystem, Celestial | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 8 | 8 | 319 | 254 | 628 | 06_world_terrain_voxels_ecosystem\REPORT.md |
| Gameplay, Tools, Construction, Inventory, Combat, Economy | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 8 | 6 | 317 | 193 | 74 | 07_gameplay_construction_tools_inventory_combat\REPORT.md |
| AI, Creatures, Sonar, Drones, Navigation | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 7 | 9 | 185 | 70 | 137 | 08_ai_creatures_sonar_drones\REPORT.md |
| Audio, Narrative, PDA, Cinematics, Public Text | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 7 | 6 | 161 | 149 | 107 | 09_audio_narrative_presentation\REPORT.md |
| Persistence, Streaming, Release, Platform, Modding, Testing | YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING | 8 | 13 | 631 | 126 | 1345 | 10_persistence_streaming_release_platform\REPORT.md |

## Route Integrity

- PROJECT_BIBLES.md route targets: no missing root markdown files detected by static route parse.

## Highest Priority Next Work

- YELLOW: All runtime suspect queues now have system-local `LINE_LEVEL_CLASSIFICATION.md` files. Next work is blocker closure plus Unity/player/profiler/device evidence, not more raw grep classification.
- P0/P1: close registered runtime asset/fallback blockers (`RB-001`, `RB-015`, `RB-017`, `RB-018`, `RB-123`, `RB-130`, `RB-131`, `RB-132`) and collect proof packets listed in each system report.

## Dynamic Summary Reports

- `WHAT_EXISTS_WHAT_MISSING.md` - compact per-system exists/missing/proof status.
- `MANDATE_CURRENCY_MATRIX.md` - every `.agents-skills/*.txt` mandate mapped to expected bible routes and audit groups.
- `MANDATE_CURRENCY_SUMMARY.md` - mandate status and repeated currency flags summary.
- `MANDATE_ACTUALITY_INDEX.md` - per-system mandate/bible/code actuality verdicts; identifies where root bibles override stale mandate wording.
- `RISK_CATEGORY_MATRIX.md` - static runtime suspect categories by system.
- `RUNTIME_PRECLASSIFICATION_MATRIX.md` - heuristic first-pass legality/risk classes by system.
- `HOTSPOT_REVIEW.md` - files with the highest concentration of unresolved `REVIEW_*` risk lines.
- MANUAL_REVIEW_PASS_1.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_10.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_11.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_12.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_13.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_14.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_15.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_16.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_17.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_18.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_19.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_2.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_20.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_21.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_22.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_23.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_3.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_4.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_5.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_6.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_7.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_8.md - human static review notes for critical hotspot pass.
- MANUAL_REVIEW_PASS_9.md - human static review notes for critical hotspot pass.
- Per-system `MANUAL_REVIEW.md` files - human static review notes for system-local hotspot classification when present.
- Per-system `RUNTIME_TRIAGE.md` files - first-pass categorized review queues for runtime suspects.
- Per-system `RUNTIME_PRECLASSIFICATION.md` files - generated suspect lines grouped by preliminary class.
- Per-system `LINE_LEVEL_CLASSIFICATION.md` files - static method-level classification for every runtime suspect queue; strongest status remains runtime proof pending.
- Per-system `MANDATE_ACTUALITY.md` files - static review of whether mandate wording, root bible authority, and current code evidence agree.
- `_scans/` - full raw evidence and risk lists for follow-up review.

## Rerun Command

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Audit\Run-BibleMandateAudit1700.ps1
```
