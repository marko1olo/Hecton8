# 1871 Transport Visual Source Package

Date: 2026-06-04
Agent: 1871
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Static source package for replacing primitive visual bodies on:

- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`

No source, prefab, asset, scene, `.meta`, binary, Unity menu, import, bake, PlayMode, profiler, dotnet build, or Data Monolith action was run.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `vehicles.md`
- `water.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_PRIMITIVE_REPLACEMENT_QUEUE.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## Static Findings

All four transport prefabs use the Unity built-in cube mesh on the root transport body:

- `m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}`
- Evidence lines: cargo sled `PFB_CargoSled_Transport.prefab:49`, exosuit frame `PFB_Exosuit_Frame_Transport.prefab:49`, micro-sub `PFB_MicroSub_Transport.prefab:49`, scout glider `PFB_ScoutGlider_Transport.prefab:49`.

All four use one unresolved material GUID:

- `m_Materials: {fileID: 2100000, guid: 31321ba15b8f8eb4c954353edc038b1d, type: 2}`
- Evidence lines: all four prefabs line `96`.
- `rg -n "guid: 31321ba15b8f8eb4c954353edc038b1d" -g "*.meta" .` returned no `.meta` path.
- The same GUID appears as `m_DefaultMaterial` in `Assets/_Project/Data/UniversalRenderPipelineGlobalSettings.asset:197`, so the current transport visuals are effectively default-material blockout references unless Unity resolves this internally. No production transport material is proven.

All four preserve the same script/component owner pattern:

- `PlayerTransportFeelContract` script GUID `08e7fb5790e444f4bb5866ee91366d62` resolves to `Assets/_Project/Scripts/Gameplay/PlayerTransportFeelContract.cs`.
- `MountablePlayerTransport` script GUID `9933b8a52a9feae4d8f82d5b802888d2` resolves to `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs`.
- `PlayerTransportPreset` script GUID `9d512a24877f33949b5c90ec07b45730` resolves to `Assets/_Project/Scripts/Gameplay/PlayerTransportPreset.cs`.

Resolved preset owners:

- Cargo sled: `Assets/_Project/Data/Transport/TransportPreset_CargoSled.asset`
- Exosuit frame: `Assets/_Project/Data/Transport/TransportPreset_ExosuitFrame.asset`
- Micro-sub: `Assets/_Project/Data/Transport/TransportPreset_MicroSub.asset`
- Scout glider: `Assets/_Project/Data/Transport/TransportPreset_ScoutGlider.asset`
- Adjacent but unused by target prefabs: `Assets/_Project/Data/Transport/TransportPreset_Manta.asset`

Anchor preservation evidence:

- Cargo sled: `RiderAnchor` local position `{x: 0, y: 0.15, z: 0}`, `DismountAnchor` local position `{x: 1.6, y: 0.1, z: 0}`.
- Exosuit frame: `RiderAnchor` local position `{x: 0, y: 0.75, z: 0}`, `DismountAnchor` local position `{x: 1.3, y: 0.2, z: 0}`.
- Micro-sub: `RiderAnchor` local position `{x: 0, y: 0.4, z: 0}`, `DismountAnchor` local position `{x: 2.4, y: 0.1, z: 0}`.
- Scout glider: `RiderAnchor` local position `{x: 0, y: 0.15, z: 0}`, `DismountAnchor` local position `{x: 1.6, y: 0.1, z: 0}`.
- All listed anchors have identity local rotation and unit local scale in prefab YAML.

Current collider state:

- Each target prefab has a root `BoxCollider` on the same root GameObject as the primitive visual body.
- Replacement must not move or reinterpret `riderAnchor`, `dismountAnchor`, preset data, kinematic truth, AUP, collision truth, trigger ownership, seats, or drive/dismount behavior.

## Candidate Search Result

No accepted first-party non-primitive transport body mesh or prefab was found in `Assets/_Project` by targeted vehicle terms.

Rejected as direct body replacements:

- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab`: component-only static evidence; no `m_Mesh`/renderer proof in targeted scan. It is a runtime/vehicle-system owner prefab, not a visual hull source.
- `Assets/_Project/Prefabs/WorldProceduralProxy/*`: forbidden by task and proxy-named; not a transport source.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/*`: placeholder path; not accepted production vehicle art.
- `Assets/_Project/Data/AI/CreatureArchetypes/Ambient/Archetype_WallGlider.asset` and related wall-glider fauna assets: creature data, not transport body art.
- `Assets/Shapes/*` generated/subtractive materials: primitive/diagnostic tooling, not production vehicle art.

Reusable material/detail candidates requiring later Unity/import/visual proof:

- First-party hull/glass/wet steel material family:
  - `Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_PressureHull.mat`
  - `Assets/_Project/Art/RuntimeShell1428/H8_Shell_Submarine_WetSteel.mat`
  - `Assets/_Project/Art/RuntimeShell1428/H8_Shell_1428_BlackGlass.mat`
  - `Assets/_Project/Art/RuntimeShell1428/H8_Shell_1428_HazeGlass.mat`
  - `Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_WetDeck.mat`
  - `Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_AmberSignal.mat`
  - `Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_CyanSignal.mat`
- Runtime visual proof material family:
  - `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetPressureMetal.mat`
  - `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_DirtyPressureGlass.mat`
  - `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetEdgeSteel.mat`
- First-party existing water/vehicle support data:
  - `Data/Physics/seaglide_vehicle_profiles.csv`
  - `Data/Physics/exosuit_performance_profiles.csv`
  - `Data/Physics/vehicle_hull_profiles.csv`
  - `Data/Physics/vehicle_sampling_profiles.csv`
  - `Data/Physics/Submarine_Specs.json`
- Third-party/detail-only candidates, not accepted body replacements without license/import/art proof:
  - `Assets/ScifiFacility/Materials/Hull.mat`
  - `Assets/ScifiFacility/Materials/Base_Rubber.mat`
  - `Assets/ScifiFacility/Materials/Glass.mat`
  - `Assets/ScifiFacility/Materials/GlassWet.mat`
  - `Assets/ScifiFacility/Models/props/details/controlpanels/controlpanel_*.fbx`
  - `Assets/ScifiFacility/Models/structural/walls/hull.fbx`

## Required Silhouettes

Cargo sled:

- Flat industrial load platform with frame rails, skid plates, buoyancy tanks, cargo clamps, side handles, tow points, and visible ride/dismount clearance.
- Must read as load-hauling equipment, not a submarine, glider, or generic crate.

Exosuit frame:

- Pressure-rated mechanical frame with torso cage, shoulder/hip hardpoints, limb sockets, hydraulic housings, thruster pods, service clamps, and visible operator attachment logic.
- Must read as wearable industrial survival equipment, not a cube backpack or decorative mech statue.

Micro-sub:

- Compact pressure vessel with rounded hull, viewport/glass, rubber seals, ballast/tank volumes, thrusters, docking clamps, service panels, and access/dismount side clearance.
- Must read as heavy, safer-near-surface transport with deeper-pressure vulnerability.

Scout glider:

- Directional underwater glider/scooter silhouette with fins or wings, nose sensor/light, hand/ride rails, battery pod, ducted thruster or flow body, and clear forward axis.
- Must read as agile, exposed, rider-driven scout transport, not a full shelter.

No one generic cube-replacement shell is acceptable for all four.

## Material And Texture Identity

Required material slots per transport family:

- Slot 0: pressure hull or structural frame metal/composite.
- Slot 1: rubber seals, grips, pads, straps, and gasket surfaces.
- Slot 2: glass, lens, viewport, sensor, or cockpit transparency/opaque-glass proxy.
- Slot 3: hazard paint, labels, clamps, worn panel trim, signal/emissive accents, and decals.

Required texture identity:

- Wet pressure metal with normal/MRAO, edge wear, salt deposits, scratches, and corrosion.
- Dirty pressure glass with scratches, condensation/wetness, and grime.
- Rubber seals/grips that remain matte and physically distinct from metal.
- Hazard paint and service labels that read at pickup/boarding distance without becoming UI decoration.
- Grime/corrosion/wetness masks from offline texture work, not runtime material clones or flat colors.

## Collider And Proxy Split

Replacement must split visual and gameplay truth:

- Visual hull/frame under named render children such as `VIS_*` or `LOD_*`.
- Existing `riderAnchor` and `dismountAnchor` preserved exactly unless a future vehicle owner approves an anchor migration with screenshot/player proof.
- Collision remains simple compound `COL_*` primitive proxies or existing root proxy ownership; no LOD0 visual `MeshCollider`.
- Triggers, mount/dismount logic, seat/occupancy mode, AUP/platform truth, kinematic force/command route, and preset values remain untouched.
- Runtime path changes require vehicle capture and profiler proof. This packet does not authorize runtime changes.

## LOD/HLOD Expectations

Each transport needs:

- LOD0: near product-face silhouette with bevels, panel breakup, material slots, anchors preserved.
- LOD1: reduced rails/tanks/panels/fins while preserving recognizability.
- LOD2: coarse silhouette that still reads cargo sled, exosuit frame, micro-sub, or scout glider at distance.
- HLOD/impostor: parked/distant transport clusters only, dithered with hysteresis. No alpha-blend overdraw path.
- Collider proxy identity must not change by visual LOD.

## Continuous Quality Consequences

`GlobalQualityWeight` scales presentation only.

- Low: no ugly mode; strong silhouette, material identity, readable anchors, cheap compound proxies, baked AO/detail masks, no material clones.
- Middle: richer decals, grime, labels, rubber/glass distinction, stronger LOD residency near player.
- High: higher bevel density, wetness response, panel/clamp detail, better glass and hull material response.
- Ultra: secondary cables, straps, small bolts, richer grime/wetness, subtle sensor/lamp/glass detail, longer near-field detail residency. No gameplay truth, AUP, collision identity, preset, seat, or anchor changes.

## Priority

First transport priority: `PFB_ScoutGlider_Transport`.

Reason: static preset evidence shows it is the most likely first-hour traversal product face: `speedMultiplier: 3.1`, `propulsionForce: 1250`, `energyDrainPerSecond: 3`, `swimPresentationScale: 0.9`, `thrusterAudioScale: 1`, `cameraMotionScale: 0.92`, and exposed rider-style `occupancyMode: 1`. It supports semi-open early exploration better than the cargo sled, is less late-depth/shelter-coded than the micro-sub, and is more immediately readable than the exosuit frame.

Second priority: `PFB_CargoSled_Transport` for salvage/load identity. Third: `PFB_Exosuit_Frame_Transport` for mechanical survival identity. Fourth: `PFB_MicroSub_Transport` because it needs the heaviest pressure-vessel proof and must not be treated as a cosmetic shell if runtime/cockpit behavior changes.

## Quarantine Decisions

None of the four target transport prefabs should be quarantined at this packet stage. They have valid preset/script/anchor owner routes and need visual replacement, not deletion.

Quarantine applies only to attempted source candidates that are primitive/proxy/placeholder:

- `WorldProceduralProxy` sources: do not use.
- `WorldRuntime/ProceduralPlaceholders` sources: do not use.
- Unity default material GUID route: replace or resolve; do not carry forward as final material source.

## Proof Ladder

Static package proof required now:

- Current prefab YAML primitive evidence.
- Current material GUID evidence.
- Preset owner paths.
- Anchor local transform contract.
- Candidate source/rejection matrix.

Replacement proof required later:

- Prefab YAML proving no enabled built-in primitive visual mesh.
- Mesh asset paths and material asset paths.
- Material texture role report for hull, glass, rubber, hazard paint, grime/wetness/corrosion, normal/MRAO.
- Anchor preservation proof against current local transform contract.
- Collider/proxy split proof: visual hull/frame separate from `COL_*` or existing primitive collision proxies.
- LOD/HLOD proof with dither/hysteresis.
- Unity screenshot/player capture before visual acceptance.
- Compact and High capture for readability/material floor.
- Profiler/GC proof only if runtime vehicle path, render path, collision, VFX, or hot presentation changes.

## Acceptance Boundary

Static source can prove only text/YAML/doc facts. This packet does not prove:

- Unity import health.
- Visual quality.
- screenshot/player capture.
- runtime vehicle feel.
- collision correctness.
- profiler/GC/frame time.
- material render response.

Evidence state remains `STATIC_SOURCE / STATIC_DOC`. Visual acceptance remains `PENDING VERIFICATION`.
