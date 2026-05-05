# FRAME TIMELINE — HECTON-8 Runtime Execution Order
Date: 2026-05-04
Status: REFERENCE


> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md § Tick System · § Jobs/Burst · § Init Order Safety  
> **Scope:** First-party production runtime under `Assets/_Project/Scripts/`  

---

## 1. ARCHITECTURE OVERVIEW

HECTON-8 uses a **dual-layer tick architecture**:

| Layer | Owner | Contract | Lists |
|-------|-------|----------|-------|
| **Dispatcher** | `SystemDispatcher` | `IUpdatable` · `IFixedTickable` · `ISlowTickable` | `RegistryBucket<T>` per `PriorityLayer` lane |
| **Legacy Manager** | `GameTickManager` | `ITickable` · `IFixedTickable` · `ISlowTickable` | internal `TickList<T>` (buffered add/remove) |

`GameTickManager` is itself registered into `SystemDispatcher` **Core lane** as `IUpdatable`/`IFixedTickable`.  
Therefore, `GameTickManager.Tick()` and `GameTickManager.FixedTick()` are invoked by `SystemDispatcher`, and they in turn fan-out to the legacy `ITickable`/`IFixedTickable`/`ISlowTickable` lists.

---

## 2. CHRONOLOGICAL FRAME FLOW

### Phase A — Unity FixedUpdate
```
UnityEngine FixedUpdate
└── SystemDispatcher.FixedUpdate()        [DefaultExecutionOrder -9950]
    └── Lane 0 : Core
        └── GameTickManager.FixedTick()   [registered as IFixedTickable]
            └── FOR each IFixedTickable in _fixedTickables (TickList)
    └── Lane 1 : Environment              IFixedTickable[]
    └── Lane 2 : Player                   IFixedTickable[]  (skipped during bootstrap)
    └── Lane 3 : UI                       IFixedTickable[]
```

### Phase B — Unity Update
```
UnityEngine Update
└── SystemDispatcher.Update()             [DefaultExecutionOrder -9950]
    ├── BootstrapStatus.TryTriggerSafeHalt()
    ├── FoveatedSimulationManager.BeginDispatcherFrame(dt)
    ├── PredatorCognitionDomain.BeginDispatcherFrame(frameCount)
    │
    ├── Lane 0 : Core
    │   └── GameTickManager.Tick(dt)      [registered as IUpdatable]
    │       ├── FOR each ITickable in _tickables (TickList)
    │       └── ProcessSlowTickIfNeeded() → IF _accumulator >= 0.5s
    │           └── ExecuteSlowTick() → FOR each ISlowTickable in _slowTickables
    │
    ├── Lane 1 : Environment              IUpdatable.Tick(dt)
    ├── Lane 2 : Player                   IUpdatable.Tick(dt)  (skipped during bootstrap)
    ├── Lane 3 : UI                       IUpdatable.Tick(dt)
    │
    ├── PredatorCognitionDomain.ScheduleFrameEvaluation(frameCount)   [BURST JOB SCHEDULE]
    ├── FoveatedSimulationManager.ScheduleFrameJobs()                  [BURST JOB SCHEDULE]
    │
    └── RunSlowTick(dt)  ← *only if GameTickManager path missed it*
        └── Lane 0..3 : ISlowTickable.SlowTick()
```

### Phase C — Unity LateUpdate
```
UnityEngine LateUpdate
└── SystemDispatcher.LateUpdate()
    ├── FoveatedSimulationManager.CompleteFrameJobs()    [BURST JOB COMPLETE]
    ├── WorldSpatialHashGrid.LateFrameMaintenance(frameCount)
    └── UnsafeArenaAllocator.ResetFrame()
```

### Phase D — SRP Render (per camera)
```
RenderPipelineManager.beginCameraRendering
└── RenderDispatcher.HandleBeginCameraRendering()   [DefaultExecutionOrder -9940]
    ├── RenderSettingsSnapshot.Capture()
    ├── GlobalRenderContext.SetCurrent(context, camera)
    ├── FOR each IRenderable in GlobalRegistry.Renderables (reverse order)
    │       └── renderable.Render(dt)
    └── GlobalRenderContext.Clear()
```

---

## 3. LANE PRIORITY TABLE

| Lane Index | PriorityLayer | Typical Systems | DefaultExecutionOrder samples |
|------------|---------------|-----------------|-------------------------------|
| 0 | **Core** | GameTickManager, CrashTelemetryBuffer, PerformanceMonitor, InputDispatcher, HectonFloatingOrigin | -10000 … -9000 |
| 1 | **Environment** | MapMagicBridge, GlobalWeatherDirector, HectonFluidEngine, BiomeSamplerCache, AmbientWaterMotionManager | -7000 … -4000 |
| 2 | **Player** | HectonPlayerMovement, PlayerActionController, PlayerInteraction, HectonSurvivalSystem, VisorHUDController | — |
| 3 | **UI** | HUDQuickBar, InteractionUI, PauseMenuController, LoadingScreenController | — |

> **Bootstrap Gate:** During `BootstrapState.IsGameReady == false`, Lane 2 (Player) is skipped. All other lanes continue so that startup queues, residency, and spawn drains can complete.

---

## 4. BURST JOB SCHEDULE / COMPLETE WINDOWS

| Job Owner | Schedule Location | Complete Location | Safety |
|-----------|-------------------|-------------------|--------|
| `PredatorCognitionDomain` | End of `SystemDispatcher.Update` | N/A (consumes next frame) | Frame-delayed read |
| `FoveatedSimulationManager` | End of `SystemDispatcher.Update` | `SystemDispatcher.LateUpdate` | Same-frame completion |
| `WorldSpatialHashGrid` | N/A (background thread) | `LateFrameMaintenance` in LateUpdate | Deferred disposal |

> **Rule Compliance:** No `JobHandle.Complete()` is called inside a hot-path `Tick()` method. All completions happen in designated end-of-frame swap windows (`LateUpdate`).

---

## 5. REGISTRY vs LEGACY TICK DUALITY

| Interface | Registered Via | Dispatcher Path | Manager Path |
|-----------|----------------|-----------------|--------------|
| `IUpdatable` | `GlobalRegistry.RegisterUpdatable(layer)` | SystemDispatcher.Update lane order | — |
| `IFixedTickable` | `GlobalRegistry.RegisterFixedTickable(layer)` | SystemDispatcher.FixedUpdate lane order | — |
| `ISlowTickable` | `GlobalRegistry.RegisterSlowTickable(layer)` | SystemDispatcher.Update (RunSlowTick) | GameTickManager.ProcessSlowTickIfNeeded |
| `ITickable` | `GameTickManager.Register()` | — | GameTickManager.Tick() |
| `IFixedTickable` (legacy) | `GameTickManager.Register()` | — | GameTickManager.FixedTick() |
| `ISlowTickable` (legacy) | `GameTickManager.Register()` | — | GameTickManager.ExecuteSlowTick() |

**WARNING:** Some classes implement **both** `ITickable` and `IUpdatable` (e.g. `FaunaBrain`, `HectonUnderwaterVisuals`). They receive **double dispatch** unless the author explicitly no-ops one path. Verify per-class registration logic before adding new tick contracts.

---

## 6. NATIVE Update/FixedUpdate AUDIT

| File | Method | Status |
|------|--------|--------|
| `SystemDispatcher.cs` | `Update()` · `FixedUpdate()` · `LateUpdate()` | ✅ ALLOWED — dispatcher root |
| `GameTickManager.cs` | None directly; implements `IUpdatable.Tick()` · `IFixedTickable.FixedTick()` | ✅ COMPLIANT |
| All other first-party gameplay scripts | None | ✅ COMPLIANT |

Third-party packages (Crest, Feel, GPUInstancer, etc.) contain native `Update()` calls. These are **outside first-party architecture** and are tracked as third-party debt, not violations.

---

*Report generated by ARCHIVARIUS sweep. Next audit: post-major-system merge.*
