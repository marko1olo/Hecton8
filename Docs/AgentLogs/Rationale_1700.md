# Rationale_1700 - 3D Model Generation Standards Director

Status: PENDING VERIFICATION

## Decision 001 - Authority Shape

Problem: Existing procedural generation lanes can create meshes, but without a single binding geometry contract they can drift into primitive blobs, un-beveled boxes, stretched UVs, and high-poly colliders.
Solution: Create a root `3dmodel.md` authority bible plus specialist root files for hard-surface modules, flora/coral, fauna, geology/rocks, equipment, and texture/material rules. Update `AGENTS.md` with a short routing hook.
Rejected Alternatives: A single vague art manifesto was rejected because implementation agents need numeric gates. Editing every generator now was rejected because the assignment is standards-only and concurrent agents own those files.
Scalability potential: Low tier gets low triangle counts, atlas reuse, baked masks, proxy colliders, and shader fakes. Middle tier raises mesh density and material detail. High tier keeps silhouette and richer near-field detail. Ultra tier spends saved cycles on denser authored LOD0 and more expressive baked maps without changing runtime authority.
Hardware Impact: Expected i3/MX350 gain is avoided runtime mesh/texture allocation, fewer collider narrow-phase triangles, lower SetPass from atlas/material slot discipline, and predictable mesh upload. Microseconds saved: PENDING PROFILER; estimated avoidance is 100-3000 us per avoided runtime mesh/collider build depending on asset size.

## Decision 002 - Existing Generator Risk

Problem: The codebase already contains editor builders for coral, seaweed, geology, flora materials/textures, wreckage, and station box surrogates, but several paths can still emit simple blobs, ribbons, boxes, or broad proxy meshes if no shared acceptance law forces beveling, UV density, tangent validity, LOD chain ownership, and material slots.
Solution: Standards must state generator acceptance gates, not just artistic direction. Existing good practices such as geology LOD sets, collision proxy assets, texture import enforcement, MRAO verification, and mesh data layout should become mandatory baseline for every future family.
Rejected Alternatives: Rewriting all generators now was rejected because this agent owns standards, not domain implementation. Ignoring current scripts was rejected because the standards must address real output risks.
Scalability potential: Low uses baked masks, fewer islands, coarser LODs, and simple proxies. Middle uses better silhouettes and material variation. High adds denser bevels/ridges, tighter UV density, and richer baked detail. Ultra allows hero silhouettes, layered material wear, and more elaborate offline decimation as long as runtime data remains static.
Hardware Impact: Expected gain on i3/MX350 is reduced hot-path mesh churn, reduced physics narrow phase, and fewer material state changes. Microseconds saved: 200-3000 us avoided on frames where a bad runtime generator or LOD0 collider would otherwise execute; PENDING PROFILER.

## Decision 003 - Offline Permanence

Problem: The project has runtime-facing procedural systems and editor generation systems. Without a hard boundary, future agents can justify runtime mesh work for convenience and violate 0 B/frame, upload, and physics budgets.
Solution: `3dmodel.md` starts with an absolute offline generation law and requires serialized mesh, prefab, material, texture, LOD, and collision proxy assets before runtime consumption.
Rejected Alternatives: Allowing runtime generation for small meshes was rejected because small exceptions become hot-path debt and are hard to audit under concurrent agent work. Allowing runtime collider cooking was rejected because PhysX cooking is a spike source and not visual value.
Scalability potential: Low tier benefits from no surprise generation spikes and pre-decimated assets. Middle gains stable streaming. High and Ultra spend runtime budget on richer shaders, lighting, and near-field art rather than rebuilding geometry.
Hardware Impact: Expected i3/MX350 gain is elimination of avoidable vertex array manipulation, tangent rebuild, texture pixel fill, and collider cooking during gameplay. Estimated avoided frame spikes: 1000-5000 us for bad runtime generation events; PENDING PROFILER.

## Decision 004 - Geometry Quality Gates

Problem: Existing generated visual assets can pass functional generation while still reading as cubes, blobs, tubes, ribbons, or flat color surfaces. That fails the Deep Sea Noir/NASA-punk target and wastes runtime budget on compensating effects.
Solution: Define concrete topology laws: hard edges need bevels and split/weighted normals; organic assets need nonprimitive growth structure and semantic vertex channels; UVs need conformal or justified triplanar routes, atlas packing, padding, and channel-packed material masks.
Rejected Alternatives: Letting shaders hide poor topology was rejected because PBR depends on actual surface normals and bevels for believable highlights. Letting agents rely on generated colors was rejected because material response needs authored or baked texture data.
Scalability potential: Low keeps mandatory silhouettes and simplest masks. Middle improves density and atlas fidelity. High adds richer bevels, branch/ridge detail, and material variation. Ultra adds hero bevel segments, denser organic silhouettes, and richer baked maps without changing runtime truth.
Hardware Impact: Expected i3/MX350 gain is fewer SetPass/material changes, less runtime deformation work, and fewer corrective VFX. Estimated saved CPU: 20-1000 us depending on scene density; PENDING PROFILER.

## Decision 005 - Collision And Validation

Problem: Visual meshes are attractive to reuse as collision because they already fit the shape, but that routes decorative triangles into PhysX truth and creates runtime CPU waste. Bad generated geometry can also be saved silently if validation happens after asset writes.
Solution: Ban LOD0 MeshCollider use, require `COL_*` primitive or convex proxies, cap convex proxy triangles, and require validation before save/prefab writes. Add black-box bake ring requirements for invalid geometry and exceptions.
Rejected Alternatives: Using LOD0 MeshCollider for rocks/coral was rejected because narrow-phase cost scales with decorative triangles. Post-save audit was rejected because corrupted assets can enter the project before failure is visible.
Scalability potential: Low uses coarse primitives and few convex hulls. Middle uses better proxy decomposition. High and Ultra keep the same collision truth while visual LOD0 becomes richer; physics cost remains stable.
Hardware Impact: Expected i3/MX350 gain is reduced PhysX broad/narrow-phase work and no runtime collider cooking. Estimated saved CPU: 200-2000 us in collision-heavy scenes; PENDING PROFILER.

## Decision 006 - Rendering Compatibility Audit

Problem: A geometry bible can over-focus on topology and still allow generated assets that break URP batching, GPU Resident Drawer, BRG ownership, or continuous quality scaling.
Solution: Added a root rendering compatibility section requiring SRP Batcher-compatible shared materials, stable vertex streams, finite conservative bounds, dithered LOD cross-fade, shared meshes/materials, and continuous `GlobalQualityWeight` scaling.
Rejected Alternatives: Leaving rendering compatibility implicit was rejected because future agents may create material-per-variant assets or binary LOD branches. Adding runtime batching code was rejected because this task owns standards only.
Scalability potential: Low keeps stable shared materials and cheap HLOD. Middle raises density without new material surfaces. High and Ultra add richer visual detail while preserving the same runtime material and collision identities.
Hardware Impact: Expected i3/MX350 gain is fewer SetPass calls, stable GPU culling bounds, and no per-instance material mutation. Estimated saved CPU: 20-250 us in material-heavy scenes; PENDING PROFILER.

## Decision 007 - Texture Generation Playbook

Problem: The root bible and texture/material standard defined UVs, atlases, PBR packing, import settings, and rejection gates, but did not fully define how high-quality texture families are produced from AI-assisted or procedural sources. Without this, agents could satisfy import rules while still shipping flat noise, baked-light AI outputs, or random masks.
Solution: Added `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` and routed it from `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, and `AGENTS.md`. The playbook defines source prompts, material family recipes, procedural field usage, mandatory map stacks, continuous quality lanes, and texture acceptance gates.
Rejected Alternatives: Saying textures will be good because UV/import rules exist was rejected as false. Letting Unity runtime correct weak textures through effects was rejected because it spends frame time to hide bad source art. Requiring only human-authored textures was rejected because the user permits AI texture generation, but the output still needs PBR validation.
Scalability potential: Compact uses shared 512/1024 maps and baked AO; middle increases map size and decal clarity; high uses richer normals and atlas families; ultra spends source bake precision and decal layers on hero assets while preserving the same runtime material contracts.
Hardware Impact: Expected i3/MX350 gain is indirect but real: fewer material variants, fewer corrective overlays, fewer uncompressed/import mistakes, and less shader waste from unusable masks. Estimated saved CPU: 20-250 us in material-heavy scenes through fewer SetPass/material correction paths; VRAM savings depend on rejected maps and are PENDING IMPORT AUDIT.

## Decision 008 - Hero Realism Overkill Standard

Problem: The baseline bible rejects broken geometry and weak texture generation, but a valid generated mesh can still look merely acceptable if it lacks reference discipline, high-poly source detail, deliberate retopology, render proof, and layered macro/meso/micro visual hierarchy.
Solution: Added `3DMODEL_HERO_REALISM_OVERKILL.md` and routed it from `3dmodel.md` and `AGENTS.md`. The new standard activates for close-camera, premium, cinematic, AAA, and maximum-realism generated assets. It requires reference contracts, high-poly procedural sources, retopology rules, anti-low-poly rejection, material/story plausibility, bake maps, trim/decal strategy, render proof gates, and continuous quality scaling.
Rejected Alternatives: Raising triangle budgets as the primary answer was rejected because heavy primitive shapes still look cheap and can break MX350 budgets. Writing only motivational art direction was rejected because agents need executable gates. Runtime corrective shaders were rejected because realism must be bought by offline bakes and source quality, not hot-path compensation.
Scalability potential: Compact keeps macro silhouette, key bevels, bakes, and strong LOD falloff. Middle adds meso cuts and material masks. High adds richer bake/decal detail. Ultra adds hero-only sculpt/bake precision and tighter LOD transitions without changing runtime authority.
Hardware Impact: Expected i3/MX350 gain is indirect: better detail per triangle through bakes, fewer corrective runtime effects, stable collider proxies, and preserved batching/material contracts. Estimated saved CPU remains PENDING PROFILER; likely 20-250 us avoided in material-heavy scenes and much larger avoided spikes if agents would otherwise brute-force runtime detail.

## Decision 009 - UI/Menu Production Standards

Problem: `taste.md` correctly said that interfaces must feel like fragile instruments, but it did not define binding production gates for menus, HUD, terminals, cockpit panels, scanner screens, layout, typography, color roles, interaction, zero-GC text, localization, or screenshot review. The provided screenshot exposes the failure mode: decorative gridlines, random diagonals, floating buttons, weak hierarchy, and fake telemetry can look "technical" while carrying no useful state.
Solution: Added root `ui.md`, specialist `UI_MENU_SCREEN_STANDARDS.md`, and specialist `UI_DIEGETIC_HUD_STANDARDS.md`. Updated `AGENTS.md` so future UI/menu/HUD agents must route through these files. Updated `taste.md` concisely with production-standard links, UI/menu taste additions, generated asset taste additions, and new rejection gates for decorative UI templates and primitive generated assets.
Rejected Alternatives: Redrawing only the screenshot was rejected because the user requested durable documents for future generation. Generic UI advice was rejected because HECTON-8 needs diegetic instrument rules, not website/product UI taste. Purely decorative animation was rejected because UI motion must report state. Runtime-heavy UI polish was rejected because UI must obey zero-GC and MX350 budget constraints.
Scalability potential: Compact keeps readable hierarchy, static atlases, zero-GC text, low-cadence readouts, and physical/diegetic carrier cues. Middle adds richer screen material, route diagrams, and failure states. High adds better scanline/glass/detail maps and smoother transitions. Ultra adds secondary screen response, richer degradation, and cinematic panel material while preserving the same controls and state truth.
Hardware Impact: Expected i3/MX350 gain is fewer Canvas rebuild spikes, no hot-path text allocation, fewer RenderTexture surprises, and rejection of costly decorative effects. Estimated saved CPU: 100-1000 us if agents would otherwise mutate TMP strings or rebuild canvases frequently; exact proof PENDING PROFILER.
