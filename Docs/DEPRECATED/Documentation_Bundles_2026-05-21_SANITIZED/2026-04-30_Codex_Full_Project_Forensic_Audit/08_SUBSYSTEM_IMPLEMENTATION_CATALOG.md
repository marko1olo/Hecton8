# Subsystem Implementation Catalog

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


Purpose:
- describe which project domains are materially implemented
- separate heavy runtime domains from thin or mostly-supporting zones
- attach readiness judgment to actual code mass and ownership shape

## Static Runtime Mass

Primary first-party runtime-heavy folders by source size:

| Folder | Historical / R18 Static Size | Current reading |
|---|---:|---|
| `World` | historical `108 / 64,476`; R18 `183 / 125,062` | dominant simulation/content/runtime infrastructure surface |
| `UI` | historical `78 / 36,339`; R18 `115 / 79,632` | large user-facing runtime and presentation logic layer |
| `Gameplay` | historical `104 / 33,995`; R18 `141 / 75,206` | large player/tool/interaction/game-state core |
| `Core` | historical `33 / 9,568`; R18 `180 / 83,921` | real shared runtime backbone |
| `Construction` | historical `24 / 8,932` | substantial system, not placeholder |
| `Visor` | historical `19 / 8,217` | real dedicated feature lane, not cosmetic garnish |
| `Audio` | 13 | 7,874 | custom runtime depth is real |
| `Fauna` | 15 | 7,759 | meaningful simulation layer |
| `Optimization` | 22 | 4,294 | active governance/tooling layer |
| `Atmosphere` | 5 | 3,370 | non-trivial environment state lane |

Interpretation:
- the project is world-first, then player/UI-heavy
- this is not a menu shell with a thin world
- it is also not a cleanly compressed runtime

## Runtime Domain Catalog

### Core Runtime Backbone

Evidence:
- `GlobalRegistry`
- `SystemDispatcher`
- `GameTickManager`
- `ThreadSafeCommandQueue`
- runtime context services

Strength:
- real architecture backbone exists

Weakness:
- service authority is still mixed with singleton residue

Readiness:
- 72%

### Bootstrap / Scene Handoff

Evidence:
- `BootstrapController`
- `GameBootstrapper`
- `SceneBootstrap`
- separate Archivarius bootstrap truth doc already confirms split authority

Strength:
- startup is not accidental; there is explicit orchestration

Weakness:
- one bootstrap owner does not exist

Readiness:
- 45%

### World Simulation / Streaming / Procedural Surface

Evidence:
- `World` folder dominates the project by source size
- `HectonMapMagicVegetationBridge`
- `WorldProceduralScatterDirector`
- `WorldProceduralFieldSampler`
- `WorldStateManager`
- `WorldProceduralStateRegistry`

Strength:
- real runtime ownership and deep procedural ambition

Weakness:
- monolith size is now a major regression surface
- jobs/native density is high, but discipline is uneven

Readiness:
- 61%

### Gameplay / Player Runtime Core

Evidence:
- `HectonPlayerMovement`
- tool stack
- survival, scan, beacon, builder, sampler, cutter, propulsion, repair

Strength:
- real systemic gameplay implementation exists

Weakness:
- large dependency sprawl
- too many gameplay owners still bridge into older singleton assumptions

Readiness:
- 56%

### UI / PDA / HUD

Evidence:
- `UI` is the second-largest runtime folder by line count
- `SuitHUDV4CanvasOverlay`
- `PlayerPDA`
- `HectonFabricatorUI`
- multiple dedicated tabs and registries

Strength:
- this is a real product-facing layer, not temporary debug UI only
- HUD path already shows deliberate zero-GC work

Weakness:
- UI is large enough to be its own platform and still carries many ticked owners
- `SetActive`-style old-school patterns likely still exist in places

Readiness:
- 79%

### Construction / Base Modules / Logistics

Evidence:
- `ConstructionManager`
- module templates/catalogs
- logistics graph and service interfaces
- ambient accident logic, persistence, module registry

Strength:
- system is materially implemented and integrated with save/runtime service surfaces

Weakness:
- still mixed-authority with singleton ownership
- late-save and runtime complexity raise integration cost

Readiness:
- 63%

### Inventory / Items

Evidence:
- `PlayerInventory`
- native SOA-backed owner comments match actual code shape
- item data and item catalog exist

Strength:
- inventory is a real engineered subsystem, not a `List<ItemStack>` prototype

Weakness:
- small folder count hides high centrality
- player inventory sits on a lot of downstream dependencies

Readiness:
- 74%

### Save / Persistence

Evidence:
- `SaveManager`
- `SaveBinaryStorage`
- `SaveBinaryPayloadCodec`
- `SaveDataMigration`
- broad `ISaveable` participant ledger already exists in Archivarius docs

Strength:
- large real save graph exists
- binary/native persistence intent is serious

Weakness:
- save graph breadth multiplies restore-order risk
- current evidence is mostly source truth, not broad live replay truth

Readiness:
- 76%

### Audio / Spatial / Procedural DSP

Evidence:
- `SpatialAudioManager`
- `PlayerCriticalProceduralAudioRenderer`
- multiple audio managers and runtime owners

Strength:
- custom audio engineering depth is real

Weakness:
- audio still coexists with some classic service-ownership residue
- complex native surface means regression risk is high

Readiness:
- 73%

### Fauna / Ecosystem

Evidence:
- `FaunaDirector`
- fauna cognition domains
- genetics/ecosystem health/resource scarcity neighbors

Strength:
- not fake; there is actual simulation and persistence

Weakness:
- ecology/fauna is spread across several neighboring domains and can drift in ownership

Readiness:
- 58%

### Quest / Narrative / Progression

Evidence:
- `QuestManager`
- `QuestStateManager`
- `LoreDatabaseManager`
- `AtlasSignalSystem`
- `FirstHourDirector`

Strength:
- narrative and quest persistence are much more real than a typical prototype

Weakness:
- some of this layer restores early and likely has hidden dependency edges

Readiness:
- 67%

### Visor / Scanner / Spectrum

Evidence:
- `Visor` folder is large
- `SpectrumSystem`
- scan/sonar/HUD linkage exists across gameplay and UI

Strength:
- feature lane is distinct and substantial

Weakness:
- split across UI, gameplay, visor, and spatial query surfaces

Readiness:
- 68%

## Domains That Are Real But Operationally Thin

These are implemented, but they do not dominate project reality:
- `Economy`
- `Ecosystem`
- `Meta`
- `PDA`
- `Power`
- `Progression`
- `Narrative`

Interpretation:
- these are not fake folders
- they are secondary or supporting verticals, not the current mass center

## Domains That Are Mostly Tooling / Validation / Support

- `Dev`
- `Editor`
- `BuildTools`
- `Optimization`

Interpretation:
- the project has a very large self-observation/tooling surface
- that is useful, but it also indicates the runtime has become hard enough to need many bespoke validators

## Brutal Reading

The project already contains many real systems.
Its problem is not lack of implementation.

Its problem is that several of the biggest domains are now so large, so connected, and so stateful that they are expensive to trust.
