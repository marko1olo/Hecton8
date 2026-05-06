# GLOBAL REGISTRY DEPENDENCY GRAPH

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-backed dependency orientation for core runtime services
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## 1. Purpose

This file is a narrowed dependency orientation page.
It does not claim exhaustive compile-time graph completeness and does not claim runtime initialization was measured in Unity during this pass.

## 2. Verified Core Dependency Surface

Current source recheck confirmed these core service owners/interfaces in the active registry-contract layer:

- `InputDispatcher` -> `IInputService`
- `PhysicsApplySystem` -> `IPhysicsService`
- `SpatialAudioManager` -> `IAudioService`
- `SceneRuntimeService` -> `ISceneService`
- `SaveManager` -> `ISaveService`
- `SuitHUDV4CanvasOverlay` -> `IUIService`
- `PlayerRuntimeContextService` -> `IPlayerRuntimeContext`
- `PlayerInventoryManager` -> `IPlayerInventoryService`
- `ModularEquipmentEngine` -> `IModularEquipmentService`
- `PlayerSensoryManager` -> `IPlayerSensoryService`
- `EnvironmentRuntimeContextService` -> `IEnvironmentRuntimeContext`
- `GlobalWeatherDirector` -> `IWeatherService`
- `AbyssalThermalManager` -> `IThermodynamicsService`
- `ConstructionManager` -> `ILogisticsService`
- `WorldProceduralScatterDirector` -> `IWorldGenService`
- `HectonDirectorAI` -> `IEncounterDirectorService`
- `QuestManager` -> `IQuestSystem`
- `OceanKinematicsRuntimeService` -> `IHectonOceanKinematicsService`
- `EquipmentInteractionHandler` -> `IInteractionSignalService`
- `DebrisManager` -> `IDebrisService`
- `EcosystemDirector` -> `IEcosystemDirectorService`
- `PowerGridManager` -> `IPowerGridService`
- `SubmarineCoreDirector` -> `ISubmarineRuntimeContext`
- `SubmarineStructuralGrid` -> `ISubmarineHullBreachReadModel`

## 3. Structural Interpretation

Observed dependency style is mixed, not pure:

- `GlobalRegistry` / service-locator access is present
- queue-backed event buses are present
- legacy direct static buses are still present
- direct component/service references are also present

This is not a single clean DI graph.
It is a layered runtime with several communication styles coexisting.

## 4. High-Risk Dependency Areas

### 4.1 Audio

- `SpatialAudioManager` is now the direct `IAudioService` owner in source
- dependent docs that still call audio ownership unresolved are stale
- runtime slot occupancy still needs Unity-side verification

### 4.2 UI

- `SuitHUDV4CanvasOverlay` is now the direct `IUIService` owner in source
- older "fragmented UI owner" language is stale at class-declaration level
- scene/bootstrap dominance is still not proven by static readback alone

### 4.3 Event Architecture

- some first-party buses already use queue-backed late-flush semantics
- other first-party buses still use direct static `Action` fanout
- dependency reasoning that assumes one universal event style will be wrong

### 4.4 Damage / Integrity

- `HabitatIntegrityManager` now implements the global `IDamageReceiver`
- habitat-specific signal contracts still exist beside it
- the risk is no longer a duplicate interface name in current source
- the risk is multi-layer damage semantics spread across packet and callback models

## 5. Dependency Narrative

Representative architectural path inferred from current code:

1. bootstrap/runtime setup establishes registry-facing service owners
2. feature systems query `GlobalRegistry` or subscribe to event surfaces
3. player/world/UI systems exchange state through mixed direct calls, queue buses, and some legacy direct static buses
4. mod-facing signals can also pass through `HectonEventBus`

This is sufficient for orientation, not for claiming deterministic or leak-free startup.

## 6. What Was Removed

Removed from older versions:

- claim that `IAudioService` had no verified implementor
- claim that `IUIService` was structurally fragmented in current pass
- stale dependency language built on those false premises

## 7. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: no runtime sequencing changed
Correctness: improved by replacing stale ownership claims with current source-backed orientation

## 8. Hot Path Impact

None. Markdown-only change.

## 9. Failure Modes

- hidden scene wiring may bypass the dependency picture described here
- registry ownership can drift if code changes without paired doc maintenance
- compile-time relationships outside the rechecked core surfaces are not fully enumerated here

STATUS: PENDING VERIFICATION
