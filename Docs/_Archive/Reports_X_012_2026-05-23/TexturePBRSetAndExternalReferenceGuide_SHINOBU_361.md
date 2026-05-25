# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 PBR Set And External Reference Guide

Status: ACTIVE / OPERATOR READY / PENDING ART QA
Agent: SHINOBU_361
Domain: Echelon 8 Presentation / Tech Art / Static PBR Texture Audit
Scope: 175 unique texture targets from `TextureProductionQueue_SHINOBU_361_HANDMADE.md`

This guide answers the missing operator question: what a PBR set is, how to build it from image-generator outputs, and how to use internet pre-references without breaking HECTON-8 style ownership.

## Direct Answer

A PBR set is not one pretty picture. It is the texture bundle a material needs so Unity can light it correctly.

For this SHINOBU texture queue, one finished material source normally means:

1. `*_Albedo.png` - color only.
2. `*_Normal.png` - fake surface relief.
3. `*_ORM.png` - packed material masks.

The albedo carries identity. The normal carries fake geometry. The ORM carries how light reacts to the surface. If the albedo is beautiful but the normal/ORM are wrong, the material will look fake in Unity.

## Critical Channel Warning

There are two packing contracts in play.

SHINOBU authoring ORM contract:

- Red = Ambient Occlusion.
- Green = Roughness.
- Blue = Metallic.
- sRGB = off.

Unity URP Lit official mask-style contract:

- Red = Metallic.
- Green = Occlusion.
- Blue = unused.
- Alpha = Smoothness.
- Smoothness = `1.0 - Roughness`.

Therefore:

- Do not assign a SHINOBU `R=AO/G=Roughness/B=Metallic` ORM directly into a standard URP Lit Metallic Map unless a shader/importer converts the channels.
- If the target material uses custom HECTON shaders that decode SHINOBU ORM, keep SHINOBU ORM.
- If the target material uses raw URP Lit, repack before assignment: `R=Metallic`, `G=AO`, `A=1-Roughness`.
- Mark any uncertain material as `PENDING_SHADER_ROUTE_CHECK`, not done.

Sources reviewed:

- Unity normal map import: https://docs.unity.cn/6000.0/Documentation/Manual/StandardShaderMaterialParameterNormalMapImport.html
- Unity texture import reference: https://docs.unity.cn/6000.0/Documentation/Manual/textures-reference.html
- Unity URP Lit channel packing: https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4014.0/manual/lit-shader.html
- Unity URP Complex Lit channel packing: https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.0/manual/shader-complex-lit.html

## File Naming

Use the target name from the prompt card when it already names the role:

- Target says `_Albedo.png`: final file is the albedo.
- Target says `_Normal.png`: final file is the normal or the normal-ready height source, depending on the card.
- Target says `_ORM.png`: final file is the SHINOBU packed ORM unless a material route card says URP repack.

For complete generated families, keep sibling names stable:

- `floor_05_stripes_basecolor_Albedo.png`
- `floor_05_stripes_basecolor_Normal.png`
- `floor_05_stripes_basecolor_ORM.png`

Do not invent casual suffixes like `final`, `better`, `new`, or `v7`. Candidate names can carry letters. Approved target names must be stable.

## Albedo Build

Role: color and material identity only.

Generate or edit albedo as a flat material plate:

- no baked shadow
- no baked highlight
- no rim light
- no scene lighting
- no perspective object
- no readable text
- no logo
- no watermark
- seamless tile

Acceptable albedo detail:

- off-white ceramic color variation
- brushed titanium grain as color variation only
- graphite rubber matte fields
- teal/amber paint zones
- salt residue in seams
- polished wear marks
- opal mineral color
- restrained biological membrane color

Rejected albedo detail:

- black crushed grime
- white painted specular highlights
- dark ambient occlusion painted across wide panels
- big rust stains as the default look
- horror blood/slime tone
- fake labels or pseudo-letters

## Normal Build

Role: shallow fake geometry. This is the Dear Lie: it sells ribs, grooves, pores, scratches, gasket lips, and rock relief without adding mesh cost.

Preferred workflow:

1. From the approved albedo candidate, decide what must become actual relief.
2. Generate or paint a grayscale height source:
   - black = lower groove/cavity
   - mid gray = base surface
   - white = raised lip/rib/edge
3. Convert height to tangent-space normal.
4. Import as Normal Map, sRGB off, Standalone BC5.

Generator prompt for dedicated height source:

Create a seamless grayscale height map for the approved HECTON-8 material. Black only in recessed seams, gasket channels, fastener wells, and deep pores; mid gray for broad base panels; white only on raised ribs, bevel lips, anti-slip ridges, mineral veins, or biological raised membranes. Preserve the same large shape rhythm as the approved albedo. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text, no labels, no logo, no watermark.

Generator prompt for direct normal map:

Create a seamless tangent-space normal map for the approved HECTON-8 material, neutral blue-purple base, raised ribs and bevel lips encoded as clean normal relief, recessed seams and gasket channels encoded as shallow grooves, no albedo color, no lighting, no shadow, no text, no logo, no border, perfect seamless tiling.

Normal acceptance:

- neutral flat areas read close to tangent normal blue
- no random red/green inversion patches
- no deep canyon relief for thin paint
- no lighting baked into the normal
- no noisy scratch field over every pixel
- seams and ribs line up with the albedo
- relief still reads after mip compression

Per-family normal intent:

- Habitat panels: shallow bevels, gasket lips, screw wells, anti-slip ridges.
- Floors: broad seams, drainage channels, anti-slip grain, stripe paint thickness.
- Titanium: fine brushed grain plus edge bevels, not dented junk.
- Rubber: soft matte grain, shallow compression grooves.
- Glass/polymer: micro-scratches and stress arcs only; no deep cracks unless the material is explicitly damaged.
- Flora: membrane pores, vein ridges, wet tissue folds.
- Geology: broad sediment/rock relief first, fine mineral noise second.

## ORM Build

Role: material response packed into one texture so we do not waste three samplers.

SHINOBU ORM channel meaning:

- Red AO: white means open/no occlusion; dark means crevice/cavity.
- Green Roughness: black means mirror smooth; white means matte rough.
- Blue Metallic: black means dielectric; white means metal.

Build order:

1. Start from approved albedo and height source.
2. Paint AO from geometry logic, not from scene lighting.
3. Paint roughness by material type.
4. Paint metallic only on actual metal.
5. Pack channels into one RGB image.
6. Save as PNG, sRGB off on import.

AO values:

- open panels: 0.90-1.00
- shallow seams: 0.65-0.85
- deep gasket grooves: 0.35-0.65
- fastener wells: 0.40-0.70
- flora pores/folds: 0.45-0.85
- rock cracks: 0.35-0.75

Roughness values:

- clean glass/polymer: 0.05-0.22
- scratched glass/polymer: 0.18-0.35
- satin titanium: 0.28-0.55
- polished walking wear: 0.35-0.50
- ceramic/composite panels: 0.45-0.70
- painted amber/teal accents: 0.50-0.72
- graphite rubber: 0.68-0.90
- salt/mineral dust: 0.75-0.95
- wet flora membrane: 0.18-0.45
- dry silt/basalt: 0.65-0.92
- opal mineral flecks: 0.18-0.45

Metallic values:

- ceramic/composite: 0.00
- rubber: 0.00
- glass/polymer: 0.00
- paint/enamel: 0.00
- flora: 0.00
- sediment/basalt: 0.00
- opal/mineral flecks: 0.00-0.15
- exposed satin titanium rails/ribs/screws: 0.80-1.00
- worn metal edge peeking through paint: 0.35-0.70

Do not make everything metallic because it looks expensive. That is physically wrong and produces oily garbage in Unity.

## URP Lit Repack

If a material route uses standard URP Lit, create a converted mask from SHINOBU ORM:

- URP Red = SHINOBU Blue Metallic.
- URP Green = SHINOBU Red AO.
- URP Blue = 0 or white if a custom detail route requires it.
- URP Alpha = `1.0 - SHINOBU Green Roughness`.

Example:

- SHINOBU roughness 0.70 becomes URP smoothness 0.30.
- SHINOBU roughness 0.15 becomes URP smoothness 0.85.

This conversion is required because Unity URP Lit samples smoothness from the metallic map alpha by default. If alpha is missing, smoothness behavior becomes slider-dependent and not faithful to the authored roughness map.

## Emissive Mask Rule

Emission is not albedo.

Use emission masks only for:

- flora veins
- diegetic terminal light pipes
- active power strips
- gameplay signal surfaces
- emergency/support locator accents

Do not paint bright glow into albedo. Albedo stays plausible under neutral light; glow belongs in emission/material response.

## Full Operator Procedure

For each target card:

1. Read the card in `TextureProductionQueue_SHINOBU_361_HANDMADE.md` or the Batch 01 golden override.
2. Attach at most three refs: project/global ref, category ref, same-family approved ref.
3. Generate three candidates.
4. Reject text, logos, scene lighting, non-seamless output, black grime, and high-frequency noise.
5. Pick one candidate as approved albedo or approved height/normal source.
6. If target is albedo, build matching normal and ORM only where the material route needs them.
7. If target is normal, build the normal directly from height logic; do not waste time on a pretty diffuse image.
8. If target is ORM, build packed masks manually from material logic; do not ask the generator for a random pretty RGB texture.
9. Check channel order against material route: SHINOBU ORM or URP repack.
10. Save approved files under stable target names.
11. Run `Tools/BatchImportTextures.py` dry-run.
12. Import in Unity and inspect material previews and route surfaces before claiming done.

## External Pre-Reference Policy

Internet images are allowed only as do-references for taste and construction grammar. They are not source textures, not paint-over targets, and not HECTON style authority.

Reference priority:

1. Project-owned refs: current planet/cloud/flora/geology textures and approved generated outputs.
2. Real subsea engineering refs: modern underwater habitats, NEEMO/Aquarius, pressure vessels, lab interiors.
3. Subnautica and related concept art: use for broad underwater readability and optimistic alien-ocean tone only.
4. Pinterest boards: use for fast browsing keywords and direction checks only; never as primary style truth.

External sources reviewed:

- DEEP Sentinel subsea habitat: https://www.deep.com/sentinel
- DEEP habitats overview: https://www.deep.com/habitats
- DEEP Vanguard habitat: https://www.deep.com/vanguard
- Aquarius Underwater Laboratory / CSA: https://www.asc-csa.gc.ca/eng/missions/neemo/aquarius.asp
- NEEMO 22 Aquarius NASA image / Wikimedia: https://commons.wikimedia.org/wiki/File:NEEMO_22_Aquarius_underwater_habitat.jpg
- Subnautica concept art index: https://subnautica.fandom.com/wiki/Concept_Art
- FOX3D Subnautica concept art page: https://fox3d.artstation.com/projects/VWO4b
- Subnautica interior sketch: https://www.indiedb.com/games/subnautica/images/subnautica-concept-art-interior-sketch
- Unknown Worlds exterior sketch: https://unknownworlds.com/en/news/subnautica-exterior-sketch
- Pinterest underwater base concept board: https://www.pinterest.com/ideas/underwater-base-concept-art/934071435313/

## What To Take From External Refs

DEEP Sentinel:

- clean, funded, habitable underwater design
- circular viewports
- curved pressure interiors
- white/neutral interior surfaces
- modular subsea system logic

Aquarius / NEEMO:

- real pressure-cylinder proportions
- compact research utility
- steel habitat truth
- visible life-support and lab equipment
- no fantasy luxury without function

Subnautica:

- optimistic alien ocean color
- readable underwater technology
- bright enough biology
- modular base language
- not pure horror and not pure military

Pinterest:

- search vocabulary and quick visual sorting only
- good for "industrial underwater base", "submarine corridor", "deep sea lab", "oceanpunk habitat"
- reject anything that pushes gothic horror, cyberpunk city glow, generic spaceship corridor, or dirty military bunker

## HECTON Translation

The requested direction is not "Subnautica clone". It is:

Subnautica readability and wonder, shifted darker by environment, pressure, industry, and noir lighting, while keeping texture albedo clean enough to read.

Do not darken by painting black mud into every texture. Darken through:

- level fog
- controlled lighting
- cooler shadows
- wet roughness response
- teal/amber emissive restraint
- industrial ribs, gaskets, and pressure hardware

Keep these style ratios:

- 45 percent premium research habitat
- 25 percent real subsea industrial hardware
- 15 percent alien ocean biology
- 10 percent NASA/spaceflight material discipline
- 5 percent noir mood through color restraint and wear

## Internet Search Queries

Use these as manual search strings:

- `Subnautica concept art underwater base interior`
- `Subnautica base interior sketch concept art`
- `modern subsea habitat interior circular viewport`
- `DEEP Sentinel subsea habitat interior`
- `Aquarius underwater laboratory interior`
- `NEEMO Aquarius underwater habitat NASA`
- `underwater research base concept art industrial`
- `sci fi submarine corridor industrial concept art`
- `oceanpunk underwater facility concept art`
- `deep sea lab interior industrial design`

## Per-Batch Reference Mix

Batch 0 seeds:

- one project planet/cloud mood image
- one DEEP/Aquarius real habitat reference for construction truth
- no Pinterest unless the seed is failing taste

Batch 1 blockers:

- approved seed image
- real habitat or Subnautica construction reference for broad mood
- no more than one internet image

Flora:

- project flora atlas first
- Subnautica biology concept only for wonder/readability
- no horror gore refs

Geology:

- project planet surface first
- Subnautica biome concepts only for color hierarchy
- no cliff photographs as direct texture source

Tools/support:

- approved habitat anchor
- real tool/pressure hardware mood if needed
- no weapon/military reference unless explicitly required by target

## Final Acceptance

A target is finished only when:

1. Albedo/normal/ORM or route-specific subset exists.
2. The maps use the correct channel contract.
3. Import settings match role: albedo sRGB on, normal sRGB off, masks sRGB off.
4. The material does not break under Unity lighting.
5. The surface remains readable on MX350 mip pressure.
6. The same source can scale upward with detail masks/emission on High/Ultra without changing identity.

Evidence remains STATIC_DOC until actual generated files, Unity import, material preview, route surface capture, Memory Profiler, and Frame Debugger artifacts exist.
