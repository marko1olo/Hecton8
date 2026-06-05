# FAUNA SURFACE PHOTIC TEXTURE PROMPTS

Date: 2026-06-04  
Target: Gemini prompt package for first-hour surface, photic, and medium-depth fauna materials.  
Boundary: prompts only. No claim that textures, meshes, materials, prefabs, or runtime results exist.

## Universal Contract

Use for every prompt:

- HECTON-8 visual language: NASA-punk, deep sea noir, premium photic ocean life.
- Surface and photic fauna must be bright, readable, wet, and beautiful. Do not hide weak art in darkness.
- Creature anatomy must look coherent and authored: head or sensory zone, locomotion fins, body mass, breathing or filtering structures, material zones, scars or biological asymmetry.
- No primitive shapes, capsules, spheres, tubes, crude blobs, toy colors, flat fills, plastic, cartoon fish, or generic alien monsters.
- No text, logos, UI marks, labels, hard baked shadows, baked lighting, muddy albedo, or monochrome skin.
- Texture set must support `Hecton_LeviathanOrganic`.
- Outputs must be compatible with:
  - Base/albedo map.
  - Normal or height-to-normal reference.
  - Packed `MaskV1`: R metallic or chitin response, G ambient occlusion, B smoothness, A emission mask.
  - `BiolumPulse64` atlas or emission pulse guide when the species uses biolum.
- Keep emission anatomically localized: organs, lateral line, fin edges, lure tips, gill seams, eye slits, or warning patches.
- Photoreal-ish PBR surface detail is required. Painterly blockout is rejected.
- Include a flat material override proof target and a textured render proof target in downstream QA.

Suggested negative prompt for all species:

`primitive capsule, sphere creature, low poly placeholder, flat color, untextured material, toy plastic, cartoon, cute mascot, generic monster, muddy texture, black blob, random glowing dots, full body neon, baked shadow, text, logo, UI decal, symmetrical sticker pattern, crayon texture, proxy mesh, greybox`

## Prompt 01: ShoreSkimmer

Role: small harmless shoal, first safe photic route.

Prompt:

`Create a production PBR texture concept for ShoreSkimmer, a small harmless photic shoal creature in HECTON-8. Body is slim and fast with translucent wet fins, silver-blue dorsal skin, pale cyan belly, tiny ribbed membrane fins, soft gill seams, and subtle lateral-line biolum pinpoints. It must read as safe and alive, not cute or toy-like. Surface detail includes wet scale variation, membrane veins, tiny salt scratches, darker dorsal countershading, and clear photic-water readability. Generate albedo/basecolor guidance, normal/height detail, MaskV1 guidance with chitin/smoothness/AO/emission, and a restrained BiolumPulse64 lateral-line pulse. No baked lighting. No flat color. No primitive fish shape.`

Required proof names:

- `QA_FAUNA_SHORESKIMMER_textured_render_20260604.png`
- `QA_FAUNA_SHORESKIMMER_flat_material_override_20260604.png`
- `QA_FAUNA_SHORESKIMMER_material_channels_20260604.md`
- `QA_FAUNA_SHORESKIMMER_vat_loop_20260604.mp4`

## Prompt 02: KelpRaylet

Role: small harmless shoal or curious passive raylet near kelp and reef corridors.

Prompt:

`Create a production PBR texture concept for KelpRaylet, a small passive ray-like photic fauna species. Anatomy has broad flexible side fins, a compact central body, kelp-camouflage dorsal pattern, amber and cyan biolum freckles near fin roots, pale underside, and ribbed membrane texture. The material must feel wet, thin, and organic with subtle translucency cues but no full transparency requirement. Dorsal pattern should break silhouette among kelp without becoming muddy. Generate albedo/basecolor guidance, normal/height membrane ribs, MaskV1 channels, and a low-intensity fin-root BiolumPulse64 guide.`

Required proof names:

- `QA_FAUNA_KELPRAYLET_textured_render_20260604.png`
- `QA_FAUNA_KELPRAYLET_flat_material_override_20260604.png`
- `QA_FAUNA_KELPRAYLET_lod_distance_strip_20260604.png`
- `QA_FAUNA_KELPRAYLET_material_channels_20260604.md`

## Prompt 03: SiltDrifter

Role: harmless bottom drifter and route calm marker.

Prompt:

`Create a production PBR texture concept for SiltDrifter, a passive bottom-drifting photic fauna creature. Body is low and gliding with soft ventral fins, sediment-dusted belly, warm grey-blue dorsal skin, small protective plates, fine sand abrasion, and low amber-cyan biolum dots along gill seams. It must read as harmless and route-stabilizing while still premium and alien. Surface detail must separate silt dust, wet skin, small chitin plates, soft membrane folds, and pale underside. Generate albedo/basecolor guidance, normal/height sediment and plate detail, MaskV1 guidance, and optional dim BiolumPulse64 guide.`

Required proof names:

- `QA_FAUNA_SILTDRIFTER_textured_render_20260604.png`
- `QA_FAUNA_SILTDRIFTER_flat_material_override_20260604.png`
- `QA_FAUNA_SILTDRIFTER_compact_readability_20260604.png`
- `QA_FAUNA_SILTDRIFTER_material_channels_20260604.md`

## Prompt 04: LanternSifter

Role: curious medium fauna and biolum navigation cue.

Prompt:

`Create a production PBR texture concept for LanternSifter, a curious medium photic-to-twilight filter-feeding fauna creature. Anatomy has a calm sensory face, filter whisker structures, side fins, a broad wet body, translucent throat membranes, and paired lantern organs used for navigation cues. Color design uses teal, deep blue, pale pearl, and controlled cyan biolum. It must look intelligent and curious, not hostile, not cute. Surface detail includes membrane folds, filter pores, scarred wet skin, chitin edges around lantern organs, and strong readability in bright photic water and medium-depth haze. Generate albedo/basecolor guidance, normal/height detail, MaskV1 channels, and a BiolumPulse64 atlas for calm-route pulse, alert pulse, and retreat pulse.`

Required proof names:

- `QA_FAUNA_LANTERNSIFTER_textured_render_20260604.png`
- `QA_FAUNA_LANTERNSIFTER_flat_material_override_20260604.png`
- `QA_FAUNA_LANTERNSIFTER_material_channels_20260604.md`
- `QA_FAUNA_LANTERNSIFTER_biolum_pulse_atlas_20260604.png`

## Prompt 05: WallGlider

Role: curious medium wall and reef route fauna.

Prompt:

`Create a production PBR texture concept for WallGlider, a medium photic reef-wall gliding fauna creature. Anatomy has a vertical-readable silhouette, side grip fins, gliding membranes, darker reef-facing dorsal surface, pale edge fins, and sensory ridges that face the player during curiosity behavior. Material should mix wet skin, biofilm, small chitin grip pads, and subtle blue-green photic highlights. It must read against reef walls without blending into a muddy patch. Generate albedo/basecolor guidance, normal/height membrane and grip-pad detail, MaskV1 channels, and minimal emission at sensory ridges for route recognition.`

Required proof names:

- `QA_FAUNA_WALLGLIDER_textured_render_20260604.png`
- `QA_FAUNA_WALLGLIDER_flat_material_override_20260604.png`
- `QA_FAUNA_WALLGLIDER_hitbox_overlay_20260604.png`
- `QA_FAUNA_WALLGLIDER_material_channels_20260604.md`

## Prompt 06: NeedleHunter

Role: warning or aggressive small predator near hazard pockets.

Prompt:

`Create a production PBR texture concept for NeedleHunter, a small aggressive predator for first-hour medium-depth warning pockets. Anatomy has a narrow fast body, needle-like forward jaw or crest, sharp fin blades, high-contrast warning patches, lean muscle bands, and readable attack direction. Color design uses dark teal wet skin, pale belly, limited warning orange, and cyan slit emission near eyes or gill cuts. It must look dangerous but not oversized or final-boss-like. Surface detail includes scratches, fin edge wear, chitin ridges, high smoothness wet skin, and distinct warning zones visible before attack. Generate albedo/basecolor guidance, normal/height detail, MaskV1 channels, and BiolumPulse64 warning blink guide.`

Required proof names:

- `QA_FAUNA_NEEDLEHUNTER_textured_render_20260604.png`
- `QA_FAUNA_NEEDLEHUNTER_flat_material_override_20260604.png`
- `QA_FAUNA_NEEDLEHUNTER_hitbox_overlay_20260604.png`
- `QA_FAUNA_FIRSTHOUR_predator_warning_distance_20260604.png`

## Prompt 07: PocketAmbusher

Role: small predator for reef or sediment danger pockets.

Prompt:

`Create a production PBR texture concept for PocketAmbusher, a compact ambush predator that hides near reef pockets and sediment ledges. Anatomy has a compressed body, camouflaged dorsal plates, concealed mouth seam, side eyes, short burst fins, and underside warning emission that becomes readable when it turns or lunges. Dorsal material should match reef-sediment colors without becoming flat: slate green, wet basalt grey, sand abrasion, small red-orange warning scars, and cyan eye slits. Generate albedo/basecolor guidance, normal/height plates and mouth seam, MaskV1 channels, and BiolumPulse64 warning flash guide.`

Required proof names:

- `QA_FAUNA_POCKETAMBUSHER_textured_render_20260604.png`
- `QA_FAUNA_POCKETAMBUSHER_flat_material_override_20260604.png`
- `QA_FAUNA_POCKETAMBUSHER_hitbox_overlay_20260604.png`
- `QA_FAUNA_POCKETAMBUSHER_material_channels_20260604.md`

## Prompt 08: Background Silhouette Fish Sheet

Role: distant photic silhouettes and migration bands only.

Prompt:

`Create a distant-only fauna silhouette texture and shape guide for photic fish migration bands. Output should support far background readability, parallax, and route direction in bright ocean water. Shapes must be varied but simple: small shoal darts, long gliders, ray-like profiles, and distant medium silhouettes. Do not create close-range creature body art. Use high-contrast readable silhouettes with subtle fin translucency and water-column breakup. Include guidance for impostor/VAT use, density reduction, and distance-only fade. No flat confetti dots. No cartoon fish.`

Required proof names:

- `QA_FAUNA_BACKGROUND_SILHOUETTES_route_readability_20260604.png`
- `QA_FAUNA_SWARM_PROCEDURAL_DISTANCE_LIMIT_20260604.png`
- `QA_FAUNA_FIRSTHOUR_surface_photic_route_readability_20260604.png`

## Prompt 09: Biolum Navigation Pulse Atlas

Role: living route cue for oxygen return, safe corridors, and depth transition.

Prompt:

`Create a BiolumPulse64 atlas concept for first-hour fauna navigation cues in HECTON-8. The atlas must support calm route pulse, oxygen return pulse, predator warning pulse, curious approach pulse, and retreat scatter pulse. Patterns are anatomically localized: lateral line dots, lantern organs, fin-edge seams, eye slits, gill seams, and underside warning patches. Color range is cyan, teal, pearl, and limited amber warning. It must enhance bright photic beauty and medium-depth readability without turning the whole animal into neon. Provide loop-safe pulse phases and mask-alpha guidance for Hecton_LeviathanOrganic emission.`

Required proof names:

- `QA_FAUNA_BIOLUM_NAV_PULSE_ATLAS_20260604.png`
- `QA_FAUNA_FIRSTHOUR_oxygen_return_cue_alignment_20260604.png`
- `QA_FAUNA_FIRSTHOUR_predator_warning_distance_20260604.png`

## Downstream QA Gate

Do not mark any generated texture package production-ready until the following are present for each close or medium-range species:

- Textured render.
- Flat material override render.
- Wireframe or topology proof.
- LOD strip.
- Hitbox overlay.
- Material channel audit.
- VAT loop proof for swarm or distant animation route.
- Route-context screenshot proving the creature improves readability, oxygen pacing, danger pacing, or photic beauty.

Any output that looks like a primitive, proxy, flat material, generic monster, or random glow fails the first-hour route.
