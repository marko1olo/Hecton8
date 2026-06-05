# 2101 Wet Basalt Shoreline Prompt Pack

Agent ID: 2101  
Evidence class: STATIC_DOC  
Generation state: NOT RUN  
Browser/Gemini state: NOT RUN

Use only under a future explicit generation/intake task. Do not write outputs into `Assets/**`. Save candidates under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` and run intake QA before any derivation.

## Prompt ID: TX_B21_WetBasaltShoreline_Albedo_20260604

Priority: Rank 1 / spend-first from Batch21 2022 queue.  
Output: seamless square albedo/base-color source, sRGB intent, material source only.

Prompt:

```text
Create ONE seamless square tileable PBR albedo texture for a premium Unity URP ocean survival game. Subject: natural wet basalt shoreline on an alien ocean moon, volcanic black-gray basalt with salt-water erosion, chipped fracture planes, pores, stratified cracks, subtle teal mineral staining, pale salt residue in crevices, tiny sediment caught in cracks, and believable wet shoreline material breakup. This is base color only. Use orthographic top-down material view, even neutral bright photic daylight, no baked light, no cast shadows, no perspective, no horizon, no object silhouette, no text, no logo, no UI. Make the pattern reusable with no large hero cracks or obvious repeated plates. Output one image only.
```

Negative prompt:

```text
visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, cliff scene, object render, labels, text, logo, UI, watermark, frame, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black noir darkness, cartoon, painterly, low-poly, generic noise, smooth blob rock, chrome wetness, uniform glossy overlay, dirty mud replacing material truth
```

Tiling requirement:

```text
4096x4096 preferred. Square, seamless, tileable on all edges. Orthographic top-down. No directional lighting.
```

Static QA command shape:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_WetBasaltShoreline_Albedo_20260604
```

Static acceptance criteria:

- no hard seam;
- no clipped albedo;
- no baked lighting;
- no text/UI;
- no perspective;
- no obvious 3x3 repetition;
- basalt readable in bright photic daylight;
- suitable for later height/normal/MRAO/wetness derivation.

## Prompt ID: TX_B21_ShoreFoamSaltContact_Mask_20260604

Priority: Rank 2 / spend-first from Batch21 2022 queue.  
Output: seamless square RGBA mask source, linear intent, presentation only.

Prompt:

```text
Create ONE seamless square tileable RGBA foam and shoreline contact mask source for natural wet basalt at a bright alien ocean shoreline. Subject: clean photic-shallow shoreline contact, thin white sea foam lace, translucent micro bubbles, tide-sheared strands, broken foam cells, wet edge breakup, salt and sediment contact residue, and natural foam interaction with rough wet basalt cracks. This is source data for a Unity URP material, not a scenic wave photo. Orthographic top-down material mask view, no perspective, no horizon, no object silhouette, no text, no logo, no UI, no baked lighting. Channel intent: Red long foam strand/contact strength, Green cross-flow wet edge breakup, Blue foam lace bubbles and sediment interruption, Alpha optional confidence only. Output one image only.
```

Negative prompt:

```text
visible seams, flat white strips, opaque snow foam, dirty storm foam default, wave photo, perspective, horizon, object render, labels, text, logo, UI, watermark, baked light, cast shadow, black muddy contact band, repeated bubble stamps, low-resolution mush, cartoon, painterly, generic noise, foam used to hide weak rock art
```

Tiling requirement:

```text
2048x2048 minimum, 4096x4096 preferred. Square, seamless, tileable. Orthographic top-down mask/source view.
```

Static QA command shape:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_ShoreFoamSaltContact_Mask_20260604.png --out-dir Docs/GeneratedAssets/Gemini/QA/Batch21/TX_B21_ShoreFoamSaltContact_Mask_20260604
```

Static acceptance criteria:

- no hard seams in 2x2 or 3x3;
- sparse natural foam lace;
- channel regions visually separable;
- no opaque full-white strip;
- no scenic perspective;
- no text/UI;
- usable for later packing.

## Required Future Intake Notes

- `PASS_STATIC` is not Unity acceptance.
- `REVIEW` is allowed only if the hard reject gates are clear and manual review is still needed.
- Failed candidates remain diagnostic/reference only.
- No final PBR derivation may begin if albedo or mask seam/material gates fail.
- No Unity import or material binding claim may be made from this prompt pack.

