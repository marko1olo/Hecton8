# HECTON-8 CONSTRUCTION RUNTIME INTEGRATION MAP

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-backed map of current construction, habitat, logistics, and power integration
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this map as current project truth.
- This file maps construction/base ownership; it does not prove long-session module stability, save/load restoration, or native-buffer lifetime safety.
- `HabitatGraphManager`, `BaseModule`, `BaseAirlock`, and logistics graph ownership remain active review surfaces for memory lifecycle and authority boundaries.

## Purpose

Older docs had surgery logs and narrow remediations for equipment and habitat systems, but no broad active map tying construction, module templates, topology rebuilds, save/load, and power together.

This file closes that gap.

## Proof Boundary

This map is based on direct reads of:

- `Assets/_Project/Scripts/ConstructionManager.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/BaseModuleTemplate.cs`
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- `Assets/_Project/Scripts/PowerGridManager.cs`

This is not proof that:

- construction behaves correctly in a long play session
- all spawned modules survive scene transitions
- save/load restore every module edge case without live runtime verification

## Current Ownership Split

The construction stack is currently divided into five primary owners:

1. `ConstructionManager` = orchestration, persistence, and logistics service publication
2. `BaseModuleTemplate` = immutable authored module definition
3. `BaseModule` = placed runtime module instance
4. `HabitatGraphManager` = topology extraction and CSR/logistics graph rebuild
5. `PowerGridManager` = global power propagation and brownout distribution

That split is coherent.
It is also more mature than older docs implied.

## 1. ConstructionManager

`Assets/_Project/Scripts/ConstructionManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `38` | class is `ConstructionManager : MonoBehaviour, IUpdatable, ISaveable, ISlowTickable, ILogisticsService` |
| `169-170` | registers with save runtime on enable |
| `177` | unregisters from save runtime on disable |
| `372` | `SavePriority => 90` |
| `373` | `LoadPriority => 90` |
| `388` | implements `PopulateSaveData(...)` |
| `497` | implements `LoadFromSaveData(...)` |
| `742` | publishes through `GlobalRegistry.RegisterLogisticsService(this)` |

Operational role:

- tracks placed modules
- coordinates spawn/despawn and restoration
- persists construction state
- bridges runtime construction with logistics service access
- rebuilds topology after load or structural changes

This is the real construction orchestrator.
It is not only a build-placement helper.

## 2. BaseModuleTemplate

`Assets/_Project/Scripts/BaseModuleTemplate.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `18` | `CreateAssetMenu` marks this as an authored module template asset |
| `55` | stable string id exists |
| `58` | stable hash id exists |
| `62` | owns `snapPoints` |
| `66` | owns `buildCost` |
| `70` | owns `powerDrawKW` |
| `73` | owns `airVolumeM3` |
| `87` | owns `vfxSockets` |

This is the immutable authored definition for module families.
It currently carries:

- persistent template identity
- build costs
- spatial snap definitions
- power draw
- air-volume defaults
- degradation/VFX metadata

This is a healthy split.
Authored definition is not fused into runtime save state.

## 3. BaseModule

`Assets/_Project/Scripts/BaseModule.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `85` | class is `BaseModule : MonoBehaviour, IPowerComponent, IPoolable, ISlowTickable, ICuttable` |
| `131` | carries `BaseModuleTemplate` reference |
| `375` | exposes `PowerRating` |
| `377` | exposes `PowerPriority` |
| `379` | exposes `HasPower` |
| `386` | implements `OnPowerStatusChanged(bool)` |
| `772+` | overload set for `SetState(...)` used during restoration |

Runtime role:

- placed module instance
- power participant
- cut/damage participant
- pooled runtime object
- slow-tick participant
- save-restored state receiver

This is a dense runtime owner, but the responsibilities are still domain-coherent around a placed module.

## 4. HabitatGraphManager

`Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `15` | comment explicitly limits ownership to base-module topology; crate pipes stay under `LogisticsPipeNode` |
| `31-37` | owns socket/module buffers and adjacency lookup maps |
| `39` | owns `NativeArray<LogisticsNetworkGraph.LogisticsNode>` |
| `45` | owns `LogisticsNetworkGraph` |
| `88` | `Rebuild(IReadOnlyList<GameObject> modules)` is the central rebuild path |
| `229-264` | indexes sockets and assembles adjacency through quantized socket matching |
| `597` | allocates persistent node storage for graph rebuild output |

This is not a UI helper or convenience class.
It is the topology compiler for placed habitat.

Its job is to translate scene/module objects into:

- node list
- edge list
- socket compatibility matches
- node capacity / resistance / priority flags
- reusable logistics graph representation

## 5. PowerGridManager

`Assets/_Project/Scripts/PowerGridManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `PowerGridManager : MonoBehaviour, ISlowTickable, IPowerGridService` |
| `96` | survives through `DontDestroyOnLoad(gameObject)` |
| `114` | owns centralized `SlowTick()` path |
| `288` | analyzes topology through `LogisticsNetworkGraph.TopologySummary` |
| `383` | publishes through `GlobalRegistry.RegisterPowerGridService(this)` |

This is the global power authority.
It is downstream of habitat/logistics topology, not independent from it.

## End-to-End Data Flow

Current construction flow reads as:

1. Authored module definition exists as `BaseModuleTemplate`.
2. Runtime placed object exists as `BaseModule`.
3. `ConstructionManager` tracks placed modules and persists them.
4. `HabitatGraphManager` rebuilds topology from the placed-module set.
5. `PowerGridManager` consumes topology snapshots and distributes power/brownout state.

This is a sensible pipeline.
The main weakness is not ownership chaos.
It is proof depth and runtime verification coverage.

## Save / Load Integration

Construction is fully in the save/load path, not an isolated runtime feature.

Confirmed facts:

- `ConstructionManager` is `ISaveable`
- it registers with `SaveManager` on enable
- it writes state through `PopulateSaveData(...)`
- it restores through `LoadFromSaveData(...)`
- it sits at `SavePriority = 90`, `LoadPriority = 90`

That priority places it late enough to depend on earlier core/player systems, but before very-late UI layers.

## What Looks Good

- Construction ownership is materially cleaner than many older docs described.
- Authored module template and placed runtime instance are separated.
- Topology rebuild is explicitly isolated in `HabitatGraphManager`.
- Power is downstream of topology rather than hardcoded per object pair.
- Save/load participation is direct and explicit.

## What Looks Merely Acceptable

- `ConstructionManager` is still a large owner with orchestration, save/load, logistics publication, and lifecycle duties combined.
- `BaseModule` carries several compatible responsibilities, but it is still a broad class.
- Some comments and docs in the area are older than the code and should not be trusted blindly.

## What Looks Weak

- No fresh active doc before this one tied construction, habitat graph, logistics, and power together.
- There is still no measured runtime proof showing construction spam, module rebuild storms, and brownout propagation under real gameplay load.
- Scene/prefab authored truth for every construction support service was not fully established in this pass.

## Failure Modes To Watch

- save/load can restore modules while topology/power rebuild timing is out of sync
- module-template identity drift can break restoration if stable IDs change
- large rebuild bursts can hide CPU cost without profiler proof
- broad `ConstructionManager` scope can attract unrelated responsibilities over time

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves subsystem navigation and reduces false assumptions about where construction, topology, and power really live. |

## Verdict

The current construction stack is not a loose pile of scripts.
It already has a recognizable architecture:

- `BaseModuleTemplate` defines authored module identity and cost
- `BaseModule` represents placed runtime behavior
- `ConstructionManager` owns orchestration and persistence
- `HabitatGraphManager` compiles connectivity
- `PowerGridManager` consumes connectivity and drives power outcome

The architecture is stronger than the old doc layer suggested.
The missing part is measured runtime proof, not basic subsystem ownership.

STATUS: PENDING VERIFICATION
