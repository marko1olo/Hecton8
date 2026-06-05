# 2106 Product-Face Tool And Resource Prompt Pack

Agent ID: 2106
Batch: batch21_art_replacement_wave
Evidence class: STATIC_DOC
Generation status: NOT RUN

## Boundary

These prompts are source-candidate instructions only. Do not run browser/Gemini from this task. Do not save outputs into `Assets/**`. Future outputs belong under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` and must pass intake QA, manual tiling review, PBR derivation, Unity import, material binding, screenshots, and profiler/VRAM proof where applicable.

## Universal Negative Terms

Use for every prompt:

```text
No directional lighting, no cast shadows, no baked highlights, no perspective, no object render, no scene background, no frame, no border, no watermark, no logo, no readable random text, no UI screenshot, no camera depth of field, no glossy toy plastic, no clean generic sci-fi, no purple/blue gradient sci-fi, no random glowing ore, no currency gem, no flat color, no low-resolution blur, no crayon marks, no black/noir concealment.
```

## Prompt 2106-A: Scanner Tool Casing Material Source

Prompt ID: `TX_B21_ProductFaceScannerCasing_AlbedoHeightSource_2106`

Intended use: source candidate for scanner/tool casing albedo and height-like derivation. Future material owner must derive normal and packed mask using the target shader channel contract.

```text
Orthographic seamless square PBR material sample, pressure-rated handheld underwater scanner casing material, dark graphite painted titanium and rugged black polymer composite, worn beveled edge paint, salt deposits in seams, small scratches from gloved hands, subtle oil grime near panel cuts, rubbed contact polish around grip zones, NASA-punk deep sea industrial equipment, realistic wet material response, base color plus height-like surface information, 1 meter tile scale, detailed but not noisy, physically plausible corrosion and wear, no lighting or shadows.
```

Required QA focus:

- edge wear must look like casing/paint wear, not random grunge;
- no generated text or logos;
- no object silhouette;
- enough height signal for scratches/grip/paint chips;
- compatible with scanner body, analyzer, repair, cutter, and builder casing variants after atlas/decal work.

## Prompt 2106-B: Scanner Glass And Display Wear Source

Prompt ID: `TX_B21_ProductFaceScannerGlassWear_MaskSource_2106`

Intended use: source candidate for future screen/glass scratch, salt, dirt, and wetness masks. Current ToolScreenDiegetic premium scratch/grime/wetness channel remains blocked until a shader/material owner extends and proves it.

```text
Orthographic seamless square transparent-mask style material source for dirty pressure glass on a handheld underwater scanner display, fine scratches, salt specks, condensation streaks, rubbed clean arcs from gloved thumb, subtle edge grime, sparse wetness beads, readable clear zones for instrument text, NASA-punk industrial dive equipment, mask-ready grayscale detail, no text, no icons, no glowing UI, no screen content, no lighting or shadows.
```

Required QA focus:

- must not obscure critical readout zones;
- no fake glyphs or UI;
- no heavy white scratches everywhere;
- future channel owner must define exact mask packing before material use.

## Prompt 2106-C: Copper Ore Pickup Material Source

Prompt ID: `TX_B21_ResourcePickupCopperOre_AlbedoHeightSource_2106`

Intended use: source candidate for copper-bearing pickup host rock and ore inclusions. It must support a non-primitive ore chunk mesh with visible host-rock boundary.

```text
Orthographic seamless square PBR material sample, wet fractured dark basalt host rock with localized copper mineral inclusions and green-blue copper oxide seams, chipped rock planes, cavity dirt, underwater sheen in recesses, small scale mineral streaks, rough physical ore surface, realistic geology, no glowing fantasy crystal, no gold coin look, no colored currency rock, base color plus height-like source, 1 meter tile scale, no lighting or shadows.
```

Required QA focus:

- ore must be localized in seams/inclusions, not full-rock metallic;
- host rock must remain visible;
- no glowing ore;
- future metallic mask may mark only real copper/exposed mineral regions.

## Prompt 2106-D: Silver Ore Pickup Material Source

Prompt ID: `TX_B21_ResourcePickupSilverOre_AlbedoHeightSource_2106`

Intended use: source candidate for silver-bearing pickup host rock and narrow seam language distinct from copper.

```text
Orthographic seamless square PBR material sample, dark cold fractured host rock with narrow silver-gray mineral veins and chipped fracture planes, wet underwater cavity AO, subtle metallic flecks only inside mineral seams, rough basalt/shale matrix, sediment in cracks, physically credible salvage resource material, base color plus height-like source, 1 meter tile scale, no glow, no currency gem, no lighting or shadows.
```

Required QA focus:

- distinct from copper by seam color and vein shape;
- no full-rock metallic;
- no fantasy crystal or random glitter field.

## Prompt 2106-E: Titanium Scrap Pickup Material Source

Prompt ID: `TX_B21_ResourcePickupTitaniumScrap_AlbedoHeightSource_2106`

Intended use: source candidate for salvage plate/chunk pickups and canonical titanium scrap visuals, not a new item identity.

```text
Orthographic seamless square PBR material sample, cut and bent titanium salvage plate material from a pressure-rated underwater industrial module, scratched dark paint remnants, exposed bright torn metal edges, bolt-hole wear, salt crust, oil grime, faint worn service stencil fragments without readable text, chipped coating, realistic wet metal, base color plus height-like source, 0.5 meter tile scale, no object render, no logo, no lighting or shadows.
```

Required QA focus:

- service markings must be abstract/non-readable unless future decal workflow owns text;
- metal/paint/cut-edge separation must be clear;
- source cannot define a separate `Item_Titanium` truth route.

## Static Acceptance Rules

Any future candidate from this prompt pack remains `CANDIDATE` until:

- saved outside `Assets/**`;
- SHA-256 recorded;
- 2x2 static tile QA and manual 3x3 review pass;
- no baked lighting/object render/text/logo/perspective contamination;
- material role manifest declares albedo, normal/height source, packed-mask derivation, and shader-specific channel order;
- future Unity owner imports and binds with proof.
