# Visual Reference Critique Checklist - 2026-06-05

ID: `VISUAL_OWNER_01_REFERENCE_CRITIQUE_CHECKLIST_WRITER`
Status: `REJECTED / H8_1475_PROOF_PENDING`
Evidence class: `STATIC_DOC + STATIC_IMAGE_METADATA + STATIC_REPORT_SYNTHESIS`
Runtime proof: absent.
Unity proof: absent.
Profiler/GC/memory proof: absent.
Asset mutation: none.

## Evidence Boundary

This checklist is a review instrument for future `h8_1475` screenshot review. It does not claim that current visuals pass. Static image QA can reject weak visuals, but it cannot prove Unity runtime state, scene wiring, frame time, GC, memory, Crest binding, material effectiveness, or product readiness.

Current state remains rejected because the canonical proof packet is missing:

- Required packet root: `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`
- Required packet files: `manifest.json`, `manifest.sha256`, copied Unity log, console export, no-mutation readback report, dirty-state audit, Frame Debugger/Stats report, and canonical screenshots.
- Raw `Docs/Screenshots/MCP/*.png` files are diagnostic context only. They are not acceptance artifacts.

## Mandates Followed

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Relevant authority reads: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `water.md`, `rendering.md`, `ui.md`, `presentation.md`, `quality.md`, `world.md`, `VISUAL_REFERENCE_REJECTION_20260605.md`, `VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.md/.csv`, and `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`.

## Mandatory Reference Inventory

Reference folder: `Docs/OBYAZATELNYE PRIMERY PO KARTINKAM` (actual folder uses Cyrillic basename).

| Reference | Dimensions | Review signal |
|---|---:|---|
| `photo_1_2026-06-04_11-12-33.jpg` | 1280 x 714 | underwater route density, cockpit/HUD framing, water volume, readable negative space |
| `photo_2_2026-06-04_11-12-33.jpg` | 1103 x 624 | water ceiling, surface interaction, refraction, shallow visibility |
| `photo_3_2026-06-04_11-12-33.jpg` | 562 x 533 | shoreline contact, foam/wetness, terrain material scale |
| `photo_4_2026-06-04_11-12-33.jpg` | 714 x 496 | Aegir/sky hero composition, limb atmosphere, cloud/band detail |
| `SSS.jpg / Cyrillic source basename` | 750 x 400 | sky/coast/terrain mood floor, celestial or shoreline material expectation |

The references are mandatory floor signals, not optional inspiration. They require premium visual structure. Darkness, fog, bloom, haze, and screenshot angles cannot be used to hide missing water, terrain, sky, HUD, route density, or material truth.

## Required H8 Screenshot Set

The future `h8_1475` review must use these packet names from the proof execution packet:

- `h8_1475_surface_sky_aegir_ocean_hud_game.png`
- `h8_1475_surface_shoreline_waterline_game.png`
- `h8_1475_photic_terrain_route_game.png`
- `h8_1475_underwater_0_5m_route_game.png`
- `h8_1475_player_hud_binding_scene_selected.png`
- `h8_1475_sky_aegir_slots_inspector.png`
- `h8_1475_crest_ocean_slots_inspector.png`
- `h8_1475_terrain_material_slots_inspector.png`
- `h8_1475_product_face_primitive_targets_inspector.png`

Optional but preferred when safe:

- `h8_1475_underwater_20_50m_route_game.png`
- `h8_1475_pda_or_cockpit_hud_readable_game.png`
- `h8_1475_frame_debugger_sky_ocean_terrain.png`
- `h8_1475_stats_overlay_surface_route.png`

If any required view cannot be captured without mutation, the packet must include `ABORTED_<view>.md` with the failed prerequisite and last safe step. Missing view means `PENDING_VERIFICATION` or `REJECTED`, not pass.

## Critique Rules

Use the checklist rows in the CSV as hard rejection gates. A reviewer may only mark a category as passing when the exact `RequiredH8Artifact` exists in the canonical packet and the visual evidence meets the reference signal without disallowed camouflage.

Disallowed camouflage across all categories:

- darkness/noir grade on surface, shoreline, sky, Aegir, ocean skin, photic shallows, or medium-depth hero route;
- fog, bloom, full-screen haze, marine snow, vignette, depth-of-field, or misleading camera angle hiding weak assets;
- random coral/rock scatter placed over broken base terrain/water;
- decorative UI or fake telemetry that does not expose a player decision;
- raw MCP screenshots, static reports, stale notes, or controller prose used as acceptance proof;
- compact-tier "ugly mode" or binary low/high quality switches.

Severity vocabulary:

- `CRITICAL`: blocks product-face visual promotion and first-20 route trust.
- `HIGH`: blocks category acceptance and requires owner remediation before promotion.
- `MEDIUM`: requires fix or stronger proof before final review, but may not alone block all packet triage.

## Checklist Summary

| Category | Required pass condition | Immediate rejection trigger |
|---|---|---|
| Water volume | Water reads as a medium with ceiling/surface interaction, depth falloff, refraction, route visibility, and non-flat color. | Green/blue slab water, empty fog, black water, no seabed/ceiling read, or no canonical underwater screenshot. |
| Shoreline contact | Waterline shows wet terrain edge, foam/contact breakup, shallow transparency, material scale, and believable geometry contact. | Water beside dark terrain, decorative foam not touching geometry, repeated mask look, or no close shoreline proof. |
| Terrain material truth | Coast/photic terrain shows strata, wet geology, sediment, scale witnesses, route silhouettes, material masks, and non-primitive source. | Crushed silhouettes, noisy slick slopes, primitive blobs, blurry material, random scatter camouflage, or missing material slot proof. |
| Aegir/sky hero quality | Aegir/sky show premium texture detail, cloud/band structure, limb atmosphere, lighting context, and world-route integration. | Muddy pasted sphere, sine stripes, smeared bands, weak limb, duplicate/stale sky ownership, or permanent surface gloom. |
| Underwater density/readability | Route has cliffs/shelves, flora/coral, particles as evidence, fauna silhouettes where relevant, negative space, return cues, and instrument context. | Empty water, full-screen haze/snow, random carpet scatter, no decision, no route cue, or invalid/mislabeled underwater capture. |
| HUD/cockpit/player integration | HUD/cockpit/instruments are readable, physically/diegetically integrated where first-person proof is claimed, and expose oxygen/pressure/power/signal/tool decisions. | Flat generic overlay, fake telemetry, unreadable UI, missing player binding proof, or no decision-bearing instrument state. |
| Proof validity | Canonical packet exists with manifest, checksum, Unity log, screenshots, readback reports, dirty-state audit, and visual comparison. | Raw MCP PNGs only, missing `h8_1475` root, fake hash, absent log, absent manifest, dirty/mutation risk, or static-prose acceptance. |

## GlobalQualityWeight Consequences

- Low/compact near `0.0`: must still preserve readable ocean color, terrain silhouettes, shoreline contact, sky/Aegir readability, route cues, and instrument legibility. Reducing secondary density is allowed; ugly water, muddy sky, and flat terrain are rejected.
- Middle around `0.35`: expected player lane. Must show production player/HUD, route-owned sky/ocean/terrain stacks, no visible proxy/default/primitive contamination, and stable first-20 composition.
- High around `0.7`: saved budget should buy richer cloud/Aegir detail, stronger waterline breakup, denser route geology/flora, cleaner HUD material response, and longer LOD residency.
- Ultra near `1.0`: visual overkill is allowed through layered atmosphere, richer surface sparkle, denser route dressing, and cockpit/visor polish. It must not change gameplay truth, save identity, DTO layout, Crest ownership, or proof state.

## Current Decision

Current visual state remains `REJECTED / H8_1475_PROOF_PENDING`.

No row in this checklist is a current pass. The next valid action is a no-mutation Unity owner producing the canonical `h8_1475` packet, then using this checklist to reject or triage each visual category with artifact-backed evidence.
