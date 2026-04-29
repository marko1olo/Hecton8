# HECTON-8 INTERFACE HEALTH DASHBOARD

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Source Basis:** `GlobalRegistryContracts.cs` + direct first-party class declaration readback in `Assets/_Project/Scripts`

---

## Executive Summary

| Metric | Count |
|---|---:|
| Total interfaces in `GlobalRegistryContracts.cs` | 19 |
| Structurally live | 15 |
| Ghost | 1 |
| Conflicting | 1 |
| Partial / fragmented | 2 |

**Current debt tally:** `IAudioService` ghost + `IDamageReceiver` conflict + `IRenderable` thin usage + `IUIService` fragmented ownership.

---

## Inventory

| # | Interface | Verified implementor(s) | State | Comment |
|---|---|---|---|---|
| 1 | `IUpdatable` | Many | LIVE | Core dispatcher contract |
| 2 | `IRenderable` | `HectonUnderwaterVisuals` | PARTIAL | Single known implementor |
| 3 | `IDamageReceiver` | `HabitatIntegrityManager` | CONFLICTING | Canonical contract plus shadow/nested definition |
| 4 | `IDebrisDefinition` | `OrganicDebrisProfile` | LIVE | Present and used |
| 5 | `IInputService` | `InputDispatcher` | LIVE | Service owner found |
| 6 | `IPhysicsService` | `PhysicsApplySystem` | LIVE | Service owner found |
| 7 | `IAudioService` | None found | GHOST | Registry contract exists without implementor |
| 8 | `ISceneService` | `SceneRuntimeService` | LIVE | Service owner found |
| 9 | `ISaveService` | `SaveManager` | LIVE | Service owner found |
| 10 | `IUIService` | `HectonFabricatorUI`, `HectonSuitHUD_v4`, `SuitHUDV4CanvasOverlay` | FRAGMENTED | More than one owner candidate |
| 11 | `IPlayerRuntimeContext` | `PlayerRuntimeContextService` | LIVE | Service owner found |
| 12 | `IPlayerInventoryService` | `PlayerInventoryManager` | LIVE | Service owner found |
| 13 | `IPlayerSensoryService` | `PlayerSensoryManager` | LIVE | Service owner found |
| 14 | `IEnvironmentRuntimeContext` | `EnvironmentRuntimeContextService` | LIVE | Service owner found |
| 15 | `IWeatherService` | `GlobalWeatherDirector` | LIVE | Service owner found |
| 16 | `IHectonOceanKinematicsService` | `OceanKinematicsRuntimeService` | LIVE | Service owner found |
| 17 | `IInteractionSignalService` | `EquipmentInteractionHandler` | LIVE | Service owner found |
| 18 | `IDebrisService` | `DebrisManager` | LIVE | Service owner found |
| 19 | `IEcosystemDirectorService` | `EcosystemDirector` | LIVE | Service owner found |

---

## Corrections Applied Against The Previous Version

| Previous claim | Verified correction |
|---|---|
| `IRenderable` had 0 implementors | False. `HectonUnderwaterVisuals` implements `IRenderable`. |
| `IWeatherService` owner was `HectonAtmosphereManager` | False. Verified implementor is `GlobalWeatherDirector`. |
| `IHectonOceanKinematicsService` owner was `CrestOceanBridge or equivalent` | Too vague. Verified implementor is `OceanKinematicsRuntimeService`. |
| `IInteractionSignalService` owner was `InteractionSignalRouter` | False. Verified implementor is `EquipmentInteractionHandler`. |
| `IDebrisService` owner was `DebrisBurstManager` | False. Verified implementor is `DebrisManager`. |
| `IEcosystemDirectorService` owner was `FaunaDirector` / `EcosystemSimulator` | False. Verified implementor is `EcosystemDirector`. |
| `IPlayerRuntimeContext` owner was `PlayerRuntimeContext` | False. Verified implementor is `PlayerRuntimeContextService`. |
| `Conflicting interfaces = 2` | Not supported by current code readback in this pass. Confirmed conflict count is 1: `IDamageReceiver`. |

---

## Primary Findings

### `IAudioService`

- `GlobalRegistry` exposes audio registration methods.
- No first-party `IAudioService` implementor was found.
- This is the only confirmed ghost interface in the current contract file.

### `IUIService`

- Three first-party classes implement it.
- The problem is not absence.
- The problem is ambiguous ownership: the contract implies one authoritative UI root, but the code exposes multiple candidates.

### `IRenderable`

- Thin contract, not dead contract.
- Any report calling it a ghost interface is stale.

### `IDamageReceiver`

- Highest semantic-risk interface in the current set.
- Canonical definition exists, but ownership remains muddy because of a shadow/nested definition context in `HabitatIntegrityManager.cs`.

---

## Recommended Actions

| Priority | Action | Reason |
|---|---|---|
| P0 | Decide whether `SpatialAudioManager` should implement `IAudioService` or the interface should be deleted | Registry contract currently has no owner |
| P0 | Collapse `IDamageReceiver` onto one canonical definition | Removes routing ambiguity |
| P1 | Define one authoritative `IUIService` root and demote other UI implementors to subcontrollers | Removes ambiguous registry ownership |
| P2 | Decide whether `IRenderable` should stay as a single-owner contract or be folded into another render hook | Prevents future stale-doc drift |
