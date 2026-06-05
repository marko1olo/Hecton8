# FAUNA PHOTIC CREATURE VISUAL PACKAGE

Date: 2026-06-04  
Scope: offline audit and task-ready package for first-hour surface, photic, and medium-depth fauna visuals.  
Execution boundary: no Unity, no build, no Assets edits. Evidence is static source, static asset path, and existing report inspection only.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `creatures.md`
- `3DMODEL_FAUNA.md`
- `3dmodel.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `world.md`
- `ecosystem.md`
- `ai.md`
- `survival.md`
- `water.md`

Mandates loaded:

- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `REND_GPU_Driven_Animation_VAT.txt`
- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `QA_Evidence_Text_Filter_Audit.txt`

## Result

First-hour fauna visuals are not production-ready today. The source-side offline routes exist for rigging, VAT, texture baking, and prefab assembly, but the required raw fauna sources and generated creature outputs are absent from the inspected target paths. Existing placeholder fauna prefabs are primitive proxy assets with flat materials and must not ship as first-hour visible creature art.

The package below defines the required first-hour roles, current routes, blockers, prompt targets, proof names, placement constraints, and quality-tier consequences.

## First-Hour Visible Fauna Roles

### 1. Small Harmless Shoals

Primary data candidates:

- `ShoreSkimmer`
- `KelpRaylet`
- `SiltDrifter`

Design job:

- Sell immediate surface and photic life without combat pressure.
- Show water scale, current direction, oxygen return routes, safe shallows, and nearby biome transitions.
- React to player motion, light, sonar, predator presence, and route hazards. No random wandering cloud.

Visual requirements:

- Authored organic body plan, not capsules, spheres, tubes, or built-in primitive meshes.
- Clear silhouette at 8 m to 30 m.
- Wet translucent or iridescent photic material response.
- At least LOD0, LOD1, LOD2, and far silhouette/VAT form.
- Texture package: albedo, normal, packed mask, optional restrained emission.
- VAT route preferred for groups. No Animator or SkinnedMeshRenderer for boid swarm rendering.

Current status: blocked by missing raw/generator outputs.

### 2. Curious Medium Fauna

Primary data candidates:

- `LanternSifter`
- `WallGlider`
- Larger `KelpRaylet` variant if body silhouette proves distinct.

Design job:

- Inspect player from safe distance.
- Create soft guidance and route memory through motion, turn arcs, and biolum pulse.
- Establish the "creatures are intelligent ecology" promise without forcing combat.

Visual requirements:

- Recognizable head/sensory anatomy and readable non-hostile behavior.
- Medium-depth readability under photic haze and caustics.
- Biolum organs that are authored by material mask and pulse atlas, not random glow.
- Animation must communicate curiosity: approach, orbit, retreat, look-at, and group response.

Current status: blocked by missing visual body package and proof.

### 3. Warning or Aggressive Small Predator

Primary data candidates:

- `NeedleHunter`
- `PocketAmbusher`

Design job:

- Teach that route edges and medium-depth pockets can be dangerous.
- Pressure oxygen planning and route discipline without turning the first hour into constant combat.
- Provide warning tells before hit windows.

Visual requirements:

- Predator silhouette must be readable before aggression.
- Warning color or emission must mark threat zones without looking like arcade UI.
- Attack anatomy must be coherent: jaw, spine, fin blades, or ambush mouth, not a red capsule.
- Predator placement belongs at hazard pockets, medium-depth edges, cave mouths, or optional route pressure zones.

Current status: blocked by missing visual body package and proof. Existing data supports hunter behavior intent, but visual readiness is absent.

### 4. Background Silhouettes

Current candidate:

- `Assets/_Project/Art/Meshes/World/Photic1428/MESH_H8_PhoticFishSilhouettes_1430.asset`
- `Assets/_Project/Art/Shaders/Hecton_AbyssalSwarmProcedural.shader`

Design job:

- Add distant parallax life, migration bands, and scale.
- Point toward route landmarks or away from predator pockets.
- Never substitute for close-range fauna bodies.

Visual requirements:

- Distant-only unless upgraded with full body, material, animation, and proof.
- Strong water-column integration: haze, caustic breakup, school direction.
- Must not become confetti scatter.

Current status: unknown for visual quality, acceptable only as background route cue until screenshot and in-context proof exist.

### 5. Biolum Navigation Fauna

Primary candidates:

- `LanternSifter`
- emission variants of `KelpRaylet` and `SiltDrifter`
- distant silhouette pulse groups

Design job:

- Mark breathable route return, safe ledges, current corridors, wreck/cable approach, and depth transition.
- Support oxygen and danger pacing with living cues, not UI-only markers.

Visual requirements:

- Material mask alpha owns emission.
- Pulse atlas loops cleanly.
- Pulse frequency varies by role: calm route, alert scatter, predator warning, deep-return cue.
- Biolum in photic routes must enhance beauty and readability, not darken the scene.

Current status: blocked by missing baked textures and pulse atlases.

## Existing Routes and Status

| Route | Evidence | Status | Reason |
|---|---:|---|---|
| `Assets/_Project/Art/Fauna/Raw` | path absent | BLOCKED | No raw FBX, OBJ, or mesh fauna source found in required source folder. |
| `Assets/_Project/Art/Generated/Fauna` | path absent | BLOCKED | No generated fauna mesh route available for inspection. |
| `Assets/_Project/Art/Generated/Fauna/Rigged1610` | generator target | BLOCKED | `AbyssalAnatomyStudio1610` targets this route, but generated output path is absent. |
| `Assets/_Project/Art/Generated/Fauna/VAT1610` | generator target | BLOCKED | VAT output route exists in source only. |
| `Assets/_Project/Art/Textures/Creatures/Fauna1725` | baker target | BLOCKED | Texture baker route exists in source only; output path is absent. |
| `Assets/_Project/Art/Materials/Fauna/VAT1610` | generator target | BLOCKED | VAT material output route absent. |
| `Assets/_Project/Prefabs/Nature/Fauna` | path absent | BLOCKED | No nature fauna prefabs found. |
| `Assets/_Project/Prefabs/Nature/Fauna/Rigged1610` | generator target | BLOCKED | Rigged prefab output route exists in source only. |
| `Assets/_Project/Prefabs/Creatures` | path absent | BLOCKED | `FaunaPrefabFactory` output route absent. |
| `AbyssalAnatomyStudio1610.cs` | source present | ROUTE_READY_SOURCE_ONLY | Offline rig/VAT generator source exists; no output proof. |
| `FaunaTextureBaker.cs` and `FaunaTextureBaker.compute` | source present | ROUTE_READY_SOURCE_ONLY | Offline texture baker source exists; no baked texture proof. |
| `FaunaPrefabFactory.cs` | source present | ROUTE_READY_SOURCE_ONLY | Prefab assembly source exists; no prefab output proof. |
| `FaunaDataTemplate_*.asset` | data present | DESIGN_DATA_PRESENT | Role data exists for passive and hunter species; visual body proof absent. |
| `ProceduralRule_rule_fauna_passive.asset` | data present | PLACEMENT_RULE_PRESENT | Passive fauna uses `fauna_density`, 0 m to 2500 m, 2 to 6 anchors. |
| `ProceduralRule_rule_fauna_predator.asset` | data present | PLACEMENT_RULE_PRESENT | Predator fauna uses `hazard_density`, 120 m to 5000 m, 1 to 2 anchors. |
| `WorldRuntime/ProceduralPlaceholders/Fauna` | prefabs present | BLOCKED_PROXY_ONLY | Built-in primitive meshes, flat materials, placeholder markers. |
| `WorldSupport/Final/PFB_Support_CreatureSpawn_*.prefab` | prefabs present | UNKNOWN_PLACEMENT_SUPPORT_ONLY | Support markers are not creature body art. |
| `MESH_H8_PhoticFishSilhouettes_1430.asset` | mesh present | UNKNOWN_ROUTE_CUE_ONLY | Potential distant silhouette support; no visual proof or close-body readiness. |

## Primitive and Proxy Rejection

Reject from first-hour visible fauna route when any of these are true:

- Built-in Unity primitive mesh file IDs are the visible body.
- `WorldProceduralPlaceholderMarker.generatedPlaceholder` is true.
- Material is flat URP base color with no authored texture package.
- Procedural triangle shader is the only body representation at close range.
- No authored silhouette, appendage logic, sensory anchor, LOD chain, collider suite, or material zones.
- No proof artifacts: textured render, flat override render, wireframe/topology, LOD strip, hitbox overlay, VAT loop, material channel audit.

Existing `PFB_family_creature_spawn_passive_Placeholder.prefab` and `PFB_family_creature_spawn_predator_Placeholder.prefab` fail these gates. They can remain route scaffolding only.

## Required Texture, VAT, and Material Package

Per close or medium-range species, produce:

- `TX_Fauna1725_Albedo_<Species>.png`
- `TX_Fauna1725_Normal_<Species>.png`
- `TX_Fauna1725_MaskV1_<Species>.png`
- `TX_Fauna1725_BiolumPulse64_<Species>.png`
- `MAT_Fauna_<Species>_LeviathanOrganic.mat`
- `MESH_Fauna_<Species>_LOD0.fbx` or `.asset`
- `MESH_Fauna_<Species>_LOD1.fbx` or `.asset`
- `MESH_Fauna_<Species>_LOD2.fbx` or `.asset`
- `GEN_FaunaVAT1610_<Species>*` for swarm or distant animation route
- `GEN_FaunaRig1610_<Preset>_<Species>*` for hero/medium creature route
- `PFB_Fauna_<Species>.prefab`

Material contract:

- Use `Hecton_LeviathanOrganic` for close and medium fauna unless a stricter fauna shader exists.
- Base map must carry organic hue, wetness variation, and recognizable species identity.
- Normal map must define fins, plates, membrane ribs, scars, gills, and sensory ridges.
- Packed mask uses the existing contract: R metallic or chitin response, G AO, B smoothness, A emission.
- Emission is controlled by mask alpha plus pulse atlas, not full-body glow.
- No baked lighting, no muddy albedo, no single-color blob texture.

## Required QA Proof Names

Per species:

- `QA_FAUNA_<SPECIES>_textured_render_20260604.png`
- `QA_FAUNA_<SPECIES>_flat_material_override_20260604.png`
- `QA_FAUNA_<SPECIES>_wire_topology_20260604.png`
- `QA_FAUNA_<SPECIES>_lod_distance_strip_20260604.png`
- `QA_FAUNA_<SPECIES>_hitbox_overlay_20260604.png`
- `QA_FAUNA_<SPECIES>_vat_loop_20260604.mp4`
- `QA_FAUNA_<SPECIES>_compact_readability_20260604.png`
- `QA_FAUNA_<SPECIES>_material_channels_20260604.md`

Route-level proof:

- `QA_FAUNA_FIRSTHOUR_surface_photic_route_readability_20260604.png`
- `QA_FAUNA_FIRSTHOUR_oxygen_return_cue_alignment_20260604.png`
- `QA_FAUNA_FIRSTHOUR_predator_warning_distance_20260604.png`
- `QA_FAUNA_FIRSTHOUR_low_middle_high_ultra_lod_strip_20260604.png`

No readiness claim is valid without these proof artifacts or an equivalent stricter artifact set.

## Placement Constraints

Fauna placement must be authored by route logic, density channels, shelter, current, oxygen pacing, and danger pacing.

Rules:

- No random scatter.
- Passive shoals sit on `fauna_density` near safe photic corridors, reef shelves, current edges, and oxygen-return landmarks.
- Curious medium fauna orbit landmarks, wreck approaches, caves mouths, route splits, and safe overlook spaces.
- Predator fauna uses `hazard_density` and should not occupy first calm exit space.
- `NeedleHunter` and `PocketAmbusher` are suitable after the oxygen loop is taught, near medium-depth edges, cave mouths, or optional risk pockets.
- Distant silhouettes may cross in front of landmarks only if they reinforce direction.
- Biolum navigation fauna must align with return paths, breathable pockets, or readable safe corridors.
- Surface and photic routes stay bright and legible. Darkness belongs to depth, caves, interiors, storms, and temporary eclipse windows.

Pacing:

- 0 m to 60 m: harmless shoals and distant silhouettes. High beauty, low threat.
- 60 m to 140 m: curious medium fauna and mild biolum route cues. Oxygen loop becomes active.
- 120 m and deeper: first warning predator pockets may appear where escape route and visual tell are clear.
- Medium-depth transition: predator pressure, biolum navigation, and route landmarks must converge without hiding weak art in darkness.

## Low, Middle, High, Ultra Consequences

Low:

- Fewer agents, stronger authored pathing, VAT or impostor swarms, 512 to 1024 creature textures, short LOD distance.
- Must preserve species silhouette, material identity, route cue behavior, and oxygen/danger truth.
- No "ugly mode"; reduction is density, cadence, and texture resolution, not primitive visuals.

Middle:

- Normal first-hour population, 1024 to 2048 creature textures, VAT shoals plus limited medium skinned hero fauna.
- Biolum route cues active with conservative pulse counts.
- Predator pockets use fewer simultaneous actors but keep warning tells.

High:

- Longer LOD distances, richer normal and mask maps, stronger caustic integration, more reactive shoal turns, better wet material response.
- Medium curious fauna can hold screen presence longer without becoming a frame-time tax.

Ultra:

- 2048 to 4096 texture package where justified, denser far silhouettes, richer secondary fins and pulse variation, longer readable silhouettes, more route-cued migration bands.
- Visual overkill does not change gameplay truth, DTO layout, spawn authority, or save identity.

## Top Blockers

1. `Assets/_Project/Art/Fauna/Raw` is absent. There is no inspected raw body source for the first-hour fauna set.
2. Generated fauna mesh, VAT, material, texture, and prefab output routes are absent from the inspected target paths.
3. Existing fauna placeholder prefabs are primitive, flat, and explicitly proxy-marked. They are blocked for first-hour visible creature art.
4. No current proof artifacts exist for textured render, flat override, topology, LOD, hitbox, VAT loop, route readability, or material channel audit.
5. Historical logs contain source-only progress and unrelated compile blockers. No Unity or build verification was run for this package.

## Task-Ready Output List

Immediate asset tasks:

- Author or import raw body sources for `ShoreSkimmer`, `KelpRaylet`, `SiltDrifter`, `LanternSifter`, `WallGlider`, `NeedleHunter`, and `PocketAmbusher` under the fauna raw route.
- Bake texture packages using the `FaunaTextureBaker` contract.
- Generate rigged and VAT outputs through the offline anatomy route.
- Assemble prefabs through `FaunaPrefabFactory`.
- Capture the required QA proof set before declaring any route production-ready.

Immediate design tasks:

- Bind passive shoals to route-readable `fauna_density` anchors.
- Bind predators to medium-depth `hazard_density` pockets with warning tells and escape routes.
- Assign biolum navigation fauna to oxygen return, safe corridor, and route transition landmarks.

Final status: offline package prepared. Production readiness remains blocked until source assets, generated outputs, prefab assembly, and proof artifacts exist.
