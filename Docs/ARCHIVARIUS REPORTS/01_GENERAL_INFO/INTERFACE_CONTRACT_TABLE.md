# INTERFACE CONTRACT TABLE

Date: 2026-05-11
Status: PENDING VERIFICATION
Source basis: `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` plus direct first-party class declarations under `Assets/_Project/Scripts`; May 11 focused recheck of `IDamageReceiver`
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Interface -> Implementor Table

| Interface | Implementor(s) found | Classification | Notes |
|---|---|---|---|
| `IUpdatable` | Many first-party systems | LIVE | Base dispatcher contract |
| `ILateFrameTickable` | Multiple first-party systems | LIVE | End-of-frame swap-window contract |
| `IPostFixedTickable` | Multiple first-party systems | LIVE | Post-fixed deferred contract |
| `IRenderable` | `HectonUnderwaterVisuals`, `HectonSubmarineOS`, `MissionMarkerSystem` | LIVE | Real render-dispatch hook |
| `IDamageReceiver` | `HabitatIntegrityManager`, `HectonPlayerHealth` | LIVE | Global damage packet receivers present; both register transient runtime target ids through `CombatDamageRuntime` where active |
| `IDebrisDefinition` | `OrganicDebrisProfile` | LIVE | Debris definition owner |
| `IInputService` | `InputDispatcher` | LIVE | Registry-backed input owner |
| `IPhysicsService` | `PhysicsApplySystem` | LIVE | Force-routing owner |
| `IAudioService` | `SpatialAudioManager` | LIVE | Old ghost claim is stale |
| `ISceneService` | `SceneRuntimeService` | LIVE | Scene-transition owner |
| `ISaveService` | `SaveManager` | LIVE | Save-system owner |
| `IUIService` | `SuitHUDV4CanvasOverlay` | LIVE | Single direct implementor in current source scan |
| `IPlayerRuntimeContext` | `PlayerRuntimeContextService` | LIVE | Runtime player-context owner |
| `IPlayerInventoryService` | `PlayerInventoryManager` | LIVE | Inventory/tool owner |
| `IModularEquipmentService` | `ModularEquipmentEngine` | LIVE | Equipment owner |
| `IPlayerSensoryService` | `PlayerSensoryManager` | LIVE | Sensory/presentation owner |
| `IEnvironmentRuntimeContext` | `EnvironmentRuntimeContextService` | LIVE | Environment runtime owner |
| `IWeatherService` | `GlobalWeatherDirector` | LIVE | Weather owner |
| `IThermodynamicsService` | `AbyssalThermalManager` | LIVE | Thermodynamics owner |
| `ILogisticsService` | `ConstructionManager` | LIVE | Logistics owner |
| `IWorldGenService` | `WorldProceduralScatterDirector` | LIVE | World-generation owner |
| `IEncounterDirectorService` | `HectonDirectorAI` | LIVE | Encounter-direction owner |
| `IQuestSystem` | `QuestManager` | LIVE | Quest-system owner |
| `IHectonOceanKinematicsService` | `OceanKinematicsRuntimeService` | LIVE | Ocean-provider selector owner |
| `IInteractionSignalService` | `EquipmentInteractionHandler` | LIVE | Queued interaction owner |
| `IDebrisService` | `DebrisManager` | LIVE | Debris runtime owner |
| `IEcosystemDirectorService` | `EcosystemDirector` | LIVE | Ecosystem-sector owner |

## Corrections Applied

| Previous claim | Current verified state |
|---|---|
| `IAudioService` had no first-party implementor | False. `SpatialAudioManager` implements it. |
| `IUIService` had multiple direct implementors | False in current source scan. Direct implementor found: `SuitHUDV4CanvasOverlay`. |
| `IDamageReceiver` was shadow-conflicted by `HabitatIntegrityManager` | False in current source. Habitat and player health now implement the global contract directly; habitat-local callbacks remain separate `IDamageSignalReceiver` / `IDamageSignalEmitter` contracts. |
| `IRenderable` had only one live implementor | False. Three direct implementors were rechecked in this pass. |

## Verification Boundary

This table records class-level ownership only.
It does not prove:

- scene presence
- bootstrap success
- registration order
- runtime slot occupancy after scene transitions

STATUS: PENDING VERIFICATION
