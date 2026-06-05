# 2022 Priority Texture Prompts - Gemini Budget Queue

Agent ID: 2022
Date: 2026-06-04
Evidence class: STATIC_DOC

No images were generated. No browser was opened. No Unity import or material assignment was performed.

Save generated candidates under:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/`

Never save browser downloads directly into `Assets/**`. Do not record browser account names or emails.

## Budget Order

Spend Gemini budget first on prompts 1-5 only:

1. Wet basalt shoreline albedo
2. Shore foam/salt contact mask
3. Photic seabed substrate
4. Shallow branching coral material
5. Aegir cloud bands

Do not retry before QA. No more than 1 retry per texture unless it is prompt 1-3 and the failure reason is clear, narrow, and prompt-fixable.

## Shared Negative Prompt

Append this to every prompt unless the specific prompt already contains a stricter negative list:

```text
Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, scene camera, object render, labels, readable text, numbers, logo, UI, watermark, frame, copied game art, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black noir darkness used to hide weak detail, muddy grade, cartoon, painterly, low-poly, generic noise, smooth blobs, random neon, opaque cover-up.
```

## 1 - Wet Basalt Shoreline Albedo

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604.png`

Prompt:

```text
Create ONE seamless square tileable PBR albedo texture for a premium Unity URP ocean survival game.

Subject: natural wet basalt shoreline on an alien ocean moon, volcanic black-gray basalt with salt-water erosion, chipped fracture planes, small pores, stratified cracks, subtle teal mineral staining, pale salt residue in crevices, tiny sediment caught in cracks, and believable wet shoreline material breakup.

This is base color only. It is not a full PBR material and it must not contain normal-map colors, roughness data, AO dirt, emission, or a beauty-render lighting pass.

Use orthographic top-down material view. Use even neutral bright photic daylight. No baked light. No baked highlights. No cast shadows. No lighting gradient. No perspective. No horizon. No object silhouette. No text. No logo. No UI.

Make the pattern reusable: no large hero cracks, no repeated rock plates, no giant teal vein that becomes obvious in a 3x3 tile preview. The material must remain bright/readable enough for surface and photic shoreline use, not dark noir.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604
```

## 2 - Shore Foam Salt Contact Mask

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShoreFoamSaltContact_Mask_20260604.png`

Prompt:

```text
Create ONE seamless square tileable RGBA foam and shoreline contact mask source for natural wet basalt at a bright alien ocean shoreline.

Subject: clean photic-shallow shoreline contact, thin white sea foam lace, translucent micro bubbles, tide-sheared strands, broken foam cells, wet edge breakup, salt and sediment contact residue, and natural foam interaction with rough wet basalt cracks.

This is source data for a Unity URP material. It is not a scenic wave photograph and not a finished water simulation.

Use orthographic top-down material mask view. No perspective. No horizon. No object silhouette. No text. No logo. No UI. No baked lighting. No cast shadows. No directional highlights.

Suggested RGBA channel intent:
Red = long foam strand and contact foam strength.
Green = cross-flow wet edge breakup.
Blue = foam lace, small bubbles, and sediment interruption.
Alpha = optional confidence mask only; keep it clean and separable.

Foam must be sparse and broken enough to blend. It must not become a solid opaque white strip.

Output one 2048x2048 or 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShoreFoamSaltContact_Mask_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ShoreFoamSaltContact_Mask_20260604
```

## 3 - Photic Seabed Substrate

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable PBR material source for a bright photic shallow seabed substrate in a premium Unity URP ocean survival game.

Subject: pale gray-black basalt chips, volcanic sand, shell fragments, silt seams, small reef limestone pieces, soft sediment pockets, subtle teal mineral traces, and small scale witnesses that keep the material readable through clear shallow water.

This is an albedo plus height-like source for later normal, roughness, and AO derivation. It is not a final PBR stack. Do not bake lighting or directional shadow into the image.

Use orthographic top-down material view. Even bright diffuse daylight. No perspective. No horizon. No object silhouette. No text. No logo. No UI. No scenic beach photo.

The result must be beautiful and readable for 0-100 m photic water. It must not be beige mud, brown sand, dark abyss material, or generic blue fog-dependent terrain.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604
```

## 4 - Shallow Branching Coral Material

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable PBR material source for shallow alien branching coral in a bright photic reef.

Subject: calcified coral surface with porous cups, growth rings, chipped pale cyan and pearl mineral edges, muted coral-violet tissue stains, small sediment in pores, broken tip wear, and cavity cues for branch intersections.

This is an albedo plus height-like source for geometry-backed coral material derivation. It is not a whole coral object render, not an alpha-card texture, and not a final PBR stack.

Use orthographic top-down material view. Even bright diffuse daylight. No perspective. No horizon. No whole branch silhouette. No labels. No text. No logo. No UI. No baked shadows. No directional highlights.

The material should be colorful and alien but physically believable. It must not look like candy reef art, smooth plastic tube coral, random neon, or dark abyss coral.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ShallowBranchingCoral_AlbedoHeightSource_20260604
```

## 5 - Aegir Cloud Bands

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_AegirCloudBands_AlbedoSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable atmospheric cloud-band source texture for Aegir, a huge methane-rich blue and blue-purple gas giant seen above a bright alien ocean.

This is a source panel for a Unity URP celestial shader. It is not a final planet disc, not a space background, and not a beauty render.

Include believable large cloud bands, sheared ribbons, storm eddies, soft turbulent strata, cyan, deep blue, indigo, pale violet, and muted gray-white cloud structures. The band hierarchy must remain readable when mapped onto a distant giant planet.

No hard terminator. No black space. No stars. No labels. No text. No logo. No UI. No sine stripes. No muddy gradient. No random noise bands. No cartoon planet. No crayon texture.

The output should tile cleanly as a source panel. Horizontal wrap must be clean; full square tileability is preferred if possible.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_AegirCloudBands_AlbedoSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_AegirCloudBands_AlbedoSource_20260604
```

## 6 - Surface Cloud Deck

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_SurfaceCloudDeck_CloudCoverageSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable bright surface cloud deck source texture for HECTON-8.

Subject: layered white and pale gray coastal clouds above an alien ocean, soft cyan sky scatter gaps, wispy edges, distant cloud depth, feathered coverage, and clean bright daylight.

This is a source panel for later cloud atlas and coverage-mask derivation. It is not a panorama and not a final skybox.

No horizon. No sun disc. No planet baked into this texture. No stars. No text. No logo. No UI. No dark storm-only sky. No stock cloud collage. No hard card rectangles.

The cloud edges must be useful for later alpha or coverage masks while preserving a bright readable surface sky.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_SurfaceCloudDeck_CloudCoverageSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_SurfaceCloudDeck_CloudCoverageSource_20260604
```

## 7 - Caustic Particle Lookup

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_CausticParticleLookup_Mask_20260604.png`

Prompt:

```text
Create ONE seamless square tileable linear mask source for bright shallow underwater caustic and suspended particle breakup in a premium Unity URP ocean survival game.

Subject: clean photic daylight caustic lace, soft refracted streaks, subtle silt sparkle distribution, broken nonuniform bands, and water-column micro variation.

This is source data for a cheap shader or particle lookup. It is not a scenic underwater render and not a global abyss caustic pass.

Use orthographic top-down source-panel view. No perspective. No horizon. No fish. No diver. No terrain scene. No text. No logo. No UI. No baked shadows. No bloom glare.

Keep it subtle and shader-friendly. It must support visual fake-first water presentation without flattening route readability.

Output one 2048x2048 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_CausticParticleLookup_Mask_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_CausticParticleLookup_Mask_20260604
```

## 8 - Kelp Blade Holdfast Material

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable PBR material source for photic-zone kelp blade and holdfast tissue in a premium Unity URP ocean survival game.

Subject: tough wet olive-teal kelp fibers, lengthwise ribs, torn blade edge grain, darker holdfast root pads, sand abrasion, small cavities, subtle salt and mineral speckles, and restrained cyan biological traces.

This is an albedo plus height-like source for geometry-backed kelp. It is not a whole plant render, not a flat ribbon, and not an alpha-card final.

Use orthographic top-down material view. Even diffuse daylight. No perspective. No horizon. No whole kelp object. No text. No logo. No UI. No baked lighting.

The material must keep kelp readable in bright photic water without random neon or muddy dark mass.

Output one 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_KelpBladeHoldfast_AlbedoHeightSource_20260604
```

## 9 - Scanner Rubber Glass Tool Material

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ScannerRubberGlassToolMaterial_AlbedoHeightSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable material source for a pressure-rated handheld scanner in a hard sci-fi underwater survival game.

Subject: graphite rubber grip fields, scratched smoky glass sensor lens surface, dark sealed polymer casing, satin titanium small bevels, cyan phosphor instrument accents, amber inactive locator enamel, salt dust in gasket seams, fine micro scratches, and wet use wear.

This is a material source for later scanner/tool mesh work. It is not a fake UI, not a decal label sheet, and not a finished scanner render.

Use orthographic top-down material view. No object render. No perspective. No horizon. No readable text. No numbers. No labels. No logos. No fake UI screen. No baked lighting.

The material must separate rubber, glass, polymer, metal, and small instrument accents clearly enough for later mask derivation.

Output one 2048x2048 or 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ScannerRubberGlassToolMaterial_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ScannerRubberGlassToolMaterial_AlbedoHeightSource_20260604
```

## 10 - Resource Ore Pickup Mineral

Target file:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ResourceOrePickupMineral_AlbedoHeightSource_20260604.png`

Prompt:

```text
Create ONE seamless square tileable PBR material source for HECTON-8 resource pickup chunks.

Subject: irregular basalt host rock with copper and silver mineral veins, pearly mineral crust, opal flecks, turquoise deposits, small amber crystalline inclusions, chipped fracture edges, wet translucent mineral faces, and pale sediment caught in cavities.

This is a material source for later ore pickup meshes. It is not an icon sheet, not a cube render, not abstract colored currency, and not a full PBR stack.

Use orthographic top-down material view. Even diffuse lighting. No perspective. No horizon. No object render. No text. No logo. No UI. No baked shadows. No full-metallic rock.

The material must read as discoverable physical resource embedded in host rock, with metallic regions localized only where real ore is present.

Output one 2048x2048 or 4096x4096 square image only.
```

QA command:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ResourceOrePickupMineral_AlbedoHeightSource_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ResourceOrePickupMineral_AlbedoHeightSource_20260604
```

## Acceptance Boundary

Passing the prompt and static QA does not mean production-ready. A Unity owner must still prove import settings, PBR channels, material binding, route screenshots, Frame Debugger/RenderGraph state, and profiler/VRAM/GC impact where applicable.
