# Persistence, Content, And Ecosystem Deep Dive

Status: PENDING VERIFICATION

Purpose:
- show how much authored content and persistence surface the project actually carries
- describe where persistence is mature versus where it is merely broad

## Authored Content Surface Is Large

Observed asset counts:

| Path | File count | Reading |
|---|---:|---|
| `Assets/_Project/Data` | 2649 | large authored data surface |
| `Assets/_Project/Data/Items` | 146 | item/content authoring is substantial |
| `Assets/_Project/Data/World/FloraTemplates` | 70 | flora authoring is real and varied |
| `Assets/_Project/Data/Lore/Quests` | 14 | quest count is modest but concrete |
| `Assets/_Project/Data/Construction` | 58 | construction/module authoring is meaningful |
| `Assets/_Project/Data/Scavenging/ResourceNodes` | 46 | scavenging/resource template surface is real |

Interpretation:
- the project is not only system-heavy
- it also has enough authored data to be considered a serious content-bearing codebase

## ScriptableObject Reality

The project uses `ScriptableObject` authoring heavily across:
- gameplay
- world
- tools
- VFX
- fauna
- narrative
- data
- environment
- construction
- scavenging

This matters because the authored/runtime boundary is not small.
It is one of the main project realities.

## Persistence Owners

### `WorldStateManager`

Static shape:
- ~532 lines
- `ISaveable`
- singleton-style instance
- `DontDestroyOnLoad`
- save priority `50`

Reading:
- this is a real world-depletion and persistence owner
- it is not huge, but it is foundational

Strength:
- clear purpose
- explicit save registration

Weakness:
- still classic persistent-manager style

### `WorldProceduralStateRegistry`

Static shape:
- ~385 lines
- `ISaveable`
- save priority `55`
- direct event surface via `PlacementStateChanged`

Reading:
- procedural world retention is not theoretical
- this registry exists to stabilize generated-world state across sessions

Strength:
- good sign of procedural-world seriousness

Weakness:
- small file, but high semantic importance
- event model is still direct, not fully harmonized with stronger queue-backed patterns

### `PlayerInventory`

Static shape:
- ~1925 lines
- `ISaveable`, `ISlowTickable`
- save/load priority `20`
- native references `52`

Reading:
- inventory persistence is real and engineered
- inventory is one of the stronger gameplay subsystems

Strength:
- SOA/native design
- explicit runtime degradation and state words

Weakness:
- player inventory is a dependency hub

### `QuestManager`

Static shape:
- ~576 lines
- `ISaveable`, `IQuestSystem`
- save/load priority `7`
- native references `3`

Reading:
- quest persistence exists early in the restore order
- quest runtime is more concrete than many similar-sized game projects

Strength:
- clear authored lookup and runtime state bridge

Weakness:
- early restore priority may hide dependency assumptions

### `FaunaDirector`

Static shape:
- ~4619 lines
- `IUpdatable`, `ISlowTickable`, `ISaveable`
- save/load priority `56`
- native references `15`
- static/singleton residue and many `.Instance` dependencies

Reading:
- fauna is a real persistent world subsystem
- it is also one of the clearest examples of mixed architectural eras

Strength:
- broad actual behavior
- explicit save participation

Weakness:
- large dependency web:
  - `MapMagicBridge.Instance`
  - `ObjectPoolManager.Instance`
  - `FaunaGeneticsManager.Instance`
  - `EcosystemHealthDirector.Instance`
  - `PersistentWorldRegistry.Instance`
  - `DepthZoneDirector.Instance`
  - `DynamicResolutionScaler.Instance`

This is not clean service authority.
It is practical accretion.

## Audio As A World-State Consumer

### `SpatialAudioManager`

Static shape:
- ~2565 lines
- `IAudioService`, `IUpdatable`
- registry references `15`
- native references `14`
- `NativeQueue<DelayedAudioEvent>`

Reading:
- audio is not just a utility
- it is reading player/runtime/world context actively

Strength:
- meaningful runtime engineering depth

Weakness:
- service still presents itself in singleton language while living in registry reality

## Ecosystem Reality

The ecosystem lane is not one clean subsystem.
It is distributed across:
- `FaunaDirector`
- `FaunaGeneticsManager`
- `EcosystemHealthDirector`
- `ResourceScarcityDirector`
- `EnvironmentalStrainManager`
- world registries and persistence owners

This means:
- the ecosystem is real
- the ecosystem is also fragmented in ownership

## Brutal Summary

Persistence and content are not the weak parts of this project.
Their problem is not absence.

Their problem is breadth.

You already have enough authored data, enough save participants, and enough cross-system persistence that restore-order and ownership discipline matter a lot more than adding one more feature.
