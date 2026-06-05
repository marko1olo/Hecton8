# 1872 Player Body Visual Source Package

Evidence class: STATIC_SOURCE + STATIC_DOC. Runtime, screenshot, profiler, Unity validator, and build evidence are PENDING VERIFICATION.

## Scope

Owned outputs only:

- `Docs/Tasks/Status_1872.md`
- `Docs/AgentLogs/Rationale_1872.md`
- `Docs/AgentLogs/LOG_1872.md`
- `Docs/Reports/Batch18/1872_PLAYER_BODY_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1872_PLAYER_BODY_VISUAL_SOURCE_MATRIX.csv`

No Unity, build, source, prefab, asset, scene, meta, or binary edits were performed.

## Authority Loaded

- Root/project: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`
- Domain bibles: `player.md`, `tools.md`, `survival.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`
- Mandates: `QA_Evidence_Text_Filter_Audit.txt`, `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`, `MATH_AUP_Determinism_Sync.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Task-requested mandate `CORE_Interaction_Deterministic_AUP_NoRuntimeSearch.txt` was not present in `.agents-skills`; nearest AUP/determinism mandates were used. This is a stale prompt-path blocker, not a permission to lower interaction standards.

## Static Prefab Findings

Target prefab: `Assets/_Project/Prefabs/Player.prefab`

The player body is still driven by active primitive visual pieces. Static YAML scan found 17 active built-in primitive mesh references:

| Object | Prefab line | Mesh line | Built-in mesh | Active |
|---|---:|---:|---|---:|
| `Swim_LeftShoulder` | 525 | 532 | `10202` cube | 1 |
| `Swim_RightShoulder` | 615 | 622 | `10202` cube | 1 |
| `Swim_RightForearm` | 3047 | 3054 | `10202` cube | 1 |
| `Swim_RightUpperArm` | 3137 | 3144 | `10202` cube | 1 |
| `Swim_LeftGlove` | 3748 | 3755 | `10202` cube | 1 |
| `Swim_LeftUpperArm` | 3838 | 3845 | `10202` cube | 1 |
| `Swim_RightGlove` | 3928 | 3935 | `10202` cube | 1 |
| `Swim_Torso` | 4539 | 4546 | `10202` cube | 1 |
| `Swim_Pelvis` | 4629 | 4636 | `10202` cube | 1 |
| `Swim_LeftThigh` | 4719 | 4726 | `10202` cube | 1 |
| `Swim_RightThigh` | 4809 | 4816 | `10202` cube | 1 |
| `Swim_LeftCalf` | 4899 | 4906 | `10202` cube | 1 |
| `Swim_RightCalf` | 4989 | 4996 | `10202` cube | 1 |
| `Swim_LeftFin` | 5079 | 5086 | `10202` cube | 1 |
| `Swim_RightFin` | 5169 | 5176 | `10202` cube | 1 |
| `Swim_LeftForearm` | 5507 | 5514 | `10202` cube | 1 |
| `Suit_Visor` | 2812 | 2819 | `10207` primitive | 1 |

No disabled player body primitive remnant was found in the static primitive scan. `Suit_Visor` is active, not disabled. A disabled `Underwater_ShallowSunBeam` object exists, but it is unrelated to player body replacement.

The only non-primitive mesh found inside `Player.prefab` is `Suit_Diegetic_HUD_V4_Projection`, mesh GUID `6324179048a21564b92a102ebcd3a27c`, resolved to `Assets/_Project/Art/Meshes/M_Diegetic_HUD_V4_CurvedPanel.asset`. It is HUD projection geometry and is not a body replacement candidate.

## Current Materials

Resolved material paths:

- `62647c5379e618e40bf73270423be8dd` -> `Assets/_Project/Art/Materials/MAT_Diegetic_HUD_V4_Projection.mat`
- `0a7a9fcc24662af4caef76532d12fbe7` -> `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`
- `3c9d5ec2c203c77409d0cb6d22c962a1` -> `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat`

Unresolved in project-owned assets:

- `31321ba15b8f8eb4c954353edc038b1d`

The unresolved GUID appears on multiple primitive body parts. This is a static asset dependency risk and must be resolved before replacement validation.

Reusable support material candidates found by static search:

- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitGraphiteNoir.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitCyanEdge.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitAmberLatch.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_DirtyPressureGlass.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetPressureMetal.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetEdgeSteel.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WornLabel*.mat`
- `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`
- `Assets/_Project/Art/Shaders/SuitVisor.shader`

These are material/texture/shader candidates only. They do not satisfy the missing authored mesh requirement.

## Collider And Anchor Inventory

Colliders found:

- Root `Player` owns a `CapsuleCollider` for movement/body proxy. Keep it. Do not replace with visual mesh collision.
- `Suit_Visor` owns an active `SphereCollider`. Treat as visor/proxy risk; replacement must define whether this remains as interaction/proximity proxy or becomes a named `COL_Visor` proxy.

Anchors and systems to preserve:

- `HandAnchor`
- `Main Camera`
- `FirstPerson_Overlay_Camera`
- `SpaceCamera`
- `HUD_Render_Camera`
- `Suit_Diegetic_HUD_V4_Projection`
- `Suit_Visor`
- `VisorHUDController`
- `SuitHUDPresentationController`
- `PlayerToolManager.handAnchor`
- `PlayerSwimPresentationController` left/right hand guides and equipped tool weights
- `Swim_*Attachment` transforms for shoulders, upper arms, forearms, hands, torso, pelvis, thighs, calves, and fins

Tool, HUD, survival, camera, movement, and AUP truth ownership must remain outside the visual mesh package. Replacement art must attach to existing transforms or a documented new visual-only root.

## Source Route Findings

`PlayerSwimBlockoutRig.cs` is the current near-camera swim blockout visual driver. It exposes `showDebugCubes` and `SetDebugCubesVisible(bool visible)`, resolves named `Swim_*` transforms, enforces the viewmodel layer recursively, and provides stable attachment transform accessors for future authored art.

`PlayerSwimBlockoutRig.Body.cs` extends the same route for torso, pelvis, thighs, calves, and fins. This makes the primitive body a known blockout rig route, not an accepted product visual.

`PlayerSwimPresentationController.cs` owns profile-driven first-person presentation and tool support weights. It consumes movement/tool/transport truth and drives guides/blockout presentation. Replacement art must consume this route without changing gameplay truth.

`KineticCharacterAnimatorRuntime.cs` and hand IK/AUP routes provide future skinned or procedural visual sync options, but no current accepted skinned player body prefab or mesh was found in static search.

## Existing Source Candidate Classification

Accepted body mesh candidate:

- None found.

Reusable support candidates:

- Runtime visual proof suit materials and visor glass materials.
- Visor runoff/droplet textures.
- Suit visor shader routes.
- Swim presentation profile assets, as animation/presentation data only.
- Suit survival data assets, as gameplay data only.

Rejected as replacement body source:

- `Swim_*` primitive body pieces in `Player.prefab`
- `Suit_Visor` primitive visual
- `Suit_Diegetic_HUD_V4_Projection` HUD curved panel
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` HUD-only route
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab` transport route and prior primitive blocker
- Geology/procedural/world meshes under `Assets/_Project/Art/Meshes`

## Replacement Contract

Required body parts:

- Helmet and visor housing, not a sphere primitive.
- First-person gloves/hands, forearms, and shoulder/chest edge readable at near camera.
- Torso, pelvis, thighs, calves, and fins for third-person, corpse, reflection, shadow, and external-camera use.
- Tool mount/hand support detail aligned to `HandAnchor`, left/right hand guides, and existing attachment transforms.
- Hoses, hard-shell plates, gasket seams, worn labels, amber latches, cyan/green instrument strips, scratches, salt, wetness, and pressure glass detail.

Required material slots:

- Primary graphite/rubber suit shell.
- Wet pressure metal or hard plate shell.
- Visor glass with controlled transparency/reflection/scratch/droplet response.
- Cyan/green emissive instrument trims.
- Amber warning/latch trims.
- Worn label/seal decals or trim sheet.
- Packed normal/MRAO detail for suit body and gloves.

Collider split:

- Keep root movement `CapsuleCollider` as gameplay truth.
- Do not use visual mesh as movement collision.
- Add only explicit named proxy colliders such as `COL_Visor`, `COL_Torso_Interact`, or `COL_ToolMount` if an interaction owner requires them.
- Physics/proxy changes require their own source/prefab task and runtime validation.

Anchor preservation:

- Existing `HandAnchor`, guide transforms, camera stack, HUD projection, visor controller references, and swim attachment transforms must remain stable.
- No scene search or hot polling dependency may be introduced.
- GlobalRegistry remains cold identity/dependency injection only. Hot presentation signals must remain through first-party signal or immutable DTO routes.

LOD/HLOD expectation:

- LOD0: first-person arms/gloves/forearms/visor-adjacent hero detail, near third-person full suit.
- LOD1: simplified plates, reduced hose/trim geometry, retained silhouette and material readability.
- LOD2: coarse suit silhouette, readable helmet/torso/fins, no primitive/blockout look.
- HLOD/impostor: distant corpse/third-person only, dithered and hysteresis-gated. No alpha blend spam or material clone churn.

## Continuous Quality Consequences

`GlobalQualityWeight` must scale fidelity continuously and must not alter gameplay truth, DTO layout, save identity, authority route, or collision ownership.

- Compact range `0.00-0.35`: authored simplified suit silhouette, helmet/visor housing/gloves/forearms/chest/fins still readable; reduced texture resolution; minimal emissive trims; shortest LOD residency; no ugly blockout fallback.
- Middle range `0.35-0.65`: LOD0 near arms and torso longer; grime, labels, gasket seams, and wetness visible; stable tool-support presentation.
- High range `0.65-0.90`: richer bevels, detail normals, visor scratches/droplets, cyan/amber instrument trim clarity, longer LOD0 residency.
- Ultra range `0.90-1.00`: secondary fittings, hose/strap micro-detail, richer wet material response, higher first-person micro-detail budget. Still no change to survival, tool, movement, camera, or collider truth.

## Temporary Hide Decision

`showDebugCubes=false` / `SetDebugCubesVisible(false)` can hide primitive renderers while preserving attachment transforms. This is an emergency mitigation only. It is not visual acceptance because the player would either become visually incomplete or depend on missing replacement art.

Accepted route: preserve the rig/attachment transform truth and replace primitive renderers with authored non-primitive visual mesh packages.

## Accidental Regeneration Risk

The source route does not appear to create primitives at runtime in the scanned scripts, but `PlayerSwimBlockoutRig` resolves named `Swim_*` objects and exposes a debug cube visibility switch. If those primitive objects remain in the prefab or variants, they can be re-enabled by inspector defaults, prefab variants, or validation/editor routes.

Mitigation route:

- Move primitive mesh renderers to an explicit hidden debug-only path or remove renderer use after authored replacement lands.
- Preserve transform and attachment names until consuming systems are migrated.
- Add static prefab validation that rejects active built-in primitive body meshes on player visual roots.
- Add visual proof capture after replacement.

## Proof Ladder Required For Acceptance

Static source proof:

- Prefab YAML contains no active built-in primitive body MeshFilters on player visual roots.
- Authored mesh and material GUIDs resolve to project-owned assets.
- Collider proxies are named and separate from visual meshes.
- Tool/HUD/camera/hand guide references remain stable.

Visual proof:

- First-person swimming/tool-held screenshot.
- Third-person or external player body screenshot.
- Corpse/distant or reflection/shadow route screenshot if that route is in scope.
- Compact/middle/high/ultra quality captures or equivalent controlled toggles.

Runtime proof:

- Unity validator or editor static validator after prefab edit.
- Build/compile only when allowed by task scope and CPU/build rules.
- Profiler evidence if render path adds features or material/LOD logic.

## First 20 Minutes Product Route Impact

The player body is a first-session product-face object because it enters first-person hands, tools, visor/HUD, swimming, screenshots, and survival feedback. Current active cube body parts and primitive visor fail the taste floor. Replacement must ship as an authored suit/visor package before product-facing capture or public proof.

## Blockers

- No accepted authored body mesh source exists in static search.
- `Player.prefab` currently contains active primitive body/visor MeshFilters.
- Material GUID `31321ba15b8f8eb4c954353edc038b1d` did not resolve under project-owned assets.
- Task forbids Unity, build, screenshots, runtime validator, and actual prefab/source/asset edits; therefore acceptance remains static route package only.
