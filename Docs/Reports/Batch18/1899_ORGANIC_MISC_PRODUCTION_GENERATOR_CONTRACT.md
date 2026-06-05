# 1899 Organic Misc Production Generator Contract

Evidence class: STATIC_SOURCE
Runtime proof: PENDING UNITY
Unity/import/render/profiler proof: not run by task rule.
Date: 2026-06-04
Agent: 1899

## Boundary

This is a static implementation contract for future authorized work. It does not edit source, assets, prefabs, scenes, `.meta`, generated meshes, binaries, DataMonolith, or task files.

The contract translates `Docs/Reports/Batch18/1856_ORGANIC_MISC_FINAL_MESH_REBUILD_PACKET.md` into generator/source requirements for replacing:

- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_EggCluster.prefab`
- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_PlantGiant.prefab`

No runtime, visual, import, prefab, profiler, or gameplay quality claim is made. All future implementation remains `PENDING UNITY` until the proof stack below exists.

## Authorities And Mandates Read

Root and domain authorities:

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
- `3DMODEL_FLORA_CORAL.md`
- `PROCEDURAL_ASSET_PIPELINE.md`

Batch/static evidence:

- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1856_ORGANIC_MISC_FINAL_MESH_REBUILD_PACKET.md`
- `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md`

Targeted source reads:

- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralCoralMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraBakedStarterGenerator.cs`

Mandates:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/PHYS_Destructible_Organic_Entropy.txt`

## Current Defect

Static evidence from 1851 and 1856:

- `PFB_Organic_EggCluster` has `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH` and family link `family.egg.cluster.final.nest_cluster` has `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`.
- `PFB_Organic_PlantGiant` has `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH` and family link `family.plant.giant.final.silhouette` has `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`.
- 1856 records 11 visible primitive mesh refs for the egg cluster and 12 visible primitive mesh refs for the giant plant.
- Existing family assets mark both variants `proxyOnly: 0` and `finalReady: 1`, so this is a false-ready production defect, not harmless proxy state.

Targeted source evidence:

- `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals` is guarded by `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring`.
- `BuildEggClusterLods` uses visible `PrimitiveType.Cylinder` and `PrimitiveType.Sphere` specs.
- `BuildPlantGiantLods` uses visible `PrimitiveType.Cylinder`, `PrimitiveType.Sphere`, and `PrimitiveType.Capsule` specs.
- `CreateVisualPrimitive` calls `GameObject.CreatePrimitive(primitiveType)`.

Conclusion: the existing authoring route is a defect map only. Future production must not unblock or relax that legacy gate.

## Future Generator Components

Future implementation may create these source files after mutation is explicitly authorized:

- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscMeshBuilder.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscProductionGenerator.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscManifestWriter.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscValidator.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscProofChecklist.cs`

Allowed internal source patterns:

- Reuse `WorldProceduralSeaweedMeshBuilder` concepts for holdfasts, stipes, blade sockets, lamina/fronds, bulbs, LOD mesh creation, and writable mesh data.
- Reuse `WorldProceduralCoralMeshBuilder` concepts for warped blobs, porous pads, plates, tubes, branch knuckles, tip clusters, welded cross-links, LOD mesh creation, and writable mesh data.
- Reuse `WorldProceduralFloraBakedStarterGenerator` only as an editor pipeline pattern for three visible LOD meshes, `LODGroup`, shared materials, mesh asset persistence, and finite bounds sanitization.

Required new generator responsibilities:

- Build OrganicMisc-specific geometry, not generic kelp/coral variants copied into final prefabs.
- Emit deterministic mesh assets, material/texture role manifests, final prefab assembly rules, hidden anchors, collider proxies, validation reports, and named proof paths.
- Run validation before any future `AssetDatabase.SaveAssets` or `PrefabUtility.SaveAsPrefabAsset`.
- Keep any last-300-step generator diagnostic ring in future critical generator code if the bake can fault on non-finite geometry or validation abort.

## Files Future Implementation Must Not Unblock

Future implementation must not:

- Relax `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring`.
- Run production through `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals`.
- Treat `WorldProceduralOrganicMiscFinalAuthoring.cs` primitive specs as accepted mesh source.
- Change family `finalReady`/`proxyOnly` state or clear audit errors before replacing visible primitive mesh refs and producing proof.
- Relink `family.egg.cluster.final.nest_cluster` or `family.plant.giant.final.silhouette` to new assets before validation and screenshot proof exist.
- Use `WorldProceduralFloraBakedStarterGenerator` output as a drop-in final for OrganicMisc without OrganicMisc-specific manifests, anchors, collider policy, material proof, and visual proof.
- Use visible primitives as gameplay markers, anchors, or proof placeholders.

## Egg Cluster Contract

Target family:

- `family.egg.cluster`
- Final variant: `family.egg.cluster.final.nest_cluster`
- Target prefab: `PFB_Organic_EggCluster`

Required LOD0 mesh features:

- 8 to 14 asymmetric ovoid eggs built from generated or DCC organic surfaces, not stock sphere meshes.
- Per-egg deformation: off-axis bulge, uneven poles, local dents, seam/cap ridges, growth scars, shell pores, and non-repeating silhouettes.
- Uneven rotations and spacing. No radial/even placement pattern.
- Membrane webbing between eggs and substrate: elastic sheets, cords, torn thin edges, thickened seams, slime rims, and root filaments.
- Porous substrate saddle: coral/root/rock-like pad with cavities, embedded residue, sediment contact, and AO-heavy underside.
- Visible tendril sockets and membrane attachment zones, but gameplay logic remains hidden in anchors.
- Close-inspection readability at 1 to 3 meters without darkness, fog, turbidity, or post-processing hiding shape weakness.

Required LOD1:

- Consolidated asymmetric egg masses preserving clutch silhouette.
- Fewer but thicker membranes/cords.
- Non-flat substrate pad remains visible.
- Target cost: about 35 to 50 percent of LOD0 triangles after proof.

Required LOD2/HLOD:

- Non-primitive silhouette shell or clustered organic mesh with baked normal/AO/biolum masks.
- Preserves clutch mass, substrate pad, and membrane silhouette.
- No built-in sphere/cylinder/capsule mesh refs.
- HLOD may be an impostor/card only if shape, alpha clip/dither, and route readability survive proof.

Material roles:

- `MAT_OrganicEggCluster_Shell`: shell pores, ridges, scars, wet specular breakup, subtle translucency cue through an opaque or dithered subsurface approximation.
- `MAT_OrganicEggCluster_Membrane`: vein normals, wet highlights, torn membrane rims, controlled alpha clip/dither or opaque membrane fake; no compact alpha-blend field.
- `MAT_OrganicEggCluster_Substrate`: porous organic rock/coral/root pad, wet sediment contact, cavity AO.
- `MAT_OrganicEggCluster_Biolum`: optional low-area biolum accent for embryo veins/cracks/tips only; random glow is rejected.

Vertex color semantics:

- R = water/current sway amplitude. Eggs 0. Rooted substrate 0. Loose membranes/tendrils low to medium.
- G = biolum mask/phase for restrained embryo glow, membrane veins, cracks, or tips.
- B = baked AO/cavity darkness for underside, inter-egg contact, substrate pits, and membrane roots.
- A = wetness, shell thickness, scan-story mask, or damage/harvest eligibility. The chosen meaning must be recorded in the manifest and remain stable across LODs.

Hidden gameplay anchors:

- `ANCHOR_Scan`
- `ANCHOR_Spawn`
- `ANCHOR_StoryLookAt`
- `ANCHOR_EggCenter`
- `ANCHOR_FaunaInterest`

Collider policy:

- Default visible mesh is art only.
- Root trigger or `COL_*` primitive children may represent scan/spawn/fauna interest volumes.
- Optional convex proxy only with validation, triangle budget, and profiler proof in a later implementation pass.
- LOD0 mesh collider is rejected.

## Giant Plant Contract

Target family:

- `family.plant.giant`
- Final variant: `family.plant.giant.final.silhouette`
- Target prefab: `PFB_Organic_PlantGiant`

Required LOD0 mesh features:

- Holdfast/root mass gripping substrate with radial roots, knuckles, cavities, tendrils, and sediment/wet contact.
- Tapered trunk/stalk with nonuniform radius, ribbing, twist, scars, vertical bands, branch nodes, and current-shaped lean.
- Canopy/fronds built as folded lamina, serrated/lobed ribbons, broad undulating sheets, or thickened organic fronds. Capsules are rejected.
- Asymmetric canopy distribution shaped by current and light. Overlapping fronds are allowed only if curved, thickened, and non-card-flat.
- Bud/organs grown into stalk nodes with membrane collars and seam rims, not isolated spheres.
- Strong 20 to 60 meter silhouette for route/biome readability.
- Close inspection at 5 to 12 meters must show wet tissue, rib grooves, vascular lamina, pores/scars, node collars, and controlled biolum detail.

Required LOD1:

- Preserve stalk twist, root mass, dominant canopy arcs, and key bud/node silhouettes.
- Merge tertiary fronds and minor buds into baked silhouette clusters.
- Target cost: about 35 to 50 percent of LOD0 triangles after proof.

Required LOD2/HLOD:

- Non-primitive silhouette shell or impostor that keeps root mass and crown identity.
- Dithered LOD fade/hysteresis policy. Alpha-blend field is rejected on compact.
- HLOD uses GPU Resident Drawer-compatible MeshRenderer ownership or documented BRG route only after proof.

Material roles:

- `MAT_OrganicPlantGiant_Stalk`: ribbed wet tissue, vertical scars, roughness variation, low emission in deep grooves only.
- `MAT_OrganicPlantGiant_Frond`: vascular lamina, serrated/lobed rims, wet highlight response, current-driven masks.
- `MAT_OrganicPlantGiant_Holdfast`: porous wet root/substrate blend, sediment contact, cavity AO.
- `MAT_OrganicPlantGiant_Bud`: biolum organ with low-area emission, shell/membrane rim, age/damage variation.

Vertex color semantics:

- R = sway amplitude. Holdfast/root 0. Trunk low to medium. Frond tips high.
- G = biolum mask/phase limited to buds, edge veins, node organs, or rib channels.
- B = baked AO/cavity darkness in root cracks, trunk grooves, canopy overlaps, and bud collars.
- A = wetness, age/damage, stiffness, or current-response mask. Meaning must be recorded in the manifest and preserved across LODs.

Hidden gameplay anchors:

- `ANCHOR_Scan`
- `ANCHOR_FaunaInterest`
- `ANCHOR_CanopyCenter`
- `ANCHOR_RootBase`
- `ANCHOR_Harvest`

Collider policy:

- Visible plant mesh is not physics truth.
- Default flora collision is none unless route/blocking/interaction requires it.
- Allowed proxies: root trigger sphere/capsule, coarse canopy/fauna-interest trigger, coarse root/cover proxy, or compound primitive blockers.
- Optional convex collider only when validation and runtime proof exist.
- LOD0 `MeshCollider` is rejected.

## Output Folders

Future generated assets should use these paths:

- Egg meshes: `Assets/_Project/Art/Generated/OrganicMisc/EggCluster/`
- Giant plant meshes: `Assets/_Project/Art/Generated/OrganicMisc/PlantGiant/`
- Textures: `Assets/_Project/Art/TEXTURES/OrganicMisc/`
- Materials: `Assets/_Project/Art/Materials/Nature/OrganicMisc/`
- Final prefabs: `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/`
- Local generated-asset manifests: `Docs/Reports/GeneratedAssets/OrganicMisc/`
- Named screenshots: `Docs/Screenshots/GeneratedAssets/OrganicMisc/`
- Batch summary reports: `Docs/Reports/Batch18/`
- Generator dumps if a future bake faults under explicit agent ID: `Docs/AgentLogs/Dump_[ID].bin`

Suggested asset stems:

- `OrganicEggCluster_NestCluster`
- `OrganicPlantGiant_Silhouette`

Suggested filenames:

- `MESH_OrganicEggCluster_NestCluster_LOD0.asset`
- `MESH_OrganicEggCluster_NestCluster_LOD1.asset`
- `MESH_OrganicEggCluster_NestCluster_LOD2.asset`
- `MESH_OrganicPlantGiant_Silhouette_LOD0.asset`
- `MESH_OrganicPlantGiant_Silhouette_LOD1.asset`
- `MESH_OrganicPlantGiant_Silhouette_LOD2.asset`
- `MANIFEST_OrganicEggCluster_NestCluster.md`
- `MANIFEST_OrganicPlantGiant_Silhouette.md`

## Manifest Schema

Each final OrganicMisc package manifest must include:

- `assetStem`
- `targetFamily`
- `targetVariant`
- `targetPrefab`
- `sourceRoot`
- `generatorName`
- `generatorVersion`
- `deterministicSeed`
- `GlobalQualityWeightBakeRange`
- `meshFeatures`
- `lodMeshes`: `lod0`, `lod1`, `lod2`, optional `hlod`
- `lodPolicy`: triangle counts, threshold distances, dither/hysteresis, decimation method, preserved anchors
- `boundsAndPivot`
- `materialSlots`
- `textureRoles`: albedo, normal, MRAO/ORM, emission, detail/height if used
- `textureImportPolicy`: sRGB/linear, normal type, compression, mip/streaming policy, max size by quality range
- `uvOrProjection`: unwrap/triplanar route, texel density, stretch, atlas padding, edge bleed
- `vertexColorContract`: R/G/B/A meanings and ranges
- `hiddenGameplayAnchors`
- `colliderProxy`: type, bounds, primitive count or convex triangle count, or explicit no-collision reason
- `gameplayTruthSeparation`
- `biomeRoute`: depth band, route use, light/current/substrate logic
- `streamingPolicy`: Addressables group target, HLOD/impostor note, GPU Resident Drawer/BRG eligibility
- `validatorHooks`
- `staticPrimitiveScanResult`
- `proofArtifacts`
- `auditIssuesBlockedOrCleared`
- `rejectionReview`
- `createdBy`
- `reviewedBy`
- `dateUtc`

## Screenshot Proof Set

Required future proof after implementation:

- Neutral studio close inspection with final materials.
- Neutral studio clay/flat material pass proving mesh silhouette without texture masking.
- Wireframe/topology view for LOD0, LOD1, and LOD2.
- Vertex color debug views for R, G, B, and A.
- Collider overlay with visible art enabled and visible mesh separated from gameplay colliders.
- LOD transition strip at expected distances with dither/hysteresis note.
- Shallow/surface lighting proof if placed in 0 to 100 m photic routes.
- Medium-depth lighting proof if placed in 200 to 400 m routes.
- Gameplay distance proof:
  - Egg cluster at 1 to 3 meters and 8 to 15 meters.
  - Giant plant at 5 to 12 meters and 20 to 60 meters.
- Compact, middle, high, and ultra captures if material, shader, LOD residency, density, or render features scale.

Rejected proof:

- Cropped darkness shots.
- Fog/turbidity masking.
- Single beauty screenshot.
- Screenshot where geometry, vertex color, material roles, LODs, or colliders cannot be inspected.
- Static text proof used to clear visual proof. `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` requires named render/screenshot artifacts.

## Validation Stack

Minimum future validation before production relink:

- Static primitive scan proves zero visible built-in primitive mesh refs in both final prefabs.
- `LODGroup` exists with LOD0/LOD1/LOD2 or approved HLOD/impostor policy.
- Mesh validation: finite positions/normals/tangents/UVs/colors/bounds, valid indices, no degenerate triangles, normalized normals/tangents, nonzero finite bounds.
- Vertex color semantic validation for R/G/B/A ranges by family and LOD.
- UV/projection validation: density, stretch, atlas rects, mip padding, edge bleed, triplanar justification if used.
- Texture role/import validation: albedo sRGB, normal map type/linear, MRAO/ORM linear, compression, mips, streaming settings.
- Material slot validation: bounded slots, shared `MAT_*`, SRP Batcher/instancing compatibility, no per-instance material clones.
- Collider validation: invisible `COL_*` proxies or explicit no-collision reason; no LOD0 visual mesh collider.
- Anchor validation: required `ANCHOR_*` transforms exist, are hidden, and are not visible primitive meshes.
- Family variant validator: only after assets and proof exist.
- Generated asset audit rerun only by an authorized future task that owns the audit output.
- Screenshot/taste review against `TASTE.md`, `3dmodel.md`, and `3DMODEL_FLORA_CORAL.md`.

Proof state after this 1899 task remains `STATIC_SOURCE` and `PENDING UNITY`.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` is continuous. The labels below are documentation checkpoints, not binary switches.

Compact consequence:

- Preserve non-primitive silhouette, anchor identity, collider identity, vertex color semantics, wet material identity, and Subnautica-level shallow readability.
- Use simpler LOD residency, fewer membranes/fronds, lower pore/serration density, smaller shared texture targets, baked AO/normal reliance, dithered fades, no alpha-blend fields, and simple/no collision for flora.
- Compact cannot be muddy, flat, blurry, primitive, or hidden by darkness.

Middle consequence:

- Restore normal authored texture set, richer masks, clearer wetness/AO, moderate biolum accents, denser fronds/membranes, and route-context readability.
- No new gameplay truth, no anchor changes, no collider identity changes.

High consequence:

- Longer LOD residency, richer cavity/wetness/biolum response, more frond/membrane silhouette detail, better near-field normals, stronger material breakup, and GPU Resident Drawer-friendly static rendering where proven.
- No gameplay truth changes.

Ultra consequence:

- Offline visual overkill: denser organic scars, pores, knuckles, membrane rims, vascular lamina, wetness layers, emission organs, and close-camera material response.
- Ultra buys sensory density only. It must not add hidden gameplay truth, new scan facts, new collision, changed harvest points, or altered family identity.

## Gameplay Truth Separation

Visual meshes are art carriers. Gameplay truth must be explicit and hidden:

- Scanner/story/spawn/fauna/harvest/cover decisions live in metadata, anchors, owner systems, or future typed signals.
- Runtime shaders may consume baked vertex colors for sway, wetness, and biolum presentation.
- Runtime code must not rebuild mesh topology, UVs, textures, colliders, or per-vertex gameplay weights.
- Read accessors must remain pure; no scene search, allocation, mutation, event publication, or hot polling.
- If future destructible or harvestable organic entropy is added, it must use pool/data-driven policies from `PHYS_Destructible_Organic_Entropy.txt`, not visible-mesh physics.

## Exact Blocked Legacy Route

Blocked route:

`WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals` -> `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring` -> `BuildEggClusterLods` / `BuildPlantGiantLods` -> `CreateVisualPrimitive` -> `GameObject.CreatePrimitive`

This route must remain blocked for production because it authors visible Unity primitives into final prefabs. It may be read as a dimension and defect map only.

## Acceptance Gate For Future Relink

Do not relink or mark accepted until all are true:

- `PFB_Organic_EggCluster` and `PFB_Organic_PlantGiant` have zero visible Unity built-in primitive mesh refs.
- Mesh, material, texture, collider, anchor, LOD, HLOD, and manifest validation pass.
- Required screenshot proof exists with filename-matched asset stems.
- Compact captures prove non-ugly silhouette and material readability.
- High/ultra captures add sensory value only.
- Family validator and generated asset audit no longer report the two OrganicMisc primitive issue codes.
- Runtime/profiler claims, if any, have Unity/profiler artifacts. Static text remains insufficient.

## Result

Contract status: STATIC_SOURCE_COMPLETE.

Production status of target assets: BLOCKED / PENDING UNITY.

Reason: this task produced the future generator contract only. It did not implement the generator, mutate assets, run Unity, render screenshots, import assets, profile runtime, or verify gameplay behavior.

## Static Verification

Commands run:

- `git diff --check -- Docs/Reports/Batch18/1899_ORGANIC_MISC_PRODUCTION_GENERATOR_CONTRACT.md Docs/Reports/Batch18/1899_ORGANIC_MISC_PRODUCTION_GENERATOR_MATRIX.csv Docs/Tasks/Status_1899.md Docs/AgentLogs/Rationale_1899.md Docs/AgentLogs/LOG_1899.md`
- `Import-Csv Docs/Reports/Batch18/1899_ORGANIC_MISC_PRODUCTION_GENERATOR_MATRIX.csv | Measure-Object`
- Static term cross-check for `PFB_Organic_EggCluster`, `PFB_Organic_PlantGiant`, `WorldProceduralOrganicMiscFinalAuthoring`, `vertex color`, `biolum`, `Subnautica`, `PENDING UNITY`.

Results:

- `git diff --check`: clean.
- CSV row count: 2.
- Static term cross-check: all required terms present.
