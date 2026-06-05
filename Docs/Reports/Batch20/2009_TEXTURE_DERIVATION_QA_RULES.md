# 2009 Texture Derivation QA Rules

Batch ID: 2009  
Evidence class: STATIC_DOC  
Unity, import, image generation, profiler, and visual acceptance: NOT RUN

## Boundary

These rules decide whether a generated candidate source can enter later PBR derivation. They do not approve Unity import, material binding, scene placement, or runtime visuals.

## Intake Required Before QA

Every candidate must have a filled row in `2009_CANDIDATE_INTAKE_MANIFEST_TEMPLATE.csv` copied into a real manifest file by the future intake owner. Required fields:

- prompt ID and exact source file path;
- SHA-256 of the candidate file;
- resolution and tileability claim;
- source role: albedo, height, mask, cloud coverage, foam RGB, reference only, decal sheet, or ProductFace source;
- declared channel contract;
- license/source status;
- negative constraints confirmation;
- handoff owner.

No candidate may be imported, packed, or assigned to a material if the intake row is missing.

## Hard Rejection

Reject immediately if any candidate has:

- text, labels, UI glyphs, logos, watermarks, readable serials, or brand-like marks;
- visible copied copyrighted game art or style plagiarism;
- perspective object render when the row required material source;
- horizon or scenic camera framing in a material-source row;
- baked lighting, cast shadows, terminator shadows, glare, or directional highlights;
- dark/noir surface default, muddy grade, black crush, or fog hiding weak art;
- primitive/crayon/toy/low-poly read;
- nonseparable material channels;
- alpha-blend dependency for dense flora or foam fields on compact lane;
- undefined packed channel order;
- claim of generated image without an existing file path and hash.

## Per-Category QA

### Sky, Aegir, Clouds, Moons

Required:

- Aegir cloud bands show hierarchy: macro bands, meso eddies, micro storm breakup.
- Storm masks are structured and band-aware, not random noise.
- Limb/haze masks soften integration without making a sticker edge or opaque fog wall.
- Moon sources are body-specific and do not reuse terrain basalt visibly as final moon identity.
- Cloud atlas edges are soft and coverage-mask-friendly.
- Horizon/sky sources preserve bright coastline, ocean, and route silhouettes.

Reject:

- sine stripes;
- pale translucent disc/sticker read;
- flat gray moon;
- one-note blue or purple gradient;
- storm darkness as normal surface art.

### Ocean Surface, Foam, Caustics

Required:

- Foam masks have separable long-flow, cross-flow, and breakup regions.
- Caustics are mask/flipbook sources only, justified for shallow light or local projectors.
- Ocean detail sources support normals without perspective wave glare.
- Water color remains bright, clear, and photic in surface/shallow roles.

Reject:

- global abyss caustics;
- generic blue fog;
- opaque paint foam;
- non-tileable scenic waves;
- foam used to hide missing shoreline material.

### Shoreline, Geology, Terrain Blend

Required:

- Wet/dry rock has strata, erosion, mineral stain, waterline, salt, sediment, and roughness logic.
- Height sources describe relief, not color noise.
- Cobble and cliff sources include scale witnesses and wave/pressure history.
- Terrain blend masks are separable into rock/sand/silt/rubble/wetness regions.

Reject:

- smooth blobs;
- flat gray cliffs;
- black smear waterline;
- random resource-dot masks;
- albedo-only final claims.

### Kelp, Coral, Sponge, Fan, Biofilm

Required:

- Kelp blade fibers run root to tip.
- Kelp stems/stipes have ribbing and wet tissue logic.
- Holdfast/root sources include anchor/socket AO.
- Coral surfaces show pores, cups, knuckles, chipped rims, branch intersections, or plate undersides according to family.
- Sponge/fan sources support geometry-backed forms, not transparent flat cards.
- Biolum is sparse, semantic, and compatible with future vertex color G masks.

Reject:

- flat ribbons;
- plastic tubes;
- random neon;
- candy reef colors;
- alpha-card dependency as the primary asset identity.

### ProductFace

Required:

- Tool paint, rubber, glass, hull, connector, and decal sources separate material states clearly.
- Metallic appears only where real exposed metal exists.
- Rubber remains matte/wet-worn, not black mush.
- Visor dirt/scratch/salt/condensation preserves HUD readability.
- Decal sheet has no readable real-world text, logos, or gameplay instructions.

Reject:

- placeholder flat materials;
- package/default Lit donor logic;
- generic grunge;
- clean sci-fi plastic;
- direct generated-image prefab binding.

## Derivation Rules

1. Run 2x2 tile preview for every tileable source. Any obvious seam requires seam repair before PBR derivation.
2. Check albedo histogram. Reject crushed black/white unless a material reference demands it and route owner accepts it.
3. Detect baked lighting. Broad directional gradients, cast shadows, or specular highlights disqualify albedo/height sources.
4. Derive normals only from true height/sculpt/bake-compatible sources. Do not emboss albedo color as physical relief.
5. AO must be cavity-biased. Do not use broad darkness or random dirt as AO.
6. Roughness must follow material state: wet cracks, dry raised rock, rubber grooves, exposed metal, calcified matte coral, slick kelp membrane.
7. Metallic is zero for rock, sand, foam, water, coral, kelp, sponge, clouds, moons unless a real metal/ore/ProductFace row declares exposed metal.
8. Emission is zero unless a row declares biolum, instrument, hot vent, or route cue.
9. Packed channels must pass independence checks. Identical channels are rejected unless a manifest proves a uniform material reason.
10. Mip preview must preserve route identity, not become mud.
11. Compression preview must preserve foam strands, cloud edges, coral pores, kelp fibers, and ProductFace scratches.
12. Reference-only rows must not be packed as material maps.

## Rejection Rubric

Score each item `PASS`, `REPAIR`, or `REJECT`.

- Source exists and hash recorded.
- Prompt ID matches source.
- Legal/source status recorded.
- Negative constraints clean.
- Tileability and 2x2 test pass if tileable.
- No baked lighting.
- Material identity visible without post/fog/darkness.
- Channel contract declared.
- Derivation path exists for every requested map.
- Three-pillar role is stated.
- Low/middle/high/ultra consequences are recorded.
- Handoff route owner is named.

Any hard rejection item sets `intake_status = REJECTED`. Any missing source/hash sets `intake_status = BLOCKED_SOURCE_MISSING`. Any source with only static docs and no Unity proof remains `STATIC_QA_ONLY`.

## Three-Pillar Acceptance Boundary

- Graphics pass requires actual candidate previews and later Unity captures. This document cannot pass graphics alone.
- Optimization pass requires packed channels, mips, compression, atlas/streaming plan, and later profiler/VRAM proof. This document cannot pass optimization alone.
- Gameplay pass requires a named route/decision/readability role and later in-game proof. This document cannot pass gameplay alone.

## Handoff

- Candidate intake owner fills manifest rows and rejects missing/illegal/low-quality files.
- PBR derivation owner creates albedo, normal, MRAO/ORM/PackedMask, emission, detail, decal, or LUT outputs according to route shader.
- Unity owner imports, checks color space/compression/mips, binds materials, captures proof, and records profiler/VRAM evidence where runtime/render paths changed.
