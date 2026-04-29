# INTERFACE CONTRACT TABLE

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Source Basis:** `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` + first-party class declarations under `Assets/_Project/Scripts`

---

## Interface -> Implementor Table

| Interface | Implementor(s) found | Classification | Notes |
|---|---|---|---|
| `IUpdatable` | Many first-party systems | LIVE | Base dispatcher contract |
| `IRenderable` | `HectonUnderwaterVisuals` | PARTIAL | Single live implementor, not ghost |
| `IDamageReceiver` | `HabitatIntegrityManager` | CONFLICTING | Canonical contract exists, but a shadow/nested definition also exists in `HabitatIntegrityManager.cs` |
| `IDebrisDefinition` | `OrganicDebrisProfile` | LIVE | Authoring/runtime debris definition contract |
| `IInputService` | `InputDispatcher` | LIVE | Registry-backed input owner |
| `IPhysicsService` | `PhysicsApplySystem` | LIVE | Force-routing service |
| `IAudioService` | None found | GHOST | Registry slot exists, no first-party implementor found |
| `ISceneService` | `SceneRuntimeService` | LIVE | Guarded scene-transition service |
| `ISaveService` | `SaveManager` | LIVE | Save/load service |
| `IUIService` | `HectonFabricatorUI`, `HectonSuitHUD_v4`, `SuitHUDV4CanvasOverlay` | FRAGMENTED | More than one owner candidate |
| `IPlayerRuntimeContext` | `PlayerRuntimeContextService` | LIVE | Runtime player context service |
| `IPlayerInventoryService` | `PlayerInventoryManager` | LIVE | Inventory/tool context |
| `IPlayerSensoryService` | `PlayerSensoryManager` | LIVE | Camera/audio/visor context |
| `IEnvironmentRuntimeContext` | `EnvironmentRuntimeContextService` | LIVE | Construction/hazard context |
| `IWeatherService` | `GlobalWeatherDirector` | LIVE | Weather snapshot owner |
| `IHectonOceanKinematicsService` | `OceanKinematicsRuntimeService` | LIVE | Ocean-provider selector |
| `IInteractionSignalService` | `EquipmentInteractionHandler` | LIVE | Queued interaction service |
| `IDebrisService` | `DebrisManager` | LIVE | Debris burst runtime service |
| `IEcosystemDirectorService` | `EcosystemDirector` | LIVE | Sector population service |

---

## High-Risk Corrections

| Previous claim | Current verified state |
|---|---|
| `IRenderable` ghost | False. `HectonUnderwaterVisuals` implements it. |
| `IAudioService` implemented by `SpatialAudioManager` | False. No `IAudioService` implementor found. |
| `IPlayerRuntimeContext` implemented by `PlayerRuntimeContext` | False. Implementor found is `PlayerRuntimeContextService`. |
| `IWeatherService` implemented by `HectonAtmosphereManager` | False. Implementor found is `GlobalWeatherDirector`. |
| `IEcosystemDirectorService` implemented by `FaunaDirector` / `EcosystemSimulator` | False. Implementor found is `EcosystemDirector`. |

---

## Verification Boundary

This file records structural code ownership only.  
It does not prove scene presence, bootstrap ordering, or runtime registration success.
