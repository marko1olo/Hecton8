# BOOT ORDER VIOLATIONS

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` and immediate bootstrap-owned service owners
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `STRM_Persistent_Object_Registry.txt`

## Audit Verdict

Boot-time dependency-cycle detection exists, but boot-order safety is not compliant.

- Kahn-style cycle validation: `PASS`
- Topological execution ownership: `FAIL`
- GlobalRegistry owner-before-consumer discipline: `FAIL`

## What Was Verified

`GameBootstrapper.ValidateBootstrapDependencyGraph()` is a real Kahn-style cycle validator.

- Declared edges live at `GameBootstrapper.cs:94-116`.
- In-degree counting and queue seeding live at `GameBootstrapper.cs:468-485`.
- Dependency drain and `processedCount == nodeCount` success condition live at `GameBootstrapper.cs:487-505`.

This proves the declared graph can detect cycles.
It does not prove the declared graph matches real runtime reads.
It also does not drive initialization order.

Real initialization is still hard-coded by phase methods:

- core phase: `GameBootstrapper.cs:238-252`
- environment phase: `GameBootstrapper.cs:254-267`
- player phase: `GameBootstrapper.cs:269-303`

## Violations

### 1. `RenderDispatcher` self-registers in `Awake()`

Owner path:

- bootstrap creation path: `GameBootstrapper.cs:388-395`
- self-registration path: `SystemDispatcher.cs:868-885`

Evidence:

- `EnsureRenderDispatcherRegistered()` only creates the object and returns `AddComponent<RenderDispatcher>()`.
- There is no explicit `InitializeService()` call for `RenderDispatcher`.
- `RenderDispatcher.Awake()` reads `GlobalRegistry.RenderDispatcher` before registration and then performs `GlobalRegistry.RegisterRenderDispatcher(this)`.

Why this fails:

- registration side effects are happening in `Awake()`, not under explicit bootstrap ownership
- the declared bootstrap graph says `RenderDispatcher -> SystemDispatcher`, but the actual registration moment is an `Awake()` side effect, not a phase-controlled initialization step

### 2. `GlobalPhysicsStateManager` self-registers in `Awake()`

Owner path:

- bootstrap creation path: `GameBootstrapper.cs:397-403`
- self-registration path: `GlobalPhysicsStateManager.cs:308-340`

Evidence:

- `EnsureGlobalPhysicsStateManagerRegistered()` only creates the object and returns `AddComponent<GlobalPhysicsStateManager>()`.
- There is no explicit `InitializeService()` call for `GlobalPhysicsStateManager`.
- `GlobalPhysicsStateManager.Awake()` reads `GlobalRegistry.PhysicsStateManager` before registration and then performs `GlobalRegistry.RegisterPhysicsStateManager(this)`.

Why this fails:

- same contract breach as `RenderDispatcher`
- registration timing is hidden inside `Awake()`
- bootstrap phase ownership is declared but not actually enforced

### 3. `PlayerRuntimeContextService` reads later player-layer owners before they are registered

Owner path:

- player-layer initialization order: `GameBootstrapper.cs:294-302`
- service init path: `PlayerRuntimeContextService.cs:263-289`
- early registry reads: `PlayerRuntimeContextService.cs:474-512`

Evidence:

- bootstrap order is:
  - `InputDispatcher.InitializeService()`
  - `PlayerRuntimeContextService.InitializeService()`
  - `PlayerInventoryManager.InitializeService()`
  - `PlayerSensoryManager.InitializeService()`
- `PlayerRuntimeContextService.InitializeService()` immediately calls `SyncPlayerContext()`.
- `SyncPlayerContext()` immediately calls `RefreshDynamicContextReferences()`.
- `RefreshDynamicContextReferences()` reads:
  - `GlobalRegistry.PlayerInventory`
  - `GlobalRegistry.PlayerSensory`

Why this fails:

- those owners have not been initialized yet at the time of the read
- the read is null-safe, but it is still a real graph omission and a real premature registry access
- the declared dependency graph only models `PlayerRuntimeContextService -> InputDispatcher`; it does not model its opportunistic reads of `PlayerInventory` and `PlayerSensory`

## Adjacent Structural Finding

The bootstrap dependency graph is validation-only.

- There is no computed topological order reused by the runtime initialization steps.
- The actual runtime order is still manually authored per phase.

Result:

- cycle detection can pass while real owner/consumer drift still exists
- missing edges remain invisible until a service performs a real registry read during initialization

## Regression Model

CPU: audit-only, no runtime mutation
GC: audit-only, no gameplay-path mutation
Memory: audit-only, no asset or scene mutation
Cadence: no runtime cadence change
Correctness: improved because the current drift is now explicit instead of hidden behind a passing cycle check

## Hot Path Impact

None. Markdown-only report.

## Failure Modes

- more hidden reads may exist in scene-authored services outside the bootstrap-owned layer
- self-registration in `Awake()` can still reintroduce order coupling even when the declared graph remains acyclic
- a clean cycle check may be misread as a full boot-order proof when it is not

## Why Kept

Kept because this report separates algorithm validity from architecture validity.
Rejected conclusion: "cycle detector exists, therefore bootstrap order is safe."

STATUS: PENDING VERIFICATION
