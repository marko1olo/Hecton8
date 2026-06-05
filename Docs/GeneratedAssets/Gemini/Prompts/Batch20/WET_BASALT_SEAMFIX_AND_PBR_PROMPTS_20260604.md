# Wet Basalt Seam-Fix And PBR Source Prompt Pack

Date: 2026-06-04
Evidence class: STATIC_DOC
Runtime, Unity import, material assignment, screenshots, profiler, and visual acceptance: PENDING VERIFICATION

## Boundary

This file authors English prompts and channel guidance only. It does not generate images, download candidates, edit `Assets/**`, import textures, assign materials, run Unity, run builds, or claim final visual quality.

Static source basis:

- Existing wet basalt albedo source: `TX_H8_WetBasaltShoreline_Albedo_1428`.
- Batch19 1906 QA rejected direct production PBR derivation from the current albedo because it is albedo-only and its edge seam mean absolute RGB diff was too high: left-right `30.78`, top-bottom `33.40`.
- Batch19 1907 package requires seam-fixed wet basalt albedo, matching normal/height source, roughness/wetness logic, cavity AO, salt/mineral mask, waterline transition mask, and foam/contact mask sources before Unity-owner material promotion.

Primary prompt count: 7.

## Shared Operator Rules

Use Gemini/browser generation as source authoring only. Save generated candidates under `Docs/GeneratedAssets/Gemini/` first, not under `Assets/**`.

Every accepted candidate must pass the Batch20 QA checklist before a Unity owner imports or binds it:

- square;
- seamless;
- tileable;
- orthographic;
- no perspective;
- no shadows baked;
- no labels, text, UI, watermark, logo, or frame;
- no lighting gradient;
- no directional cast shadow;
- natural wet basalt shoreline;
- alien ocean moon context, but realistic PBR material behavior;
- bright/readable surface and photic-shallow material response, not black/noir hiding.

Global negative prompt to append to every generation request:

```text
Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, smooth blob rock, chrome wetness, uniform glossy overlay, dirty mud replacing material truth.
```

## B20-WB-001 Seamless Tileable Albedo

Purpose: regenerate or seam-fix the wet basalt shoreline albedo as true base color only.

Prompt:

```text
Create ONE square seamless tileable PBR albedo texture.

Subject: natural wet basalt shoreline on an alien ocean moon, realistic volcanic black-gray basalt with salt-water erosion, chipped fracture planes, small pores, stratified cracks, subtle teal mineral staining, pale salt residue in crevices, tiny sediment caught in cracks, and believable wet shoreline material breakup.

This is base color only for a Unity URP terrain/coastline material. Square, seamless, tileable, orthographic top-down material view, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Lighting requirements: even neutral diffuse daylight, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow, no photographic glare, no specular hotspot. Natural bright photic-shallow coastline readability, not dark noir.

Material target: realistic PBR source, Subnautica-level or better surface readability, HECTON-8 NASA-punk deep-sea world, alien but physically believable wet basalt shoreline. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, smooth blob rock, chrome wetness, uniform glossy overlay, dirty mud replacing material truth.
```

Acceptance intent:

- Albedo must contain color and material breakup only.
- Wetness may darken natural stone color, but it must not paint a specular highlight.
- Reject if a 3x3 tile preview shows hard seams, repeated hero cracks, or a global light direction.

## B20-WB-002 Normal Map Reference Source

Purpose: create a matching normal-map source/reference for the seam-fixed albedo. This is source guidance until checked by a PBR/Unity owner.

Prompt:

```text
Create ONE square seamless tileable OpenGL tangent-space normal map for the attached or referenced wet basalt shoreline albedo.

Subject relief: natural wet basalt shoreline on an alien ocean moon, realistic fracture planes, chipped basalt edges, small pores, eroded salt-water cracks, mineral ridges, shallow sediment caught in crevices, and fine rough volcanic grain.

Square, seamless, tileable, orthographic top-down material map, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Map requirements: normal map colors only, no albedo color, no grayscale height rendering, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom. Preserve the same material scale as the albedo.

Material target: realistic PBR source for Unity URP terrain/coastline basalt. The relief must describe physical surface height, not color noise. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, smooth blob rock, chrome wetness, uniform glossy overlay, dirty mud replacing material truth, embossing random albedo stains as height, flat purple normal map with no fracture relief, inverted-looking cavities.
```

Acceptance intent:

- Use as a normal candidate only after visual and channel QA.
- If Gemini cannot output stable tangent-space normals, request a seamless height/relief source instead and derive the normal offline.
- Unity owner must confirm normal orientation and import as normal map before material use.

## B20-WB-003 Roughness Map Source

Purpose: create a grayscale roughness source. This is source/intermediate guidance, not proof of a final shipped texture binding.

Prompt:

```text
Create ONE square seamless tileable grayscale PBR roughness map for the attached or referenced wet basalt shoreline albedo.

Subject: natural wet basalt shoreline on an alien ocean moon. Roughness logic must be physically believable: wet cracks, puddled micro-cavities, and mineral-stained damp bands are darker and smoother; raised dry basalt chips, salt crust, rough pores, and exposed eroded edges are lighter and rougher. Do not make the whole material uniformly glossy.

Square, seamless, tileable, orthographic top-down material map, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Map requirements: grayscale roughness only, no albedo color, no normal-map colors, no AO dirt layer, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom. Preserve the same material scale as the albedo.

Material target: realistic PBR source for Unity URP wet basalt coastline. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, constant gray roughness, full black mirror wetness, full white chalk map, random dirt pretending to be roughness.
```

Acceptance intent:

- For the current `MraoAtlasLit_MRAO` contract, roughness belongs in packed channel G.
- If a target shader expects smoothness instead, invert offline and document the contract. Do not guess.

## B20-WB-004 AO Map Source

Purpose: create a grayscale ambient occlusion source biased to cavities and contact crevices.

Prompt:

```text
Create ONE square seamless tileable grayscale ambient occlusion map for the attached or referenced wet basalt shoreline albedo.

Subject: natural wet basalt shoreline on an alien ocean moon. AO logic must be cavity-biased: deep cracks, undercut chips, pores, fracture intersections, sediment-filled crevices, and mineral pockets are darker; exposed flat basalt planes and raised dry stone faces remain light. Do not turn the whole rock into dirty black mud.

Square, seamless, tileable, orthographic top-down material map, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Map requirements: grayscale AO only, no albedo color, no normal-map colors, no roughness map, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom. Preserve the same material scale as the albedo.

Material target: realistic PBR source for Unity URP wet basalt coastline. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, random dirt over exposed surfaces, broad vignette, full black cracks from fake lighting, flat white map with no cavity signal.
```

Acceptance intent:

- For the current `MraoAtlasLit_MRAO` contract, AO belongs in packed channel B.
- AO must be independent from roughness. Identical roughness/AO maps are rejected unless a material owner documents why.

## B20-WB-005 MRAO Packed Source Guidance

Purpose: request a packing reference for the final combined mask. Exact channel packing should be done offline after separate channel QA because AI image tools may not preserve exact channel semantics or alpha.

Prompt:

```text
Create ONE square seamless tileable RGBA PBR packed mask source for the attached or referenced wet basalt shoreline albedo.

Subject: natural wet basalt shoreline on an alien ocean moon, realistic volcanic basalt, salt-water erosion, mineral staining, wet cracks, dry raised chips, cavity occlusion, and shoreline material breakup.

Square, seamless, tileable, orthographic top-down packed material map, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Channel intent for source guidance:
Red channel = metallic. Basalt is non-metallic, so this channel must be black or near zero except no ore is present here.
Green channel = roughness. Wet cracks and damp mineral stains darker/smoother; raised dry chips and salt crust lighter/rougher.
Blue channel = ambient occlusion. Cavity-biased only; cracks, pores, undercuts, and sediment pockets darker; exposed surfaces light.
Alpha channel = emission mask or reserved family mask. For normal wet basalt, use black/zero unless a Unity material owner explicitly locks a wetness/family-mask contract later.

Map requirements: no albedo color, no normal-map colors, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom. Preserve the same material scale as the albedo.

Material target: realistic PBR source for Unity URP wet basalt coastline. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, metallic whole rock, colored albedo leaking into masks, identical roughness and AO channels, fake emissive lava, alpha used as random dirt without contract.
```

Packing guidance:

- Accepted static contract from Batch19 1906 for `Hecton_MraoAtlasLit`: `_MraoMap R Metallic, G Roughness, B AO, A EmissionMask`.
- Wet basalt metallic must be zero unless a separate ore/vent route is created.
- If a future shader owner routes wetness into A, the material contract must be updated before packing. Do not silently repurpose A.

## B20-WB-006 Waterline Wetness Mask

Purpose: create a separate mask source for wet/dry shoreline transition and mineral breakup. This supports shader blending or offline packing under a future Unity-owner material contract.

Prompt:

```text
Create ONE square seamless tileable grayscale or RGBA waterline wetness mask source for a natural wet basalt shoreline on an alien ocean moon.

Subject: wet/dry basalt transition at the ocean waterline, irregular tide edge, salt residue, teal mineral staining, drying falloff, sediment caught in cracks, small bead-like wet cavities, and believable coastal erosion. The mask must help blend wet shoreline rock into dry raised basalt without using darkness or fog to hide the edge.

Square, seamless, tileable, orthographic top-down material mask, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Suggested RGBA channel intent for source guidance:
Red = wetness strength.
Green = drying falloff / transition softness.
Blue = salt, sediment, and mineral breakup.
Alpha = specular boost or reserved confidence mask, only if the future shader owner accepts it.

Map requirements: mask data only, no albedo beauty render, no normal-map colors, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom.

Material target: realistic PBR wet basalt coastline waterline source. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, uniform black band, hard straight waterline stripe, muddy dark overlay, glossy plastic wetness, storm grime replacing material truth.
```

Acceptance intent:

- This mask is not terrain truth and not save truth. It is presentation/material blending.
- The surface/coastline must stay bright and readable. A dark waterline cover-up is rejected.

## B20-WB-007 Foam And Contact Mask

Purpose: create a foam/contact source for shoreline foam lace, contact breakup, and wet edge blending.

Prompt:

```text
Create ONE square seamless tileable RGBA foam and shoreline contact mask source for natural wet basalt at an alien ocean shoreline.

Subject: clean photic-shallow shoreline contact, thin white sea foam lace, small translucent bubbles, tide-sheared strands, broken foam cells, wet edge breakup, salt/sediment contact residue, and natural foam interaction with rough wet basalt cracks. The material must look like premium realistic ocean shoreline source data, not a scenic wave photo.

Square, seamless, tileable, orthographic top-down material mask, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame.

Suggested RGBA channel intent for source guidance:
Red = long foam strand / contact foam strength.
Green = cross-flow wet edge breakup.
Blue = foam lace breakup, small bubbles, and sediment/salt interruption.
Alpha = optional caustic receiver or confidence mask, only if the future shader owner accepts it.

Map requirements: mask/source data only, no albedo beauty render, no normal-map colors, no lighting, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow. Preserve exact edge continuity left-right and top-bottom. Foam must be sparse enough to blend, not a solid opaque white strip.

Material target: realistic PBR shoreline foam/contact source for Unity URP coastline presentation. Output ONE image only.

Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black/noir darkness used to hide weak detail, cartoon, painterly, low-poly, generic noise, opaque snow foam, flat white stripes, dirty storm foam default, wave-scene perspective, repeated bubble stamps, black muddy contact band.
```

Acceptance intent:

- Foam is a visual fake and presentation source, not fluid simulation.
- Compact quality may use sparse lower-resolution masks, but it must keep clean foam identity and readable wet contact.
- High and Ultra add density/detail only after the same mask logic passes tile and channel QA.

## Seam-Fix Retry Prompt

Use this only when a candidate is close but fails tileability or obvious repetition.

```text
Revise the attached texture into a TRUE production seamless square tile.

Keep the same natural wet basalt shoreline material identity and the same PBR channel role, but fix tileability: left edge must match right edge invisibly, top edge must match bottom edge invisibly, and a 3x3 tiled preview must show no hard seams.

Remove large recognizable repeated hero shapes. Make the pattern more isotropic, natural, and stochastic while preserving believable basalt fractures, salt-water erosion, wet cracks, mineral stains, and material scale.

Square, seamless, tileable, orthographic top-down material view, no perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no watermark, no frame, no shadows baked, no baked highlights, no lighting gradient, no directional cast shadow.

Do not darken the material into black/noir. Do not hide weak detail with mud, fog, or shadow. Output ONE corrected square texture only.
```

## Continuous Quality Consequences

These consequences describe source and material fidelity scaling only. They do not change terrain truth, water truth, save identity, collision, gameplay authority, or shader channel semantics.

| Lane | Consequence |
|---|---|
| Compact / near 0.0 | 512-1024 shipped/import target after Unity owner review, readable basalt identity, seam-fixed albedo, one correct normal, one packed mask, sparse foam/contact masks, no muddy/noir fallback. |
| Middle / around 0.35 | 1024-2048 source targets, clearer roughness/AO independence, stronger wet/dry band, better mineral and sediment breakup, moderate foam lace. |
| High / around 0.7 | 2048 key coastline sources, richer fracture normals, sharper cavity AO, denser wetness/mineral masks, improved shoreline foam breakup after proof. |
| Ultra / near 1.0 | 2048-4096 hero-only source bakes, dense but controlled foam/contact detail, high precision MRAO packing, richer mineral/salt/wetness detail. Visual overkill only; material contracts stay fixed. |

## Handoff

The next operator must generate candidates, save them under `Docs/GeneratedAssets/Gemini/`, and run the Batch20 QA checklist. A Unity owner later handles import settings, material/TerrainLayer assignment, scene proof, Frame Debugger/RenderGraph/profiler evidence, and final rejection or promotion.
