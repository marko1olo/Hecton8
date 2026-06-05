# 1875 Resource Pickup Mesh Source Authoring Implementation

Agent ID: 1875
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/import/bake/PlayMode/screenshot/profiler execution: NOT RUN

## Scope

Owned source route created:

- `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs.meta`

Future generated mesh output route encoded in the tool:

- `Assets/_Project/Art/Generated/ProductFace/Resources`

No prefab, asset, scene, material, texture, binary, import, bake, or Unity execution was performed.

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
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_MATRIX.csv`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- Existing source patterns listed in the task prompt.

## Implementation

`ProductFaceResourcePickupMeshSourceAuthoring` is wrapped in `#if UNITY_EDITOR`, placed under `Assets/_Project/Scripts/Editor`, and exposes a future menu item:

- `HECTON-8/Product Face/Author Resource Pickup Source Meshes`

The code manually builds `Mesh` vertex/index data. It does not use `GameObject.CreatePrimitive`.

Included resource specs:

- `CopperOre` -> canonical data owner metadata `Data_Copper`
- `FiberKelp` -> `Data_FiberKelp`
- `HydrocarbonResin` -> `Data_HydrocarbonResin`
- `MembraneTissue` -> `Data_MembraneTissue`
- `SilicaShards` -> `Data_SilicaShards`
- `SilverOre` -> `Data_SilverOre`
- `SulfurClumps` -> `Data_SulfurClumps`
- `TitaniumScrap` -> `Data_TitaniumScrap`

`Item_Titanium` is documented in code as quarantine/canonical-route only and is not generated as a duplicate mesh source.

## Geometry Routes

- CopperOre: irregular copper-bearing host-rock chunk with raised oxide streaks.
- FiberKelp: folded harvested kelp frond bundle with ragged strips.
- HydrocarbonResin: sagging resin lobes with oily clump silhouette.
- MembraneTissue: folded torn wet sheet geometry with thickness.
- SilicaShards: angular shard cluster with varied fracture heights.
- SilverOre: darker host-rock ore with narrower silver seam language.
- SulfurClumps: brittle nodule cluster plus vent residue rubble.
- TitaniumScrap: bent cut manufactured plate shards with torn-edge silhouette.

Continuous `GlobalQualityWeight` affects rings, columns, lobes, fronds, shards, nodules, fold segments, and plate subdivisions. It does not change item ids, data owner metadata, future collider truth, save identity, or authority route.

## Validation In Source

The future authoring path validates:

- non-null mesh;
- non-empty vertices;
- non-empty triangle index buffer;
- triangle index count divisible by three;
- finite vertices;
- finite nonzero bounds;
- in-range triangle indices;
- non-degenerate triangle area;
- non-duplicated per-resource silhouette signatures;
- non-empty resource/data/comment metadata.

## Limitations

This task did not and cannot claim:

- Unity import success;
- generated `.asset` existence;
- material/texture acceptance;
- MRAO/normal/UV density proof;
- prefab relink correctness;
- collider/proxy proof;
- screenshot/capture visual acceptance;
- runtime or profiler behavior;
- first-20-minute product-face closure.

## Verification

Claim: editor-only source route exists.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`.
Command or tool: static file read / git diff.
Date: 2026-06-04.
Residual risk: Unity compilation/import not run by task constraint.

Claim: forbidden primitive API is absent.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`.
Command or tool: `rg -n "GameObject\\.CreatePrimitive|CreatePrimitive" Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`.
Date: 2026-06-04.
Residual risk: text scan only; compile/import not proven.

Claim: required resource ids are present.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`.
Command or tool: `rg -n "CopperOre|FiberKelp|HydrocarbonResin|MembraneTissue|SilicaShards|SilverOre|SulfurClumps|TitaniumScrap|Data_Copper|Item_Titanium" Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`.
Date: 2026-06-04.
Residual risk: text scan only; generated mesh assets not produced.

## Result

What was wrong: resource pickup source package was missing; current product-face pickup prefabs remain primitive debt.

What changed: added compile-oriented editor-only source authoring code for future manual mesh generation, plus concise 1875 status/rationale/log/report artifacts.

In-game result: PENDING VERIFICATION. Unity execution was forbidden.

What was verified: static source/doc evidence only.

## Orchestrator Follow-Up

After agent completion, the local orchestrator made one compile-compatibility hygiene patch in the owned source route:

- Replaced `float.IsFinite` usage with a local `IsFinite(float)` wrapper based on `float.IsNaN` / `float.IsInfinity`.

Evidence class remains `STATIC_SOURCE`. Unity import/menu execution is still pending.
