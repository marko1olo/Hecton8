# ASSET DEPENDENCY MAP — HECTON-8 Hard-Referenced / Addressables Migration

**Date:** 2026-04-29  
**Scope:** first-party world prefabs, ambient audio beds, wreck modules, and first-wave hero prop textures  
**Status:** POPULATED — persistence/addressables paging owners identified

---

## EXECUTIVE SUMMARY

This document is the concrete dependency map for the persistence streaming path.

Current ownership split:
- `ItemCatalog` owns dropped-item world prefab Addressables.
- `PersistentWorldRegistry` pages sector payloads, requests prefab prewarm, then hydrates only after handles are ready.
- `SaveBinaryStorage` owns the v8 indexed sector blocks and sector override commits.

The previous placeholder state is no longer valid.

---

## ADDRESSABLES GROUP PLAN

| Group | Asset Kind | Runtime Owner | Notes |
|---|---|---|---|
| `World_HeroProps` | dropped tool-world prefabs | `ItemCatalog` | prewarmed from sector payload hash IDs before hydration |
| `Audio_Ambient` | ambient loop beds and underwater background layers | audio bootstrap / music director | cold-load only, never sync on sector hydrate |
| `Audio_Music` | scored ambient stems and biome music layers | `HectonMusicDirector` | profile-driven, not part of world item hydration |
| `Wrecks_Modules` | wreck/debris/module landmark prefabs | world streaming / wreck generator | separate from tool drops; future sector-local prewarm target |
| `World_HeroProps_Textures` | first-wave hero prop texture set | art streaming / render residency | seed group for expensive close-range prop textures |

---

## WORLD HERO PROPS — TOOL DROP PREFABS

These are the concrete GUID fallbacks now mirrored in `ItemCatalog`.

| PersistentId | World Prefab | GUID | Group | Path |
|---|---|---|---|---|
| `Item_Tool_BeaconDeployer` | `Item_Tool_BeaconDeployer_World` | `d174d546f879a4742bc018eb043e67b7` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab` |
| `Item_Tool_Builder` | `Item_Tool_Builder_World` | `a9d920f69f572794da38a80172350742` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab` |
| `Item_Tool_EnvAnalyzer` | `Item_Tool_EnvAnalyzer_World` | `f31fbadc22133c74a9c4e0dafbec547e` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab` |
| `Item_Tool_Flashlight` | `Item_Tool_Flashlight_World` | `40a67b632626b2b4ca1b22462448c725` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab` |
| `Item_Tool_HarpoonLauncher` | `Item_Tool_HarpoonLauncher_World` | `2f2aaf08a7039d74ab54a9f41530b73c` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab` |
| `Item_Tool_Knife` | `Item_Tool_Knife_World` | `774f5752cc67c7f49916466b60350a64` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab` |
| `Item_Tool_LaserCutter` | `Item_Tool_LaserCutter_World` | `5d6d90d471f7ea44291faf2907d11145` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab` |
| `Item_Tool_Propulsion` | `Item_Tool_Propulsion_World` | `f9ee01257418ed74696850470ef62d20` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab` |
| `Item_Tool_Repair` | `Item_Tool_Repair_World` | `fd6fc0a78e6568b4e972561e8b888d34` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab` |
| `Item_Tool_SalvageSampler` | `Item_Tool_SalvageSampler_World` | `fa20e563eef211a4daf00fe5b0ca6412` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab` |
| `Item_Tool_Scanner` | `Item_Tool_Scanner_World` | `48435f04343913447adc3ca4573951fc` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab` |
| `Item_Tool_StunPistol` | `Item_Tool_StunPistol_World` | `1cedfa8d3d2816f48afce0afcdbdc9c0` | `World_HeroProps` | `Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab` |

---

## AMBIENT AUDIO BEDS

These are first-wave candidates for `Audio_Ambient` / `Audio_Music` grouping so the persistence path does not force sync loads from scene references.

| Asset | GUID | Group | Path | Notes |
|---|---|---|---|---|
| `Underwater Ambient.wav` | `0d1a03d1d70c9dd448ad1fbab16de520` | `Audio_Ambient` | `Assets/_Project/Audio/Underwater Ambient.wav` | raw underwater bed, imported as 3D |
| `spaceship sounds - ambient.mp3` | `2ede8b6de5633f74f87f3fda9c473b04` | `Audio_Ambient` | `Assets/_Project/Audio/Ambient/spaceship sounds - ambient.mp3` | base industrial loop bed |
| `Atmos 5 Loop.wav` | `8b8df95839a84b047b51962b44b4927b` | `Audio_Ambient` | `Assets/_Project/Audio/Atmos 5 Loop.wav` | long loop layer |
| `ambient_deep_1_Underwater Muffled Silence.ogg` | `2120a917b8f58f5419996f894b1ff0f5` | `Audio_Music` | `Assets/_Project/Audio/Music for Game/ambient_deep_1_Underwater Muffled Silence.ogg` | abyssal biome music stem |

---

## WRECK / MODULE PREFABS

These are the first-wave wreck sector assets that should live outside hard scene references.

| Prefab | GUID | Group | Path | Notes |
|---|---|---|---|---|
| `PFB_Debris_WreckField` | `c055ab313b8da864f821536c0c9e19aa` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab` | dense wreck field landmark |
| `PFB_Module_Foundation` | `3fd47dbaa6552004b99e7b71e2a866d0` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Foundation.prefab` | large structural chunk |
| `PFB_Module_Corridor` | `7693821d1cc09294fbd732cffd8a94ea` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab` | narrow traversal chunk |
| `PFB_Module_Pylon` | `1171decc9d7897e48b74478799d80969` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab` | route marker / support |
| `PFB_Module_ServicePump` | `3df7ebb48b8d74e46801634595e1e864` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab` | service ruin piece |
| `PFB_Module_CurrentTurbine` | `850ede05779e5bd46ac311a1e7a0397c` | `Wrecks_Modules` | `Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab` | turbine ruin set piece |

---

## HERO PROP TEXTURE SEED SET

These are the currently identified high-value close-range texture assets. This is a seed list, not a full authoring sweep.

| Texture | GUID | Group | Path | Notes |
|---|---|---|---|---|
| `Rock012_2K-JPG_AmbientOcclusion.jpg` | `94b963597ba98fb469d6a14460fc30b0` | `World_HeroProps_Textures` | `Assets/_Project/Art/Models/Rocks/Rock 4 - ????????????? ?????/????????????? ????? (????????)/Rock012_2K-JPG_AmbientOcclusion.jpg` | close-range rock AO map |
| `Hecton_WreckIndirectLit.shader` | `1e5b5ec6c08d84e44aa16d5ec269aa05` | `Wrecks_Modules` | `Assets/_Project/Art/Shaders/Hecton_WreckIndirectLit.shader` | not a texture, but shared wreck visual dependency |

---

## RUNTIME RULES

- Sector page-in scans decompressed persistent-world payloads for unique item `hashId` values.
- `ItemCatalog` owns the prefab prewarm queue and the GUID fallback map.
- `PersistentWorldRegistry` must wait until all queued prefab handles are ready before hydrating pooled entities.
- Wreck/module groups are documented here, but no runtime prewarm owner exists for them yet.
- Audio groups are documented here, but they are not part of the `PersistentWorldRegistry` hydration path.

---

## OPEN ITEMS

- Full hero-prop texture sweep is incomplete. Only the first seed assets are mapped here.
- Wreck/module Addressables group assignment is documented, but no runtime sector-local prewarm consumer has been added yet.
- Ambient audio grouping is documented, but the audio bootstrapper still needs the final Addressables ownership pass.
- AUP save surgery remains separate from this asset map. The current save payload still stores compact `AbsoluteUniversePosition` in the metadata prefix.

**STATUS:** POPULATED — first-wave Addressables migration map established.
