# Asset Owner 14 - Sky / Aegir / Cloud Slot Proof Packet

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_IMAGE_QA` only.
Scope: execution packet for proving or replacing sky, Aegir, cloud, moon, and surface hero source slots. This packet is not Unity proof.

No Unity launch, Play Mode, import, material edit, prefab edit, scene save, build, Frame Debugger capture, profiler capture, Addressables build, or `Assets/` mutation was performed.

First-20 route moment: bright first surface exit with readable sky, Aegir, moons, clouds, ocean surface, shoreline, and photic route context.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

## Boundary

This packet can assign the proof route only. It cannot accept the current sky, Aegir, moon, cloud, or surface material result.

Allowed evidence here:

- `STATIC_DOC`: authority docs, matrices, packet text.
- `STATIC_SOURCE`: serialized/source reachability, material path lists, importer/source path lists.
- `STATIC_IMAGE_QA`: contact sheets or still-image review of source candidates.

Rejected evidence inflation:

- Static material path does not prove active renderer use.
- Contact sheet does not prove Unity shader response.
- `01_ORBIT` or prologue references do not prove `02_HECTON_WORLD` first-route visibility.
- Scene YAML/static refs do not prove importer settings, VRAM, SetPass, shader variants, Frame Debugger pass order, screenshots, or runtime route quality.

## Exact Blocker Groups

- Aegir baked disc too soft/toy-like for hero surface use. `TX_H8AegirGasGiantBakedDisc_1428.png` cannot be final hero proof without replacement or strong Unity slot/screenshot proof.
- Sky/cloud source slots need readback. `Mat_HectonSky.mat`, `Mat_HectonSky_CloudOverlay.mat`, `_MainCloudTex`, `_HighCloudTex`, `_MainCloudAtlas`, `MAT_AegirSky_Master.mat`, and active world skybox slots must be read in Unity before claims.
- Prologue/orbit refs cannot prove `02_HECTON_WORLD` route. `01_ORBIT` and `_PROLOGUE_CONTENT` may provide sources or candidates only.
- Moon/sky material reuse risks. Moon materials using generic terrain/rock/basalt maps are rejected for hero celestial views until material role, route visibility, and screenshot proof exist.

## Visual Requirements

- Surface first viewport must be bright, legible, beautiful, and route-readable.
- Aegir, moons, clouds, and sky must read premium: texture detail, cloud bands, atmosphere softness, scale, and material response.
- `BEST ILLUST` is the primary surface target for this packet: huge readable Aegir/gas-giant body, layered high clouds, bright coastline/island context, ocean/whitewater read, vegetation/route scale, and no surface-dark concealment.
- Previous-development sky/cliff/gas-giant references prove the direction is valid only if the active route keeps bright surface legibility, cloud-band texture quality, limb softness, and ocean/shoreline context.
- Ocean surface, shoreline, and photic shallows must meet or exceed the Subnautica-level floor on every hardware lane.
- Darkness, fog, bloom, exposure crush, vignette, storm grade, or noir post must not hide weak sky/celestial/water art.
- Storms and eclipse may be temporary route states. They do not define default surface presentation.

## Safe Execution Route

1. Run Unity readback only when the ASSET_OWNER_06 gate is green: CPU <= 50 percent, no busy Unity/build/import/shader/package process, no dirty scene prompt.
2. Open scenes read-only. Do not save scenes or project.
3. For `02_HECTON_WORLD`, record effective skybox, sky renderer, Aegir renderer, cloud overlay, moon renderer, shader, texture slots, null refs, missing shader warnings, active/inactive state, and route visibility.
4. For `00_BOOTSTRAP`, `01_MAIN_MENU`, and `01_ORBIT`, record skybox/material state as context only. Do not let orbit/prologue refs satisfy world-route proof.
5. Read importer roles for every candidate sky/Aegir/cloud/moon texture: sRGB, texture type, compression, mip chain, streaming mips, platform max size, alpha/channel role, and source path.
6. Build contact sheets outside `Assets/` only when source candidates need image QA. Include source filename, family, role, and rejection note.
7. Capture bright surface Game View and Scene View screenshots only after readback identifies active slots. Do not use dark/storm-only views as acceptance.
8. Capture Frame Debugger/Stats for skybox, Aegir/cloud/moon renderers, SetPass, batches, shader keywords/variants where visible.
9. Record Texture Memory, Total Reserved Memory, VRAM pressure state, and async upload budget if runtime/import proof is later authorized.
10. If replacement is needed, author/import through route-owned material families and import-role rows first. No raw YAML, no scene save, no material clone, no runtime wrapper.
11. Compare future surface captures against `CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`. The comparison must state whether `BEST ILLUST` surface composition, Aegir/gas-giant scale, cloud layering, ocean/whitewater read, and route scale signals pass or fail.

## Required Readback Targets

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- `Assets/_Project/Art/Materials/Sky/MAT_AegirSky_Master.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceCloudDeck_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceGasGiantDisc_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceGasGiant_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8AegirGasGiantReal_1428.mat`
- `Assets/_Project/Art/Materials/Mat_AegirHazeOverlay.mat`
- `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
- `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_*.mat`
- Candidate source folders: `Assets/_Project/Art/TEXTURES`, `Assets/_Project/Art/TEXTURES/Sky`, `Assets/_Project/Art/Skyboxes`, `_PROLOGUE_CONTENT/Textures/Planets`, `_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT`.

## Acceptance Gates

All gates are required for route acceptance:

- Skybox/material slot readback: active world skybox, sky renderer, Aegir/cloud/moon renderer materials, shaders, slots, nulls, and active state.
- Importer readback: role-correct color space, type, compression, mips, streaming mips, platform max size, alpha/channel semantics.
- Bright surface screenshots: Game View and Scene View proving sky/Aegir/moons/clouds/ocean surface without darkness/post concealment.
- Mandatory reference comparison: explicit pass/fail rows for `BEST ILLUST`, previous-development cliff/sky/gas-giant reference, and sky/ocean/shoreline context.
- Frame Debugger/Stats: visible skybox/Aegir/cloud/moon passes, SetPass/batch notes, shader/variant risk notes.
- Memory/VRAM: texture memory, total reserved memory, VRAM pressure and mip downgrade risk. No residency claim from source size alone.
- Route scene evidence: `02_HECTON_WORLD` proof only. Orbit/prologue evidence remains candidate/source context.
- Visual floor: Aegir/moons/clouds must be premium and legible; surface must be bright and Subnautica-level or better.

## Rejection Gates

Reject if:

- Aegir reads soft, toy-like, muddy, low-resolution, or procedural-scribbled in bright surface capture.
- Active sky/cloud slots are null, missing, stale, or only proven by orbit/prologue context.
- Moons visibly reuse generic rock/basalt/terrain textures as hero celestial art.
- Screenshots rely on darkness, fog, bloom, exposure crush, storm grade, or vignette to conceal weak art.
- Future captures lack the mandatory huge Aegir/gas-giant read, layered clouds, bright coastline/ocean context, or route scale required by the image-read digest.
- Import roles are missing or channel semantics are unknown.
- Texture memory/VRAM proof is absent after route-visible import or material change.
- Raw YAML edits, scene saves, material clones, or runtime wrappers are proposed.

## Regression Model

- CPU: readback packet changes no runtime CPU. Future renderer/material work must prove no new hot polling, scene search, material instantiation, or render feature over `0.1 ms` without load-shed.
- GC: no runtime code touched. Future HUD/material/renderer changes need `0 B/frame` proof before acceptance.
- VRAM: source presence is not residency. Future imports must fit compact 1800 MB VRAM ceiling, 900 MB texture budget, mip pressure rules, and async upload budget.
- SetPass: material slot replacement must not inflate SetPass or break SRP Batcher without Frame Debugger proof.
- Shader variants: new shader keywords or variants require variant count proof and loading-screen warmup route where applicable.
- Correctness: one fact, one owner, one route, one proof artifact. `02_HECTON_WORLD` owns route proof for this packet.
- Visual floor: any regression to dark, flat, blurry, muddy, or placeholder-looking surface/sky/Aegir/moon/cloud art is a hard reject even if faster.

## GlobalQualityWeight Consequences

- Low: preserve bright composition, readable ocean color, premium Aegir silhouette, cloud structure, moon readability, compressed role-correct maps, baked/simple sky response, and no ugly mode. Reduce residency, secondary layers, reflection quality, and cloud depth smoothly.
- Middle: route-owned material stacks, stable import roles, sky/cloud/Aegir slot readback, and bright surface screenshots are mandatory before broader route use.
- High: spend saved budget on richer Aegir/cloud detail, stronger atmospheric softness, better surface reflection response, longer LOD/residency, and cleaner moon material identity after proof.
- Ultra: visual overkill through layered atmosphere, deeper cloud bands, higher-fidelity celestial texture response, capture-grade sky/ocean composition, and richer surface lighting. Gameplay truth, save identity, DTO layout, and route ownership do not change.

## Output Artifacts For Future Unity Owner

- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_14_UNITY_READBACK_sky_aegir_cloud_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_14_IMPORT_ROLE_sky_aegir_cloud_<timestamp>.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_OWNER_14_FRAME_DEBUGGER_sky_aegir_cloud_<timestamp>.md`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_14_sky_aegir_cloud_game_surface_<timestamp>.png`
- `Docs/Screenshots/HectonProofPackets/ASSET_OWNER_14_sky_aegir_cloud_scene_selected_<timestamp>.png`
- Contact sheets under `Docs/GeneratedAssets/AssetSystem_20260605/` or `Docs/Reports/AssetSystem_20260605/`, never under `Assets/`.

Final status: `PENDING_VERIFICATION`.
