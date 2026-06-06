# Flora Geology Placement Prep Synthesis - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`, `STATIC_ASSET_TEXT`, `STATIC_YAML_READBACK`.
Owner packet: `taskslocal/night_controller_20260605/NIGHT_OWNER_08_FLORA_GEOLOGY_PLACEMENT_PREP.txt`.

This is not production placement approval. No Unity import, scene save, prefab edit, material edit, Play Mode, profiler, or screenshot capture was performed. Current process gate is red: CPU sample `100`, active `dotnet`, Unity Hub, and Unity licensing processes. Unity readback and any editor mutation remain blocked.

## Mandates Followed

- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `world.md`
- `terrain.md`
- `ecosystem.md`
- `3dmodel.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`

## Current Verdict

The project has enough source pools to prepare premium flora, coral, and geology placement. It does not have enough proof to place them into the production surface route tonight while water, shoreline, terrain, sky, Aegir, player/HUD, and proof tooling are still rejected or unproved.

Hard rejection: do not use flora, coral, rocks, fog, or dense dressing to hide slab water, black terrain undersides, weak sky, bad Aegir, missing HUD, or absent player route proof.

## Static Candidate Pools

| Pool | Static facts | Verdict |
|---|---|---|
| `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` | 49 prefabs; static matrix says 49/49 have `LODGroup`, no built-in primitive visual mesh refs, no `MeshCollider`; sample prefabs include `PFB_Geo_CaveEntrance_00.prefab`, `PFB_Geo_LandmarkSpire_00.prefab`, `PFB_Geo_RockArch_00.prefab`. | `CANDIDATE_GEOMETRY_STATIC_ONLY`; strongest geology source, but needs Unity material, collider proxy, LOD transition, route screenshot, Stats, and Frame Debugger proof. |
| `Assets/_Project/Prefabs/Nature/Flora/Baked` | 89 family prefabs; static matrix says 89/89 have `LODGroup`, no primitive visual mesh refs; families include coral branching/brittle/low/massive/plate and kelp abyssal/canopy/patch_dense/tall. | `CANDIDATE_MESH_POOL_BLOCKED_BY_MATERIAL`; usable only after non-proxy final material proof and silhouette capture. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp` | 100 prefabs; 100/100 have `LODGroup`, no primitive visual mesh refs, no `MeshCollider`. | `CANDIDATE_SOURCE_POOL_BLOCKED_BY_MATERIAL`; good shallow density source if material/import/readback pass. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral` | 50 prefabs; 50/50 have `LODGroup`, no primitive visual mesh refs, no `MeshCollider`. | `CANDIDATE_SOURCE_POOL_BLOCKED_BY_MATERIAL`; candidate reef detail after material proof. |
| `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported` | 11 imported families with albedo/detail/mask/normal sources: coral branching, branching v2, brittle, low, massive, massive 2, plate, kelp abyssal, canopy, patch dense, tall. | Source candidates only; no binding, import, color-space, compression, atlas, or visual acceptance proof. |
| `Assets/_Project/Data/World/ProceduralPlacementRules` and `ProceduralFamilies` | Rule/family assets exist for coral, kelp, rocks, reef, landmark spire, safe/resource/hazard pockets, ruins, fauna zones, debris. | Placement grammar source exists; production route still needs Unity readback of actual consumers and deterministic mask/seed proof. |
| `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605` | Promotion-prep source sets exist for `WetBasaltShoreline`, `PhoticSeabedSubstrate`, and `PhoticShellSandSubstrate`. | Terrain material source candidates only; import/binding/channel semantics/tiling proof absent. |

## Rejected Or Blocked Pools

| Pool | Reason |
|---|---|
| `Assets/_Project/Prefabs/WorldProceduralProxy` | Static matrix: 88/88 primitive visual mesh refs, 0/88 `LODGroup`, proxy materials. Visible route placement is rejected. |
| `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders` | Static matrix: 30/30 primitive visual mesh refs, 0/30 `LODGroup`, placeholder materials. Visible route placement is rejected. |
| `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock` | 50/50 have `LODGroup` but 50/50 include `MeshCollider`; collider route is rejected until visual mesh vs collision proxy ownership is proven. |
| `Assets/_Project/Prefabs/Nature/GOTOVYE_PREFABY_KAMNEY` | 25/25 have `LODGroup`, but 25/25 include `MeshCollider` refs in static matrix. Candidate only after collider proxy proof; not compact fallback. |
| `Assets/_Project/Prefabs/Nature/OrganicMisc/Final` | 2/2 have `LODGroup`, but 2/2 have built-in primitive visual mesh refs. Rejected for product-facing placement. |
| Any `WorldProceduralProxy` material on active world renderers | Active-route contamination blocker. Must be replaced or proven not visible before flora/coral density claims. |

## Placement Rules For Later Unity Owner

1. Base gate first: surface water, shoreline contact, terrain material, sky/Aegir, player, HUD, and tool route must be non-rejected before final dressing.
2. Route grammar first: place geology to form shelves, return-path landmarks, cave mouth framing, foam/wetness edges, and safe/unsafe pockets; do not evenly scatter rocks.
3. Ecology reason required: flora/coral follows light, current, shelter, substrate, food/cover logic, and industrial contamination; random decorative density is rejected.
4. Negative space is mandatory: keep player route readability, oxygen return line, tool targets, salvage evidence, and camera sightlines clear.
5. Near field: use `ProceduralFinals` geology and baked/BioForge flora only after material proof; no `WorldProceduralProxy`.
6. Mid field: reduce density, preserve silhouette landmarks, use dithered LOD transitions; no low-poly silhouette collapse.
7. Far field: HLOD/impostor/card or GPU Resident Drawer route only when renderer ownership and Frame Debugger proof exist.
8. Collision: geology collision must be primitive/convex/SDF proxy; flora/coral collision is none by default except coarse harvest/root blockers. LOD0 visual `MeshCollider` is rejected.
9. Materials: final PBR roles must be documented; imported textures are source candidates until sRGB/linear, compression, mip, normal, MRAO channel, and binding proof exists.
10. Proof shots must include compact and high route views, not only pretty isolated prefab shots.

## Future Unity Readback Fields

- Active scene renderer users of `WorldProceduralProxy` materials.
- Actual material slots on `ProceduralFinals`, Baked flora, BioForge kelp, BioForge tube coral, PorousRock, and `GOTOVYE_PREFABY_KAMNEY`.
- `LODGroup` transitions, fade mode, renderer counts, and cull distances for each candidate pool.
- Any `MeshCollider` component using visual LOD mesh assets.
- Renderer static flags, batching/static/GPU Resident Drawer eligibility, and shadow flags.
- Texture import roles for flora/coral/geology materials: albedo sRGB, normal type/BC5, mask linear, mip/streaming, max size.
- Scene placement consumers of `ProceduralPlacementRules` and `ProceduralFamilies`.
- Stats/Frame Debugger: SetPass, batches, draw calls, shadow casters, texture memory after any placement.

## Low / Middle / High / Ultra Consequences

- Low: sparse but premium silhouettes, clean landmarks, no proxy materials, no dense alpha blend, no visual LOD0 colliders, early dither/cull, baked AO and strong material identity.
- Middle: denser route-side kelp/coral, more wet geology breakup, more small silhouettes near traversal lines, still no random carpets.
- High: richer near-field flora/coral density, longer LOD residency, stronger shadows only where justified, improved material layering and wetness.
- Ultra: visual overkill in close shallow clusters, richer reef silhouettes, more secondary ecology and geology detail, but route truth, collision identity, and save identity remain unchanged.

## First-20 Route Impact

Improves the first-20 shallow exit route only as a blocker map. It prepares geology/flora/coral owners to build route landmarks, shallow ecology, safe/unsafe pockets, and evidence dressing after the base surface route stops failing. It removes the immediate risk of an agent covering broken water and black shoreline with dense decorative scatter.

## Required Next Action

Wait for process gate green. Then execute no-mutation Unity readback against the fields above. Only after readback can a Unity owner choose a tiny proof placement patch, capture compact/high screenshots, and either promote or reject the candidate pool.

Final status: `PENDING VERIFICATION`.

## 2026-06-06 Archimedes Static Asset Dressing Ranking

Evidence class: `STATIC_ASSET_TEXT / STATIC_YAML_READBACK / NO_UNITY_READBACK`.

Archimedes completed the narrow rocks/flora/coral inventory pass. This does not approve production placement. It only ranks what a later Unity owner is allowed to inspect first after Owner02 stops failing.

Ranked candidate order:

1. `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/` - strongest geology pool. Use first for shoreline silhouettes, route landmarks, arch/spire/cave-mouth framing, and wet basalt breakup. Still blocked by Unity readback of material slots, LOD transitions, collider proxy route, shadow flags, screenshot proof, and Stats/Frame Debugger evidence.
2. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral/` - strongest shallow coral detail pool. Candidate for reef-side foreground only after material/import/renderer proof. Do not scatter before water/shore/terrain base passes.
3. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp/` - strong shallow density pool. Needs material proof, believable water integration, motion/VAT or static fallback decision, and alpha/shadow cost proof.
4. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock/` - visually useful but blocked by collider proof. Static scan reports MeshCollider presence risk; production use requires primitive/convex/SDF proxy separation from visual LOD meshes.
5. `Assets/_Project/Prefabs/Nature/Flora/Baked/family_*` - useful family library, but blocked by final material replacement/proof. Proxy-looking material or toy coral silhouette remains rejected.
6. `Assets/_Project/Art/Meshes/World/Photic*/` plus `Assets/_Project/Art/Materials/World/Photic*/` - source/art-direction support, not automatic placement approval.
7. `Assets/_Project/Prefabs/Nature/Rocks/*.prefab`, `Assets/_Project/Prefabs/Nature/Flora/Baked`, and `Assets/_Project/Prefabs/Nature/GOTOVYE_PREFABY_KAMNEY/` - secondary only; vendor/GPUInstancer/import/collider risks must be read back first.

Visible-placement rejection remains absolute:

- `Assets/_Project/Prefabs/WorldProceduralProxy/`
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/`
- any primitive/proxy material pool used to cover the broken shoreline, water sheet, black terrain, or missing player/HUD/tool proof.

Placement sequence for the next green Unity window:

1. Owner02 readback and base-route repair first: water body, terrain graph/material, shoreline foam/wetness, sky/Aegir, player/HUD/tool witness.
2. No-mutation material/collider/LOD readback for the ranked candidate pools.
3. Tiny controlled placement patch only after base route is not visually rejected.
4. Compact and high-lane screenshots plus console/import/Stats/Frame Debugger proof before promotion.
5. Reject the patch if it hides base failures or creates random aquarium scatter.

Low / Middle / High / Ultra consequences:

- Low: fewer objects, but every visible object keeps premium silhouette, material identity, LOD/cull discipline, and no proxy material.
- Middle: route-side coral/kelp/geology density increases around readable current/light/substrate logic.
- High: richer wet geology, longer LOD residency, denser near-field ecology, and stronger material layering after base water/terrain pass.
- Ultra: capture-grade close clusters and secondary ecology, without changing collision truth, save identity, or route ownership.

Updated status: `PENDING UNITY READBACK / BLOCKED BY BASE SURFACE ROUTE`.

## 2026-06-06 Maxwell Texture Import Role Repair Packet

Evidence class: `STATIC_META / STATIC_LEDGER / NO_UNITY_IMPORT_READBACK`.

Maxwell completed the exact texture role-ledger sidecar for flora/coral imported textures and the oxygen mask sprite. The validator rejection is real. It is not a script false positive under current policy.

Current static blockers:

- `python -B Tools\ValidateTextureRoleTechnicalLedger.py --no-fail` returns `TEXTURE_ROLE_TECHNICAL_LEDGER_REJECTED blockers=3 rows=13 exact_asset_rows=2 directory_rows=4 docs_source_only_rows=7 ledger_matches=136`.
- `flora_coral/albedo` role row expects mipmaps and streaming mips under `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported`; 11 matched albedo assets all report `meta_streaming_mips=0`.
- `flora_coral/normal_detail_mask` role row expects mipmaps and streaming mips under the same source scope; 33 matched detail/mask/normal assets all report `meta_streaming_mips=0`.
- `Assets/_Project/Art/Sprites/oxygen-tank.png` is classified as `ui_oxygen_mask/mask`; static meta reports `textureType=8`, `sRGBTexture=1`, `enableMipMap=0`, `streamingMipmaps=0`.

Judgment:

- The flora/coral imported source stack already has mipmaps enabled, but streaming mipmaps are off. That blocks compact VRAM/load-shed policy for future shallow flora/coral placement.
- The oxygen texture is not allowed to remain sRGB while the role matrix classifies it as a mask. If the UI owner proves it is a colored icon instead, reclassify the role; until then, importer repair is the correct route.
- No raw `.meta` edits are allowed. Future repair must use Unity `TextureImporter` / `AssetDatabase` APIs and then regenerate the technical ledger from post-import state.

Safe future Unity sequence:

1. For `WorldProceduralFlora/Imported/**/albedo___*.png`: keep `TextureType=Default`, `sRGB=true`, `mipmapEnabled=true`, set `streamingMipmaps=true`.
2. For `detail___*.png` and `mask___*.png`: keep `TextureType=Default`, `sRGB=false`, `mipmapEnabled=true`, set `streamingMipmaps=true`.
3. For `normal___*.png`: keep `TextureType=NormalMap`, `sRGB=false`, `mipmapEnabled=true`, set `streamingMipmaps=true`.
4. For `Assets/_Project/Art/Sprites/oxygen-tank.png`: if mask role remains, keep `TextureType=Sprite`, `mipmapEnabled=false`, `streamingMipmaps=false`, set `sRGB=false`, then reimport.
5. Regenerate `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv` from Unity readback and rerun `Tools\ValidateTextureRoleTechnicalLedger.py`.
6. Update `Tools/test_validate_texture_role_technical_ledger.py` after repair because the current first test intentionally expects this known rejection state.

Placement consequence:

- Flora/coral candidate pools remain blocked by material/import proof, not just by base surface route. Do not place them into production until streaming mip repair, material slot readback, compact/high screenshots, and route visual proof exist.

## 2026-06-06 Huygens Placement Sidecar

Evidence class: `STATIC_PREFAB_YAML / STATIC_MATERIAL_TEXTURE_LEDGER / NO_UNITY_READBACK`.

Huygens independently confirmed the placement order and rejection gates.

Ranked use order:

1. `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals` - 49 prefabs, LODGroups present, no MeshCollider/primitive refs in static scan. Strongest geology pool, still blocked by material/LOD/collider/visual proof.
2. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/TubeCoral` - 50 LODGroup prefabs, no collider/primitive refs, proxy-material blocked.
3. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/Kelp` - 100 LODGroup prefabs, no collider/primitive refs, proxy-material blocked.
4. `Assets/_Project/Prefabs/Nature/Flora/Baked` - 89 LODGroup prefabs, blocked by family proxy material/static material risks.
5. `BioForge/Shallows/PorousRock` and `Nature/GOTOVYE_PREFABY_KAMNEY` - conditional only after collider proxy proof; static scan sees MeshCollider tokens across the pools.

Rejected for visible placement:

- `Assets/_Project/Prefabs/WorldProceduralProxy`
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final`
- any proxy/placeholder material visible in-route
- any dressing used to hide failed water, shoreline, terrain, sky/Aegir, player/HUD, or tool proof

Tiny later patch remains:

- only after base water/shore/terrain/sky route is non-rejected;
- Unity API only;
- one proof parent;
- 6-8 `ProceduralFinals` geology prefabs on route edges;
- 2-3 TubeCoral and 2-3 Kelp only after material/import proof;
- compact/high screenshots plus Console/import/Stats/Frame Debugger evidence;
- reject if it reads as random aquarium scatter or hides base failures.
