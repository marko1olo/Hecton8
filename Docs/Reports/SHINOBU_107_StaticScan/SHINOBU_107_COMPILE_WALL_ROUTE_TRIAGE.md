# SHINOBU_107 Compile Wall Route Triage

Source report: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Compile_Wall.json`

## Summary

- Critical findings: `71`
- Warning findings: `0`
- Scanner rule: `CORE_SOURCE_DOMAIN_EDGE`
- Patch decision: no C# source patch in this loop.

Reason: after the repeated `ContentRuntimeServices.cs` dead-import cleanup, the remaining rows are live Core route dependencies. Deleting these `using` statements or fully qualified sibling-domain references would break compile or erase a registry/dispatcher route without moving ownership to contracts.

## File Buckets

| Count | File | Route Type |
| ---: | --- | --- |
| 17 | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | global cold service slot references to sibling runtime types |
| 17 | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | dispatcher phase and runtime manager references to sibling runtime types |
| 10 | `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` | contracts file still importing sibling runtime namespaces |
| 5 | `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs` | player runtime snapshot imports from gameplay/audio/inventory/world |
| 5 | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs` | player context service imports from gameplay/audio/inventory/world/environment |
| 3 | `Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs` | AUP math references to World absolute-position types |
| 3 | `Assets/_Project/Scripts/Core/PlayerSensoryManager.cs` | sensory manager imports from audio/environment/gameplay |
| 2 | `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs` | inventory/gameplay route |
| 2 | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs` | construction/gameplay route |
| 2 | `Assets/_Project/Scripts/Core/GlobalSignals.cs` | legacy direct queue alias to World AUP types |
| 1 | `Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs` | inventory namespace marker embedded in Core contracts source |
| 1 | `Assets/_Project/Scripts/Core/Contracts/GroundRadarContracts.cs` | world namespace marker embedded in Core contracts source |
| 1 | `Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs` | physics route |
| 1 | `Assets/_Project/Scripts/Core/InstanceCullingServiceRegistryBridge.cs` | world instance-culling contract route |
| 1 | `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs` | gameplay foveated target route |

## Domain Buckets

| Count | Sibling Domain |
| ---: | --- |
| 12 | `World` |
| 9 | `Gameplay` |
| 8 | `Audio` |
| 7 | `Inventory` |
| 6 | `Environment` |
| 4 | `Construction` |
| 4 | `Physics` |
| 3 | `Systems.AI` |
| 3 | `SaveSystem` |
| 2 | `Visor` |
| 2 | `Optimization` |
| 2 | `Celestial` |
| 2 | `Atmosphere` |
| 2 | `Narrative` |
| 2 | `Quest` |
| 1 | `Power` |
| 1 | `AI` |
| 1 | `VFX` |

## Required Owner Follow-Up

- `GlobalRegistry.cs`: replace concrete sibling runtime slots with interfaces that live in `Hecton8.Core.Contracts` or a neutral `Hecton8.Contracts` assembly. Runtime owners register once during boot; hot consumers read cached contract references.
- `SystemDispatcher.cs`: move domain-specific manager construction and phase registration behind contract adapters. Dispatcher should schedule lanes by contract, not by concrete sibling namespaces.
- `GlobalRegistryContracts.cs`: split true contracts from runtime-domain DTOs. Contracts must not import sibling runtime assemblies.
- `GlobalSignals.cs` and `HectonXRRuntimeState.cs`: retire direct World AUP aliases by moving the required blittable AUP DTO/math surface into a neutral contract package or a documented World.Contracts assembly.
- `Core/Contracts/*`: do not embed sibling namespaces in Core source files. Move those markers/contracts to their owning contract assemblies.

## Rejected Changes

- Rejected deleting live Core `using` directives without replacing type routes. That would break compile and create fake progress.
- Rejected moving `GlobalRegistry` service slots in a static-gate loop. Registry slot migration changes cold dependency injection identity and requires a route card.
- Rejected moving dispatcher phase ownership without domain owner proof. Dispatcher ordering is gameplay authority, not a text cleanup.
- Rejected changing `GlobalSignals` AUP aliases in this loop. The doctrine marks GlobalSignals direct queues as legacy bridge lanes; retirement needs a bridge route card and consumer migration.

## Proof Notes

The current compile-wall report is all `CORE_SOURCE_DOMAIN_EDGE` rows. `ContentRuntimeServices.cs` has no remaining `Hecton8.Optimization` token after Loop 393. The remaining 71 rows are broad route migrations, not dead imports.
