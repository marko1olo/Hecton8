# Gemini Prompts - Sky, Aegir, Moons Source Textures

Date: 2026-06-04
Evidence class: STATIC_DOC
Unity/build/import: NOT RUN
Assets edited: NO

Use these prompts to generate offline source images for later route-owner review. Do not import or bind outputs without Unity-owner shader-slot discovery and proof.

## Shared Direction

Project: HECTON-8, NASA-punk / deep sea noir.

Surface sky is not the abyss. Above-water, coastline, ocean skin, photic shallows, Aegir, moons, and clouds must be bright, readable, detailed, and premium. Darkness belongs to depth, caves, interiors, storms, and temporary eclipse windows only.

Aegir visual direction: huge methane-rich gas giant, blue/purple acceptable, but only if the texture has strong cloud-band hierarchy, storm structure, scale, atmospheric softness, and route context. Aegir must not read as a pale transparent sticker, flat disc, muddy sine-stripe planet, or generic sci-fi gradient.

All outputs are source candidates. They must avoid baked lighting unless the role explicitly asks for a visual albedo reference. Source files need clean channels and no UI/text/logos.

## Global Negative Prompt

Reject: flat pale planet disc, transparent sticker look, hard circular cutout, procedural sine stripes, random noise bands, muddy blue/purple gradient, low-resolution blur, crayon texture, cartoon planet, fantasy aquarium sky, black/noir surface, overexposed white wash, stock-photo cloud collage, visible card edge, terrain texture moon, flat grey moon, baked light inside masks, text labels, symbols, UI, stars as noisy speckles, compression artifacts, unrelated nebula art, generic space scene.

## Prompt 1 - Aegir Albedo Cloud Bands

Role: `SKY20_AEGIR_ALBEDO_CLOUD_BANDS`

Prompt:

```text
Create a premium methane-rich gas giant source texture for a near-horizon planet named Aegir in a hard sci-fi underwater survival game. The planet is enormous, blue and blue-purple, with believable large-scale cloud bands, storm cells, sheared atmospheric ribbons, soft polar variation, and layered turbulent cloud depth. It must feel cinematic-realistic, not cartoon and not NASA stock copy. The texture should support a giant fixed map-position body seen behind an ocean horizon, so the mid-latitude banding must remain readable at distance. Use rich but controlled cyan, deep blue, indigo, pale violet, and muted grey-white cloud structures. Add complex storm knots and band breakup, but no random scribbles. No hard terminator, no labels, no stars, no black background.
```

Output target:

- 4096x2048 equirectangular source, sRGB.
- Also produce a centered 2048x2048 disc preview if the generator supports variants.
- No baked shadow or terminator. Lighting is shader-owned.

Reject if:

- Bands look like sine waves.
- Planet becomes pale transparent wash.
- Texture reads muddy or one-note purple.
- Details disappear when downsampled to 2048.

## Prompt 2 - Aegir Storm Density Mask

Role: `SKY20_AEGIR_STORM_DENSITY_MASK`

Prompt:

```text
Create a clean grayscale storm-density mask for the Aegir gas giant texture. The mask should mark storm knots, cloud-band density, eddy structures, and high-altitude turbulent ribbons. It must be useful as a linear shader mask, not a beauty render. White means stronger storm/cloud overlay, black means no overlay. Preserve large-scale band hierarchy and avoid salt-and-pepper noise. No lighting, no color, no shadows, no text, no stars.
```

Output target:

- 4096x2048 linear grayscale.
- Optional RGBA variant: R storm density, G high cloud veil, B small eddies, A broad band confidence.

Reject if:

- Looks like generic Perlin noise.
- Same density everywhere.
- Has baked light/shadow.
- Would make Aegir look dirty rather than structured.

## Prompt 3 - Aegir Limb Softness And Atmosphere Mask

Role: `SKY20_AEGIR_HAZE_LIMB_SOFTNESS`

Prompt:

```text
Create a linear radial limb-softness and atmosphere-thickness mask for a huge blue-purple methane gas giant. The mask must support a soft atmospheric rim, gentle edge falloff, and non-uniform upper-atmosphere thickness. It should prevent a hard sticker edge when the planet sits behind a bright ocean horizon. White marks thicker visible atmosphere and rim softness; black marks interior/no rim effect. Keep it clean, smooth, shader-friendly, and slightly non-uniform with subtle cloud-height influence. No beauty colors, no stars, no hard circular outline.
```

Output target:

- 2048x2048 or 4096x4096 linear grayscale/radial source.
- Centered disc-aligned source if route shader uses disc impostor.

Reject if:

- Hard circular boundary.
- Full uniform halo.
- Noisy or grainy edge.
- Looks like opacity for a sticker instead of atmospheric thickness.

## Prompt 4 - Atmospheric Horizon Occlusion Mask

Role: `SKY20_ATMOSPHERIC_OCCLUSION_MASK`

Prompt:

```text
Create a horizon atmospheric occlusion veil mask for a giant planet behind an ocean horizon. The mask should create believable loss of contrast through humid atmosphere, sea haze, and low cloud veil. It must not cut the planet texture directly. The horizon band should be soft, layered, and slightly uneven, preserving coastline and ocean readability. White means stronger horizon haze/occlusion; black means clear sky. The result is a shader mask, not a finished painting.
```

Output target:

- 2048x512 linear mask/ramp.
- Optional 4096x1024 high-source variant.

Reject if:

- Opaque fog wall.
- Hard horizontal cut.
- Hides coastline or water.
- Looks like storm/darkness cover.

## Prompt 5 - Surface Cloud Panorama

Role: `SKY20_SURFACE_CLOUD_PANORAMA_A`

Prompt:

```text
Create a bright surface-day cloud panorama source for HECTON-8. The sky should be beautiful and readable above an alien ocean, with layered white and pale grey clouds, subtle cyan sky scatter, distant cloud depth, and soft horizon structure. The style is cinematic realism, not fantasy, not cartoon, not a dark horror sky. Clouds should frame a huge gas giant without making it look pasted on. Preserve open blue sky areas, soft shadowed cloud underside, and believable atmospheric scale.
```

Output target:

- 4096x2048 equirectangular or route-approved panorama source, sRGB.
- No sun disc, no UI, no stars, no planet baked into this cloud texture.

Reject if:

- Stock cloud collage.
- Flat white blobs.
- Visible tiling/seams.
- Dark/noir surface sky.

## Prompt 6 - Surface Cloud Coverage Mask

Role: `SKY20_SURFACE_CLOUD_COVERAGE_MASK`

Prompt:

```text
Create a linear cloud coverage and soft-edge alpha mask for a bright surface cloud panorama. White means dense cloud, grey means thin veil, black means clear sky. The mask needs varied large cloud masses, feathered edges, distant horizon haze, and enough clear breaks for a huge gas giant to remain readable. It must be clean enough for shader alpha or coverage use. No color, no lighting, no hard card rectangles.
```

Output target:

- 4096x2048 linear grayscale.
- Optional alpha-only export.

Reject if:

- Uniform fog.
- Salt-and-pepper noise.
- Hard rectangular/card edges.
- Cloud mask removes all route visibility.

## Prompt 7 - Moon Albedo Set

Role: `SKY20_MOON_ALBEDO_SET`

Prompt:

```text
Create a set of realistic moon albedo source textures for visible moons in a hard sci-fi ocean planet sky. Each moon must have a distinct identity but still belong to the same system: cold cratered ice-basalt moon, dark fractured basalt moon with pale salt scars, muted methane-frost moon with violet-blue tint, and pale eroded regolith moon with impact basins. These are albedo/color sources only, not beauty renders. No baked directional lighting, no black space background, no labels. Surfaces must remain readable when small in the sky and must not look like reused terrain rock.
```

Output target:

- 2048x2048 or 4096x4096 square albedo per moon, sRGB.
- Four variants minimum.

Reject if:

- Flat grey discs.
- Terrain-rock copy look.
- Baked crescent lighting.
- Cartoon crater stickers.

## Prompt 8 - Moon Normal And Height Sources

Role: `SKY20_MOON_NORMAL_HEIGHT_SET`

Prompt:

```text
Create moon surface relief sources matching the moon albedo set: crater rims, fracture lines, eroded basins, ice ridges, basalt shelves, salt scar ridges, and small impact fields. The output should be suitable for deriving normal maps and optional height masks. No color beauty render, no baked shadows, no directional light, no noisy false detail. Relief must match believable crater geology and remain stable at distance.
```

Output target:

- 2048x2048 height source per moon, linear grayscale.
- Normal map can be generated downstream by route owner if needed.

Reject if:

- Baked lighting in height.
- Random noise pretending to be crater relief.
- Plastic/smooth toy surface.
- Relief contradicts albedo structure.

## Prompt 9 - Moon Phase Terminator Mask

Role: `SKY20_MOON_PHASE_TERMINATOR_MASK`

Prompt:

```text
Create clean linear terminator/phase ramp masks for moon rendering. The masks should support soft crescent, half, and gibbous presentation without changing moon surface identity. They are shader control ramps, not final lit images. Provide smooth physically plausible falloff with slight atmospheric/edge softness where appropriate. No hard binary black cut, no stars, no labels, no texture detail baked into the mask.
```

Output target:

- 1024x1024 or 2048x2048 linear grayscale ramp variants.

Reject if:

- Binary crescent cut.
- Dramatic baked lighting.
- Surface texture mixed into phase mask.
- Unmotivated glow.

## Prompt 10 - Bright Surface Day Gradient LUT

Role: `SKY20_DAY_GRADIENT_SKY_LUT`

Prompt:

```text
Create a bright surface-day sky gradient LUT/ramp for an alien ocean world. It should support a readable blue-cyan horizon, slightly warmer sun-facing haze, clean zenith blue, and subtle atmospheric depth around a giant gas planet. It must be premium cinematic realism, not generic purple-blue sci-fi and not a dark noir sky. Preserve coastline, ocean color, and cloud readability. No stars, no planet, no clouds, no UI.
```

Output target:

- 1024x256 sRGB ramp preview.
- Optional 256x16 linear LUT variant if route shader needs compact LUT.

Reject if:

- One-note blue.
- Purple gradient identity.
- Washed white sky.
- Muddy/dark horizon.

## Prompt 11 - Horizon Veil Mask

Role: `SKY20_HORIZON_VEIL_MASK`

Prompt:

```text
Create a horizon veil mask for a bright ocean surface scene. The veil should blend sky, coastline, ocean surface, low clouds, and the lower part of a giant gas planet through atmospheric haze. It must preserve route silhouettes and ocean readability. White means stronger veil; black means clear. Include subtle uneven low haze and distant moisture bands, but no storm darkness and no opaque fog wall.
```

Output target:

- 2048x512 linear grayscale.
- Optional 4096x1024 high-source variant.

Reject if:

- Hides terrain/ocean route.
- Makes Aegir disappear through hard cut.
- Reads as grey wall.
- Dark/noir cover.

## Prompt 12 - Planet-Shine Water Context Ramp

Role: `SKY20_PLANET_SHINE_WATER_CONTEXT_CUE`

Prompt:

```text
Create a subtle color-ramp source for optional Aegir atmospheric and ocean-context response. The ramp should express faint methane-blue and violet-blue planet-shine influence in air haze and water reflection without turning the ocean purple or changing route readability. It is a low-frequency presentation guide, not a full sky render. Keep values restrained, cinematic, and physically motivated.
```

Output target:

- 512x64 sRGB ramp preview plus optional linear ramp.

Reject if:

- Ocean becomes purple soup.
- Ramp is necessary for navigation.
- High saturation sci-fi glow.
- Dirty/muddy color shift.

## Handoff Notes

- Save generated outputs outside `Assets` until the Unity owner approves shader slots and import settings.
- Suggested staging folder after generation review: `Docs/GeneratedAssets/Gemini/Outputs/Batch20/SkyAegirMoons/`.
- Unity owner must decide final import names, compression, mip policy, color space, and material binding.
- Runtime acceptance requires the proof shots listed in `Docs/Reports/Batch20/SKY_AEGIR_MOONS_SOURCE_ROLE_PACKAGE_20260604.md`.

## No Unity Confirmation

No Unity Editor action, asset import, build, profiler capture, or Frame Debugger capture was performed for this prompt package.
