# Mesh Prefab Source Folder Route Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_YAML_SCAN`, `STATIC_SOURCE`, `STATIC_DOC`.
Scope: folder-level prefab source map derived from `.prefab` YAML tokens and existing mesh/prefab promotion static table.

No Unity run, prefab edit, material edit, scene save, import, build, Play Mode, screenshot proof, Stats, or Frame Debugger proof was performed.

CSV companion: `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`.

## Static Summary

- Prefab folders mapped: `40`.
- Prefabs covered: `602`.
- Prefabs without static `LODGroup` token: `221`.
- Prefabs with built-in primitive mesh refs: `183`.
- Prefabs with static `MeshCollider` token: `76`.
- Proxy/placeholder-risk folders: `20`.
- Folder priority distribution: `P0`=8, `P1`=14, `P2`=18.

## Route-Critical Folders

- `Assets/_Project/Prefabs/WorldProceduralProxy`: prefabs `88`, LOD `0`, no LOD `88`, primitive refs `88`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Proxy prefab folder: primitive mesh refs, no LOD chain, proxy route only. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora`: prefabs `8`, LOD `0`, no LOD `8`, primitive refs `8`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Construction`: prefabs `7`, LOD `0`, no LOD `7`, primitive refs `7`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/TerrainLod`: prefabs `5`, LOD `0`, no LOD `5`, primitive refs `5`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/LargeThreats`: prefabs `4`, LOD `0`, no LOD `4`, primitive refs `4`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Fauna`: prefabs `3`, LOD `0`, no LOD `3`, primitive refs `3`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Debris`: prefabs `2`, LOD `0`, no LOD `2`, primitive refs `2`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Resources`: prefabs `1`, LOD `0`, no LOD `1`, primitive refs `1`, MeshCollider `0`, priority `P0`, disposition `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder folder: primitive/proxy content cannot be visible route art. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future replacement owner`.
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp`: prefabs `100`, LOD `100`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_SOURCE_POOL_BLOCKED_BY_MATERIAL`. BioForge organic source pool blocked by shared proxy material and missing visual proof. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock`: prefabs `50`, LOD `50`, no LOD `0`, primitive refs `0`, MeshCollider `50`, priority `P1`, disposition `CANDIDATE_SOURCE_POOL_SPLIT_COLLIDER_BLOCKED`. PorousRock source pool blocked by material route and MeshCollider proof. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / BioForge owner`.
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral`: prefabs `50`, LOD `50`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_SOURCE_POOL_BLOCKED_BY_MATERIAL`. BioForge organic source pool blocked by shared proxy material and missing visual proof. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`: prefabs `49`, LOD `49`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_GEOMETRY_STATIC_ONLY`. Strongest static geology candidate; still needs Unity material/LOD/collider/screenshot proof. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / geology prefab owner`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy`: prefabs `15`, LOD `15`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal`: prefabs `14`, LOD `14`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall`: prefabs `14`, LOD `14`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense`: prefabs `12`, LOD `12`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Construction/Final`: prefabs `10`, LOD `6`, no LOD `4`, primitive refs `10`, MeshCollider `0`, priority `P1`, disposition `PRODUCT_FACE_REJECT_STATIC`. Construction final intent exists, but built-in primitive visual meshes and missing LODs block product-face route. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / hard-surface prefab owner`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle`: prefabs `10`, LOD `10`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching`: prefabs `6`, LOD `6`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low`: prefabs `6`, LOD `6`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive`: prefabs `6`, LOD `6`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate`: prefabs `6`, LOD `6`, no LOD `0`, primitive refs `0`, MeshCollider `0`, priority `P1`, disposition `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Baked flora geometry candidate, blocked by WorldProceduralProxy material route until readback/replacement. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals`: prefabs `27`, LOD `0`, no LOD `27`, primitive refs `0`, MeshCollider `0`, priority `P2`, disposition `NARRATIVE_PREFAB_STATIC_ONLY`. Narrative prefab folder is not a visual asset promotion source in current front. Next: `future narrative/world-space UI owner`.
- `Assets/_Project/Prefabs/Nature/GOTOVYE_PREFABY_KAMNEY`: prefabs `25`, LOD `25`, no LOD `0`, primitive refs `0`, MeshCollider `25`, priority `P2`, disposition `STATIC_SOURCE_NEEDS_ROUTE_PROOF`. Legacy/nature rock source folder needs material, LOD, collider, and route screenshot proof before promotion. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / geology prefab owner`.
- `Assets/_Project/Prefabs`: prefabs `21`, LOD `0`, no LOD `21`, primitive refs `6`, MeshCollider `1`, priority `P2`, disposition `UNCLASSIFIED_PREFAB_SOURCE`. Prefab folder needs owner assignment before visible route promotion. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future prefab owner`.
- `Assets/_Project/Prefabs/Items/Tools`: prefabs `12`, LOD `0`, no LOD `12`, primitive refs `12`, MeshCollider `0`, priority `P2`, disposition `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF`. Product-face prefab folder needs mesh/material/LOD/import proof before route use. Next: `future product-face prefab owner`.
- `Assets/_Project/Prefabs/Tools/Held`: prefabs `12`, LOD `0`, no LOD `12`, primitive refs `12`, MeshCollider `0`, priority `P2`, disposition `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF`. Product-face prefab folder needs mesh/material/LOD/import proof before route use. Next: `future product-face prefab owner`.
- `Assets/_Project/Prefabs/WorldSupport/Final`: prefabs `9`, LOD `9`, no LOD `0`, primitive refs `9`, MeshCollider `0`, priority `P2`, disposition `UNCLASSIFIED_PREFAB_SOURCE`. Prefab folder needs owner assignment before visible route promotion. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future prefab owner`.
- `Assets/_Project/Prefabs/Resources/Pickups`: prefabs `8`, LOD `0`, no LOD `8`, primitive refs `8`, MeshCollider `0`, priority `P2`, disposition `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF`. Product-face prefab folder needs mesh/material/LOD/import proof before route use. Next: `future product-face prefab owner`.
- `Assets/_Project/Prefabs/Transport`: prefabs `4`, LOD `0`, no LOD `4`, primitive refs `4`, MeshCollider `0`, priority `P2`, disposition `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF`. Product-face prefab folder needs mesh/material/LOD/import proof before route use. Next: `future product-face prefab owner`.
- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final`: prefabs `2`, LOD `2`, no LOD `0`, primitive refs `2`, MeshCollider `0`, priority `P2`, disposition `UNCLASSIFIED_PREFAB_SOURCE`. Prefab folder needs owner assignment before visible route promotion. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future prefab owner`.
- `Assets/_Project/Prefabs/Buildings`: prefabs `1`, LOD `0`, no LOD `1`, primitive refs `1`, MeshCollider `0`, priority `P2`, disposition `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF`. Product-face prefab folder needs mesh/material/LOD/import proof before route use. Next: `future product-face prefab owner`.
- `Assets/_Project/Prefabs/Diagnostics`: prefabs `1`, LOD `0`, no LOD `1`, primitive refs `1`, MeshCollider `0`, priority `P2`, disposition `UNCLASSIFIED_PREFAB_SOURCE`. Prefab folder needs owner assignment before visible route promotion. Next: `MESH_PREFAB_REVIEW_QUEUE_20260605.md / future prefab owner`.

## Rejections

- Do not treat YAML token counts as Unity prefab readback or visual acceptance.
- Do not promote `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` into visible route content.
- Do not use built-in primitive mesh refs as product-face visible meshes.
- Do not raw YAML patch prefab/material/scene files from this matrix.
- Do not claim LOD transition quality, collider correctness, material quality, Addressables residency, SetPass, or runtime performance from this matrix.

## Regression Model

- CPU: static YAML scan only; no runtime CPU change.
- GC: no runtime code changed; no hot-path proof.
- Memory/VRAM: prefab folder risk only; no residency proof.
- SetPass: material/proxy risk identified only; no Frame Debugger proof.
- Correctness: prefab folder ownership is clearer; Unity readback and visual proof remain required.

Final status: `PENDING_VERIFICATION`.
