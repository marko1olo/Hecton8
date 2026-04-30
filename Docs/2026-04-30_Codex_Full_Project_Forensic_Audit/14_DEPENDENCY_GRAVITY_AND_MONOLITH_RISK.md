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

## 2026-04-30 Rechecked Monolith Classes

Current static scan.
For partial owners, the count below is the owner family, not only the root file.

| Owner | Lines | `.Complete()` hits | `.IsCompleted` hits | `GlobalRegistry.` hits | Native surface hits | Assigned class |
|---|---:|---:|---:|---:|---:|---|
| `HectonMapMagicVegetationBridge` family | 15,605 | 12 | 13 | 10 | 547 | Class A: Platform |
| `WorldProceduralScatterDirector` family | 16,873 | 7 | 3 | 11 | 108 | Class B: Orchestrator |
| `HectonPlayerMovement.cs` | 9,148 | 0 | 0 | 15 | 0 | Class C: Stateful Core |

### Late Revalidation Notes

Counting method:
- `HectonMapMagicVegetationBridge` includes `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` and `Assets/_Project/Scripts/World/VegetationMemoryPool.cs`.
- `WorldProceduralScatterDirector` includes 18 partial-family files matching `WorldProceduralScatterDirector`.
- `HectonPlayerMovement` remains a single-file owner.

Important correction:
- the old `WorldProceduralScatterDirector.cs` single-file count understated the actual owner gravity.
- the partial-family view exposes additional synchronous acceptance jobs in `WorldProceduralScatterDirectorCandidateAcceptance.cs`.
- this does not prove a runtime deadlock by itself; it proves a larger barrier audit surface.

### Class A: Platform Monolith - `HectonMapMagicVegetationBridge`

Why this class:
- it is no longer a bridge; it is a world platform layer around MapMagic, vegetation density, threat sampling, artificial structure hashes, HLOD, abyssal navigation, thermal grid sampling, terrain holes, and chunk payload storage.
- it owns double-buffered `NativeParallelMultiHashMap` surfaces for threat chunks and artificial structures (`4351-4380`).
- it participates in dispatcher cadence and origin-shift ownership (`13018-13045`).

Barrier pressure:
- multiple job families are completed through local helper fences: threat propagation (`4459-4462`), flow field (`4646-4649`), thermal grid (`4663-4666`), HLOD cull (`7604-7607`), terrain hole jobs (`13785-13788`), defrag (`11675-11683`), and reader disposal (`12660-12663`).
- many are `IsCompleted` or `forceComplete` gated, but the file still concentrates too many frame/cadence synchronization decisions in one owner.
- additional forced/synchronous-looking completion points still require owner-level review: pending payload job completion (`6949`, `8680`) and local handle completion (`11353`). They may be cold/editor/disposal-safe, but this document does not prove that.

Registry and dependency coupling:
- `GlobalRegistry.Weather` is read for environmental event coupling (`4841`, `4904-4905`).
- `MapMagicBridge.Instance` fallback exists (`12966`).
- this is a third-party-adjacent owner, so regressions can hit terrain, vegetation, nav, world audio density, and hazard-like sampling together.

Risk statement:
- Class A means platform authority. A bad edit here can destabilize multiple world substrates, not one feature.

### Class B: Orchestrator Monolith - `WorldProceduralScatterDirector`

Why this class:
- it implements `ITickable`, `ISlowTickable`, `IUpdatable`, `ISceneBootstrapEventListener`, and `IWorldGenService` in one owner (`23`).
- it registers as the world-generation service (`644`) and dispatcher participant (`1413-1414`).
- it coordinates procedural state registry callbacks (`627`, `10788-10797`), placement registration, prefab registration, candidate maps, acceptance batches, and instancing-service handoff.

Barrier pressure:
- the root sampling completion point is `_samplingJobHandle.Complete()` (`801`).
- surrounding gates at `597` and `836` check `IsCompleted`, but the owner still controls when procedural sampling can block finalization.
- the partial-family scan adds synchronous acceptance jobs in `WorldProceduralScatterDirectorCandidateAcceptance.cs` (`1357`, `1515`, `1688`).
- `WorldProceduralScatterDirectorSamplingPipeline.cs` also contains cold-sync bootstrap/editor-prime completions (`274`, `288`) with explicit comments, so those are not automatically defects, but they are barrier surfaces.

Registry and dependency coupling:
- `GlobalRegistry.WorldGen` defines initialization truth (`31`).
- `GlobalRegistry.RegisterWorldGenService` / `UnregisterWorldGenService` make this owner a service boundary (`644`, `678`, `700`).
- `ActiveRuntimeInstance` and repeated registry callback registration preserve a singleton-like discovery seam even without `DontDestroyOnLoad`.

Risk statement:
- Class B means sequencing authority. The file coordinates many domains but should not keep absorbing new domain state.

### Class C: Stateful Core Monolith - `HectonPlayerMovement`

Why this class:
- it is the player-state core: movement, water state, transport platform binding, KCC sweep scheduling, input cache, inventory load, audio impulses, origin shifts, environmental currents, hull stress, and transport bailout all share one file.
- it implements `IUpdatable`, `IFixedTickable`, and `IOriginShiftListener` (`45`).
- it registers and unregisters through dispatcher and floating-origin listeners (`2555-2560`, `2693-2707`, `2841-2854`).

Barrier pressure:
- direct `.Complete()` hits are zero in this file.
- the real cadence risk is delegated KCC gating: it consumes scheduled capsule sweeps at `6748` and schedules the next batch at `6787`.
- no direct barrier does not mean low risk; it means synchronization pressure is hidden behind `HectonPlayerMotor`.
- current static scan found no direct Jobs/Burst surface in this file, so any player movement deadlock suspicion must inspect `HectonPlayerMotor` and KCC scheduling rather than this class alone.

Registry and dependency coupling:
- `GlobalRegistry.PlayerInventory` is read for encumbrance/tool linkage (`2864-2866`).
- `GlobalRegistry.Input`, `GlobalRegistry.Audio`, `GlobalRegistry.OceanKinematics`, and `GlobalRegistry.PlayerSensory` are used across the owner (`4764`, `4004`, `4696`, `4724`).
- transport binding uses `ITransportPlatform`, `PlayerTransportCoordinator`, and fallback submarine platform logic (`2874-2959`).

Risk statement:
- Class C means correctness gravity. Most edits will not crash immediately; they will change player feel, transport behavior, inventory penalties, audio feedback, or survival coupling in ways that are hard to isolate.

## Practical Monolith Risk Classes

Class definitions for future audits:

- Class A Platform Monolith: owns substrate truth used by several domains; split only with migration plan and hard verification.
- Class B Orchestrator Monolith: sequences many services; extract ownership only when the new owner has a clear lifecycle and no circular bootstrap.
- Class C Stateful Core Monolith: carries central mutable gameplay state; changes require targeted regression cases because bugs become behavior drift.

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
