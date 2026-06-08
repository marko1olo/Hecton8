# Batch34 Texture Source Expansion Navigation

Status: SOURCE_GENERATION_QUEUE_ONLY
Evidence class: STATIC_DOC
Date: 2026-06-08
Scope: parallel image-service prompts for texture sources, trim sheets, decal atlases, flora/fauna UV atlases, and pickup/source-material atlases.

This is not Unity import proof, runtime proof, material binding proof, or final art acceptance. Generated outputs stay under:

`Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/`

Prompt pack:

`Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_PROMPT_PACK_20260608.md`

Service-agent instruction file:

`Docs/GeneratedAssets/Gemini/Prompts/Batch34/3402_TEXTURE_SERVICE_AGENT_INSTRUCTIONS_20260608.md`

Install the service-agent instruction before sending task prompts. It defines reference-upload presets, output discipline, watermark handling, rate-limit behavior, and reject rules for the external generation service.

Direct 25-job submission packs:

- Part 1: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3403_TEXTURE_SOURCE_EXPANSION_DIRECT_PART1_25_20260608.md`
- Part 2: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3404_TEXTURE_SOURCE_EXPANSION_DIRECT_PART2_25_20260608.md`

Use the direct packs when the service agent can process approximately 25 queued jobs at once. They are split from the full prompt pack and include the shared style/negative rules plus intake notes.

## Why Batch34 Exists

Batch34 covers useful texture-source gaps without duplicating the active Gemini material catalog:

- more terrain/world material range beyond basalt, vent crust, shell sand, and generic biome samples;
- hard-surface trim sheets and modular detail sources for base modules, wreckage, tools, hatches, and corridors;
- decal atlases for rust, leaks, salt, scratches, pressure cracks, wetness, glass dirt, and contamination;
- flora/coral UV atlases that feed mesh families instead of flat icon-like images;
- fauna UV atlases for readable creature material zones, not full creature beauty renders;
- pickup/resource atlases for real 3D resource meshes, not mobile-game inventory icons.

## Anti-Duplicate Locks

Do not generate these again unless a later owner task proves a specific missing role:

- generic wet basalt tile;
- generic ocean foam or shoreline foam beauty texture;
- generic carbon composite, orange safety panel, black rubber, tool housing metal, dark anodized metal, ribbed trim, pressure glass, white ceramic, salvage repair metal;
- generic hydrothermal vent crust, living kelp frond, soft jelly membrane, abyssal predator hide, bioluminescent coral flesh, pale tube coral, creature bone plate;
- oxygen/resource icons as flat 2D icon art.

Allowed near-neighbor generation is role-specific: trim sheet, decal atlas, UV atlas, repair/cut cross-section, resource nodule atlas, or missing biome material not already covered.

## Output Types

Use each prompt exactly as a standalone task. The service may generate in parallel.

- `SEAMLESS_TILE`: square 1:1, seamless/tileable edges, orthographic material scan, no baked lighting, no perspective, no text.
- `TRIM_SHEET`: square 1:1, modular horizontal/vertical bands, each band tileable along its long axis, no text.
- `DECAL_ATLAS`: square 1:1, not seamless, isolated decals with generous padding, transparent if supported or dark neutral removable background.
- `UV_ATLAS`: square 1:1, not seamless, multiple material islands with generous padding, no full object render.
- `PICKUP_ATLAS`: square 1:1, not seamless, source material islands for 3D pickup meshes, not inventory icons.

If the image service can output maps, request:

- BaseColor: sRGB, no baked light/shadow.
- NormalGL: tangent normal source, no albedo.
- Roughness: grayscale, material-true.
- Height: grayscale, derivation source.
- AO: grayscale, cavity-biased.

If it cannot, generate BaseColor/source only. Codex/intake derives normal/MRAO later.

## Priority Order

### P0 - Highest Gameplay/World Value

Run first because these immediately help terrain route, base/wreck assets, and Unity material binding:

- B34-3401 Photic Limestone Rubble Shelf
- B34-3403 Brine Canyon Salt-Crust Silt
- B34-3411 Pressure Base Exterior Hull Trim Sheet
- B34-3412 Pressure Base Interior Wall Trim Sheet
- B34-3413 Wet Service Deck Anti-Slip Floor
- B34-3419 Welded Seam And Rivet Row Trim Sheet
- B34-3423 Leak Rust Biofilm Decal Atlas
- B34-3424 Paint Chip Scratch Decal Atlas
- B34-3426 Instrument Glass Smudge Alpha Decal Atlas
- B34-3433 Brine Vane Flora UV Atlas
- B34-3441 Neutral Grazer Skin UV Atlas
- B34-3448 Resource Nodule Pickup UV Atlas

### P1 - Strong Secondary Coverage

- B34-3404 Abyssal Manganese Nodule Plain
- B34-3406 Serpentinite Fault Rock
- B34-3408 Clay Silt Turbidity Slope
- B34-3414 Rubber Gasket Ring Trim Sheet
- B34-3415 Cable Jacket Repair Wrap Tile
- B34-3418 Thick Viewport Glass Edge Decal Atlas
- B34-3427 Pressure Crack Glass Decal Atlas
- B34-3435 Plate Coral Rim UV Atlas
- B34-3437 Kelp Holdfast Root Atlas
- B34-3442 Filter Feeder Gill Membrane Atlas
- B34-3444 Armored Benthic Shell Atlas
- B34-3449 Industrial Salvage Small Parts Atlas

### P2 - Fill-In Library

All remaining prompts are useful, but they can wait until P0/P1 outputs are reviewed or while the service has spare parallel slots.

## Intake Contract For Future Agents

After download:

1. Save under `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/`.
2. Keep original filename plus target id when possible: `B34_3401_<short_name>`.
3. Repair unavoidable watermark only after localizing it. Do not crop away important image content.
4. Compress source candidates honestly: target approximately 0.5-1.5 MB for cleaned BaseColor JPEG/WebP candidates before Unity import, unless high-detail source review needs the original.
5. Run 2x2 tile preview for every `SEAMLESS_TILE`.
6. Reject if seams, baked light, perspective, text, logo, watermark-like design mark, cropped atlas islands, mobile-game icon style, black-crush, or muddy low-detail output remain visible.
7. Promote to `Assets/**` only through explicit intake/import task with material manifests, BC7/BC5 import settings, normal/MRAO derivation, preview sheets, and Unity proof.

Final status: READY_FOR_PARALLEL_SOURCE_GENERATION.
