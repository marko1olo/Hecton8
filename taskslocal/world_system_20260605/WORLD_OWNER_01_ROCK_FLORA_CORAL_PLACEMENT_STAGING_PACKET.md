# WORLD_OWNER_01 - Rock / Flora / Coral / Debris Placement Staging Packet

ID: `WORLD_OWNER_01_PLACEMENT_STAGING_PACKET_WRITER`
Status: `FUTURE_PLACEMENT_PACKET / STATIC_DOC / PENDING BASE PROOF`
Evidence class: `STATIC_DOC`
Workspace: `c:\hades\Hecton8`
Owned write scope for this packet writer: this file only.

No Unity run, no import, no build, no Play Mode, no scene mutation, no prefab mutation, no material mutation, no Addressables mutation, no screenshot capture, no profiler capture, and no runtime claim was performed by this packet writer.

## First-20 Route Impact

First-20 moment: world load, first exit, swim, resource approach, tool/harvest decision, hazard response, return path readability.

Route blocker addressed by future work: route dressing must not become random decoration or camouflage. Rocks, flora, coral, and debris may enter the visible first route only after base proof passes for water, sky/Aegir/moons, terrain, player/HUD readability, route materials, and lighting. Placement must sharpen navigation, survival choices, salvage choices, scale, cover, return-path memory, hazard telegraphing, and world credibility.

## Authority And Mandates

Root and domain authorities read for this packet:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `3dmodel.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `terrain.md`
- `world.md`
- `quality.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/AssetAudit/README.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_25_PREFAB_PRIMITIVE_MESH_REPLACEMENT_PACKET.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET.md`

Mandates followed by this packet:

- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Non-Negotiable Sequencing

Visible placement is deferred until base proof passes.

Base proof means current proof artifacts exist for:

- water/ocean surface and underwater volume, including foam/contact contribution where visible;
- sky, Aegir, moon silhouettes, cloud layers, and surface lighting when the route can see them;
- terrain/geology material route, wetness, waterline, slope/traversal readability, and collision/proxy relationship;
- player spawn, camera, movement, oxygen/depth/HUD readability, and return-path readability;
- route material proof for candidate rocks/flora/coral/debris: no proxy, placeholder, default, null, or undocumented material roles;
- Unity Console/import state, route screenshots, Frame Debugger/Stats, profiler/GC/memory proof when runtime state or scene placement changes.

Until that exists, the future owner may stage placement rules, candidate lists, masks, density caps, proof manifests, and rejection checklists only. Do not scatter assets into the visible route to make failed base art look busier.

## Accepted Candidate Pools

Candidate means eligible for future Unity readback and visual proof. It does not mean accepted for placement.

1. `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`
   - Static audit: 49 prefab candidates, LODGroup and collider evidence in static scan, no MeshCollider and no built-in primitive refs in current static summary.
   - Future use: geology landmarks, route edge framing, wet basalt shelves, shallow resource framing, cover/occlusion silhouettes, return-path anchors.
   - Required before placement: Unity prefab readback, material readback, LOD transition proof, collider/proxy proof, route screenshots, Stats/Frame Debugger, memory/profiler proof if placed in scene.

2. `Assets/_Project/Prefabs/Nature/Flora/Baked`
   - Static audit: 89 prefab candidates, 267 LOD mesh assets, LODGroups, no colliders and no built-in primitive refs in current static summary.
   - Future use: photic kelp, coral, shallow alien biota, route scale cues, harvest affordance candidates, shelter/cover grammar.
   - Required before placement: material rebinding proof, vertex color semantic proof when shader reads sway/biolum/AO/thickness, LOD silhouette proof, no alpha-blend dense field on compact lane, route capture.

3. `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows`
   - Static audit: kelp, tube coral, porous rock source pool; porous rock remains blocked by collider/visual reference risk in prior evidence.
   - Future use: source/candidate pool only until Unity readback and visual proof.
   - Required before placement: same proof as baked flora plus collider proof for porous rock if it blocks route or traversal.

4. Cleaned/generated source packs under `Docs/GeneratedAssets/AssetSystem_20260605/`
   - Future use: source/reference only for material masks, foam/contact masks, shallow beam/caustic support, marine snow/fish card source prep.
   - Required before placement: cleanup, final import candidate manifest, alpha/mip/channel proof, Unity material slot readback, Frame Debugger, texture memory delta, compact/high route captures.

5. Project terrain/geology texture families under `Assets/_Project/Art/TEXTURES/Terrain Textures` and approved PBR route materials.
   - Future use: material support for rocks/terrain, not standalone placement acceptance.
   - Required before placement: import setting proof, sRGB/linear role proof, compression/mip proof, material binding proof, no stale `terrain.mat` or `Mat_TriplanarRock.mat` route.

## Rejected Pools For Visible Route Placement

These are rejected for visible route content unless a separate owner replaces the actual visual mesh/material route and proves it.

- `Assets/_Project/Prefabs/WorldProceduralProxy`
  - Static audit: 88 prefabs, no LODGroup, built-in primitive mesh refs.
  - Rejection: visible route placement forbidden.

- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
  - Static audit: 30 placeholder prefabs with primitive refs.
  - Rejection: visible route placement forbidden.

- Any prefab or material route still carrying `WorldProceduralProxy` material refs.
  - Rejection: proxy material cannot be hidden under density, fog, bloom, darkness, or distance.

- Generated source images or Gemini/prototype outputs imported directly as final art.
  - Rejection: source-only until cleaned PBR/mask role, import settings, material binding, screenshot, Frame Debugger, and texture memory proof exist.

- Any Unity built-in primitive visual mesh route: cube, sphere, capsule, cylinder, plane, ribbon/card used as final close-route asset without authored silhouette reason.
  - Rejection: primitive with better material is still primitive.

- Any placement that exists only to hide bad water, terrain, sky, Aegir, shoreline, foam/contact, or material failure.
  - Rejection: camouflage, not world composition.

## Placement Acceptance Rules

Placement must improve at least one route decision:

- entry/exit line;
- oxygen return planning;
- hazard avoid/approach choice;
- resource/tool affordance;
- cover/shelter/line-of-sight choice;
- landmark memory;
- depth/slope/traversal read;
- salvage/evidence interpretation;
- scale against player, vehicle, terrain, or Aegir/sky context.

Placement is rejected if it only fills empty space. Pretty emptiness is still emptiness.

## LOD, Collider, Material, And Streaming Requirements

LOD:

- Props or cluster objects above 0.5 m require `LOD0`, `LOD1`, `LOD2`, and cull/HLOD or an explicit proof-backed exemption.
- LOD transitions use dither/crossfade where supported and must preserve silhouette, root/anchor identity, harvest identity, ore/vent readability, and route landmark shape.
- LOD hysteresis is mandatory: no immediate state flipping. Use at least 3 consecutive frames or 3-5 m band equivalent before bucket change.

Collider:

- Visual `LOD0` mesh is not production `MeshCollider` truth.
- Rocks/blockers use compound primitives, convex proxy under 200 triangles, or SDF/nav proxy as owner-approved.
- Flora/coral default to no collision; interaction uses coarse root/harvest trigger spheres/capsules only when gameplay reads it.
- Debris uses primitive/convex proxies with route clearance proof and no hidden traversal snag.

Material:

- Shared `MAT_*` families only; no runtime material clones.
- Texture roles must be documented: albedo, normal, MRAO, emission/detail/mask where applicable.
- Albedo is sRGB; normals/masks are linear; mips on for world textures; BC7/BC5 or platform-approved compressed formats.
- No blockout/default/proxy/null material route on visible placement.
- Dense flora/coral uses alpha clip/dither, not alpha blend, on compact lane.

Rendering/streaming:

- MeshRenderer-owned repeated props prefer Unity 6 GPU Resident Drawer where compatible.
- Manual BRG is allowed only for data-only procedural ownership with no stable MeshRenderer route.
- Static batching and GPU instancing must not double-own the same object.
- Heavy placement groups must route through Addressables planning only after readback/proof. Static planning docs do not create runtime readiness.
- No `Resources.Load`, no runtime mesh generation, no runtime texture generation, no `renderer.material`, no hot scene search.

## Scatter Density Rules

Scatter density is authored consequence, not random fill:

- rocks follow terrain strata, shore erosion, slope breaks, cave mouths, vents, collapsed edges, impact paths, or resource geology;
- flora follows light, current, sediment, substrate, shelter, food, and depth band;
- coral clusters follow anchor surfaces, parent/child growth logic, plate/branch/mound ecology, and current exposure;
- debris follows industrial route logic: breakage direction, fall line, buoyancy drift, salvage cuts, cable paths, old colony remnants, or wreck collapse;
- clear space is intentional when it preserves route readability, hazard legibility, or performance.

Density caps must be written per route segment before placement:

- Near route critical view: highest scrutiny, fewer but stronger assets, no clutter over affordances.
- Return path landmark: controlled silhouette density, stable in compact tier.
- Resource pocket: assets support discovery and extraction path, not concealment.
- Hazard edge: assets telegraph danger and give choices; they do not hide unfair contact.
- Scenic pause pocket: allowed, but must still show route/return cue and at least one evidence/scale cue.

No evenly spaced scatter. No coral carpets without ecology. No resource dots. No random debris confetti.

## h8_1475 Proof Requirement

Future placement acceptance must use the h8_1475 proof lane when the Unity process gate is clean:

- Read `taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md` before executing that proof route.
- Use `Docs/AssetAudit/H8_1475_READBACK_FIELD_MANIFEST_20260605.csv` for required readback fields.
- Use `Docs/AssetAudit/VISUAL_REFERENCE_CAPTURE_GAP_TABLE_20260605.csv` for screenshot gaps.
- Required proof folder: `Docs/Screenshots/HectonProofPackets/WORLD_OWNER_01_<YYYYMMDD_HHMMSS>/`
- Required h8_1475 captures:
  - base proof before placement: water/sky/terrain/player/HUD/material state;
  - placement delta capture from same view and gameplay height;
  - compact-readable view;
  - high/ultra enrichment view if density/fidelity changed;
  - route return-path view;
  - collider/LOD/debug overlay or equivalent editor proof for representative placed families;
  - Frame Debugger/Stats report for SetPass, batches, shadow casters, material variants, LOD state, SRP Batcher/GPU Resident Drawer compatibility;
  - profiler/GC/memory/VRAM report when scene placement, renderer counts, colliders, runtime scatter, Addressables, or VFX changed.

Without h8_1475 or equivalent fresh proof, final status remains `PENDING VERIFICATION`.

## Phase-Gated Future Tasks

1. Confirm process gate before any Unity work: CPU <= 50 percent and no busy `Unity`, `dotnet`, `csc`, `MSBuild`, `Unity.ILPP.Runner`, `UnityShaderCompiler`, `UnityPackageManager`, import, or build process. If red, do static prep only.
2. Read the authority list above plus current `Docs/AssetAudit/README.md` and `ASSET_SYSTEM_INDEX_20260605.md`. Do not bulk-read unrelated archives.
3. Lock the selected first-20 route segment: base, exit, swim lane, resource pocket, hazard edge, return path, scenic pause pocket.
4. Produce a base-proof checklist for water, sky/Aegir/moons, terrain, player/HUD, lighting, route materials, and material readback. Mark every missing proof as `BLOCKED_BASE_PROOF`.
5. Build a candidate pool table from accepted pools only: path, family, depth band, intended route function, LOD status, collider status, material status, proof state.

Checkpoint 1: if base proof is missing, stop before visible route placement. Only staging tables, masks, and density rules may proceed.

6. Build a rejected pool table: `WorldProceduralProxy`, `WorldRuntime/ProceduralPlaceholders`, proxy materials, primitive mesh refs, source-only generated images, stale/default material routes.
7. Define route-function tags for every intended placement: `LANDMARK`, `RETURN_PATH`, `RESOURCE_READ`, `HAZARD_EDGE`, `COVER`, `SCALE_CUE`, `EVIDENCE`, `SCENIC_PAUSE`, `SALVAGE_TRAIL`, `CURRENT_FLOW`.
8. Define placement masks: depth band, slope class, substrate, current exposure, light exposure, sediment, industrial influence, cave/open-water state, traversal exclusion, player camera clearance.
9. Define scatter density caps per route segment and per hardware curve. Use deterministic seeds; record seed, mask source, and route segment.
10. Define clear-space rules around tool targets, oxygen return routes, HUD sightlines, hazard cues, doors/hatches, ladders/docks, vehicle clearance, and pickup silhouettes.

Checkpoint 2: reject any plan that improves density but reduces player decision readability, return-path memory, or compact-tier route clarity.

11. For rocks/geology, choose only candidates with believable geological process: strata, fracture, wetness, erosion shelf, mineral vein, vent logic, or waterline material.
12. For flora/coral, choose only candidates with grown structure: anchor, taper, branching hierarchy, pigment/biolum logic, vertex color semantics, and LOD-preserved root identity.
13. For debris, choose only authored or repaired assets with industrial/salvage reason, no primitive visual route, readable mass, material role, LOD, collider proxy, and no product-face rejection.
14. Define candidate material proof per family: `MAT_*`, `TX_*` roles, shader, texture import expectations, SRP Batcher/instancing note, no proxy/default/null route.
15. Define collider/proxy proof per family: no visual LOD0 MeshCollider, `COL_*` children or convex/proxy/SDF route, traversal clearance, interaction trigger identity where applicable.

Checkpoint 3: if candidate proof is static-only, classify `CANDIDATE_POOL_BLOCKED_BY_UNITY_READBACK`; do not promote to route placement.

16. Stage route composition in a no-save planning pass: landmark skeleton first, return-path anchors second, resource pocket support third, hazard edge support fourth, scenic density last.
17. For every proposed cluster, write a one-line reason: what decision it sharpens and what physical fact justifies it. Delete cluster plans that cannot answer both.
18. Define premium approximation rules for flora motion, marine snow, fish cards, caustic/beam masks, and foam/contact support. Prefer authored shader/VFX/audio/haptic/UI/proxy approximation; physics only when gameplay truth requires it.
19. Define continuous `GlobalQualityWeight` density curve: density, LOD residency, texture residency, shadow eligibility, VFX density, and diagnostic depth scale smoothly from 0.0 to 1.0.
20. Define load-shed behavior: under VRAM/frame pressure, reduce optional density, texture residency, shadow eligibility, and VFX cadence before removing route-critical landmarks or material identity.

Checkpoint 4: reject binary quality switches, ultra-only readability, and compact "ugly mode." Compact must still pass route clarity and visual floor.

21. Execute Unity placement only after base proof and candidate proof exist. Use Unity API/Prefab Stage/scene tooling only; no raw YAML mutation.
22. Capture h8_1475 base-before and placement-after screenshots from identical gameplay-height views. Include compact and high/ultra comparison when density/fidelity changes.
23. Capture LOD/collider/material proof for representative placed rocks, flora/coral, and debris: flat material, final material, wireframe or LOD view, collider overlay, material channel/readback table.
24. Capture Frame Debugger/Stats: SetPass, batches, shadow casters, shader variants, SRP Batcher/GPU Resident Drawer/instancing state, overdraw risk for dense alpha-clip fields.
25. Capture profiler/GC/memory/VRAM when runtime placement, scene object count, renderer count, collider count, Addressables, VFX, scatter code, or streaming residency changes. No numbers means no runtime acceptance.

Checkpoint 5: promotion is blocked if screenshots show camouflage, cluttered affordances, proxy materials, primitive shapes, route-hidden hazards, unreadable return path, flat water/sky/terrain, or compact-tier visual collapse.

26. Produce final future-owner report: files touched, exact placed families, route segment, seed/mask, density caps, proof artifact paths, rejected candidates, rollback actions, unresolved blockers, Low/Middle/High/Ultra consequences, and proof labels.

Checkpoint 6: final label is `PENDING VERIFICATION` unless the required Unity/player/profiler artifacts exist. Static docs cannot accept route placement.

## Brutal Rejection Gates

Reject immediately:

- proxy/primitive/blockout placement;
- `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` in visible route content;
- placement used to hide weak water, sky, Aegir, terrain, foam, shoreline, or material failure;
- darkness, fog, bloom, dense particles, caustics, or post effects used as cover;
- assets with no LOD proof above threshold;
- visual `LOD0` MeshCollider;
- default/proxy/null/package material routes;
- material channels without documented role;
- alpha-blend dense flora/coral on compact lane;
- even/random scatter;
- coral/flora without ecology/substrate/light/current logic;
- rocks that read as smooth blobs or random noise;
- debris with no industrial/salvage/failure reason;
- screenshots from flattering angles that avoid traversal/player-height proof;
- route beauty that does not sharpen a player decision;
- any public or internal report claiming placement readiness without h8_1475 or equivalent fresh proof.

## Continuous GlobalQualityWeight Consequences

These are labels on one continuous curve, not binary modes.

- Low / compact `0.0-0.25`: preserve route landmarks, premium silhouettes, compact-readable water/sky/terrain composition, baked AO, compressed PBR roles, low density, shorter optional LOD residency, fewer shadow casters, sparse VFX. Never substitute proxy meshes, flat materials, or ugly mode.
- Middle `0.25-0.55`: expected player lane. Maintain full route readability, richer but controlled scatter, stable LOD bands, route-owned materials, clear return path, and believable ecology/geology.
- High `0.55-0.85`: extend LOD0/LOD1 residency, add denser near-field biota and debris where route decisions remain readable, richer material detail, stronger wetness/caustic/contact response, improved lighting and shadow eligibility with proof.
- Ultra `0.85-1.0`: visual overkill after compact passes: longer sightline richness, dense but authored clusters, deeper material layering, richer flora sway/VFX presentation, capture-grade shoreline/photic/medium-depth route dressing. Gameplay truth, collider authority, seed identity, material channel semantics, save identity, and route ownership remain unchanged.

## Regression Model

- CPU: risk from renderer count, LODGroup evaluation, culling, collider count, scatter logic, VFX systems, and streaming integration. Any feature over `0.1ms` is suspicious until profiler proof and load-shed path exist.
- GC: future runtime scatter or VFX users must prove `0 B/frame`. Reject hot `GetComponent`, `Find*`, LINQ, string churn, `Resources.Load`, runtime mesh/texture creation, runtime material clone, or unmanaged readback allocation.
- Memory/VRAM: risk from added meshes, material slots, texture residency, shadow casters, VFX atlases, and longer LOD residency. Compact VRAM ceiling remains 1800 MB and texture budget 900 MB.
- SetPass/batches: risk from material proliferation, shader variants, alpha fields, shadow casters, and unbatched unique prefabs. Use shared materials, atlases/arrays, SRP Batcher, GPU Resident Drawer/instancing where proven.
- Cadence: LOD, density, VFX, streaming, and diagnostics must scale continuously with `GlobalQualityWeight` and hysteresis. No binary quality switches.
- Correctness: placement must not change route truth, resource identity, collision truth, harvest identity, save state, terrain authority, water authority, HUD/player ownership, or asset ownership route.
- Visual: surface/photic/medium-depth hero routes must remain bright, legible, beautiful, and Subnautica-level or better. If placement makes the route busy but not better, reject it.

## Proof State

Current packet proof state: `STATIC VERIFIED` for written task constraints only.

Future placement acceptance state: `PENDING VERIFICATION` until Unity readback, h8_1475 screenshots, route captures, Frame Debugger/Stats, profiler/GC/memory proof, and rejection-gate review exist.

Final disposition: do not place rocks/flora/coral/debris near the route end until the base route proves water, sky/Aegir, terrain, player/HUD, lighting, and materials first. Placement is the last amplifier, not camouflage.
