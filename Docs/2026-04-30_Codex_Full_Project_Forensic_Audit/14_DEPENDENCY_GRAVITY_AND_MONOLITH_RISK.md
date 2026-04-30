# Dependency Gravity And Monolith Risk

Status: PENDING VERIFICATION

Purpose:
- identify where implementation size and dependency density have become strategic risks

## Largest Owner Gravity

Notable owner mass:
- `HectonMapMagicVegetationBridge` ~15.7k lines
- `WorldProceduralScatterDirector` ~11.7k lines
- `HectonPlayerMovement` ~9.1k lines
- `SuitHUDV4CanvasOverlay` ~5.4k lines
- `FaunaDirector` ~4.6k lines
- `SpatialAudioManager` ~2.5k lines
- `PlayerInventory` ~1.9k lines

Interpretation:
- the project’s most important runtime truth sits in a small set of very large files

## Native Density Risk

Highest native-heavy owners observed in current pass:
- `HectonMapMagicVegetationBridge`
- `HectonVoxelEngine`
- `PlayerCriticalProceduralAudioRenderer`
- `SaveBinaryStorage`
- `SubmarineAtmosphereSystem`
- `SubmarineFluidDynamics`
- `PersistentWorldRegistry`
- `FaunaDirector`
- `PlayerInventory`
- `SpatialAudioManager`

Reading:
- native depth is a strength
- native density also raises the maintenance floor

## Barrier Risk

Owners with notable `.Complete()` pressure:
- `HectonMapMagicVegetationBridge`
- `WorldSpatialHashGrid`
- `SubmarineFluidDynamics`
- `SaveBinaryStorage`
- several world/support utilities

Reading:
- jobs exist
- async benefit is at constant risk of collapsing back into frame synchronization

## Registry Gravity

High `GlobalRegistry` coupling in major owners:
- `HectonPlayerMovement`
- `SuitHUDV4CanvasOverlay`
- `SpatialAudioManager`
- `SaveManager`
- `WorldProceduralScatterDirector`
- `HectonMapMagicVegetationBridge`

Reading:
- registry solved part of the old architecture problem
- it also became the center of gravity for cross-system coupling

## Singleton Residue Gravity

Still visible in important domains:
- `ConstructionManager`
- `WorldStateManager`
- `QuestManager`
- `InputDispatcher`
- `SpatialAudioManager`
- `FaunaDirector` via `.Instance` dependencies

Reading:
- the codebase is not singleton-led anymore
- but it is still singleton-haunted

## UI Gravity

Most teams underestimate UI risk.
This project should not.

Reasons:
- `UI` is one of the largest runtime folders
- `SuitHUDV4CanvasOverlay` is a service owner, not just a panel controller
- `PlayerPDA` carries logic, events, battery policy, and player-blocking semantics

Reading:
- UI is now part of the game’s systems layer
- it should be treated with the same seriousness as world and gameplay

## Practical Monolith Risk Classes

### Class A: Platform Monoliths

Examples:
- `HectonMapMagicVegetationBridge`
- `WorldProceduralScatterDirector`

Risk:
- too much world truth per file

### Class B: Orchestrator Monoliths

Examples:
- `HectonPlayerMovement`
- `SuitHUDV4CanvasOverlay`
- `FaunaDirector`

Risk:
- too many dependencies per owner

### Class C: Stateful Core Monoliths

Examples:
- `SaveManager`
- `SpatialAudioManager`
- `PlayerInventory`

Risk:
- fewer files, but very high correctness cost when touched

## What To Fear More Than Bugs

Not single bugs.

Fear these:
- touching one large owner and invalidating three adjacent systems
- silently reintroducing old singleton assumptions into registry-driven code
- increasing file size instead of splitting authority
- adding more “temporary” ownership to already overloaded classes

## Brutal Summary

The project’s main risk is now structural gravity.

Large owners are no longer just code smell.
They are the real organizational limit on how safely the project can keep growing.
