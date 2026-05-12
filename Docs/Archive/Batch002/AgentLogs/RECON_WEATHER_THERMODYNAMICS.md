# RECON WEATHER_THERMODYNAMICS

Scan date: 2026-05-11  
Scope requested: `Assets/_Project/Art/VFX/` and scripts  
Actual art scope: `Assets/_Project/Art/VFX/` is missing. Scanned `Assets/_Project/Art` prefab/scene/asset files instead.

## Commands
- `rg -n "ParticleSystem\.(CollisionModule|SubEmittersModule)|ParticleSystemCollision|ParticleSystemSubEmitter|SubEmittersModule|CollisionModule|\.collision\s*=|\.subEmitters\s*=" Assets/_Project/Scripts -g "*.cs"`
- `rg -n "m_SubEmitters|collisionModule|CollisionModule|subEmitters|SubEmitters|ParticleSystem" Assets/_Project/Art -g "*.prefab" -g "*.unity" -g "*.asset"`

## Findings
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:757` uses `ParticleSystem.CollisionModule collision = _hullImpactSparkParticles.collision;`. This is not boiling-water VFX, but it is a CPU ParticleSystem collision-module usage and should be reviewed by the submarine damage/VFX owner.
- No `SubEmittersModule` usage found in scanned scripts.
- No ParticleSystem collision/sub-emitter records found under `Assets/_Project/Art` prefab/scene/asset files.
- `Assets/_Project/Scripts/ThermalGeyser.cs` no longer contains a `ParticleSystem` field or play/stop path for eruption boiling.

## Thermal Action
- Removed CPU `ParticleSystem` control from `ThermalGeyser`.
- Boiling bubbles now publish `_HectonThermalBubbleCommandCount` and `_HectonThermalBubbleCommands` for compute/VFX consumption from `AbyssalThermalManager`.
