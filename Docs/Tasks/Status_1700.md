# Status_1700 - 3D Model Generation Standards Director

Status: PENDING VERIFICATION
Prompt source: inline `<AGENT_PROMPT id="1700">`; archive `CURRENT_BATCH.md` search found no active 1700 block.
Domain: offline Editor-time generated mesh, texture, material, LOD, and collision standards for generated visual assets.
Task count: 13.

## Mandates Used

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: runtime must not allocate or manipulate geometry buffers in hot paths.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: bake visible consequences offline before simulating invisible causes.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`: MX350/2GB VRAM, triangle, texture, and LOD budgets define acceptance.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: URP, SRP Batcher, HLOD, dithered LOD, and channel-packed PBR masks.
- `REND_Instanced_Flora_Physics.txt`: flora uses authored mesh streams, vertex/shader fakes, BRG/GPU Resident Drawer paths.
- `STRM_Async_Asset_Upload_Texture_Settings.txt`: static meshes/textures must be serialized and upload-budget aware.
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`: visual meshes cannot become physics truth; collision proxies are mandatory.
- `TOOL_Procedural_Wreckage_Generator.txt`: wreckage/modules need bitmask sockets, merged mesh tiers, and atlas/material discipline.

## Loop 1 - Tasks 01-03

- [x] Task 01 EXISTING_GENERATOR_INQUISITION - DOD: inspected editor/runtime generator surfaces and confirmed existing lanes for coral, seaweed, geology, wreckage, primitive shells, and DeepReach box surrogates. Rejected alternative: generic art-bible prose without codebase evidence. Estimate: avoids 500-3000 us runtime waste per asset if future agents follow offline/proxy rules instead of hot mesh/collider generation.
- [x] Task 02 TEXTURE_ASSET_REPOSITORY_MAPPING - DOD: inspected texture/material authoring and baker lanes, including generated albedo/normal/MRAO paths, BC5/BC7 import enforcement, material scalar contracts, and flora material families. Rejected alternative: synthetic flat colors as acceptable fallback. Estimate: saves 20-150 us CPU render setup and reduces SetPass pressure through atlas/material slot discipline; exact proof PENDING PROFILER.
- [x] Task 03 DOCUMENT_STRUCTURE_ARCHITECTING - DOD: defined root bible plus specialist files for hard surface, flora/coral, fauna, geology/rocks, equipment/modules, and textures/materials. Rejected alternative: one large unrouteable document. Estimate: saves future agent time and prevents asset drift; runtime microseconds not directly measurable.

## Loop 2 - Tasks 04-05

- [x] Task 04 ROOT_DOCUMENT_INITIALIZATION - DOD: created root `3dmodel.md` with preamble, routing map, generated asset package law, and Deep Sea Noir/NASA-punk visual target. Rejected alternative: burying this in `/Docs` where the explicit task asked for repository root. Estimate: runtime microseconds not directly measured; prevents future runtime generator drift.
- [x] Task 05 THE_LAW_OF_OFFLINE_PERMANENCE - DOD: root document begins with hard ban on runtime mesh/texture/UV/tangent/collider generation and requires serialized `.mesh`, `.prefab`, `.mat`, `.png`, `.asset` outputs. Rejected alternative: allowing runtime exceptions for convenience. Estimate: avoids 1000-5000 us spikes from mesh/collider/texture construction on bad frames; PENDING PROFILER.

## Loop 3 - Tasks 06-08

- [x] Task 06 HARD_SURFACE_NASA_PUNK_STANDARDS - DOD: codified bevel thresholds, bevel width ranges, smoothing group split rules, weighted normals, socket panelization, material slots, and hard-surface wear masks in `3dmodel.md` and `3DMODEL_HARD_SURFACE_MODULES.md`. Rejected alternative: accepting box/socket surrogates as final visuals. Estimate: 50-250 us saved per frame in downstream scenes through fewer material/collider hacks; visual benefit primary.
- [x] Task 07 ORGANIC_ABYSSAL_TOPOLOGY_STANDARDS - DOD: codified manifold/open-shell rules, branch weld/knuckle rules, flora/coral topology families, fauna deformation loops, and required vertex color R/G/B/A semantics. Rejected alternative: primitive tube/blob/ribbon final output. Estimate: runtime shader mask reads replace per-vertex CPU logic; 100-1000 us avoided if agents would otherwise compute deformation masks at runtime.
- [x] Task 08 UV_UNWRAPPING_AND_ATLASING_MANDATES - DOD: codified conformal/cylindrical/box/triplanar routes, forbidden UV states, MaxRects/Skyline packing, padding values, edge bleed, texel density, MRAO channel packing, and import roles. Rejected alternative: flat generated colors or unmeasured atlas packing. Estimate: 20-150 us CPU render setup saved by material consolidation; VRAM/mip seam benefits PENDING CAPTURE.

## Loop 4 - Tasks 09-10

- [x] Task 09 PHYSICS_COLLIDER_DECOUPLING_LAW - DOD: codified LOD0 MeshCollider ban, `COL_*` proxy naming, primitive collider routes, convex hull budgets, geology/coral/fauna/equipment proxy limits, and prefab rejection rules. Rejected alternative: complex MeshCollider convenience. Estimate: avoids 200-2000 us PhysX narrow-phase cost in dense scenes; PENDING PROFILER.
- [x] Task 10 AUTOMATED_QUALITY_ASSURANCE_GATES - DOD: codified pre-save validation before `AssetDatabase.SaveAssets`/prefab save, including finite data, degenerate triangles, UV density, normalized normals/tangents, LOD budgets, material slots, collision proxy, texture import, naming, manifest, and black-box dump rules. Rejected alternative: saving assets then auditing later. Estimate: prevents corrupt asset import and runtime stalls; microseconds saved depend on rejected defect.

## Loop 5 - Tasks 11-13

- [x] Task 11 DOCUMENT_COMPREHENSIVENESS_AUDIT - DOD: performed static content audit for offline law, URP/SRP/BRG compatibility, continuous `GlobalQualityWeight`, bevel/smoothing, organic vertex channels, UV/atlas, LOD, collision, validation, and black-box requirements. Rejected alternative: relying on manual reading only. Estimate: prevents future batching/material/collider defects; runtime proof PENDING.
- [x] Task 12 AESTHETIC_ALIGNMENT_VERIFICATION - DOD: verified root and family docs reject sterile sci-fi, low-poly toy silhouettes, flat colors, perfect primitives, and smooth noise blobs while requiring pressure-rated, corroded, abyssal, biological, geological, and NASA-punk cues. Rejected alternative: purely mathematical standards with no visual target. Estimate: visual quality impact primary; CPU saved only through fewer corrective effects.
- [x] Task 13 FINAL_FILE_COMMIT - DOD: saved all root docs, updated `AGENTS.md`, and appended final report to `Docs/AgentLogs/LOG_1700.md`. Rejected alternative: chat-only report. Estimate: no runtime microsecond claim; documentation handoff complete.

## Follow-up Audit - Texture Completeness

- [x] Texture production gap closed - DOD: added `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` for AI-assisted/procedural texture source generation, family recipes, map-stack rules, continuous quality lanes, and texture acceptance gates. Rejected alternative: claiming `3DMODEL_TEXTURES_MATERIALS.md` alone guarantees final texture quality. Estimate: visual quality gate primary; expected CPU/VRAM gain comes from rejecting bad source maps before they create material proliferation or runtime correction effects.

## Follow-up Audit - Hero Realism

- [x] Hero realism gap closed - DOD: added `3DMODEL_HERO_REALISM_OVERKILL.md` and routed it from `3dmodel.md` and `AGENTS.md` for close-camera, premium, and maximum-realism generated assets. It mandates reference contracts, high-poly source generation, deliberate retopology, anti-low-poly rejection gates, layered macro/meso/micro detail, bake maps, trim/decal strategy, render proof, and continuous overkill scaling. Rejected alternative: answering with advice while leaving no binding file. Estimate: visual quality primary; runtime gains come from moving hero detail into offline bakes instead of brute-force runtime geometry or corrective effects.

## Follow-up Audit - UI/Menu Standards

- [x] UI/menu production standards created - DOD: added `ui.md`, `UI_MENU_SCREEN_STANDARDS.md`, and `UI_DIEGETIC_HUD_STANDARDS.md`; routed them through `AGENTS.md`; updated `taste.md` with concise binding links, UI/menu taste, generated asset taste, and screenshot/rejection gates. Mandates used: UI diegetic physical interfaces, zero-GC UI data streaming, localization/font zero-alloc, cinematic fake-first, render/performance budgets. Rejected alternative: improving only the screenshot or writing subjective taste prose without production gates. Estimate: visual quality primary; expected low-tier savings come from zero-GC text, canvas separation, pooled RTs, and rejecting decorative UI effects before runtime implementation.

## Follow-up Audit - Project Bible Coverage

- [x] Cross-system bible gaps closed - DOD: audited root docs and mandate registry, then added `PROJECT_BIBLES.md`, `gameplay.md`, `world.md`, `audio.md`, `presentation.md`, `creatures.md`, and `quality.md`. Routed all through `AGENTS.md` and added concise links to `taste.md`. Rejected alternative: relying on archived reports or `.agents-skills` alone, because they do not provide a current root route for taste/system acceptance before implementation. Estimate: visual/gameplay quality primary; expected savings come from rejecting empty systems before code, avoiding decorative VFX/UI/audio, and enforcing proof gates before acceptance.
