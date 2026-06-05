# 1853 Primitive Final Replacement Plan

Date: 2026-06-04
Evidence class: STATIC_SOURCE
Unity compile: PENDING VERIFICATION
Runtime proof: PENDING VERIFICATION

## Scope

This report consolidates the current production `Final` prefab blocker found by `Tools/GeneratedAssetProductionAudit.py`.

Current static audit result:

- `generated_asset_packages=392`
- `fatal=0`
- `error=41`
- `warn=1281`
- `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH`: 21 direct production `Final` prefabs
- `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`: 20 final-ready/non-proxy family links

The 20 family-link errors are not separate content from the direct prefab-root errors. They are evidence that 20 of the primitive `Final` prefabs are already wired as production family variants. `PFB_SargassumCollapseChunk.prefab` is the extra unlinked production-path primitive prefab caught by the direct root scan.

## Blocked Final Prefabs

### World Support

- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_CreatureSpawn_Passive.prefab`
  - Family link: `family.creature.spawn.passive.final.school_anchor`
  - Current defect: 11 Unity built-in primitive mesh refs.
  - Required replacement: hidden gameplay spawn support plus visible reef/kelp school-anchor habitat built from authored/generated non-primitive meshes.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_CreatureSpawn_Predator.prefab`
  - Family link: `family.creature.spawn.predator.final.predator_lair`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: hidden predator spawn support plus real predator lair carrier from custom geology/nest/scratch meshes.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_AbyssApex.prefab`
  - Family link: `family.creature.zone.abyss_apex.final.ownership_zone`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: custom abyss landmark carrier; ownership/trigger data must remain hidden support logic.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_LargeThreat.prefab`
  - Family link: `family.creature.zone.large_threat.final.ownership_zone`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: authored threat-territory visual using non-primitive rock/spine/perch carriers.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_ReefApex.prefab`
  - Family link: `family.creature.zone.reef_apex.final.ownership_zone`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: bright shallow reef apex composition from baked coral/kelp custom meshes plus hidden support volumes.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_RuinApex.prefab`
  - Family link: `family.creature.zone.ruin_apex.final.ownership_zone`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: real ruin-frame/perch mesh set; do not reuse primitive construction finals.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Hazard.prefab`
  - Family link: `family.pocket.hazard.final.vent_cluster`
  - Current defect: 15 Unity built-in primitive mesh refs.
  - Required replacement: custom vent chimney/tube-worm/geology carrier plus VFX; `bubble vent atlas - bad - redo.png` is not proof.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Resource.prefab`
  - Family link: `family.pocket.resource.final.cache`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: custom geology/deposit mesh set; pickup/resource logic remains separate.
- `Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Safe.prefab`
  - Family link: `family.pocket.safe.final.shelter`
  - Current defect: 11 Unity built-in primitive mesh refs.
  - Required replacement: readable safe-pocket shelter using rock arch/cave/coral/kelp non-primitive carriers.

### Construction

- `Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab`
  - Family link: `family.debris.field.final.wreck_field`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: torn hull plates, ribs, pipes, salvage cuts, LOD0-2, primitive collision proxies only.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Debris_ScrapCluster.prefab`
  - Family link: `family.debris.scatter.final.scrap_cluster`
  - Current defect: 9 Unity built-in primitive mesh refs.
  - Required replacement: beveled scrap plates, cut pipes, cable glands, broken fasteners, shared construction/scrap materials.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab`
  - Family link: `family.route.power.final.pylon`
  - Current defect: 1 Unity built-in primitive mesh ref.
  - Required replacement: pressure-rated pylon with base clamp, insulators, cable sockets, bolts, service panels.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab`
  - Family link: `family.route.power.final.current_turbine`
  - Current defect: 1 Unity built-in primitive mesh ref.
  - Required replacement: turbine assembly with rotor, shroud, support struts, cable/power anchors.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_ClusterMedium.prefab`
  - Family link: `family.ruin.cluster.medium.final.cluster_medium`
  - Current defect: 16 Unity built-in primitive mesh refs.
  - Required replacement: abandoned module cluster with bevels, broken sockets, corrosion, exposed frames, readable route gaps.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_Megastructure.prefab`
  - Family link: `family.ruin.megastructure.final.megastructure`
  - Current defect: 23 Unity built-in primitive mesh refs.
  - Required replacement: landmark-scale pressure frames, large silhouettes, internal voids, HLOD/impostor plan.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Foundation.prefab`
  - Family link: `family.ruin.module.single.final.foundation`
  - Current defect: 9 Unity built-in primitive mesh refs.
  - Required replacement: generated/manufactured foundation mesh from construction template: bevels, plates, sockets, legs/anchors, worn edges.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab`
  - Family link: `family.ruin.module.single.final.corridor`
  - Current defect: 9 Unity built-in primitive mesh refs.
  - Required replacement: pressure corridor shell with ribs, flanges, gasket frames, socket frames, retained interior trigger.
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab`
  - Family link: `family.service.scar.final.service_pump`
  - Current defect: 1 Unity built-in primitive mesh ref.
  - Required replacement: pump casing, intake/outflow ports, gauge/screen mask, bolted access panel.
- `Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab`
  - Family link: not currently flagged as final-ready/non-proxy.
  - Current defect: 1 Unity built-in primitive mesh ref in a production `Final` folder.
  - Required replacement: either move to explicit dev/proxy quarantine or rebuild as non-primitive collapse chunk with material/LOD/collider proof.

### Organic Misc

- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_EggCluster.prefab`
  - Family link: `family.egg.cluster.final.nest_cluster`
  - Current defect: 11 Unity built-in primitive mesh refs.
  - Required replacement: varied eggs, membrane webbing, substrate pad, AO/cavity vertex colors, LOD0-2.
- `Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_PlantGiant.prefab`
  - Family link: `family.plant.giant.final.silhouette`
  - Current defect: 12 Unity built-in primitive mesh refs.
  - Required replacement: holdfast/root, tapered trunk, canopy/fronds, sway/biolum/AO semantics, LOD0-2.

## Rejected Shortcuts

- Do not relink to nearby `WorldProceduralProxy` prefabs. The inspected proxy neighborhoods are also primitive.
- Do not relink creature/support families to AI proxy objects such as hunter/leviathan/passive proxies. Support gameplay logic is not visible production art.
- Do not keep visible Unity primitive child meshes and claim material polish solves it. The visual floor rejects primitive final silhouettes.
- Do not delete families or set everything proxy-only as a completion claim. That only hides missing production content.
- Do not use storms, darkness, fog, or depth grading to hide these assets on surface/shallow/mid-depth routes.

## Safe Work Split

These tracks can run independently without blocking each other:

- WorldSupport visible-carrier rebuild specification and generator design.
- Construction final mesh generator/authoring design.
- Organic misc egg/giant-plant generator design.
- Sargassum collapse chunk classification and replacement route.
- Final prefab proof/manifests pass for generated flora/geology packages currently warning on `MISSING_MANIFEST`, `MISSING_NAMED_PROOF`, and `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`.

Unity/live-scene work should wait for the current Unity owner slot. Static source work can continue.

## Acceptance

Each replacement must provide:

- non-primitive authored/generated visible mesh refs;
- LOD0/LOD1/LOD2 or a documented HLOD/impostor route for large landmarks;
- collider proxy that is simple but not used as visible art;
- material slots tied to real construction/organic/geology/support materials;
- manifest/proof artifact;
- screenshot/render proof for surface, shallow, or hero-route usage;
- family link restored only after the above passes.

The current state is intentionally red. That is correct until real production final prefabs replace the primitive visuals.
