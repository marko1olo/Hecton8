# 1885 Product-Face Prefab Anchor Reference Static Snapshot

Date: 2026-06-04
Agent: 1885
Mode: REPORT_ONLY_STATIC_PREFAB_SNAPSHOT
Evidence class: STATIC_YAML / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Owned outputs only:

- `Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_STATIC_SNAPSHOT.md`
- `Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_MATRIX.csv`
- `Docs/Tasks/Status_1885.md`
- `Docs/AgentLogs/Rationale_1885.md`
- `Docs/AgentLogs/LOG_1885.md`

No source code, Unity assets, prefabs, scenes, binaries, generated meshes, task files, `.meta`, imports, Unity menus, screenshots, PlayMode, profiler, builds, or DataMonolith work were touched.

## Authorities Read

- Root/project: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`
- Domain bibles: `quality.md`, `player.md`, `tools.md`, `inventory.md`, `vehicles.md`, `water.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`
- Prior Batch18 packets: `1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`, `1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`, `1879_PRODUCT_FACE_RELINK_SEQUENCE.csv`, `1872_PLAYER_BODY_VISUAL_SOURCE_PACKAGE.md`, `1869_TOOL_VISUAL_SOURCE_PACKAGE.md`, `1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`, `1871_TRANSPORT_VISUAL_SOURCE_PACKAGE.md`
- Mandates: `QA_Evidence_Text_Filter_Audit.txt`, `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`, `DATA_Inventory_Resources_Items_SOA_Layout.txt`, `PHYS_Physics_Integrity_Determinism_ForceMode.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: static product-face prefab anchor/reference preservation.

## Static Boundary

This packet preserves current static YAML facts for future relink. It does not prove runtime wiring, scene overrides, visual quality, material import state, frame time, GC, screenshot quality, or player behavior.

All current product-face primitive visuals remain `RED_STATIC`. This is preservation evidence, not acceptance.

## Player Prefab Preservation

Target: `Assets/_Project/Prefabs/Player.prefab`

Preserve and compare local transforms before/after relink:

- `HandAnchor`: line 2756; local position `{x: 0.3, y: -0.3, z: 0.5}`, rotation identity, scale identity.
- `Suit_Visor`: line 2791; local position `{x: 0, y: 1.8, z: 0.4500122}`, rotation `{x: -0.008726535, y: -0, z: -0, w: 0.9999619}`, scale `{x: 2, y: 0.55, z: 0.36}`.
- `Suit_Diegetic_HUD_V4_Projection`: line 15; local position `{x: 0, y: 0, z: 0.72}`, rotation identity, scale `{x: 1.42, y: 0.8, z: 1}`.
- `Main Camera`: line 290; local position `{x: 0, y: 1.8, z: 0}`, rotation identity, scale identity.
- `FirstPerson_Overlay_Camera`: line 2465; local position `{x: 0, y: 0, z: 0}`, rotation identity, scale identity.
- `SpaceCamera`: line 3418; local position `{x: 0, y: 1.8, z: 0}`, rotation identity, scale identity.
- `HUD_Render_Camera`: line 3548; local position `{x: 0, y: 1.8, z: 0}`, rotation identity, scale identity.
- `Swim_ViewmodelRoot`: line 3995; local position `{x: 0, y: -0.18, z: 0.38}`, rotation `{x: 0.05233596, y: 0, z: 0, w: 0.9986295}`, scale identity.
- `Swim_*Attachment` transforms: `Swim_LeftShoulderAttachment`, `Swim_RightShoulderAttachment`, `Swim_LeftUpperArmAttachment`, `Swim_RightUpperArmAttachment`, `Swim_LeftForearmAttachment`, `Swim_RightForearmAttachment`, `Swim_LeftHandAttachment`, `Swim_RightHandAttachment`, `Swim_TorsoAttachment`, `Swim_PelvisAttachment`, `Swim_LeftThighAttachment`, `Swim_RightThighAttachment`, `Swim_LeftCalfAttachment`, `Swim_RightCalfAttachment`, `Swim_LeftFinAttachment`, `Swim_RightFinAttachment`; static YAML shows identity local transform for each attachment.

Visible primitive body/visor roots from prior and current evidence:

- `Swim_LeftShoulder`, `Swim_RightShoulder`, `Swim_LeftUpperArm`, `Swim_RightUpperArm`, `Swim_LeftForearm`, `Swim_RightForearm`, `Swim_LeftGlove`, `Swim_RightGlove`, `Swim_Torso`, `Swim_Pelvis`, `Swim_LeftThigh`, `Swim_RightThigh`, `Swim_LeftCalf`, `Swim_RightCalf`, `Swim_LeftFin`, `Swim_RightFin`: cube `m_Mesh fileID 10202`.
- `Suit_Visor`: primitive `m_Mesh fileID 10207`.

Visible component types in YAML include:

- `HectonSurvivalSystem`
- `Hecton8.Interaction.PlayerInteraction`
- `Hecton8.Gameplay.HectonPlayerMovement`
- `Hecton8.Physics.BuoyancyObject`
- `Hecton8.Gameplay.PlayerToolManager`
- `Hecton8.Inventory.PlayerInventory`
- `Hecton8.Gameplay.PlayerSwimPresentationController`
- `Hecton8.Gameplay.PlayerSwimBlockoutRig`
- `NASAPunk.Visor.VisorHUDController`
- `NASAPunk.Visor.SuitHUDPresentationController`
- Unity `Camera`, `CapsuleCollider`, `SphereCollider`, and URP `UniversalAdditionalCameraData`

Risk if broken: hand/tool attachment, first-person camera stack, diegetic HUD projection, visor references, swim presentation, collision proxy, and survival/UI coupling can silently drift before visual replacement is even visible.

## Tool Held And World Preservation

Targets:

- Held: `Assets/_Project/Prefabs/Tools/Held/*.prefab`
- World: `Assets/_Project/Prefabs/Items/Tools/*.prefab`

Static YAML found current visible primitive body state:

- All 12 held tool families contain built-in cube `m_Mesh fileID 10202` on root or `VisualBody*`.
- All 12 world tool pickups contain built-in cube `m_Mesh fileID 10202`.
- Current status for each tool body is `RED_STATIC_PRIMITIVE`.

Held owner components/data references visible:

- `Tool_BeaconDeployer_Held`: `Hecton8.Gameplay.BeaconDeployerTool`, `_toolData`.
- `Tool_Builder_Held`: `Hecton8.Gameplay.BuilderTool`, `_toolData`.
- `Tool_EnvAnalyzer_Held`: `Hecton8.Gameplay.EnvironmentalAnalyzerTool`, `_toolData`.
- `Tool_Flashlight_Held`: `Hecton8.Gameplay.FlashlightTool`, `_toolData`.
- `Tool_HarpoonLauncher_Held`: `Hecton8.Gameplay.HarpoonLauncherTool`, `_toolData`.
- `Tool_Knife_Held`: `Hecton8.Gameplay.KnifeTool`, `_toolData`.
- `Tool_LaserCutter_Held`: `Hecton8.Gameplay.LaserCutter`, `_toolData`.
- `Tool_Propulsion_Held`: `Hecton8.Gameplay.MantaScooter`, `_toolData`, plus `PlayerTransportFeelContract`.
- `Tool_Repair_Held`: `Hecton8.Gameplay.RepairTool`, `_toolData`.
- `Tool_SalvageSampler_Held`: `Hecton8.Gameplay.SalvageSamplerTool`, `_toolData`.
- `Tool_Scanner_Held`: `Hecton8.Gameplay.ScannerTool`, `_toolData`.
- `Tool_StunPistol_Held`: `Hecton8.Gameplay.StunPistolTool`, `_toolData`.
- All held prefabs also expose `Hecton8.Gameplay.PlayerToolSwimContract`.

World owner components/data references visible:

- All world tool pickup prefabs expose `Hecton8.Interaction.PickupItem` and `Hecton8.Interaction.InteractionHighlighter`.
- `Item_Tool_Repair_World.prefab` additionally exposes `Hecton8.Gameplay.ScannableFragment`, `canBeScanned: 1`, and scan UnityEvents.

Explicit named anchors:

- Current scoped YAML did not show explicit `ANCHOR_Grip_R`, `ANCHOR_Grip_LR`, `ANCHOR_Pickup`, `ANCHOR_ScanOrigin`, `ANCHOR_RayOrigin`, `ANCHOR_BeamOrigin`, `ANCHOR_Muzzle`, `ANCHOR_Nozzle`, `ANCHOR_TetherAnchor`, or `ANCHOR_AUP_LocalOrigin` children.
- Future relink must add or preserve those anchors per 1869/1879 only under a Unity owner slot. This packet marks their current absence as `MISSING_STATIC_EVIDENCE`, not as approval to derive gameplay origins from visual mesh bounds.

Held/world family pair requirement:

- Relink held and world variants by tool family in the same Unity owner slot.
- Preserve `_toolData`, item data, `PlayerToolSwimContract`, `PickupItem`, `InteractionHighlighter`, and any scan/transport-support components.
- Do not split held and world relinks across concurrent agents.

Risk if broken: tool verbs may still compile but lose ray/scan/beam/muzzle/tether/pickup origin consistency, item identity, or first-person grip alignment.

## Resource Pickup Preservation

Targets:

- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_HydrocarbonResin.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_MembraneTissue.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SulfurClumps.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`

Static common component/reference pattern:

- `Hecton8.Interaction.InteractionHighlighter`
- `Hecton8.Interaction.PickupItem`
- `itemData` reference
- primitive collider on root: Box, Capsule, or Sphere, with `m_IsTrigger: 0`
- visible built-in primitive mesh

Canonical data path expectations confirmed:

- `PFB_Resource_CopperOre.prefab` uses GUID resolving to `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`; data asset `m_Name: Data_Copper`, `stableId: Data_Copper`, `legacyItemName: Copper Ore`.
- `PFB_Resource_TitaniumScrap.prefab` and `Item_Titanium.prefab` use GUID resolving to `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset`; data asset `m_Name: Data_TitaniumScrap`, `stableId: Data_TitaniumScrap`, `legacyItemName: Titanium Scrap`.

Resource-specific primitive/collider evidence:

- CopperOre: cube `10202`, `BoxCollider`, data `Data_Copper.asset`.
- FiberKelp: plane `10208`, `CapsuleCollider`, data `Data_FiberKelp.asset`.
- HydrocarbonResin: plane `10208`, `CapsuleCollider`, data `Data_HydrocarbonResin.asset`.
- MembraneTissue: primitive `10207`, `SphereCollider`, data `Data_MembraneTissue.asset`.
- SilicaShards: primitive `10207`, `SphereCollider`, data `Data_SilicaShards.asset`.
- SilverOre: cube `10202`, `BoxCollider`, data `Data_SilverOre.asset`.
- SulfurClumps: primitive `10207`, `SphereCollider`, data `Data_SulfurClumps.asset`.
- TitaniumScrap: cube `10202`, `BoxCollider`, data `Data_TitaniumScrap.asset`.
- Item_Titanium: cube `10202`, `BoxCollider`, data `Data_TitaniumScrap.asset`, `ScannableTarget`.

Material/data risk:

- `Item_Titanium.prefab` still carries unresolved material GUID `31321ba15b8f8eb4c954353edc038b1d` per 1870.
- `Data_Copper` naming is canonical. Do not invent `Data_CopperOre.asset`.
- `Item_Titanium` is a duplicate/legacy-looking root that needs production-reference proof before quarantine or canonical relink.

## Transport Preservation

Targets:

- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`

Current status:

- All four use cube `m_Mesh fileID 10202` on root transport body.
- All four expose root `BoxCollider`.
- All four expose `Hecton8.Gameplay.PlayerTransportFeelContract`, `Hecton8.Gameplay.MountablePlayerTransport`, and `preset` references.
- Current visual primitive state is `RED_STATIC`.

Anchor local transform contracts:

- CargoSled: `RiderAnchor` position `{x: 0, y: 0.15, z: 0}`; `DismountAnchor` position `{x: 1.6, y: 0.1, z: 0}`; both rotation identity and scale identity.
- ExosuitFrame: `RiderAnchor` position `{x: 0, y: 0.75, z: 0}`; `DismountAnchor` position `{x: 1.3, y: 0.2, z: 0}`; both rotation identity and scale identity.
- MicroSub: `RiderAnchor` position `{x: 0, y: 0.4, z: 0}`; `DismountAnchor` position `{x: 2.4, y: 0.1, z: 0}`; both rotation identity and scale identity.
- ScoutGlider: `RiderAnchor` position `{x: 0, y: 0.15, z: 0}`; `DismountAnchor` position `{x: 1.6, y: 0.1, z: 0}`; both rotation identity and scale identity.

Preserve before visual relink:

- `RiderAnchor`
- `DismountAnchor`
- `PlayerTransportFeelContract`
- `MountablePlayerTransport`
- transport preset references
- occupancy/drive/camera feel fields visible in YAML
- root collider/proxy behavior until a vehicle owner explicitly changes collision truth

Risk if broken: mount/dismount can become physically unsafe, camera feel can detach from transport truth, and vehicle anchors can drift while the visible mesh appears acceptable.

## Sky And Ocean Preservation

Sky target: `Assets/_Project/Prefabs/Sky_System.prefab`

- Root `Sky_System` exposes `SkySystemFollowCamera`.
- `runtimeCamera: {fileID: 0}` is visible in YAML; runtime camera route is unresolved statically and must be assigned/proven by future Unity owner.
- Child `Sphere` uses built-in primitive `m_Mesh fileID 10207`.
- Current sky source primitive state is `RED_STATIC`; no visual acceptance is claimed.

Ocean target: `Assets/_Project/Prefabs/Ocean_Crest.prefab`

Visible Crest/runtime components:

- `Crest.RegisterAlbedoInput` on `SargassumOilFilmInput`, primitive plane `10209`.
- `Crest.RegisterAnimWavesInput` on `SargassumWaveDampingInput`, primitive plane `10209`.
- `Crest.RegisterFoamInput` on `SargassumFoamDampingInput`, primitive plane `10209`.
- `Crest.OceanDepthCache` on `OceanDepthCache`.
- `Crest.OceanRenderer` on `Ocean_Crest`.
- `Crest.ShapeFFT`.
- `Hecton8.World.SargassumCrestDampingController`.
- `Hecton8.World.SargassumMicroFaunaBoids`, `viewCamera: {fileID: 0}`.
- `Hecton8.Physics.Crest4KinematicsAdapter`.
- `Hecton8.World.HectonCrestOceanDepthCacheBootstrap`.
- `Hecton8.World.HectonCaveVoxelAmbientOcclusionController`, `viewerCamera: {fileID: 0}`.

Hidden-input candidates, pending proof:

- `SargassumOilFilmInput`
- `SargassumWaveDampingInput`
- `SargassumFoamDampingInput`

These are exact hidden-input candidates only. No broad validator bypass is authorized. If Frame Debugger/runtime proof later shows they render visibly as art, they remain product-face primitive debt.

## Legacy Loose Roots

`Assets/_Project/Prefabs/Item_Titanium.prefab`

- Uses cube `10202`, `BoxCollider`, `PickupItem`, `InteractionHighlighter`, `ScannableTarget`, and `Data_TitaniumScrap.asset`.
- Production-reference uncertainty: could be duplicate/legacy root or still referenced by production content. Must prove references before quarantine. If retained, canonicalize to titanium scrap source package.

`Assets/_Project/Prefabs/STRUCTURES.prefab`

- Contains child `Item_Titanium` with cube `10202`.
- Production-reference uncertainty: aggregate may leak primitive titanium into production. Must prove aggregate references and child route before quarantine or relink.

`Assets/_Project/Prefabs/Buildings/Cube.prefab`

- Uses cube `10202`.
- Production-reference uncertainty: raw placeholder unless construction/building owner proves it is still referenced. Must prove references before quarantine; if retained, relink to pressure-rated module source.

## Future Unity-Owner Serialized Checklist

1. Create pre-relink YAML snapshot of target prefab and owned backup/VCS state.
2. Resolve and record all MeshFilter/sharedMesh, Renderer/material, Collider, script, data asset, camera, and anchor references before mutation.
3. Compare local position, rotation, and scale for all named anchors before and after relink: `HandAnchor`, `Suit_Visor`, camera stack, `Swim_*Attachment`, tool anchors where present, `RiderAnchor`, `DismountAnchor`, sky/ocean runtime camera routes, and pickup trigger roots.
4. Replace only visual `MeshFilter.sharedMesh` / Renderer material references under `VIS_*` or equivalent visual roots; do not move gameplay anchors to chase mesh shape.
5. Preserve component references: tool `_toolData`, item `itemData`, `PickupItem`, `InteractionHighlighter`, `ScannableTarget`/`ScannableFragment`, transport presets/contracts, Crest components, camera routes, HUD/visor controllers.
6. Run static validators: product-face prefab gate, generated asset audit `--fail-on-error`, sky/ocean primitive gate where applicable, and CSV/report diff checks.
7. Capture visual proof only after static gates pass: first-person player/tools, world pickups, resource pickups, transport boarding/near view, surface sky/ocean/waterline/photic/medium-depth. Compact and High captures are required for visual claims.
8. If any reference, anchor, validator, or capture fails, rollback the whole category before continuing. Do not accept mixed-slot proof.

## Highest Static Breakage Risks

1. Player: `HandAnchor`, camera stack, HUD projection, `Suit_Visor`, and `Swim_*Attachment` transforms are fragile because visual relink can appear correct while tool/HUD/swim presentation silently drifts.
2. Tools: explicit `ANCHOR_*` transforms are missing in current YAML, so future relink must not infer ray/beam/muzzle/tether/pickup truth from mesh bounds.
3. Resources: CopperOre must keep `Data_Copper.asset`; TitaniumScrap and loose `Item_Titanium` must keep `Data_TitaniumScrap.asset` or be quarantined only after reference proof.
4. Transport: `RiderAnchor` and `DismountAnchor` local transforms are mount/dismount safety truth and need exact comparison.
5. Sky/Ocean: Crest input planes may be hidden-input candidates, but only exact Frame Debugger/runtime proof can exempt them from visible art debt.

## Continuous Quality Consequences

This task changes no runtime or visuals. For future relink:

- Compact: preserve readable authored silhouettes, material identity, pickup/tool/transport anchors, ocean/sky readability, and no ugly primitive fallback.
- Middle: add grime, labels, wetness masks, and longer LOD residency without changing truth.
- High: add richer material response, bevels, glass/surface detail, and stronger route readability.
- Ultra: add micro fittings, droplets, secondary details, richer foam/atmosphere/micro-fauna presentation only. Gameplay truth, DTOs, item IDs, collision identity, anchors, and save identity do not change.

## Verification

Required commands were run after writing:

- `git diff --check -- Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_STATIC_SNAPSHOT.md Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_MATRIX.csv Docs/Tasks/Status_1885.md Docs/AgentLogs/Rationale_1885.md Docs/AgentLogs/LOG_1885.md`
- `Import-Csv Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_MATRIX.csv | Measure-Object`
- static term cross-check for `HandAnchor`, `Suit_Visor`, `Swim_`, `RiderAnchor`, `DismountAnchor`, `Data_Copper`, `Data_TitaniumScrap`, `Ocean_Crest`, and `Sky_System` in the report and CSV.

Results are recorded in `Docs/AgentLogs/LOG_1885.md`.
