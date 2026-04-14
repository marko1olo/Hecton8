# ECS / DOTS Adoption Plan

Status: `PROPOSED`
Verification: `PENDING VERIFICATION`

This document defines where ECS / DOTS is worth introducing in HECTON-8, where it is not, and in what order the work should happen.

## Implementation Status

The following first-party groundwork is already landed in code:

- scatter runtime profiler markers around tick / slow tick / dispatch / sampling / processing / reconcile phases
- batch Unity compile proof after asmdef baseline repair:
  - `*** Tundra build success`
  - `ExitCode: 0`
- shared scatter simulation contracts:
  - `ScatterSimulationBackendKind`
  - `ScatterSimulationCandidate`
  - `ScatterSimulationResult`
  - `IScatterSimulationBackend`
- classic adapters for the current non-DOTS path:
  - `ScatterClassicSimulationBackend`
- runtime orchestration seam:
  - `ScatterRuntimeBackendFacade`
- owner-side integration groundwork:
  - backend lifecycle wiring in `WorldProceduralScatterDirector`
  - runtime rule/family lookup bridge
  - height sample bridge
  - optional shadow-pass scheduling/completion path for backend parity checks
  - shadow parity counters for classic queued-candidate count vs backend candidate count

What this means:

- scatter now has an explicit simulation backend boundary
- future DOTS work can slot behind that seam without creating a second runtime owner
- `WorldProceduralScatterDirector` remains the intended owner
- owner can now schedule the backend seam in shadow mode without giving it placement ownership
- owner can now record basic parity deltas before any live-backend takeover is attempted
- bootstrap runtime state now has a contract-only read-model:
  - `BootstrapState`
  - `Hecton8.Bootstrap.Contracts`
  - `SceneBootstrap` publishes lifecycle/player state into it
  - `GameTickManager`, `WorldRuntimeReferenceUtility`, and selected world/scatter readers can consume bootstrap state without depending on the full bootstrap owner
- scatter backend rollout now has a dedicated hybrid entry point contract:
  - `ScatterHybridRuntimeEntryPoint`
  - `ScatterHybridRuntimePlan`
  - backend fallback / shadow-only gating / live-ownership refusal no longer live as ad-hoc logic in the director
  - owner now rebuilds backend facade when the resolved backend kind changes, instead of silently staying on stale runtime wiring
- requested rollout mode is now explicit instead of being only a legacy bool:
  - `scatterBackendRequestedExecutionMode`
  - legacy `enableScatterBackendShadowPass` only acts as compatibility input when requested mode remains `Disabled`
- scatter backend runtime state now lives behind one owner-local host contract:
  - `ScatterBackendRuntimeHost`
  - plan refresh, facade sync, binding-state lifetime, and shadow baseline bookkeeping are no longer spread across the director partial
- scatter backend binding state now lives behind one owner-local contract:
  - `ScatterBackendBindingState`
  - representative layer-family indices and height-sample bridge are no longer a loose field cluster on the director partial
- backend host now exposes a typed runtime status read-model:
  - `ScatterBackendRuntimeStatus`
  - director debug wiring no longer needs to read host state field-by-field
- scatter backend scheduling and shadow completion now use typed owner-local contracts:
  - `ScatterBackendScheduleRequest`
  - `ScatterBackendShadowCompletion`
  - backend config building, height-sample bridging, and shadow parity completion bookkeeping moved into `ScatterBackendRuntimeHost`
- scatter backend binding lookup now lives behind an owner-local bridge:
  - `ScatterBackendBindingBridge`
  - representative family-index rebuild is no longer embedded directly in the main backend integration partial
- scatter backend support helpers now live under one owner-local support bundle:
  - `ScatterBackendSupportContext`
  - binding bridge and request factory are no longer separate ad-hoc fields on the integration partial
- scatter contracts now live in a neutral assembly boundary:
  - `Hecton8.World.Contracts`
  - `ScatterSimulationContracts.cs`
  - `ScatterSimulationBackendRegistry.cs`
  - core runtime and DOTS backend can now share scatter contracts without a direct compile-time cycle
- DOTS prototype scaffolding now exists:
  - `com.unity.entities` added to `Packages/manifest.json`
  - `Hecton8.World.Dots.asmdef`
  - `ScatterEntitiesSimulationBackend`
  - registration is provider-based, so the owner assembly still does not take a direct compile-time dependency on the DOTS assembly
- current Entities backend is still shadow-safe prototype work, not approved live placement ownership
- narrow DOTS scope contract now exists for scatter data bookkeeping only:
  - `ScatterSimulationCellState`
  - `ScatterSimulationEligibilityFlags`
  - `ScatterSimulationQuotaState`
  - `ScatterSimulationSuppressionState`
  - `ScatterSimulationDirtyFlags`
  - current Entities prototype now materializes cell-state / quota / suppression / dirty-flag data instead of only raw height samples

Current package stance:

- `com.unity.entities` is now declared in `Packages/manifest.json`
- package restore / import is still `PENDING VERIFICATION`
- rollout work still remains under the existing owner and shadow-only backend seam until profiler/runtime proof justifies live ownership changes

Current blocker:

- direct owner takeover of live placement reconciliation is still intentionally not finished
- current Entities backend is a minimal prototype and does not yet prove parity with the classic evaluator
- compile is currently green after asmdef baseline changes, but runtime/editor proof is still absent
- enabling the new backend for live placement ownership before parity/profiler proof would increase regression risk in the current runtime path
- `SceneBootstrap` still remains a large owner and event source; only the read-model was extracted so far
- current local batch Unity verification is blocked by editor licensing on this machine:
  - `No valid Unity Editor license found`
  - return code `198`

This is not a generic Unity DOTS note.

This is a project-specific adoption plan constrained by:

- current HECTON-8 owner stack
- current MonoBehaviour runtime architecture
- existing Jobs/Burst backend work
- MX350 target hardware
- existing third-party dependencies

## Goal

Introduce DOTS only where it gives predictable value:

- lower main-thread cost
- lower residency bookkeeping overhead
- better large-scale simulation density
- preserved deterministic ownership boundaries

Do not introduce DOTS where it creates:

- a second runtime stack
- broken prefab-centric workflows
- save / pool / scene / plugin integration damage
- performance regressions hidden behind architecture churn

## Current Truth

### Package State

- `com.unity.entities` is declared in `Packages/manifest.json`.
- `com.unity.physics` is not installed.
- `Hecton8.World.Dots` asmdef exists.
- `ScatterEntitiesSimulationBackend` exists as minimal prototype code.
- first-party `Baker`, `SubScene`, `ISystem`, and `SystemBase` usage is still absent.
- `com.unity.burst`, `com.unity.collections`, and `com.unity.mathematics` already exist.
- package import / editor runtime proof is still `PENDING VERIFICATION`.

### Runtime Shape

The project is not a naive MonoBehaviour project.

It already uses a hybrid data-oriented style:

- centralized cadence via `GameTickManager`
- preallocated containers
- `NativeArray` working memory
- Burst jobs
- NonAlloc physics
- object pooling
- GPUI for mass flora rendering

That matters because a large part of DOTS-style value is already partially present without Entities.

### Measured Architectural Facts

Static repo facts from first-party scripts:

- `419` first-party `.cs` files
- `91` files with `ITickable`
- `48` files with `ISlowTickable`
- `7` files with `IFixedTickable`
- `18` files using `Jobs/Burst/NativeArray`
- first-party ECS/DOTS files now exist, but only as isolated scatter prototype scaffolding under `Assets/_Project/Scripts/World/Dots`

### Existing Runtime Owners That Must Remain Owners

These are the important anchors:

- `GameTickManager`
- `ObjectPoolManager`
- `WorldProceduralScatterDirector`
- `WorldProceduralFieldSampler`
- `WorldProceduralFillDirector`
- `WorldGenerativeGeologyIntegrationDirector`
- `WorldGenerativeGeologySeamExecutionDirector`
- `WorldGenerativeGeologyVoxelBridgeDirector`
- `FaunaDirector`
- `HectonFluidEngine`
- `HectonVoxelEngine`

### Hard Constraint From Existing Docs

`Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` already defines the critical rule:

- one shared runtime placement owner
- no new scatter stack per category
- no separate runtime fork per content vertical

That rule applies directly to DOTS adoption.

If DOTS creates a second placement owner, the design is rejected.

## Executive Verdict

Full-project DOTS migration is the wrong move.

The correct move is a narrow hybrid DOTS backend used only under existing runtime owners.

The first valid DOTS target is not UI, save, player, interaction, construction, or active AI.

The first valid DOTS target is world-scale placement and residency simulation.

## System Classification

## Tier A: Do Not Move To ECS

These systems should stay classic Unity unless there is new evidence:

- player movement
- interaction
- tools
- construction
- save system
- audio system
- visor / HUD / PDA
- loading / pause / menu UI
- active pooled fauna controllers using `Rigidbody`

Reason:

- tight coupling to `MonoBehaviour`, `Transform`, `Rigidbody`, `Canvas`, TMP, pooled lifecycle, and third-party assets
- low reward for high rewrite risk

Key files:

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/UI/*`
- `Assets/_Project/Scripts/Visor/*`
- `Assets/_Project/Scripts/HectonBaseAI.cs`

## Tier B: Keep Hybrid, Do Not Convert To Entities Yet

These systems already use the right shape and should be improved in-place first:

- `HectonFluidEngine`
- `HectonBoidController`
- `HectonVoxelEngine`
- `ProximityColliderSystem`
- `RaycastBatchHelper`

Reason:

- they already use Burst, GPU compute, `NativeArray`, or staged job pipelines
- the current bottleneck is not necessarily “lack of Entities”
- rewrite cost is large relative to likely gain

### `HectonFluidEngine`

File:

- `Assets/_Project/Scripts/HectonFluidEngine.cs`

Current shape:

- managed registry of `BuoyancyObject` + `Rigidbody`
- SoA native buffers
- Burst `IJobParallelFor`
- main-thread gather
- immediate `Schedule` -> `Complete`
- main-thread `AddForce`

What is wrong:

- not enough overlap between simulation and other work
- still bound to `Rigidbody` writeback
- gains from Entities Physics are not free because the gameplay side still expects classic Unity physics

Correct direction:

- keep hybrid
- improve schedule/complete cadence only if profiler proves need
- reduce gather/apply overhead
- batch observer / LOD decisions better

Do not move this to Entities first.

### `HectonBoidController`

File:

- `Assets/_Project/Scripts/HectonBoidController.cs`

Current shape:

- GPU compute simulation
- indirect rendering
- no per-boid GameObjects

This is already more appropriate than CPU ECS for this specific use case.

Correct direction:

- keep GPU path
- profile VRAM and dispatch cost
- do not replace with Entities Graphics just to “use DOTS”

### `HectonVoxelEngine`

File:

- `Assets/_Project/Scripts/HectonVoxelEngine.cs`

Current shape:

- large Burst pipeline
- async generation
- native buffers
- classic mesh/material/collider output

What remains expensive:

- mesh assembly
- collider assignment
- object lifecycle

Entities does not remove those costs by itself.

Correct direction:

- keep as hybrid compute backend
- only consider DOTS around request scheduling and residency, not the mesh owner

### `ProximityColliderSystem`

File:

- `Assets/_Project/Scripts/ProximityColliderSystem.cs`

Current shape:

- frame N schedules distance job
- frame N+1 completes and reconciles pooled colliders
- proper hybrid cadence already exists

This is already a clean mini-example of project-compatible pseudo-ECS.

Correct direction:

- keep hybrid
- move to ECS only if point count becomes too large for current main-thread reconciliation

## Tier C: Best ECS Candidates

These are the real candidates.

## Candidate 1: Scatter Cell Simulation And Residency

Primary files:

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirectorSamplingPipeline.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterWorkingMemory.cs`
- `Assets/_Project/Scripts/World/ScatterEvaluator.cs`
- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`

Why this is the strongest candidate:

- this is already the main world-scale selection owner
- it has large amounts of bookkeeping data
- it already separates pure evaluation from spawn reconciliation
- it already has a job-friendly field sampling stage
- current code size and monolithic state make classic maintenance expensive

What should move to ECS:

- scatter cell residency state
- cell eligibility state
- placement quotas and density bookkeeping
- occupancy maps
- candidate scoring state
- suppression / retain / stale state
- spawn request generation

What must stay outside ECS:

- `WorldProceduralScatterDirector` as the owner facade
- prefab resolution
- `ObjectPoolManager.Spawn/Despawn`
- GPUI registration calls
- all save-facing and scene-facing ownership

Correct architecture:

1. `WorldProceduralScatterDirector` gathers world dependencies
2. facade writes a compact input frame into DOTS world
3. ECS systems evaluate residency and candidate outputs
4. result buffer is read back on main thread
5. existing owner-driven reconcile path or GPUI bridge applies changes

This keeps:

- one runtime placement owner
- same scene/prefab/pool integration
- same save and suppression semantics

This is the first DOTS slice to prototype.

### Why This Is Better Than A Full Scatter Rewrite

The project already has:

- `WorldProceduralFieldSampler.ScheduleCellSamplingJob(...)`
- `ScatterEvaluator`
- owner-driven scatter reconcile/apply path inside `WorldProceduralScatterDirector`

That means the architecture already wants a data backend and a main-thread owner boundary.

Entities should replace only the massive bookkeeping core, not the whole stack.

### Explicit Stop Rule

If the proposed DOTS version requires:

- a separate scatter manager
- a second residency owner
- direct prefab spawning inside ECS
- replacing GPUI integration with a new rendering stack

it is the wrong design.

## Candidate 2: Passive Fauna Ecology Layer

Primary files:

- `Assets/_Project/Scripts/FaunaDirector.cs`
- `Assets/_Project/Scripts/HectonDirectorAI.cs`
- `Assets/_Project/Scripts/WorldFaunaSpawnRegistry.cs`

Why it is a candidate:

- passive ecology decisions are sparse, repetitive, and world-scale
- much of the logic is quota / density / anchor / desirability bookkeeping
- this work does not need full `Rigidbody` ownership

What should move to ECS:

- passive fauna spawn desirability maps
- anchor scoring
- biome pressure accumulation
- spawn slot eligibility
- despawn eligibility summaries

What should not move:

- actual active creatures
- `HectonBaseAI`
- `Rigidbody` locomotion
- per-creature obstacle avoidance
- attack / threat / stimulus logic

Correct result:

- ECS produces passive fauna spawn requests and suppressions
- `FaunaDirector` stays owner and applies pooled scene changes

This is a second-phase candidate, not the first.

## Candidate 3: Geology Seam / Voxel Request Scheduling

Primary files:

- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`

Why it is a candidate:

- runtime keys, priorities, blend weights, chunk ownership, stale request rejection, and request queues are data-oriented
- these systems already think in deterministic request records

What should move to ECS:

- seam plan residency
- voxel request prioritization
- chunk ownership tables
- stale / pending / active request state
- per-zone request budget decisions

What must stay outside ECS:

- actual voxel mesh build
- mesh and collider ownership
- `HectonVoxelEngine` output
- terrain mutation
- GameObject runtime roots

Correct use:

- ECS as request scheduler and residency graph
- Mono as build executor and world integrator

This is a strong third-phase candidate.

## Tier D: Avoid For Now

## `HectonBaseAI`

File:

- `Assets/_Project/Scripts/HectonBaseAI.cs`

Reason to avoid:

- classic `Rigidbody` physics
- rich state machine
- player stimulus caching
- NonAlloc raycast obstacle avoidance
- pooled lifecycle
- lots of handcrafted gameplay logic

This is expensive to rewrite and easy to regress.

Use hybrid improvements only.

## UI / Visor / PDA

Files:

- `Assets/_Project/Scripts/UI/*`
- `Assets/_Project/Scripts/Visor/*`
- `Assets/_Project/Scripts/HectonInventoryUI.cs`
- `Assets/_Project/Scripts/HectonFabricatorUI.cs`
- `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`

Reason to avoid:

- Canvas, TMP, Shapes, screen-space logic, presentation timing
- no world-scale repetitive simulation benefit
- integration risk is high, reward is low

## Save / Bootstrap / Audio

Reason to avoid:

- correctness-sensitive
- async and file ownership sensitive
- no meaningful ECS advantage

## Adoption Pattern

The only valid pattern for this project is:

`Mono Owner -> DOTS Data Backend -> Main Thread Reconciler -> Existing Pool / GPUI / Scene Runtime`

Not:

`Mono runtime + separate DOTS runtime both trying to own the same gameplay space`

## Required Preparatory Work Before Any Live DOTS Rollout

These tasks come first.

### 1. Assembly Definition Split

Current state:

- first-party asmdefs now exist, but the split is still shallow:
  - `Hecton8.Bootstrap.Contracts`
  - `Hecton8.Core`
  - `Hecton8.Editor`
  - `Hecton8.Input`
  - `Hecton8.Input.Generated`
  - `Hecton8.World.Contracts`
  - `Hecton8.World.Dots`
- most first-party runtime code still lives behind one large runtime assembly boundary, not domain-focused assemblies
- some older tooling assumptions still expect `Assembly-CSharp` and must be removed as asmdef rollout continues

This is bad for DOTS introduction because package references and compile boundaries become uncontrolled.

Required split:

- `Hecton8.Bootstrap`
- `Hecton8.Core`
- `Hecton8.World`
- `Hecton8.AI`
- `Hecton8.UI`
- `Hecton8.Editor`
- keep: `Hecton8.Bootstrap.Contracts`
- keep: `Hecton8.World.Contracts`
- keep: `Hecton8.World.Dots`

Without this, DOTS adoption will be messy and expensive to unwind.

### 2. Data Contract Extraction

Extract compact runtime data contracts for:

- scatter input frame
- scatter cell state
- scatter candidate output
- fauna ecology input frame
- geology request state

These contracts must be blittable-first.

### 3. Owner Boundary Lock

Document explicitly which owner remains authoritative for:

- spawning
- despawning
- suppression
- save state
- GPUI updates
- voxel runtime creation

Without this, DOTS work will drift into architecture duplication.

## Phased Rollout Plan

## Phase 0: Preparation

Scope:

- add asmdefs
- isolate world runtime dependencies
- extract compact data contracts
- add dedicated profiling markers around scatter refresh and reconcile stages

Deliverables:

- clean compile boundaries
- documented input/output contracts
- profiler baselines

Implementation status as of 2026-04-12:

- `Hecton8.Core.asmdef` baseline created and corrected for Burst / Collections / Profiling / URP Core / third-party runtime references
- `Hecton8.Editor.asmdef` added so first-party editor tooling can stop living inside the runtime assembly boundary
- `Hecton8.Input.asmdef` added so runtime input code is no longer forced to live inside the main gameplay assembly
- generated input code moved behind dedicated asmdef: `Assets/_Project/Input/Hecton8.Input.Generated.asmdef`
- bootstrap runtime read-model moved behind dedicated contract asmdef:
  - `Assets/_Project/Scripts/Core/BootstrapContracts/Hecton8.Bootstrap.Contracts.asmdef`
  - `BootstrapState.cs`
- source plugins required by first-party asmdef were given explicit assembly boundaries:
  - `Assets/Plugins/Easy Save 3/Scripts/EasySave3.asmdef`
  - `Assets/Plugins/Easy Save 3/Editor/EasySave3Editor.asmdef`
  - `Assets/VolumetricLightBeam/Scripts/VolumetricLightBeam.asmdef`
  - `Assets/VolumetricLightBeam/Editor/VolumetricLightBeam.Editor.asmdef`
- scatter rebuild profiling is no longer a raw long-argument blob inside the director:
  - `ScatterReconcileMetrics.cs`
  - `ScatterRebuildProfileSnapshot.cs`
  - `ScatterDiagnosticsTracker.cs` now owns snapshot building + report emission
- compile flood from missing assembly refs was reduced to real code defects, then fixed
- one concrete asmdef fallout was identified and corrected:
  - `MapMagicWorldValidator` no longer hardcodes `ScatterBudgetController` lookup through `Assembly-CSharp`
- status remains `PENDING VERIFICATION`

Remaining Phase 0 gaps:

- first-party code is still mostly one large runtime assembly, not a full domain split
- `SceneBootstrap`, `BootstrapController`, and route enforcement still live inside the large runtime assembly
- bootstrap is not ready for blind assembly extraction:
  - `SceneBootstrap` is referenced by `55` first-party scripts
  - `BootstrapController` is referenced by `7` first-party scripts
  - `BootstrapRouteEnforcer` is referenced by `3` first-party scripts
  - this means a real `Hecton8.Bootstrap` split still needs more contract extraction first, not a cosmetic asmdef drop
- profiler baselines for scatter refresh / reconcile are still absent
- Unity MCP console became temporarily unavailable after domain reload, so final zero-error confirmation is not yet MCP-verified
- Unity Editor process later shut down, so MCP verification is currently blocked until the editor is restarted

Exit gate:

- no functional change
- no compile regressions
- profiling captures exist for current scatter path

## Phase 1: Scatter Backend Hardening Without Entities

Scope:

- reduce managed pressure inside `WorldProceduralScatterDirector`
- further separate facade from data backend
- move more bookkeeping into native-friendly containers where possible

Target files:

- `WorldProceduralScatterDirector.cs`
- `WorldProceduralScatterWorkingMemory.cs`
- `ScatterEvaluator.cs`

Purpose:

- prove the backend seam
- lower risk before Entities package enters the repo

Exit gate:

- scatter behavior unchanged
- no new runtime owner
- clearer IO boundary between facade and simulation

## Phase 2: DOTS Prototype For Scatter Simulation

Scope:

- validate `com.unity.entities` import in Unity on the working branch
- keep `Hecton8.World.Dots` isolated from owner-facing runtime assemblies
- expand the minimal DOTS world for scatter residency and candidate generation
- keep current Mono reconciler and GPUI path

DOTS responsibilities:

- cell entities
- residency tags
- eligibility and quota systems
- candidate generation buffers

Mono responsibilities remain:

- world reference gathering
- prefab lookup
- ObjectPool integration
- GPUI matrix submission
- save-facing suppression

Prototype success criteria:

- same visible scatter semantics in test area
- reduced main-thread time in scatter-heavy scene
- no GC regression
- no second owner introduced

Failure criteria:

- faster simulation but slower total frame time
- higher sync cost than saved main-thread work
- complex authoring burden
- broken suppression / restore semantics

## Phase 3: Passive Fauna Ecology DOTS Backend

Do only if Phase 2 works.

Scope:

- passive fauna density simulation
- anchor desirability
- spawn request output

Keep outside:

- active AI bodies
- physics-driven creatures

## Phase 4: Geology / Voxel Request Scheduling DOTS Backend

Do only if Phase 2 works and geology runtime becomes a measurable offender.

Scope:

- request prioritization
- active/stale request state
- chunk ownership
- budget throttling

Keep outside:

- mesh generation
- collider generation
- terrain mutation

## System-Specific Detailed Notes

## Scatter

### Why It Is Ready

Evidence already exists:

- `WorldProceduralFieldSampler` produces compact cell inputs and outputs
- `WorldProceduralScatterDirectorSamplingPipeline` already runs async job sampling then processes later
- `ScatterEvaluator` is already isolated from the owner-driven apply path

This is exactly the sort of system that can accept a DOTS backend under an existing owner.

### What To Represent As Entities

- cell coordinate
- sampled world context
- current occupancy
- desired occupancy
- layer quotas
- domain tags
- refresh dirty flag
- suppression tag
- candidate buffer element

### What To Keep As Plain C#

- prefab family authoring assets
- runtime rule assets
- profile lookup tables
- pool and GPUI bridge
- scene object references

## Fauna

### Why It Is Only A Partial Candidate

`FaunaDirector` is a good candidate.

`HectonBaseAI` is not.

That boundary must remain strict.

The ecology layer is repetitive and summary-driven.

The active creature brain is bespoke gameplay code.

## Fluid

### Why It Is Not Phase 1

The system already uses a proper SoA native layout and Burst.

The real remaining cost is synchronization with classic `Rigidbody` and main-thread apply.

Entities would force a much wider physics migration to be coherent.

That is not a controlled first move.

## Geology / Voxel

### Why It Is A Scheduler Candidate, Not A Mesh Candidate

The deterministic runtime key and request queue logic is an ECS-style problem.

Mesh build and collider output are still classic Unity problems in this project.

Keep that split.

## Verification Requirements

No phase is “done” from code review alone.

Each phase requires:

- Profiler capture before
- Profiler capture after
- main-thread delta
- GC delta
- total frame delta
- failure mode notes

For scatter prototype specifically:

- compare scatter rebuild CPU on the same scene and same radius settings
- compare reconcile cost separately from simulation cost
- compare total frame time, not only system time
- inspect whether readback/sync destroyed the gain

## Regression Model

Every phase must report:

- CPU regression model
- GC regression model
- memory retention model
- cadence / sync model
- correctness model

Examples of failure:

- simulation got cheaper but reconcile got more expensive
- total frame worsened because of sync fences
- memory footprint rose without real density gain
- suppression / restore became nondeterministic
- DOTS path broke pool ownership

## Explicit No-Go Conditions

Stop DOTS work immediately if any of these become true:

- need to duplicate `WorldProceduralScatterDirector`
- need to bypass `ObjectPoolManager`
- need to replace GPUI integration just to make DOTS work
- need to move save-facing ownership into ECS
- need to replace active fauna controllers with ECS bodies
- need to introduce SubScenes for authoring before backend value is proven

## Immediate Execution Slice

Do this next, in order:

1. continue contract-only asmdef extraction where dependency shape is already clean
2. keep extracting scatter runtime data contracts behind `Hecton8.World.Contracts`
3. add profiler markers around:
   - field sample preparation
   - job scheduling
   - job completion
   - candidate processing
   - spawn/despawn reconciliation
   - GPUI upload
4. produce a baseline scatter-heavy capture
5. only then evaluate whether the current DOTS prototype earns further expansion

That is the first correct path.

Not:

1. install Entities
2. start rewriting systems blindly

## Final Directive

DOTS is justified here only as a narrow backend for world-scale simulation and residency.

The project already contains several strong pseudo-ECS systems.

The job is not to replace them with fashionable architecture.

The job is to identify the single highest-yield backend seam, prove it with numbers, and stop if the numbers do not justify expansion.
