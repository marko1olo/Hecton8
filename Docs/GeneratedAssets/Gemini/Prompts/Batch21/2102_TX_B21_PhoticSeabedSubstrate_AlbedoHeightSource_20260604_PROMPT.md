# 2102 Prompt - TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604

Evidence class: STATIC_DOC
Generation state: PENDING VERIFICATION

Do not save generated output into `Assets/**`. Future generated candidates belong under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` until intake QA and Unity-owner import proof exist.

## Target

`TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604`

Purpose: seamless square source candidate for bright photic seabed substrate. The image is a source for later albedo cleanup and height/normal/mask derivation, not a finished PBR material.

## Primary Prompt

Create a seamless square orthographic PBR material source texture for a bright shallow underwater seabed substrate in the 0-30 meter photic zone. Material identity: fine sand and silt base, subtle current ripples, broken shell and calcite fragments, small reef-grit pieces, pale limestone chips, sparse algae and mineral staining in cavities, and soft sediment partially burying fragments. The surface must look believable, premium, and readable through clear shallow water, with Subnautica-level or better shallow seafloor material clarity.

Make it a flat material sample, not a scene. Use natural photic colors: warm light sand, pale shell/calcite, muted wet gray basalt grit, subtle green-brown biofilm stain, and clean shallow-water readability. Include height-like relief cues suitable for later normal and AO derivation: ripple ridges, fragment edges, cavity pockets, silt deposition, and small buried chips. Keep the scale physically plausible for a 1 to 2 meter square tile.

Strict constraints: seamless tileable square, orthographic top-down material sample, no horizon, no camera perspective, no object render, no plants or coral bodies, no fish, no sky, no UI, no text, no logo, no watermark, no border, no frame, no baked directional light, no cast shadows, no painted caustic shadows, no hard spotlight, no black noir grade, no muddy blue-gray sand, no flat Perlin noise, no fantasy candy reef carpet, no repeated obvious AI shells, no duplicated large fragments, no glossy plastic look, no crayon texture, no low-resolution mush.

Output should be albedo-dominant with usable height-like source information. Do not generate a tangent-space normal map. Do not generate colored PBR channels. Do not include fake roughness/AO/metallic labels or panels.

## Negative Prompt Tokens

perspective, landscape, horizon, object, coral colony, seaweed, kelp, fish, diver, shell pile object render, logo, text, watermark, UI, frame, border, cast shadow, dramatic lighting, directional sunlight shadow, painted caustics, black darkness, muddy grade, blue-gray generic sand, flat noise, Perlin-only, candy reef, neon coral, plastic gloss, repeated shells, cloned fragments, low resolution, blur, jpeg artifacts

## Optional Variant Prompts

Use only after the primary candidate fails with a specific QA reason or a future source owner needs narrower coverage.

### Variant A - ShellGritCalcite

Create a seamless square orthographic PBR material source texture for shallow photic shell-grit and calcite seabed substrate. Fine submerged sand and silt partially bury broken shell chips, calcite shards, pale limestone grit, and small rounded mineral fragments. The sample must be tileable, physically scaled to 1 meter, bright and readable, with no baked shadows and no repeated obvious shell shapes. Emphasize fragment edge relief and cavity pockets for later height, normal, roughness, and AO derivation. Exclude plants, coral bodies, object perspective, horizon, text, logos, borders, and painted caustic shadows.

### Variant B - ReefCalciteFloor

Create a seamless square orthographic PBR material source texture for photic reef-anchor floor substrate, not coral texture. Hard submerged basalt-limestone ground blends with shell grit, pale calcite flecks, algae stain in cavities, and sand collected around rock sockets. Bright shallow-water readability, material scale around 1-2 meters, no fantasy reef carpet, no coral branches, no plants, no object render, no baked lighting, no directional shadow, no text or logo. Include relief suitable for later height/normal/AO derivation.

### Variant C - ShallowShelfSiltLines

Create a seamless square orthographic PBR material source texture for underwater shelf sediment in shallow-to-upper-medium depth. Layered silt lines, fine sand deposits, chipped basalt/mineral flecks, sediment settled under ledge-like flow, and readable route-edge direction. It should be slightly more subdued than open photic sand but not dark, muddy, or black. Seamless tileable square, no horizon, no object render, no baked light, no painted caustics, no text, no logo, no flat noise.

### Variant D - BasaltSedimentTransition Candidate

Create a seamless square orthographic PBR material source texture for submerged seabed contact where wet basalt grit transitions into sand and silt. This is underwater substrate contact only, not shoreline wet/dry basalt. Include basalt chips, silt accumulation, mineral flecks, and soft sediment blending around rock edges. Bright readable shallow-water material, tileable square, no shoreline foam, no horizon, no baked lighting, no text, no logo. Use only if the future owner needs a seabed transition source.

## QA Required Later

- Save output under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`.
- Record prompt ID and SHA-256.
- Run static intake audit if tool supports the image format.
- Review 1x, 2x2, and manual 3x3 tile views.
- Reject if any hard constraint above is violated.
- Treat `PASS_STATIC` as source QA only, not Unity or runtime acceptance.
