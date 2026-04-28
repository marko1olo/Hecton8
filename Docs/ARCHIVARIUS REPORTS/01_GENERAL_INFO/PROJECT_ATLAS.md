# PROJECT ATLAS — HECTON-8 MASTER DIRECTORY
**Версия:** 1.1.0 | **Дата:** 2026-04-29 | **Автор:** Supreme Compliance Auditor / ARCHIVARIUS MODE

---

## 📋 TABLE OF CONTENTS

1. [Корневая структура проекта](#1-корневая-структура-проекта)
2. [Assets — Полный каталог](#2-assets—полный-каталог)
3. [Scripts — Ключевые системы](#3-scripts—ключевые-системы)
4. [.agents-skills — Мандаты AI-агентов](#4-agents-skills—мандаты-ai-агентов)
5. [Docs — Документация](#5-docs—документация)
6. [Third-Party — Зависимости](#6-third-party—зависимости)
7. [Точки входа для новых агентов](#7-точки-входа-для-новых-агентов)
8. [Статистика проекта](#8-статистика-проекта)
9. [Архивариус Аудит 2026-04-29](#9-архивариус-аудит-2026-04-29)

---

## 1. КОРНЕВАЯ СТРУКТУРА ПРОЕКТА

```
C:\hades\Hecton8\
├── .agents-skills/          # 52 мандата для AI-агентов (обязательно читать перед работой)
├── Assets/                  # Основной контент Unity
│   ├── _Project/            # Первая сторона (HECTON-8)
│   ├── _ThirdParty/         # Вторая сторона (ассеты из магазина)
├── Packages/                # Unity PackageManager (Crest, ShaderGraph, etc.)
├──Docs/                     # Вся документация
├── ProjectSettings/         # Настройки проекта (Quality, URP, Physics)
├── UserSettings/            # Локальные настройки Unity
├── VERIFICATION_REPORT_*.md # Отчёты верификации (требуют архивации)
├── THIRD_PARTY_POISON.md    # Аудит зависимости от third-party
├── GOD_OBJECT_AUDIT.md      # Аудит Player.prefab
├── AGENTS.md                # System Instructions для AI-агентов
└── *.pdf                    # Мануалы third-party (MapMagic, Bakery, Crest)
```

### ⚠️ Зоны риска (требуют санации):
- **Корень:** `VERIFICATION_REPORT_*.md`, `*.pdf` — переместить в `Assets/Docs/Archive/`
- **DOCS/:** Дубликаты логов агентов (AGENT_01_*.md) — оставить только в `Assets/DOCS/`
- **_Recovery/:** 24 устаревшие сцены — проверить актуальность, удалить или архивировать

---

## 2. ASSETS — ПОЛНЫЙ КАТАЛОГ

### 2.1 `_Project` (Первая сторона — HECTON-8)

```
Assets/_Project/
├── Art/
│   ├── Fonts/                 # Шрифты UI
│   ├── Materials/             # Материалы (MAT_*)
│   ├── Meshes/                # Импортные меш-ассеты
│   ├── Models/                # 3D модели (Blender/FBX)
│   ├── Shaders/               # Кастомные шейдеры (не ShaderGraph)
│   ├── Skyboxes/              # Небо и звезды
│   ├── Sprites/               # 2D спрайты (UI, иконки)
│   └── TEXTURES/              # Текстуры (60+ файлов)
│       ├── Detali/            # Детали (visor, bubbles, minerals)
│       ├── Sky/               # Облака, штормы
│       ├── Terrain Textures/  # Песок, камни, трава
│       └── WorldProceduralFlora/  # Текстуры флоры (coral, kelp)
│           └── Imported/      # Импортные ассеты (family.coral.*, family.kelp.*)
├── Audio/
│   └── Music for Game/
│       ├── README_MusicDirector.md
│       └── LOUDNESS_AUDIT.md
├── Core/                      # Ядро системы (Player*, Global*, Bootstrap*)
├── Data/                      # ScriptableObject (ProceduralFamily_*, ProceduralRule_*)
├── Diagnostics/               # Инструменты отладки и профилирования
├── Docs/                      # Документация проекта
│   └── Narrative_AI_Integration.md
├── Editor/                    # Editor-скрипты (инспекторы, валидаторы)
├── Input/                     # Input System конфигурации
├── Materials/                 # Материалы runtime
├── Prefabs/                   # Префабы
│   ├── PFB_*                  # Статические префабы (первая сторона)
│   ├── GEN_*                  # Процедурные префабы (генерируются)
│   └── Nature/
│       └── Flora/
│           └── Baked/
│               └── README.md  # Инструкция по бейкингу флоры
├── Scenes/                    # Сцены (00_BOOTSTRAP, 01_MAIN_MENU, 02_HECTON_WORLD)
├── Scripts/                   # C# скрипты (514+ файлов)
├── Shaders/                   # ShaderGraph шейдеры
├── Tests/                     # Тесты (Editor/PlayMode)
└── UI/                        # UI ресурсы (HUD, PDA)
```

### 2.2 Ключевые скрипты по доменам

#### 🎮 Gameplay (Core Systems)
| Файл | Назначение | Ключевой контракт |
|------|------------|-------------------|
| `GameTickManager.cs` | Единственный MonoBehaviour с Update/FixedUpdate | `ITickable`, `IFixedTickable` |
| `HectonFloatingOrigin.cs` | Плавающее начало координат (AUP) | `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt` |
| `HectonVoxelVolume.cs` | Воксельный движок (carving, SDF) | `VOX_Voxel_World_Logic_Carving_Persistence.txt` |
| `SaveManager.cs` / `SaveData.cs` | Система сохранений (LZ4 + XXHash3) | `DATA_Save_Persistence_Binary_Delta_Checksum.txt` |
| `GlobalRegistry.cs` | Service Locator (DI без контейнера) | `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` |

#### 🎒 Inventory & Items
| Файл | Назначение |
|------|------------|
| `PlayerInventory.cs` | Инвентарь игрока (Native SOA) |
| `InventoryGrid.cs` | Сетка занятости инвентаря |
| `PickupItem.cs` | Подбираемые предметы (IInteractable) |
| `ConsumableItem.cs` | Расходуемые предметы (еда, вода) |
| `InventoryEvents.cs` | События инвентаря (Zero-GC) |

#### 🔋 Power & Logistics
| Файл | Назначение |
|------|------------|
| `PowerGrid.cs` | Энергосеть (CSR граф, DSU island detection) |
| `PowerGridManager.cs` | Глобальный менеджер энергосетей |
| `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt` | Мандат (логистика энергии) |

#### 🌊 Ocean & Fluid
| Файл | Назначение | Ключевой контракт |
|------|------------|-------------------|
| `HectonCrestOceanDepthCacheRuntimeBridge.cs` | Мост к Crest (океан) | `using Crest;` — **ТОЛЬКО ЗДЕСЬ** |
| `HectonCrestOceanDepthCacheBootstrap.cs` | Bootstrap для Crest | `using Crest;` — **ТОЛЬКО ЗДЕСЬ** |
| `HectonSurfaceWeatherDirector.cs` | Погода на поверхности | ❌ **VIOLATION: using Crest;** |
| `PHYS_Fluid_Incursion_Interior.txt` | Мандат (затопление) | `IFloodQueryProvider`, `IFloodCommandReceiver` |

#### 🐟 AI & Fauna
| Файл | Назначение | Ключевой контракт |
|------|------------|-------------------|
| `FaunaDirector.cs` | Директор фауны (спавн, деспавн) | `AI_Creature_Cognition_States.txt` |
| `SargassumMicroFaunaBoids.cs` | Boids симуляция (JobSystem) | `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt` |
| `EncounterDirector.cs` | Директор встреч (AI encounter) | `AI_Director_Encounter_Manager.txt` |
| `AcousticZoneController.cs` | Акустические зоны (sonar) | `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt` |

#### 🛠️ Tools & Interaction
| Файл | Назначение |
|------|------------|
| `PlayerInteraction.cs` | Взаимодействие игрока |
| `PhysicalInteractionHandler.cs` | Физические взаимодействия (pickup, drag) |
| `FlashlightTool.cs` | Фонарик |
| `RepairTool.cs` | Ремонтный инструмент |
| `HarpoonLauncherTool.cs` | Гарпун |
| `BuilderTool.cs` | Инструмент строительства |
| `BeaconDeployerTool.cs` | Маяк |
| `HarpoonLauncherTool.cs` | Гарпун |
| `ToolEffectEvents.cs` | События инструментов (Zero-GC) |

#### 🏗️ Construction & Base
| Файл | Назущение |
|------|------------|
| `ConstructionManager.cs` | Менеджер строительства |
| `BaseModule.cs` | Базовый модуль базы |
| `HabitatIntegrityManager.cs` | Целостность базы (затопление, давление) |
| `BeaconNetworkSystem.cs` | Сеть маяков |
| `BeaconRuntime.cs` | Runtime маяка |

#### 🌍 World Generation
| Файл | Назначение | Ключевой контракт |
|------|------------|-------------------|
| `MapMagicBridge.cs` | Мост к MapMagic (террен, биомы) | `using MapMagic;` — **ТОЛЬКО ЗДЕСЬ** |
| `WorldGenerativeGeologySeamExecutionDirector.cs` | Геология (seams) | `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt` |
| `WorldGenerativeGeologyVoxelBridgeDirector.cs` | Мост вокселей | `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt` |
| `CaveGraphGenerator.cs` | Генерация пещер | `CORE_Weather_Abyssal_FlowField_Currents.txt` |
| `Cave*RuntimeBuilder.cs` | Runtime builders для пещер | |

#### 🎨 Rendering & VFX
| Файл | Назначение |
|------|------------|
| `ImpostorSystem.cs` | Импаосторы (дальний LOD) |
| `ScatterEvaluator.cs` | Scatter (JobSystem + Burst) |
| `HectonBiolumMaster.shader` | Биолуминесценция |
| `REND_*` шейдеры | Шейдеры рендеринга |

#### 🎭 UI & HUD
| Файл | Назначение |
|------|------------|
| `PDA*.cs` | PDA интерфейс (Inventory, Loadout, Construction, DataLog) |
| `HUD*.cs` | HUD компоненты |
| `InteractionUI.cs` | Промпты взаимодействия |
| `NotificationEvents.cs` | События уведомлений |
| `HudNumericStringCache.cs` | Zero-GC кэш чисел для HUD |

#### 🚣 Player & Transport
| Файл | Назначение |
|------|------------|
| `HectonPlayerMovement.cs` | Движение игрока |
| `MantaScooter.cs` | Транспорт (Seaglide) |
| `HeavyTowWinch.cs` | Лебёдка |
| `PlayerTransportCoordinator.cs` | Координатор транспорта |
| `CORE_Submarine_Vehicles_Kinematics_AUP.txt` | Мандат (транспорт) |

#### 🧠 Survival & Progression
| Файл | Назначение |
|------|------------|
| `HectonSurvivalSystem.cs` | Выживание (O2, давление, голод) |
| `HectonDiscoveryManager.cs` | Открытия биомов |
| `HectonNarrativeDirector.cs` | Повествование |
| `MissionManager.cs` | Квесты |
| `PlayerExplorationTracker.cs` | Трекер исследований |
| `PlayerAchievementRegistry.cs` | Регистр достижений |

#### 🎵 Audio
| Файл | Назначение |
|------|------------|
| `SpatialAudioManager.cs` | Пространственный звук (DSPGraph) |
| `PlayerThrusterAudio.cs` | Звук реактора игрока |
| `PlayerFootstepAudio.cs` | Шаги игрока |
| `PlayerCriticalProceduralAudioRenderer.cs` | Процедурный звук |
| `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt` | Мандат (DSP) |

#### ⚙️ Optimization
| Файл | Назначение |
|------|------------|
| `PerformanceBudgetController.cs` | Контроллер бюджета FPS |
| `DynamicResolutionScaler.cs` | Динамическое разрешение |
| `VRAMMonitor.cs` | Монитор VRAM |
| `ZeroGCComplianceScanner.cs` | Сканер Zero-GC (Editor) |
| `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | Мандат (Zero-GC) |

#### 🧪 Dev & Testing
| Файл | Назначение |
|------|------------|
| `*SmokeTester.cs` | Smoke-тесты (Barter, Builder, Tool, UI) |
| `*RuntimeVerifier.cs` | Верификаторы (PhysicalInteraction, MantaAcoustic) |
| `EditorPlayModeDiagnostics.cs` | Диагностика (Editor) |
| `RuntimePerformanceProfiler.cs` | Профилировщик runtime |
| `CrashTelemetryBuffer.cs` | Телеметрия крашей |

### 2.3 _ThirdParty (Вторая сторона)

```
Assets/_ThirdParty/
├── Crest/                     # Ocean system (URP)
├── MapMagic/                  # Terrain generation
├── AstarPathfindingProject/   # A* Pathfinding (⚠️ ЗАМЕНЁН на DOTS Nav)
├── MasterAudio/               # Audio manager (⚠️ ЗАМЕНЁН на SpatialAudio)
├── DOTween/                   # Tween system (⚠️ ЗАПРЕЩЁН)
├── Easy Save 3/               # Save system (⚠️ ЗАМЕНЁН на Native LZ4)
├── Feel/                      # Feedback system (MMFeedbacks — ОК)
└── ...                        # Другие ассеты
```

---

## 3. SCRIPTS — КЛЮЧЕВЫЕ СИСТЕМЫ

### 3.1 Архитектурные паттерны

#### ITickable / IFixedTickable / ISlowTickable
```csharp
// Все gameplay-системы регистрируются через GameTickManager
public interface ITickable { void Tick(float dt); }
public interface IFixedTickable { void FixedTick(float fdt); }
public interface ISlowTickable { void SlowTick(); } // ~0.5s

// Пример регистрации:
public sealed class FaunaDirector : MonoBehaviour, ITickable
{
    void OnEnable() => GameTickManager.Instance.Register(this);
    void OnDisable() => GameTickManager.Instance.Unregister(this);
    public void Tick(float dt) { /* ... */ }
}
```

#### IPoolable (Object Pooling)
```csharp
public interface IPoolable
{
    void OnSpawn();   // Сброс ВСЕГО состояния
    void OnDespawn(); // Отписка от ВСЕХ событий, unregister из tick
}
```

#### IInteractable (Взаимодействия)
```csharp
public interface IInteractable
{
    void Interact(InteractionPacket p);
    bool CanInteract(uint toolID);
    byte QueryState();
}
```

#### ISaveable (Сохранения)
```csharp
public interface ISaveable
{
    int SavePriority { get; }  // 0-10 Core, 11-50 World, 51-100 Player, 101+ UI
    void PopulateSaveData(NativeByteStream stream);
    void LoadFromSaveData(NativeByteReader reader);
}
```

### 3.2 События (Zero-GC EventBus)

```csharp
// Статические event bus на NativeQueue
public static class InteractionEvents
{
    public static event Action<uint, uint> OnItemCollected;  // itemId, count
    public static event Action<GameObject, GameObject> OnInteractionStarted;
}

public static class CraftingEvents
{
    public static event Action<uint> OnCraftStarted;  // recipeId
    public static event Action<uint> OnCraftCompleted;
}

public static class SaveEvents
{
    public static event Action OnSaveStarted;
    public static event Action OnSaveCompleted;
    public static event Action OnSaveFailed;
}
```

---

## 4. .AGENTS-SKILLS — МАНДАТЫ AI-АГЕНТОВ

**52 мандата в `C:\hades\Hecton8\.agents-skills/`**

### AI Systems (5)
| Мандат | Описание | Статус |
|--------|----------|--------|
| `AI_Creature_Cognition_States.txt` | CognitionBlob, CognitionCore, SpatialMemoryBank | ✅ Активен |
| `AI_Director_Encounter_Manager.txt` | EncounterDirector, AI encounter logic | ⚠️ Проверить |
| `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt` | VoxelDynamicNavGridRuntime | ⚠️ Проверить |
| `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt` | Boids симуляция, spatial hash | ⚠️ Проверить |
| `AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt` | A* Pathfinding (legacy) | ⚠️ Проверить |

### Architecture (2)
| Мандат | Описание |
|--------|----------|
| `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` | GlobalRegistry, GameBootstrapper |
| `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt` | 00_BOOTSTRAP → 01_MAIN_MENU → 02_HECTON_WORLD |

### Audio (3)
| Мандат | Описание |
|--------|----------|
| `AUDIO_Hrtf_Binaural_Spatialization.txt` | HRTF, binaural audio |
| `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt` | Сонар, акустические зоны |
| `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt` | DSPGraph, SPSC очереди |

### Core Gameplay (5)
| Мандат | Описание |
|--------|----------|
| `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt` | O2, давление, выживание |
| `CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt` | Урон, целостность корпуса |
| `CORE_Submarine_Vehicles_Kinematics_AUP.txt` | ITransportPlatform, AUP, EVA |
| `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt` | Инструменты, raycast, тепло |
| `CORE_Weather_Abyssal_FlowField_Currents.txt` | Погода, flow fields, течения |

### Data & Persistence (3)
| Мандат | Описание |
|--------|----------|
| `DATA_Inventory_Resources_Items_SOA_Layout.txt` | Native SOA инвентарь |
| `DATA_Save_Persistence_Binary_Delta_Checksum.txt` | LZ4, XXHash3, delta persistence |
| `STRM_Persistent_Object_Registry.txt` | ObjectRegistry, residency |

### GPU & Performance (4)
| Мандат | Описание |
|--------|----------|
| `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt` | Compute shaders, MX350 |
| `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` | NativeArray, NativeList, JobSystem |
| `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` | 60 FPS, 16.67ms, VRAM 1800MB |
| `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | Zero-GC hot path |

### Physics (5)
| Мандат | Описание |
|--------|----------|
| `PHYS_Destructible_Organic_Entropy.txt` | Destructible organic matter |
| `PHYS_Fluid_Incursion_Interior.txt` | Затопление, CompartmentState, slosh |
| `PHYS_Kinematic_Interaction_Hands.txt` | Kinematic hands, grab |
| `PHYS_Physics_Integrity_Determinism_ForceMode.txt` | Determinism, ForceMode |
| `PHYS_Tether_Cable_Acceleration_Constraints.txt` | Tether, cables, constraints |

### Rendering (8)
| Мандат | Описание |
|--------|----------|
| `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt` | Voxel occlusion, shadows |
| `REND_Foveated_Simulation_LOD.txt` | Foveated LOD, eye tracking |
| `REND_GPU_Driven_Animation_VAT.txt` | VAT, GPU animation |
| `REND_Instanced_Flora_Physics.txt` | Instanced flora, physics |
| `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt` | Noir, dithering, fog |
| `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` | URP, hot path, HLOD |
| `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt` | VFX, fluid, particles |
| `REND_Foveated_Simulation_LOD.txt` | Foveated LOD (дубликат) |

### Streaming (3)
| Мандат | Описание |
|--------|----------|
| `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt` | Addressables, async load |
| `STRM_World_Streaming_Residency_Chunk_Management.txt` | World streaming, chunks |
| `CTRL_Device_Abstraction_Haptics.txt` | Haptics, device abstraction |

### UI (3)
| Мандат | Описание |
|--------|----------|
| `UI_Data_Streaming_ZeroGC_Optimization.txt` | UI streaming, Zero-GC |
| `UI_Diegetic_Physical_Interfaces.txt` | Diegetic UI, physical interfaces |
| `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt` | Localization, RTL, font swap |

### Voxel (4)
| Мандат | Описание |
|--------|----------|
| `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt` | MapMagic + voxel seams |
| `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt` | SDF, marching cubes |
| `VOX_Voxel_World_Logic_Carving_Persistence.txt` | Carving, persistence |
| `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt` | Дубликат |

### Остальные мандаты (17)
`ANIM_*`, `DBG_*`, `LOGI_*`, `MATH_*`, `NET_*`, `PROG_*`, `TOOL_*`, `PROJECT_LTS_*` — см. полный список в `.agents-skills/`

---

## 5. DOCS — ДОКУМЕНТАЦИЯ

### 5.1 Актуальная документация (`Docs/`)
| Файл | Назначение |
|------|------------|
| `README.md` | Введение в документацию |
| `ROOT_DOCS_REFERENCE.md` | Индекс всех документов |
| `SYSTEMS_CONTRACTS.md` | Контракты систем (API) |
| `QUALITY_GATES.md` | Критерии качества |
| `DOC_GOVERNANCE.md` | Политика документации |
| `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` | Архитектурная карта |
| `HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` | План выполнения |
| `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` | Процедурный мир |
| `PROCEDURAL_ASSET_PIPELINE.md` | Пайплайн ассетов |
| `FLORA_SYSTEM_PLAN.md` | Система флоры |
| `SCATTER_REFACTOR_EXECUTION_PLAN.md` | Scatter рефактор |
| `ECS_DOTS_ADOPTION_PLAN.md` | DOTS план |

### 5.2 Архив (`Docs/_Archive/2026-04-16_Workspace_Cleanup/`)
- `folders/legacy_agent_drops/` — старые логи агентов
- `folders/ai_findings/` — находки AI-аудитов
- `root/legacy_plans/` — устаревшие планы
- `root/shell_reports/` — отчёты shell
- `docs/session_audits/` — сессии аудита

### 5.3 Логи агентов (`Assets/DOCS/`)
- `AGENT_01_GRAPHICS_LOG.md` ... `AGENT_06_TECHART_LOG.md`
- `INVENTORY_AUDIT/` — аудиты инвентаря и систем

---

## 6. THIRD-PARTY — ЗАВИСИМОСТИ

### 6.1 Критические (используются в runtime)
| Ассет | Версия | Назначение | Ограничения |
|-------|--------|------------|-------------|
| **Crest** | 4.x / 5.x | Ocean system (URP) | `using Crest;` ТОЛЬКО в `HectonCrestOcean*` |
| **MapMagic** | 1.x | Terrain generation | `using MapMagic;` ТОЛЬКО в `MapMagicBridge` |
| **Feel** | 3.x | MMFeedbacks (juice) | OK для runtime |
| **Odin Inspector** | 3.x | Editor attributes | Editor только |

### 6.2 Заменённые (deprecated)
| Ассет | Заменён на | Причина |
|-------|------------|---------|
| A* Pathfinding | DOTS Nav / VoxelDynamicNavGrid | Zero-GC, Burst |
| Master Audio | SpatialAudioManager (DSPGraph) | Zero-GC, native |
| DOTween | ITickable state machines | Zero-GC, Burst |
| Easy Save 3 | Native LZ4 + XXHash3 | Zero-GC, native |

---

## 7. ТОЧКИ ВХОДА ДЛЯ НОВЫХ АГЕНТОВ

### 7.1 Первый запуск (обязательно)
1. **Прочитать `AGENTS.md`** — System Instructions (CTO mandate)
2. **Прочитать `PROJECT_ATLAS.md`** — эта карта (структура проекта)
3. **Прочитать `SYSTEMS_CONTRACTS.md`** — API контракты
4. **Выбрать мандат** из `.agents-skills/` по тематике задачи

### 7.2 Ключевые контракты по доменам

#### AI & Fauna
- `AI_Creature_Cognition_States.txt` — cognition, memory, threat
- `IInteractable` — взаимодействия
- `ISaveable` — сохранения

#### Physics & Fluid
- `PHYS_Fluid_Incursion_Interior.txt` — затопление
- `IFloodQueryProvider` / `IFloodCommandReceiver` — интерфейс затопления
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt` — транспорт, AUP, EVA

#### Rendering & VFX
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` — URP, оптимизация
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` — Zero-GC

#### Data & Persistence
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt` — сохранения
- `DATA_Inventory_Resources_Items_SOA_Layout.txt` — инвентарь

### 7.3 Запрещённые паттерны (Zero-GC)
```csharp
// ❌ ЗАПРЕЩЕНО в hot path:
new List<T>();                 // GC alloc
LINQ (.Where, .Select, .Any);  // GC alloc
GetComponent<T>() uncached;     // GC alloc
string interpolation;           // GC alloc
StartCoroutine();               // GC alloc
new Action/Func/lambda;         // GC alloc

// ✅ РАЗРЕШЕНО:
NativeArray<T>;                 // Zero-GC
IJobParallelFor + Burst;        // Zero-GC
Cached GetComponent<T>;         // Zero-GC
Span<char> + TryFormat;         // Zero-GC
ITickable state machine;        // Zero-GC
```

---

## 8. СТАТИСТИКА ПРОЕКТА

| Метрика | Значение | Примечание |
|---------|----------|------------|
| **C# скриптов (первая сторона)** | 514+ | `Assets/_Project/Scripts/` |
| **Мандатов (.agents-skills)** | 52 | `.agents-skills/*.txt` |
| **PDF мануалов (third-party)** | 11 | MapMagic, Crest, Bakery, etc. |
| **Аудит-отчётов (.md)** | 60+ | Корень + DOCS + Docs |
| **Сцен в _Recovery/** | 24 | Устаревшие сцены |
| **Префабов (PFB_*)** | 50+ | `Assets/_Project/Prefabs/` |
| **Текстур (TEXTURES/)** | 60+ | PNG, TGA, EXR |
| **Шейдеров (Shaders/)** | 30+ | ShaderGraph + HLSL |
| **ScriptableObject Family** | 20+ | ProceduralFamily_*, ProceduralRule_* |

---

## 📝 ПРИМЕЧАНИЯ ПО СОХРАНЕНИЮ ДОКУМЕНТАЦИИ

### Архивация устаревших файлов:
```
Корень/*.md → Assets/Docs/Archive/RootDocs/
Корень/*.pdf → Assets/Docs/Archive/ThirdPartyManuals/
Assets/DOCS/AGENT_*.md → Assets/Docs/Archive/AgentLogs/ (сохранить только последние)
_Recovery/*.unity → Assets/Docs/Archive/RecoveryScenes/ (проверить перед удалением)
```

### Рекомендуемый порядок чтения для новых агентов:
1. `AGENTS.md` (корень) — System Instructions
2. `PROJECT_ATLAS.md` (этот файл) — Структура проекта
3. `SYSTEMS_CONTRACTS.md` — API контракты
4. `QUALITY_GATES.md` — Критерии качества
5. `.agents-skills/{ВАША_ТЕМА}.txt` — Мандат по задаче

---

## 9. АРХИВАРИУС АУДИТ 2026-04-29

**Authority:** CTO / Lead Architect (ARCHIVARIUS MODE)  
**Reports:** `INTERFACE_HEALTH_DASHBOARD.md` | `EVENT_FLOW_MAP.md`

### 9.1 Ghost Interfaces (👻 0 implementors)

| Interface | Location | Action Required |
|-----------|----------|-----------------|
| `IRenderable` | `GlobalRegistryContracts.cs` | Delete or assign owner |
| `IAudioService` | `GlobalRegistryContracts.cs` | Implement by `SpatialAudioManager` or delete |

### 9.2 Conflicting Interfaces (⚔️ 2+ definitions)

| Interface | Issue | Action Required |
|-----------|-------|-----------------|
| `IDamageReceiver` | Canonical in `GlobalRegistryContracts.cs` + shadow in `HabitatIntegrityManager.cs` | Remove nested definition, use canonical `DamagePacket` |
| `IUIService` | 3 fragmented implementations, no unified root | Create `HectonUIRoot` delegate |

### 9.3 Data Template Audit (SOA Foundations)

| Template | Type | SOA Mandate | Verdict |
|----------|------|-------------|---------|
| `FaunaDataTemplate` | `ScriptableObject` wrapping struct | ❌ Must be struct | **FAIL** |
| `ItemTemplate` | `[StructLayout(Pack=4)] struct` | ✅ | **PASS** |
| `EncounterProfile` | `ScriptableObject` wrapping struct | ❌ Must be struct | **FAIL** |
| `PowerGridModuleData` | `[Serializable] struct` | ✅ | **PASS** |

### 9.4 AUP Surgery Byte-Check

| Check | Result |
|-------|--------|
| `AbsoluteUniversePosition` Size | **PASS** — 48 bytes exact |
| `PersistentWorldItemRecord` offsets | **PASS** — sequential after 48B AUP |
| Save format `CurrentVersion` | **PASS** — `0x0008` |
| `SaveDataVersion` offset | **PASS** — shifted to 60 (was 48) |
| Migration path | **PASS** — `SaveDataMigration_AupV8.cs` exists |

### 9.5 Event Bus Nervous System

| Bus | Signals | Status |
|-----|---------|--------|
| `InteractionEvents` | 3 signals | 🔴 Verified |
| `CraftingEvents` | 3 signals | 🔴 Verified |
| `SaveEvents` | 6 signals | 🔴 Verified |
| `FlashlightEvents` | 3 signals | 🔴 Verified |
| `PDAEvents` | 3 signals | 🔴 Verified |
| `ModuleStatusEvents` | 2 signals | 🔴 Verified |
| `ScanEvents` | 3 signals | 🔴 Verified |
| `AudioLogEvents` | 4 signals | 🔴 Verified |
| `NarrativeEvents` | 3 signals | 🟡 Partial |
| `RandomEventEvents` | 5 signals | 🔴 Verified |
| `CelestialEvents` | 3 signals | 🔴 Verified |
| **Total** | **38 signals mapped** | **11 buses** |

**⚠️ Architecture drift:** Event buses use static `Action<T>` instead of `NativeQueue<T>` backing per AGENTS.md mandate. Migration required.

### 9.6 Cyrillic Sweep (First-Party `.cs`)

| File | Severity |
|------|----------|
| `Gameplay/EclipseGameplaySystem.cs` | 🔴 CI/CD BREAKER |
| `Gameplay/EndingSystem.cs` | 🔴 CI/CD BREAKER |
| `Gameplay/RandomEventSystem.cs` | 🔴 CI/CD BREAKER |
| `ITickable.cs` | 🔴 CI/CD BREAKER |
| `AudioLog/AudioLogEvents.cs` | 🔴 CI/CD BREAKER |

**Action:** All XML docs and file headers MUST be translated to English before next CI/CD run.

### 9.7 Shader Index (BETA)

| Shader | Purpose |
|--------|---------|
| `Hecton_BiolumSSGIComposite.shader` | Bioluminescence + SSGI |
| `Hecton_AbyssalVoxelRock.shader` | Voxel cave rock |
| `Hecton_FabricatorHologram.shader` | Fabricator hologram |
| `SuitVisor.shader` | Visor glass refraction |
| `SG_GasGiant_Master.shader` | Gas giant clouds |
| `Hecton_AlienSky_Master.shader` | Atmospheric dome |
| `CoralLit.shader` | Procedural coral (URP 14+) |
| `Hecton_Ocean_Master.shader` | Crest ocean integration |

**Total:** 8 first-party custom shaders + 37 variants = **45 shader assets**.

### 9.8 Debt Tally

| Category | Count |
|----------|-------|
| Ghost Interfaces | 2 |
| Conflicting Interfaces | 2 |
| Failed Data Templates | 2 |
| Cyrillic `.cs` violations | 5 |
| Event buses without NativeQueue backing | 11 |
| Direct coupling bypassing event bus | 3 |

---

**STATUS:** ✅ PROJECT_ATLAS.md создан и готов к использованию  
**LAST UPDATED:** 2026-04-29  
**NEXT REVIEW:** При добавлении новых систем или переструктурировании проекта
