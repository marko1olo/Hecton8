# 2009 Gemini Surface Shallows Prompt Packs

Batch ID: 2009  
Evidence class: STATIC_DOC / STATIC_SOURCE  
Unity, import, image generation, profiler, and visual acceptance: NOT RUN  
Assets edited: NO

## Boundary

This file is a production prompt pack and handoff contract only. It does not claim generated images exist, does not import textures, does not bind Unity materials, does not edit `Assets/**`, and does not prove runtime visuals.

Use these prompts for candidate source generation only. A candidate becomes production input only after the intake manifest is filled, source files exist, channel derivation QA passes, and the route owner imports/binds/proves the result in Unity.

## Authority Basis

Read authorities: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `3DMODEL_GEOLOGY_ROCKS.md`, `3DMODEL_FLORA_CORAL.md`, `celestial.md`, `atmosphere.md`, `water.md`, `terrain.md`, `presentation.md`.

Read mandates: `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`.

Scoped source inspection:

- `Assets/_Project/Editor/GeminiWorldBuilder.cs`: MapMagic graph builder, 27 depth tiers x 4 region matrix, static code only.
- Batch19 1905/1906/1907/1908 packets: source prompt, PBR channel QA, coastline material package, flora/coral/kelp atlas prep.
- `Aegir_storms.png`: found at `Assets/_Project/Art/TEXTURES/Aegir_storms.png` and prologue gas giant path, both 4096x2048.
- Crest source hooks: `WaveNormals.png`, `foam.png`, `Foam2.png`, `Caustics_tex_color.png` exist under `Assets/Crest/Crest/Textures`.
- Static scans found sky/Aegir/moon materials, generated shallow flora/geology families, shoreline/coastline placeholder and final-prefab routes. This is source evidence only.

## Shared Gemini Prompt Prefix

Use this exact prefix before every material-source prompt unless the row says `REFERENCE_ONLY`:

```text
Create one square source image for HECTON-8. Orthographic top-down material or mask source. Seamless tileable when the row says tileable. Diffuse even daylight. No perspective, no horizon, no object render, no labels, no text, no logo, no UI, no watermark, no frame, no baked lighting, no cast shadows, no directional highlight. Cinematic-realistic NASA-punk deep-sea material truth. Bright surface and photic-shallow readability. Subnautica-level or better visual floor. Not cartoon, not painterly, not low-poly, not blurry, not muddy, not stock-photo scene, not dark/noir surface cover.
```

## Global Negative Constraints

Reject outputs containing copyrighted game/style copying, brand-like marks, text, UI glyphs, watermarks, scenic camera perspective, horizon lines, baked sun shadows, rendered object silhouettes, primitive/crayon forms, muddy grayscale, black/noir surface default, generic blue-purple gradient identity, random neon, flat wallpaper noise, or channels that cannot be separated into declared PBR/source roles.

## Channel Contracts

Default world material source stack:

- Albedo: sRGB base color only, no baked shadows or highlights.
- Height source: linear grayscale or EXR source for offline normal/AO/roughness derivation.
- Normal: derived offline, tangent-space, BC5 route where supported.
- MRAO: linear, R = Metallic, G = Roughness unless shader owner declares smoothness, B = AO, A = Wetness, emission, or family mask as declared per row.
- Emission: sparse and semantic only; biolum, instrument, hot vent, or route cue. No random glow.
- FoamRibbon RGB: R = long-flow foam strands, G = cross-flow breakup, B = edge noise/bubble breakup. Alpha unused unless route owner declares it.
- ProductFace ToolDecayLit PackedMaskV1: R = Metallic, G = AO, B = Smoothness, A = EmissionMask.
- ProceduralBio ORM: R = Occlusion, G = Roughness, B = Metallic, A = EmissionMask.
- Visor masks: R = dirt, G = scratch, B = salt, A = condensation.

No row may guess packed-channel order. If the final shader differs, repack after shader-slot discovery.

## Prompt Pack

| Prompt ID | Category | Resolution target | Tileability | PBR/source need | Gemini prompt delta | Reject if |
|---|---|---:|---|---|---|---|
| G2009-SKY-001 | Aegir gas giant cloud band source | 4096x2048 equirect source or 4096 square panel | Horizontal wrap required if equirect | Albedo/cloud source, storm mask derivation | Blue-purple methane gas giant cloud bands, nested turbulent strata, soft eddies, pale cyan storm filaments, deep cobalt/violet gas lanes, believable atmospheric softness, no planet disc unless equirect source is requested by route owner. | Sine stripes, flat sticker disc, muddy blur, hard horizon cut, unstructured purple gradient. |
| G2009-SKY-002 | Aegir storm density mask | 4096 square or 4096x2048 | Tileable/wrappable | Linear mask source | Grayscale storm-density source for Aegir, organized cyclonic cells embedded in band flow, varying cloud opacity, clean separable white storm cores and gray veil regions, no lighting. | Random noise, uniform density, thundercloud photo, baked terminator, no band relation. |
| G2009-SKY-003 | Aegir limb/haze softness | 2048 square | Not tileable | Linear radial/edge mask | Soft atmospheric limb falloff and horizon veil mask source for Aegir integration, clean radial gradient with turbulent edge feathering, no color art, no hard alpha edge. | Hard cutout, opaque fog wall, noisy speckle, halo that reads like UI glow. |
| G2009-SKY-004 | Moon icy regolith surface | 4096 square | Tileable source panel | Albedo + height source | Cold gray-blue icy moon regolith, fine craters, fractured frost crust, salt/mineral grains, impact micro-ridges, extractable base color and relief, no planet sphere. | Whole moon render, terminator shadow, basalt terrain reuse, flat gray ball source. |
| G2009-SKY-005 | Moon basalt-salt crater source | 4096 square | Tileable source panel | Albedo + height + AO source | Dark basalt and pale salt-crust moon surface, shallow crater rims, radial ejecta grains, fractured plates, scale-free material panel for body-specific moon texture derivation. | Earth moon copy, scenic space scene, baked lighting, identical craters tiled like stamps. |
| G2009-SKY-006 | Bright premium cloud atlas | 4096 square | Tileable atlas cells | Cloud color + coverage masks | Bright surface cloud atlas sheet, soft cumulus and high wispy layers, daylight white/off-white/soft cyan, alpha-friendly edges, multiple cloud cells separated enough for mask extraction. | Storm-only darkness, stock sky panorama, hard card edges, flat white blobs. |
| G2009-SKY-007 | Horizon haze and sky color LUT source | 2048x512 or 1024x256 | Horizontal wrap | Gradient/LUT reference only | Clean photic-surface sky color ramp, bright cyan upper sky, pale green-blue ocean horizon, subtle Aegir color influence, soft haze bands preserving coastline silhouettes. | One-note blue, purple sci-fi gradient, fog wall, darkness hiding terrain. |
| G2009-WTR-001 | Ocean foam lace | 2048 square | Tileable | Albedo/mask source | White translucent sea foam lace over clear blue-green water edge, micro bubbles, broken strands, nonuniform cells, thin wet edge breakup, 0.5 m per tile. | Opaque paint, snow, hard repeated bubbles, storm mud. |
| G2009-WTR-002 | Foam ribbon packed source | 2048 square | Tileable directional | FoamRibbon RGB | Long shoreline foam strands in one flow direction, secondary cross-flow erosion, bubble pinholes, separable long/cross/breakup regions for RGB channel extraction. | Uniform bands, nonseparable shapes, alpha dependency, scenic shoreline photo. |
| G2009-WTR-003 | Caustic mask source | 2048 square | Tileable | Linear caustic mask/flipbook source | Abstract shallow-water caustic lace, crisp natural interference, medium contrast, no seafloor material, designed for projected mask or flipbook where shallow light is justified. | Contains baked seabed lighting, too high contrast, non-tileable scene, abyss-global caustics. |
| G2009-WTR-004 | Ocean detail normal source | 2048 square | Tileable | Height/normal derivation source | Top-down long-swell ocean breakup height source, small capillary ripples crossing broad swell, subtle foam hints removed from height channel if needed, clean photic water read. | Perspective waves, horizon, glare, storm-black water, impossible sharp noise. |
| G2009-WTR-005 | Wet basalt albedo source | 4096 square | Tileable | Albedo source | Wet black-gray volcanic basalt shoreline, fractured strata, teal mineral staining, salt residue, pores, small sediment in cracks, bright readable surface material without baked specular. | Pure black crush, glossy plastic, flat procedural noise, no wet/dry history. |
| G2009-WTR-006 | Wet basalt height source | 4096 square | Tileable | Height/normal/AO source | Grayscale relief source for wet basalt, chipped high ridges, pores, eroded cavities, salt pits, fracture lips, sediment-filled cracks, clean extractable height. | Color-photo luma pretending to be height, directional shadows, random noise. |
| G2009-WTR-007 | Wet/dry roughness mask source | 2048 square | Tileable directional | Roughness/wetness/AO source | Wet-dry basalt transition mask, saturated lower wet band, drying gray-black upper rock, salt/mineral crust, foam residue, cavity wetness, no highlights. | Simple gradient, black smear, oil-only dirt, hides rock material. |
| G2009-WTR-008 | Shallow water clarity/detail source | 2048 square | Tileable | Color/detail reference | Clear photic shallow water material source, blue-green depth tint, faint suspended mineral shimmer, soft surface breakup, suitable for water-color/detail reference only. | Generic blue fog, aquarium haze, blurry stock water, dark storm grade. |
| G2009-GEO-001 | Beach cobble basalt field | 4096 square | Tileable | Albedo + height + roughness source | Wet and damp basalt cobbles, black volcanic stones, pale shell chips between stones, sand/silt packed in gaps, rounded wave erosion, clear scale witnesses. | Dry generic gravel, repeated pebble stamps, beige beach stock photo. |
| G2009-GEO-002 | Coast cliff wet/dry face | 4096 square | Tileable vertical strip | Albedo + height + waterline masks | Coastal cliff material strip, dry upper basalt, wet lower band, vertical mineral streaks, wave undercut chips, salt bloom, algae only in damp pockets. | Flat gray wall, pure black bottom, no strata, baked side lighting. |
| G2009-GEO-003 | Shoreline limestone/basalt blend | 4096 square | Tileable | Albedo + height + AO source | Mixed wet limestone and basalt shelf, layered mineral bands, chipped fracture edges, tidepool abrasion, algae/sediment in cracks, photic readability. | Generic cave wall, smooth blob rock, no waterline history. |
| G2009-GEO-004 | Terrain coastline blend map source | 2048 square | Tileable/control source | Control mask source | Terrain blend-control source for rock, black sand, silt, coral rubble, and wetness bands, clean separable regions with organic shoreline breakup, no visible objects. | Nonseparable mud, noisy mask, resource dots, scene photo. |
| G2009-GEO-005 | Coast cliff geology hero reference | 4096 reference | Not tileable | Reference only | Neutral isolated reference sheet for coastline cliffs, arches, tidepool rims, eroded basalt shelves, cobble fields, and shallow reef anchor rocks, showing silhouette logic and material zones. | Finished game screenshot, dark cover-up, low-poly toy cliffs. |
| G2009-BIO-001 | Kelp blade fiber | 4096 square | Tileable lengthwise | Albedo + height + roughness source | Kelp blade surface with lengthwise fibers, olive/bronze/deep green translucent membrane, ribs, tears, edge thickness, salt micro-scratches, root-to-tip flow. | Flat green plastic, leaf photo perspective, alpha-card wallpaper. |
| G2009-BIO-002 | Kelp stipe/stem ribbing | 4096 square | Tileable lengthwise | Albedo + normal/roughness source | Kelp stipe/stem material, ribbed tapered wet tissue, darker root base, pale rubbed ridges, small scars, sediment trapped near anchor side, extractable longitudinal normals. | Tree bark, rope, uniform cylinder texture, dry brown dirt. |
| G2009-BIO-003 | Kelp holdfast root | 4096 square | Tileable | Albedo + AO + wetness source | Kelp holdfast root clusters gripping rock, knotted olive-brown pads, sand abrasion, cavities, wet tissue, root socket AO, pale worn contact tips. | Loose roots floating, generic plant roots, muddy mass. |
| G2009-BIO-004 | Branching coral calcified surface | 4096 square | Tileable | Albedo + height + AO source | Calcified branching coral pores, welded branch collars, knuckles, chipped tips, pale calcium with muted coral/teal mineral staining and sediment in intersections. | Smooth tubes, plastic antlers, neon coral, whole coral object render. |
| G2009-BIO-005 | Massive coral pitted surface | 4096 square | Tileable | Albedo + height + AO source | Massive coral dome surface, lobed old reef growth, crater pores, wet matte calcium, subtle algae in cavities, chipped shelter holes, photic color restraint. | Generic rock, cartoon sponge, blurry pore dots. |
| G2009-BIO-006 | Plate coral rim/underside | 4096 square | Tileable/sheet | Albedo + height + AO source | Plate coral ledge material, thick layered rims, underside striations, chipped shelf terraces, mineral bands, AO under plate lips, sparse crack biolum mask. | Paper sheets, mushroom caps, flat decals, full-surface glow. |
| G2009-BIO-007 | Reef sponge/low coral bed | 4096 square | Tileable | Albedo + height + AO source | Low coral and reef sponge bed, porous mounds, sponge openings, calcium grains, shell scars, sand abrasion, muted reef colors, localized pore glow only. | Flat carpet, cartoon sponge, noisy gravel, neon flood. |
| G2009-BIO-008 | Reef fan soft rib sheet | 4096 square | Source sheet | Albedo + mask + height source | Soft reef fan and brittle coral rib sheet, branching support veins, holes, tears, frayed edges, pale mineral ribs, muted teal/coral tissue, edge-only biolum masks. | Transparent flat card, decorative lace, random glow, no rib structure. |
| G2009-BIO-009 | Biofilm/algae mineral contact | 2048 square | Tileable/decal source | Detail/decal mask source | Thin shallow biofilm, algae streaks, mineral crust, salt speckling, cyan-green traces over rough wet substrate, useful for coral/rock/root contacts. | Mold wallpaper, uniform green wash, random glowing speckles. |
| G2009-PF-001 | ProductFace tool painted metal | 4096 square | Tileable | ToolDecayLit albedo + PackedMask source | Pressure-aged orange-gray tool paint over metal, chipped edges, salt oxidation, tiny scratches, oil grime in recesses, exposed metal only where paint breaks. | Generic grunge, text labels, clean sci-fi plastic, metallic paint error. |
| G2009-PF-002 | ProductFace rubber grip | 2048 square | Tileable | Albedo + normal + roughness source | Black rubberized grip, ribbed micro texture, worn finger contact, salt dust in grooves, small cuts, matte wet wear, no object render. | Tire photo, black mush, glossy plastic, no grip scale. |
| G2009-PF-003 | ProductFace glass/visor scratches | 2048 square | Tileable mask source | Visor scratch/salt/condensation source | Transparent pressure glass scratch and salt-residue mask source on neutral substrate, fine arcs, fingerprints, condensation islands, readability-preserving dirt. | Window frame render, opaque dirt, unreadable grime, UI symbols. |
| G2009-PF-004 | ProductFace composite hull abrasion | 4096 square | Tileable | MRAO/albedo/height source | Pressure-rated composite hull abrasion, gray ceramic-fiber matrix, worn paint remnants, salt scratches, grime in cuts, damp edge wear, no labels. | Camouflage, clean plastic, no material scale, baked light. |
| G2009-PF-005 | ProductFace oxidized connector | 2048 square | Tileable | Albedo + roughness + metallic source | Aged brass/copper connector surface, green-blue oxidation in seams, polished contact ridges, salt pitting, oil grime, metal zones separable from corrosion. | Coin photo, luxury gold, metallic rust, object silhouette. |
| G2009-PF-006 | ProductFace industrial label decal source | 2048 sheet | Sheet, not tileable | Decal source only | Worn NASA-punk industrial label/decal source with abstract hazard bands, serial-box shapes without readable text, chipped paint, salt abrasion, off-white/amber/cyan markings. | Readable text, real logos, UI panel, gameplay instructions, copyrighted markings. |

## Low / Middle / High / Ultra Consequences

These are continuous `GlobalQualityWeight` consequences, not binary modes.

- Low / compact near 0.0: 512-1024 shipped derivatives where possible, 2048 source retained for route-critical sky/coast/flora; fewer layers and variants; strong silhouette, bright surface color, wet/dry identity, foam breakup, Aegir/moon readability, and route cues must survive.
- Middle around 0.35: 1024-2048 derivatives for key world materials; stronger normal/roughness/AO separation; more foam/cloud/detail variants; coral/kelp/geology source families become distinct instead of shared catch-all.
- High around 0.7: 2048-class hero derivatives, richer wetness, storm masks, coral pores, kelp fibers, cliff strata, ProductFace scratches, and channel QA depth.
- Ultra near 1.0: 2048/4096 hero-only source residency, dense decal/mask layers, richer Aegir/cloud/moon atmosphere, stronger material detail. No gameplay truth, channel semantics, save identity, collider route, or owner route changes.

## Three-Pillar Checks

- Graphics: Candidate must improve material/celestial/water/source identity and preserve bright surface/photic readability. Static prompts alone do not pass graphics.
- Optimization: Candidate must support offline PBR derivation, shared atlases, packed masks, mip safety, and shader/visual fakes. Static prompts alone do not pass optimization.
- Gameplay: Candidate must clarify route, scale, wetness, ecology, ProductFace affordance, or sky/celestial timing context. Decorative-only beauty is insufficient.

## Handoff Routes

- Sky/Aegir/moons/cloud/horizon: celestial + atmosphere + rendering owner. Use route shader slots only.
- Ocean foam/caustic/detail/water clarity: water + rendering owner. Crest inputs remain third-party route-owned; do not clone/override complex Crest materials without owner approval.
- Shoreline wet/dry rock, cliff, cobble, terrain blend: terrain + geology + rendering owner.
- Kelp/coral/sponge/fan/biofilm: flora/coral + ecosystem + terrain placement owner.
- ProductFace sources: ProductFace/tools/player/vehicles/construction owner. No direct prefab binding from generated images.

## Final Claim

This packet defines 35 Gemini-ready source prompts and their static channel/QA contracts. It generated no images and imported nothing.
