# Mandate Actuality Index

Status: STATIC MANDATE / BIBLE / CODE ACTUALITY REVIEW - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`

This index is the next layer after line-level runtime suspect classification. It answers whether the mandates and root bibles are current enough to guide implementation against the present codebase.

## Global Verdict

- Root bible routing is structurally current: no missing root markdown route was found in `PROJECT_BIBLES.md`.
- Every yellow implementation system now has a `LINE_LEVEL_CLASSIFICATION.md`.
- Mandate source files are not uniformly current: `MANDATE_CURRENCY_SUMMARY.md` reports 80 scanned mandates, 25 green, 55 yellow, 0 red.
- The 55 yellow mandate files are mostly incomplete rather than false: 48 lack explicit continuous `GlobalQualityWeight` wording, 11 contain deprecated/legacy language, and 1 lacks explicit proof wording.
- Current authority order for agents is: `AGENTS.md` -> root bibles -> system `MANDATE_ACTUALITY.md` / `LINE_LEVEL_CLASSIFICATION.md` -> older `.agents-skills` wording.

## System Actuality Matrix

| System | Mandate actuality verdict | Code actuality verdict | Required next proof |
|---|---|---|---|
| 00_mandate_registry | `YELLOW_SOURCE_WORDING_REFRESH_REQUIRED` | Route coverage exists; source mandates need refresh. | Refresh yellow source mandates or keep root bibles as explicit override. |
| 00_project_routes | `GREEN_ROUTE_CURRENT_STATIC` | Root route files exist; runtime proof is separate. | Keep route index synchronized with every new bible and audit folder. |
| 01_generated_assets | `YELLOW_VALID_BUT_ROOT_BIBLE_OVERRIDES_REQUIRED` | 249 lines classified; `RB-001` and `RB-015` remain. | Authored generated packages, no runtime mesh/material generation, import/collider/render/profiler proof. |
| 02_ui_frontend_hud | `YELLOW_VALID_BUT_LOCALIZATION_AND_UI_PROOF_GAPS` | 241 lines classified; `RB-131` remains. | Authored UI assets, no fallback meshes/materials, localization/input proof, 0 B/frame UI proof. |
| 03_rendering_visuals | `YELLOW_RENDER_MANDATES_NEED_QUALITY_REFRESH` | 109 lines classified; `RB-123` remains. | RenderGraph, Frame Debugger, shader/material/readback/GPU/device proof. |
| 04_runtime_architecture_data_telemetry | `YELLOW_ARCH_MANDATES_NEED_QUALITY_REFRESH` | 275 lines classified; no new violations, multiple proof blockers remain. | DataVault/H8Memory growth, boot prewarm, direct debug, black-box, player-build proof. |
| 05_physics_vehicles_water | `YELLOW_PHYSICS_MANDATES_VALID_BUT_PROOF_INCOMPLETE` | 281 lines classified; `RB-001`, `RB-015`, `RB-122`, `RB-130` remain. | Collider proxy, fixed-step, vehicle/tether/buoyancy/storm/thermal/readback proof. |
| 06_world_terrain_voxels_ecosystem | `YELLOW_WORLD_VOXEL_MANDATES_NEED_LEGACY_REFRESH` | 254 lines classified; `RB-001` and `RB-015` remain. | SDF/DataMonolith, vegetation/radar/scatter/chunk/voxel/material-pool proof. |
| 07_gameplay_construction_tools_inventory_combat | `YELLOW_GAMEPLAY_MANDATES_VALID_BUT_PROOF_GAPS` | 193 lines classified; no new violations, composition proof remains. | First-hour gameplay, economy/submarine composition, construction preview, interaction zero-GC proof. |
| 08_ai_creatures_sonar_drones | `YELLOW_AI_MANDATES_NEED_QUALITY_REFRESH` | 70 lines classified; no new violations. | AI cadence, sensory truth, sonar/SDF, fauna material/collider, drone proof. |
| 09_audio_narrative_presentation | `YELLOW_AUDIO_NARRATIVE_PROOF_GAPS` | 149 lines classified; `RB-017`, `RB-018`, `RB-123` remain. | DSP/native bridge, authored banks/markers/PDA, narrative evidence, subtitle/capture proof. |
| 10_persistence_streaming_release_platform | `YELLOW_STREAMING_MODDING_MANDATES_NEED_LEGACY_REFRESH` | 126 lines classified; no new violations, `RB-132` remains. | Save/load, Addressables, residency, mod envelope, legacy quarantine, player-build proof. |

## Non-Negotiable Interpretation

- A green route is not a green implementation.
- A yellow source mandate is not automatically wrong; it is unsafe as standalone authority if it lacks current proof, quality scaling, or anti-legacy wording.
- Static source classification is not runtime proof.
- No system in this audit is release-ready until its proof artifacts exist and are linked from the system report.

