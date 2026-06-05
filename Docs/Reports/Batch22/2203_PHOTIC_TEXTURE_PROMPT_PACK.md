# 2203 Photic Texture Prompt Pack

Evidence class: STATIC VERIFIED. Unity was not run. No `Assets/**` files were edited.

## Authority Mandates

- Surface, coastline, ocean skin, wet rock, and photic shallows must stay bright, readable, beautiful, and Subnautica-level or better on every hardware lane.
- Gemini output is source art only until manifest, static audit, 2x2 preview, channel-role plan, derivation proof, import settings, and Unity material preview exist.
- Albedo prompts must request base color/source only: no baked shadows, no directional highlights, no scenic perspective, no labels, no borders, no fake PBR stack.
- One generated image must not be reused as albedo, normal, roughness, and AO. Height, normal, roughness, AO, MRAO, wetness, and caustic masks need deliberate derivation.
- `GlobalQualityWeight` may scale texture size, decal density, detail-map strength, atlas page count, and residency cadence. It must not change material truth or gameplay route identity.
- Compact lane texture work must preserve material identity through compression, mips, silhouettes, masks, and composition. Blurry mud is rejected.
- Async upload and texture residency remain budgeted: compact class has 1800 MB hard VRAM ceiling and 900 MB texture budget; texture upload buffers are tiered, not free.

## Current Intake Summary

| Asset | Current state | Evidence | Use allowed |
|---|---|---|---|
| `TX_H8_WetBasaltShoreline_Albedo_1428` | `SOURCE_ONLY / REJECT` | WetBasalt1428 audit rejects LR/TB edge mismatch; large teal vein repeats. | Reference, small masked decal study only. |
| `TX_H8_WetBasaltShoreline_Albedo_1429` | `SOURCE_ONLY / REJECT` | WetBasalt1429 audit rejects edge/band mismatch; large rock forms repeat. | Reference for correction prompt only. |
| `TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean` | `REJECT` | Strict band audit catches worse band mismatch, black/white clipping, channel saturation. | Do not derive final PBR from it. |
| `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742` | `SOURCE_REFERENCE_ONLY / REJECT` | Batch21 audit: top-bottom band mismatch, edge warnings, diagonal dune/ripple repetition. | Reference for bright seabed color and shell/calcite direction. |
| `TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642` | `SOURCE_REFERENCE_ONLY / REJECT` | Batch21 audit: top-bottom band mismatch, edge warnings, possible baked/crushed albedo range, repeated shell/stone clusters. | Reference for corrected sand/shell prompt or manual cleanup plan. |

No existing inspected image is `CANDIDATE` or `READY_FOR_DERIVATION`. All inspected sources remain blocked from direct Unity import or production material binding.

## Target Taxonomy

### Wet Basalt Shoreline

- Appears on exposed shoreline shelves, wave-wet cliffs, waterline ledges, shallow volcanic outcrops.
- Scale: 2 m per tile for terrain/triplanar base; 0.5-1 m detail decals for near waterline.
- Requirements: albedo cleanup source, height source for cracks/pores, BC5 normal derived offline, MRAO with non-metallic basalt, roughness variation, optional wetness/contact mask.
- Repetition tolerance: no hero crack or teal vein visible in 2x2/3x3.
- Forbidden: black abyss grade, baked shine, chrome wetness, smooth blob rock, giant diagonal veins.

### Basalt With Cyan Mineral Veins

- Appears as rare shoreline and shallow cliff accent, not the entire coastline.
- Scale: 1.5-2 m per tile; cyan vein decals 0.25-0.75 m.
- Requirements: base basalt albedo plus separate vein mask; height for chipped vein edges; roughness differentiates wet basalt, pale salt, and mineral inclusions.
- Repetition tolerance: vein network must break under macro masks; no single vein shape can repeat naked.
- Forbidden: neon lines, sci-fi circuit look, full-rock metallic, glowing dirt.

### Sand/Shell Substrate

- Appears in 0-100 m bright seabed, shallow shelves, sand pockets between basalt/coral.
- Scale: 1 m per tile; shell fragments mostly 1-7 cm; no large hero shells.
- Requirements: bright albedo source, height source for shell/silt relief, roughness/AO derived offline, optional shell/calcite mask.
- Repetition tolerance: stochastic small/medium grain, no diagonal dune bands in 2x2.
- Forbidden: beige mud, beach-photo perspective, baked shell shadows, obvious repeated shell cluster.

### Shoreline Foam/Salt Lace

- Appears at waterline contacts, wet basalt edge decals, shallow wave contact strips.
- Scale: 0.5-2 m projected masks; sparse blendable source.
- Requirements: mask source, not scenic wave photo; separable channels for foam strand, wet-edge breakup, bubbles/sediment, confidence/wetness.
- Repetition tolerance: broken foam cells must survive tiling without a white stripe.
- Forbidden: opaque snow band, storm-only dirty foam, perspective wave photo, foam hiding weak rock art.

### Caustic Decal/Mask

- Appears in bright shallows, justified lamps/glass/pools, wet terrain and local water-column masks.
- Scale: 1-4 m projected decals; 0.25-1 m fine lookup.
- Requirements: grayscale or RGBA mask source; no baked terrain/object shadows; derived as presentation/fake-first water support.
- Repetition tolerance: loopable lace without obvious grid or repeated bright knots.
- Forbidden: global abyss caustics without light reason, bloom glare, underwater beauty render, fish/diver/scene content.

### Shallow Algae/Coral Tint Breakup

- Appears as color/material breakup on coral, algae stains, rock biofilm, shallow substrate overlays.
- Scale: 0.5-1.5 m per tile; small spots 1-10 cm; macro masks decide placement.
- Requirements: albedo/tint source plus mask source; optional height for calcified/soft tissue transition; roughness differentiates matte algae, wet biofilm, calcite.
- Repetition tolerance: nonuniform but not noisy; no repeated coral cup hero shape.
- Forbidden: candy reef gradients, random neon, black abyss coral, flat aquarium color wash.

## Primary Gemini Prompts

Use English. Save downloads only under `Docs/GeneratedAssets/Gemini/Outputs/Batch22/`. Run `Tools/GeminiTextureIntakeAudit.py` before any derivation. Each prompt targets one square texture/source.

### 01 Wet Basalt Shoreline Albedo

```text
Create ONE seamless square tileable PBR albedo texture source for a premium Unity URP ocean survival game.

Subject: bright photic-zone wet basalt shoreline rock on an alien ocean moon, black-gray volcanic basalt, salt-water erosion, chipped fracture planes, small pores, stratified cracks, pale salt residue in crevices, tiny sediment caught in cracks, and very subtle teal mineral staining.

This is base color only for later PBR derivation. Do not include normal-map colors, roughness data, AO dirt, emission, baked shadows, baked highlights, or a beauty-render lighting pass.

Use orthographic top-down material view. Use even neutral bright photic daylight suitable for albedo. No perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no border, no watermark.

Make the pattern reusable in a 3x3 tile preview: no large hero cracks, no repeated rock plates, no giant teal vein, no diagonal seam bands. The surface must stay readable and beautiful for shoreline and 0-100 m shallow water, not dark noir.

Output ONE square 4096x4096 texture only.
```

### 02 Basalt With Cyan Mineral Veins Albedo/Mask Source

```text
Create ONE seamless square tileable PBR albedo and mask source for alien shoreline basalt with sparse cyan mineral veins.

Subject: black-gray wet basalt host rock, chipped volcanic fracture, pale salt residue, fine pores, and rare cyan-blue mineral veins embedded in cracks. Mineral veins must be sparse, natural, chipped, and partly occluded by sediment, not glowing sci-fi lines.

This is a source image for later albedo cleanup and vein mask extraction. No baked lighting, no cast shadows, no directional highlights, no normal-map colors, no metallic-map fantasy, no emission.

Use orthographic top-down material view with even bright diffuse photic daylight. No perspective, no horizon, no object render, no text, no logo, no UI, no frame.

Edges must tile invisibly left/right and top/bottom. No single vein shape may repeat obviously in 2x2 or 3x3 tiling. Keep the vein network broken, varied, and usable as a rare shoreline accent.

Output ONE square 4096x4096 texture only.
```

### 03 Sand/Shell Substrate Albedo Source

```text
Create ONE seamless square tileable PBR albedo texture source for a bright photic shallow seabed substrate.

Subject: pale gray volcanic sand, small black basalt chips, shell fragments, calcite grains, tiny reef limestone pieces, soft silt pockets, small algae specks, and subtle teal mineral traces. Shell fragments should mostly be 1-7 cm scale, with no large hero shell or repeated stone cluster.

This is base color only for later height, normal, roughness, and AO derivation. Do not bake shadows or directional highlights into the albedo. Do not create a beach photograph or perspective scene.

Use orthographic top-down material view with even bright diffuse daylight. No perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no border.

The material must stay beautiful and readable through clear shallow water. Avoid beige mud, brown beach sand, dark abyss sediment, diagonal dune bands, and obvious 2x2 repetition.

Output ONE square 4096x4096 texture only.
```

### 04 Shell/Sand Height Source

```text
Create ONE seamless square grayscale height source derived for a bright photic shell-sand seabed material.

Subject relief: raised small shell fragments and calcite grains, medium volcanic sand, tiny basalt chips, low soft silt pockets, shallow cracks, and subtle algae film edges. White means raised shell/grain surfaces; black means recessed silt pockets and cracks.

This is not an albedo texture and not a rendered scene. No color, no lighting, no shadows, no perspective, no labels, no text, no logo, no UI, no border.

Use orthographic top-down material view. Edges must tile invisibly left/right and top/bottom. Avoid large repeated shells, diagonal bands, and high-contrast baked shadow shapes.

Output ONE square 4096x4096 grayscale texture only.
```

### 05 Shore Foam/Salt Contact RGBA Mask

```text
Create ONE seamless square tileable RGBA shoreline foam and salt contact mask source for wet basalt in a bright alien ocean shoreline.

Subject: thin white sea-foam lace, translucent micro-bubbles, tide-sheared strands, broken foam cells, wet edge breakup, salt residue, pale sediment contact marks, and natural foam interruption by rough basalt cracks.

This is source data for a Unity URP material, not a scenic wave photo and not a water simulation. No baked lighting, no cast shadows, no directional highlights.

Use orthographic top-down mask/source view. No perspective, no horizon, no object silhouette, no text, no logo, no UI, no border.

Channel intent: Red = long foam strand and contact foam strength. Green = cross-flow wet edge breakup. Blue = small bubbles, lace, and sediment interruption. Alpha = clean confidence/wetness mask.

Foam must be sparse and broken enough to blend. No solid opaque white strip, no repeated bubble stamps, no storm mud. Output ONE square 4096x4096 texture only.
```

### 06 Caustic Decal Mask Source

```text
Create ONE seamless square tileable grayscale caustic decal mask source for bright shallow underwater projection in a premium Unity URP ocean survival game.

Subject: clean photic daylight caustic lace, refracted curved streaks, broken nonuniform bands, subtle small silt sparkle gaps, and soft water-column micro variation.

This is a mask/source panel for shader projection and decal derivation. It is not a scenic underwater render. No terrain, no fish, no diver, no objects, no bloom glare, no baked shadows, no perspective, no horizon, no text, no logo, no UI, no border.

Use orthographic top-down source-panel view. Edges must tile invisibly in 2x2 and 3x3 previews. Keep the contrast controlled: readable caustic structure, not crushed white lines or black void.

Output ONE square 2048x2048 grayscale texture only.
```

### 07 Caustic RGBA Lookup Source

```text
Create ONE seamless square tileable RGBA lookup source for fake-first shallow-water caustics and suspended particle breakup.

Subject: layered caustic lace, soft refracted streaks, tiny suspended bright specks, broken water-column bands, and low-frequency mask variation suitable for shader animation.

This is source data, not a beauty render. No perspective, no horizon, no fish, no diver, no terrain, no labels, no text, no logo, no UI, no border, no baked lighting, no bloom.

Channel intent: Red = primary caustic lace, Green = secondary offset lace, Blue = suspended particle sparkle gaps, Alpha = soft projection confidence mask.

Edges must tile cleanly. Avoid repeated bright knots, grid patterns, harsh stripes, and generic noise.

Output ONE square 2048x2048 texture only.
```

### 08 Shallow Algae/Biofilm Tint Breakup

```text
Create ONE seamless square tileable albedo/tint source for shallow photic algae and biofilm color breakup on wet basalt and shell-sand substrate.

Subject: thin teal-green algae film, muted cyan biofilm stains, pale calcite dust, small organic speckles, soft edge breakup, and sediment interruption. The pattern should be natural and patchy, usable as an overlay mask/tint source.

This is a source texture for later mask extraction. No baked light, no cast shadows, no directional highlights, no normal-map colors, no full coral object render.

Use orthographic top-down material view with even bright diffuse daylight. No perspective, no horizon, no labels, no text, no logo, no UI, no border.

Keep the color restrained and believable. No neon slime, no candy reef gradient, no random glowing dots, no muddy dark abyss staining, no obvious 2x2 repetition.

Output ONE square 4096x4096 texture only.
```

### 09 Shallow Coral/Calcite Surface Source

```text
Create ONE seamless square tileable PBR albedo plus height-like source for shallow alien coral and calcified reef surface.

Subject: porous calcified coral cups, chipped pearl and pale cyan mineral edges, muted coral-violet tissue stains, small sediment in pores, broken tip wear, algae traces, and cavity cues for later normal/AO derivation.

This is a material source, not a whole coral branch render and not an alpha-card texture. No baked shadows, no directional highlights, no perspective, no horizon, no object silhouette, no text, no logo, no UI, no border.

Use orthographic top-down material view with even bright diffuse photic daylight. The material should be colorful and alien but physically believable.

Avoid candy reef art, smooth plastic tube coral, random neon, black abyss coral, repeated coral-cup hero shapes, and generic wallpaper.

Output ONE square 4096x4096 texture only.
```

### 10 Wet Basalt Roughness/Wetness Source

```text
Create ONE seamless square grayscale roughness and wetness source for wet shoreline basalt.

Subject response: rough dry basalt pores and salt residue should be lighter, wet polished fracture planes and waterline cavities should be darker, mineral-stained cracks should have varied roughness, and sediment-filled pits should remain mostly rough.

This is a grayscale source for later roughness/wetness packing. No color albedo, no scenic lighting, no shadows, no perspective, no horizon, no text, no logo, no UI, no border.

Use orthographic top-down material view. Edges must tile invisibly. Avoid flat constant gray, repeated hero cracks, black/white clipping, and baked highlight shapes.

Output ONE square 4096x4096 grayscale texture only.
```

### 11 Redo: Seam And Repetition Fix

```text
Revise the attached/generated texture into a TRUE production seamless square tile.

Keep the same material identity and scale, but fix tileability: left/right and top/bottom edges must match invisibly in a 2x2 and 3x3 tiled preview.

Remove large recognizable repeated hero shapes, diagonal bands, repeated shell/stone clusters, repeated veins, and any obvious border treatment. Make the pattern more isotropic and stochastic while preserving believable material structure.

Use even diffuse lighting suitable for source texture work. No baked shadows, no directional highlights, no perspective, no horizon, no labels, no text, no logo, no UI, no border.

Output ONE square texture only.
```

### 12 Redo: Albedo Cleanup

```text
Revise the attached/generated texture as a clean PBR albedo source.

Keep the material identity, but remove baked shadows, black crushed crevices, white clipped highlights, strong lighting gradients, glossy render shine, and camera/photo artifacts. Preserve base color variation and material readability under neutral URP lighting.

Do not add normal-map colors, AO dirt, roughness data, emission, text, labels, logos, UI, perspective, horizon, or object silhouettes.

Edges must remain seamless and tileable. No repeated hero shapes in 2x2 or 3x3 preview.

Output ONE square albedo texture only.
```

### Optional Image-To-Image Follow-Up: Height From Accepted Source

```text
Create ONE seamless square grayscale PBR height source derived from the attached accepted tileable material texture.

Preserve the exact tile edges, material scale, and major features. White = raised surface; black = recessed cracks, pores, silt pockets, and cavities.

No color, no lighting, no shadows, no perspective, no labels, no text, no logo, no UI, no border. Do not invent new large shapes.

Output ONE square grayscale height texture only.
```

### Optional Image-To-Image Follow-Up: Roughness From Accepted Source

```text
Create ONE seamless square grayscale PBR roughness source derived from the attached accepted tileable material texture.

Preserve exact tile edges and material scale. White = rough/dry/matte; black = smooth/wet/glossy. Match material truth: salt and sediment rough, wet basalt fracture planes smoother, algae/biofilm variable, shells/calcite mostly matte with small worn highlights.

No color, no lighting, no shadows, no perspective, no labels, no text, no logo, no UI, no border.

Output ONE square grayscale roughness texture only.
```

### Optional Image-To-Image Follow-Up: Normal From Accepted Height

```text
Create ONE seamless square OpenGL tangent-space normal map derived from the attached accepted height/source texture.

Preserve exact tile edges and material scale. Encode relief only: pores, cracks, shells, grains, chipped edges, foam cells, or coral cups according to the attached source. No albedo color, no lighting, no shadows, no perspective, no labels, no text, no logo, no UI, no border.

Output ONE square normal map texture suitable for Unity import after manual QA.
```

## Generation Budget Plan

Assumption: 7 accounts x 3-4 generations/day = 21-28 generations/day. Do not run browser automation for this task.

Priority order:

1. Wet basalt shoreline albedo: 3 first-pass attempts, then stop unless a candidate reaches `REVIEW` or better.
2. Sand/shell substrate albedo: 3 first-pass attempts, then one correction if seam failure is narrow.
3. Shore foam/salt contact RGBA mask: 2 first-pass attempts.
4. Caustic decal grayscale/RGBA mask: 2 grayscale, 2 RGBA.
5. Algae/biofilm tint breakup: 2 first-pass attempts.
6. Basalt cyan vein source: 2 attempts after base basalt improves.
7. Shallow coral/calcite: 2 attempts after substrate and tint direction are stable.
8. Optional image-to-image height/roughness/normal only after a source is `PASS_STATIC` plus visual 2x2 review.

Stop conditions:

- Stop a target for the day after two failures with the same hard issue unless a correction prompt names that issue directly.
- Stop any candidate with text/logo/border/perspective/object render; do not spend derivation budget.
- Stop direct PBR derivation if albedo is `REJECT`.
- Stop if 2x2 preview shows a hero motif visible at route scale even when audit metrics pass.

## Download Naming Convention

Use:

`TX_B22_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS].[ext]`

Examples:

- `TX_B22_WetBasaltShoreline_AlbedoSource_20260604_Gemini_221500.png`
- `TX_B22_ShoreFoamSaltContact_RGBAMaskSource_20260604_Gemini_222000.png`
- `TX_B22_CausticDecal_GrayscaleMaskSource_20260604_Gemini_222500.png`

Manifest must sit beside the source:

`TX_B22_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS]_MANIFEST.md`

Manifest minimum:

- prompt text or prompt ID;
- target taxonomy;
- source role;
- intended meters per tile;
- SHA-256;
- audit command;
- audit CSV/Markdown path;
- preview path;
- status: `SOURCE_REFERENCE_ONLY`, `STATIC_REJECTED`, `CANDIDATE_REVIEW`, or `READY_FOR_DERIVATION`;
- explicit note that Unity import is blocked until source audit passes.

## Derivation Plan For Existing Sand/Shell Candidate

Asset: `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`

Current state: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`.

If art direction still likes it:

1. Keep it in Docs as reference. Do not import to Unity.
2. Use it as image reference for prompt 03 plus redo prompts 11/12.
3. If manual cleanup is approved later, crop/retile/clone to remove top-bottom band mismatch and repeated shell/stone clusters.
4. Run `Tools/GeminiTextureIntakeAudit.py` on the cleaned candidate.
5. Only after static pass plus visual 2x2 review, derive height from shell/grain relief.
6. Derive normal from accepted height, not directly from the rejected albedo.
7. Derive roughness/AO/mask from cleaned source with shell, silt, wetness, and algae logic separated.
8. Build MRAO later: R metallic = black/non-metal, G roughness, B AO, A wetness/shell/algae family mask as shader contract decides.
9. Unity material/TerrainLayer preview is a later owner slot after source audit and channel manifest.

## Hardware Consequences

- Compact: standard world materials target 1024 max imported size where possible, caustic/water masks 256-512, decals 256-512, compressed high quality, mips on, atlas reuse, no uncompressed runtime texture. Beauty is preserved through source quality, masks, and composition, not resolution alone.
- Middle: key photic world materials 2048, stronger local decals and roughness variation, caustic/water masks around 1024 when budget allows.
- High: 2048 hero surfaces, richer normals, stronger wetness/foam decal layering, longer residency when profiler allows.
- Ultra: 4096 source/bake lane for hero-only surfaces and source archives; shipped runtime still obeys compression, streaming, and VRAM guard thresholds.

## Proof Path

1. Save Gemini download under `Docs/GeneratedAssets/Gemini/Outputs/Batch22/`.
2. Write sidecar manifest with source role and prompt.
3. Run `Tools/GeminiTextureIntakeAudit.py` to generate CSV, Markdown, 2x2 preview, and contact sheet.
4. Perform human 2x2/3x3 visual tile inspection.
5. Mark status:
   - `STATIC_REJECTED`: hard seam/band/clipping/perspective/text/hero motif failure.
   - `CANDIDATE_REVIEW`: script passes/reviews but human visual check remains.
   - `READY_FOR_DERIVATION`: source passes script, visual tile review, and channel-role sanity.
6. Derive height/normal/roughness/AO/MRAO only from `READY_FOR_DERIVATION`.
7. Unity material preview only after source audit and derivation manifest exist.

## Next Five Gemini Prompts

1. Prompt 01 Wet Basalt Shoreline Albedo.
2. Prompt 03 Sand/Shell Substrate Albedo Source.
3. Prompt 05 Shore Foam/Salt Contact RGBA Mask.
4. Prompt 06 Caustic Decal Mask Source.
5. Prompt 08 Shallow Algae/Biofilm Tint Breakup.
