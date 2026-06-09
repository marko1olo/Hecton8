# Batch34 Texture Source Targeted Regen Prompts

Status: TARGETED_DIRECT_SERVICE_SUBMISSION_QUEUE
Evidence class: STATIC_DOC
Date: 2026-06-08
Output path: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/RegenTargets/`
Source pack: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_PROMPT_PACK_20260608.md`
Required service-agent instruction: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3402_TEXTURE_SERVICE_AGENT_INSTRUCTIONS_20260608.md`
Supersedes selected older fix prompts in: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3405_TEXTURE_SOURCE_FIX_PROMPTS_20260608.md`
Superseded older fix IDs: `B34-FIX-3407`, `B34-FIX-3409`, `B34-FIX-3417`, `B34-FIX-3418`

Install the service-agent instruction before submitting these jobs. Run each prompt as a separate image-generation job. Do not merge the prompts into one request unless the service explicitly supports independent queued jobs.

For the four IDs above, use this file instead of the older `3405` fix prompts. The older file remains a broader historical fix queue for the other needs-work IDs.

## Priority

Required now:
- `B34-3409` because the existing output is `REGEN_RECOMMENDED`.
- `B34-3418` because the existing output is `REGEN_OR_MANUAL_MATTE`.

Optional backup:
- `B34-3407` because the existing output is usable only as `LOCAL_ONLY_OR_REGEN_SEAMLESS`.
- `B34-3417` because the existing output is usable only as `LOCAL_ONLY_OR_CENTER_CROP`.

Global style target for every prompt: HECTON-8, Unity 6000.4 URP AA underwater survival game, NASA-punk / deep sea noir, cinematic realism, believable PBR material truth, Subnautica-level readability as the floor, no mobile-game icon style, no toy plastic, no AI mush, no baked dramatic lighting.

Global negative prompt when the service supports a separate negative field:

```text
no readable text, no labels, no logo, no watermark-like decorative mark, no signature, no frame, no border, no perspective camera, no object showcase, no UI mockup, no mobile-game icon composition, no cartoon material, no candy gradient, no black crush, no muddy low-detail blur, no baked directional light, no cast shadow, no vignette, no fake preview lighting, no repeated obvious landmark in tile, no cropped atlas islands
```

## Required Regen Jobs

## B34-3409-R1 - Limestone Cave Ceiling Mineral Drip

Type: SEAMLESS_TILE
Use: cave ceilings/walls not covered by wet basalt; mineral cave detail.
Reason: previous source is lossy and needs seam-band review; regenerate a cleaner seamless material source.

Prompt:

```text
Square 1:1 seamless PBR basecolor material tile for HECTON-8 submerged limestone cave ceiling mineral drip. Orthographic top-down material scan, tileable edges, no perspective and no cave scene. Pale gray calcium limestone surface with small stalactite root scars, damp mineral drip streaks, salt crust along cracks, green-brown biofilm tucked inside cavities, pressure-aged underwater mineral staining, subtle underside ambient-occlusion color only as material variation, no painted shadows. Real scale about 1 meter per tile. The tile must repeat cleanly in a 2x2 preview with no edge bands, no center hero landmark, no large repeated drip island, no basalt, no dramatic lighting, no black-crush darkness, no full cave render, no text, no labels, no logo, no border. High-end realistic PBR source, 4k.
```

## B34-3418-R1 - Thick Viewport Glass Edge Decal Atlas

Type: DECAL_ATLAS
Use: viewport rims, cockpit glass, pressure windows, glass edge wear.
Reason: previous source needs regeneration or manual matte; output must preserve isolated decal islands and clean padding.

Prompt:

```text
Square 1:1 decal atlas for HECTON-8 thick underwater viewport glass edge wear, not seamless. Transparent background if supported; otherwise use a flat dark neutral removable background with no gradient and no fused matte edges. Isolated subtle decal islands with generous padding on all sides: crescent scratches, milky glass delamination edge fragments, salt fogging patches, pressure-stress cloudy arcs, gasket grime streaks, chipped glass rim wear, condensation beads, tiny cleaning scratches. Keep islands physically thin and usable as decals, not a full window render and not a UI overlay. No island may touch or nearly touch the image edge. No readable text, no UI symbols, no labels, no logo, no cropped islands, no border, no baked directional light, no cast shadow, no vignette. Realistic subtle source decals for cockpit/viewport materials, 4k.
```

## Optional Backup Jobs

## B34-3407-R1 - Iron-Oxide Seep Crust

Type: SEAMLESS_TILE
Use: oxidized seep terrain, old industrial contamination zones, non-vent mineral material.
Reason: previous source is local-only unless a cleaner seamless version is generated.

Prompt:

```text
Square 1:1 seamless PBR basecolor material tile for HECTON-8 iron-oxide cold seep crust. Orthographic top-down material scan, tileable edges, no perspective. Rust-red and brown-orange iron bacteria mats over gray clay substrate, white mineral edge deposits, wet porous crust, subtle black-green abyss staining inside cracks, physically plausible underwater seep surface. This is cold seep oxidation, not hot sulfur, not lava, not generic hydrothermal vent crust. Real scale about 0.8 meters per tile. Must repeat cleanly in a 2x2 preview without seam bands or obvious repeated hero blobs. No text, no labels, no logo, no object render, no baked shadows, no border, no vignette, no black-crush mud, 4k.
```

## B34-3417-R1 - Amber Emergency Lens Material

Type: SEAMLESS_TILE
Use: warning lights, service lamps, emissive masks, physical glass lenses.
Reason: previous source is local-only unless cropped; regenerate if a cleaner full-tile lens material is desired.

Prompt:

```text
Square 1:1 seamless PBR basecolor material tile for HECTON-8 amber emergency light lens material. Orthographic material scan, tileable edges, no object render. Translucent amber ribbed glass or pressure-rated plastic lens surface, fine horizontal/prismatic rib texture, worn micro-scratches, salt specks, tiny edge grime, subtle internal diffusion, realistic physical lamp-cover material. No active glow bloom, no lit symbol, no warning icon, no UI mark, no readable text. Real scale about 0.25 meters per tile. Must tile cleanly for deriving roughness, normal, and emission mask; avoid center badge, avoid full lamp object, avoid lens frame. No labels, no logo, no baked directional light, no border, 4k.
```

## Intake Notes

Download new outputs into the regen target folder first. Do not overwrite accepted original sources until local intake confirms the new output is better.

For `SEAMLESS_TILE`, build 2x2 previews before promotion. Reject if visible seam bands, large repeated hero marks, perspective, baked lighting, or scene composition appear.

For `DECAL_ATLAS`, inspect padding and reject if any island touches the edge, if the matte is fused into the decal shapes, or if the output is a full object/window render.
