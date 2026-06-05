# Visual + Mesh Asset Taxonomy - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence classes used here: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_IMAGE_QA`.
Scope: textures, materials, meshes, prefabs, generated source prototypes, rejected placeholder pools, and review queues.
First-20 route blocker addressed: false visual promotion risk for surface first view, first exit, photic shallows, shoreline, and medium-depth route dressing.

No Unity, import, build, prefab mutation, material mutation, scene save, profiler, Frame Debugger, or Addressables runtime proof was run for this taxonomy. Static source and document evidence only.

## Inputs Consumed

- `AGENTS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md` and `.csv`
- `Docs/AssetAudit/TEXTURE_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`
- `Docs/AssetAudit/MESH_PREFAB_REVIEW_QUEUE_20260605.csv`
- `Docs/Reports/AssetSystem_20260605/MESH_PREFAB_PROMOTION_STATIC_TABLE_3214_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`

## Inspect First

1. Waterline foam/contact: `foam.png` is visually rejected and serialized-reachable in `02_HECTON_WORLD.unity`. This is the first surface/shallow visual risk.
2. Proxy flora/coral/kelp: `WorldProceduralProxy` materials are serialized in active world route evidence. This blocks photic shallows promotion.
3. Aegir and sky: the old baked disc is prototype-only, cleanup sources remain source-only, and shader slot binding is not proven.
4. Wet basalt/shell/sand terrain: source pools exist, but direct generated import and broad terrain use remain blocked by clean PBR and route screenshot proof.
5. Mesh/prefab pools: `ProceduralFinals` rocks are the strongest static geometry candidate; flora pools are material-blocked; proxy, placeholder, and construction primitive pools are rejected for product-face placement.

## Static Taxonomy

| Category | Families | Current disposition | Future owner action |
|---|---|---|---|
| Review queues | visual queue, mesh/prefab queue, action queue | Dispatch order only | Use queues for owner order; do not treat them as proof. |
| Water/contact texture | Crest foam, `Assets/_Project/Art/TEXTURES/foam.png`, cleanup foam sources | Rejected support or source-only | Replace with authored contact masks, then prove Crest/ocean material slots and bright shoreline screenshot. |
| Sky/Aegir/cloud | baked Aegir disc, `clouds0_diff.png`, `Mat_HectonSky`, cleanup Aegir source | Source candidate/readback-blocked | Read effective skybox/material slots in Unity, then prove bright surface screenshot. |
| Terrain/geology texture | wet basalt, shell/sand, terrain PBR sources | Candidate with clean-PBR blocker | Build cleaned PBR packs, prove import roles, tile seams, terrain material route, and route screenshot. |
| Flora/coral texture stacks | `WorldProceduralFlora/Imported` | Candidate blocked by material/import proof | Resolve final material binding, streaming mips, alpha/dither, LOD silhouette, and screenshots. |
| Geology prefabs | `Nature/Rocks/ProceduralFinals` | Static geometry candidate only | Unity prefab/material readback, collider proxy proof, LOD transition proof, route screenshot, Stats/Frame Debugger. |
| Baked flora prefabs | `Nature/Flora/Baked` | Candidate mesh pool blocked by proxy materials | Replace/prove final material route before visible placement. |
| BioForge shallows | Kelp, TubeCoral, PorousRock | Kelp/TubeCoral material-blocked; PorousRock collider-blocked | Split family review; prove material, alpha/dither, LOD, and PorousRock collision route. |
| Rejected proxy prefabs | `WorldProceduralProxy` | Reject visible route placement | Keep as editor/proxy/reference only or replace with final authored/generated prefabs. |
| Rejected runtime placeholders | `WorldRuntime/ProceduralPlaceholders` | Reject visible route placement | Remove from product-face route or replace before any screenshot/proof pass. |
| Construction final pool | `Prefabs/Construction/Final` | Product-face reject from static evidence | Replace primitive visual meshes, add missing LODGroups, prove material texture route. |
| UI oxygen route | `ui/OXYGEN.png`, `oxygen-tank.png` | Detailed source candidate vs mask/silhouette | Prove atlas/import/HUD binding; do not use the black silhouette as colored final icon. |
| External/prototype refs | Crest material refs, Feel/MM prototype material | Readback required | Third-party asset integrity check; no custom runtime wrappers, clones, or material mutation. |
| Unassigned useful textures | floor panels, mineral seep masks, plume noise, organic/electric sources | Unassigned static source | Owner must assign material role and proof target before any route use. |

## Rejected

- `Assets/Crest/Crest/Textures/foam.png` and `Assets/_Project/Art/TEXTURES/foam.png` as visible waterline/contact art. Reason: flat/turquoise sheet risk and active-route reachability.
- `Assets/_Project/Prefabs/WorldProceduralProxy` for visible route placement. Reason: primitive visual mesh refs, proxy materials, no LODGroup.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders` for visible route placement. Reason: primitive visual mesh refs, placeholder materials, no LODGroup.
- `Assets/_Project/Prefabs/Construction/Final` for product-face visuals in current static state. Reason: built-in primitive visual meshes, missing LODGroups, weak/empty material texture bindings.
- `BioForge/Shallows/PorousRock` for route placement until MeshCollider purpose, triangle budget, and collision proxy ownership are proven.
- Random unassigned textures as route art. Reason: no material role, owner, import proof, or screenshot proof.

## Source-Only / Prototype-Only

- `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605` foam contact sources: useful for authored material work, not direct Crest binding.
- Aegir/cloud cleanup sources: better source direction than the old baked disc, but storm cells and shader channel response remain unproven.
- Batch31/local PBR terrain sources: source references only until cleaned PBR channel packs, import roles, tile seam proof, and terrain material route proof exist.
- Gemini/generated images: source references only. Watermarks, baked shadows, seams, naive normal/MRAO maps, or false-color masks block direct product use.
- `oxygen-tank.png`: mask/silhouette unless a UI owner proves a mask/tint role.

## Promotion Gates After Unity Readback

Future owners can promote candidates only after the matching readback/proof packet exists:

- Material readback: effective shader properties, texture slots, scene renderer users, null/stale refs, no raw YAML patching.
- Visual proof: bright surface/shoreline/photic screenshot for surface assets; no darkness, fog, bloom, or post stack hiding weak art.
- Mesh proof: LODGroup readback, triangle/transition proof, silhouette proof, collider route proof, no primitive visual mesh route.
- Rendering proof: Stats/Frame Debugger for draw calls, SetPass risk, SRP Batcher/material variant path, and no unsupported material clone/wrapper.
- Streaming proof: Addressables group/key, load mode, handle/release ownership, memory/residency evidence, and pressure behavior.

## Never Promote

- Do not promote proxy or placeholder pools as visible product content.
- Do not promote generated images directly as final material art.
- Do not promote Crest material changes through custom runtime wrappers, cloned materials, or overrides.
- Do not promote orbit-only refs as main route proof.
- Do not promote any source with only static file existence as runtime, visual, Addressables, VRAM, import, or material acceptance.

## Scalability Consequences

- Low tier: use only final proven meshes/materials. Preserve bright surface, readable sky, waterline material identity, baked AO, silhouettes, and dithered LOD. Reduce density/residency smoothly; never substitute primitive proxies.
- Middle tier: require route-owned PBR stacks, final non-proxy flora materials, clean foam/contact masks, stable LOD crossfade, and terrain material proof.
- High tier: extend LOD residency, detail normals, wetness masks, near-field geology/flora density, reflection/water response, and richer route dressing after proof.
- Ultra tier: spend extra budget on layered Aegir atmosphere, richer shoreline breakup, denser route dressing, and material overdetail. Do not change gameplay truth, prefab identity, channel semantics, save identity, or authority route.

## Regression Model

- CPU: no runtime code changed. Future placement/import work must prove no renderer, collider, material, or Addressables CPU regression.
- GC: no runtime code changed. Future runtime systems must prove 0 B/frame in hot paths.
- Memory/VRAM: source size and static reachability only. Residency, mip behavior, compression, Addressables groups, and release ledgers are unproven.
- Cadence: no runtime cadence changed.
- Correctness: taxonomy reduces false promotion by separating static candidates, source-only prototypes, hard rejects, and readback gates.

Final status: `PENDING_VERIFICATION`.
