# SINGLETON WITCH HUNT â€” FIRST-PARTY VIOLATIONS
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



**Status:** PENDING VERIFICATION
**Audit Review Date:** 2026-04-29
**Chronology Note:** The previous header claimed `2026-05-02`, which is impossible relative to the current workspace date. Treat this file as a static inventory until re-scanned.
**Rule:** AGENTS.md Â§ PRIME DIRECTIVES â€” "[FORBID] Classic Singletons and Awake() self-registration. [REQ] Managers accessed via GlobalRegistry."

---

## EXECUTIVE SUMMARY

**Violation Count:** 101 listed rows in this historical document expose `public static Instance` (or equivalent) instead of routing through `GlobalRegistry`.

**2026-05-04 Source Delta:** This file is not a live count. The May 3 source scan for `Assets/_Project/Scripts/Optimization` reported no `_instance`, `Instance =>`, `public static .*Instance`, `internal static .*Instance`, `DontDestroyOnLoad(`, or `SINGLETON` matches. The May 4 post-repair foundation guard scan reports `Optimization singleton residue = 0` and exits `0`. Rows for `CameraRTManager`, `PostFXRTManager`, `RenderTextureLifecycleTracker`, `RenderTexturePool`, `UIRTManager`, `VRAMMonitor`, and `VisorRTManager` are superseded by `Docs/Reports/2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`, the May 4 sweep, and `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.

**Categories:**
- Core infrastructure (tick, physics, dispatcher)
- World / environment (ocean, terrain, atmosphere, biolum)
- Player systems (inventory, movement, PDA, visor)
- Meta / progression (achievements, difficulty, profile, run modifiers)
- UI / HUD (integrity, subtitles, settings, tooltip)
- Optimization (RT managers, VRAM monitor, culling, LOD)
- Economy / construction (scrap, scarcity, power, construction)
- Narrative / audio (lore, music, audio logs, soundscape)

---

## VIOLATION LIST (Alphabetical by Script Path)

| # | Script Path | Class | Instance Pattern | Self-Reg Method |
|---|---|---|---|---|
| 1 | `AmbientWaterMotionManager.cs` | `AmbientWaterMotionManager` | `public static Instance => _instance` | `Awake` |
| 2 | `AcousticZoneController.cs` | `AcousticZoneController` | `public static Instance { get; }` | `Awake` |
| 3 | `Atlas6DirectiveSystem.cs` | `Atlas6DirectiveSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 4 | `AtlasSignalDecoder.cs` | `AtlasSignalDecoder` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 5 | `AtlasSignalSystem.cs` | `AtlasSignalSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 6 | `AsyncLoadHelper.cs` | `AsyncLoadHelper` | `public static Instance { get; }` | `Awake` |
| 7 | `AudioLogSystem.cs` | `AudioLogSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 8 | `AbyssalFluidDecalManager.cs` | `AbyssalFluidDecalManager` | `public static Instance => _instance` | `Awake` |
| 9 | `AbyssalThermalManager.cs` | `AbyssalThermalManager` | `public static Instance => _instance` | `Awake` |
| 10 | `BaseIntegrityHUD.cs` | `BaseIntegrityHUD` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 11 | `BasePollutionManager.cs` | `BasePollutionManager` | `public static Instance => _instance` | `Awake` |
| 12 | `BeaconNetworkSystem.cs` | `BeaconNetworkSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 13 | `BootstrapController.cs` | `BootstrapController` | `public static Instance => _instance` | `Awake` |
| 14 | `CameraJuiceSystem.cs` | `CameraJuiceSystem` | `public static Instance => _instance` | `Awake` |
| 15 | `CameraRTManager.cs` | `CameraRTManager` | `public static Instance => _instance` | `Awake` |
| 16 | `ConstructionManager.cs` | `ConstructionManager` | `public static Instance { get; }` | `Awake` |
| 17 | `CorporateOrderSystem.cs` | `CorporateOrderSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 18 | `CullingManager.cs` | `CullingManager` | `public static Instance => _instance` | `Awake` |
| 19 | `DepthZoneDirector.cs` | `DepthZoneDirector` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 20 | `DynamicDifficultyDirector.cs` | `DynamicDifficultyDirector` | `public static Instance => _instance` | `Awake` |
| 21 | `DynamicResolutionScaler.cs` | `DynamicResolutionScaler` | `public static Instance => _instance` | `Awake` |
| 22 | `EcosystemHealthDirector.cs` | `EcosystemHealthDirector` | `public static Instance => _instance` | `Awake` |
| 23 | `EmergencyServiceRelayDirector.cs` | `EmergencyServiceRelayDirector` | `public static Instance => ResolveInstance()` | `Awake` |
| 24 | `EndingSystem.cs` | `EndingSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 25 | `EntityChangeDetector.cs` | `EntityChangeManager` | `public static Instance { get; }` | `Awake` |
| 26 | `EnvironmentalStrainManager.cs` | `EnvironmentalStrainManager` | `public static Instance => _instance` | `Awake` |
| 27 | `EclipseGameplaySystem.cs` | `EclipseGameplaySystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 28 | `FaunaGeneticsManager.cs` | `FaunaGeneticsManager` | `public static Instance => _instance` | `Awake` |
| 29 | `FieldOperationLogSystem.cs` | `FieldOperationLogSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 30 | `FirstHourDirector.cs` | `FirstHourDirector` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 31 | `FlowFieldVisualizer.cs` | `FlowFieldVisualizer` | `public static Instance { get; }` | `Awake` |
| 32 | `GameTickManager.cs` | `GameTickManager` | `public static Instance { get; }` | `Awake` |
| 33 | `GlobalPhysicsStateManager.cs` | `GlobalPhysicsStateManager` | `public static Instance => _instance` | `Awake` |
| 34 | `GlobalProfileManager.cs` | `GlobalProfileManager` | `public static Instance => _instance` | `Awake` |
| 35 | `HectonAtmosphereManager.cs` | `HectonAtmosphereManager` | `public static Instance { get; }` | `Awake` |
| 36 | `HectonBiolumController.cs` | `HectonBiolumController` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 37 | `HectonBiolumManager.cs` | `HectonBiolumManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 38 | `HectonDiscoveryManager.cs` | `HectonDiscoveryManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 39 | `HectonFloatingOrigin.cs` | `HectonFloatingOrigin` | `public static Instance => _instance` | `Awake` |
| 40 | `HectonFluidEngine.cs` | `HectonFluidEngine` | `public static Instance { get; }` | `Awake` |
| 41 | `HectonMusicDirector.cs` | `HectonMusicDirector` | `public static Instance => _instance` | `Awake` |
| 42 | `HectonNarrativeDirector.cs` | `HectonNarrativeDirector` | `public static Instance => _instance` | `Awake` |
| 43 | `HectonNetworkManager.cs` | `HectonNetworkManager` | `public static Instance { get; private set; }` | `Awake` |
| 44 | `HectonRockManager.cs` | `HectonRockManager` | `public static Instance { get; }` | `Awake` |
| 45 | `HectonSurfaceWeatherDirector.cs` | `HectonSurfaceWeatherDirector` | `public static Instance => _instance` | `Awake` |
| 46 | `HazardZoneManager.cs` | `HazardZoneManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 47 | `ImpostorSystem.cs` | `ImpostorSystem` | `public static Instance => _instance` | `Awake` |
| 48 | `InputManager.cs` | `InputManager` | `public static Instance { get; }` | `Awake` |
| 49 | `LODSystemManager.cs` | `LODSystemManager` | `public static Instance => _instance` | `Awake` |
| 50 | `LocalizationManager.cs` | `LocalizationManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 51 | `LoreDatabaseManager.cs` | `LoreDatabaseManager` | `public static Instance => _instance` | `Awake` |
| 52 | `MapMagicBridge.cs` | `MapMagicBridge` | `public static Instance { get; }` | `Awake` |
| 53 | `MigrationDirector.cs` | `MigrationDirector` | `public static Instance => _instance` | `Awake` |
| 54 | `MissionManager.cs` | `MissionManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 55 | `ModWorldPersistenceManager.cs` | `ModWorldPersistenceManager` | `public static Instance => _instance` | `Awake` + `[RuntimeInitialize...]` |
| 56 | `ObjectPoolManager.cs` | `ObjectPoolManager` | `public static Instance { get; }` | `Awake` |
| 57 | `PDAExchangeSystem.cs` | `PDAExchangeSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 58 | `PDALogbookManager.cs` | `PDALogbookManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 59 | `PDAMarkerRegistry.cs` | `PDAMarkerRegistry` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 60 | `PerformanceBudgetController.cs` | `PerformanceBudgetController` | `public static Instance { get; private set; }` | `Awake` |
| 61 | `PerformanceMonitor.cs` (Scripts/) | `PerformanceMonitor` | `public static Instance => _instance` | `Awake` + `[RuntimeInitialize...]` |
| 62 | `PerformanceMonitor.cs` (Tools/) | `PerformanceMonitor` | `public static Instance { get; private set; }` | `Awake` |
| 63 | `PlayerActionController.cs` | `PlayerActionController` | `public static Instance => _instance` | `Awake` |
| 64 | `PlayerExplorationTracker.cs` | `PlayerExplorationTracker` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 65 | `PlayerExpressionManager.cs` | `PlayerExpressionManager` | `public static Instance => _instance` | `Awake` |
| 66 | `PlayerInventory.cs` | `PlayerInventory` | `public static Instance => _instance` | `Awake` |
| 67 | `PostFXRTManager.cs` | `PostFXRTManager` | `public static Instance => _instance` | `Awake` |
| 68 | `PowerGridManager.cs` | `PowerGridManager` | `public static Instance { get; }` | `Awake` |
| 69 | `PrefabRegistry.cs` | `PrefabRegistry` | `public static Instance { get; }` | `Awake` |
| 70 | `QuestManager.cs` | `QuestManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 71 | `RandomEventSystem.cs` | `RandomEventSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 72 | `RaycastBatchHelper.cs` | `RaycastBatchHelper` | `public static Instance => _instance` | `Awake` + `[RuntimeInitialize...]` |
| 73 | `RenderTextureLifecycleTracker.cs` | `RenderTextureLifecycleTracker` | `public static Instance => _instance` | `Awake` |
| 74 | `RenderTexturePool.cs` | `RenderTexturePool` | `public static Instance => _instance` | `Awake` |
| 75 | `RebindingManager.cs` | `RebindingManager` | `public static Instance { get; }` | `Awake` |
| 76 | `ResourceScarcityDirector.cs` | `ResourceScarcityDirector` | `public static Instance => _instance` | `Awake` |
| 77 | `RunModifierController.cs` | `RunModifierController` | `public static Instance => _instance` | `Awake` |
| 78 | `SaveManager.cs` | `SaveManager` | `public static Instance => _instance` | `Awake` |
| 79 | `ScavengePopulator.cs` | `ScavengePopulator` | `public static Instance { get; }` | `Awake` |
| 80 | `ScanLogSystem.cs` | `ScanLogSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 81 | `SceneTransitionVerifier.cs` | `SceneTransitionVerifier` | `public static Instance { get; private set; }` | `Awake` |
| 82 | `SettingsManager.cs` | `SettingsManager` | `public static Instance { get; }` | `Awake` |
| 83 | `SoundscapeSystem.cs` | `SoundscapeSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 84 | `SpatialAudioManager.cs` | `SpatialAudioManager` | `public static Instance { get; }` | `Awake` |
| 85 | `SpectrumSystem.cs` | `SpectrumSystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 86 | `StateRecoveryVerifier.cs` | `StateRecoveryVerifier` | `public static Instance { get; private set; }` | `Awake` |
| 87 | `SubtitleManager.cs` | `SubtitleManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 88 | `SuitUpgradeManager.cs` | `SuitUpgradeManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 89 | `SystemDispatcher.cs` | `SystemDispatcher` | `public static Instance => _instance` | `[RuntimeInitialize...]` |
| 90 | `RenderDispatcher` (nested) | `RenderDispatcher` | `public static Instance => _instance` | `[RuntimeInitialize...]` |
| 91 | `ToolDurabilitySystem.cs` | `ToolDurabilitySystem` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 92 | `UITooltip.cs` | `UITooltip` | `public static Instance => _instance` | `Awake` |
| 93 | `UIRTManager.cs` | `UIRTManager` | `public static Instance => _instance` | `Awake` |
| 94 | `UserOptionsPersistence.cs` | `UserOptionsPersistence` | `public static Instance { get; }` | `Awake` |
| 95 | `VRAMMonitor.cs` | `VRAMMonitor` | `public static Instance => _instance` | `Awake` |
| 96 | `VisorRTManager.cs` | `VisorRTManager` | `public static Instance => _instance` | `Awake` |
| 97 | `WorldStateManager.cs` | `WorldStateManager` | `public static Instance { get; }` | `Awake` |
| 98 | `HectonBiolumManager` | `HectonBiolumManager` | `public static Instance { get; private set; }` | `Awake` + `[RuntimeInitialize...]` |
| 99 | `SargassumCutManager.cs` | `SargassumCutManager` | `public static Instance => _instance` | `Awake` |
| 100 | `SargassumGlobalDragManager.cs` | `SargassumGlobalDragManager` | `public static Instance => _instance` | `Awake` |
| 101 | `PersistentWorldRegistry.cs` | `PersistentWorldRegistry` | `public static Instance => _instance` | `Awake` |

---

## EXCLUSIONS (NOT COUNTED AS VIOLATIONS)

| Class | Reason |
|---|---|
| `GlobalRegistry` | Explicit service-locator pattern; mandated by AGENTS.md. |
| `HectonEventBus` | Static dispatcher, not a MonoBehaviour singleton. |
| `HectonAPI` | Static modding surface; no runtime instance. |

---

## REMEDIATION COST

- **High-touch systems** (tick, physics, player, world): Refactor to `GlobalRegistry.Register/Unregister` in bootstrap. Risk: execution-order regressions.
- **Low-touch systems** (verifiers, debug HUDs): Delete or merge into existing GlobalRegistry services.

**Recommendation:** Do NOT refactor all 101 violations in one pass. Batch by subsystem (Core â†’ Player â†’ World â†’ UI) with per-batch MCP validation.

---

*Report generated by ARCHIVARIUS. No optimism. Facts only.*
