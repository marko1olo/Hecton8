# Visual Reference Owner Requirement Matrix - 2026-06-05

Status: `PENDING VERIFICATION / STATIC_IMAGE_QA_ONLY`.
Evidence class: `STATIC_IMAGE_QA + STATIC_DOC`.
Runtime proof: absent.
Unity proof: absent.
Asset mutation: none.

CSV companion: `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv`.

## Scope

This matrix routes the current mandatory visual-reference set to owner packets. It does not accept current visuals, import settings, material bindings, Crest state, terrain state, HUD state, frame time, GC, memory, or build readiness.

Current reference source:

- Folder: `Docs/mandatory if you work on systems that user sees (water, terrain, sky, flora, ui) - read this and all images inside (references)/`
- Path ledger: `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.md/.csv`
- Contact sheet: `Docs/AssetAudit/ContactSheets/mandatory_visual_references_current_20260605.png`

Use `VREF-01` through `VREF-15` from the path ledger before citing filenames. Stale transliterated, Cyrillic, and mojibake folders are historical context only.

## Owner Routing Rules

- Water, shoreline, caustics, and surface contact: start with `ASSET_OWNER_20_OCEAN_CREST_CONTACT_PROOF_PACKET.md`, then `ASSET_OWNER_11_WATER_FOAM_CONTACT_AUTHORING_PACKET.md` and `ASSET_OWNER_24_PRODUCT_FACE_MATERIAL_REPAIR_PACKET.md` where source replacement is required.
- Sky, Aegir, cloud, moon, celestial scale: start with `ASSET_OWNER_14_SKY_AEGIR_CLOUD_SLOT_PROOF_PACKET.md`.
- Terrain, geology, photic cliffs, shoreline material scale: start with `ASSET_OWNER_16_TERRAIN_GEOLOGY_PBR_AUTHORING_PACKET.md`, then texture/material owners.
- Flora, coral, kelp, dense biome dressing: start with `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`, then mesh/prefab owners if geometry is primitive or LOD-invalid.
- Cockpit, HUD, instruments, player-facing frame: start with `ASSET_OWNER_17_UI_OXYGEN_SPRITE_ATLAS_PACKET.md`, `ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`, and `ASSET_OWNER_26_UNITY_READBACK_NO_MUTATION_PACKET.md`.
- Future acceptance route: `ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` only after process gate is clean.

## VREF Matrix

| VREF | Primary route | Owner packets | Required future artifact | Reject if |
|---|---|---|---|---|
| VREF-01 | underwater base/capsule readability | 20, 24, 25, 27, 36 | `h8_1475_underwater_0_5m_route_game.png` | base/capsule reads as toy geometry, water is flat, seabed route is empty |
| VREF-02 | bright shallow arch route | 12, 16, 20, 24, 36 | `h8_1475_photic_terrain_route_game.png` | arch/terrain is blurry, water lacks depth, dressing hides weak base art |
| VREF-03 | primary surface ocean/Aegir/coast target | 12, 14, 16, 20, 24, 36 | `h8_1475_surface_sky_aegir_ocean_hud_game.png` | surface is dark/muddy, Aegir lacks premium bands/limb, coast lacks material truth |
| VREF-04 | cliff waterline contact | 16, 20, 24, 36 | `h8_1475_surface_shoreline_waterline_game.png` | water only sits beside rock, no foam/wet edge/contact breakup |
| VREF-05 | prior sky/Aegir/cliff look | 14, 16, 20, 36 | `h8_1475_sky_aegir_slots_inspector.png` | gas giant is smeared/pasted, sky is gloomy, cliff material is primitive |
| VREF-06 | deep bioluminescent route | 12, 16, 27, 36 | `h8_1475_underwater_20_50m_route_game.png` | biolum is sparse noise, darkness hides route, no readable silhouettes |
| VREF-07 | kelp forest density | 12, 22, 27, 36 | `h8_1475_underwater_0_5m_route_game.png` | kelp is proxy material, floating/detached, no LOD/VAT/static fallback proof |
| VREF-08 | medium-depth capsule route | 16, 24, 25, 27, 36 | `h8_1475_underwater_20_50m_route_game.png` | capsule lacks material scale, geology/flora density is missing |
| VREF-09 | cockpit biome read | 17, 20, 25, 27, 36 | `h8_1475_pda_or_cockpit_hud_readable_game.png` | HUD is flat/fake, cockpit frame is absent, route has no decision state |
| VREF-10 | shallow terrain readability | 12, 16, 20, 27, 36 | `h8_1475_photic_terrain_route_game.png` | seabed is flat/noisy, caustic cue absent, fauna/dressing lacks route purpose |
| VREF-11 | shallow surface shimmer | 12, 16, 20, 36 | `h8_1475_underwater_0_5m_route_game.png` | water ceiling/surface shimmer absent, sand/rock scale collapses |
| VREF-12 | Subnautica-like photic floor | 12, 16, 20, 24, 36 | `h8_1475_photic_terrain_route_game.png` | looks below Subnautica floor, blurry terrain, flat water, no color anchors |
| VREF-13 | prior underwater water ceiling | 16, 20, 36 | `h8_1475_crest_ocean_slots_inspector.png` | water ceiling/contact does not survive Unity readback |
| VREF-14 | medium-deep cockpit route | 16, 17, 25, 27, 36 | `h8_1475_pda_or_cockpit_hud_readable_game.png` | cockpit/HUD is decorative, depth is unreadable, particles hide empty route |
| VREF-15 | prior sky/coast/terrain floor | 14, 16, 20, 36 | `h8_1475_surface_sky_aegir_ocean_hud_game.png` | Aegir/coast/water cannot match prior route mood without hiding weak assets |

## Low / Middle / High / Ultra Consequences

- Low/compact: every VREF still requires readable silhouettes, water color, material identity, and route cues. Density can reduce; flat or muddy visuals are rejected.
- Middle: expected proof lane. It must show route-owned sky/ocean/terrain stacks, no visible proxy materials, and no primitive product-face geometry.
- High: spend saved budget on richer Aegir/cloud detail, waterline breakup, detail normals, longer LOD residency, and denser near-field biome dressing.
- Ultra: visual overkill can increase density, reflection quality, cockpit polish, and layered atmosphere. It must not change gameplay truth ownership, save identity, DTO layout, or proof state.

Final status: `PENDING VERIFICATION`.
