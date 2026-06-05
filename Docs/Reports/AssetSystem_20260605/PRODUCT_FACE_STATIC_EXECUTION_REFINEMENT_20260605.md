# Product-Face Static Execution Refinement - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_SOURCE` + `STATIC_YAML_SCAN` + `STATIC_IMAGE_QA`.
Owned output: `Docs/Reports/AssetSystem_20260605/PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.md/.csv`.

No Unity run, build, Play Mode, import edit, prefab edit, material edit, shader edit, scene edit, Addressables operation, deletion, runtime capture, profiler capture, or `Assets/` mutation was performed. This report is execution refinement only.

First-20 route moment affected: bright first exit, ocean skin, shoreline/waterline, photic shallows, Aegir/sky context, player/suit presence, held tools, resource pickups, transport silhouettes, flora/coral route density, and medium-depth product-face trust.

## Mandates Followed

- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## What Was Wrong

The product-face material and prefab target tables were correct but still too broad for execution:

- `PRODUCT_FACE_MATERIAL_P0_TARGET_TABLE_20260605.csv` has 124 rows: flora/coral/fauna 44, proxy/placeholder 31, water/ocean contact 29, sky/Aegir/cloud 13, terrain/geology 3, plus single player blockout, package-default, foam support, and caustic/foam rows.
- `PRODUCT_FACE_PREFAB_P0_TARGET_TABLE_20260605.csv` has 39 rows: Tools/Held 12, Items/Tools 12, Resources/Pickups 8, Transport 4, Player 1, Sky_System 1, and Ocean_Crest micro-fauna 1.
- All 39 prefab rows carry missing static LODGroup proof. The static absence is not final LOD failure, but it blocks promotion until Unity readback proves the real state.
- Material rows mix visible material repair, Crest slot boundaries, source-only texture rows, proxy contamination, package-default material risk, sky/Aegir hero slots, and terrain/geology route proof.
- Prefab rows mix close-camera held visuals, world pickups, transport macro forms, player product-face state, Sky_System primitive risk, and Ocean_Crest visible micro-fauna risk.
- Static YAML, CSV, batchmode validator text, and critique tables can reject or route work. They cannot prove active renderer binding, import state, visual quality, frame time, GC, memory, Crest ownership, collider truth, or runtime readiness.

Current visual state remains rejected or pending by the critique checklist because the canonical `h8_1475` proof packet is absent.

## Refined Order

1. Process gate and no-mutation gate. If Unity, compiler, import, shader compiler, package manager, dirty scene, or save pressure is present, stop. No repair starts from ambiguous editor state.
2. Material readback before material authoring. Owner 24 reads active renderers, material slots, texture roles, shaders, keywords, null/default/proxy refs, Crest visible slots, sky/Aegir slots, and terrain slots.
3. Prefab readback before mesh replacement. Owner 25 reads prefab renderers, mesh refs, materials, LODGroup state, colliders, scripts, pivots, sockets, anchors, active state, and scene overrides.
4. Lock material family contracts. Define shared `MAT_*` families and `TX_*` roles for ocean/contact, flora/coral/kelp, terrain/geology, sky/Aegir/cloud, player, tools, pickups, and transport before binding.
5. Lock mesh source plans. Define authored or offline-generated LOD0/LOD1/LOD2 meshes, cull or HLOD behavior, finite topology, UVs, normals/tangents, material IDs, and `COL_*` proxy plans before prefab mutation.
6. Treat sky and Crest as boundary cases. Sky_System mesh replacement does not authorize sky material rewrite. Ocean_Crest micro-fauna replacement does not authorize Crest wrappers, material clones, or hidden data-input primitive changes.
7. Close LOD, collider, and material proof together. A mesh replacement without material proof is rejected. A material repair on a primitive route is rejected. A visual pass without collider/LOD proof is incomplete.
8. Produce canonical proof only after scoped readback and authorized future edits. Required proof is `h8_1475` style packet material: manifest, hash, Unity log, readback tables, screenshots, Console, Frame Debugger/Stats, and memory/VRAM evidence where relevant.

CSV row actions are in `PRODUCT_FACE_STATIC_EXECUTION_REFINEMENT_20260605.csv`.

## No-Mutation Rules

- Do not edit `Assets/`, `ProjectSettings/`, `Packages/`, `UserSettings/`, scenes, prefabs, materials, shaders, importers, Addressables, or code from this refinement.
- Do not raw-edit `.prefab`, `.unity`, `.mat`, `.asset`, `.meta`, importer, package, or scene YAML in future execution unless a separate explicit owner run proves file ID and GUID safety.
- Do not delete or unwire any target from static rows alone.
- Do not promote `foam.png`, WorldProceduralProxy, WorldRuntime placeholder routes, blockout materials, package-default `Lit.mat`, null slots, empty PBR roles, or visible Unity primitive meshes.
- Do not hide weak water, shoreline, terrain, Aegir, sky, tools, resources, transport, flora, coral, or micro-fauna with darkness, fog, bloom, vignette, storm grade, random scatter, camera crop, or post-processing.
- Do not introduce Crest runtime wrappers, material clones, or custom override paths.
- Do not change gameplay truth, prefab identity, collider authority, material channel semantics, Addressables identity, DTO layout, save identity, or owner route through quality scaling.

## Proof Boundary

Accepted future proof classes for this refinement:

- `STATIC_DOC`: tables, owner packets, critique checklist, routing synthesis, dependency graph.
- `STATIC_SOURCE`: CSV rows, serialized source references, static validator logs.
- `EDITOR_VERIFIED`: Unity readback of material slots, prefab stage state, importer state, Console state, dirty-state audit.
- `PLAYER_CAPTURE_VERIFIED`: canonical product-face visual captures compared against the critique checklist.
- `PROFILER_VERIFIED`: frame time, GC, SetPass, batch, memory, and VRAM evidence when runtime state, renderer count, collider count, materials, streaming, or scene placement changes.

Forbidden proof claims from this report:

- No Unity import state.
- No scene or prefab instance correctness.
- No material binding acceptance.
- No visual acceptance.
- No runtime behavior acceptance.
- No `0 B/frame`, frame-time, memory, or VRAM claim.
- No Addressables or lifecycle acceptance.

## Continuous GlobalQualityWeight Consequences

- Low/compact near `0.0`: preserve final proven silhouettes, bright readable ocean color, shoreline contact, Aegir/sky readability, terrain scale, material identity, `COL_*` proxies, and route cues. Reduce density, texture residency, shadow eligibility, and LOD distance smoothly. Never expose primitives, proxy materials, muddy sky, flat water, or ugly fallback art.
- Middle around `0.35`: full route-owned PBR stacks, stable shared materials, complete LOD transition bands, readable tools/resources/transport, and proven active slot ownership.
- High around `0.7`: spend recovered budget on longer LOD0/LOD1 residency, stronger wet-edge/contact response, richer Aegir/cloud detail, detail normals, trims, labels, wear, flora/coral masks, and denser near-field route dressing after proof.
- Ultra near `1.0`: add capture-grade bevel density, layered material response, richer shoreline/ocean/sky overdetail, stronger organic material depth, and longer route residency. Gameplay truth, prefab identity, collider authority, material channel semantics, Crest ownership, owner route, DTO layout, and save identity do not change.

`GlobalQualityWeight` is continuous. Binary low/high branches are rejected. Hysteresis is required for future LOD or residency changes.

## Regression Model

- CPU: static report only. Future risk comes from added renderers, LODGroup evaluation, collider count, material instance growth, shader keyword growth, Frame Debugger-visible SetPass growth, Crest slot misuse, and product-face validator/editor overhead.
- GC: no runtime code touched. Future risk comes from runtime mesh/material generation, hot scene search, string logging, unpooled VFX, Addressables callback churn, and direct component lookup during presentation updates.
- Memory/VRAM: no residency proof. Future risk comes from new texture packs, longer LOD residency, material proliferation, shadow casters, mesh buffers, duplicated Crest materials, and compact 1800 MB VRAM / 900 MB texture budget pressure.
- Cadence: no runtime cadence changed. Future LOD, texture residency, VFX, material uploads, and readback routes need continuous quality scaling plus hysteresis, not binary switches.
- Correctness: high-risk facts are active material ownership, Crest visible/data slot separation, prefab GUIDs, scene overrides, pivots, sockets, interaction anchors, hand poses, collider truth, item identity, transport docking/tow truth, and Addressables identity.
- Visual: current state is not accepted. Surface, sky/Aegir, ocean, shoreline, photic terrain, HUD/player context, tools, resources, transport, flora/coral, and micro-fauna remain `PENDING_VERIFICATION` or rejected until canonical proof passes the critique checklist.

Final disposition: `PENDING_VERIFICATION`.
