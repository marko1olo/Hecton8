# HECTON-8 Underwater Texture Prompt Pack

Status: `PENDING VERIFICATION`
Date: `2026-04-15`
Goal: regenerate weak underwater support textures with prompts that are reusable in production and safe even when alpha is absent.

## General Rules For The Image Model

- Output square textures unless atlas layout is requested explicitly.
- No baked camera perspective.
- No scene framing.
- No lighting story baked into the texture unless explicitly requested.
- No UI, no labels, no borders, no mockup presentation.
- Prefer neutral grayscale for masks and breakup maps.
- Prefer clean RGB normal data for normals.
- If alpha is unavailable, keep the useful mask information in RGB luminance.

## 1. Bubble Vent Atlas

### Primary Prompt

`Underwater hydrothermal vent bubble sprite sheet, 4x4 atlas, every cell centered and isolated, generous empty padding between cells, transparent-style separation, realistic bubble clusters rising upward, varied bubble sizes from micro bubbles to medium bubbles, mixed plume density from sparse to heavy, deep sea cold lighting, clean silhouettes, soft edge falloff, particle-system ready, no hard rectangles, no background contamination, no scene composition, no seabed, no pipes, no rocks, production-ready VFX atlas`

### Stronger Cinematic Variant

`Deep sea methane-like vent bubble atlas for a NASA-punk underwater game, 4x4 sprite sheet, each frame unique but consistent, clustered spherical bubbles with believable pressure breakup, soft underwater scattering, subtle bluish deep-ocean tone, isolated cells with clear empty margins, ideal for particle flipbook use, readable at small sizes, no fog blocks, no square artifacts, no environment details, no background plate`

### Negative Prompt

`rectangular haze, boxed fog, visible atlas grid lines, environment background, seabed, machinery, text, labels, UI, frame decorations, photographic scene, muddy silhouettes, merged cells, hard square edges, low contrast mush, collage look`

### Import / Usage Notes

- Best target: VFX sprite atlas for vent columns.
- If alpha is missing, extract mask from luminance and repack later.
- Each cell needs enough empty space to avoid atlas bleed in particles.

## 2. Mineral Seep Mask

### Primary Prompt

`Tileable grayscale mineral seep mask for submerged sci-fi structures, vertical wet streak hierarchy, calcified residue, porous corrosion islands, damp mineral flow, salt and oxide breakup, reusable material mask, no baked object identity, no perspective, no panel layout, no lighting, no storytelling scene, high contrast but still natural, production-ready, seamless texture`

### Cleaner Material-Authoring Variant

`Seamless monochrome material mask for underwater mineral seep and wet calcification, large medium and fine breakup layers, gravity-driven drip streaks, branching runoff traces, residue pools, porous edge erosion, designed for shader masking, no baked shadows, no highlights, no metal panel shapes, no scene context, no decals, pure reusable grayscale texture`

### Negative Prompt

`visible panels, sci-fi wall composition, perspective depth, object render, dramatic lighting, vignette, horizon, camera angle, embossed shapes, repeating stamp pattern, hard rectangular borders, text, symbols`

### Import / Usage Notes

- Best target: sheen/wetness mask, not direct albedo.
- Keep it grayscale-first so RGB luminance is enough.
- If a version comes back with too much story baked in, reject it and rerun.

## 3. Soft Plume Noise

### Primary Prompt

`Tileable grayscale underwater particulate plume breakup texture, soft suspended sediment wisps, cloudy density pockets, broad soft structures plus micro grain, no hard blobs, no directional scene composition, reusable VFX modulation map, monochrome, smooth but detailed, production-safe, seamless texture`

### Denser Leak Variant

`Seamless monochrome underwater silt plume noise texture for leak and sediment VFX, layered soft turbulence, diffuse cloudy breakup, drifting particulate feel, subtle internal contrast, no isolated sharp dots, no obvious directional streaking, ideal for dissolve, opacity, and distortion modulation`

### Negative Prompt

`smoke photograph, visible lighting beam, framed composition, big black voids, ink blotches, sharp circles, obvious tiling cross, strong diagonal composition, scene background, text`

### Import / Usage Notes

- Good for opacity breakup, distortion modulation, and plume edge softening.
- Keep it grayscale. No alpha dependency required.
- If the model keeps over-detailing it, explicitly ask for softer cloud logic and fewer hard blobs.

## 4. Optional Wet Streak Breakup

### Primary Prompt

`Grayscale wet streak breakup mask for hard-surface underwater sci-fi modules, thin rivulets, medium branching drips, occasional thicker runoff channels, clean black background, reusable material mask, no perspective, no lighting, no frame narrative, high readability, seamless texture`

### Negative Prompt

`full wall render, panel illustration, cinematic lighting, glossy beauty shot, text, icons, labels, camera angle, floor, horizon, photographic scene`

### Import / Usage Notes

- Best for corridors, support modules, and leak-driven wetness layering.
- Can be multiplied with mineral seep mask for less repetitive results.

## 5. Optional Visor Secondary Mask

Use only if the current visor pair later proves insufficient.

### Prompt

`Grayscale secondary visor droplet breakup mask for curved helmet glass, sparse large droplets mixed with faint micro-beads, subtle runoff seeds, clean black background, no perspective, no helmet frame, no lighting, reusable mask for refraction and highlights, production-ready`

### Negative Prompt

`helmet render, face, reflections of environment, cinematic scene, UI overlay, text, bright white background, water splash photograph`

## Acceptance Criteria

- Bubble atlas: every frame isolated, no rectangular contamination, readable at particle scale.
- Mineral seep: reusable mask, not a painted wall illustration.
- Soft plume: seamless modulation texture, not a framed effect shot.
- Any mask texture: useful information must remain readable from RGB luminance alone.

## Rejection Triggers

- Any texture that looks like a scene render instead of a reusable map.
- Any texture that depends on alpha for all useful information.
- Any atlas with cells touching or bleeding.
- Any mask with obvious perspective or baked object storytelling.
