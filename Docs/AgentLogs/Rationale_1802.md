# Agent 1802 Rationale

## Authority Basis

Task is explicit Agent ID 1802. Status, rationale, and log artifacts are required.

Read authority documents:
AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, TASTE.md, quality.md, 3dmodel.md, PROCEDURAL_ASSET_PIPELINE.md, 3DMODEL_TEXTURES_MATERIALS.md, 3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md, 3DMODEL_HERO_REALISM_OVERKILL.md, 3DMODEL_FLORA_CORAL.md, 3DMODEL_GEOLOGY_ROCKS.md, water.md, terrain.md, celestial.md, lighting.md, rendering.md.

Read relevant mandates:
OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt, OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt, STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt, STRM_Async_Asset_Upload_Texture_Settings.txt, REND_URP_Graphics_HotPath_Optimization_HLOD.txt, REND_Terrain_VirtualTexturing.txt, REND_Shader_Noir_Aesthetics_Dithering_Fog.txt, REND_Instanced_Flora_Physics.txt.

Docs/Actual Domains of Project.txt was absent. Narrow domain inferred: surface/shallow water, terrain/geology, celestial/lighting/rendering, generated asset pipeline, and static proof hygiene.

## Exact Visual Floor

Surface, sky, Aegir, moons, clouds, coastline, ocean surface, and photic shallows are not the dark/noir zone.

Surface, photic shallows, and medium-depth hero routes must be Subnautica-level or better. This is the floor, not the target ceiling.

0-100 m open water is mostly bright, beautiful, colorful, and readable. Deep caves may be dim inside shallow bands. 200-400 m becomes subdued/twilight. 400-500 m and below can become true darkness, but route structure, silhouettes, evidence, and instruments must survive.

Darkness, fog, post-processing, or storm/noir language must not hide primitive terrain, weak textures, empty water, muddy sky, poor Aegir/moon art, or unfinished surface assets.

GlobalQualityWeight 0.0 is not ugly mode. Compact preserves ocean color, surface readability, material identity, silhouettes, route cues, and instrument legibility. Higher tiers buy richer reflections, cloud depth, shafts, wetness, foam, caustics, density, and visual overkill without changing gameplay truth.

Static path existence is not production proof. Assets found by file scan are CANDIDATE or REJECTED_PLACEHOLDER unless backed by render/import/validation proof.

## Decisions

- Use static inventory only unless Unity becomes explicitly free and needed. Current task can be completed as static proof; Unity visual quality remains pending.
- Third-party Crest/MapMagic assets are inventory/configuration inputs only. Do not mutate third-party code, materials, or packages.
- Existing placeholder families under WorldRuntime/ProceduralPlaceholders are production rejection candidates, not final visual references.
- The reported missing render material on `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/Hecton8_Surface.prefab` is not accepted as a hard fact. Static YAML shows two renderer material refs to `Mat_HectonSurface.mat`. The null `m_Material` found in that prefab is a collider physics-material slot, not render material proof. Final state remains CANDIDATE/PENDING UNITY SLOT.
- The task-supplied `Assets/_Project/Art/TerrainLayers/H8_TerrainLayer_AbyssBasalt_1428.terrainlayer` path is wrong. Actual verified path is `Assets/_Project/Data/H8_TerrainLayer_AbyssBasalt_1428.terrainlayer`.
- Baseline screenshots were viewed locally. They prove readable horizon/UI, not premium quality. Static observation: coast reads grey/procedural, waterline is sparse, and Aegir needs better atmospheric integration.
- First-party ocean upgrade route should start from `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat` plus `Assets/_Project/Prefabs/Ocean_Crest.prefab`, but only through Unity slot configuration or first-party bridge. Crest package assets remain use-as-is.
- Wet basalt materials are useful candidates but not complete hero proof because static scan shows detail texture slots empty and terrain layer normal/mask slots null.
- Baked flora prefabs under `Assets/_Project/Prefabs/Nature/Flora/Baked/` are the preferred static candidates over runtime placeholders because they include LOD structure and material refs.
- Industrial trace assets need offline replacement/dressing before hero use. Representative `PFB_Debris_WreckField.prefab` uses built-in primitive meshes, so it is layout evidence, not final visual evidence.
