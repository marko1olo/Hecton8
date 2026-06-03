# Gameplay / Tools / Construction / Inventory / Combat Manual Review

Status: STATIC REVIEW - NO GAMEPLAY PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`
- `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs`
- hotspot queue references for construction/extractor systems from `HOTSPOT_REVIEW.md`
- `Assets/_Project/Scripts/Construction/AutonomousExtractorSystem.cs`
- `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorData.cs`
- `Assets/_Project/Scripts/Construction/FoundationSnappingCalculatorJobs.cs`
- `Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

## What Exists

- `SomaticKinematicsRuntime` uses fixed/post-fixed dispatcher phases, native state buffers, and a black-box ring. The reviewed hot path schedules a job in `FixedTick()` and completes in `PostFixedTick()` through `DispatcherJobFence`.
- `ScavengingLootOracle` uses fixed native request/result/telemetry storage and queued requests rather than allocating managed loot objects per interaction in the reviewed route.
- Root bibles exist for gameplay, player, tools, construction, inventory, combat, performance, telemetry, and quality.
- `AutonomousExtractorSystem` uses slow-tick ownership, cold fixed native state, and bounded contact collection in the reviewed route.
- `ConstructionRuntimeProxyFactory` is file-level guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, so its runtime proxy GameObjects, wirebox mesh, and material are not release-player legal unless the build is explicitly a development build.

## What Is Missing / Not Proven

- No first-20-min gameplay proof, interaction spam proof, construction graph proof, or combat hit/proxy proof was run.
- `ScavengingLootOracle` emergency/mock table routes must be proven editor/manual/fallback-only, not release data truth.
- Construction/extractor hotspots still need line-level classification beyond the static queue.
- `FoundationPylonGpuBatch` can fall back to `CreateDefaultMockSdfConfig()` and `MockSdfFallback` if encoded SDF is unavailable. Production construction snapping must not silently use mock terrain/substrate truth.
- `FoundationPylonGpuBatch` also creates a runtime pylon material fallback if no authored material is assigned.
- `DroneFleetManager` has mock repair/mining signal routes, mock SDF grid routes, fallback chassis specs, and a procedural material route. Production automation cannot use mock data as normal gameplay truth.
- The 193 runtime suspect lines are now classified in `LINE_LEVEL_CLASSIFICATION.md`, but this is static source evidence only.
- `EconomyRuntimeInstaller.EnsureRuntimeSystems()` can create `__HECTON_ECONOMY_RUNTIME` and add economy managers if missing. This is classified as cold fail-safe recovery, not as release scene composition proof.
- `SubmarineCoreDirector.CacheReferences()` can auto-add `SubmarineAutoLevelBallastController` when the legacy PhysX auto-level flag is set. This is classified as cold legacy repair, not as proof that release submarines are authored correctly.

## Current Classification

- `SomaticKinematicsRuntime.cs`: `GREENISH_OWNER_PHASE_WITH_PROOF_REQUIRED`.
- `ScavengingLootOracle.cs`: `YELLOW_DATA_SOURCE_AND_STRESS_PROOF_REQUIRED`.
- `AutonomousExtractorSystem.cs`: `GREENISH_OWNER_PHASE_WITH_STRESS_PROOF_REQUIRED`.
- `FoundationPylonGpuBatch.cs`: `P0_FOUNDATION_MOCK_SDF_TRUTH_ROUTE`.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.
- `ConstructionRuntimeProxyFactory.cs`: `LEGAL_EDITOR_OR_DEVELOPMENT_BUILD_PROXY`.
- Runtime suspect line queue: `YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`.

## Required Next Proof

- 300-frame interaction/tool/loot profiler with GC Alloc visible.
- Save/load proof for item identities and construction state.
- Combat hitbox/proxy and tool query proof using NonAlloc/bounded routes.
- DataMonolith/encoded SDF proof for foundation snapping and pylon presentation; mock SDF must be disabled or dev-only in release.
- Release build symbol proof that `ConstructionRuntimeProxyFactory` is excluded from non-development player builds.
- Production drone provider proof for navigation/SDF/repair/mining/material data; mock buses may remain only for editor/test/headless diagnostics.
- Authored economy runtime root proof and counters showing `EconomyRuntimeInstaller` does not assemble normal release scenes.
- Authored submarine ballast/compound collider proof and counters showing legacy auto-install/collider cache rebuilds do not execute as normal gameplay composition.

## Pass 6 Addendum - Gameplay Scene Lookup Boundary

- `ScavengingLootOracle.cs:1782` uses `Resources.FindObjectsOfTypeAll<GameObject>()` for HideAndDontSave orphan cleanup. Static context says reload cleanup, but it must be proven fault/reload-only and absent from interaction/scavenging hot paths.
- Tool/gameplay Temp payload routes found by the non-editor scan need callsite classification before profiler proof is meaningful.

## Pass 7 Addendum - Scavenging Host And Scratch Detail

- `ScavengingLootOracle` native scratch is fixed-size persistent storage for requests, resolved yields, and telemetry. That static shape is good for gameplay interaction, but it still needs 300-frame interaction proof.
- `PostSimulationTick()` completes publish work through non-forced `DispatcherJobFence.TryComplete`, so the reviewed publish route is not a same-frame forced completion defect.
- `EnsureHost()` creates a `HideAndDontSave` runtime GameObject/MonoBehaviour and `DestroyUnboundHostObjectsCold()` scans all GameObjects for orphan cleanup. Production should author/bootstrap this host or prove the creation/scan only happens during boot/reload.

## Pass 10 Addendum - Somatic Kinematics And Extractor Owner Phases

- `SomaticKinematicsRuntime` has a strong owner-phase shape: fixed/post-fixed dispatcher registration, fixed persistent local scratch arrays, SignalBus publication, and black-box buffer ownership. The open risk is not static architecture; it is runtime proof that `PostFixedTick()` completion and forced completion during teardown/origin-shift do not become hidden stalls.
- `AutonomousExtractorSystem` has a fixed-capacity SOA shape: `MaxModuleCapacity = 256`, persistent native arrays allocated once by `EnsureExtractorNativeStateCold()`, slow-tick job scheduling, and non-forced completion on the next slow tick. This is `GREENISH_FIXED_CAPACITY_SLOWTICK_WITH_STRESS_PROOF_REQUIRED`.
- Required closure for both systems: 300-frame gameplay stress with native memory counters, job completion telemetry, 0 B/frame after bootstrap, origin-shift/teardown force-complete evidence, and extractor module registration/unregistration churn at/near capacity.

## Pass 12 Addendum - Construction Preview GPU And Material Lifecycle

- `FoundationPylonGpuBatch` uses double-buffered matrix/surface/args `GraphicsBuffer` routes and `GraphicsBufferUploadUtility.UploadNativeArray()`. This is structurally aligned with GPU bandwidth discipline, but `EnsureGraphicsBuffers()` can recreate buffers when capacity changes and `EnsurePylonMaterial()` creates a runtime fallback material.
- `HectonBlueprintPreviewBatch` and `VRPipeBlueprintPreview` also use double-buffered state/visual/args buffers and late-frame upload. Both can create runtime preview materials when serialized materials are missing.
- Current classification: `YELLOW_CONSTRUCTION_PREVIEW_GPU_BUFFER_MATERIAL_PROOF_REQUIRED`.
- Required closure: authored preview/pylon materials, fixed buffer capacity proof, no post-bootstrap capacity churn, no recurring material creation under enable/disable, compact/high GPU captures, and construction preview readability proof.

## Pass 20 Addendum - Line-Level Runtime Closure

- Added `LINE_LEVEL_CLASSIFICATION.md`.
- Classified all 193 runtime suspect lines: 124 editor/dev guarded, 55 cold/setup/fault/owner-lifetime, 14 false positives, and 0 new runtime violations.
- Strengthened `RB-008` for `EconomyRuntimeInstaller` dynamic economy root/components.
- Strengthened `RB-130` for `SubmarineCoreDirector` legacy PhysX auto-level component auto-install.
- This closes the static line queue only. It does not close gameplay, construction, economy, combat, or tool runtime proof.
