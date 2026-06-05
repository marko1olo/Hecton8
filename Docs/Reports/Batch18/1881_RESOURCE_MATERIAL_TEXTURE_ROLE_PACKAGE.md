# 1881 Resource Material Texture Role Package

Agent ID: 1881
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/import/bake/PlayMode/profiler/Data Monolith execution: NOT RUN

## Scope

Report-only audit for resource pickup material and texture roles. No source code, Unity assets, prefabs, scenes, binaries, generated meshes, `.meta`, Unity menu, import, bake, profiler, build, PlayMode, or Data Monolith action was run.

Owned matrix:

- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv`

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `inventory.md`
- `world.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: resource pickup material/texture source audit.

## Static Findings

All current resource material assets under `Assets/_Project/Art/Materials/Resources` are URP Lit opaque materials with flat `_BaseColor` values and empty texture slots for `_BaseMap`, `_BumpMap`, `_MetallicGlossMap`, `_OcclusionMap`, `_EmissionMap`, and detail maps.

Current material paths:

- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat`
- `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`

Conclusion: these materials are path evidence only. They do not prove albedo, normal, packed MRAO, wetness, translucency, emission, import settings, SRP Batcher behavior, or final visual quality. Generic recolor is rejected.

No accepted project-owned resource source package exists under:

- `Assets/_Project/Art/Generated/ProductFace`
- `Assets/_Project/Art/Generated/Resources`
- `Assets/_Project/Prefabs/Resources/Sources`

## Data Truth Boundary

`CopperOre` maps to canonical `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`. Do not create or reference `Data_CopperOre.asset`.

`Item_Titanium` and `TitaniumScrap` both route to `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset` in prior static evidence. `Item_Titanium` is a legacy root route until scoped production-reference proof says otherwise. It must either inherit canonical `TitaniumScrap` visual/material truth or be quarantined. It must not keep an unresolved material GUID or separate item identity.

## Project-Owned Candidate Pools

Credible candidate pools found:

- Kelp/biological textures: `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_KelpTall_*`, `TX_KelpPatch_*`, `TX_KelpCanopy_*`, plus imported `family.kelp.*` albedo/normal/mask/detail PNGs.
- Biological/coral texture context: `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_Coral*_*` and imported `family.coral.*` maps. These are context candidates only for membrane/organic shader semantics unless a biological pickup source is authored.
- Geology meshes: `Assets/_Project/Art/Meshes/WorldProceduralGeology/RockSmallFloor`, `RockClusterMedium`, `CaveEntrance`, `LandmarkSpire`, `RockArchLarge`, `RockShelfLarge`.
- Geology/terrain texture candidates: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`, `Rock031_1K-JPG_NormalGL.jpg`; `Terrain Textures/rocks/Rocks019_1K-JPG_*`; `Terrain Textures/2rock/Rocks007_1K-JPG_*`; `Terrain Textures/gravel/Gravel020_1K-JPG_*`; `Assets/_Project/Art/TEXTURES/TX_H8SurfaceBasaltWetSediment_1428.asset`, `TX_H8TerrainBasaltSediment_1428.asset`, `TX_SurfaceBasaltWetStrata_1428.asset`.
- Data context: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset`, `SilverVein`, `SilicaShardCluster`, `Silicon7BGlassVein`, `SulfurVentClump`, `ToxicSulfurDeposit`, `FiberKelpStand`, `HydrocarbonResinPod`, `MembraneTissueBloom`, `TitaniumScrap`, and `TitaniumBasaltMass`.

Rejected as final material proof:

- Current flat `Mat_Resource_*` materials by themselves.
- Terrain textures used as a full ore material without seam/shard/nodule-specific overlays.
- Data templates as art proof.
- Collision-only meshes as visual material proof.
- Unresolved GUID carry-forward from `Item_Titanium`.
- Recoloring one generic rock for copper, silver, silica, sulfur, and titanium.

## Role Package By Resource

### CopperOre

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`.

Material roles:

- ore host rock: wet basalt or dark fractured rock from geology/terrain candidates;
- mineral seam: new copper/green oxide seam source required;
- fracture normal: chipped planes and vein relief;
- wetness: underwater sheen mask in packed alpha or approved shader mask;
- AO: cavities and seam recesses.

Status: candidate host/textures only; copper seam source is `MISSING_SOURCE_REQUIRED`.

### FiberKelp

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset`.

Material roles:

- wet frond/tissue: `TX_KelpTall_*`, `TX_KelpPatch_*`, or imported `family.kelp.*` candidates;
- fiber veins: normal/detail map role;
- translucency: dithered clip/thickness mask only after shader proof;
- wetness/AO: packed mask channels for folded bundle.

Status: candidate kelp texture and generated flora mesh language exists; harvested pickup bundle source is `MISSING_SOURCE_REQUIRED`.

### HydrocarbonResin

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_HydrocarbonResin.asset`.

Material roles:

- oily amber body;
- embedded grit;
- sagging lobe normal;
- translucency/thickness;
- wetness/AO under lobes.

Status: data context only; resin albedo/normal/MRAO source is `MISSING_SOURCE_REQUIRED`.

### MembraneTissue

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_MembraneTissue.asset`.

Material roles:

- wet biological sheet;
- veins;
- folds and thick cut edges;
- translucency/thickness;
- fold AO and wetness.

Status: biological context only; membrane pickup texture/mesh source is `MISSING_SOURCE_REQUIRED`.

### SilicaShards

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_SilicaShards.asset`.

Material roles:

- milky/glassy shard albedo;
- hard fracture normal;
- glint/translucency mask;
- AO between shards;
- wet edge response.

Status: geology context only; silica shard texture/source is `MISSING_SOURCE_REQUIRED`.

### SilverOre

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_SilverOre.asset`.

Material roles:

- dark host rock;
- narrow silver seam distinct from copper;
- fracture normal;
- wetness;
- metallic seam mask and cavity AO.

Status: candidate host/textures only; silver seam source is `MISSING_SOURCE_REQUIRED`.

### SulfurClumps

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_SulfurClumps.asset`.

Material roles:

- brittle yellow sulfur nodule/crystal;
- porous normal;
- dark vent soot/residue;
- AO between nodules;
- optional reactive mask.

Status: vent/geology context only; sulfur nodule texture/source is `MISSING_SOURCE_REQUIRED`.

### TitaniumScrap

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset`.

Material roles:

- bent titanium metal;
- exposed cut edge;
- worn paint and labels;
- salt deposits;
- oil grime;
- scratch/bend normals;
- metal/roughness/AO/wear masks.

Status: current scrap material path exists but is flat placeholder; actual scrap PBR source is `MISSING_SOURCE_REQUIRED`.

### Item_Titanium

Data owner: `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset`.

Decision:

- retain only by relinking to canonical TitaniumScrap material/mesh/source package; or
- quarantine after scoped production-reference proof.

Rejected:

- independent material route;
- unresolved GUID carry-forward;
- duplicate data truth;
- creation of new titanium data identity.

## Shader And Texture Semantics

Default shipped role contract unless a chosen shader declares otherwise:

- Albedo: sRGB material color with no baked lighting.
- Normal: linear/normal import, fracture/fiber/fold/bend detail matching the mesh.
- Packed MRAO: R = metallic, G = roughness unless shader declares smoothness, B = AO, A = wetness/translucency/glint/emission/family mask as declared per material.
- Detail: material-scale grain, fibers, scratches, chips, or pores.
- Emission: only for explicit bioluminescent or reactive material behavior; none of these pickups require default emission.
- Translucency: use dithered clip/thickness or proven shader route. Alpha-blend spam for dense pickup fields is rejected.

Virtual texturing note: do not increase material variety through extra independent `Texture2D` bindings per terrain/resource shader. For dense geology/resource fields, prefer atlases or texture arrays until SVT has platform proof.

## Continuous Quality Scaling

`GlobalQualityWeight` scales material richness only. It must not change item ids, `Data_Copper` alias truth, `Data_TitaniumScrap` truth, recipe truth, collider truth, save identity, DTO layout, or pickup authority.

- `0.0` checkpoint: no ugly mode. Each pickup keeps distinct silhouette, material family, readable contrast, shared material route, cheap proxy collider, and 512/1K array-ready map path.
- Middle: stronger normal/detail masks, residue/veins/frays/paint where physical.
- High: richer wetness, fracture response, glass/translucency response, metallic seams, longer LOD0 residency.
- Ultra: micro chips, secondary folds, scratches, small bolts, grit, bubbles, and richer masks only.

## Future Unity Proof Steps

Do not claim visual acceptance until a future Unity owner performs:

1. Source prefab/package exists under `Assets/_Project/Prefabs/Resources/Sources` or approved equivalent.
2. Mesh LOD0/LOD1/LOD2/HLOD paths are present and not Unity built-in primitives.
3. Material manifest names albedo, normal, packed MRAO, detail, optional translucency/emission roles and MRAO G-channel meaning.
4. Import report proves sRGB/linear/normal map/compression/mip/streaming settings.
5. Collider/proxy report proves `VIS_*` or `LOD_*` visual split from `COL_*` pickup bounds; no LOD0 visual `MeshCollider`.
6. Static prefab YAML scan proves no visible built-in primitive mesh references.
7. Compact pickup screenshots at interaction distance.
8. Normal-tier/player capture of individual pickups.
9. Resource field/cluster capture for dense placements.
10. Frame Debugger/profiler/GC only when shader/render/HLOD/instancing/runtime route changes or acceptance is claimed.

## Verification

Claim: required authority files and mandated skill files were read.
Evidence Class: STATIC_DOC.
Artifact: files listed in `Authorities Read`.
Command or tool: `Get-Content -Raw`.
Date: 2026-06-04.
Residual risk: docs are static authority, not runtime proof.

Claim: current `Mat_Resource_*` materials do not contain assigned texture maps.
Evidence Class: STATIC_SOURCE.
Artifact: eight `.mat` files under `Assets/_Project/Art/Materials/Resources`.
Command or tool: `Get-Content -Raw Assets\_Project\Art\Materials\Resources\Mat_Resource_*.mat`.
Date: 2026-06-04.
Residual risk: Unity import/render state not inspected.

Claim: project-owned candidate texture/mesh/data paths exist.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Art` and `Assets/_Project/Data` file listings.
Command or tool: `rg --files`, `Get-ChildItem`, `rg -n`.
Date: 2026-06-04.
Residual risk: existence does not prove visual quality, import settings, or shader compatibility.

Claim: `CopperOre` uses canonical `Data_Copper.asset`, and `Item_Titanium` must not invent separate truth.
Evidence Class: STATIC_SOURCE / STATIC_DOC.
Artifact: `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`, `Data_TitaniumScrap.asset`, and prior Batch18 reports.
Command or tool: `rg --files Assets/_Project/Data`, `rg -n`.
Date: 2026-06-04.
Residual risk: runtime registry/Data Monolith bake not run.

Final file verification:

- `git diff --check -- Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv Docs/Tasks/Status_1881.md Docs/AgentLogs/Rationale_1881.md Docs/AgentLogs/LOG_1881.md` -> PASS, no output.
- `Import-Csv Docs\Reports\Batch18\1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv` -> PASS, 9 rows parsed.
- Static cross-check against report and CSV for `CopperOre`, `FiberKelp`, `HydrocarbonResin`, `MembraneTissue`, `SilicaShards`, `SilverOre`, `SulfurClumps`, `TitaniumScrap`, `Item_Titanium`, and `Data_Copper` -> PASS, all terms present in both files.

## Result

What was wrong: existing resource pickup materials are flat color placeholders with empty texture slots, and no accepted material/texture source package exists for distinct ore, biological, resin, shard, sulfur, or scrap material identity.

What I did: produced the static material/texture role package and CSV matrix. It maps credible project-owned candidate sources, marks missing source requirements, preserves `Data_Copper` truth, rejects unresolved GUID carry-forward, and defines shader semantics plus continuous quality scaling.

In-game result: PENDING VERIFICATION. Unity and runtime proof were forbidden by task.

What was verified: static docs, data paths, material YAML, and art/data file listings only.
