# 02 - Water / Vehicle / Damage / Lighting Documentation Anchors

Status: READY_FOR_AGENT

Evidence class: STATIC_DOC target.

## Mission

Close exact stable-doc anchor gaps for:

- `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs`
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs`
- `Assets/_Project/Scripts/Power/SubmarineOsThermalGridRuntime.cs`
- `Assets/_Project/Scripts/Lighting/DynamicPointLightCulling/DynamicPointLightCullingDirector.cs`

## Target Docs

- `water.md`
- `physics.md`
- `vehicles.md`
- `logistics.md`
- `lighting.md`
- `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`
- `Docs/ARCHITECTURE/Vehicle_Component_Damage_Router_SHINOBU_152.md`
- existing submarine dynamics / lighting route docs discovered by `rg`.

## Required Output

Add compact class anchors and proof gaps. Include:

- no Crest material clone/runtime wrapper claim;
- force/damage ownership boundaries;
- black-box route presence vs runtime proof absence;
- Frame Debugger/profiler/GC proof requirements.

No Unity run. No scene/prefab/material mutation.
