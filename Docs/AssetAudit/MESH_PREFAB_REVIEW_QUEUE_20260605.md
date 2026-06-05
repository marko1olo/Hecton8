# Mesh And Prefab Review Queue - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_YAML_SCAN`.
Scope: mesh/prefab promotion review order only. No prefab, scene, material, import, or `Assets` file was changed.

## Input Evidence

- `Docs/Reports/AssetSystem_20260605/MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`

Queue file:

- `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv`

## Review Order

1. `Nature/Rocks/ProceduralFinals`: strongest static candidate; prove shared material, LOD transition, collider proxy, and route screenshot.
2. `Nature/Flora/Baked`: geometry candidate blocked by proxy materials.
3. `BioForge/Shallows` kelp/tube coral: source candidates blocked by shared proxy material.
4. `BioForge/Shallows/PorousRock`: blocked by MeshCollider/collision proof.
5. `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders`: visible placement rejected.
6. `Construction/Final`: product-face rejected until primitive visual meshes and missing LODs are replaced.
7. External/prototype material refs: readback only; no third-party material mutation.

## Required Proof

- Unity prefab/material readback.
- Renderer user list for active scene or route placement.
- LODGroup, transition, triangle, and silhouette proof.
- Collider proxy readback and no complex MeshCollider misuse.
- Material import/binding proof and no proxy/placeholder material path on visible content.
- Addressables/load/release proof before broad placement.
- Bright photic/surface screenshots and Stats/Frame Debugger proof before product-face promotion.

## Scalability Consequences

- Low/compact: use only final proven meshes with LODs and material identity. Do not substitute primitive proxies.
- Middle: candidate pools can place visible content only after material, LOD, collider, and screenshot proof.
- High: extend LOD residency and density only after baseline proof.
- Ultra: spend budget on richer geometry, material layering, and near-field density; rejected proxy pools stay rejected.

## Regression Model

- CPU: no runtime code changed.
- GC: no runtime code changed.
- Memory/VRAM: no residency or import change.
- Cadence: no runtime cadence changed.
- Correctness: separates candidate geometry pools from visible-route rejects.

Final status: `PENDING_VERIFICATION`.
