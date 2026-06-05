# 1856 Organic Misc Final Mesh Rebuild Packet

Evidence class: STATIC_SOURCE
Task: 1856
Date: 2026-06-04

## Boundary

This packet is a no-mutation production rebuild packet. It used text/static source inspection only. It does not contain Unity Editor proof, import proof, render proof, screenshot proof, profiler proof, validator runtime proof, or gameplay proof.

No prefabs, family assets, source files, scenes, binaries, or `.meta` files were edited. `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals` remains blocked and must not be unblocked as part of this task.

## Authorities Read

Project authorities:
- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `creatures.md`
- `world.md`
- `terrain.md`
- `vfx.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`

Relevant mandates:
- `QA_Evidence_Text_Filter_Audit.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_Instanced_Flora_Physics.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `PHYS_Destructible_Organic_Entropy.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

Batch evidence read:
- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1852_PROCEDURAL_PLACEHOLDER_FINAL_GATE.md`
- `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`

Amended source files included:
- `Assets/_Project/Scripts/Editor/WorldProceduralCoralMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraBakedStarterGenerator.cs`

## Current Blockers

| Family | Final variant | Current prefab | Prefab GUID | Static defect |
|---|---|---|---|---|
| `family.egg.cluster` | `family.egg.cluster.final.nest_cluster` | `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_EggCluster.prefab` | `008ee5331a7a1cd4e8a882a029168c14` | 11 visible built-in Unity primitive mesh refs |
| `family.plant.giant` | `family.plant.giant.final.silhouette` | `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_PlantGiant.prefab` | `dd7b28339c769af499c834e77907a132` | 12 visible built-in Unity primitive mesh refs |

The family assets mark both final variants `proxyOnly: 0` and `finalReady: 1`. The current state is therefore not a harmless proxy issue. The finals are declared ready while the visible art is still primitive-composite.

## Current Visible Roles

### Egg Cluster

Current prefab root:
- `PFB_Organic_EggCluster`
- Root `BoxCollider` size `{x: 3, y: 1.6, z: 3}`, center `{x: 0, y: 0.8, z: 0}`
- `LODGroup` size `2.2773068`

Visible primitive roles:
- LOD0: `NestBase` cylinder, `EggA` sphere, `EggB` sphere, `EggC` sphere, `EggD` sphere, `NestRidgeA` cylinder, `NestRidgeB` cylinder
- LOD1: `NestBase` cylinder, `EggMassA` sphere, `EggMassB` sphere
- LOD2: `EggSilhouette` sphere

Static primitive count: 11.

### Giant Plant

Current prefab root:
- `PFB_Organic_PlantGiant`
- Root `BoxCollider` size `{x: 8.4, y: 15, z: 8.4}`, center `{x: 0, y: 7.5, z: 0}`
- `LODGroup` size `24.8`

Visible primitive roles:
- LOD0: `StemCore` cylinder, `StemBulb` sphere, `CanopyA` capsule, `CanopyB` capsule, `CanopyC` capsule, `CanopyD` capsule, `BudA` sphere, `BudB` sphere
- LOD1: `Stem` cylinder, `CanopyMass` capsule, `Bud` sphere
- LOD2: `PlantSilhouette` capsule

Static primitive count: 12.

## Source Pattern To Avoid

`Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs` is a legacy primitive-composite authoring route:
- It is guarded by `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring`.
- `BuildEggClusterLods` declares visible `PrimitiveType.Cylinder` and `PrimitiveType.Sphere` specs.
- `BuildPlantGiantLods` declares visible `PrimitiveType.Cylinder`, `PrimitiveType.Sphere`, and `PrimitiveType.Capsule` specs.
- `CreateVisual` uses `GameObject.CreatePrimitive(primitiveType)`.

Do not relax the gate. Do not route production through this menu. It is useful only as a defect map for dimensions, child naming, current roles, and family scale envelopes.

## Material Inventory

Current OrganicMisc materials under `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc` are flat URP Lit color materials with no proven texture slots populated:
- `Mat_Organic_EggShell`: base color `{0.9, 0.84, 0.7, 1}`, smoothness `0.18`
- `Mat_Organic_EggNest`: base color `{0.36, 0.3, 0.24, 1}`, smoothness `0.10`
- `Mat_Organic_PlantStem`: base color `{0.12, 0.42, 0.28, 1}`, smoothness `0.18`
- `Mat_Organic_PlantCanopy`: base color `{0.22, 0.66, 0.42, 1}`, smoothness `0.26`
- `Mat_Organic_PlantBud`: base color `{0.44, 0.8, 0.6, 1}`, smoothness `0.32`

Candidate source materials/textures:
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_AlbedoAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_NormalAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_ORMAtlas.png`
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/TX_ProceduralBio_Shallows_MatCap.png`
- Imported coral and kelp albedo/detail/normal/mask folders under `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported`
- `Mat_Resource_Membrane.mat` as a membrane color/semantic reference only, not proof
- Wet basalt/sediment and terrain wetness candidates as substrate reference only, not proof

Production OrganicMisc materials must not stop at flat base colors. Required maps per visible material set:
- Albedo with macro color breakup and micro organism detail
- Normal map for shell pits, membrane ridges, vascular frond veins, root bark, and wet substrate grain
- MRAO/ORM mask for wetness, roughness, cavity AO, and material separation
- Emission or packed biolum mask for restrained alien accent organs
- Vertex color semantics consumed by shader or material logic

## Candidate Source Classification

| Source | Static inventory | Classification | Use limit |
|---|---:|---|---|
| `Assets/_Project/Prefabs/Nature/Flora/Baked` | 89 baked flora prefabs; static built-in primitive refs: 0 | reusable source/inspiration | Not a drop-in OrganicMisc replacement until exact prefab compatibility, manifests, render proof, validators, and family integration proof exist |
| BioForge Kelp | 300 mesh assets | source/inspiration | Useful for giant plant stalk/frond/canopy language; not an OrganicMisc proof artifact |
| BioForge PorousRock | 150 mesh assets | carrier/source | Useful for egg substrate pad and plant holdfast base; not a final drop-in |
| BioForge TubeCoral | 150 mesh assets | source/inspiration | Useful for membrane supports, pores, cavities, and coral-adjacent organic breakup; not a final drop-in |
| `WorldProceduralSeaweedMeshBuilder.cs` | Procedural holdfast, stipe, blades, bulbs, roots, LOD, vertex colors, writable mesh data | source pattern | Relevant to giant plant rebuild; requires OrganicMisc-specific output, manifest, proof, and validator route |
| `WorldProceduralCoralMeshBuilder.cs` | Procedural warped blobs, plates, tubes, branches, knuckles, tips, LOD, vertex colors, writable mesh data | source pattern | Relevant to substrate, cavities, egg nest pad, roots, and membrane scaffold; not direct egg/plant proof |
| `WorldProceduralFloraBakedStarterGenerator.cs` | Three visible LOD starter-prefab pipeline using seaweed/coral builders | route pattern | Useful for future OrganicMisc generator structure; starter assets are not final art proof |

## Egg Cluster Production Mesh

Required visual read:
- Believable alien clutch/nest, not a pile of scaled Unity spheres.
- Viable in shallow and medium-depth lighting without darkness, turbidity, fog, or grading hiding form weakness.
- Close inspection should show shell variation, membrane attachment, substrate contact, cavities, wetness, AO, and biological asymmetry.

LOD0 required mesh features:
- 8 to 14 varied asymmetric ovoid eggs generated from sculpted or procedural organic surfaces, not stock sphere meshes.
- Per-egg deformation: off-axis bulge, uneven poles, subtle pinch, local dents, seam/cap ridges, growth scars, and shell pores.
- Egg sizes and rotations must avoid even spacing and repeated silhouettes.
- Membrane webbing between eggs and substrate: elastic sheets, cords, torn thin edges, thickened contact seams.
- Substrate pad: porous rock/coral/root saddle under the clutch, with cavities and embedded organic residue.
- Attachment points: named hidden anchors for scan/spawn/story metadata; visible mesh may include tendril sockets, slime rims, and root filaments.
- Cavity details: recesses and occluded underside around eggs; AO must be visible in material/vertex color proof.

LOD1:
- Consolidated egg forms retain asymmetric clutch silhouette.
- Membrane webbing becomes fewer thicker sheets/cords.
- Substrate pad remains non-flat and non-circular.
- Triangle count target: about 35 to 50 percent of LOD0 after proof.

LOD2/HLOD:
- Single or few non-primitive organic silhouette meshes with baked normal/ao/emissive data.
- No sphere/cylinder/capsule built-in mesh refs.
- Must preserve clutch mass, substrate pad, and membrane silhouette at gameplay distance.

Egg vertex color semantics:
- R: motion/sway weight. Eggs mostly 0; membranes and loose tendrils can use low values.
- G: biolum/emission mask and phase seed in veins, cracks, tips, or embryo glow accents.
- B: AO/cavity/occlusion density for underside, between eggs, substrate pits, and torn membrane roots.
- A: wetness, shell thickness, damage, or scan-story mask as chosen by final shader contract.

Egg materials:
- `MAT_OrganicEggCluster_Shell`: opaque or dithered subsurface approximation, shell translucency cue, pores, ridges, scars, wet specular breakup.
- `MAT_OrganicEggCluster_Membrane`: translucent-looking membrane through shader approximation or controlled alpha/dither; vein normals and wet highlights.
- `MAT_OrganicEggCluster_Substrate`: porous organic rock/coral/root pad, wet sediment contact, AO-heavy cavities.
- `MAT_OrganicEggCluster_Biolum`: optional low-area accent only; must not turn the nest into a glow prop.

Gameplay separation:
- Visible clutch mesh is art carrier only.
- Spawn, scanner, story, nest occupancy, and harvest/read interactions must live in hidden metadata components or named anchors.
- Suggested hidden anchors: `ANCHOR_Scan`, `ANCHOR_Spawn`, `ANCHOR_StoryLookAt`, `ANCHOR_EggCenter`, `ANCHOR_FaunaInterest`.
- Do not use visible primitive meshes as gameplay markers.
- Invisible simple triggers/colliders are allowed as `COL_*` children.

## Giant Plant Production Mesh

Required visual read:
- Huge alien underwater plant silhouette, not a cylinder trunk with capsule leaves.
- Must read as biome-scale ecology in shallow and medium-depth routes.
- Needs strong silhouette from 20 to 60 meters and convincing wet/organic detail from close inspection.

LOD0 required mesh features:
- Holdfast/root mass gripping substrate with radial roots, knuckles, cavities, and secondary tendrils.
- Tapering trunk/stalk with non-uniform radius, ribs, twisting profile, scar bands, and branching node geometry.
- Canopy/fronds must be organic lamina or folded ribbons with serrated or lobed edges, not capsule forms.
- Canopy distribution should be asymmetric and current-shaped: overlapping planes are acceptable only when thickened, curved, and non-card-flat.
- Bud/organs should be grown into nodes or pods with membrane collars, not isolated spheres.
- Use seaweed builder concepts for holdfast/stipe/blade logic, but author OrganicMisc-specific scale, silhouette, and proof.

LOD1:
- Preserve stalk twist, root mass, and dominant canopy arcs.
- Merge small buds and tertiary fronds into baked silhouette clusters.
- Triangle count target: about 35 to 50 percent of LOD0 after proof.

LOD2/HLOD:
- Non-primitive silhouette mesh or impostor shell with baked normals/AO/emissive masks.
- Root mass and crown must remain identifiable.
- For distant HLOD, use baked cards or shell meshes only when silhouette and parallax survive gameplay camera checks.

Giant plant vertex color semantics:
- R: sway weight, 0 at root/holdfast, medium along trunk, high on frond tips.
- G: biolum mask/phase, restrained to buds, edge veins, node organs, or deep rib channels.
- B: AO/cavity density in root cracks, trunk grooves, canopy overlaps, and bud collars.
- A: wetness, age/damage, stiffness, or current response mask per shader contract.

Giant plant materials:
- `MAT_OrganicPlantGiant_Stalk`: ribbed tissue, wet roughness variation, vertical scars, low emission.
- `MAT_OrganicPlantGiant_Frond`: vascular lamina, current-driven wet highlight, packed sway and vein masks.
- `MAT_OrganicPlantGiant_Holdfast`: porous wet root/substrate blend, cavity AO, sediment contact.
- `MAT_OrganicPlantGiant_Bud`: biolum organ with low-area emission, shell/membrane rim, age/damage variation.

Gameplay separation:
- Visible plant art is not physics truth.
- Harvest, scan, fauna interest, cover, or traversal markers must be hidden anchors or metadata components.
- Suggested hidden anchors: `ANCHOR_Scan`, `ANCHOR_FaunaInterest`, `ANCHOR_CanopyCenter`, `ANCHOR_RootBase`, `ANCHOR_Harvest`.
- Runtime sway must read vertex color/packed masks and quality-scaled cadence. It must not mutate gameplay truth ownership.

## Collider Policy

Allowed:
- Invisible primitive colliders as root or `COL_*` children.
- Coarse `BoxCollider`, `CapsuleCollider`, or `SphereCollider` proxies if no visible mesh renderer is on the collider object.
- Trigger volumes for scanner, fauna interest, spawn, or harvest.
- Optional custom convex collider only with import/profiler proof and no LOD0 `MeshCollider` default.

Rejected:
- Visible primitive mesh children.
- `MeshCollider` on dense LOD0 art without explicit proof.
- Collider geometry doubling as art.
- Hiding bad visible forms with fog, darkness, turbidity, or depth grading.

## Generator And Authoring Route

Future owner may implement one of two routes after mutation is authorized:

1. DCC/baked route:
   - Author meshes externally or through approved offline tools.
   - Import under OrganicMisc-specific output paths.
   - Build prefabs with three visible LODs, materials, metadata anchors, and invisible collider proxies.

2. Procedural editor route:
   - Add a new OrganicMisc-specific mesh builder, for example `WorldProceduralOrganicMiscMeshBuilder`, without unblocking the legacy primitive authoring menu.
   - Reuse seaweed/coral builder algorithms only as internal source patterns.
   - Generate deterministic mesh assets and prefabs with manifests and visual proof.

Suggested output paths:
- Meshes: `Assets/_Project/Art/Generated/OrganicMisc/EggCluster/`
- Meshes: `Assets/_Project/Art/Generated/OrganicMisc/PlantGiant/`
- Final prefabs: `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_EggCluster.prefab`
- Final prefabs: `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_PlantGiant.prefab`
- Materials: `Assets/_Project/Art/Materials/Nature/OrganicMisc/`
- Textures: `Assets/_Project/Art/TEXTURES/OrganicMisc/`
- Proof manifests: `Docs/Reports/GeneratedAssets/OrganicMisc/`
- Batch proof summary: `Docs/Reports/Batch18/`

Suggested names:
- `MESH_OrganicEggCluster_NestCluster_LOD0.asset`
- `MESH_OrganicEggCluster_NestCluster_LOD1.asset`
- `MESH_OrganicEggCluster_NestCluster_LOD2.asset`
- `MAT_OrganicEggCluster_Shell.mat`
- `MAT_OrganicEggCluster_Membrane.mat`
- `MAT_OrganicEggCluster_Substrate.mat`
- `MESH_OrganicPlantGiant_Silhouette_LOD0.asset`
- `MESH_OrganicPlantGiant_Silhouette_LOD1.asset`
- `MESH_OrganicPlantGiant_Silhouette_LOD2.asset`
- `MAT_OrganicPlantGiant_Stalk.mat`
- `MAT_OrganicPlantGiant_Frond.mat`
- `MAT_OrganicPlantGiant_Holdfast.mat`
- `MAT_OrganicPlantGiant_Bud.mat`

Required manifest fields:
- Family ID and variant ID
- Seed and source generator/DCC version
- Source meshes/textures/materials
- `GlobalQualityWeight` assumptions and low/middle/high/ultra consequences
- Triangle counts per LOD
- Submesh/material slot counts
- Bounds and pivot
- LOD thresholds
- Collider policy and collider bounds
- Vertex color channel contract
- UV sets and UV density/stretch checks
- Texture import settings and texture role map
- Static primitive mesh scan result
- Screenshot/render proof links
- Validator command and output after mutation is permitted
- If procedural generation runs jobs or can fault, a fixed-size last-300-frame or last-300-step diagnostic buffer/dump plan

## Family Integration Gate

Do not change `family.egg.cluster.final.nest_cluster` or `family.plant.giant.final.silhouette` to point at new assets until all of the following exist:
- Static scan proves zero visible built-in primitive Unity mesh refs in the final prefab.
- Prefab has LOD0/LOD1/LOD2 or approved HLOD/impostor policy.
- Materials are assigned and texture role manifests exist.
- Vertex color semantics are documented and visually proven.
- Collider proxies are invisible and separated from visible art.
- Family validator passes after mutation is authorized.
- Visual proof includes close inspection and gameplay distance.
- Generated asset audit no longer reports `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH` or `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH` for these two finals.

## Screenshot And Render Proof Views

Proof must be captured after assets exist:
- Neutral studio inspection, final materials, close camera.
- Neutral studio inspection, flat material override or clay pass to prove silhouette and geometry without texture masking.
- Wireframe/topology view for LOD0/LOD1/LOD2.
- Vertex color debug views for R/G/B/A channels.
- Collider overlay with visible art enabled.
- LOD transition strip at expected distances.
- Shallow/surface light proof if placed in shallow routes.
- Medium-depth light proof if placed in medium-depth routes.
- Gameplay distance proof:
  - Egg cluster at 1 to 3 meters and 8 to 15 meters.
  - Giant plant at 5 to 12 meters and 20 to 60 meters.
- Compact/low, middle, high, and ultra quality captures if runtime material/mesh features scale.

Rejected proof:
- Cropped darkness shots.
- Fog/turbidity masking.
- Screenshot where the shape cannot be inspected.
- Single beauty shot without static validation.

## Validation Gates

Minimum post-mutation gates:
- Static primitive scan over both final prefabs.
- Renderer/material slot check.
- LODGroup check.
- Bounds/pivot check.
- Collider invisibility check.
- Texture role/import check.
- Vertex color semantic proof.
- Family variant validator.
- Generated asset audit rerun.
- Visual screenshot review at required views.
- Optional but expected: sway/biolum shader debug proof for giant plant and egg membrane/biolum accents.

Performance gates:
- LOD0 near-field density must buy visible form quality, not hidden complexity.
- LOD1/LOD2 must cut cost while preserving silhouette and material identity.
- Alpha/transparency must be controlled. Prefer dither/opaque subsurface approximation unless transparent sorting cost is proven acceptable.
- No runtime generator hot path without proof. Runtime read accessors must remain pure.
- `GlobalQualityWeight` must be continuous:
  - Low/compact: fewer membranes/fronds, shorter shader feature list, strong baked normals/AO, aggressive LOD, no flat or muddy asset.
  - Middle: full authored texture set, normal wetness/AO, moderate emissive accents.
  - High: denser membranes/fronds, richer wetness/current response, longer LOD residency.
  - Ultra: hero close-inspection density, stronger material layering, richer emissive organs, unchanged gameplay truth.

## Production Acceptance

This packet does not make the assets production-ready. It defines the rebuild contract. Production readiness requires all three pillars:
- Graphics: non-primitive believable organic forms with premium materials and proof views.
- Optimization: LOD/HLOD/material cost and collider policy proven.
- Gameplay: spawn/scan/fauna/harvest anchors separated from visible art and integrated only after validation.

Until those proofs exist, both OrganicMisc final family variants remain blocked.
