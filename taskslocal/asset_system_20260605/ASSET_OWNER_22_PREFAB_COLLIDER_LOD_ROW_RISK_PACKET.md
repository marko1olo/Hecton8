# ASSET_OWNER_22 - Prefab Collider / LOD Row Risk Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_YAML_SCAN` + `STATIC_DOC` only.
Scope: future-owner packet for row-level prefab risks recorded in `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.md` and `.csv`.
Boundary: no Unity run, prefab edit, raw YAML mutation, material edit, import, build, Play Mode, screenshot, Stats, Frame Debugger, Addressables build, runtime test, or visual claim was performed.
First-20 route blocker mapped: false promotion risk for visible route prefabs whose static rows show missing LOD token, built-in primitive visual refs, MeshCollider token, product-face scope, proxy/placeholder route, or no renderer token.

## Mandates Followed

- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `3dmodel.md`
- `3DMODEL_HARD_SURFACE_MODULES.md`
- `performance.md`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

## Static Row Risk Summary

Source rows: `602` prefab YAML rows under `Assets/_Project/Prefabs`.

| Static row risk | Row count | Meaning | Required future owner action |
|---|---:|---|---|
| No static `LODGroup` token | 221 | YAML did not contain an `LODGroup` token. This is not proof that Unity lacks LOD wiring. | Unity prefab readback, LODGroup inspection, renderer list inspection, and scene instance readback. |
| Built-in primitive mesh refs | 183 | YAML contained Unity built-in primitive mesh references. | Replace visible product-face/proxy visuals with authored or offline-generated meshes; prove mesh, material, and LOD route. |
| `MeshCollider` token | 76 | YAML contained `MeshCollider`. This does not prove collider authority, convex state, shared mesh path, or runtime cost. | Read back collider components; reject visual LOD0 mesh as collision truth; replace with `COL_*` primitive or bounded convex proxies. |
| Product-face scope flags | 47 | Visible tools, pickups, construction, transport, or building rows are in player-facing scope. | Product-face owner must prove silhouette, materials, LODs, colliders, anchors, and route captures before promotion. |
| Proxy/placeholder route flags | 118 | Rows are proxy or procedural placeholder route material. | Keep out of visible route placement until replaced by final route-owned assets. |
| No renderer token | 23 | YAML had no renderer token. This may be manager/audio/logic prefab or malformed visual asset. | Unity readback must classify manager-only rows separately from visual rows missing renderer ownership. |

## Row Groups For Future Owners

Product-face rows:

| Folder | Rows | Static blocker |
|---|---:|---|
| `Assets/_Project/Prefabs/Tools/Held` | 12 | Primitive visual refs, no static LOD token. |
| `Assets/_Project/Prefabs/Items/Tools` | 12 | Primitive visual refs, no static LOD token. |
| `Assets/_Project/Prefabs/Construction/Final` | 10 | Primitive visual refs; 4 rows have no static LOD token. |
| `Assets/_Project/Prefabs/Resources/Pickups` | 8 | Primitive visual refs, no static LOD token. |
| `Assets/_Project/Prefabs/Transport` | 4 | Primitive visual refs, no static LOD token. |
| `Assets/_Project/Prefabs/Buildings` | 1 | Primitive visual ref, no static LOD token. |

Proxy/placeholder rows:

| Folder | Rows | Static disposition |
|---|---:|---|
| `Assets/_Project/Prefabs/WorldProceduralProxy` | 88 | Visible route placement rejected until replacement. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Flora` | 8 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Construction` | 7 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/TerrainLod` | 5 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/LargeThreats` | 4 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Fauna` | 3 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Debris` | 2 | Placeholder route only. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/Resources` | 1 | Placeholder route only. |

MeshCollider row clusters:

| Folder | Rows | Required split |
|---|---:|---|
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock` | 50 | Prove whether MeshCollider is convex proxy, visual mesh misuse, or source-only blocker. |
| `Assets/_Project/Prefabs/Nature/GOTOVYE_PREFABY_KAMNEY` | 25 | Legacy rock source needs collider proxy proof before route promotion. |
| `Assets/_Project/Prefabs` | 1 | Root-row owner must classify before visible route use. |

No-renderer row clusters:

| Folder | Rows | Required split |
|---|---:|---|
| `Assets/_Project/Prefabs` | 11 | Manager/logic rows versus visual rows with missing renderer proof. |
| `Assets/_Project/Prefabs/Nature/Rocks` | 5 | Source rows need Unity classification. |
| `Assets/_Project/Prefabs/Nature/Rocks/Metki_dlya_narostov` | 3 | Marker rows must stay source/marker only unless visual route is proven. |
| `Assets/_Project/Prefabs/Audio` | 2 | Audio/manager rows are not visual prefab health proof. |
| `Assets/_Project/Prefabs/WorldRuntime` | 1 | Runtime owner classification required. |
| `Assets/_Project/Prefabs/Nature/Rocks/Baked` | 1 | Baked source classification required. |

## Evidence Boundary

- `STATIC_YAML_SCAN` proves only that text tokens were present or absent in prefab YAML rows.
- `STATIC_DOC` proves only that a documented route, blocker, or rule exists.
- Token absence is not Unity component absence.
- Token presence is not component correctness, material quality, collider authority, LOD quality, Addressables residency, SetPass cost, memory residency, or in-game quality.
- The next owner must not use these rows as import, prefab, scene, visual, runtime, GC, or profiler proof.
- The next owner must compare visible prefab route captures against the mandatory visual-reference digest before promotion. Static LOD/collider health cannot excuse a primitive silhouette, proxy material, flat panel, sparse route dressing, or dark/fog-hidden asset.

## Unity Prefab Readback Route

After the process gate clears, the future owner must:

1. Build an explicit target list from the row groups above.
2. Open prefabs through Prefab Stage or a scoped Editor API tool. Do not raw-edit YAML.
3. Read back prefab asset components: renderers, mesh filters, LODGroup, materials, colliders, scripts, anchors, and child names.
4. Read back active scene instances when the prefab is placed on a route.
5. Separate manager/audio/marker/source-only rows from visual prefab rows.
6. Record prefab asset result and scene override result separately.
7. Capture route screenshots, Stats, Frame Debugger, and profiler data only after prefab readback and scoped edits.

## Safe Prefab Workflow

- Do not edit `.prefab`, `.mat`, `.unity`, or `.asset` files as raw YAML.
- Do not use blanket Apply All or Revert All.
- Do not save dirty scenes without scoped dirty-object proof.
- Do not mutate Crest, MapMagic, third-party, or project settings from this packet.
- Preserve prefab GUIDs, gameplay scripts, interaction anchors, Addressables identity, save identity, sockets, pivots, and scene references unless a named owner approves a route change.
- If an override exists, classify it as intentional route data or stale drift before changing it.
- If a prefab was working before a scoped edit and breaks after it, revert the scoped edit and identify the exact broken reference.

## Collider Proxy Requirements

- Visual LOD0 meshes must not be used as production `MeshCollider.sharedMesh`.
- Hard-surface prefabs use `COL_*` compound `BoxCollider`, `CapsuleCollider`, and bounded `SphereCollider` children wherever gameplay contact allows.
- Rocks/geology use primitive decomposition or convex proxy under the documented triangle budget; visual surface triangles stay visual-only.
- Flora/coral use no solid collision by default; use coarse trigger capsules/spheres only for interaction or harvest points.
- Collider children must not move interaction anchors, sockets, hand poses, docking points, or gameplay truth between LODs.
- Future proof must list collider count, type, convex state, owner layer, trigger state, and whether any MeshCollider references a visual mesh.

## LOD Proof Gates

Static `LODGroup` text is not enough. The future owner must prove:

- LOD0, LOD1, LOD2, and cull/HLOD route where object scale or route visibility requires it.
- Props above 0.5 m have at least 3 LOD levels or a documented HLOD/impostor route.
- LOD1 preserves macro silhouette, sockets, and primary bevels.
- LOD2 preserves bounding silhouette and replaces small detail with baked normal/mask detail.
- Transitions use hysteresis and dithered cross-fade where renderer support exists; dense alpha-blend fades are rejected for compact lanes.
- Triangle counts, transition distances, bounds, material slots, and renderer ownership are captured per prefab family.
- Scene route captures show no hard pop, silhouette collapse, primitive shape exposure, or placeholder material exposure.
- Scene route captures include digest comparison notes for bright surface/photic, shoreline, medium-depth, cockpit/visor, or product-face context where the prefab is visible.

## Product-Face Rejection Gates

Reject promotion when any target row still has:

- Built-in primitive visible mesh ref on a tool, pickup, construction, transport, building, or support prefab.
- Missing LOD proof for a visible prop above the size threshold.
- Plain cube/capsule/cylinder silhouette without bevels, seams, trim, panels, wear, gasket, material breakup, or pressure-rated design language.
- Flat unbroken hard-surface panel above the documented size limit.
- Runtime material clone, per-prefab one-off material clone, or undocumented shader variant route.
- Proxy or placeholder folder source placed in a visible route.
- MeshCollider using visual LOD0 geometry.
- Darkness, fog, or post effects used to hide weak mesh or material quality.
- Failure against the mandatory digest's surface, shoreline, photic, medium-depth, or cockpit/visor visual floor.

## Rollback Conditions

Future prefab edits must be reverted when:

- Unity readback shows lost script reference, missing renderer, moved gameplay anchor, broken socket, changed prefab identity, or scene override drift.
- Collider replacement changes player contact, interaction reach, docking/hand pose, harvest trigger, or traversal truth without owner approval.
- LOD replacement causes silhouette collapse, hard pop, visible primitive fallback, or missing cull behavior.
- Material replacement creates unique material proliferation, lost texture role, SRP Batcher risk, or visual quality below route floor.
- Addressables identity, scene reference, save identity, or prefab GUID route changes without a named owner.
- Stats, Frame Debugger, profiler, or memory evidence shows a regression after scoped edit.

## Regression Model

- CPU: risk from added renderer count, LODGroup evaluation, collider count, convex collider cost, shadow caster count, and prefab instance count. Static packet makes no CPU claim.
- GC: no runtime code touched; no allocation claim. Future owner must reject runtime mesh generation, runtime material clone creation, `Resources.Load`, hot scene search, or per-frame string work introduced by prefab scripts.
- Memory/VRAM: risk from replacement meshes, texture sets, material slots, shadow maps, longer LOD residency, and Addressables residency. Static rows do not prove loaded memory.
- SetPass/batches: risk from material slot growth, unique shader variants, clone materials, and renderer count. Future proof needs Frame Debugger/Stats.
- Cadence: no runtime cadence changed by this packet. Future LOD and visual updates must use continuous `GlobalQualityWeight`, hysteresis, and authored load shedding.
- Correctness: risk centers on prefab identity, collision truth, interaction anchors, sockets, scene overrides, and route ownership.
- Visual floor: primitive silhouettes, proxy materials, flat panels, missing bevels, weak LODs, blurry textures, or hidden-by-darkness output are rejected.

## Continuous GlobalQualityWeight Consequences

These checkpoints describe one continuous `GlobalQualityWeight` curve, not binary switches.

- Low/compact, about `0.0-0.25`: final proven silhouettes only. Keep bevels on visible hard edges, baked AO, compressed PBR maps, stable collider proxies, dithered LOD where supported, and readable route function. Reduce density, texture residency, shadow eligibility, and LOD distance smoothly; never substitute primitive visible meshes.
- Middle, about `0.25-0.55`: maintain full product-face material identity, stable LOD transition bands, proxy collider truth, and route-readable tools/pickups/support assets.
- High, about `0.55-0.85`: extend LOD0/LOD1 residency, add richer trim/wear/detail normals, keep stronger labels/emissive/display masks, and increase near-field dressing after measured proof.
- Ultra, about `0.85-1.0`: spend saved budget on denser bevels, layered panel detail, wetness, glass/display response, richer decals, longer LOD residency, and stronger route dressing. Gameplay truth, prefab identity, collider authority, save identity, material channel semantics, and DTO routes stay unchanged.

## Future Owner Execution Order

1. Confirm process gate: no build/import/compiler contention and Unity safe for scoped prefab work.
2. Target row group: product-face, proxy/placeholder, MeshCollider, or no-renderer classification.
3. Read back prefab asset and active route instances.
4. Classify manager/source rows separately from visual asset rows.
5. Replace visible primitive/proxy visuals with authored or offline-generated meshes only after target proof exists.
6. Add `COL_*` proxy children or bounded convex proxies; remove visual LOD0 collider misuse.
7. Bind shared `MAT_*` materials and documented texture roles.
8. Record LOD, collider, material, route screenshot, Stats/Frame Debugger, profiler, memory, and Addressables evidence as applicable.
9. Revert scoped edits on any rollback condition above.

Final status: `PENDING_VERIFICATION`.
