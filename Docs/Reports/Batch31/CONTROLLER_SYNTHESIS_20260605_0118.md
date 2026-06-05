# Batch31 Controller Synthesis - 2026-06-05 01:18

Status: ACTIVE / STATIC SYNTHESIS / NO VISUAL ACCEPTANCE

## Current Front

HECTON-8 first surface/photic product face is still rejected.

No current screenshot set is production proof. Raw MCP PNGs are diagnostic only.

## Evidence Integrated

Read-only subagent evidence:

- Scene diff classifier: `02_HECTON_WORLD.unity` has 93k-line churn and cannot be accepted as cleanup.
- ProofGate scout: `Docs/Screenshots/HectonProofPackets` is absent; raw `h8_1474` group rejects as `RAW_PNG_SET`.
- Reference comparator: current screenshots fail mandatory visual references across water, shoreline, Aegir, surface brightness, underwater density, and terrain material truth.
- Aegir/sky scout: active route is coherent but visually failed; `Mat_HectonSky` cloud slots are missing/null and active Aegir impostor has no proven `GlobalQualityWeight` hook.
- Texture scout: no inspected wet basalt, shell sand, foam/contact, caustic, or particle texture is production-ready.
- UI/player scout: input, movement, camera, visor/HUD, and interaction systems exist, but scene wiring, active HUD render path, duplicate interaction UI, kinematic repair route, and runtime proof remain blockers.

Local static checks:

- `python -B -m unittest discover -s Tools\ProofGate -p test_*.py` returned `OK` for 21 tests in shell output. Static tool proof only; no persisted artifact path was attached in this synthesis.
- `python -B Tools\ProofGate\unity_process_proof_watchdog.py --repo-root C:\hades\Hecton8 --strict --no-write` returned `STATIC_BLOCKED`, `DIRTY_LOG_TOKENS_FOUND`, `RAW_PNG_SET_NO_MANIFEST`.
- Fast forbidden-pattern scan of selected UI/player files did not find direct `Update`, `Input.Get*`, `TMP_Text.text =`, `SetText(string)`, `SetActive`, `Camera.main`, or `Find*` hits in the scanned subset. This is not runtime proof.
- Local UI/player audit seed written to `Docs/Reports/Batch31/3109_LOCAL_UI_PLAYER_CODE_AUDIT_SEED.md`.
- `GeneratedAssetProductionAudit.py`: 434 packages, 83 errors, 1281 warnings.
- `PrimitiveNullDefaultStaticValidator2104.py`: 3008 findings, including 1947 critical and 875 high.
- `GeminiTextureIntakeAudit.py`: 9 images scanned, 7 rejected, 2 review, 0 pass.
- Manual visual review overrode the interpretation of Gemini intake: some sources are usable as source/reference, but none are accepted for direct production import.
- Local PBR source-bake pack created under `Docs/GeneratedAssets/Batch31_LocalPBR/` and reviewed as source only.
- Texture visual review report written to `Docs/Reports/Batch31/GEMINI_TEXTURE_VISUAL_REVIEW_AND_LOCAL_PBR_20260605.md`.
- 3107 prefab placement scout: 49 rock procedural final prefabs and 89 baked flora/coral starter prefabs have static LOD/component positives, but visible placement is blocked by material/render/proof gates and by product-face primitive/proxy debt.
- 3107 owner report/status/log/rationale written.
- Local 3109 wiring audit: `Player.prefab` binds `PlayerInteraction`, `HectonPlayerMovement`, PDA, swim presentation, `VisorHUDController`, and `SuitHUDPresentationController`; `Suit_HUD_Canvas.prefab` binds `SuitHUDV4CanvasOverlay` and `Hecton8.Interaction.InteractionUI`; `HUD_Internal.prefab` binds `SuitHUDScreenCompositor` with `forceScreenSpaceOverlay: 1`.
- 3109 static owner report written to `Docs/Reports/Batch31/3109_FULL_UI_PLAYER_MOVEMENT_OWNER.md`.
- 3110 lore/world scout: first route must be playable salvage chain from damaged bathy-drop through bright photic exit, swim, oxygen/depth pressure, starter resource, repair/craft improvement, and save/load restoration.
- 3110 static owner report written to `Docs/Reports/Batch31/3110_LORE_WORLD_CONSISTENCY_OWNER.md`.
- Proof harness scout: `H8VisualProofCapture1912.cs` is unsafe as a harness base because `QuarantineSurfaceRejectsAndExit()` disables renderers and saves `02_HECTON_WORLD.unity`; capture-only paths are still raw/ProofGate-invalid.
- Proof replacement spec written to `Docs/Reports/Batch31/PROOF_HARNESS_REPLACEMENT_SPEC_20260605.md`.
- 3102 proof owner report/status/log/rationale written.
- Scene-flow drift report written to `Docs/Reports/Batch31/SCENE_FLOW_AUTHORITY_DRIFT_20260605.md`.
- UI/player scene wiring scout: `02_HECTON_WORLD` statically contains a shell `Player` with `HectonWorldShellController1428`; it does not prove production `Player.prefab`/HUD stack is active.
- Copper chain scout: copper requires Drill, but first-hour seafloor drill item/metadata/held prefab/acquisition route are missing. Copper is not currently a proven first-route resource spine.
- Copper reachability report written to `Docs/Reports/Batch31/COPPER_STARTER_CHAIN_REACHABILITY_20260605.md`.

## Hard Rejects

- `H8VisualProofCapture1912.cs` is a delete/replace candidate. It contains scene mutation via save path and must not be used as proof harness.
- `H8VisualProofCapture1912.cs` must not be extended. Replacement harness needs new file scope under `Assets/_Project/Scripts/Editor/Proof/` and output only under `Docs/Screenshots/HectonProofPackets/`.
- 93k-line scene diff is not accepted as cleanup.
- Raw MCP PNG groups are not proof packets.
- Current water is flat/green/opaque or false-labeled.
- Current shoreline foreground reads black primitive/low-authoring.
- Current Aegir reads muddy/sticker/translucent, not premium atmospheric body.
- Underwater route lacks density, particles/fauna/scale cues, and player decision stake.
- Existing shoreline texture candidates are not production-ready; direct import would launder rejected art into production.
- Existing Gemini shoreline/seabed candidates are usable source material after manual visual review, including temporary material prototyping. They remain non-final until watermark cleanup where relevant, PBR separation, import settings, material binding, seam check, and route screenshot proof.
- Product-face/static asset debt is not cosmetic: built-in primitive meshes, placeholder/proxy material refs, empty base texture slots, null material slots, and unresolved GUIDs exist in product-facing route bands.
- Prefab scatter is forbidden as a cosmetic patch. Rocks/flora/coral must pass substrate, material, LOD, collider, ecology, and proof gates before placement.
- `HUD_Internal.prefab` forcing screen-space overlay is a UI acceptance blocker until runtime readback proves it is only a preview/noninteractive bridge path or it is corrected through the proper owner.
- Boot route contradiction exists: root scene flow excludes `01_ORBIT`, while first-20/topology docs and BuildSettings include it. Do not let a GUI owner wire stale route flow blindly.
- Copper starter chain is statically blocked by Drill requirement and missing starter drill route. If copper remains V0 spine, author the drill route; otherwise switch to FiberKelp/PressureSeal or another reachable chain.

## Required Next Actions

### Unity Scene Lane

- One Unity owner only.
- Do not raw-edit scene YAML.
- Do not blindly restore all disabled objects; some are visually rejected.
- In Unity, review object groups:
  - `H8_PHOTIC_ROCK_GARDEN_1469`
  - `H8_PHOTIC_SOFT_WATER_HAZE_1430`
  - `H8_FloorCausticSoft_1443`
  - `H8_HeroWetBasaltBoulder_1453_*`
  - broken foam groups
  - `Water_Mass_Far_1428`
  - `Water_Mass_Mid_1428`
  - noir curtains/veils/vignette slabs
  - main camera and surface sun changes
- Restore/replace/delete only after object-level proof.

### Proof Lane

Next valid proof candidate must be:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

Required packet files:

- `manifest.json`
- `manifest.sha256`
- copied Unity editor log
- six production screenshots under `screenshots/`
- no output under `Assets`
- clean post-capture log window of at least 60 seconds

Harness rules:

- never call `SaveScene`;
- never call `MarkSceneDirty`;
- never disable renderers as proof cleanup;
- never output proof artifacts under `Assets`;
- never use raw PNG folders as acceptance candidates;
- emit manifest, checksum, copied log, canonical screenshots, and route/depth/UI predicates.

### Visual Lane

Water/shore/Aegir/underwater must be rebuilt or replaced against mandatory examples.

Required work:

- transparent photic water with readable seabed and believable surface/ceiling contact;
- wet basalt/sediment/foam waterline;
- non-muddy Aegir with atmospheric limb and cloud-band detail;
- underwater route density: cliffs/shelves/coral/particles/fauna/scale/evidence cue;
- surface brightness with material truth, not black slabs plus green artifact.

### Aegir / Sky Lane

- Keep one Aegir owner.
- Do not enable `SURFACE_LOW_SUN_DISC_1428`.
- Do not enable `H8_AEGIR_SKY_BACKDROP_1428` beside the active Aegir sphere.
- Do not promote `MAT_SurfaceNoirProceduralSkybox_1428` for normal surface.
- Verify and fix `Mat_HectonSky.mat` cloud slots:
  - `_HighCloudTex`
  - `_MainCloudAtlas`
  - `_MainCloudTex`
- Proof needs material/runtime readback: active skybox GUID, sky shader GUID, Aegir material/shader GUID, resolved texture slots, `PrimarySunDiscOwner=SkyMaterial`, mesh sun disabled.

### Texture / Asset Lane

- Reject direct shoreline use of existing `foam.png`; it reads as repeated turquoise sheet, not semantic waterline contact.
- Treat wet basalt 1428/1429 and Batch21 shell/sand as planning references only.
- Hold Crest foam/caustic textures only inside Crest route, gated by material proof.
- Use existing 2K rock stacks and mineral seep masks as study sources, not accepted production imports.
- Generate or author a full PBR package before object placement:
  - albedo;
  - normal;
  - MRAO;
  - wetness/contact/salt masks;
  - tiling/seam QA.

### UI / Player Movement Lane

Batch31 now includes `3109_FULL_UI_PLAYER_MOVEMENT_OWNER`.

Static early facts:

- `HectonPlayerMovement` exists and includes dry walk, shallow wade, surface swim, underwater swim, and exosuit locomotion labels.
- `InputDispatcher` exists as frame-cached input owner.
- `PlayerInputState` is fixed-layout 64-byte snapshot.
- `SurvivalHUDController` uses `ILateFrameTickable`; it is likely a survival bar HUD, but runtime wiring/proof is absent.
- `InteractionUI` uses `ILateFrameTickable` and a char buffer, but needs deeper audit for prompt string churn and localization behavior.
- `HectonPlayerMovement.cs` is 13,929 lines and is a god-object risk; do not expand blindly.
- `InputDispatcher.cs` is 4,689 lines and appears to be the correct input owner boundary, pending runtime GC proof.
- `SuitHUDV4CanvasOverlay.cs` is 7,993 lines; UI path needs method-level audit before patching.
- `Hecton8.Interaction.InteractionUI` is the cleaner event-driven interaction UI candidate.
- `Hecton8.UI.InteractionUI` is a duplicate/legacy prompt path and must be classified in scene before any retirement.
- `HectonPlayerCameraRig`, `PlayerInteraction`, and `InteractableRegistry` exist, but scene/prefab wiring remains unproven.
- `ScheduleKinematicRepairTargetProbe` disabled route is a movement acceptance blocker until classified.
- `Player.prefab` static bindings are present: `PlayerInteraction`, `HectonPlayerMovement`, `PlayerPDA`, swim presentation, `VisorHUDController`, and `SuitHUDPresentationController`.
- `Suit_HUD_Canvas.prefab` static bindings are present: `SuitHUDV4CanvasOverlay` and the cleaner event-driven `Hecton8.Interaction.InteractionUI`.
- `Hecton8.UI.InteractionUI` remains suspect because it exposes string `UnityEvent` prompt changes and cached string construction; absence from active scene is not yet proven.
- `HUD_Internal.prefab` sets `forceScreenSpaceOverlay: 1`; full diegetic UI acceptance is blocked until this path is proven diagnostic/bridge-only or corrected.
- `02_HECTON_WORLD` static YAML does not prove `Player.prefab`, `Suit_HUD_Canvas.prefab`, or `HUD_Internal.prefab` are active; runtime bootstrap spawning is unproven.
- `ScheduleKinematicRepairTargetProbe` is statically dead: movement calls it, motor returns false.
- New blocker report: `Docs/Reports/Batch31/PLAYER_HUD_BOOTSTRAP_BINDING_BLOCKER_20260605.md`.
- Static source now suggests the scene-authored tagged `Player` shell with `HectonWorldShellController1428` currently wins over the production `Player.prefab` route.
- Static GUID search found no `Assets`/`ProjectSettings` reference to `Player.prefab`, `Suit_HUD_Canvas.prefab`, or `HUD_Internal.prefab` GUIDs.
- `GameBootstrapper` can resolve a tagged scene `Player`; if it publishes the shell to `BootstrapState.CurrentPlayerObject`, `PlayerRuntimeContextService` binds the wrong object.
- Full UI/movement acceptance is blocked until Play Mode readback proves the production player/HUD graph is active or a Unity owner safely replaces/disables the shell route.

### Static Asset Audit Lane

Current machine audit outputs:

- `Docs/Reports/Batch31/GeneratedAssetProductionAudit_batch31.md`
- `Docs/Reports/Batch31/PrimitiveNullDefaultStaticValidator_batch31.md`
- `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/GeminiTextureIntakeAudit.md`

Priority blockers:

- built-in primitive meshes in product-face/final prefabs;
- placeholder/proxy material refs in product-face bands;
- empty base texture slots in sky/water/foam/photic materials;
- unresolved texture GUIDs, including ocean/terrain-related materials;
- generated texture candidates rejected or review-only.

End-of-wave prefab placement is forbidden until the target object families pass these gates.

Candidate but not accepted:

- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`: 49 static-positive geology prefabs.
- `Assets/_Project/Prefabs/Nature/Flora/Baked`: 89 static-positive baked starter flora/coral prefabs.
- `Assets/_Project/Prefabs/Construction/Final`: caution only; current audits still flag primitive-mesh debt.

Rejected visible placement:

- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`
- `Assets/_Project/Prefabs/WorldProceduralProxy`

### Material / Texture Criticals Lane

Integrated static material scout:

- Report: `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`.
- Active photic coral plate and branch thicket fields still route to `WorldProceduralProxy` materials. This is product-face material contamination until a Unity owner rebinds to final route-owned materials and captures proof.
- `MAT_H8_SurfaceCrestOcean_1428.mat` has unresolved Crest/wave-data/sky GUID refs. Owner must classify stale runtime wave-data slots versus true missing artist textures. Crest rule still applies: no custom runtime material clone/wrapper.
- New resolver report: `Docs/Reports/Batch31/CREST_TERRAIN_GUID_RESOLUTION_20260605.md`.
- Crest `_WD_*` missing GUIDs are likely runtime/stale wave-data slots because they repeat in canonical Crest materials. Do not replace them with artist textures by text edit.
- `Mat_HectonSky.mat` and cloud overlay have missing/null cloud/star/horizon slots. Sky owner must map existing sky textures before binding; no dark/fog fallback.
- Surface foam/veil and several photic VFX materials are color-only or mask-missing. Detached strips and global fog are rejected.
- Terrain/triplanar rock material route has missing shader/texture refs and must be repaired before shoreline geometry can pass visual proof.
- `terrain.mat` and `Mat_TriplanarRock.mat` are stale; valid static candidates include `Mat_Terrain.mat`, `TerrainMaster.shader`, `H8_PhoticTerrainLit_1453.shader`, `MAT_H8_HeroWetBasaltRock_1453.mat`, and `MAT_H8_AuthoredWetBasaltBreakup_1465.mat`.
- Nuance: some `Hecton_CoralMaster_GPUI` materials use `[MainTexture] _BaseMap`; `_MainTex` null alone is not proof of failure.
- Texture policy correction: Gemini/Batch31 sources are not final production imports, but may be used as temporary prototype sources when the full source is preserved. Small Gemini mark is final-cleanup debt, not automatic prototype rejection.

### Sky Slot Resolution Lane

Integrated sky texture resolver:

- Report: `Docs/Reports/Batch31/SKY_TEXTURE_SLOT_RESOLUTION_20260605.md`.
- `Mat_HectonSky.mat` is statically active in `02_HECTON_WORLD` and `00_BOOTSTRAP`.
- `_HighCloudTex` and `_MainCloudAtlas` missing GUIDs are stale/deleted references, not moved files.
- `_MainCloudTex` candidate for `Mat_HectonSky.mat`: `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`.
- Keep existing valid bindings unless visual proof fails: `Aegir_storms.png`, `clod1.png`, `clod2.png`, `oblakajip.png`, and `MAT_H8SurfaceCloudDeck_1428` `_BaseMap = oblaka!.png`.
- Do not bind `_HighCloudTex`/`_MainCloudAtlas` blindly; shader/material readback must prove whether those serialized rows are effective.

### Lore / World Route Lane

Mandatory first-20 route:

- damaged bathy-drop / safe anchor;
- bright photic exit;
- swim and return route;
- oxygen/depth/pressure pressure;
- reachable starter resource;
- craft/repair/build improvement;
- save/load same-state restoration.

Required route objects:

- bathy-drop/P-63;
- heat-shield trail;
- pinger/service buoy/relay;
- first tool with physical verb;
- first fair hazard;
- Deep Reach lie panel with visible contradiction;
- Atlas trace;
- save/load state objects.

Current blockers:

- scene proof absent;
- boot-flow conflict around `01_ORBIT`;
- copper currently requires Drill while starter drill route is missing;
- quest activation/completion runtime proof absent;
- localization final proof absent.

Reject any claim that movement/UI works until Play Mode capture and GC/profiler evidence exist.

## Low / Middle / High / Ultra Consequences

- Low: preserve readable water/sky/shore silhouettes, movement clarity, oxygen/depth/pressure UI, route cue, no flat/black/green placeholder visuals.
- Middle: add richer material response, wet foam masks, underwater particles, stable route instruments.
- High: extend LOD residency, denser coral/rocks/fauna, stronger Aegir cloud bands, richer visor/camera feedback.
- Ultra: visual overkill only after low-tier readability and input/UI proof hold; no new gameplay truth through quality tier.

## Current Disposition

`PENDING VERIFICATION`.

Static tool proof exists for ProofGate tests only. Visual, runtime, Play Mode, profiler, and player-control proof remain absent.
