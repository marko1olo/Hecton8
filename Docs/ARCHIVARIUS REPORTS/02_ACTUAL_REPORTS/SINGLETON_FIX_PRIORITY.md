# SINGLETON ELIMINATION ROADMAP — HECTON-8

## Current-State Addendum (2026-04-29)

This roadmap remains useful as a singleton-removal planning artifact, but some service-slot guidance inside it is too broad for the current architecture truth.

Current direct ownership already rechecked in source:

- `SpatialAudioManager -> IAudioService`
- `SuitHUDV4CanvasOverlay -> IUIService`

Because of that, this document should not be read as permission to turn `IAudioService` or `IUIService` into catch-all buckets for every audio-adjacent or UI-adjacent singleton.

Use current direct-owner docs first:

- `INTERFACE_HEALTH_DASHBOARD.md`
- `INTERFACE_CONTRACT_TABLE.md`
- `INTERFACE_STRATEGY.md`
- `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`

Status remains `PENDING VERIFICATION`.

**Status:** PENDING VERIFICATION  
**Authority:** CTO / Lead Architect  
**Rule Basis:** AGENTS.md § PRIME DIRECTIVES — "[FORBID] Classic Singletons and Awake() self-registration. [REQ] Managers accessed via GlobalRegistry."  
**Source Violations:** `SINGLETON_VIOLATIONS.md` (101 first-party violations)  
**Mandates Followed:** AGENTS.md [RULE] MANDATE CONTEXTUAL INGESTION, [RULE] ARCHITECTURE FIRST, [RULE] NO OPTIMISM.

---

## EXECUTIVE SUMMARY

**101 violations** mapped to **3 Tiers**. Tier 1 must move to `GlobalRegistry` typed properties + explicit `Register/Unregister` in `GameBootstrapper.Initialize()` immediately. Tier 2 and 3 are sequenced by blast radius.

**WARNING:** `GlobalRegistry` does **not** expose a generic `Get<T>()` today. It exposes typed properties (`GlobalRegistry.Audio`, `GlobalRegistry.Save`, etc.) and typed `Register/Unregister` methods. The snippets below use the **actual API**. If a generic accessor is required, it must be added to `GlobalRegistry.cs` first.

---

## TIER 1 — CRITICAL (Thread Safety / Tick / Physics / AUP / Dispatch)

**Rationale:** These systems are accessed from Burst jobs, native containers, physics callbacks, or origin-shift handlers. Awake-order races here cause deterministic crashes, not soft failures.

| # | Class | File | Why Critical | Registry Target |
|---|-------|------|--------------|-----------------|
| 1 | `GameTickManager` | `GameTickManager.cs` | Every `ITickable` registration routes here. Self-registration in `Awake` creates init-order hell. | `GlobalRegistry.RegisterUpdatable/RegisterSlowTickable` (already exists) |
| 2 | `SystemDispatcher` | `SystemDispatcher.cs` | Owns dispatch lanes. `[RuntimeInitializeOnLoadMethod]` self-reg bypasses bootstrap. | Must be bootstrapped via `GameBootstrapper.Initialize()`; lanes cleared by `GlobalRegistry.ResetStaticState()` |
| 3 | `RenderDispatcher` | nested in `SystemDispatcher.cs` | Same as above; render lane corruption = frame stutter. | Same as SystemDispatcher |
| 4 | `GlobalPhysicsStateManager` | `GlobalPhysicsStateManager.cs` | Physics query constants, layer masks, gravity overrides. Burst jobs read these. | `GlobalRegistry.RegisterPhysicsService(IPhysicsService)` |
| 5 | `HectonFloatingOrigin` | `HectonFloatingOrigin.cs` | `CurrentTotalOffset` is read by AUP math in jobs. Singleton access in job structs is a data race. | `GlobalRegistry.RegisterEnvironmentRuntimeContext` or new `RegisterFloatingOriginService` |
| 6 | `PersistentWorldRegistry` | `PersistentWorldRegistry.cs` | Owns `NativeArray` hydration slots. `Awake` self-reg + `Allocator.Persistent` = leak on duplicate. | `GlobalRegistry.RegisterEnvironmentRuntimeContext` |
| 7 | `ObjectPoolManager` | `ObjectPoolManager.cs` | Spawning path is hot. `Instance` access from jobs is forbidden (managed ref). | `GlobalRegistry` does not have a pool slot; add `IPoolService` interface + `RegisterPoolService` |
| 8 | `RaycastBatchHelper` | `RaycastBatchHelper.cs` | Physics job scheduling. Singleton access in job gather phase is not thread-safe. | `GlobalRegistry.RegisterPhysicsService` (extend `IPhysicsService` to expose batch helper) |
| 9 | `InputManager` | `InputManager.cs` | `Awake` self-reg + action-map state. Race = null-ref on first frame input. | `GlobalRegistry.RegisterInputService(IInputService)` |
| 10 | `BootstrapController` | `BootstrapController.cs` | Orchestrates init order. If this is a singleton, the orchestrator has no orchestrator. | Keep as explicit field in `GameBootstrapper`; remove `public static Instance` |
| 11 | `SaveManager` | `SaveManager.cs` | Binary write path. `Awake` self-reg + background thread = file handle race. | `GlobalRegistry.RegisterSaveService(ISaveService)` |
| 12 | `MapMagicBridge` | `MapMagicBridge.cs` | Terrain height queries in AUP space. Burst vegetation jobs call this. | `GlobalRegistry.RegisterEnvironmentRuntimeContext` or new `RegisterTerrainService` |
| 13 | `SpatialAudioManager` | `SpatialAudioManager.cs` | Native DSP graph + SPSC queues. `Awake` init of DSP nodes must be sequenced after audio device init. | `GlobalRegistry.RegisterAudioService(IAudioService)` |

---

### TIER 1 — MIGRATION SNIPPETS (Actual GlobalRegistry API)

#### 1. GameTickManager
```csharp
// BEFORE (SuitHUDV4CanvasOverlay.cs → AutoResolve)
GameTickManager.Instance.RegisterTickable(this); // FORBIDDEN overload

// AFTER
GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
// In OnDisable:
GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
```

#### 2. SystemDispatcher
```csharp
// BEFORE
SystemDispatcher.Instance.Register(this, PriorityLayer.Environment);

// AFTER
// Remove [RuntimeInitializeOnLoadMethod] self-registration.
// GameBootstrapper.Initialize() calls:
SystemDispatcher.EnsureRuntimeInstance();
// Consumers use:
GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
```

#### 3. GlobalPhysicsStateManager
```csharp
// BEFORE
GlobalPhysicsStateManager.Instance.SetGravityVector(down);

// AFTER
// In GameBootstrapper.Initialize():
GlobalRegistry.RegisterPhysicsService(physicsManager);
// Consumer:
GlobalRegistry.Physics?.SetGravityVector(down);
```

#### 4. HectonFloatingOrigin
```csharp
// BEFORE (in FaunaDirector.cs)
Vector3 aup = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);

// AFTER
// HectonFloatingOrigin implements IEnvironmentRuntimeContext
// GameBootstrapper.Initialize():
GlobalRegistry.RegisterEnvironmentRuntimeContext(floatingOrigin);
// Consumer:
Vector3 aup = GlobalRegistry.Environment?.ToAbsoluteUniversePosition(pos) ?? pos;
```

#### 5. PersistentWorldRegistry
```csharp
// BEFORE
PersistentWorldRegistry.Instance.TryRegisterDroppedItem(...);

// AFTER
// In GameBootstrapper.Initialize():
GlobalRegistry.RegisterEnvironmentRuntimeContext(worldRegistry);
// Consumer:
GlobalRegistry.Environment?.TryRegisterDroppedItem(...);
```

#### 6. ObjectPoolManager
```csharp
// BEFORE
ObjectPoolManager.Instance.Spawn(prefab, pos, rot);

// AFTER
// Add to GlobalRegistry:
public static void RegisterPoolService(IPoolService instance) { ... }
public static IPoolService Pool => _pool;
// GameBootstrapper.Initialize():
GlobalRegistry.RegisterPoolService(poolManager);
// Consumer:
GlobalRegistry.Pool?.Spawn(prefab, pos, rot);
```

#### 7. RaycastBatchHelper
```csharp
// BEFORE
RaycastBatchHelper.Instance.ScheduleBatch(commands, count);

// AFTER
// Extend IPhysicsService to expose batch scheduling:
GlobalRegistry.Physics?.ScheduleRaycastBatch(commands, count);
// Or register helper explicitly:
GlobalRegistry.RegisterPhysicsService(raycastHelper); // if helper implements IPhysicsService
```

#### 8. InputManager
```csharp
// BEFORE
InputManager.Instance.GetActionState(actionId);

// AFTER
// GameBootstrapper.Initialize():
GlobalRegistry.RegisterInputService(inputManager);
// Consumer:
GlobalRegistry.Input?.GetActionState(actionId);
```

#### 9. SaveManager
```csharp
// BEFORE
SaveManager.Instance.CaptureSaveSnapshot();

// AFTER
// GameBootstrapper.Initialize():
GlobalRegistry.RegisterSaveService(saveManager);
// Consumer:
GlobalRegistry.Save?.CaptureSaveSnapshot();
```

#### 10. MapMagicBridge
```csharp
// BEFORE
float h = MapMagicBridge.Instance.SampleHeightAUP(aup);

// AFTER
// Extend IEnvironmentRuntimeContext or add ITerrainService:
GlobalRegistry.Environment?.SampleHeightAUP(aup);
```

#### 11. SpatialAudioManager
```csharp
// BEFORE
SpatialAudioManager.Instance.PlayAtPoint(clip, pos, volume);

// AFTER
// GameBootstrapper.Initialize():
GlobalRegistry.RegisterAudioService(spatialAudioManager);
// Consumer:
GlobalRegistry.Audio?.PlayAtPoint(clip, pos, volume);
```

#### 12. BootstrapController
```csharp
// BEFORE
BootstrapController.Instance.ShowFatalError(msg);

// AFTER
// Remove public static Instance entirely.
// Access only through GameBootstrapper injected reference:
_gameBootstrapper.ShowFatalError(msg);
```

---

## TIER 2 — FUNCTIONAL (Gameplay Managers)

**Rationale:** These affect game state but do not sit on the thread-boundary. Can wait for next refactor sprint. Migrate in subsystem batches (Player → World → Economy).

| # | Class | File | Blast Radius if Changed |
|---|-------|------|------------------------|
| 1 | `PlayerInventory` | `PlayerInventory.cs` | HUD, PDA, Crafting, Construction, Quickbar |
| 2 | `PlayerActionController` | `PlayerActionController.cs` | Interaction, Tools, Builder |
| 3 | `HectonSurvivalSystem` | `HectonSurvivalSystem.cs` | HUD vitals, death logic, depth/pressure |
| 4 | `HectonPlayerMovement` | `HectonPlayerMovement.cs` | Camera, Physics, Floating Origin, Visor |
| 5 | `HectonFluidEngine` | `HectonFluidEngine.cs` | Buoyancy, Currents, Submarine dynamics |
| 6 | `HectonAtmosphereManager` | `HectonAtmosphereManager.cs` | Weather, Biome visuals, Sky |
| 7 | `HectonSurfaceWeatherDirector` | `HectonSurfaceWeatherDirector.cs` | Storms, Wind, Wave params |
| 8 | `HectonBiolumManager` | `HectonBiolumManager.cs` | Flora shaders, Cave lighting |
| 9 | `HectonNarrativeDirector` | `HectonNarrativeDirector.cs` | Dialog, Lore triggers, Cutscenes |
| 10 | `ConstructionManager` | `ConstructionManager.cs` | Builder tool, Base modules, Power |
| 11 | `PowerGridManager` | `PowerGridManager.cs` | All powered devices, Batteries |
| 12 | `MissionManager` | `MissionManager.cs` | Objectives, PDA tabs, Rewards |
| 13 | `QuestManager` | `QuestManager.cs` | Progression, Unlock trees |
| 14 | `HectonDiscoveryManager` | `HectonDiscoveryManager.cs` | Scan log, Encyclopedia |
| 15 | `BeaconNetworkSystem` | `BeaconNetworkSystem.cs` | Navigation, Waypoints |
| 16 | `HazardZoneManager` | `HazardZoneManager.cs` | Damage, Status effects |
| 17 | `DynamicDifficultyDirector` | `DynamicDifficultyDirector.cs` | Spawn rates, Resource scarcity |
| 18 | `EcosystemHealthDirector` | `EcosystemHealthDirector.cs` | Fauna behavior, Flora state |
| 19 | `ResourceScarcityDirector` | `ResourceScarcityDirector.cs` | Loot tables, Drop rates |
| 20 | `RunModifierController` | `RunModifierController.cs` | New-game modifiers |
| 21 | `FaunaGeneticsManager` | `FaunaGeneticsManager.cs` | Creature variants |
| 22 | `ScavengePopulator` | `ScavengePopulator.cs` | World loot placement |
| 23 | `HectonRockManager` | `HectonRockManager.cs` | Procedural geology |
| 24 | `SargassumCutManager` | `SargassumCutManager.cs` | Vegetation interaction |
| 25 | `SargassumGlobalDragManager` | `SargassumGlobalDragManager.cs` | Flora physics |
| 26 | `HectonNetworkManager` | `HectonNetworkManager.cs` | Multiplayer (if enabled) |
| 27 | `ModWorldPersistenceManager` | `ModWorldPersistenceManager.cs` | Mod save data |
| 28 | `WorldStateManager` | `WorldStateManager.cs` | Global world flags |

---

## TIER 3 — UI / VISUAL (Secondary Feedback)

**Rationale:** These are presentation-only. Lowest blast radius. Merge or delete where possible.

**Current-state safety note:** the recommendation column below is an old migration sketch, not a literal service-slot assignment contract.  
Direct current owners already confirmed in source remain:

- `SuitHUDV4CanvasOverlay -> IUIService`
- `SpatialAudioManager -> IAudioService`

Do not treat every `Register as IUIService` or `Register as IAudioService extension` line below as current approved architecture without a fresh owner-by-owner review.

| # | Class | File | Recommended Action |
|---|-------|------|-------------------|
| 1 | `BaseIntegrityHUD` | `BaseIntegrityHUD.cs` | Merge into `SuitHUDV4CanvasOverlay` or register as `IUIService` |
| 2 | `SubtitleManager` | `SubtitleManager.cs` | Register as `IUIService` |
| 3 | `UITooltip` | `UITooltip.cs` | Register as `IUIService` |
| 4 | `PerformanceMonitor` (Scripts) | `PerformanceMonitor.cs` | Move to `GlobalRegistry` debug overlay or delete |
| 5 | `PerformanceMonitor` (Tools) | `PerformanceMonitor.cs` | Editor-only; remove `Instance`, use `FindAnyObjectByType` in editor tools |
| 6 | `UIRTManager` | `UIRTManager.cs` | Merge into `VisorRTManager` or register as `IUIService` |
| 7 | `VisorRTManager` | `VisorRTManager.cs` | Register as `IUIService` |
| 8 | `CameraRTManager` | `CameraRTManager.cs` | Register as `IUIService` |
| 9 | `PostFXRTManager` | `PostFXRTManager.cs` | Register as `IUIService` |
| 10 | `RenderTexturePool` | `RenderTexturePool.cs` | Move to `GlobalRegistry` as `IRenderTextureService` |
| 11 | `RenderTextureLifecycleTracker` | `RenderTextureLifecycleTracker.cs` | Merge into `RenderTexturePool` |
| 12 | `CullingManager` | `CullingManager.cs` | Register as `IEnvironmentRuntimeContext` extension |
| 13 | `LODSystemManager` | `LODSystemManager.cs` | Register as `IEnvironmentRuntimeContext` extension |
| 14 | `ImpostorSystem` | `ImpostorSystem.cs` | Register as `IEnvironmentRuntimeContext` extension |
| 15 | `DynamicResolutionScaler` | `DynamicResolutionScaler.cs` | Register as `IUIService` or `IEnvironmentRuntimeContext` |
| 16 | `PerformanceBudgetController` | `PerformanceBudgetController.cs` | Register as `IEnvironmentRuntimeContext` |
| 17 | `VRAMMonitor` | `VRAMMonitor.cs` | Editor-only overlay; remove `Instance` |
| 18 | `BasePollutionManager` | `BasePollutionManager.cs` | Merge into `EcosystemHealthDirector` |
| 19 | `EnvironmentalStrainManager` | `EnvironmentalStrainManager.cs` | Merge into `EcosystemHealthDirector` |
| 20 | `DepthZoneDirector` | `DepthZoneDirector.cs` | Register as `IEnvironmentRuntimeContext` |
| 21 | `AcousticZoneController` | `AcousticZoneController.cs` | Register as `IAudioService` extension |
| 22 | `HectonMusicDirector` | `HectonMusicDirector.cs` | Register as `IAudioService` extension |
| 23 | `SoundscapeSystem` | `SoundscapeSystem.cs` | Register as `IAudioService` extension |
| 24 | `SpectrumSystem` | `SpectrumSystem.cs` | Register as `IAudioService` extension |
| 25 | `AudioLogSystem` | `AudioLogSystem.cs` | Register as `IAudioService` extension |
| 26 | `LocalizationManager` | `LocalizationManager.cs` | Register as `IUIService` |
| 27 | `LoreDatabaseManager` | `LoreDatabaseManager.cs` | Register as `IUIService` |
| 28 | `SettingsManager` | `SettingsManager.cs` | Register as `IUIService` |
| 29 | `RebindingManager` | `RebindingManager.cs` | Register as `IInputService` extension |
| 30 | `UserOptionsPersistence` | `UserOptionsPersistence.cs` | Merge into `SettingsManager` or `SaveManager` |
| 31 | `AsyncLoadHelper` | `AsyncLoadHelper.cs` | Register as `ISceneService` extension |
| 32 | `SceneTransitionVerifier` | `SceneTransitionVerifier.cs` | Remove; verify in `GameBootstrapper` |
| 33 | `StateRecoveryVerifier` | `StateRecoveryVerifier.cs` | Remove; verify in `GameBootstrapper` |
| 34 | `EntityChangeDetector` | `EntityChangeDetector.cs` | Merge into `PersistentWorldRegistry` |
| 35 | `CameraJuiceSystem` | `CameraJuiceSystem.cs` | Register as `IPlayerSensoryService` |
| 36 | `PlayerExpressionManager` | `PlayerExpressionManager.cs` | Register as `IPlayerRuntimeContext` extension |
| 37 | `PlayerExplorationTracker` | `PlayerExplorationTracker.cs` | Register as `IPlayerRuntimeContext` extension |
| 38 | `SuitUpgradeManager` | `SuitUpgradeManager.cs` | Register as `IPlayerRuntimeContext` extension |
| 39 | `ToolDurabilitySystem` | `ToolDurabilitySystem.cs` | Register as `IPlayerInventoryService` extension |
| 40 | `PDALogbookManager` | `PDALogbookManager.cs` | Register as `IUIService` |
| 41 | `PDAExchangeSystem` | `PDAExchangeSystem.cs` | Register as `IUIService` |
| 42 | `PDAMarkerRegistry` | `PDAMarkerRegistry.cs` | Register as `IUIService` |
| 43 | `ScanLogSystem` | `ScanLogSystem.cs` | Register as `IUIService` |
| 44 | `FirstHourDirector` | `FirstHourDirector.cs` | Register as `IEnvironmentRuntimeContext` |
| 45 | `EndingSystem` | `EndingSystem.cs` | Register as `IEnvironmentRuntimeContext` |
| 46 | `CorporateOrderSystem` | `CorporateOrderSystem.cs` | Register as `IUIService` |
| 47 | `FieldOperationLogSystem` | `FieldOperationLogSystem.cs` | Register as `IUIService` |
| 48 | `AtlasSignalSystem` | `AtlasSignalSystem.cs` | Register as `IUIService` |
| 49 | `AtlasSignalDecoder` | `AtlasSignalDecoder.cs` | Register as `IUIService` |
| 50 | `Atlas6DirectiveSystem` | `Atlas6DirectiveSystem.cs` | Register as `IUIService` |
| 51 | `EclipseGameplaySystem` | `EclipseGameplaySystem.cs` | Register as `IEnvironmentRuntimeContext` |
| 52 | `EmergencyServiceRelayDirector` | `EmergencyServiceRelayDirector.cs` | Register as `IEnvironmentRuntimeContext` |
| 53 | `MigrationDirector` | `MigrationDirector.cs` | Register as `IEnvironmentRuntimeContext` |
| 54 | `RandomEventSystem` | `RandomEventSystem.cs` | Register as `IEnvironmentRuntimeContext` |
| 55 | `FlowFieldVisualizer` | `FlowFieldVisualizer.cs` | Editor-only; remove `Instance` |
| 56 | `AmbientWaterMotionManager` | `AmbientWaterMotionManager.cs` | Register as `IEnvironmentRuntimeContext` |
| 57 | `AbyssalFluidDecalManager` | `AbyssalFluidDecalManager.cs` | Register as `IEnvironmentRuntimeContext` |
| 58 | `AbyssalThermalManager` | `AbyssalThermalManager.cs` | Register as `IEnvironmentRuntimeContext` |
| 59 | `HectonBiolumController` | `HectonBiolumController.cs` | Merge into `HectonBiolumManager` |
| 60 | `GlobalProfileManager` | `GlobalProfileManager.cs` | Merge into `SaveManager` or register as `ISaveService` extension |
| 61 | `PrefabRegistry` | `PrefabRegistry.cs` | Register as `ISceneService` extension |

---

## TOP 5 MOST DANGEROUS SINGLETONS

| Rank | Singleton | Why It Will Kill You First |
|------|-----------|---------------------------|
| **1** | `GameTickManager` | Init-order race causes `NullReferenceException` on `ITickable` registration before `Awake` completes. Every gameplay system hits this. |
| **2** | `HectonFloatingOrigin` | Burst vegetation jobs read `CurrentTotalOffset` via static field. If origin shifts mid-job, flora bends to wrong coordinates = visible pop. |
| **3** | `PersistentWorldRegistry` | `Allocator.Persistent` NativeArrays + `Awake` self-reg. Duplicate scene load = double allocation = 20+ MB leak + crash on second `Awake`. |
| **4** | `SystemDispatcher` / `RenderDispatcher` | `[RuntimeInitializeOnLoadMethod]` runs before bootstrap. If domain reload is off, stale dispatch lanes from previous play mode corrupt frame pacing. |
| **5** | `ObjectPoolManager` | Hot-path spawn/despawn. `Instance` property access is a managed ref read in spawn loop. Also used by Burst-queued hydration callbacks = thread hazard. |

---

## MIGRATION ORDER (Recommended)

```
Week 1: Tier 1 Core (Tick, Dispatcher, Physics, FloatingOrigin, Save)
Week 2: Tier 1 World (PersistentWorldRegistry, MapMagicBridge, ObjectPoolManager)
Week 3: Tier 2 Player (Inventory, Movement, Survival, ActionController)
Week 4: Tier 2 World/Economy (Construction, Power, Mission, Ecosystem)
Week 5: Tier 3 UI (HUD managers, RT managers, Performance monitors)
Week 6: Purge + MCP validation per batch
```

---

*STATUS: PENDING VERIFICATION*  
*No optimism. Facts only. Next step: CTO approves Tier 1 batch order.*
