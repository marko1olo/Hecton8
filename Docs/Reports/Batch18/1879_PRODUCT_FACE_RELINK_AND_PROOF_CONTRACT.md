# 1879 Product-Face Relink And Proof Contract

Date: 2026-06-04
Agent: 1879
Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

This is a no-mutation contract for future removal of product-face primitive prefabs after source mesh packages exist. It does not edit source, prefabs, assets, scenes, `.meta`, or binaries. It does not run Unity, import, bake, PlayMode, profiler, dotnet, or Data Monolith work.

Controlling sequence CSV:

`Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv`

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `player.md`
- `tools.md`
- `inventory.md`
- `world.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`
- `Docs/Reports/Batch18/1869_TOOL_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1871_TRANSPORT_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1872_PLAYER_BODY_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1873_SKY_OCEAN_SOURCE_CLEANUP_AND_PROOF_SLOT_PACKET.md`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`

`Docs/Actual Domains of Project.txt` is absent. Narrow domain used: product-face mesh/source relink and proof contract.

## 1867 Blocker Coverage

The 1867 audit names 42 product-face primitive-prefab errors. This contract represents every blocker:

- Player: `Assets/_Project/Prefabs/Player.prefab`
- Sky/ocean: `Assets/_Project/Prefabs/Sky_System.prefab`; `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- Held tools: `Tool_BeaconDeployer_Held`, `Tool_Builder_Held`, `Tool_EnvAnalyzer_Held`, `Tool_Flashlight_Held`, `Tool_HarpoonLauncher_Held`, `Tool_Knife_Held`, `Tool_LaserCutter_Held`, `Tool_Propulsion_Held`, `Tool_Repair_Held`, `Tool_SalvageSampler_Held`, `Tool_Scanner_Held`, `Tool_StunPistol_Held`
- World tool pickups: `Item_Tool_BeaconDeployer_World`, `Item_Tool_Builder_World`, `Item_Tool_EnvAnalyzer_World`, `Item_Tool_Flashlight_World`, `Item_Tool_HarpoonLauncher_World`, `Item_Tool_Knife_World`, `Item_Tool_LaserCutter_World`, `Item_Tool_Propulsion_World`, `Item_Tool_Repair_World`, `Item_Tool_SalvageSampler_World`, `Item_Tool_Scanner_World`, `Item_Tool_StunPistol_World`
- Resource pickups: `PFB_Resource_CopperOre`, `PFB_Resource_FiberKelp`, `PFB_Resource_HydrocarbonResin`, `PFB_Resource_MembraneTissue`, `PFB_Resource_SilicaShards`, `PFB_Resource_SilverOre`, `PFB_Resource_SulfurClumps`, `PFB_Resource_TitaniumScrap`
- Transport: `PFB_CargoSled_Transport`, `PFB_Exosuit_Frame_Transport`, `PFB_MicroSub_Transport`, `PFB_ScoutGlider_Transport`
- Loose/legacy: `Assets/_Project/Prefabs/Item_Titanium.prefab`; `Assets/_Project/Prefabs/STRUCTURES.prefab`; `Assets/_Project/Prefabs/Buildings/Cube.prefab`

Static coverage only. This does not prove current audit count, Unity validator state, or runtime visibility.

## Contract Rules

Do not relink a target prefab until its source package has:

- project-owned non-primitive LOD mesh assets;
- material paths with albedo, normal, packed MRAO, emission/wetness/detail roles where relevant;
- documented MRAO G-channel meaning for the shader;
- `VIS_*` or `LOD_*` visual hierarchy separate from `COL_*` proxies;
- preserved anchors, data references, and presentation references;
- LOD/HLOD path with dither/hysteresis where visible at distance;
- rollback backup plan;
- static primitive scan and Unity validator plan;
- screenshot/profiler proof plan for route-specific acceptance.

Do not resolve product-face primitives by hiding them behind darkness, fog, storm, eclipse, bloom, silt, UI, crushed exposure, or camera crop. Surface, sky, Aegir, ocean surface, waterline, photic shallows, tools, pickups, player body, and transport silhouettes must be readable and materially credible on compact hardware.

## Category Contracts

### Player Suit / Body / Visor

Blocked roots:

- `Assets/_Project/Prefabs/Player.prefab`

Expected future mesh folder:

- `Assets/_Project/Art/Generated/ProductFace/PlayerSuit/`
- source prefab route under `Assets/_Project/Prefabs/Player/VisualSources/` or an explicitly approved equivalent.

Material/source requirements:

- graphite/rubber suit shell;
- wet pressure metal/hard plates;
- scratched visor glass with droplet/runoff masks;
- cyan/green instrument trims;
- amber latches and worn labels;
- packed normal/MRAO texture roles documented.

Collider proxy split:

- keep root movement `CapsuleCollider` as gameplay truth;
- visual meshes never become movement collision;
- any new interaction proxies must be named `COL_Visor`, `COL_Torso_Interact`, `COL_ToolMount`, or equivalent explicit `COL_*`.

Anchors/presentation references to preserve:

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
- `PlayerSwimPresentationController`
- all `Swim_*Attachment` transforms.

LOD/HLOD:

- LOD0: first-person arms/gloves/visor-adjacent hero detail plus near full suit.
- LOD1: reduced plates/hose/trim while preserving silhouette.
- LOD2: coarse suit silhouette with helmet, torso, and fins still readable.
- HLOD/impostor only for distant external/corpse/reflection routes.

Rollback:

- duplicate prefab before relink;
- preserve blockout attachment transforms until replacement is validated;
- do not delete primitive transform anchors until player presentation and tool attachment proof exists.

### Tools

Blocked roots:

- `Assets/_Project/Prefabs/Tools/Held/*.prefab`
- `Assets/_Project/Prefabs/Items/Tools/*.prefab`

Expected future mesh folder:

- `Assets/_Project/Art/Generated/ProductFace/Tools/`
- source prefab route under `Assets/_Project/Prefabs/Tools/Sources/`

Material/source requirements:

- shared held/world family meshes per tool identity;
- worn casing, rubber grip, metal barrel/nozzle, glass/lens/screen, labels, heat or residue where physical;
- `Hecton_ToolDecayLit.shader` and `Hecton_ToolScreenDiegetic.shader` may support final materials but do not prove them alone.

Collider proxy split:

- `COL_Grip*`, `COL_PickupTrigger`, `COL_Body*`, and verb-specific proxies such as `COL_BeamOriginProxy`, `COL_MuzzleProxy`, `COL_SampleContactProxy`;
- no LOD0 visual `MeshCollider`;
- runtime ray/beam/scan/tether truth remains in tool owners.

Anchors/presentation references to preserve:

- `ANCHOR_Grip_R` or `ANCHOR_Grip_LR`
- family ray/scan/beam/muzzle/nozzle origins
- `ANCHOR_TetherAnchor` for harpoon
- `ANCHOR_Pickup` for world variants
- `ANCHOR_AUP_LocalOrigin` where range/projectile/tether/scan can cross origin-shift-sensitive space.

LOD/HLOD:

- LOD0 first-person hero held mesh;
- LOD1 world pickup mesh from the same family;
- LOD2 coarse pickup silhouette;
- HLOD only for staged clutter clusters.

Rollback:

- relink held/world pair in one Unity slot per tool family;
- keep old prefab backup or prefab variant until both held and pickup pass static scan;
- never split held and world relinks across concurrent Unity owners.

### Resource Pickups

Blocked roots:

- `Assets/_Project/Prefabs/Resources/Pickups/*.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`

Expected future mesh folder:

- `Assets/_Project/Art/Generated/ProductFace/Resources/`
- source prefab route required by prior packets: `Assets/_Project/Prefabs/Resources/Sources/`

Material/source requirements:

- ore: host rock, mineral streaks, fracture normals, wetness, cavity AO;
- biological: kelp/tissue thickness, veins, wet masks, dithered translucency if used;
- resin: oily/translucent clump, grit, sagging lobes;
- scrap: bent/cut manufactured metal, bolt holes, paint remnants, salt, oil grime.

Collider proxy split:

- dumb pickup proxy uses item data as truth;
- `VIS_*` resource mesh separate from `COL_PickupTrigger` and coarse box/capsule/sphere/convex pickup bounds;
- no LOD0 visual `MeshCollider`.

Anchors/presentation references to preserve:

- item data references under `Assets/_Project/Data/Items/Resources/Raw/`;
- collection/highlight/scan component references;
- stable pickup trigger location and readable interaction distance.

LOD/HLOD:

- LOD0 near pickup identity;
- LOD1 reduced shards/fronds/veins;
- LOD2 coarse but identifiable;
- resource-field HLOD/impostor for dense placements.

Rollback:

- relink one canonical resource source package at a time;
- `Item_Titanium.prefab` either relinks to canonical titanium scrap or is quarantined with production-reference proof;
- no duplicate titanium visual/data truth.

### Transport

Blocked roots:

- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`
- `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`

Expected future mesh folder:

- `Assets/_Project/Art/Generated/ProductFace/Transport/`
- source prefab route under `Assets/_Project/Prefabs/Transport/Sources/`

Material/source requirements:

- pressure hull or structural frame metal/composite;
- rubber seals/grips/pads;
- dirty glass/lenses/viewports;
- hazard paint, labels, clamps, worn panels, signal accents.

Collider proxy split:

- visual hull/frame under `VIS_*` or `LOD_*`;
- keep existing rider/dismount anchors unless a vehicle owner approves migration;
- collision remains simple compound `COL_*` proxies or current vehicle collision owner;
- no LOD0 visual `MeshCollider`.

Anchors/presentation references to preserve:

- `RiderAnchor`
- `DismountAnchor`
- transport preset references
- `PlayerTransportFeelContract`
- `MountablePlayerTransport`
- occupancy, AUP, kinematic, drive, mount/dismount, and camera feel routes.

LOD/HLOD:

- LOD0 distinct silhouette per transport;
- LOD1 reduced rails/tanks/panels/fins;
- LOD2 coarse but recognizable;
- HLOD/impostor for parked or distant vehicles only.

Rollback:

- relink one transport at a time;
- preserve anchors by static local transform comparison;
- if runtime vehicle behavior changes, revert the visual relink until vehicle capture/profiler proof exists.

### Sky / Ocean Source Cleanup

Blocked roots:

- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`

Expected future mesh/material folder:

- sky: `Assets/_Project/Art/Generated/Sky/` or current production sky dome route with explicit mesh/material proof;
- ocean source cleanup: `Assets/_Project/Art/Generated/Ocean/` only for first-party micro-fauna/input carrier replacements; Crest package assets remain assigned, not cloned.

Material/source requirements:

- sky source prefab must not require a scene override to avoid a built-in primitive sphere;
- sky material/texture route must support readable Aegir/moons/clouds, not sine/noise placeholder art;
- Crest input carriers may remain only as hidden-input-only with runtime/Frame Debugger proof;
- micro-fauna boid mesh must be authored/generated or a designed impostor/card set, not raw built-in plane.

Collider proxy split:

- not gameplay collision unless an explicit owner says so;
- source cleanup must distinguish visible presentation from Crest data-input carriers.

Anchors/presentation references to preserve:

- `SkySystemFollowCamera`
- runtime camera reference route;
- Crest ocean material assignment route;
- primary light reference;
- Crest input component bindings;
- micro-fauna owner references.

LOD/HLOD:

- sky dome/celestial visual route must preserve surface and photic readability at compact and high tiers;
- micro-fauna/card route must use depth fade, material animation, orientation rules, and density/HLOD gates where applicable.

Rollback:

- backup source prefabs before source cleanup;
- if hidden-input proof fails, convert input carrier route instead of adding a broad validator bypass;
- do not mutate Crest package materials.

### Loose / Legacy Roots

Blocked roots:

- `Assets/_Project/Prefabs/Item_Titanium.prefab`
- `Assets/_Project/Prefabs/STRUCTURES.prefab`
- `Assets/_Project/Prefabs/Buildings/Cube.prefab`

Contract:

- `Item_Titanium.prefab` must either become canonical titanium scrap visual or be quarantined with production-reference proof.
- `STRUCTURES.prefab` must not leak primitive `Item_Titanium` into production; it should delegate to canonical child assets or be quarantined.
- `Buildings/Cube.prefab` is a raw placeholder unless a construction owner provides a real pressure-rated module source package.

No deletion is authorized by this packet.

## One Unity-Owner Sequence

The future Unity owner must execute this serialized order:

1. Confirm uncontested Unity slot: no other Unity/build/profiler/import/DataMonolith owner active, no active `dotnet`/`csc`/Unity import/build job, CPU below project gate.
2. Snapshot backups of target prefabs to an owned rollback folder outside runtime product routes or use VCS branch/commit backup according to integrator policy.
3. Import or generate source mesh/material assets for one category batch only.
4. Refresh/import once and wait for Unity readiness.
5. Relink prefabs in this order: player suit/body/visor; first-priority held/world tools; resource pickups; transport; sky/ocean source cleanup; loose/legacy quarantine/relink.
6. Preserve anchors/references and compare anchor transforms against the current static contracts.
7. Run `Hecton8/Validation/Product-Face Prefab Quality Gate`.
8. Run `Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error`.
9. Run sky/ocean validator from 1878: `Hecton8/Validation/Sky-Ocean Source Primitive Gate`.
10. Capture route-specific screenshots: first-person player/tools, world pickups, resource fields, transport boarding/near view, surface ocean/sky/waterline/photic shallows/medium-depth.
11. Capture profiler/Frame Debugger/GC only after visual relinks are in the route and only when runtime/render path changes or acceptance is being claimed.
12. Rerun static audit and Unity validator after any rollback.

Do not split validator, screenshot, and profiler acceptance from the same Unity state. Acceptance from mixed slots is rejected.

## Parallel Work Before Unity Ownership

Can run while Unity is busy:

- mesh-source authoring per tool/resource/transport/player/sky/ocean family in separate source folders;
- material/source inventory and texture role manifests;
- static prefab/YAML audit updates that do not mutate prefabs;
- validator/report preparation;
- screenshot shot-list prep;
- rollback plan prep;
- CSV task slicing for future agents;
- sky/ocean validator follow-up/report prep around `Hecton8/Validation/Sky-Ocean Source Primitive Gate`.

Must not run in parallel inside Unity:

- mesh asset generation/import refresh;
- prefab relink;
- Unity validator menu execution;
- static audit rerun if it depends on just-imported Unity output;
- screenshot/player capture;
- profiler/Frame Debugger/GC capture;
- Data Monolith, bake, PlayMode, or build work.

## Red Gates

Hard red gates for future closure:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error
```

```text
Hecton8/Validation/Product-Face Prefab Quality Gate
```

```text
Hecton8/Validation/Sky-Ocean Source Primitive Gate
```

Route-specific screenshot/profiler proof is mandatory before runtime or visual acceptance. Static docs do not close product-face visuals.

## Continuous GlobalQualityWeight Consequences

`GlobalQualityWeight` scales presentation continuously. It does not change gameplay truth, item ids, recipe truth, collision identity, save identity, transport presets, anchors, DTO layout, or authority route.

- Player suit: compact keeps authored suit/visor/gloves/fins silhouette and readable anchors; middle adds grime/labels; high adds wetness, bevels, visor scratches; ultra adds hoses, straps, droplets, and micro fittings.
- Tools: compact keeps grip/nozzle/lens/muzzle/tool verb silhouette; middle adds labels/grime; high adds material response and bevels; ultra adds small screws, cables, glow, slag/spark presentation only.
- Resources: compact keeps physical resource silhouette and material family; middle adds veins/fronds/residue; high adds fracture/wetness/glass/translucency response; ultra adds micro chips, folds, and secondary shards.
- Transport: compact keeps vehicle-specific silhouette, anchors, compound proxies, and material identity; middle adds decals/grime; high adds hull/glass/rubber richness; ultra adds secondary cables, bolts, straps, and lights.
- Sky/ocean: compact keeps beautiful readable ocean color, sky/Aegir/moon silhouette, waterline, foam/specular identity, and photic route cues; middle adds cloud/ocean richness; high adds reflection/material depth; ultra adds atmosphere, foam, micro-fauna density, and longer visual residency without hiding route truth.

## Acceptance Boundary

This packet is STATIC_DOC / STATIC_SOURCE only.

Not claimed:

- Unity import health;
- product-face validator pass;
- sky/ocean validator pass;
- player-visible visual quality;
- screenshots/player capture;
- profiler/GC/frame time;
- runtime behavior;
- scene wiring;
- source mesh existence beyond prior static reports.

All runtime and visual acceptance remains PENDING VERIFICATION.

## Verification Plan For This Packet

Required for this no-mutation packet:

- `git diff --check` on owned outputs.
- Static cross-check that the 42 product-face blocker roots from 1867 are represented in this report/CSV.

Future owners must not treat this packet as acceptance evidence. It is a task contract.
