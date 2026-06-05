# Mesh/Prefab Promotion Static Table - Asset Worker 3214 - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence classes used: `STATIC_SOURCE`, `STATIC_YAML_SCAN`.
Scope: static candidate/reject table only. Unity, dotnet, screenshots, prefab mutation, material mutation, and scene saves were not run.
First-20 route blocker addressed: false promotion risk for bright surface, first-exit, photic shallows, and medium-depth route placement.

Mandates loaded:

- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Authority files loaded: `AGENTS.md`, `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`, `3dmodel.md`, `3DMODEL_GEOLOGY_ROCKS.md`, `3DMODEL_FLORA_CORAL.md`, `3DMODEL_TEXTURES_MATERIALS.md`.

No visual acceptance is claimed. Static LOD/material references prove only disk structure and YAML reachability.

## Static Promotion Table

| Pool/path | Count sampled/scanned | LODGroup status | Primitive mesh refs | Proxy/placeholder/null material refs | Collider risk | Material proof blocker | Promotion disposition | Next proof required |
|---|---:|---|---|---|---|---|---|---|
| `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` | 49/49 prefabs | 49/49 have LODGroup | 0/49 built-in primitive mesh refs; 0 null mesh refs | 0 proxy material refs; 0 placeholder material refs; 0 null material slots; 0 unresolved prefab material GUIDs | 49/49 primitive collider components; 0 MeshCollider. Acceptable only if collider fitting is intentional and route-owned. | All prefabs resolve to one shared rock material: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`. Static material scan shows `SG_Rock_Triplanar.shadergraph` and texture refs, but Unity import/readback and unresolved texture GUID review remain pending. | `CANDIDATE_GEOMETRY_STATIC_ONLY`. Candidate pool can remain in promotion queue, not product-face accepted. | Unity readback for mesh/material slots; import status for rock textures and shadergraph; collider proxy readback; LOD triangle/transition proof; route screenshots; Stats/Frame Debugger/GC proof. |
| `Assets/_Project/Prefabs/Nature/Flora/Baked` | 89/89 prefabs; 267 mesh-like assets under pool | 89/89 have LODGroup | 0/89 built-in primitive mesh refs; 0 null mesh refs | 89/89 resolve to `WorldProceduralProxy` materials; 0 null material slots; 0 unresolved prefab material GUIDs | 0 colliders, 0 MeshCollider. Low physics risk; interaction/collision still unproven if promoted. | Nine proxy material assets under `Assets/_Project/Art/Materials/WorldProceduralProxy/` are assigned. Texture stacks may exist, but material route is still proxy-labeled and Unity visual proof is absent. | `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`. Geometry can remain a candidate. Visible route placement rejected until material ownership is corrected/proven. | Rebind or prove final non-proxy material route; vertex color R/G/B/A semantic proof; alpha-clip/dither proof; LOD silhouette proof; screenshots in photic route; Frame Debugger/Stats/GC proof. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows` | 200/200 prefabs | 200/200 have LODGroup | 0/200 built-in primitive mesh refs; 0 null mesh refs | 200/200 resolve to `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows.mat`; 0 null material slots; 0 unresolved prefab material GUIDs | 50/200 MeshCollider refs, all under `PorousRock`; `Kelp` 100/100 no MeshCollider; `TubeCoral` 50/50 no MeshCollider. | Single shared proxy material blocks final placement for every prefab. PorousRock also violates final collider-proof expectations until MeshCollider role, convexity, and triangle budget are proven. | `CANDIDATE_SOURCE_POOL_SPLIT`. Kelp/TubeCoral remain candidate source meshes blocked by material proof. PorousRock remains route-rejected until collider proxy proof exists. | Separate candidate lists by family; replace/prove final material; audit PorousRock MeshCollider route; prove no visual mesh collider; screenshot Kelp/TubeCoral/PorousRock under final material; profiler/Frame Debugger proof. |
| `Assets/_Project/Prefabs/WorldProceduralProxy` | 88/88 prefabs | 0/88 have LODGroup | 88/88 built-in primitive mesh refs | 88/88 resolve to `Assets/_Project/Art/Materials/WorldProceduralProxy/` materials; 0 null material slots; 0 unresolved prefab material GUIDs | 0 colliders found in static scan. Visual risk dominates. | Material path and prefab path both identify proxy use. No LOD chain. Primitive mesh refs. | `REJECT_VISIBLE_ROUTE_PLACEMENT`. May remain as editor/proxy/reference only. No static evidence contradicts rejection. | Replace with final generated/authored mesh pools; add LODGroup chain; use final material assets; then Unity readback, route screenshot, Stats/Frame Debugger, and Addressables/residency proof. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders` | 30/30 prefabs | 0/30 have LODGroup | 30/30 built-in primitive mesh refs | 30/30 resolve to `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/` placeholder materials; 0 null material slots; 0 unresolved prefab material GUIDs | 0 colliders found in static scan. Visual risk dominates. | Placeholder material path is explicit. Primitive mesh refs and no LOD chain block visible placement. | `REJECT_VISIBLE_ROUTE_PLACEMENT`. Runtime placeholder pool is not product-face content. No static evidence contradicts rejection. | Replace with final route-owned prefabs or hide from visible placement; prove final mesh/material/LOD route in Unity before any route screenshot. |
| `Assets/_Project/Prefabs/Construction/Final` | 10/10 prefabs | 6/10 have LODGroup; 4/10 have no LODGroup | 10/10 built-in primitive mesh refs | 0 proxy material refs; 0 placeholder material refs; 0 null material slots; 0 unresolved prefab material GUIDs | 10/10 primitive collider components; 0 MeshCollider. Collider shape may be cheap enough, but visual mesh is primitive. | Materials are non-proxy assets, but 8/9 referenced construction/resource/support materials have zero non-null texture bindings in static YAML; `Mat_RuinSeepSheen` has one. Their shared shader GUID `933532a4fcc9baf4fa0491de14d08ed7` is referenced widely, but no owning `.meta` was found by static `Assets/Packages/ProjectSettings` search in this pass. | `PRODUCT_FACE_REJECT_STATIC`. Useful intent only. Primitive visual meshes block product-face placement. | Replace primitive visual meshes with authored/generated hard-surface modules, add LODGroup to missing 4 prefabs, prove material shader/texture route, verify prefab/scene instances in Unity, then screenshot and performance proof. |

## Top Static Candidates

- `Nature/Rocks/ProceduralFinals`: strongest geometry candidate from static evidence: complete LODGroup coverage, no built-in primitive visual meshes, no proxy/null prefab materials, no MeshCollider.
- `Nature/Flora/Baked`: valid candidate mesh pool only after material route is no longer proxy-labeled or Unity proves the assigned route is final.
- `Nature/Flora/BioForge/Shallows/Kelp` and `TubeCoral`: candidate source meshes blocked by shared proxy material and missing visual proof.

## Top Static Rejections

- `WorldProceduralProxy`: hard reject for visible route placement. Static scan found 88/88 primitive visual mesh refs and 0/88 LODGroups.
- `WorldRuntime/ProceduralPlaceholders`: hard reject for visible route placement. Static scan found 30/30 primitive visual mesh refs, 30/30 placeholder material refs, and 0/30 LODGroups.
- `Construction/Final`: product-face reject from static evidence. Every prefab still references built-in primitive visual meshes; 4/10 lack LODGroup.
- `BioForge/Shallows/PorousRock`: route reject until MeshCollider usage is proven as a proper collision proxy route, not decorative visual collision.

## GlobalQualityWeight Consequences

These are continuous checkpoints, not binary quality switches. `GlobalQualityWeight` may scale LOD residency, instance density, texture/detail residency, and optional dressing. It must not change prefab identity, collider authority, material channel semantics, save identity, or gameplay truth.

| Checkpoint | Consequence for candidate promotion |
|---|---|
| Low / compact, about 0.0-0.25 | Use only final proven meshes, not proxy/placeholder pools. Keep silhouettes, material identity, baked AO, dithered LOD, and route readability. Reduce density and residency smoothly; do not replace visible assets with primitive proxies. |
| Middle, about 0.25-0.55 | Full route-owned PBR stacks, stable LOD crossfade, final non-proxy materials, and resolved collider proxies are required before placement. Flora/coral uses alpha clip/dither, not alpha blend fields. |
| High, about 0.55-0.85 | Extend LOD0/LOD1 residency, add denser near-field rocks/flora, stronger detail normals/masks, and richer material response. No new gameplay truth route. |
| Ultra, about 0.85-1.0 | Spend saved cost on visual overkill: richer geology breakup, coral pore/detail density, wetness/edge masks, higher route dressing density, and stronger material layering. Do not promote rejected proxy pools just because hardware is strong. |

## Regression Model

- CPU: no runtime code changed. Future promotion must prove no renderer/collider submission regression.
- GC: no runtime code changed. Future route placement must prove 0 B/frame hot path.
- Memory/VRAM: static prefab/material presence only. Addressables, residency, texture import, and mip behavior are unproven.
- Cadence: no runtime cadence changed.
- Correctness: this table reduces false visual promotion risk by separating static geometry candidates from proxy/placeholder/product-face rejects.

## Final Static Disposition

`ProceduralFinals` rocks remain static geometry candidates. `Flora/Baked` and BioForge Kelp/TubeCoral remain candidate source pools blocked by material/readback proof. `WorldProceduralProxy`, `WorldRuntime/ProceduralPlaceholders`, `Construction/Final`, and BioForge PorousRock for route placement remain rejected from product-face visible placement until the named proof exists.
