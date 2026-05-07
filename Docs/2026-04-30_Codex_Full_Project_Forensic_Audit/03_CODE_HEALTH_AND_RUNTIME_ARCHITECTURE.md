# Code Health And Runtime Architecture

Date: 2026-05-07
Status: PENDING VERIFICATION

## Actual Runtime Shape

The runtime is not controlled by one clean authority.

The real authority surface is split across:
- scene composition
- `GameBootstrapper`
- `SceneBootstrap`
- `SystemDispatcher`
- `GameTickManager`
- many static `Instance`-style service owners

That split matters because the project documents claim tighter discipline than the code consistently enforces.

## Architecture Truth

### 1. GlobalRegistry is real

`Assets/_Project/Scripts/Core/GlobalRegistry.cs` is large, broad, and heavily used.

This is not a decorative pattern. It is a live backbone.

### 2. Registry sovereignty is not real

The project still contains heavy parallel authority through:
- `public static ... Instance`
- `DontDestroyOnLoad`
- scene-root manager clusters
- direct scene-owned runtime dependencies

This means the project has not completed its architecture migration.

### 3. Dispatcher exists and matters

`Assets/_Project/Scripts/Core/SystemDispatcher.cs` is one of the strongest architecture files in the project because it actually owns runtime cadence lanes and deferred flush behavior.

It also exposes the uncomfortable truth:
- native `Update`
- native `LateUpdate`
- native `FixedUpdate`

So the projectâ€™s â€œno Update in gameplay codeâ€ standard is partially aspirational, partially enforced, and partially bypassed.

## State-Truth Trust Problem

The initial pass captured first-party compile-specific errors.
Fresh reverification no longer supports treating those specific lines as current live blockers.

What is current:
- old compile specifics became stale inside the same day
- `Editor.log` is now saturated by `Resource ID out of range in SetResource`
- MCP/editor readiness is volatile
- console truth and log truth are not staying aligned

This is worse than a routine bug because it breaks trust in the observation surface itself.

It means any doc claiming current health must be treated as non-authoritative until reverified against fresh code and fresh editor state.

## Monolith Risk

Several owner files are extreme in size:
- `HectonMapMagicVegetationBridge.cs`
- `HectonPlayerMovement.cs`
- `WorldProceduralScatterDirector.cs`
- `HectonUnderwaterVisuals.cs`
- `SuitHUDV4CanvasOverlay.cs`
- `FaunaDirector.cs`
- `PlayerCriticalProceduralAudioRenderer.cs`
- `SargassumMicroFaunaBoids.cs`
- `HectonVoxelEngine.cs`
- `WorldProceduralFieldSampler.cs`

Large files do not automatically mean bad design.
These ones now cross the line into audit concern because they combine:
- high ownership density
- heavy integration responsibility
- performance sensitivity
- state management complexity

That combination sharply raises regression cost.

## Jobs / Burst Assessment

The project has real native/Burst credibility.

Evidence:
- widespread `NativeArray` / `NativeList` / `NativeHashMap` / `NativeQueue`
- multiple `BurstCompile` surfaces
- heavy world, fluid, fauna, voxel, and audio compute paths

The problem is not absence of jobs.
The problem is yield collapse from too many completion barriers.

Observed pattern:
- scheduling exists
- but many systems still call `.Complete()` aggressively
- this compresses async benefit back into frame stalls

Verdict:
- Jobs/Burst adoption is real
- Jobs/Burst discipline is incomplete

## Spatial Registry Risk

Fresh reverification exposed a more concrete current risk inside the world spatial stack.

Observed facts:
- `HectonSpatialHash.Register(...)` allocates handles through monotonic `_nextHandle++`
- no visible handle reuse or cap guard exists at allocation point
- reset of the native hash occurs only on subsystem registration
- `WorldSpatialHashGrid` accepts returned handles directly into its metadata registry

This does not prove that the current `SetResource` flood is caused by this handle path.
It does prove that the spatial registry has a long-session handle-growth risk that is not defended at the allocator boundary.

Verdict:
- current root cause is not proven
- current spatial-hash handle contract is still weak enough to deserve blocker status

## Zero-GC Reality

The project has strong local examples of disciplined allocation avoidance.

Best visible example:
- `SuitHUDV4CanvasOverlay.cs`

The project also still carries too many risk surfaces for a full â€œzero-GC hot pathâ€ trust declaration:
- coroutine presence is widespread at project scale
- native Unity loop usage still exists in important owners
- large integration owners increase hidden allocation probability
- scene/runtime clutter undermines confidence in strict lifetime control

Verdict:
- zero-GC is a serious mandate
- zero-GC is not yet a universally enforced property

## DOTS Reality

The DOTS lane is not dead conceptually.
It is dead operationally for current production confidence.

Evidence:
- `Hecton8.World.Dots.asmdef` exists
- active manifest does not include `com.unity.entities`
- `ScatterEntitiesSimulationBackend.cs` is a stub seam

Verdict:
- keep it as an R&D seam if needed
- do not count it as a live project strength

## Scene And Editor Hygiene

The active `02_HECTON_WORLD` scene contains real gameplay systems plus obvious residue:
- debug UI roots
- temporary preview roots
- trial/fabrication objects
- procedural proxies
- large manager concentration blocks

That is normal during development.
It is not acceptable as a long-term truth source for production confidence.

## Cinematic Collider Fake Standard

The gold standard for distant voxel collision is not more PhysX work. It is selective refusal to bake geometry the player cannot physically inspect.

Rule:
- near/interactable voxel chunks may enter the asynchronous physics bake chain.
- distant, noninteractive, or presentation-only voxel chunks must prefer a cinematic collider fake: cheap primitive/proxy collider, hazard distance check, or no collider at all if gameplay cannot reach it.
- synchronous `Physics.BakeMesh(...)` on the main thread is forbidden for runtime streaming.
- any `Physics.BakeMesh(...)` path must prove it is asynchronous/job-bound, loading-screen-only, editor-only, or replaced by the fake path above.

Current source scan:

```powershell
rg -n "Physics\.BakeMesh|BakeMesh" Assets/_Project/Scripts -g '*.cs'
```

Current review candidates:
- `HectonWorldGenerator.cs:554` calls `Physics.BakeMesh(MeshEntityId, false)` inside the bake job struct path.
- `HectonVoxelEngine.cs:3059` calls `UnityEngine.Physics.BakeMesh(MeshId, Convex)` inside `VoxelMeshBakeJob`.
- `HectonBrinePoolMeshGenerator.cs:353` calls `global::UnityEngine.Physics.BakeMesh(MeshId, false)` inside a mesh bake job path.

Static interpretation:
- these are not automatically main-thread runtime defects from the text scan alone.
- they remain hard audit points because the product rule is to skip mesh baking for distant/noncritical voxels and use a cinematic collider fake instead.

## What Is Ready, What Is Not

Ready enough to keep and harden:
- registry backbone
- dispatcher backbone
- save architecture
- audio architecture
- HUD formatting discipline

Not ready to trust without major cleanup:
- bootstrap ownership
- monolith world owners
- player owner sprawl
- DOTS production claims
- automated regression confidence
- doc-to-code synchronization discipline
