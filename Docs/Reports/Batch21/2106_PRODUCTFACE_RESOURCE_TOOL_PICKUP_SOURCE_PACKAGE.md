# 2106 Product-Face Resource Tool Pickup Source Package

Agent ID: 2106
Batch: batch21_art_replacement_wave
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW
Unity/build/import/profiler/player execution: NOT RUN

## Boundary

This is a static/source-package task. It does not generate images, meshes, textures, materials, prefabs, imports, screenshots, profiler captures, or runtime proof. It defines constraints, prompt requirements, QA gates, and future Unity-owner handoff packets for product-face tools, scanner/tool surfaces, resource ore pickup materials, salvage plate/chunk pickups, and pickup mesh replacements.

First-20-minutes route blocker removed: future owners now have one source-package contract for scanner/tool and pickup visuals that support early scanning, pickup readability, salvage, crafting, and repair route decisions without accepting primitive cube/plane/sphere placeholders.

## Authorities Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `3DMODEL_EQUIPMENT_PROPS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_HERO_REALISM_OVERKILL.md`
- `tools.md`
- `inventory.md`
- `construction.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `rendering.md`
- `shaders.md`
- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md`

Mandates loaded:

- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Batch21 2022 Evidence

`Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md` ranks scanner/tool material as rank 9 and resource ore pickup material as rank 10. It states they are included because Batch18 evidence shows product-face cube/plane primitive debt for scanner and pickups. It also states they are lower priority than surface, sky, terrain, waterline, and photic blockers.

Evidence label: STATIC_DOC. 2022 did not generate images, download candidates, run QA, import materials, or prove Unity visuals.

## Targeted Batch18 Evidence

Targeted search was restricted to `Docs/Reports/Batch18` for scanner/tool/pickup/cube/plane/resource/ore/primitive/ProductFace terms. Relevant files read:

- `Docs/Reports/Batch18/1864_PRODUCT_FACE_PRIMITIVE_REPLACEMENT_QUEUE.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1869_TOOL_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_AUDIT.md`

## Evidence Table

| Evidence | Static finding | Status for 2106 |
|---|---|---|
| 1864 primitive queue | Product-facing player/tool/item/resource/transport prefabs contain Unity built-in primitive mesh GUID `0000000000000000e000000000000000`; tool and resource pickups remain primitive visual debt. | STATIC VERIFIED |
| 1869 tool source package | All 12 held/world tool pairs still use cube body meshes; scanner marker assets are support only, not scanner body; accepted non-primitive body meshes were not found. | STATIC VERIFIED |
| 1870 resource pickup package | CopperOre, FiberKelp, HydrocarbonResin, MembraneTissue, SilicaShards, SilverOre, SulfurClumps, TitaniumScrap, and Item_Titanium use cube/plane/sphere-class built-in primitives. | STATIC VERIFIED |
| 1874 tool mesh authoring | Editor-only future source route exists for tool mesh source assets, but Unity execution and generated mesh assets are pending. | CANDIDATE SOURCE ROUTE |
| 1875 resource mesh authoring | Editor-only future source route exists for resource pickup mesh source assets, but Unity execution and generated mesh assets are pending. | CANDIDATE SOURCE ROUTE |
| 1880 tool materials | Current tool body materials are placeholder/no-texture or package-cache `Lit.mat`; ToolDecayLit channel contract is `_MaskMap R Metallic / G AO / B Smoothness / A EmissionMask`. | STATIC VERIFIED |
| 1881 resource materials | Current resource materials are flat URP Lit colors with empty texture slots; distinct PBR source packages are missing. | STATIC VERIFIED |
| 1888 channel manifest | Product-face packed-map channel meanings differ by shader. Agents must not infer channel order from filenames. | STATIC VERIFIED |
| 1896 tool screen audit | ToolScreenDiegetic has a narrow `_ToolScreenTex.rgb` screen-signal contract; alpha is unused; no material assignment or premium scratch/grime/wetness channel proof exists. | STATIC VERIFIED / BLOCKED FOR PREMIUM SCREEN MATERIAL |

Unverified paths, generated outputs, Unity assignments, screenshots, and profiler claims remain `CANDIDATE` or `PENDING VERIFICATION`.

## Scope Split

Owned by this package:

- source constraints for scanner/tool casing material, grip/rubber, display/emissive glass, resource ore pickup material, salvage plate/chunk pickup material, and pickup proxy mesh replacement;
- prompt requirements for future Gemini/source-image attempts;
- QA gates for source images, meshes, materials, proof packets, and handoff;
- owner boundary so source art does not take over tool verbs, item ids, recipes, save identity, or economy truth.

Not owned by this package:

- broad inventory redesign;
- crafting balance;
- item id changes;
- recipe truth;
- save/load identity;
- scanner gameplay truth;
- Unity import/relink;
- prefab edits;
- material edits;
- runtime UI/HUD binding;
- proof screenshots or profiler capture.

## Family Matrix

| Family | Evidence basis | Required output | Status |
|---|---|---|---|
| Scanner/tool casing material | 1869, 1880, 2022 rank 9 | worn pressure-rated metal/polymer casing with scratches, salt, labels, AO, packed mask, no flat placeholder | CANDIDATE SOURCE PACKAGE |
| Tool grip/rubber | equipment prop bible, 1880 | ribbed worn rubber, seals, cable insulation, roughness variation, contact polish | CANDIDATE SOURCE PACKAGE |
| Display/emissive glass | 1896 | physical screen/glass material around `_ToolScreenTex`, scratched/dirty glass only after channel proof | BLOCKED PREMIUM CHANNEL CONTRACT |
| Resource ore pickup material | 1870, 1881, 2022 rank 10 | host rock plus localized mineral inclusions/veins, fracture normals, AO, wetness, no generic colored rock | CANDIDATE SOURCE PACKAGE |
| Salvage plate/chunk pickup | 1870, 1881 | bent/cut titanium or metal scrap, bolt holes, torn paint, salt/oil grime, exposed cut edges | CANDIDATE SOURCE PACKAGE |
| Pickup proxy mesh replacement | 1864, 1870, 1875 | LOD0/1/2 non-primitive visual meshes with `COL_*` pickup proxy split | CANDIDATE SOURCE ROUTE |

Unsupported families not listed here remain `CANDIDATE` until evidence exists.

## Functional Geometry Contract

Product-facing tools and pickups must still read when textures are disabled.

Required for scanner/tool bodies:

- grip zone with human hand scale;
- bevels/chamfers on all close-view hard edges;
- screen/display inset with glass bevel where a display exists;
- lens/sensor/nozzle/muzzle/emitter geometry by verb;
- latch, fastener, screw, hinge, cartridge, cable, connector, or service panel logic;
- scale witness: screws, grip rib spacing, labels, battery latch, lens ring, cartridge seam;
- named anchors for `ANCHOR_Grip_*`, `ANCHOR_ScanOrigin`, `ANCHOR_RayOrigin`, `ANCHOR_BeamOrigin`, `ANCHOR_Muzzle`, `ANCHOR_TetherAnchor`, `ANCHOR_Pickup`, or verb-specific equivalents where applicable.

Required for resource pickups:

- ore host-rock boundary modeled or baked into geometry/material masks;
- fractured silhouette, chipped edges, clusters, fronds, folds, clumps, shards, or bent plates by item identity;
- pickup scale witness: shard count, bolt holes, kelp bundle tie, cut metal edge, mineral vein thickness;
- no cube, sphere, capsule, plane, or texture-only fake read as final art.

## Material Truth Table

| Family | Material truth | Rejected shortcut |
|---|---|---|
| Scanner/tool casing | painted metal/composite, chipped edge wear, salt, oil, grime, pressure-rated seams | flat colored casing, toy plastic, package Lit |
| Tool grip/rubber | ribbed rubber, seal/gasket wear, contact polish, cable insulation | smooth black block |
| Display/glass | dirty pressure glass, scratch normal/mask, inset screen, owner-truth RT or authored fallback | generic emissive rectangle, fake UI state |
| Ore pickup | host rock, localized ore/mineral inclusion, fracture normal, cavity AO, underwater wetness | one recolored rock for all ore |
| Biological/resource pickup | wet folds, veins, fibers, torn edges, translucency only through proven shader route | sphere blob, alpha-blend spam |
| Salvage metal pickup | bent titanium/scrap, cut edge, bolt holes, paint remnants, salt/oil grime | cube with metal material |

## Map Stack And Channel Rules

| Map role | Source requirement | Channel rule |
|---|---|---|
| Albedo | base color only, no baked lighting, no directional highlights, no cast shadow | sRGB at import by future owner |
| Normal | tangent normal from height/high-poly/source bake; no fake color normal | normal import, BC5 where supported |
| Packed mask | shader-specific PBR response | no guessed channel order |
| ToolDecayLit `_MaskMap` | tool casing/body route from 1880/1888 | R Metallic, G AO, B Smoothness, A EmissionMask |
| MraoAtlasLit `_MraoMap` | explicit MRAO route only | R Metallic, G Roughness, B AO, A EmissionMask |
| ProceduralBio `_ORMAtlas` | organic/flora route only | R Occlusion, G Roughness, B Metallic, A EmissionMask |
| Screen signal | ToolScreenDiegetic current static route | `_ToolScreenTex.rgb`; alpha unused |
| Detail/decal/label | offline decals or atlas slots; readable where gameplay-relevant | no random text/logos in generated texture |
| Emission/display | sparse, semantic, physical lens/screen/charge source | no random glowing ore or generic sci-fi glow |
| Ore inclusion mask | localized inclusion/vein mask for host-rock boundary | metallic only where real exposed metal/mineral route declares it |

Resource mineral packed maps are `BLOCKED_CHANNEL_CONTRACT_REQUIRED` until the future material owner chooses and documents the shader route.

## Prompt Pack

Prompt pack written:

`Docs/GeneratedAssets/Gemini/Prompts/Batch21/2106_productface_tool_resource_prompts_20260604.md`

Rules:

- prompts request orthographic seamless material samples, not objects;
- no lighting, no shadows, no perspective, no text, no logo, no framed product render;
- outputs are source candidates only;
- no browser/Gemini generation was run by 2106;
- no candidate may enter `Assets/**` before QA and future Unity-owner acceptance.

## Source QA Checklist

Reject source image if any are present:

- baked lighting, cast shadows, directional highlights, horizon, perspective, object render, frame, border, watermark, logo, readable random text;
- toy plastic, generic clean sci-fi, generic blue/purple glow, random glowing ore, colored currency rock;
- low-resolution mush, crayon marks, JPEG damage, repeated AI artifacts in 2x2 or manual 3x3 tile check;
- impossible material truth such as metallic rust, glowing dirt, uniformly glossy coral, full-rock metallic ore;
- black/noir grade used to hide missing material detail.

Accept only as source candidate when:

- material identity is readable at compact size;
- tileability passes static and manual review;
- albedo has no baked light;
- height/normal source supports physical relief;
- future packed-map derivation can be documented with shader-specific channel order.

## Mesh Replacement Constraints

Future generated or authored mesh packages must include:

- `MESH_<Family>_<Name>_LOD0.asset`, `LOD1`, `LOD2`;
- `COL_*` primitive/capsule/box/convex proxy children or proxy assets;
- `VIS_*` or `LOD_*` visual children separated from collision;
- pivot/orientation contract: tool grip and ray/emitter origins preserved; pickup bottom rests on placement plane; ore/scrap scale is human-readable;
- material slot order: Slot 0 primary casing/host/tissue, Slot 1 exposed wear/fracture/mineral, Slot 2 rubber/glass/secondary trim, Slot 3 emissive/display/decal when needed;
- anchor list for interactable tools and pickups;
- LOD dither/hysteresis expectation for fields/clusters;
- no LOD0 visual `MeshCollider`;
- proof renders: flat-material silhouette, final material, albedo, normal/mask/emission debug, wireframe, collider overlay, compact icon/UI read if applicable.

## Item And Tool Truth Boundary

Source art does not own:

- tool verb;
- tool capability mask;
- scan confidence truth;
- ray/beam/muzzle gameplay origin;
- item id;
- recipe;
- stack rule;
- save identity;
- economy value;
- pickup authority;
- crafting station truth.

Source art may provide:

- serialized visual mesh;
- material slots;
- anchor transforms;
- collision proxy children;
- visual scale witness;
- source manifest and proof paths.

Runtime owner must consume source artifacts through cold setup, prefab references, data registries, or explicit owner interfaces. No hot scene search, no material clone route, no runtime mesh/texture generation.

## Future Product-Face Proof Packet Template

Each future closure packet must include:

- asset family and item/tool id;
- source manifest and seed/prompt id;
- flat-material silhouette capture;
- final-material capture;
- albedo-only view;
- normal/matcap view;
- packed-mask channel view;
- emission/display debug view if used;
- wireframe view;
- collider/trigger overlay;
- LOD0/LOD1/LOD2 transition view;
- compact pickup/tool readability capture;
- icon/UI compact read if the object appears in HUD/inventory/crafting UI;
- rejection notes for any failed candidate.

## Future Unity-Owner Import/Bind Packet Template

The Unity owner must provide:

- final asset paths and GUIDs;
- mesh LOD paths;
- material paths and shader names;
- texture role paths and import settings;
- packed-map channel contract and chosen shader route;
- material slot order;
- SRP Batcher/instancing note;
- prefab path and YAML primitive scan result;
- anchor/collider child list;
- no default/package/placeholder/null material proof;
- no material clone route;
- screenshot/player capture path;
- profiler/Frame Debugger/GC/memory/VRAM proof if runtime render, shader, HLOD, instancing, material update, or UI RT route changes.

## Rejection Gates

Reject future work if any condition is true:

- final visible tool/pickup remains cube, plane, sphere, capsule, cylinder, or primitive-derived blockout;
- texture-only detail is used to hide primitive silhouette;
- ore reads as colored-rock currency;
- tool has no visible function, grip, lens/nozzle/muzzle/emitter/display, or scale witness;
- close-view hard edges are unbeveled;
- generic sci-fi glow replaces physical material truth;
- material channel order is guessed from filename;
- default, package, null, placeholder, debug, or flat-color material is used;
- runtime mesh, texture, UV, collider, or material generation is required;
- LOD0 visual mesh is used as collider;
- source art claims item/crafting/tool truth;
- proof label is upgraded beyond the evidence artifact.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` affects source fidelity only. It must not change gameplay truth, item id, recipe, save identity, collider identity, anchor names, DTO layout, material channel semantics, or authority route.

| Lane | Consequence |
|---|---|
| Low / compact | Clear silhouette, material family, grip/lens/pickup identity, shared materials, compact texture size, cheap `COL_*` proxy, no primitive final, no muddy/noir fallback. |
| Middle | Expected player lane: bevels, grip zones, material masks, ore host-rock boundary, readable labels, normal/detail maps, compact UI/icon readability. |
| High | Richer scratches, labels, grime, wetness, ore inclusions, display/emission masks, better normals, longer near LOD residency. |
| Ultra | Close-inspection hero detail, secondary cables/bolts/chips/folds, denser decals/masks, richer screen/glass only after source, mesh, import, capture, and profiler proof. |

## Dependency Scan Template

Future Unity owner must scan and report:

```text
rg -n "m_Mesh: \\{fileID: (10202|10207|10208), guid: 0000000000000000e000000000000000" Assets/_Project/Prefabs/Tools Assets/_Project/Prefabs/Items/Tools Assets/_Project/Prefabs/Resources/Pickups Assets/_Project/Prefabs/Item_Titanium.prefab
rg -n "Mat_Tool_.*Placeholder|Mat_Resource_|PackageCache/.*/Lit.mat|Default-Material|Hecton_RuntimeFlatColor|Hecton_RuntimeCheckerboardUnlit|MAT_ErrorCube" Assets/_Project/Prefabs Assets/_Project/Art/Materials
rg -n "MeshCollider" <future_prefab_paths>
```

The scan is static evidence only. Closure still needs Unity import, capture, collider, and profiler proof where applicable.

## Sibling Dependency Audit

2106 does not depend on 2101-2105 or 2107. All source constraints are based on root authorities, 2022, and targeted Batch18 reports listed above.

## Evidence Label Audit

STATIC VERIFIED:

- 2022 evidence was read.
- Batch18 relevant evidence was found and read.
- Product-face tool and pickup primitive debt is documented as static source/doc evidence.
- Source constraints, prompt requirements, QA gates, rejection gates, proof packet, import/bind handoff, and dependency scan template were written.

PENDING VERIFICATION:

- image generation;
- image download;
- source image QA;
- mesh generation;
- Unity import;
- prefab binding;
- scene/tool/pickup captures;
- interaction proof;
- profiler, GC, memory, and VRAM proof.

## CANDIDATE Unresolved Evidence

- Scanner/tool casing material source is a candidate only. No generated texture exists from 2106.
- Resource ore pickup material source is a candidate only. No generated texture exists from 2106.
- ToolScreenDiegetic premium screen wear/glass channel contract remains blocked.
- ProductFace tool mesh source authoring route from 1874 is candidate until Unity import/menu execution and generated assets are proven.
- Resource pickup mesh source authoring route from 1875 is candidate until Unity import/menu execution and generated assets are proven.
- `Item_Titanium` remains a legacy/canonicalization risk until a future owner proves quarantine or canonical relink.

## Result

What was wrong: Batch18/2022 evidence shows product-face scanner/tool and resource pickups remain primitive/material debt at static source level. Current materials and support shaders do not prove final visual quality.

What I did: wrote the Batch21 2106 source package, prompt pack, source QA gates, mesh/material/collider/LOD/anchor constraints, future proof templates, rejection gates, continuous quality scaling, dependency scan template, and evidence-label audit.

In-game result: PENDING VERIFICATION. Unity and runtime proof were forbidden.

What was verified: static authority docs, selected registry mandates, 2022 queue decision, targeted Batch18 reports, and docs-only output creation.
