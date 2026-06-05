# 2204 Procedural Mesh Generator Quality Audit

Worker: 2204
Evidence mode: STATIC SOURCE AND ASSET INSPECTION
Runtime/visual proof: PENDING VERIFICATION
Unity/generator execution: NOT RUN

## Summary
The procedural content stack is not a single generator. It is a mixed pipeline of editor/offline mesh builders, material/texture authoring helpers, family validators, runtime scatter/placement systems, proxy preview tools, and placeholder assets. The current rejection risk is not lack of files. The risk is that proxy/dev output and starter-generated assets can be mistaken for production visuals.

Static validators already block several hard failures, but they do not prove Subnautica-level surface/photic/medium appearance, dry-land sea flora legality, authored material quality, collision proxy correctness, or route composition.

## Generator And Tooling Routes

| Route | Files / folders | Output type | Execution class | Static classification | Required gate before product use |
|---|---|---:|---:|---|---|
| Coral mesh builder | `WorldProceduralCoralMeshBuilder.cs` | Coral mesh geometry | Editor/offline helper | Usable only as asset authoring input | LOD0/1/2, UVs, premium material maps, placement legality, screenshot proof |
| Seaweed / kelp mesh builder | `WorldProceduralSeaweedMeshBuilder.cs` | Flora mesh geometry | Editor/offline helper | High dry-land misuse risk | Underwater-only zone proof, material maps, LOD crossfade, instancing proof |
| Flora baked starter generator | `WorldProceduralFloraBakedStarterGenerator.cs` | Baked starter prefabs/meshes | Editor/offline generator | Provisional starter route | Authored or source texture proof; generated starter warning must not be hidden |
| Flora final authoring / validator | `WorldProceduralFloraFinalVariantAuthoring.cs`, `WorldProceduralFloraFinalVariantValidator.cs` | Flora/coral final variants | Editor validation | Strongest current flora gate | Must pass shader, `_BaseMap`, `_DetailMap`, `_NormalMap`, `_MaskMap`, LOD, budget, and import checks |
| Flora material/texture helpers | `WorldProceduralFloraMaterialAuthoring.cs`, `WorldProceduralFloraTextureAuthoring.cs`, `WorldProceduralFloraProxyShapeBuilder.cs` | Materials/textures/proxy shapes | Editor/offline support | Source support, not final quality proof | Texture provenance, import settings, atlas consistency, non-flat authored look |
| Geology final/profile authoring | `WorldProceduralGeologyFinalAuthoring.cs`, `WorldProceduralGeologyProfileAuthoring.cs`, `WorldProceduralGeologyFinalValidator.cs` | Rocks, arches, shelves, cave entrances, landmarks | Editor/offline authoring/validation | Product route if not proxy/primitive | Existing rock FBX/GLB preferred; no built-in primitive final; LOD/collision/material proof |
| Runtime generative geology | `WorldGenerativeGeology*.cs`, `HectonRockManager.cs` | Runtime geology/service meshes | Runtime/service | Suspicious for final asset generation | Must not cook final mesh/texture/collider at runtime without explicit proof and owner phase |
| Support final authoring | `WorldProceduralSupportFinalAuthoring.cs`, `WorldProceduralSupportFinalValidator.cs`, `WorldProceduralSupportContract.cs` | Resource/hazard/safe pockets, creature spawn zones, route power/service scars | Editor/offline authoring/validation | Contains primitive/proxy authoring risk | Any primitive/flat/proxy output is dev-only; final use requires authored meshes/materials and route proof |
| Structural / wreckage support | `WorldProceduralStructuralContract.cs`, `WorldProceduralStructuralFinalValidator.cs`, `WorldProceduralStructuralStatusReport.cs`, `WfcBuilderTunerWindow.cs`, `WreckColliderFitter.cs` | Ruins, debris, wreck modules, colliders | Editor/offline support | Needs authored module library | WFC/socket output must be authored modules with bitmask sockets, LODs, async nav/collider proof |
| Interior colony final authoring | `WorldProceduralInteriorColonyFinalAuthoring.cs` | Interior/colony set dressing | Editor/offline authoring | Structural/interior route | No proxy material refs, no primitive finals, route readability proof |
| Organic misc authoring | `WorldProceduralOrganicMiscFinalAuthoring.cs`, `WorldProceduralOrganicMiscFinalValidator.cs`, `WorldProceduralOrganicMiscContract.cs` | Eggs, organic clusters, misc biota | Editor/offline authoring/validation | Biology/gameplay proof required | Sensory/gameplay role, material proof, spawn ecology legality |
| Family contract validator | `WorldProceduralFamilyContractValidator.cs`, `WorldProceduralFinalPrefabQualityGate.cs` | Family linkage and final prefab validation | Editor/static validation | Required gate | Blocks placeholder-only finals and built-in primitive meshes, but not full visual quality |
| Proxy/placeholder routes | `WorldProceduralProxyAuthoring.cs`, `WorldProceduralProxySceneBuilder.cs`, `WorldProceduralPlaceholderAuthoring.cs`, `ProceduralPlaceholders/**`, `WorldProceduralProxy/**` | Preview/proxy/dev assets | Editor/dev | Not product art | Must be absent from production scenes/prefabs except isolated diagnostics |
| Scatter preview / gizmo | `WorldProceduralScatterPreviewBuilder.cs`, `WorldProceduralScatterPreviewGizmoDrawer.cs` | Preview placement | Editor/dev | Debug route | Must not become production placement proof |
| Runtime scatter director | `WorldProceduralScatterDirector*.cs`, `WorldProceduralPlacementRule.cs`, `WorldProceduralFieldSampler.cs` | Runtime placement/scatter | Runtime | Placement route, not final art authoring | Depth/slope/substrate/biome proof; no hot GlobalRegistry polling or allocation proof gaps |
| Biota density map baker | `World/BiotaDensityMapBaker/Editor/**` | Biota density `.h8bin` / reports | Editor/offline baker | Data route, not visual asset route | Replace mock terrain inputs with real terrain proof; species matrix must block dry-land sea flora |
| Biome weight map baker | `World/BiomeWeightMapBaker/Editor/**` | Biome/control texture data | Editor/offline baker | Terrain data route | Needs real terrain/material verification before product claims |
| Offline geometry baker | `OfflineGeometryBaker/Shinobu213/**` | Baked geometry | Editor/offline support | Candidate optimization route | Must preserve material/LOD/collider identity and reject primitive proxies |
| AI texture/control map baker | `AITextureControlMapBaker/Shinobu269/**` | Texture/control maps | Editor/offline source route | Candidate material support | AI/generated maps must pass texture bible role/import/provenance gates |

## Current Validator Coverage

STATIC VERIFIED coverage:
- Family IDs and placement contract presence.
- Placeholder-only final family rejection.
- Final-ready non-proxy variants using built-in primitive meshes rejected by YAML scan.
- Missing renderer/material/null shared material failures.
- Flora final shader family checks for kelp/coral.
- Flora texture stack checks for `_BaseMap`, `_DetailMap`, `_NormalMap`, and `_MaskMap`.
- Flora LODGroup checks for three visible LODs, crossfade, and triangle cascade.
- Geology/support expected streaming layer and runtime scatter checks.

Missing or insufficient gates:
- Dry-land live seaweed/coral/abyssal flora rejection at scene-instance level.
- Route-aware depth, substrate, current, slope, light, cave/interior legality proof.
- Visual quality scoring for silhouette, beveling, organic deformation, repeated stamps, and authored finish.
- Non-flora material texture role/import/provenance checks.
- Collision proxy mismatch checks against render mesh and LOD0 MeshCollider misuse.
- Scene-level ban on `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` references in product scenes.
- Runtime cost, GC, and frame ownership proof for runtime scatter/geology routes.

## Batch21 / 2104 Cross-Check

Source: `Docs/Reports/Batch21/2104_PRIMITIVE_NULL_DEFAULT_STATIC_VALIDATOR.md` and CSV samples.

Static evidence:
- 930 files scanned.
- 3008 total findings.
- 346 active-scene findings.
- Severity counts: 1947 CRITICAL, 875 HIGH, 179 MEDIUM, 7 LOW.
- Issue classes include built-in primitive mesh refs, empty texture slots, null renderer material slots, placeholder/proxy material assets, placeholder/proxy material refs, and unresolved GUID refs.
- Surface/photic/medium/product-facing route bands contain high critical load.
- Active scene evidence includes `H8_PHOTIC_RIBBON_KELP_FIELD_1430` using `WorldProceduralProxy/MAT_family_kelp_patch_dense.mat`.
- `WorldProceduralProxy` materials for coral, kelp, rocks, ruins, creature zones, and support pockets have empty texture-slot findings.

Conclusion: Batch21/2104 already proves that primitive/proxy/default leakage is not hypothetical. This audit treats those routes as hard reject candidates until a fresh Unity-owner proof packet clears them.

## Existing Assets To Use Before Generation

STATIC VERIFIED source pools:
- Rock FBX/GLB assets and texture sets under `Assets/_Project/Art/Models/Rocks/**`, including Nordic beach rocks, mossy forest rocks, river rock, rock shelves, and 2K PBR texture sets.
- Shared rock materials under `Assets/_Project/Art/Materials/Rocks/**`.
- Coral texture inputs under `Assets/_Project/Art/Models/Sandbox/Coral_Albedo.png` and `Coral_Normal.png`.
- Baked generated flora/coral prefab families under `Assets/_Project/Prefabs/Nature/Flora/Baked/**`, including `family_coral_low`, `family_coral_branching`, and `family_kelp_abyssal` with LOD mesh assets.

Use these before new mesh generation. Generation is justified only when an existing authored/source asset cannot satisfy the specific route, silhouette, material, or gameplay need.

## New Source Texture / Gemini Candidate List

Require new authored/source texture evidence before production use:
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_*`.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_*`.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_*`, `MAT_family_landmark_spire`, `MAT_family_cave_entrance`.
- `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_*`, `MAT_family_debris_*`, `MAT_family_route_power`, `MAT_family_service_scar`.
- `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/**`.
- Generated starter flora/coral textures when validators flag editor-generated source instead of authored/source texture provenance.

## Generator Run Proof Packet

Any future Unity owner running a narrow generator route must attach:
- Generator command/menu path and exact family/profile/seed.
- Asset manifest: mesh, material, texture, prefab, LOD, collision proxy, and placement profile paths.
- Validator output for family, final prefab quality, flora/geology/support/structural domain gate, and primitive/null/default scan.
- Screenshots: close material view, normal gameplay distance, compact/low quality, LOD transition, collision/placement debug.
- Static placement proof: depth, waterline, slope, substrate, biome, cave/interior/open-water zone.
- Runtime proof only if runtime scatter/geology is touched: profiler frame cost, GC allocation, and owner-phase route.

## Acceptance State

Graphics: PENDING VERIFICATION.
Optimization: PENDING VERIFICATION.
Gameplay/placement: PENDING VERIFICATION.
Static audit: COMPLETE.
