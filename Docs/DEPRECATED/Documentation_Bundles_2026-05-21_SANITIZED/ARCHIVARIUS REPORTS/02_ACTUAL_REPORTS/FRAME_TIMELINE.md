# FRAME TIMELINE â€” HECTON-8 Runtime Execution Order
Batch007 warning: [DEPRECATED] for runtime phase authority. Use `.agents-skills/ARCH_Execution_Phases.txt`. Raw Unity loop names in this report are historical evidence, not permission for private gameplay schedulers.

Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



> **Status:** ETA SANITIZED
> **Mandates Followed:** AGENTS.md Â§ Tick System Â· Â§ Jobs/Burst Â· Â§ Init Order Safety
> **Scope:** First-party production runtime under `Assets/_Project/Scripts/`

---

## 1. ARCHITECTURE OVERVIEW

HECTON-8 uses a **dual-layer tick architecture**:

| Layer | Owner | Contract | Lists |
|-------|-------|----------|-------|
| **Dispatcher** | `SystemDispatcher` | `IUpdatable` Â· `IFixedTickable` Â· `ISlowTickable` | `RegistryBucket<T>` per `PriorityLayer` lane |
| **Legacy Manager** | `GameTickManager` | `ITickable` Â· `IFixedTickable` Â· `ISlowTickable` | internal `TickList<T>` (buffered add/remove) |

`GameTickManager` is itself registered into `SystemDispatcher` **Core lane** as `IUpdatable`/`IFixedTickable`.
Therefore, `GameTickManager.Tick()` and `GameTickManager.FixedTick()` are invoked by `SystemDispatcher`, and they in turn fan-out to the legacy `ITickable`/`IFixedTickable`/`ISlowTickable` lists.

---

## 2. CHRONOLOGICAL FRAME FLOW

### Phase A â€” Unity FixedUpdate
```
UnityEngine FixedUpdate
â””â”€â”€ SystemDispatcher.FixedUpdate()        [DefaultExecutionOrder -9950]
    â””â”€â”€ Lane 0 : Core
        â””â”€â”€ GameTickManager.FixedTick()   [registered as IFixedTickable]
            â””â”€â”€ FOR each IFixedTickable in _fixedTickables (TickList)
    â””â”€â”€ Lane 1 : Environment              IFixedTickable[]
    â””â”€â”€ Lane 2 : Player                   IFixedTickable[]  (skipped during bootstrap)
    â””â”€â”€ Lane 3 : UI                       IFixedTickable[]
```

### Phase B â€” Unity Update
```
UnityEngine Update
â””â”€â”€ SystemDispatcher.Update()             [DefaultExecutionOrder -9950]
    â”œâ”€â”€ BootstrapStatus.TryTriggerSafeHalt()
    â”œâ”€â”€ FoveatedSimulationManager.BeginDispatcherFrame(dt)
    â”œâ”€â”€ PredatorCognitionDomain.BeginDispatcherFrame(frameCount)
    â”‚
    â”œâ”€â”€ Lane 0 : Core
    â”‚   â””â”€â”€ GameTickManager.Tick(dt)      [registered as IUpdatable]
    â”‚       â”œâ”€â”€ FOR each ITickable in _tickables (TickList)
    â”‚       â””â”€â”€ ProcessSlowTickIfNeeded() â†’ IF _accumulator >= 0.5s
    â”‚           â””â”€â”€ ExecuteSlowTick() â†’ FOR each ISlowTickable in _slowTickables
    â”‚
    â”œâ”€â”€ Lane 1 : Environment              IUpdatable.Tick(dt)
    â”œâ”€â”€ Lane 2 : Player                   IUpdatable.Tick(dt)  (skipped during bootstrap)
    â”œâ”€â”€ Lane 3 : UI                       IUpdatable.Tick(dt)
    â”‚
    â”œâ”€â”€ PredatorCognitionDomain.ScheduleFrameEvaluation(frameCount)   [BURST JOB SCHEDULE]
    â”œâ”€â”€ FoveatedSimulationManager.ScheduleFrameJobs()                  [BURST JOB SCHEDULE]
    â”‚
    â””â”€â”€ RunSlowTick(dt)  â† *only if GameTickManager path missed it*
        â””â”€â”€ Lane 0..3 : ISlowTickable.SlowTick()
```

### Phase C â€” Unity LateUpdate
```
UnityEngine LateUpdate
â””â”€â”€ SystemDispatcher.LateUpdate()
    â”œâ”€â”€ FoveatedSimulationManager.CompleteFrameJobs()    [BURST JOB COMPLETE]
    â”œâ”€â”€ WorldSpatialHashGrid.LateFrameMaintenance(frameCount)
    â””â”€â”€ UnsafeArenaAllocator.ResetFrame()
```

### Phase D â€” SRP Render (per camera)
```
RenderPipelineManager.beginCameraRendering
â””â”€â”€ RenderDispatcher.HandleBeginCameraRendering()   [DefaultExecutionOrder -9940]
    â”œâ”€â”€ RenderSettingsSnapshot.Capture()
    â”œâ”€â”€ GlobalRenderContext.SetCurrent(context, camera)
    â”œâ”€â”€ FOR each IRenderable in GlobalRegistry.Renderables (reverse order)
    â”‚       â””â”€â”€ renderable.Render(dt)
    â””â”€â”€ GlobalRenderContext.Clear()
```

---

## 3. LANE PRIORITY TABLE

| Lane Index | PriorityLayer | Typical Systems | DefaultExecutionOrder samples |
|------------|---------------|-----------------|-------------------------------|
| 0 | **Core** | GameTickManager, CrashTelemetryBuffer, PerformanceMonitor, InputDispatcher, HectonFloatingOrigin | -10000 â€¦ -9000 |
| 1 | **Environment** | MapMagicBridge, GlobalWeatherDirector, HectonFluidEngine, BiomeSamplerCache, AmbientWaterMotionManager | -7000 â€¦ -4000 |
| 2 | **Player** | HectonPlayerMovement, PlayerActionController, PlayerInteraction, HectonSurvivalSystem, VisorHUDController | â€” |
| 3 | **UI** | HUDQuickBar, InteractionUI, PauseMenuController, LoadingScreenController | â€” |

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
| `IUpdatable` | `GlobalRegistry.RegisterUpdatable(layer)` | SystemDispatcher.Update lane order | â€” |
| `IFixedTickable` | `GlobalRegistry.RegisterFixedTickable(layer)` | SystemDispatcher.FixedUpdate lane order | â€” |
| `ISlowTickable` | `GlobalRegistry.RegisterSlowTickable(layer)` | SystemDispatcher.Update (RunSlowTick) | GameTickManager.ProcessSlowTickIfNeeded |
| `ITickable` | `GameTickManager.Register()` | â€” | GameTickManager.Tick() |
| `IFixedTickable` (legacy) | `GameTickManager.Register()` | â€” | GameTickManager.FixedTick() |
| `ISlowTickable` (legacy) | `GameTickManager.Register()` | â€” | GameTickManager.ExecuteSlowTick() |

**WARNING:** Some classes implement **both** `ITickable` and `IUpdatable` (e.g. `FaunaBrain`, `HectonUnderwaterVisuals`). They receive **double dispatch** unless the author explicitly no-ops one path. Verify per-class registration logic before adding new tick contracts.

---

## 6. NATIVE Update/FixedUpdate AUDIT

| File | Method | Status |
|------|--------|--------|
| `SystemDispatcher.cs` | `Update()` Â· `FixedUpdate()` Â· `LateUpdate()` | âœ… ALLOWED â€” dispatcher root |
| `GameTickManager.cs` | None directly; implements `IUpdatable.Tick()` Â· `IFixedTickable.FixedTick()` | âœ… COMPLIANT |
| All other first-party gameplay scripts | None | âœ… COMPLIANT |

Third-party packages (Crest, Feel, GPUInstancer, etc.) contain native `Update()` calls. These are **outside first-party architecture** and are tracked as third-party debt, not violations.

---

*Report generated by ARCHIVARIUS sweep. Next audit: post-major-system merge.*
