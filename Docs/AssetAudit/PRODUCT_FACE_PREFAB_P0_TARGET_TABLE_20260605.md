# Product-Face Prefab P0 Target Table - 2026-06-05

Status: `PENDING UNITY PREFAB READBACK`.
Evidence class: `STATIC_DOC + STATIC_YAML_SCAN + UNITY_BATCHMODE_LOG`.
Runtime proof: absent.
Visual proof: absent.
Profiler/GC/memory proof: absent.

Write boundary: this report and `Docs/AssetAudit/PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` only. No Unity run, no Assets mutation, no prefab/material/scene/code edit, no Status/Rationale/LOG write.

## Required Reads Used

- `AGENTS.md`
- `TASTE.md`
- `3dmodel.md`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_18_PRODUCT_FACE_VALIDATOR_SYNTHESIS_20260605.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.csv`
- `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv`
- `taskslocal/asset_system_20260605/ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`

`Docs/Actual Domains of Project.txt` was absent. Narrow inferred domain: product-face prefab/mesh replacement targeting for future Unity readback owners.

## Counts

CSV target rows: 39.

| Group | Rows | Static built-in primitive refs | Rows missing static LODGroup token | Static mesh collider refs | Renderer tokens | Material tokens |
|---|---:|---:|---:|---:|---:|---:|
| Player.prefab | 1 | 17 | 1 | 0 | 18 | 20 |
| Tools/Held | 12 | 12 | 12 | 0 | 12 | 18 |
| Items/Tools | 12 | 12 | 12 | 0 | 12 | 24 |
| Resources/Pickups | 8 | 8 | 8 | 0 | 8 | 16 |
| Transport | 4 | 4 | 4 | 0 | 4 | 8 |
| Sky_System | 1 | 1 | 1 | 0 | 1 | 1 |
| Ocean_Crest | 1 | 4 | 1 | 0 | 3 | 3 |
| Total | 39 | 56 | 39 | 0 | 60 | 90 |

Ocean note: `Ocean_Crest.prefab` has four static primitive refs in the CSV row. The P0 visible target is only `SargassumMicroFaunaBoids.boidMesh` using the Unity built-in `Plane`, as reported by the sky/ocean validator. The three accepted Crest hidden input primitives remain data-only exceptions and are not product art clearance.

Model source matrix note: `MODEL_FILE_IMPORT_RISK_MATRIX_20260605.csv` has 16 rows; all 16 are `PENDING_VERIFICATION`, all 16 carry static mesh-compression/import-animation risk flags, and 9 carry static material-import route risk. That matrix does not provide a directly acceptable mesh source for these P0 product-face replacements.

## Rejection Notes

- Product-face promotion remains blocked by `ASSET_OWNER_18`: material/texture gate `FAILED`, prefab quality gate `FAILED`, sky/ocean source primitive gate `FAILED`.
- `VISUAL_REFERENCE_REJECTION_20260605.md` rejects current route visuals. Placement camouflage is explicitly rejected: do not hide bad water, sky, Aegir, terrain, shoreline, or prefab primitives with rocks, flora, fog, darkness, bloom, or camera framing.
- A primitive with stronger material is still rejected. Final visible assets must be authored or offline-generated meshes with topology, normals/tangents, UVs, material IDs, LOD chain, collider proxies, and proof artifacts.
- Static YAML rows are evidence of text/source state only. They are not Unity prefab readback, scene instance proof, render proof, collider proof, material proof, route visual proof, or runtime performance proof.
- The absence of static `MeshCollider` tokens is not collider acceptance. Collider truth still needs Unity readback and `COL_*` proxy proof for each target family.

## Next Owner Mapping

| Owner lane | Target rows | Required first action |
|---|---:|---|
| Player/Suit product-face owner | 1 | Unity Prefab Stage readback of player renderers, mesh refs, material refs, colliders, anchors, and scene overrides before any mesh replacement. |
| Tools held owner | 12 | Replace primitive close-camera held visuals with function-specific authored/offline-generated meshes; preserve hand pose, effect origins, and interaction anchors. |
| World tool item owner | 12 | Build pickup/world versions from held-tool silhouette language; preserve pickup triggers, item identity, and route readability. |
| Resource pickup owner | 8 | Create material-specific resource silhouettes; one generic rock body with recolors is rejected. |
| Transport/vehicles owner | 4 | Replace primitive transport bodies with pressure-rated macro forms and compound `COL_*` proxies; preserve docking, tow, hand, cockpit, and thruster anchors. |
| Sky/Aegir owner | 1 | Read back whether `Sky_System/Sphere` is active visible route; replace only the mesh route unless material owner approval exists. |
| Ocean/Crest owner | 1 | Replace `SargassumMicroFaunaBoids.boidMesh` plane route only; preserve Crest canonical materials and hidden data-input exceptions. |

## Proof Required From Future Owners

Every row in the CSV has status `PENDING UNITY PREFAB READBACK` until the relevant owner provides:

- Unity Prefab Stage or scoped Editor API readback of renderer object paths, mesh refs, material refs, LODGroup state, colliders, scripts, pivots, sockets, anchors, active/inactive state, and scene overrides.
- Mesh source report with authored/offline generation method, LOD0/LOD1/LOD2 triangle counts, bounds, finite topology, normals/tangents, UV density, material slots, and import settings.
- Collider report with `COL_*` child list, collider type, layer, trigger state, convex/shared mesh state, and visual LOD0 MeshCollider misuse check.
- Material report with `MAT_*` paths, shader, `TX_*` roles, channel semantics, compression/mip/import state, SRP Batcher/instancing risk, and no blockout/default/proxy/null route.
- Screenshots or captures: flat-material silhouette, final material, wireframe, collider overlay, LOD transition, and route context for first-exit/tool/pickup/transport/sky/ocean where relevant.
- Console/import state plus Frame Debugger/Stats for SetPass, batches, materials, shadow casters, LOD state, and GPU Resident Drawer/SRP Batcher compatibility.
- Runtime profiler/GC/memory proof if renderer count, collider count, scene placement, prefab scripts, Addressables, or runtime behavior changes.

## Continuous GlobalQualityWeight Consequences

Low/compact `0.0-0.25`: keep final silhouettes, bevels or organic thickness where camera-visible, baked AO/material identity, dithered LOD where supported, and stable `COL_*` proxies. Reduce density, texture residency, shadow eligibility, and LOD distance smoothly; never expose primitive visible meshes.

Middle `0.25-0.55`: maintain complete material identity, route-readable tools/resources/transport, stable LOD transition bands, and separate collision truth.

High `0.55-0.85`: spend added budget on longer LOD0/LOD1 residency, stronger trims, labels, wear, wetness, detail normals, emissive masks, and richer near-field route dressing after Frame Debugger/Stats proof.

Ultra `0.85-1.0`: add capture-grade bevel density, layered panel detail, richer material response, longer route LOD residency, and visual overkill. Gameplay truth, prefab identity, collider authority, material channel semantics, save identity, and owner route do not change.

Final disposition: `PENDING UNITY PREFAB READBACK`.
