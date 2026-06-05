# ASSET_OWNER_13 - Product-Face Prefab Primitive Replacement Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_YAML_SCAN` + `STATIC_DOC` only.
Scope: execution packet for replacing product-face primitive visual meshes in tools, held tools, construction, transport, resources, buildings, world support, and root prefabs.
Boundary: no Unity run, prefab edit, material edit, import, build, Play Mode, screenshot, Stats, Frame Debugger, Addressables build, or runtime test was performed.
First-20 route blocker mapped: false promotion risk for visible tools, pickups, construction pieces, transport props, buildings, and support prefabs on the first-exit/photic/medium-depth route.

## Mandates Followed

- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

## Static Blocker Groups

Source: `Docs/AssetAudit/MESH_PREFAB_SOURCE_FOLDER_ROUTE_MATRIX_20260605.md` and `.csv`.

| Folder | Static count | Static blocker | Disposition |
|---|---:|---|---|
| `Assets/_Project/Prefabs/Construction/Final` | 10 prefabs; 6 with LODGroup; 4 without LODGroup; 10 primitive refs | Final-intent construction prefabs still use built-in primitive visual meshes and incomplete LOD coverage. | `PRODUCT_FACE_REJECT_STATIC` |
| `Assets/_Project/Prefabs/Tools/Held` | 12 prefabs; 0 LODGroup; 12 primitive refs | Held tool prefabs need authored/generated mesh, material, LOD, and import proof before route use. | `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF` |
| `Assets/_Project/Prefabs/Items/Tools` | 12 prefabs; 0 LODGroup; 12 primitive refs | Tool item prefabs need authored/generated mesh, material, LOD, and import proof before route use. | `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF` |
| `Assets/_Project/Prefabs/Resources/Pickups` | 8 prefabs; 0 LODGroup; 8 primitive refs | Pickup prefabs need authored/generated mesh, material, LOD, and import proof before route use. | `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF` |
| `Assets/_Project/Prefabs/Transport` | 4 prefabs; 0 LODGroup; 4 primitive refs | Transport prefabs need authored/generated mesh, material, LOD, and import proof before route use. | `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF` |
| `Assets/_Project/Prefabs/Buildings` | 1 prefab; 0 LODGroup; 1 primitive ref | Building prefab needs authored/generated mesh, material, LOD, and import proof before route use. | `PRODUCT_FACE_SOURCE_NEEDS_MATERIAL_MESH_PROOF` |
| `Assets/_Project/Prefabs/WorldSupport/Final` | 9 prefabs; 9 with LODGroup; 9 primitive refs | Support prefabs have LODGroup tokens but still use primitive visual refs; owner assignment and visual replacement are required. | `UNCLASSIFIED_PREFAB_SOURCE` |
| `Assets/_Project/Prefabs` | 21 prefabs; 0 LODGroup; 6 primitive refs; 1 MeshCollider token | Root prefab folder needs owner assignment before visible route promotion. Primitive refs and MeshCollider token require scoped proof. | `UNCLASSIFIED_PREFAB_SOURCE` |

Related hard rejects, not this packet's visible replacement source: `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` remain `REJECT_VISIBLE_ROUTE_PLACEMENT` until replaced by final route-owned prefabs.

## Mandatory Visual Reference Alignment

Future route screenshots must compare product-face replacements against:

`Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

Required consequences:

- Tools, pickups, construction, transport, buildings, and support prefabs must survive bright surface/photic, shoreline, medium-depth, and cockpit/visor contexts without reading as primitive shapes with textures.
- Sky/ocean-facing product assets must not camouflage weak `BEST ILLUST`-level surface requirements: bright coastline/ocean read, Aegir/sky scale, waterline contact, and route composition.
- Medium-depth proof must preserve silhouette, material identity, and functional read under darker water and biolum anchors; it cannot hide weak assets with black fog or bloom.

## Replacement Requirements

- Replace built-in primitive visual meshes with authored or offline-generated hard-surface meshes. Product-face construction, tools, transport, pickups, buildings, and support prefabs must read as manufactured, handled, pressure-rated equipment, not cubes/capsules with materials.
- Each visible hard edge above the domain threshold needs bevel/chamfer support. Flat unbroken panels larger than 1.5 m need seams, trim, access plates, bolts, decals, grime/wear masks, or material breakup.
- Required LOD chain: `LOD0`, `LOD1`, `LOD2`, and cull/HLOD where object scale or route visibility requires it. Props larger than 0.5 m need at least 3 LOD levels. Product-face hero/held tools need close-view silhouette proof.
- Material texture roles must be documented per asset family: albedo, normal, MRAO, emission/decal/detail when used. Albedo is sRGB; normal and masks are linear; normal maps use BC5 where valid; albedo/MRAO use compressed high-quality platform formats.
- Material slots must stay meaningful and batchable: primary structure, exposed wear/trim, gasket/rubber/glass/secondary, emissive/decal/special. More than four slots requires proof.
- Use shared `MAT_*` assets and `TX_*` texture roles. No runtime material clones, no per-prefab clone materials, no runtime texture generation.
- Visual LOD0 meshes must not be assigned to production `MeshCollider`. Collision uses `COL_*` primitive children or bounded convex proxy proof.
- Pivots, sockets, hand/contact zones, interaction anchors, and collider truth must not move across LODs or quality checkpoints.

## Safe Unity Route After Gate Clears

1. Open each prefab through Unity-safe prefab workflow only. Use Prefab Stage or an Editor API tool with explicit target list after CPU/process gate clears.
2. Do not raw YAML patch `.prefab`, `.mat`, `.unity`, or `.asset` files.
3. Do not use blanket Apply All or Revert All. Apply only scoped prefab changes and read back the prefab asset plus any active scene instance.
4. Do not mutate third-party/Crest/MapMagic materials or create runtime wrappers. Assign approved shared materials only.
5. Do not use visual mesh triangles as collider truth. Add or replace `COL_*` proxy children and prove no LOD0 visual mesh is used by `MeshCollider`.
6. Preserve prefab identity, gameplay scripts, interaction anchors, Addressables keys, and scene references unless the owner explicitly approves a route change.
7. If a scene instance overrides prefab values, document the override and prove whether it is intentional. No scene save without scoped dirty-object proof.

## Acceptance Gates

Static documents do not clear these gates.

- Unity prefab readback: each target prefab shows final mesh assets, no built-in primitive visual mesh refs, expected renderer/material slots, and required LODGroup chain.
- Scene instance readback: active route instances match prefab intent or have documented intentional overrides.
- Material/import proof: texture role paths, import settings, compression, mip chain, shader assignment, and SRP Batcher compatibility are proven in Unity.
- LOD proof: triangle counts, transition distances, dither/crossfade behavior, silhouette captures, and cull/HLOD behavior are recorded.
- Collider proof: `COL_*` proxy layout, primitive/convex budget, interaction trigger layout, and no visual LOD0 MeshCollider misuse.
- Route screenshot proof: bright surface/photic/medium-depth route captures show product-face tools/pickups/construction/transport/support assets without primitive silhouettes or placeholder materials.
- Visual-reference comparison proof: route captures name the digest signals passed or failed for surface, shoreline, photic, cockpit/visor, and medium-depth contexts.
- Stats/Frame Debugger proof: SetPass, batches, shadow casters, material variants, and renderer ownership stay inside budget.
- Addressables/residency proof: required before broad placement, streaming groups, or route-wide promotion. Must include load/release handle ownership and memory/residency evidence.

## Regression Model

- CPU: risk from added renderer count, collider count, LODGroup evaluation, shadow casters, and Addressables load dispatch. Gate with Unity Stats/Profiler; any system over `0.1ms` needs load-shed proof.
- GC: prefab replacement must not add hot-path allocation, runtime mesh generation, runtime material clones, `Resources.Load`, or per-frame string/UI mutation. Required runtime claim remains `PENDING_VERIFICATION` until GCMonitor/Profiler proof.
- VRAM: risk from new albedo/normal/MRAO/emission textures and longer LOD residency. Compact ceiling remains 1800 MB VRAM and 900 MB texture budget; mip downgrade triggers at used/total > 0.90.
- SetPass/batches: risk from material proliferation and unique shader variants. Shared materials, atlases/trim sheets, SRP Batcher, GPU Resident Drawer, and bounded slots are required.
- Correctness: collider proxy, interaction anchor, socket, pivot, save identity, and Addressables identity must not change without owner approval.
- Visual floor: primitive silhouettes, flat colors, missing bevels, blurry textures, proxy materials, and darkness/fog hiding weak assets are rejected.

## Continuous GlobalQualityWeight Consequences

These are checkpoints on one continuous `GlobalQualityWeight` curve, not binary modes.

- Low/compact, about `0.0-0.25`: use final proven meshes only. Keep bevels on inspected edges, readable function silhouettes, baked AO, compressed PBR maps, dithered LOD where supported, and proxy colliders. Reduce density, texture residency, shadow eligibility, and LOD residency smoothly; never substitute primitive visuals.
- Middle, about `0.25-0.55`: keep full route-owned PBR material stacks, stable LOD transitions, documented collider proxies, and final non-proxy materials. Product-face props must remain readable in first-person/near-route views.
- High, about `0.55-0.85`: extend LOD0/LOD1 residency, increase decal/wear/detail-normal density, keep richer emissive/display masks, and allow denser near-field dressing where Stats/Frame Debugger permits.
- Ultra, about `0.85-1.0`: spend saved budget on stronger bevel density, richer trim, labels, edge wear, wetness, glass/display response, longer LOD residency, and higher near-field route density. Gameplay truth, prefab identity, collider authority, material channel semantics, and save identity stay unchanged.

## Execution Order

1. Gate process safety: no active build/import/compiler and Unity safe for prefab work.
2. Build target list from the blocker groups above; exclude proxy/placeholder folders from promotion.
3. For each family, select or generate offline hard-surface mesh replacements with LOD0/1/2/cull and `COL_*` proxies.
4. Bind shared material families with documented texture roles and import settings.
5. Apply scoped prefab edits through Unity-safe workflow.
6. Read back prefab asset and active scene instances.
7. Capture route screenshots and Stats/Frame Debugger evidence.
8. Only after broad placement is intended, produce Addressables/residency proof.

Final disposition: `PENDING_VERIFICATION` until Unity prefab/material/LOD/collider/route/performance proof exists.
