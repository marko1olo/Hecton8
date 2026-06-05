# H8 1475 Visual Reference Comparison Template - 2026-06-05

Status: `STATIC_TEMPLATE / PENDING_H8_1475_PACKET`.
Evidence class: `STATIC_DOC`.
Unity run: not performed.
Runtime proof: absent.
Asset mutation: none.

Use this template for the future file:

`Docs/Screenshots/HectonProofPackets/h8_1475_<YYYYMMDD_HHMMSS>/h8_1475_visual_reference_comparison.md`

This template does not prove any visual state. It only fixes the required comparison shape for the no-mutation `h8_1475` proof packet.

## Required Inputs

- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_CRITIQUE_CHECKLIST_20260605.md`
- `Docs/Reports/AssetSystem_20260605/H8_1475_CANONICAL_SHOTLIST_20260605.md`
- `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md`
- `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png`
- `manifest.json`
- `manifest.sha256`
- `UnityLog.txt`
- `console_export.txt`
- `no_mutation_readback_report.md`
- `dirty_state_audit.md`
- `frame_debugger_stats.md`
- canonical `h8_1475_*.png` captures or exact `ABORTED_<view>.md` notes

If any required input is missing, write `PENDING_VERIFICATION` for the affected row. Do not infer a pass from memory, stale MCP PNGs, controller notes, or static reports.

## Packet Header Fields

Future owner must fill:

- `SessionId`:
- `UnityVersion`:
- `ActiveScene`:
- `ProcessGateResult`:
- `MutationResult`: `NO_MUTATION` or `ABORTED_BEFORE_MUTATION`
- `ManifestHashVerified`: `true/false`
- `ConsoleState`: `CLEAR`, `ERRORS_PRESENT`, or `NOT_CAPTURED`
- `DirtyState`: `CLEAN`, `DIRTY_OBJECT_REPORTED`, or `NOT_CAPTURED`
- `FrameDebuggerStatsState`: `CAPTURED` or `NOT_CAPTURED`
- `OverallVisualDecision`: `REJECTED` or `PENDING_VERIFICATION`

No `ACCEPTED`, `READY`, runtime-clean, frame-time, memory, or `0 B/frame` decision is allowed in this file.

## Comparison Table

| Shot | Required artifact | Mandatory reference signals | Required readback/proof | Pass/fail fields | Decision |
|---|---|---|---|---|---|
| `H8_1475_SHOT_01_Surface` | `h8_1475_surface_sky_aegir_ocean_hud_game.png` | `VREF-03 BEST ILLUST`: bright coastline/island, readable ocean/whitewater, dense vegetation or route scale cue, huge Aegir/gas-giant read, layered clouds. `VREF-05` and `VREF-15`: prior Aegir/sky direction only. | Sky/Aegir slot readback, Crest/ocean readback, player/HUD rows, Frame Debugger/Stats, console. | `surface_brightness`, `ocean_read`, `aegir_scale`, `cloud_layers`, `hud_readability`, `no_dark_cover`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_02_Shoreline` | `h8_1475_surface_shoreline_waterline_game.png` | `VREF-03`, `VREF-04`, `VREF-10`, `VREF-11`, `VREF-12`: wet edge, foam/contact breakup, shallow transparency, rock/sand material scale, water touching geometry. | Crest foam/contact slots, terrain material slots, Frame Debugger/Stats, console. | `wet_edge`, `foam_contact`, `shallow_transparency`, `terrain_material_scale`, `no_decorative_ring`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_03_PhoticTerrain` | `h8_1475_photic_terrain_route_game.png` | `VREF-02`, `VREF-09`, `VREF-10`, `VREF-11`, `VREF-12`: readable cyan photic water, terrain shelves/arches, sediment/rock identity, organic density, route/return cue. | Active terrain receiver/material, product-face primitive blocker readback if visible, Frame Debugger/Stats, console. | `terrain_identity`, `route_cue`, `organic_density`, `material_breakup`, `no_random_scatter_camouflage`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_04_Underwater0_5m` | `h8_1475_underwater_0_5m_route_game.png` | `VREF-02`, `VREF-09`, `VREF-10`, `VREF-11`, `VREF-12`, `VREF-13`: readable water ceiling, seabed, caustic impression, particles as depth evidence, scale cue, no flat slab. | Crest underwater material, player/HUD rows, terrain/material readback, console. | `water_volume`, `ceiling_read`, `seabed_read`, `particle_discipline`, `scale_cue`, `no_fullscreen_haze`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_05_MediumDepth` | `h8_1475_underwater_20_50m_route_game.png` or `ABORTED_underwater_20_50m_route.md` | `VREF-06`, `VREF-07`, `VREF-08`, `VREF-14`: foreground/mid/background separation, biolum/color anchors, geology/flora density, route readability, cockpit/instrument context where present. | Crest/ocean, terrain/material, product-face/ecology blocker rows if visible, Frame Debugger/Stats, console. | `depth_layers`, `biolum_anchors`, `silhouette_read`, `route_readability`, `no_black_void`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_06_PlayerHUD` | `h8_1475_player_hud_binding_scene_selected.png` plus gameplay HUD crop if available | `VREF-09` and `VREF-14`: physical cockpit/visor/instrument context, readable oxygen/pressure/power/signal/tool decisions, no flat generic overlay. | Active player source, production prefab binding, `HectonWorldShellController1428` state, HUD canvas render modes, oxygen/sprite rows, dirty-state audit. | `production_player_source`, `diegetic_carrier`, `oxygen_read`, `tool_state_read`, `no_screenspace_camouflage`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_07_ProductFace` | `h8_1475_product_face_primitive_targets_inspector.png` plus route close shot if available | Digest product-face consequence: visible tools/resources/transport/support must not read as cubes, spheres, capsules, planes, flat panels, or blockout materials in surface/photic/medium-depth contexts. | Product-face primitive target readback, renderer/material rows, LODGroup/collider proxy rows, Frame Debugger/Stats where visible. | `nonprimitive_mesh`, `material_identity`, `lod_proof`, `collider_proxy`, `no_default_material`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_08_CrestReadback` | `h8_1475_crest_ocean_slots_inspector.png` | Supports `VREF-03`, `VREF-04`, `VREF-10`, `VREF-11`, `VREF-12`, `VREF-13`; inspector alone cannot pass visual. | Effective ocean/underwater material, foam/normals/caustic slots, `_WD_*` classification, no Crest clone/wrapper. | `canonical_crest_route`, `visible_slots_read`, `wd_slots_classified`, `no_clone_wrapper`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_09_SkyReadback` | `h8_1475_sky_aegir_slots_inspector.png` | Supports `VREF-03`, `VREF-05`, `VREF-15`; inspector alone cannot pass visual. | Active skybox, `Mat_HectonSky` cloud/star slots, Aegir band/cloud/disc slots, active renderer state. | `active_skybox`, `cloud_slots`, `aegir_slots`, `moons_classified`, `no_stale_candidate_claim`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_10_TerrainReadback` | `h8_1475_terrain_material_slots_inspector.png` | Supports `VREF-02`, `VREF-04`, `VREF-10`, `VREF-11`, `VREF-12`; inspector alone cannot pass visual. | Active terrain receiver/material/shader rows, stale material classifications, MapMagic relation if exposed. | `active_receiver`, `material_slots`, `shader_route`, `stale_rows_classified`, `no_source_only_claim`. | `PENDING_VERIFICATION` or `REJECTED` |
| `H8_1475_SHOT_11_FinalPacket` | contact sheet or manifest table | All VREF rows used only as comparison evidence. | Manifest, hash, Unity log, console, dirty audit, no-mutation report, Frame Debugger/Stats, screenshot or abort notes. | `manifest_complete`, `hash_real`, `log_present`, `dirty_audit`, `all_views_accounted`. | `PENDING_VERIFICATION` or `REJECTED` |

## Mandatory Rejection Notes

Future owner must write exact rejection notes for any of these:

- `RAW_MCP_ONLY`: raw MCP PNGs used without canonical packet.
- `MISSING_MANIFEST_OR_HASH`: manifest or actual hash absent.
- `MISSING_READBACK`: screenshot exists but matching active material/player/HUD/terrain/Crest readback is absent.
- `MISSING_FRAME_DEBUGGER_STATS`: render-route proof absent where required.
- `DIRTY_OR_MUTATION_RISK`: dirty state not audited or mutation occurred during no-mutation pass.
- `SURFACE_DARK_COVER`: surface/shore/photic view hidden by darkness, fog, bloom, crop, or exposure.
- `FLAT_WATER`: water reads as slab, fog sheet, black void, or repeated texture.
- `WEAK_AEGIR_SKY`: gas giant/sky reads as muddy disc, pasted sphere, smear, or weak cloud stack.
- `WEAK_TERRAIN`: terrain reads as noisy slope, toy cliff, blurry tile, proxy, or random scatter camouflage.
- `WEAK_ORGANICS`: flora/coral/kelp reads as sparse, card-like, proxy-colored, or aquarium-toy.
- `FLAT_UI`: HUD/cockpit reads as generic overlay, fake telemetry, black icon, or no player decision.
- `PRIMITIVE_PRODUCT_FACE`: visible product asset keeps cube/sphere/capsule/plane/blockout/default material route.

## Decision Rule

One failed critical row makes the packet `REJECTED`.

Any missing required input makes the affected row `PENDING_VERIFICATION`.

This file cannot mark product visuals, runtime, memory, profiler, GC, build, or platform readiness as passed. It only records visual comparison status for the canonical packet.

Final status: `STATIC_TEMPLATE / PENDING_H8_1475_PACKET`.
