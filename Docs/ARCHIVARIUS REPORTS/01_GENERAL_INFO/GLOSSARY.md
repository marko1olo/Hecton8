# HECTON-8 — GLOSSARY OF TERMS
Date: 2026-05-04
Status: REFERENCE


**Версия:** 1.0.0 | **Дата:** 2026-04-28 | **Автор:** Supreme Compliance Auditor

---

## 📋 TABLE OF CONTENTS

1. [Архитектурные термины](#1-архитектурные-термины)
2. [Математика и координаты](#2-математика-и-координаты)
3. [Оптимизация и производительность](#3-оптимизация-и-производительность)
4. [Системы и компоненты](#4-системы-и-компоненты)
5. [Третья сторона (Third-Party)](#5-третья-сторона-third-party)
6. [Процедурная генерация](#6-процедурная-генерация)
7. [Аудио и DSP](#7-аудио-и-dsp)
8. [Рендеринг и графика](#8-рендеринг-и-графика)

---

## 1. АРХИТЕКТУРНЫЕ ТЕРМИНЫ

### AUP (Absolute Universe Position)
**Определение:** Система координат с плавающим началом (Floating Origin), где позиция хранится как `int64x3 grid_sector + float3 local_offset`.

**Назначение:** Избегание проблем с точностью float на больших расстояниях (>10 км от начала координат).

**Пример использования:**
```csharp
struct AUPosition {
    long3 gridSector;    // 64-bit integer grid cell
    float3 localOffset;  // Local position within cell (0-1024 units)
}
```

**См. также:** `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `HectonFloatingOrigin.cs`

---

### SOA (Structure of Arrays)
**Определение:** Паттерн организации данных, где вместо массива структур (AoS) используется структура массивов.

**Пример:**
```csharp
// ❌ AoS (Array of Structures) — плохо для кэш-локальности
class Item { float weight; int id; string name; }
Item[] items = new Item[1000];

// ✅ SOA (Structure of Arrays) — хорошо для кэш-локальности
struct ItemData {
    NativeArray<float> weights;  // [1000]
    NativeArray<int> ids;        // [1000]
    NativeArray<int> nameHashes; // [1000]
}
```

**Преимущества:**
- Лучшая кэш-локальность при итерации по одному полю
- Совместимость с Unity Job System и Burst
- Zero-GC при использовании NativeArray

**См. также:** `DATA_Inventory_Resources_Items_SOA_Layout.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### DOD (Data-Oriented Design)
**Определение:** Архитектурный подход, ориентированный на данные, а не на объекты. Противоположность ООП.

**Принципы:**
1. **Cache-line alignment:** Данные располагаются в памяти последовательно
2. **Separation of data and behavior:** Данные отделены от логики
3. **Batch processing:** Обработка данных пакетами (Job System)
4. **Minimal indirection:** Избегание указателей и ссылочных типов

**Пример:**
```csharp
// ❌ ООП подход (плохо)
class Creature : MonoBehaviour {
    void Update() { /* AI logic */ }
}

// ✅ DOD подход (хорошо)
struct CreatureData {
    public float3 position;
    public float health;
    public int stateFlags;
}

class CreatureSystem : ISystem {
    NativeArray<CreatureData> _creatures;
    public void Update(float dt) {
        // Process all creatures in a single Job
    }
}
```

**См. также:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Service Locator Pattern (GlobalRegistry)
**Определение:** Паттерн доступа к сервисам через централизованный реестр вместо прямых ссылок.

**Пример:**
```csharp
// ❌ Прямая зависимость (плохо)
public class Player : MonoBehaviour {
    private AudioManager _audio;
    void Awake() => _audio = FindObjectOfType<AudioManager>();
}

// ✅ Service Locator (хорошо)
public class Player : MonoBehaviour {
    void Update() => GlobalRegistry.Audio.PlaySFX(sfxId);
}
```

**Преимущества:**
- Loose coupling между системами
- Easy testing (можно подменить сервис на mock)
- Нет FindObjectOfType в runtime

**См. также:** `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

---

### Bridge Pattern (Anti-Corruption Layer)
**Определение:** Паттерн изоляции third-party кода от первой стороны через интерфейс-прослойку.

**Пример:**
```csharp
// ✅ Anti-Corruption Layer для Crest
public interface IHectonOceanKinematics {
    float GetWaveHeight(float3 position);
    float3 GetWaterVelocity(float3 position);
}

public class HectonCrestOceanKinematics : IHectonOceanKinematics {
    // using Crest; — ТОЛЬКО ЗДЕСЬ
    public float GetWaveHeight(float3 pos) => OceanRenderer.Instance.SampleHeight(pos);
}

// ✅ Gameplay-код использует только интерфейс
public class PlayerMovement : MonoBehaviour {
    private IHectonOceanKinematics _ocean;
    void Awake() => _ocean = GlobalRegistry.OceanKinematics.ActiveProvider;
}
```

**См. также:** `THIRD_PARTY_POISON.md`

---

## 2. МАТЕМАТИКА И КООРДИНАТЫ

### Bishop Frame
**Определение:** Подвижный репер в дифференциальной геометрии, описывающий ориентацию кривой без неоднозначности вектора нормали Френе в точках перегиба.

**Использование в HECTON-8:**
- Физика тросов/кабелей (Verlet constraints) — twist-free ориентация
- Процедурная флора (kelp, sargassum) — плавное распространение ориентации по длине стебля

**Преимущества перед Frenet frame:**
- Нет разворота нормали при нулевой кривизне
- Стабильная ориентация для констрейнтов
- Совместим с Burst-векторизацией

**См. также:** `PHYS_Tether_Cable_Acceleration_Constraints.txt`

---

### Burst
**Определение:** Unity Burst Compiler — высокопроизводительный компилятор для C# Job System.

**Возможности:**
- Компиляция в оптимизированный машинный код (SIMD)
- Поддержка float precision control (Fast/Standard/Precise)
- Статический анализ на безопасность (no managed refs)

**Пример:**
```csharp
[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
struct FluidJob : IJobParallelFor {
    public void Execute(int index) {
        // Burst-компилированный код
        float result = math.sqrt(input[index]);
    }
}
```

**См. также:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### NativeArray / NativeList / NativeHashMap
**Определение:** Низкоуровневые коллекции Unity для Zero-GC работы с памятью.

**Типы:**
- `NativeArray<T>` — фиксированный массив (быстрый доступ по индексу)
- `NativeList<T>` — динамический список (аналог List<T> без GC)
- `NativeHashMap<K,V>` — хэш-таблица (аналог Dictionary<K,V> без GC)

**Алокаторы:**
- `Allocator.Temp` — один метод, автоматически освобождается
- `Allocator.TempJob` — один job cycle, требует Dispose
- `Allocator.Persistent` — персистентный, требует явного Dispose

**Пример:**
```csharp
// ✅ Правильное использование
NativeArray<float> _buffer = new NativeArray<float>(1024, Allocator.Persistent);
void OnDestroy() {
    _buffer.Dispose();
    _buffer = default;
}
```

**См. также:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### Job System (IJob, IJobParallelFor)
**Определение:** Система многопоточной обработки данных Unity.

**Типы jobs:**
- `IJob` — однопоточный job
- `IJobParallelFor` — многопоточный job (parallel for loop)
- `IJobChunk` — DOTS ECS job (для Entity queries)

**Правила:**
- Schedule() в начале кадра
- Complete() в конце кадра (или следующем)
- ❌ ЗАПРЕЩЕНО: Schedule() + Complete() в одном hot path

**См. также:** `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

---

### XXHash3
**Определение:** Быстрый non-cryptographic хэш-алгоритм (SIMD-ускоренный).

**Использование в HECTON-8:**
- Checksum для save files
- Hash для procedural generation seeds
- Quick integrity checks

**Пример:**
```csharp
// Native XXHash3 через P/Invoke
uint hash = XXHash3.ComputeHash(data, length);
```

**См. также:** `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

---

### Lotka-Volterra
**Определение:** Математическая модель хищник-жертва для симуляции экосистем.

**Уравнения:**
```
dx/dt = αx - βxy  (жертвы)
dy/dt = δxy - γy  (хищники)

где:
x = population prey
y = population predator
α = prey growth rate
β = predation rate
δ = predator growth from prey
γ = predator death rate
```

**Использование в HECTON-8:**
- Баланс фауны в биомах
- Динамическая регуляция популяций
- Emergent ecosystem behavior

**См. также:** `AI_Creature_Cognition_States.txt`

---

## 3. ОПТИМИЗАЦИЯ И ПРОИЗВОДИТЕЛЬНОСТЬ

### Zero-GC
**Определение:** Отсутствие allocations garbage collector в hot paths (Tick, Update, FixedUpdate).

**Запрещено в hot path:**
- `new class/List/Dict/array`
- LINQ (.Where, .Select, .Any, .FirstOrDefault, .ToList)
- string interpolation / concatenation
- boxing value types
- delegates / lambdas (capturing)
- StartCoroutine
- GetComponent<T>() uncached

**Разрешено:**
- NativeArray<T>, NativeList<T>
- struct allocations (Vector3, Color, Quaternion)
- cached delegates
- ITickable state machines

**См. также:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Hot Path
**Определение:** Код, выполняемый каждый кадр (Tick, Update, LateUpdate, FixedUpdate).

**Бюджеты (MX350 target):**
- Main thread: ≤12 ms
- GC: 0 B/frame
- SetPass calls: ≤600
- Batches: ≤1800
- Memory: ≤4096 MB total

**См. также:** `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

---

### Cold Alloc
**Определение:** Однократная аллокация в Init (Awake/Start), не в hot path.

**Канонический формат комментария:**
```csharp
// COLD ALLOC: Type[capacity] — reason — owner: ClassName
private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
// COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: self
```

**Правила:**
- Только в Awake/Start/OnEnable
- С явным указанием capacity для коллекций
- С комментарием о причине и владельце

**См. также:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

---

### Kahn's Topological Sort
**Определение:** Алгоритм топологической сортировки ориентированного ациклического графа (DAG) через удаление вершин с нулевой входящей степенью.

**Использование в HECTON-8:**
- `SaveManager` — определение порядка загрузки `ISaveable` по `LoadPriority` с разрешением циклических зависимостей.
- `CraftingSystem` — упорядочивание рецептов крафта по зависимостям ингредиентов.
- `PowerGrid` — валидация отсутствия циклов в энергосети перед расчётом потока.

**Сложность:** O(V + E) — линейная от числа вершин и рёбер.

**Псевдокод:**
```csharp
Queue<int> zeroInDegree = new Queue<int>();
foreach (var node in graph)
    if (node.InDegree == 0) zeroInDegree.Enqueue(node.Id);

while (zeroInDegree.Count > 0)
{
    int current = zeroInDegree.Dequeue();
    sorted.Add(current);
    foreach (int neighbor in graph.Adjacent(current))
    {
        neighbor.InDegree--;
        if (neighbor.InDegree == 0)
            zeroInDegree.Enqueue(neighbor.Id);
    }
}
// Если sorted.Count < graph.Count → цикл detected → LogError + disable.
```

**См. также:** `SaveManager.cs`, `CraftingSystem.cs`, `PowerGrid.cs`

---

### Torricelli Damping
**Определение:** Физическая модель затухания, основанная на законе Торричелли для истечения жидкости через отверстие, адаптированная для демпфирования скорости в водной среде.

**Формула:**
```
v_new = v_old * (1 - k * sqrt(|v_old|) * dt)

gде:
k = коэффициент демпфирования среды (воды / вязкости)
|v_old| = модуль скорости
```

**Использование в HECTON-8:**
- `PlayerMovement` — плавное торможение игрока в воде без резких рывков (альтернатива линейному drag).
- `FaunaBrain` — демпфирование скорости морских существ при маневрировании.
- `PhysicsApplySystem` — применение силы сопротивления среды к `ForcePacket` в подводной физике.

**Преимущества перед линейным drag:**
- Более реалистичное поведение при высоких скоростях (квадратичное сопротивление).
- Стабильная сходимость к нулю без микро-колебаний.
- Совместим с `FixedTick` и Burst-векторизацией.

**См. также:** `PHYS_Fluid_Incursion_Interior.txt`, `PlayerMovement.cs`, `FaunaBrain.cs`

---

### Double Buffer
**Определение:** Паттерн с двумя буферами для чтения/записи без блокировок.

**Пример:**
```csharp
NativeArray<CreatureData> _bufferA; // read frame N
NativeArray<CreatureData> _bufferB; // write frame N → read frame N+1

void Swap() {
    var temp = _bufferA;
    _bufferA = _bufferB;
    _bufferB = temp;
}
```

**Преимущества:**
- No race conditions
- No locks
- Cache-friendly sequential access

**См. также:** `AI_Creature_Cognition_States.txt`, `PHYS_Fluid_Incursion_Interior.txt`

---

### SPSC (Single Producer Single Consumer)
**Определение:** Lock-free очередь для односторонней коммуникации между потоками.

**Использование в HECTON-8:**
- Audio DSP thread → Main thread
- Job System → Main thread
- Physics gather → Physics apply

**Пример:**
```csharp
// Native SPSC queue for audio param sync
NativeQueue<AudioParam> _paramQueue;

// Producer (DSP thread)
_paramQueue.Enqueue(new AudioParam { ... });

// Consumer (Main thread, LateUpdate)
while (_paramQueue.TryDequeue(out var param)) {
    ApplyParam(param);
}
```

**См. также:** `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

---

## 4. СИСТЕМЫ И КОМПОНЕНТЫ

### ITickable / IFixedTickable / ISlowTickable
**Определение:** Интерфейсы для систем, обновляемых через GameTickManager.

```csharp
public interface ITickable {
    void Tick(float dt);  // Per-frame update
}

public interface IFixedTickable {
    void FixedTick(float fdt);  // Physics update (FixedDeltaT)
}

public interface ISlowTickable {
    void SlowTick();  // ~0.5s update (AI, ambient systems)
}
```

**См. также:** `GameTickManager.cs`

---

### IPoolable
**Определение:** Интерфейс для объектов в object pool.

```csharp
public interface IPoolable {
    void OnSpawn();    // Сброс ВСЕГО состояния
    void OnDespawn();  // Отписка от ВСЕХ событий, unregister из tick
}
```

**Критично:**
- OnSpawn ДОЛЖЕН сбрасывать ВСЁ состояние
- OnDespawn ДОЛЖЕН отписываться от ВСЕХ событий
- ❌ ЗАПРЕЩЕНО: async/await с destroyCancellationToken на pooled objects

**См. также:** `ObjectPoolManager.cs`

---

### IInteractable
**Определение:** Интерфейс для объектов, с которыми можно взаимодействовать.

```csharp
public interface IInteractable {
    void Interact(InteractionPacket p);
    bool CanInteract(uint toolID);
    byte QueryState();  // 0-255 state value
}
```

**См. также:** `Interaction/` scripts

---

### ISaveable
**Определение:** Интерфейс для объектов, поддерживающих сохранения.

```csharp
public interface ISaveable {
    int SavePriority { get; }    // 0-10 Core, 11-50 World, 51-100 Player, 101+ UI
    void PopulateSaveData(NativeByteStream stream);
    void LoadFromSaveData(NativeByteReader reader);
}
```

**LoadPriority:**
- 0-10: Core systems (GameTickManager, GlobalRegistry)
- 11-50: World (terrain, caves, props)
- 51-100: Player (position, inventory, tools)
- 101-200: Inventory (items, resources)
- 201+: UI (open tabs, cursor position)

**См. также:** `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `SaveManager.cs`

---

### IPowerComponent
**Определение:** Интерфейс для компонентов энергосети.

```csharp
public interface IPowerComponent {
    float PowerRating { get; }      // kW consumption/production
    int PowerPriority { get; }      // 0 = critical, 255 = optional
    bool HasPower { get; }
    event Action<bool> OnPowerStatusChanged;
}
```

**См. также:** `PowerGrid.cs`, `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

---

## 5. ТРЕТЬЯ СТОРОНА (THIRD-PARTY)

### Crest
**Определение:** Ocean simulation system для Unity (URP/HDRP).

**Использование в HECTON-8:**
- Ocean surface simulation
- Wave kinematics
- Underwater rendering

**Ограничения:**
- `using Crest;` ТОЛЬКО в `HectonCrestOcean*` классах
- gameplay-код использует `IHectonOceanKinematics` интерфейс

**См. также:** `THIRD_PARTY_POISON.md`, `HectonCrestOceanDepthCacheRuntimeBridge.cs`

---

### MapMagic
**Определение:** Procedural terrain generation system.

**Использование в HECTON-8:**
- Terrain heightmap generation
- Biome placement
- Scatter objects (rocks, trees)

**Ограничения:**
- `using MapMagic;` ТОЛЬКО в `MapMagicBridge` классе
- runtime access только через `MapMagicBridge.Instance`

**См. также:** `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`

---

### MMFeedbacks (Feel)
**Определение:** Juice/feedback system для Unity.

**Использование в HECTON-8:**
- Camera shake
- Screen vibrations
- Audio feedback
- Particle bursts

**Статус:** ✅ РАЗРЕШЁН для runtime

---

### Odin Inspector
**Определение:** Extended Unity Inspector с атрибутами.

**Использование в HECTON-8:**
- Editor-only attributes ([OdinSerialize], [ShowInInspector])
- Custom inspectors

**Статус:** ✅ Editor только, не входит в билд

---

## 6. ПРОЦЕДУРНАЯ ГЕНЕРАЦИЯ

### ProceduralFamily_*
**Определение:** ScriptableObject с параметрами для процедурной генерации объектов.

**Пример:**
```csharp
[CreateAssetMenu(fileName = "ProceduralFamily_Coral", ...)]
public class ProceduralFamily_Coral : ScriptableObject {
    public Mesh[] baseMeshes;
    public Material[] materials;
    public float sizeVariance = 0.3f;
    public float rotationVariance = 180f;
}
```

**См. также:** `PROCEDURAL_ASSET_PIPELINE.md`

---

### ProceduralRule_*
**Определение:** ScriptableObject с правилами размещения процедурных объектов.

**Пример:**
```csharp
[CreateAssetMenu(fileName = "ProceduralRule_Scatter", ...)]
public class ProceduralRule_Scatter : ScriptableObject {
    public float minDensity = 0.5f;
    public float maxDensity = 2.0f;
    public float slopeThreshold = 30f;
    public LayerMask validLayers;
}
```

**См. также:** `SCATTER_REFACTOR_EXECUTION_PLAN.md`

---

### SDF (Signed Distance Field)
**Определение:** Математическое представление поверхности как расстояния до ближайшей точки.

**Использование в HECTON-8:**
- Voxel terrain carving
- Cave generation
- Smooth mesh extraction (marching cubes)

**Формула:**
```
SDF(point) > 0  → точка вне объекта
SDF(point) = 0  → точка на поверхности
SDF(point) < 0  → точка внутри объекта
```

**См. также:** `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`

---

### Marching Cubes
**Определение:** Алгоритм извлечения полигональной поверхности из SDF/вокселей.

**Принцип:**
1. Разбить пространство на кубы (voxel grid)
2. Для каждого куба определить тип поверхности (8 вершин → 256 конфигураций)
3. Сгенерировать треугольники для каждой конфигурации

**См. также:** `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`

---

## 7. АУДИО И DSP

### DSPGraph
**Определение:** Unity audio DSP graph system для procedural audio.

**Использование в HECTON-8:**
- Procedural sound synthesis
- Real-time audio processing
- Spatial audio (HRTF)

**Пример:**
```csharp
// DSP node graph
var graph = new DSPGraph();
var oscillator = graph.CreateNode<OscillatorNode>();
var filter = graph.CreateNode<FilterNode>();
var output = graph.CreateNode<OutputNode>();

oscillator.Connect(filter);
filter.Connect(output);
```

**См. также:** `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`, `SpatialAudioManager.cs`

---

### HRTF (Head-Related Transfer Function)
**Определение:** Функция, моделирующая восприятие звука человеком в 3D пространстве.

**Использование в HECTON-8:**
- Binaural audio для подводного звука
- Spatial occlusion (через воду, через корпус)

**См. также:** `AUDIO_Hrtf_Binaural_Spatialization.txt`, `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`

---

### IAudioOutputJob
**Определение:** Native DSP synthesis interface для Zero-GC audio.

**Пример:**
```csharp
public interface IAudioOutputJob {
    void Process(float[] left, float[] right, int sampleCount);
}
```

**См. также:** `SpatialAudioManager.cs`

---

## 8. РЕНДЕРИНГ И ГРАФИКА

### URP (Universal Render Pipeline)
**Определение:** Scriptable Render Pipeline от Unity для кросс-платформенного рендеринга.

**Конфигурация HECTON-8:**
- Surface (Medium): HDR, MSAA=OFF, FXAA, scale 1.0
- Low: HDR, MSAA=OFF, FXAA, scale 0.65

**См. также:** `Assets/_Project/Data/URP_Medium.asset`

---

### SRP Batcher
**Определение:** Unity batching system для Scriptable Render Pipelines.

**Требования:**
- Один материал = один shader variant
- CBUFFER_START(UnityPerMaterial) для per-material data
- ❌ ЗАПРЕЩЕНО: MaterialPropertyBlock на стандартной геометрии

**См. также:** `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

---

### GPU Instancing
**Определение:** Рендеринг множества одинаковых объектов в одном draw call.

**Требования:**
- Enable на материале
- ❌ ЗАПРЕЩЕНО: комбинировать со Static Batching
- Использовать для повторяющихся объектов (rocks, trees, props)

**См. также:** `REND_Instanced_Flora_Physics.txt`

---

### LOD (Level of Detail)
**Определение:** Система уменьшения детализации объектов на расстоянии.

**Бюджеты HECTON-8:**
- Props > 0.5m: LOD0 + LOD1 + Cull
- Hero: LOD0 + LOD1 + LOD2 + Cull
- LOD1 ≤ 50% LOD0 poly
- LOD2 ≤ 25% LOD0 poly

**Переходы:** Crossfade/dithered near-field, discrete distant

**См. также:** `REND_Foveated_Simulation_LOD.txt`, `LOD_SYSTEM_README.md`

---

### VAT (Vertex Animation Textures)
**Определение:** Запечённая анимация вершин в текстуры для GPU-driven animation.

**Использование в HECTON-8:**
- Flora animation (kelp, coral)
- Destructible objects

**См. также:** `REND_GPU_Driven_Animation_VAT.txt`

---

### Impostors
**Определение:** 2D billboard с запечённой 3D геометрией для дальнего LOD.

**Использование в HECTON-8:**
- Very distant objects (>100m)
- Complex geometry simplification

**См. также:** `ImpostorSystem.cs`, `AmplifyImpostors/`

---

### Diegetic Editor Preview
**Определение:** Wireframe / gizmo-based visualization of a diegetic UI element in the Unity Scene view during Edit Mode, without entering Play Mode.

**Назначение:** Allows the Lead Architect and environment artists to see the spatial layout, scale, and FOV alignment of projected HUD canvases (e.g., `SuitHUDV4CanvasOverlay` in `ProjectionSource` mode) without relying on runtime camera pose updates.

**Требования реализации:**
- `#if UNITY_EDITOR` only — stripped from builds.
- No `GetComponent`, `FindObjectOfType`, or physics queries inside gizmo path.
- Must read only cached serialized fields and `SceneView.lastActiveSceneView.camera`.
- Color-coded wireframes: orange = projection frustum, cyan = element bounds, white = text labels.

**См. также:** `HUD_EDITOR_SPEC.md`, `SuitHUDV4CanvasOverlay.cs`

---

### BC7 / BC5
**Определение:** Форматы сжатия текстур DirectX.

**BC7:**
- 8 bits/pixel
- Для albedo/roughness/AO
- 2048×2048 ≈ 5.3 MB

**BC5:**
- 8 bits/pixel (RG каналы)
- Для normal maps (DXT5nm)
- 2048×2048 ≈ 5.3 MB

**См. также:** `VRAM_BUDGET_AUDIT.md`

---

## 📝 ПРИМЕЧАНИЯ ПО ИСПОЛЬЗОВАНИЮ

### Для AI-агентов:
1. **Всегда использовать точные термины** из этого глоссария
2. **Не изобретать новые термины** без явной необходимости
3. **Ссылаться на мандаты** при использовании специализированных терминов

### Для разработчиков:
1. **Добавлять новые термины** при вводе новых систем
2. **Обновлять определения** при изменении архитектуры
3. **Ссылаться на глоссарий** в документации

---

**STATUS:** ✅ GLOSSARY.md создан  
**LAST UPDATED:** 2026-04-28  
**NEXT REVIEW:** При добавлении новых систем или терминов
