# RECON_SUBMARINE_DAMAGE_CONTROL

STATUS: PENDING VERIFICATION

Scan scope: `Assets/_Project/Prefabs`
Command basis: `rg -n "Submarine|RepairTool|Leak|Plume|ParticleSystem|sparksVFX" Assets/_Project/Prefabs`

## Relevant Prefabs
- `Assets/_Project/Prefabs/PFB_Submarine_Core.prefab` contains `SubmarineCoreDirector`, `SubmarineFluidDynamics`, and `SubmarineStructuralGrid`.
- `Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab` contains `RepairTool`; `sparksVFX` is not wired.
- `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab` is the world pickup path for the repair tool.
- `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab` is transport-related, not the core damage-control owner.

## Leak/Particle Findings
- `Construction/Final/PFB_Module_Foundation.prefab` contains leak wet sheen, scuff/stripe decals, leak VFX, and ParticleSystem components.
- `Construction/Final/PFB_Module_Corridor.prefab` contains the same habitat module leak VFX pattern.
- `Construction/Final/PFB_Ruin_ClusterMedium.prefab` contains `RuinLeakPlume_Main` ParticleSystemRenderer.
- `Construction/Final/PFB_Ruin_Megastructure.prefab` contains `RuinLeakPlume_Bridge` and `RuinLeakPlume_Core`.
- No direct submarine leak ParticleSystem was found in `PFB_Submarine_Core.prefab`.

## Decision
Submarine damage-control leaks are implemented on `SubmarineStructuralGrid` with a packed local-space SOA buffer and compute dispatch. Existing habitat/ruin leak ParticleSystems are logged only; they are not reused because the prompt requires GPU leak particles and no GameObject emitter avalanche.
