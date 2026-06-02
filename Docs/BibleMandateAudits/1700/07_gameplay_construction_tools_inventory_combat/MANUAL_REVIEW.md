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

## Current Classification

- `SomaticKinematicsRuntime.cs`: `GREENISH_OWNER_PHASE_WITH_PROOF_REQUIRED`.
- `ScavengingLootOracle.cs`: `YELLOW_DATA_SOURCE_AND_STRESS_PROOF_REQUIRED`.
- `AutonomousExtractorSystem.cs`: `GREENISH_OWNER_PHASE_WITH_STRESS_PROOF_REQUIRED`.
- `FoundationPylonGpuBatch.cs`: `P0_FOUNDATION_MOCK_SDF_TRUTH_ROUTE`.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.
- `ConstructionRuntimeProxyFactory.cs`: `LEGAL_EDITOR_OR_DEVELOPMENT_BUILD_PROXY`.
- Remaining construction/extractor queue: `YELLOW_MANUAL_REVIEW_PENDING`.

## Required Next Proof

- 300-frame interaction/tool/loot profiler with GC Alloc visible.
- Save/load proof for item identities and construction state.
- Combat hitbox/proxy and tool query proof using NonAlloc/bounded routes.
- DataMonolith/encoded SDF proof for foundation snapping and pylon presentation; mock SDF must be disabled or dev-only in release.
- Release build symbol proof that `ConstructionRuntimeProxyFactory` is excluded from non-development player builds.
- Production drone provider proof for navigation/SDF/repair/mining/material data; mock buses may remain only for editor/test/headless diagnostics.

## Pass 6 Addendum - Gameplay Scene Lookup Boundary

- `ScavengingLootOracle.cs:1782` uses `Resources.FindObjectsOfTypeAll<GameObject>()` for HideAndDontSave orphan cleanup. Static context says reload cleanup, but it must be proven fault/reload-only and absent from interaction/scavenging hot paths.
- Tool/gameplay Temp payload routes found by the non-editor scan need callsite classification before profiler proof is meaningful.
