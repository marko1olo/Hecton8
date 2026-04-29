# HECTON-8 GLOBALREGISTRY RUNTIME AUTHORITY MATRIX

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: current source-backed truth for `GlobalRegistry` slots, runtime owners, publishers, and bootstrap fallback coverage
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Purpose

The active docset had interface truth and several subsystem maps, but it still lacked one detailed actual-report page dedicated to the runtime authority surface exposed through `GlobalRegistry`.

This file answers:

1. What service surface `GlobalRegistry` currently exposes.
2. Which concrete first-party owners publish into that surface.
3. Which runtime owners are service-only versus compatibility/runtime-owner slots.
4. Where bootstrap manually backfills registry coverage.

## Primary Evidence

- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- direct publisher files across `Assets/_Project/Scripts`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

## Surface Size

Confirmed contract surface in `GlobalRegistryContracts.cs`:

- `27` public interfaces

Confirmed typed interface slots in `GlobalRegistry.cs`:

- `24` interface service slots

Confirmed additional runtime-owner compatibility slots in `GlobalRegistry.cs`:

- `ObjectPoolManager`
- `HectonFluidEngine`
- `AbyssalThermalManager`
- `HectonNarrativeDirector`
- `QuestManager`
- `GameTickManager`
- `SystemDispatcher`
- `RenderDispatcher`
- `GlobalPhysicsStateManager`

This means current `GlobalRegistry` is not only a service interface locator.
It is also a mixed interface/runtime-owner access surface.

## Contract Inventory

Confirmed interface list in `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`:

| Line | Contract |
|---|---|
| `377` | `IInputService` |
| `458` | `IPhysicsService` |
| `505` | `IAudioService` |
| `565` | `ISceneService` |
| `588` | `ISaveService` |
| `634` | `IUIService` |
| `645` | `IPlayerRuntimeContext` |
| `737` | `IPlayerInventoryService` |
| `769` | `IModularEquipmentService` |
| `831` | `IPlayerSensoryService` |
| `872` | `IEnvironmentRuntimeContext` |
| `898` | `IWeatherService` |
| `934` | `IThermodynamicsService` |
| `954` | `ILogisticsService` |
| `988` | `IWorldGenService` |
| `1015` | `IEncounterDirectorService` |
| `1056` | `IQuestSystem` |
| `1100` | `IHectonOceanKinematicsService` |
| `1116` | `IInteractionSignalService` |
| `1153` | `IDebrisService` |
| `1230` | `IEcosystemDirectorService` |

Non-service but still architecturally important contracts in the same file:

| Line | Contract |
|---|---|
| `38` | `IUpdatable` |
| `51` | `ILateFrameTickable` |
| `63` | `IPostFixedTickable` |
| `75` | `IRenderable` |
| `157` | `IDamageReceiver` |
| `169` | `IDebrisDefinition` |

## Interface Slot Matrix

Confirmed slot declarations in `Assets/_Project/Scripts/Core/GlobalRegistry.cs:27-51`.

| Slot | Concrete publisher | Publication evidence | Current notes |
|---|---|---|---|
| `Input` | `InputDispatcher` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:128`, `146` | direct service owner, dual registration path in `Initialize` and `OnEnable` |
| `Physics` | `PhysicsApplySystem` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:280` | direct service owner |
| `Audio` | `SpatialAudioManager` | `Assets/_Project/Scripts/SpatialAudioManager.cs:380` | direct service owner |
| `Scene` | `SceneRuntimeService` | `Assets/_Project/Scripts/Core/SceneRuntimeService.cs:254` | direct service owner |
| `Save` | `SaveManager` | `Assets/_Project/Scripts/SaveManager.cs:273` | direct service owner |
| `UI` | `SuitHUDV4CanvasOverlay` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:849` | direct service owner |
| `Player` | `PlayerRuntimeContextService` | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:598` | canonical player runtime read model |
| `PlayerInventory` | `PlayerInventoryManager` | `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs:205` | registry-facing tooling/inventory mirror |
| `ModularEquipment` | `ModularEquipmentEngine` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:535` | compiled runtime tool-state owner |
| `PlayerSensory` | `PlayerSensoryManager` | `Assets/_Project/Scripts/Core/PlayerSensoryManager.cs:226` | registry-facing sensory/presentation mirror |
| `Environment` | `EnvironmentRuntimeContextService` | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs:209` | environment context owner |
| `Weather` | `GlobalWeatherDirector` | `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs:282` | direct service owner |
| `OceanKinematics` | `OceanKinematicsRuntimeService` | `Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs:235` | provider selector owner |
| `PowerGrid` | `PowerGridManager` | `Assets/_Project/Scripts/PowerGridManager.cs:383` | direct service owner |
| `Submarine` | `SubmarineCoreDirector` | `Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs:121` | submarine runtime root |
| `SubmarineHullBreach` | `SubmarineStructuralGrid` | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:273` | hull-breach read model |
| `InteractionSignals` | `EquipmentInteractionHandler` | `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs:86` | queued signal + raycast owner |
| `Debris` | `DebrisManager` | `Assets/_Project/Scripts/Gameplay/DebrisManager.cs:109` | direct service owner |
| `EcosystemDirector` | `EcosystemDirector` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:937` | direct service owner |
| `ThermodynamicsService` | `AbyssalThermalManager` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2317` via `RegisterThermodynamicsRuntime(this)` | service published indirectly through runtime registration |
| `Logistics` | `ConstructionManager` | `Assets/_Project/Scripts/ConstructionManager.cs:745` | direct logistics/build-network owner |
| `WorldGen` | `WorldProceduralScatterDirector` | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:643` | direct service owner |
| `EncounterDirector` | `HectonDirectorAI` | `Assets/_Project/Scripts/HectonDirectorAI.cs:130` | direct service owner |
| `QuestSystem` | `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:77` | published via `RegisterQuestRuntime(this)` |

## Runtime-Owner Compatibility Matrix

The registry still exposes compatibility/runtime-owner surfaces beyond pure interfaces.

Confirmed runtime-owner slots:

| Runtime slot | Evidence | Notes |
|---|---|---|
| `SaveRuntime` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:90` | compatibility cast back to concrete `SaveManager` |
| `ObjectPool` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:100`, `391` | concrete pool owner slot |
| `Fluid` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:196`, `560` | concrete `HectonFluidEngine` runtime slot |
| `Thermodynamics` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:201`, `568` | concrete `AbyssalThermalManager` runtime slot |
| `NarrativeDirector` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:206`, `579` | concrete narrative runtime slot |
| `Quest` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:211`, `587` | concrete `QuestManager` runtime slot |
| `TickManager` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:216`, `284` | core tick owner slot |
| `Dispatcher` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:221`, `293` | gameplay dispatcher owner slot |
| `RenderDispatcher` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:226`, `302` | render dispatcher owner slot |
| `PhysicsStateManager` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:231`, `311` | global physics-state owner slot |

Current interpretation:

- this is a mixed migration state
- some systems are properly read through interfaces
- some compatibility and orchestration still depend on concrete-owner access

## Bootstrap Backfill Coverage

One of the most important current truths is that bootstrap still manually backfills several slots if direct owner registration did not happen soon enough.

Confirmed bootstrap backfill paths in `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`:

| Source line | Coverage path |
|---|---|
| `580-584` | bootstrap explicitly runs registry coverage checks after major init |
| `587-594` | thermodynamics coverage uses `FindAnyObjectByType<AbyssalThermalManager>()` then `RegisterThermodynamicsRuntime(manager)` |
| `596-605` | logistics coverage uses `FindAnyObjectByType<ConstructionManager>()` then `RegisterLogisticsService(manager)` |
| `607-615` | worldgen coverage uses `FindAnyObjectByType<WorldProceduralScatterDirector>()` then `RegisterWorldGenService(director)` |
| `617-625` | encounter coverage uses `FindAnyObjectByType<HectonDirectorAI>()` then `RegisterEncounterDirectorService(director)` |
| `627-635` | quest coverage uses `FindAnyObjectByType<QuestManager>()` then `RegisterQuestRuntime(questManager)` |

This is a real architectural signal.
It means the project still relies on bootstrap backstop logic for some registry slots.

## Known Multi-Path Registration Cases

### Input

`InputDispatcher` currently registers through more than one path:

| Source line | Behavior |
|---|---|
| `Assets/_Project/Scripts/Core/InputDispatcher.cs:128` | registration during initialize path |
| `Assets/_Project/Scripts/Core/InputDispatcher.cs:146` | registration again on enable when initialized |

This may be benign if lifecycle assumptions hold, but it is a detail that must be documented, not hand-waved.

### Thermodynamics

`AbyssalThermalManager` registers through runtime wrapper path rather than only direct interface publication:

| Source line | Behavior |
|---|---|
| `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:2317` | calls `GlobalRegistry.RegisterThermodynamicsRuntime(this)` |
| `Assets/_Project/Scripts/Core/GlobalRegistry.cs:568-573` | runtime registration also fans into `RegisterThermodynamicsService(thermodynamicsService)` when applicable |

This is functionally coherent, but it is not a simple one-call one-slot story.

### Quest

`QuestManager` also publishes through runtime wrapper path:

| Source line | Behavior |
|---|---|
| `Assets/_Project/Scripts/Quest/QuestManager.cs:77` | calls `GlobalRegistry.RegisterQuestRuntime(this)` |
| `Assets/_Project/Scripts/Core/GlobalRegistry.cs:587+` | runtime registration also fans into interface slot registration when applicable |

## Non-Service Registry Buckets

`GlobalRegistry` is not only singleton slots.
It still owns dense multi-instance registries:

| Bucket | Evidence |
|---|---|
| `_updatables` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:17` |
| `_renderables` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:19` |
| `_fixedTickables` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:20` |
| `_slowTickables` | `Assets/_Project/Scripts/Core/GlobalRegistry.cs:21` |

This matters because the registry is both:

- service locator
- dense lifecycle/dispatch registration surface

Older docs that describe it as only one of those are incomplete.

## What Looks Good

- service surface is explicit and large enough to matter
- concrete publishers are now known for the critical slots
- several major gameplay domains are already behind interfaces instead of raw scene searches
- thermodynamics, quest, submarine, ecology, and worldgen are all part of the same visible runtime authority surface

## What Looks Merely Acceptable

- the registry still mixes clean interface slots with compatibility/concrete-owner slots
- some services rely on runtime wrapper registration rather than one obvious direct publish call
- bootstrap fallback coverage improves resilience but also proves some slots are not trusted to self-stabilize cleanly

## What Looks Weak

- bootstrap backfill still uses `FindAnyObjectByType` for some registry coverage
- authority is explicit, but not minimal
- the registry is still in a mixed migration state rather than a fully purified interface-only shape

## Failure Modes To Watch

- owner class can exist but fail to publish, leaving bootstrap fallback to paper over deeper lifecycle problems
- duplicate registration paths can become brittle during refactors
- concrete runtime-owner slots can keep legacy coupling alive longer than intended

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves runtime-authority readability and makes bootstrap fallback behavior explicit instead of implicit. |

## Verdict

Current `GlobalRegistry` truth is now much clearer:

- `27` public contracts exist
- `24` interface service slots are active
- multiple concrete runtime-owner slots still coexist for compatibility
- direct publishers are known for the major gameplay/runtime services
- bootstrap still backfills several slots through explicit coverage methods

This is a stronger and more honest picture than older docs had.
It is still not a claim that the runtime authority layer is finished or fully consolidated.

STATUS: PENDING VERIFICATION
