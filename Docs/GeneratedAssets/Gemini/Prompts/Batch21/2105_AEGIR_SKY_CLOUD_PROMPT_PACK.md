# 2105 Gemini Prompt Pack - Aegir Sky Cloud Sources

Agent ID: 2105
Batch: batch21_art_replacement_wave
Evidence class: STATIC_DOC
Unity/build/import: NOT RUN
Assets edited: NO

Use these prompts only for offline source generation. Save outputs under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` first. Do not import or bind into Unity until the Unity owner verifies shader slots, GUIDs, channel contracts, and import settings.

## Shared Direction

HECTON-8 surface sky is not the abyss. Above-water views, coastline, ocean skin, photic shallows, Aegir, moons, cloud layers, and horizon must be bright, readable, detailed, cinematic-realistic, and premium. Darkness belongs to depth, caves, interiors, storms, and temporary eclipse windows.

Aegir may be blue, violet, and methane-rich. Reject only when it is muddy, low-resolution, procedurally scribbled, pale/sticker-like, disconnected from horizon/ocean lighting, or visually below the surface floor.

## Global Negative Prompt

Reject: text, logo, UI, watermark, border, poster frame, stock-photo collage, hard object render, hard circular cutout, flat planet disc, pale transparent sticker, sine stripes, random noise bands, salt-and-pepper mask, muddy noir grade, black surface sky, crayon texture, low-res blur, cartoon planet, toy moon, generic sci-fi purple gradient, overexposed white wash, baked cast shadows in albedo, fixed terminator in albedo, horizon in seamless material source, hard cloud card edge, opaque fog wall.

## Prompt 1 - TX_B21_AegirCloudBands_AlbedoSource_20260604

Role: Aegir cloud-band color/albedo source.

```text
Create ONE premium source texture for Aegir, a huge methane-rich blue and blue-violet gas giant visible from an alien ocean horizon in a hard sci-fi underwater survival game.

Output target: 4096x2048 equirectangular RGB color source with seamless horizontal wrap on left/right edges, suitable for sRGB import. If the generator cannot reliably output equirectangular wrap, output ONE seamless square cloud-band source for later DCC conversion; label it as source-only, not direct Unity binding.

Visual requirements: layered atmospheric cloud belts, broad storm lanes, sheared ribbons, storm knots, soft polar/subpolar variation, deep cobalt, cyan, indigo, pale violet, muted grey-white cloud structures, strong readable band hierarchy at distance, cinematic-realistic scale, premium authored detail.

No hard terminator, no black space background, no rings, no moons, no stars, no text, no logo, no UI, no random scribbles, no sine-wave stripes, no pale transparent sticker look, no muddy noir grading, no visible horizontal seam.
```

Reject if bands disappear when downsampled to 2048, if the texture is one-note purple mud, or if it reads as procedural stripes instead of planetary atmosphere.

## Prompt 2 - TX_B21_AegirAtmosphereSoftness_MaskSource_20260604

Role: Aegir limb softness, haze thickness, and horizon integration mask.

```text
Create ONE clean linear grayscale atmosphere softness mask for a huge blue-violet methane gas giant.

Output target: 2048x2048 or 4096x4096 centered radial mask source, suitable for Linear import.

White means stronger visible atmosphere, rim softness, and upper-atmosphere thickness. Black means interior/no rim effect. The edge must be soft, slightly non-uniform, and influenced by subtle cloud-height variation. It must prevent a hard sticker edge when the planet sits behind a bright ocean horizon.

No beauty colors, no stars, no hard circular outline, no noisy grain, no text, no UI, no opaque halo, no sticker opacity cutout.
```

Reject if it creates a uniform halo, hard boundary, noisy edge, or fog blanket that hides Aegir detail.

## Prompt 3 - TX_B21_BrightSurfaceCloudDeck_ColorSource_20260604

Role: bright surface cloud panorama / color source.

```text
Create ONE bright surface-day cloud panorama source for HECTON-8, an alien ocean world.

Output target: 4096x2048 equirectangular RGB color panorama, suitable for sRGB import.

The sky must support daylight ocean navigation, Aegir readability, moon silhouettes, and horizon scale. Include layered white and pale grey cloud banks, thin high-altitude streaks, soft cyan sky scatter, humid coastal haze, distant cloud depth, and open blue sky windows. It must be cinematic-realistic, premium, bright, and readable.

No storm-night scene, no dark horror sky, no black/noir surface grade, no planet baked into this texture, no sun disc, no stock cloud collage, no visible seams, no flat white blobs, no text, no logo, no UI.
```

Reject if clouds only look acceptable in a dark storm, if the horizon is crushed, or if the source reads like unrelated stock photography.

## Prompt 4 - TX_B21_SurfaceCloudCoverage_LinearMaskSource_20260604

Role: cloud overlay alpha/coverage/erosion source.

```text
Create ONE clean linear grayscale cloud coverage and soft-edge alpha mask for a bright alien ocean surface sky.

Output target: 4096x2048 linear grayscale mask, with optional alpha-only export.

White means dense cloud, grey means thin veil, black means clear sky. Include large readable cloud masses, medium breakup, feathered erosion, soft horizon haze, and enough clear openings for a huge gas giant and moon silhouettes to remain readable.

No color, no lighting, no shadows, no hard card rectangles, no uniform fog, no salt-and-pepper noise, no Perlin mush, no text, no UI.
```

Reject if the mask removes all route visibility, turns into a grey wall, or has hard card/atlas edges.

## Prompt 5 - TX_B21_HorizonVeil_LinearMaskSource_20260604

Role: horizon haze/veil mask for sky, ocean, cloud, and Aegir relation.

```text
Create ONE horizontal horizon veil mask for a bright alien ocean surface scene with a huge gas giant partly softened by atmosphere.

Output target: 2048x512 linear grayscale mask/ramp, optional 4096x1024 high-source variant.

White means stronger atmospheric veil and humid sea haze; black means clear sky. The veil should be layered, uneven, soft, and physically believable. It must blend sky, low clouds, coastline silhouettes, ocean surface, and the lower part of a giant planet without cutting the planet texture directly.

No opaque fog wall, no hard horizontal cut, no storm darkness, no black vignette, no terrain/ocean hiding, no text, no UI.
```

Reject if it hides coastline/ocean route cues, makes Aegir disappear through a hard cut, or looks like a grey cover-up.

## Prompt 6 - TX_B21_MoonAlbedoSet_Source_20260604

Role: moon albedo set for visible moon silhouettes and phase readability.

```text
Create a set of realistic moon albedo source textures for visible moons in a hard sci-fi alien ocean sky.

Output target: at least FOUR distinct 2048x2048 or 4096x4096 square RGB albedo sources, suitable for sRGB import.

Each moon must have a distinct readable identity while belonging to the same planetary system: cold cratered ice-basalt, dark fractured basalt with pale salt scars, muted methane-frost with blue-violet tint, pale eroded regolith with broad impact basins. These are albedo/color sources only, not beauty renders. They must remain readable when small in a bright surface sky and must not look like reused terrain rock.

No baked crescent lighting, no black space background, no stars, no labels, no fixed terminator, no flat grey discs, no cartoon crater stickers, no text, no UI.
```

Reject if any moon reads as terrain rock reuse, toy sphere, flat debug disc, or fixed-lit beauty render.

## Prompt 7 - TX_B21_SurfaceSkyGradientOrCubemap_ColorSource_20260604

Role: bright surface sky gradient, cubemap, or LUT source support.

```text
Create ONE bright surface-day sky gradient/cubemap source for an alien ocean world.

Output target: route-approved 4096x2048 equirectangular color source or 1024x256 ramp preview plus optional 256x16 LUT derivation source.

The sky must support clean daylight, readable ocean color, cloud visibility, coastline horizon, and Aegir/moon silhouette. Use controlled blue-cyan zenith, readable blue-green horizon scatter, slight warm sun-facing haze, and subtle atmospheric depth around a huge gas giant. It must be cinematic-realistic and premium.

No generic purple sci-fi gradient, no dark noir sky, no white wash, no stars, no planet baked into this source, no clouds, no text, no logo, no UI.
```

Reject if it becomes one-note blue/purple, muddy, overexposed, or usable only as a beauty background with no horizon/readability role.

## Source QA Before Import

- Save generated files under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`.
- Record prompt ID, filename, SHA-256, source dimensions, and intended color space.
- Run 2x2 and manual 3x3 review for tileable/atlas sources.
- Downsample preview to compact max size before approval.
- Reject any source with text/logo/UI, baked lighting where not allowed, muddy noir grade, hard seams, low-res bands, random stripes, or route-hiding fog.

## Proof Boundary

STATIC VERIFIED: prompt requirements only.

PENDING VERIFICATION: generated images, source QA, import, binding, 360/crop captures, Frame Debugger, profiler, GC, memory, and VRAM.
