# HECTON-8 SURVIVAL / DAMAGE / HAZARD SYSTEM MAP

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: source-backed ownership map for survival, oxygen, pressure, thermal stress, hazard routing, damage consequences, and adjacent stress/presentation branches
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Persistent_Object_Registry.txt`

## Purpose

This file exists to answer one narrow question:

Who currently owns the player survival, damage, pressure, thermal, hazard, and stress-consequence stack in first-party code.

This is not play-mode proof.
This is a source-backed ownership map.

## Proof Boundary

Primary evidence came from:

- `Assets/_Project/Scripts/HectonSurvivalSystem.cs`
- `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`
- `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs`
- `Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs`
- `Assets/_Project/Scripts/Audio/DeepPsychosisController.cs`
- `Assets/_Project/Scripts/Visor/PlayerStressVFX.cs`
- `Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs`
- `Assets/_Project/Prefabs/Player.prefab`

It does not prove:

- integrated survival correctness in live gameplay
- measured GC/CPU cost during hazard-heavy traversal
- that all referenced components are currently authored and wired in the active scene

## Core Ownership Model

The current survival stack is not one health component.
It is split across five layers:

1. survival core simulation
2. parallel health / mutation branch
3. hazard registration and exposure routing
4. environmental source systems
5. stress/audio/visor consequence surfaces

That split is real in code and it matters, because some responsibilities overlap rather than cleanly nesting.

## Layer 1: Core Survival Authority

### `HectonSurvivalSystem` Is The Real Survival Spine

`Assets/_Project/Scripts/HectonSurvivalSystem.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `184` | class is `HectonSurvivalSystem : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ISaveable` |
| `338` | publishes `OnOxygenChanged` |
| `342` | publishes `OnPressureChanged` |
| `393` | exposes `OverpressureMeters` |
| `419` | exposes current thermal stress mode |
| `427` | exposes rapid-ascent risk |
| `429` | exposes oxygen-grace runtime state |
| `463` | registers with `GlobalRegistry.Save` |
| `504` | registers as `PriorityLayer.Player` update owner |
| `510` | registers as `PriorityLayer.Player` slow-tick owner |
| `916` | resolves environmental resistance by hazard type |
| `1365` | implements direct damage path via `TakeDamage(float amount)` |
| `1559-1560` | `SavePriority => 10`, `LoadPriority => 10` |

Direct authored placement confirmed on player root:

| Prefab line | Component | Role |
|---|---|---|
| `812` | `HectonSurvivalSystem` | authored player survival owner |

This class is much broader than “oxygen bar logic”.
It owns:

- oxygen
- energy
- depth
- suit integrity
- pressure
- hunger
- thirst
- thermal convergence and thermal stress
- decompression / rapid-ascent risk
- oxygen-grace behavior
- death-cause resolution
- survival telemetry and death advice
- persistence of the survival state

Current interpretation:

- `HectonSurvivalSystem` is the main survival authority
- it already includes significant damage semantics through integrity loss and `TakeDamage`
- it is not just a sidecar under some other health class

## Layer 2: Parallel Health Branch

### `HectonPlayerHealth` Is Real, But Architecturally Uneasy

`Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `19` | class is `HectonPlayerHealth : MonoBehaviour, ISaveable, ITickable, IUpdatable` |
| `66` | publishes `OnDeath` |
| `69` | publishes `OnDamageTaken` |
| `75` | publishes `OnMutationFlagsChanged` |
| `96` | exposes mutation bitmask |
| `104` | exposes flashlight-bypass mutation flag |
| `106` | applies radiation exposure |
| `210` | implements `TakeDamage(float damage, bool ignoreInvulnerability = false)` |
| `290` | emits `NotificationEvents.PushCritical(...)` for survival-grace branch |
| `320-321` | emits mutation notifications |
| `364` | `SavePriority => 100` |
| `367` | `LoadPriority => 100` |
| `373` | explicitly says `SaveData` has no dedicated player-health DTO |

Additional factual boundary:

- direct `Player.prefab` scan in this pass confirmed `HectonSurvivalSystem`, but did not confirm `HectonPlayerHealth` on the same prefab text surface

Current interpretation:

- `HectonPlayerHealth` is not fake or dead code
- fauna and tool code reference it directly
- it owns HP, mutation, invulnerability, radiation-fatigue, and survival-grace behavior
- but its persistence path is weak because the file itself states there is no dedicated `SaveData` DTO

This is a real architecture smell:

- `HectonSurvivalSystem` already owns integrity/damage/death semantics
- `HectonPlayerHealth` separately owns HP/mutation/grace semantics
- those two branches are adjacent enough to drift or conflict over time

## Layer 3: Hazard Registration And Exposure Routing

### `HazardZoneManager`

`Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `192` | class is `HazardZoneManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable` |
| `278` | service can be ensured through `EnsureRuntimeInstance()` |
| `604` | resolves player runtime context through `GlobalRegistry.Player` |
| `716` | applies damage through `_playerSurvival.TakeDamage(damageMagnitude)` |
| `741` | consumes survival-side hazard resistance |
| `1176` | registers hazard entries into spatial hash |
| `1237` | waits for `GlobalRegistry.Dispatcher` before registration |
| `1240-1241` | registers as environment update and late-frame owner |

This is the runtime exposure and damage-routing owner.
It is not the authored source of all hazards.
It is the place where hazard volumes become player/vehicle exposure and eventual survival damage.

### `HectonHazardManager`

`Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `HectonHazardManager : MonoBehaviour` |
| `23` | can ensure runtime instance |
| `39` | exposes runtime hazard registration |
| `53` | exposes runtime hazard unregistration |
| `73` | resolves environment context through `GlobalRegistry.Environment` |

This is the compatibility / bridge layer for hazard registration.
It is not the full exposure simulation.
It is the authoring/runtime ingress path into `HazardZoneManager`.

## Layer 4: Environmental Source Systems Feeding Survival

### `AbyssalThermalManager`

`Assets/_Project/Scripts/World/AbyssalThermalManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `19` | class is `AbyssalThermalManager : MonoBehaviour, ITickable, ISlowTickable, IOriginShiftListener, IThermodynamicsService` |
| `480-481` | validates itself against `GlobalRegistry.ThermodynamicsService` and `GlobalRegistry.Thermodynamics` |
| `1334` | registers heat hazards through `HectonHazardManager.Register(...)` |
| `2317` | publishes through `GlobalRegistry.RegisterThermodynamicsRuntime(this)` |
| `2326` | registers as environment updatable |
| `2332` | registers as environment slow-tick owner |

This is not only a temperature sampler.
It is both:

- the thermodynamics service owner
- a hazard-source owner that injects thermal hazard volumes into the hazard runtime

### `EnvironmentalStrainManager`

`Assets/_Project/Scripts/World/EnvironmentalStrainManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `EnvironmentalStrainManager : MonoBehaviour, ISaveable` |
| `51` | `SavePriority => 41` |
| `56` | `LoadPriority => 41` |
| `106` | registers with `GlobalRegistry.Save` |

Current interpretation:

- this is not the direct damage owner
- it is a save-backed ecological-pressure owner adjacent to hazard and psychosis systems

### `HabitatIntegrityManager`

`Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `105` | class is `HabitatIntegrityManager : MonoBehaviour, IUpdatable, ISlowTickable, Hecton8.Core.IDamageReceiver, IDamageSignalReceiver, IDamageSignalEmitter` |
| `594` | registers as `PriorityLayer.Core` updatable |
| `738` | registers toxicity hazards through `HectonHazardManager.Register(...)` |
| `752` | unregisters that toxicity hazard |

This class belongs to habitat/module integrity, not player survival directly.
But it is part of the survival-relevant hazard picture because it can emit toxicity into the same runtime hazard fabric.

### Oxygen Support Actors

Additional survival support actors found in source:

| Owner | Evidence | Role |
|---|---|---|
| `OxygenBubble` | `Assets/_Project/Scripts/Gameplay/OxygenBubble.cs:46` | runtime oxygen refill actor |
| `OxygenPlant` | `Assets/_Project/Scripts/Gameplay/OxygenPlant.cs:33` | spawner/owner for oxygen-bubble support path |

These are not the survival spine.
They are field-side oxygen-supply actors feeding it.

## Layer 5: Stress / Psychosis / Presentation Consequences

### `DeepPsychosisController`

`Assets/_Project/Scripts/Audio/DeepPsychosisController.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `15` | class is `DeepPsychosisController : MonoBehaviour, ITickable, IUpdatable, ISlowTickable` |
| `125` | reads survival oxygen state |
| `139-140` | composes psychosis intensity from depth/oxygen/pollution pressure |
| `170` | registers as `PriorityLayer.Player` updatable |
| `176` | registers as `PriorityLayer.Player` slow-tick owner |
| `198` | routes playback through `GlobalRegistry.Audio` |

This is an audio consequence owner, not core damage logic.
It transforms survival/hazard pressure into hallucination/stress audio behavior.

### `PlayerStressVFX`

`Assets/_Project/Scripts/Visor/PlayerStressVFX.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `15` | class is `PlayerStressVFX : MonoBehaviour, ITickable` |
| `150-152` | resolves current stress magnitude |
| `280-285` | derives stress from survival oxygen, integrity, and fatal pressure |
| `357-373` | derives fog/frost stress from oxygen grace, thermal shock, and rapid ascent |
| `264` | registers as `PriorityLayer.UI` updatable |
| `338` | routes heartbeat playback through `GlobalRegistry.Audio` |

This is the visor/presentation consequence owner for survival stress.
It is downstream of survival state, not the owner of it.

## Current Survival Graph

Current survival-facing graph can be summarized as:

`HectonSurvivalSystem`
<- hazard exposure from `HazardZoneManager`
<- runtime hazard ingress from `HectonHazardManager`
<- environmental hazard sources like `AbyssalThermalManager` and `HabitatIntegrityManager`
-> stress consequences in `DeepPsychosisController`
-> visor consequences in `PlayerStressVFX`

Parallel to that:

`HectonPlayerHealth`
-> HP / mutation / invulnerability / survival-grace branch
-> referenced directly by fauna and some tool paths

This is not a perfectly singular stack.
It is a dominant survival spine plus one parallel health branch.

## What Looks Good

- `HectonSurvivalSystem` is a real, broad, explicit survival owner with save integration and detailed death-state semantics.
- hazard exposure is not hidden in random trigger scripts; there is a proper runtime hazard-routing owner.
- thermodynamics is not just visual dressing; `AbyssalThermalManager` is a service owner and hazard-source owner.
- stress consequences are already split cleanly into audio (`DeepPsychosisController`) and visor presentation (`PlayerStressVFX`).

## What Looks Merely Acceptable

- ecological strain is separated from immediate player damage, which is sensible, but the docset still lacks live proof of how strongly it feeds the psychosis/hazard loop.
- oxygen plant / bubble actors are small but important field-side support surfaces rather than core survival logic.

## What Looks Weak

- `HectonPlayerHealth` is a real parallel branch with its own damage and mutation semantics, but its own file admits the save path is legacy and incomplete.
- direct authored placement for `HectonPlayerHealth` was not confirmed by this `Player.prefab` text scan, while `HectonSurvivalSystem` was.
- survival/damage semantics are split between `HectonSurvivalSystem` and `HectonPlayerHealth`, which raises overlap and regression risk.
- no integrated measured runtime proof exists for deep-pressure, thermal, psychosis, and visor-stress behavior under the same traversal.

## Failure Modes To Watch

- damage can look like one system in gameplay while actually diverging between integrity-based survival damage and HP-based health damage.
- hazard-source systems can register correctly while exposure routing or resistance math regresses one layer deeper.
- stress presentation can stay visually active even if the underlying survival cause chain is wrong.
- mutation/radiation behavior can appear persistent while not truly surviving save/load, because the health component itself documents a weak persistence path.

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves visibility into one of the most failure-prone gameplay domains by exposing the real overlap between survival, health, hazard, and stress-consequence systems. |

## Verdict

Current honest picture:

- `HectonSurvivalSystem` is the dominant survival authority
- `HazardZoneManager` is the runtime exposure and damage-routing owner
- `HectonHazardManager` is the ingress bridge for authored/runtime hazard sources
- `AbyssalThermalManager` and `HabitatIntegrityManager` are important hazard-source owners
- `DeepPsychosisController` and `PlayerStressVFX` are downstream consequence owners
- `HectonPlayerHealth` is a real parallel HP/mutation branch with a documented weak persistence path

This domain is more detailed and more internally split than earlier broad ledgers showed.
It still lacks integrated runtime proof.

STATUS: PENDING VERIFICATION
