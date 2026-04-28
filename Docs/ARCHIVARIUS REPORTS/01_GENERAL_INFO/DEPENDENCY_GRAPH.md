# GLOBAL REGISTRY DEPENDENCY GRAPH

**Версия:** 2026-04-28 | **Статус:** ETA VERIFIED

---

## 📋 INITIALIZATION FLOW (Mermaid Diagram)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          GRANDPARENT SYSTEMS                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐     ┌──────────────────┐                            │
│  │  GameBootstrapper │────▶│  SystemDispatcher │                            │
│  │   (Entry Point)   │     │   (Frame Owner)   │                            │
│  └────────┬─────────┘     └────────┬─────────┘                            │
│           │                         │                                       │
│           ▼                         ▼                                       │
│  ┌──────────────────┐     ┌──────────────────┐                            │
│  │ GlobalRegistry   │────▶│  ProfilerRegistry│                            │
│  │  (Service Locator)│     │  (Instrumentation)│                            │
│  └────────┬─────────┘     └──────────────────┘                            │
│           │                                                          │
└───────────┼──────────────────────────────────────────────────────────────┘
            │
            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          CORE SERVICES (Layer 0)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │InputDispatcher│  │SceneRuntime  │  │MemoryBudget  │  │RenderSettings│    │
│  │  .Input      │  │  .Scene      │  │  Tracker     │  │  Lifecycle   │    │
│  └──────┬───────┘  └──────┬───────┘  └──────────────┘  └──────────────┘    │
│         │                │                                                   │
│  ┌──────┴───────┐  ┌──────┴───────┐                                         │
│  │ PlayerRuntime│  │EnvironmentRT│                                          │
│  │  Context     │  │  Context    │                                          │
│  │  .Player     │  │  .Environment                                       │
│  └──────┬───────┘  └──────┬───────┘                                         │
│         │                │                                                   │
└─────────┼────────────────┼───────────────────────────────────────────────────┘
          │                │
          ▼                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          PLAYER SERVICES (Layer 1)                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ PlayerInventory  │  │ PlayerSensory    │  │ PlayerTool       │         │
│  │  .PlayerInventory│  │  .PlayerSensory  │  │   Manager        │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ PlayerMovement   │  │ PlayerPDA        │  │ PlayerBuilder   │         │
│  │ (HectonPlayer    │  │                  │  │                  │         │
│  │   Movement)      │  │                  │  │                  │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          WORLD SERVICES (Layer 2)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ SubmarineRuntime │  │ HectonWorld     │  │ OceanKinematics │         │
│  │  Context        │  │  Generator      │  │  Runtime        │         │
│  │  .Submarine     │  │                 │  │  Service        │         │
│  └────────┬─────────┘  └────────┬─────────┘  └────────┬─────────┘         │
│           │                     │                     │                    │
│           ▼                     ▼                     ▼                    │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ SubmarineAtmo    │  │ WorldProcedural │  │ IHectonOcean     │         │
│  │  System          │  │  ScatterDirector │  │  Kinematics      │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ SubmarineStruct  │  │ MapMagicBridge   │  │ Ecosystem       │         │
│  │  Grid            │  │                  │  │  Director        │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          GAMEPLAY SERVICES (Layer 3)                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ HazardZone      │  │ Construction     │  │ PowerGrid       │         │
│  │  Manager        │  │  Manager         │  │  Manager        │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ BiomeMatrix      │  │ BeaconNetwork    │  │ HectonDiscovery │         │
│  │  Director        │  │  System          │  │  Manager        │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          UI SERVICES (Layer 4)                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ HectonFabricator │  │ HectonInventory  │  │ HectonSuitHUD   │         │
│  │  UI              │  │  UI              │  │  _v4            │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐         │
│  │ PlayerTool       │  │ ScanLogSystem    │  │ SaveManager     │         │
│  │  Manager         │  │                  │  │                 │         │
│  └──────────────────┘  └──────────────────┘  └──────────────────┘         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 INITIALIZATION ORDER (Text Format)

### Phase 1: Bootstrap (Before Scene Load)
```
1. GameBootstrapper.Awake()
   ├── ResetStaticState() → GlobalRegistry.ClearRuntimeBuckets()
   └── GameBootstrapper.Initialize()
       ├── SystemDispatcher.EnsureRuntimeInstance()
       ├── GlobalRegistry.RegisterInputService(InputDispatcher)
       ├── GlobalRegistry.RegisterPhysicsService(PhysicsApplySystem)
       ├── GlobalRegistry.RegisterAudioService(SpatialAudioManager)
       └── GlobalRegistry.RegisterSaveService(SaveManager)
```

### Phase 2: Scene Services (During 00_BOOTSTRAP)
```
2. SceneRuntimeService.Awake()
   └── GlobalRegistry.RegisterSceneService(this)

3. InputDispatcher.Awake()
   ├── GlobalRegistry.RegisterInputService(this)
   └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core)

4. PhysicsApplySystem.Awake()
   ├── GlobalRegistry.RegisterPhysicsService(this)
   └── GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.UI)
```

### Phase 3: Player Context (During 01_MAIN_MENU → 02_HECTON_WORLD)
```
5. PlayerRuntimeContextService.Awake()
   ├── GlobalRegistry.RegisterPlayerRuntimeContext(this)
   └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core)

6. PlayerInventoryManager.Awake()
   ├── GlobalRegistry.RegisterPlayerInventoryService(this)
   └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core)

7. PlayerSensoryManager.Awake()
   ├── GlobalRegistry.RegisterPlayerSensoryService(this)
   └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core)
```

### Phase 4: World Systems (During 02_HECTON_WORLD load)
```
8. SubmarineCoreDirector.Awake()
   └── GlobalRegistry.RegisterSubmarine(this)

9. SubmarineStructuralGrid.Awake()
   ├── GlobalRegistry.RegisterSubmarineHullBreach(this)
   └── GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment)

10. SubmarineAtmosphereSystem.Awake()
    └── GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment)

11. HectonWorldGenerator.Awake()
    └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment)

12. WorldProceduralScatterDirector.Awake()
    ├── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment)
    └── GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment)
```

### Phase 5: Gameplay Directors (After Scene Load)
```
13. BiomeMatrixDirector.Awake()
    └── GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment)

14. EcosystemDirector.Awake()
    └── GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment)

15. HazardZoneManager.Awake()
    └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment)
```

### Phase 6: UI Systems (After Player Spawn)
```
16. HectonFabricatorUI.Awake()
    └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI)

17. PlayerPDA.Awake()
    ├── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI)
    └── GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI)

18. HectonSuitHUD_v4.Awake()
    └── GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI)
```

---

## 📋 DEPENDENCY CHAIN (Who Needs Whom)

### Grandfather → Father → Son Chain:

| System | Requires | Required By |
|--------|----------|-------------|
| **GameBootstrapper** | Nothing | All |
| **SystemDispatcher** | GameBootstrapper | All Tickables |
| **GlobalRegistry** | SystemDispatcher | All Services |
| **InputDispatcher** | GlobalRegistry | PlayerMovement, Tools |
| **PlayerRuntimeContext** | InputDispatcher | UI, Inventory |
| **PlayerMovement** | PlayerRuntimeContext | Camera, Audio |
| **SubmarineCoreDirector** | PlayerMovement | Atmosphere, Structural |
| **WorldGenerator** | SubmarineCoreDirector | Scatter, Flora |
| **ScatterDirector** | WorldGenerator | Boids, Fauna |

---

## 📋 SERVICE REGISTRATION SUMMARY

### Registered Services (25 total):

| Service | Interface | Layer | Priority |
|---------|-----------|-------|----------|
| InputDispatcher | IInputService | Core | 0 |
| PhysicsApplySystem | IPhysicsService | Core | 0 |
| SpatialAudioManager | IAudioService | Core | 0 |
| SceneRuntimeService | ISceneService | Core | 0 |
| SaveManager | ISaveService | Core | 0 |
| PlayerRuntimeContextService | IPlayerRuntimeContext | Core | 0 |
| PlayerInventoryManager | IPlayerInventoryService | Core | 0 |
| PlayerSensoryManager | IPlayerSensoryService | Core | 0 |
| EnvironmentRuntimeContextService | IEnvironmentRuntimeContext | Core | 0 |
| GlobalWeatherDirector | IWeatherService | Environment | 20 |
| OceanKinematicsRuntimeService | IHectonOceanKinematicsService | Physics | 20 |
| SubmarineCoreDirector | ISubmarineRuntimeContext | Gameplay | 30 |
| SubmarineStructuralGrid | ISubmarineHullBreachReadModel | Gameplay | 30 |
| DebrisManager | IDebrisService | Gameplay | 30 |
| EcosystemDirector | IEcosystemDirectorService | World | 40 |

### Registered Tickables (120+ total):

| Category | Count | Priority Layer |
|----------|-------|-----------------|
| Core | ~20 | Core |
| Environment | ~40 | Environment |
| Player | ~30 | Player |
| UI | ~30 | UI |

---

**STATUS:** ETA VERIFIED ✅

**Grandfather:** `GameBootstrapper` → `SystemDispatcher` → `GlobalRegistry`