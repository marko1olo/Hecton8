# 3107 Product-Face Prefab Placement Prep

Status: STATIC AUDIT / PLACEMENT BLOCKED
Evidence class: `STATIC_SOURCE`, `STATIC_PREFAB_YAML`, `STATIC_MATERIAL_YAML`, `STATIC_REPORT`.

## Mandates Followed

- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Verdict

Do not place product-face objects in Unity yet.

Base water/terrain/sky and active photic proxy material blockers remain. Object scatter would hide the failure instead of fixing it. `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` are rejected for visible production placement.

First-20-minutes impact: removes a route blocker for the beautiful semi-open shallow first exit. It prepares object placement rules but does not improve the route in Unity yet.

## Classification

| Pool | Static Facts | Classification | Reason |
|---|---:|---|---|
| `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` | 49 prefabs, 49 `LODGroup`, 49 collider-bearing, 0 `MeshCollider`, 0 built-in mesh refs, 0 missing scripts | `NEEDS MATERIAL / NEEDS UNITY VISUAL PROOF` | Strongest geology candidate pool. Bound to imported shared rock material `mat_Rock_Shared.mat`; material YAML still shows multiple null texture slots. Use only after Unity close/route capture proves wet geology material truth. |
| `Assets/_Project/Prefabs/Nature/Flora/Baked` | 89 prefabs, 89 `LODGroup`, 0 colliders, 0 built-in mesh refs, 0 missing scripts | `NEEDS MATERIAL / NEEDS ROUTE PROOF` | Mesh/LOD package is cleaner, but all families bind to `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_*`. Current Batch31 material criticals block proxy-bound photic placement. No colliders is acceptable for most flora/coral unless it blocks path or is harvestable. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp` | 100 prefabs, 100 `LODGroup`, 0 colliders, 0 built-in mesh refs | `NEEDS MATERIAL / UNKNOWN VISUAL QUALITY` | Bound to `MAT_ProceduralBio_Shallows.mat` under `WorldProceduralProxy`. Potential shallow-biome candidate after material rebinding and screenshot proof. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral` | 50 prefabs, 50 `LODGroup`, 0 colliders, 0 built-in mesh refs | `NEEDS MATERIAL / UNKNOWN VISUAL QUALITY` | Same proxy material blocker as BioForge kelp. No collider is acceptable unless route-blocking or harvestable. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock` | 50 prefabs, 50 `LODGroup`, 50 `MeshCollider`, 0 built-in mesh refs | `NEEDS COLLIDER / BLOCKED` | Uses `MeshCollider` against visual mesh refs. Violates geology collider law until replaced by primitive/convex proxy or SDF/nav proxy. |
| `Assets/_Project/Prefabs/Construction/Final` | 10 prefabs, 6 `LODGroup`, 10 collider-bearing, 0 `MeshCollider`, 10 with built-in primitive mesh refs | `NEEDS LOD / NEEDS MATERIAL / REJECT FOR PRODUCT-FACE UNTIL MESH AUDIT` | Useful route-hardware intent, but primitive mesh refs and missing LOD on 4 prefabs block first-viewport placement. Names marked Final do not prove final quality. |
| `Assets/_Project/Prefabs/WorldSupport/Final` | 9 prefabs, 9 `LODGroup`, 9 collider-bearing, 0 `MeshCollider`, 9 with built-in primitive mesh refs | `REJECT VISIBLE / SUPPORT-ONLY` | Support markers/zones may carry editor or systems meaning, but primitive visible geometry is not product-face art. |
| `Assets/_Project/Prefabs/WorldProceduralProxy` | 88 prefabs, 0 `LODGroup`, 0 colliders, 88 with built-in primitive mesh refs, proxy materials | `REJECT VISIBLE PLACEMENT` | Prototype/proxy lane. No LOD/collider package. Uses primitive Unity mesh refs and `WorldProceduralProxy` materials. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders` | 30 prefabs, 0 `LODGroup`, 0 colliders, 30 with built-in primitive mesh refs, 30 placeholder markers | `REJECT VISIBLE PLACEMENT` | Runtime placeholder lane only. Must not contaminate route dressing or screenshots. |

No pool is `USABLE NOW` for product-facing placement because no Unity material readback, screenshot proof, LOD transition proof, collider readback, or compact/high capture exists.

## Staged Placement Plan

1. Gate 0: base route recovery.
   - Water, terrain, sky, Aegir, and photic material blockers must have Unity readback and captures.
   - Do not use objects to hide broken surface, terrain, sky, or proxy coral.

2. Gate 1: material rebinding.
   - Flora/coral/BioForge candidates must leave `WorldProceduralProxy` materials or receive explicit Unity-owner proof that the bound material is final route-owned.
   - Rocks need `mat_Rock_Shared.mat` texture-role proof or rebinding to a route wet-basalt/geology material.
   - Hard-surface route hardware needs real PBR texture roles, not color-only materials.

3. Gate 2: asset package validation.
   - LOD0/LOD1/LOD2 or HLOD route.
   - No built-in primitive mesh refs for visible product-facing assets.
   - No `MeshCollider` on LOD0 visual meshes.
   - No missing scripts, default material, runtime material clone, or placeholder marker.

4. Gate 3: placement purpose.
   - Every object must serve one of: route landmark, scale witness, return cue, ecology cause, salvage/evidence cue, threat staging, substrate/material witness, or route hardware.
   - Random scatter and cosmetic clutter are rejected.

5. Gate 4: substrate/contact.
   - Rocks: embedded, sedimented, wetted, or contact-blended.
   - Flora/coral: anchored by light/current/substrate/depth logic.
   - Debris/hardware: follows collapse, current, salvage, cable, or failed engineering logic.
   - No floating roots, hovering rocks, unscaled debris, or unsupported cables.

6. Gate 5: proof packet.
   - Normal-tier and compact-tier gameplay-height captures.
   - Close material capture for rocks/flora/hardware.
   - LOD/collider proof from Unity inspector or validation tool.
   - Rendering stats only after scene/import state is clean.

## Candidate Use After Gates

- Geology first: `ProceduralFinals` arches, shelves, clusters, floors, and spires as route anchors, scale witnesses, cave-mouth frames, and return-path silhouettes.
- Flora/coral second: baked coral/kelp only after final material proof; use as ecological logic and route color, not carpet fill.
- BioForge shallow kelp/tube coral third: only after proxy material replacement and visual proof.
- BioForge porous rocks last: blocked until collider proxies replace `MeshCollider`.
- Construction/Final hardware: only select pieces after primitive-mesh audit; use for route hardware, salvage cuts, pumps, power relay silhouettes, and evidence.
- WorldSupport/Final: system markers only; visible geometry rejected unless rebuilt.

## Low / Middle / High / Ultra Consequences

- Low: sparse but deliberate anchored assets, strong silhouettes, BC-compressed shared materials, no proxy clutter, no floating objects.
- Middle: more material witnesses, richer contact blending, controlled coral/flora variety, better route landmarks.
- High: denser near-field dressing, longer LOD residency, stronger wetness/detail normals, more route hardware evidence.
- Ultra: extra ecological density and material overkill only after low-tier composition, frame, memory, and GC proof hold.

## Regression Model

- CPU: no runtime placement or code changed; placement plan must later use static prefabs, instancing/GRD/BRG, and Addressables residency.
- GC: no runtime code changed; later placement must target 0 B/frame and avoid hot scene search/material clones.
- Memory/VRAM: material rebinding and density must fit 900 MB texture budget and 1800 MB compact VRAM ceiling; pressure response must degrade mips/density without ugly mode.
- Cadence: staged placement waits for base route and proof gates; no same-frame scatter rebuild or runtime generation.
- Correctness: visible placement remains blocked until each asset has purpose, substrate logic, collider route, and material truth.

## Current Status

`PENDING VERIFICATION`.

Static classification is complete. Unity placement, visual acceptance, profiler proof, and runtime readiness are absent.
