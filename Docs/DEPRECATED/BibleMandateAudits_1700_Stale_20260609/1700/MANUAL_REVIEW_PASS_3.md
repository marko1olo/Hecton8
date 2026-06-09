# Manual Review Pass 3

Status: HUMAN STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

This pass extends `MANUAL_REVIEW_PASS_1.md` and `MANUAL_REVIEW_PASS_2.md` into the remaining top hotspots. Static review can prove route shape, guard shape, and obvious editor-only boundaries. It cannot prove frame cost, GC allocation, GPU cost, build inclusion, prefab assignment, or device behavior.

## New Classifications

| File | Evidence | Static Classification | Required Next Gate |
|---|---|---|---|
| `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs` | `ScheduleThreatSpatialVisualSolvePhase()` is called from `HectonMapMagicVegetationBridge.SlowTick()`; allocations are H8Memory/Persistent scratch for threat, flow, thermal, echo, and voxel arrays; jobs complete through `DispatcherJobSwap.TryComplete`. | `YELLOW_BOUNDED_SLOW_TICK_NATIVE_ALLOCATION` | Convert recurring scratch to preallocated owner pools or prove allocation cadence/bytes under vegetation stress. |
| `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | Uses `ITickable`, `ISlowTickable`, `ILateFrameTickable`; slow tick schedules residency/HLOD/threat/flow jobs; late frame completes and swaps buffers. | `YELLOW_OWNER_PHASE_SHAPE_OK_PROOF_REQUIRED` | Profiler/native memory capture for slow tick and late-frame completion windows. |
| `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs` | Chunk build jobs are capped by bridge settings, but each build prepares native snapshots for masks, height, terrain holes, structures, and threat echo. | `YELLOW_BOUNDED_CHUNK_BUILD_ALLOCATION` | Pool chunk-build scratch or prove chunk-build stress does not allocate/grow repeatedly. |
| `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs` | Fields `_generateMeshAtRuntime` and `_generateImpostorMeshAtRuntime` allow runtime strip/card mesh creation; `BuildImpostorCardMesh()` creates `new Mesh`; GPU buffers/material property blocks are owner resources. | `YELLOW_RELEASE_PREFAB_ASSIGNMENT_REQUIRED` | Release prefabs must assign authored near/impostor meshes and disable runtime mesh fallback, or fallback must be editor/development-only. |
| `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs` | `EnsureGraphicsResources()` creates `_runtimeShellMesh = CreateCubeMesh()` and `_runtimeShellMaterial = new Material(shader)` when assets are not assigned; scratch buffers are scene-lifetime persistent arrays. | `YELLOW_RUNTIME_FALLBACK_ASSET_RISK` | Authored `shellMesh`/`shellMaterial` assignment proof for production scenes; runtime cube fallback is not acceptable as final visual output. |
| `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | `EnsureFaunaPresentationMaterials()` clones per-fauna runtime materials and later mutates effect scalars; acceptable for low-count hero actors only with proof. | `YELLOW_BATCHING_RISK` | Use shared materials plus MPB/GPU instance data for crowds, or prove fauna count/material clone cost and SRP batcher impact. |
| `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs` | Persistent job buffers and graphics buffers are allocated in setup; ping/fade jobs use dispatcher fences; mock SDF route exists. | `YELLOW_UI_SENSOR_PROOF_REQUIRED` | Ping spam profiler, GPU upload budget proof, and proof that mock SDF is diagnostic/fallback rather than release truth. |
| `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs` | Native state/scratch/black-box buffers are allocated during Awake/OnEnable; fixed and post-fixed dispatcher phases schedule/complete the kinematics job. | `GREENISH_OWNER_PHASE_WITH_PROOF_REQUIRED` | Fixed-step profiler and 300-frame black-box dump test; no static hot allocation found after setup. |
| `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs` | Creates a runtime root and adds ecosystem components if missing. | `YELLOW_BOOTSTRAP_PREFAB_ROUTE_REQUIRED` | Production scenes should include an authored root prefab; dynamic AddComponent route may remain only as bootstrap recovery with proof. |
| `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs` | Fixed raw/native bridge buffers are allocated in `Initialize()`; hot writes use pointers into fixed storage and telemetry ring. | `GREEN_STATIC_RING_BUFFER_SHAPE` | DSP/thread proof and overflow stress test still required; static shape matches zero-GC black-box law. |
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | `TryGetLatestCreated()` callsites found in editor/tuner/scanner files; core allocations are owner-lifetime. | `GREENISH_CORE_OWNER_WITH_GROWTH_COUNTER_REQUIRED` | Add/inspect growth counters for `EnsureGenerationHandle` and payload cache during gameplay stress. |
| `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs` | Uses static pools, DataVault handles, persistent obstacle snapshot pool, dispatcher job completion, and telemetry. | `YELLOW_RUNTIME_GROWTH_PROOF_REQUIRED` | Prove voxel volume registration/build does not allocate repeatedly under dynamic obstacle churn. |

## Static Truth After Pass 3

- Root bible routing remains complete. There are no missing root route files in the audit baseline.
- The source `.agents-skills` registry is not fully current as wording: many mandates lack explicit `GlobalQualityWeight` wording or contain legacy terms. Root bibles compensate, but the mandate files themselves still need refresh if they are to be treated as current direct authority.
- The codebase has strong owner-phase architecture in many systems, but several production risks are real because fallback runtime assets exist: wreck merged meshes, vegetation strip/card meshes, outpost cube shell meshes/materials, HUD chevron mesh, sonar/radar fallback material paths.
- Native allocations are often tracked and disposed. That is not enough for release acceptance. Repeated runtime H8Memory/NativeArray scratch allocation under pathing, radar, vegetation flow, and chunk stress must be converted to fixed pools or proven with profiler/native memory evidence.

## Current Priority

1. Replace or prove unreachable player-runtime mesh generation in `ProceduralWreckGenerator.cs`.
2. Force authored serialized mesh/material assignment for `HectonIndirectVegetationRenderer`, `MarauderOutpostGenerationService`, HUD chevrons, radar, and sonar production prefabs.
3. Convert vegetation/radar/path/chunk recurring native scratch to preallocated owner pools unless stress proof shows allocation count is zero after bootstrap.
4. Audit fauna material cloning and convert crowd-scale fauna to shared material plus MPB/GPU instance data.
5. Run Unity import/player/profiler proof only after the static fallback/ownership issues are closed.

## Non-Closure

This audit is still static. Nothing in this file proves that a release player has 0 B/frame GC, stable GPU cost, no runtime mesh construction, no sync load, correct collision proxies, or correct device behavior.
