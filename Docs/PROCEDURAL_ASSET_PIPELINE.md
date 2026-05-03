# PROCEDURAL_ASSET_PIPELINE.md
## КОНТРАКТ НА ГЕНЕРАЦИЮ ПРОЦЕДУРНЫХ АССЕТОВ
Status: PENDING VERIFICATION
Verification: not runtime-measured in this pass
Target: NVIDIA MX350 2GB VRAM · i5-1135G7 · 12GB RAM
Engine: Unity 6000.x · URP Forward+
Tools: MapMagic 2.1.18 · GPU Instancer Pro · Mantis LOD · Mesh Baker

2026-05-02 current-state boundary:

- This is the procedural asset production contract, not proof that a specific generated asset exists or is wired.
- Current project/system truth starts at `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- For procedural world placement ownership, read `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`.
- For readable geology/seismic ownership, read `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`.
- The deprecated `Docs/HECTON8_GEOLOGY_PRODUCTION_PLAN.md` path must not be used as active authority.

---

## ПРИОРИТЕТ РЕЗУЛЬТАТА

[REQ] Цель — существующий prefab в проекте с рабочим runtime hookup.
[REQ] Задача выполнена если и только если:
  - prefab существует в папке проекта
  - material/shader подключён и компилируется без ошибок
  - GPU Instancer prototype зарегистрирован
  - MapMagic scatter profile активен в графе

[REQ] Задача НЕ выполнена если:
  - создан только validator/report без prefab
  - написан editor script который только проверяет, не создаёт
  - сгенерирован markdown с описанием того что нужно сделать

[REQ] Prefer modifying existing runtime/authoring owners
      over creating new editor utilities.
[REQ] If a reusable editor generator is created, it must directly
      create production assets, not only inspect them.
[FORBID] New validator/report/status markdown unless user
         explicitly asks for audit output.
[FORBID] Создавать validator, editor tool, audit script, markdown report,
         request packet или checklist generator если пользователь
         не запросил это явно И если нет готового production prefab.
[FORBID] Компенсировать отсутствие текстур размножением validator кода.
         Вместо этого: остановиться, выдать blocked-by список
         с именами конкретных файлов которых не хватает.

---

## КАТЕГОРИИ

[REQ] Категория ДОЛЖНА быть объявлена явно:
  ORGANIC / GEOLOGICAL / STRUCTURAL / INTERIOR_DECOR

### Бюджеты полигонов (LOD0)
| Категория      | MX350   | High-End  |
|----------------|---------|-----------|
| ORGANIC        | <= 3000 | <= 8000   |
| GEOLOGICAL     | <= 8000 | <= 20000  |
| STRUCTURAL     | <= 15000| <= 40000  |
| INTERIOR_DECOR | <= 2000 | <= 6000   |

### Глобальные лимиты экрана (MX350)
<= 2.5M visible tris · <= 800 SetPass · <= 1.6GB VRAM · <= 100 unique materials

---

## CORE PRINCIPLES

[REQ] Detail != Polygons. Realism = normal maps + vertex displacement +
      triplanar + correct LOD transitions.
[REQ] One material = one draw call. Все вариации через GPU Instancer
      Color/Scale/Rotation randomization.
[REQ] Zero UV dependency для процедурной геометрии. Triplanar MANDATORY.
[REQ] Zero CPU animation. Всё движение через vertex shader
      (WorldPos + Time + Sine/Noise).
[REQ] Follow steps in order internally. Do not stop to narrate every step
      unless blocked by missing resource.
[REQ] Если точная API сигнатура, SO тип или имя ноды MapMagic неизвестны
      -> STOP. Запросить точную сигнатуру. Не угадывать. Не писать // TODO.

---

## ГЕОМЕТРИЯ

[REQ] Quad-dominant topology, uniform density, no tris < 0.05m².
[REQ] Процедурная деформация через 3D noise (Simplex/Perlin):
      Amplitude 0.05-0.3m, Frequency 2-5.
[REQ] Auto-recalculate normals: Smooth + Preserve Hard Edges
      для rock/coral plates.
[REQ] Edge bleeding: boundary vertices snap to Average Normal
      соседних чанков для seamless stitching.

[FORBID] Chaotic triangulation, T-vertices, non-manifold geometry.
[FORBID] UV unwrap для процедурной геометрии.

---

## PHOTOREALISTIC SURFACE

[REQ] Shader MUST include:

1. SSS Approximation
   Wrap Lighting (Half-Lambert) + `1 - dot(N,L)` mask на Albedo.
   Тонкие края (coral/algae) должны пропускать свет. Контроль через Mask.A.

2. Curvature-Driven Wetness
   Convex/concave из Normal map -> модуляция Roughness.
   Concave = wet/glossy (0.2). Convex = dry/matte (0.7).
   Без ручной покраски.

3. Micro-Parallax Offset
   Mask.B -> UV offset на Albedo/Normal. Max offset = 0.03.

4. Fresnel Water Film
   Fresnel Effect node затемняет края, blend с Depth Fog Color.

5. Normal Scale Control
   Float `_NormalScale`. Default 0.75 для phototextures.

[FORBID] Direct texture sampling без curvature roughness,
         SSS mask, Fresnel blend.

---

## ДИСТАНЦИИ КУЛИНГА

| Категория      | Cull Distance | Обоснование                       |
|----------------|---------------|-----------------------------------|
| ORGANIC        | 60-120m       | Algae/coral fade into fog         |
| GEOLOGICAL     | 150-300m      | Rocks form terrain silhouette     |
| STRUCTURAL     | 250-500m      | Bases are navigation landmarks    |
| INTERIOR_DECOR | 40-80m        | Line-of-sight dependent           |

[REQ] Layer Cull Distances + GPU Instancer Distance Culling.
      Не хардкодить в скриптах.
[REQ] URP: привязать culling к Fog Density.
      Fog opacity > 0.95 -> disable rendering.
[FORBID] Culling distance < 40m для любого world-space объекта.

---

## КОЛЛИЗИИ

| Категория         | Collider              | Note                   |
|-------------------|-----------------------|------------------------|
| ORGANIC (small)   | None                  | Pass-through           |
| ORGANIC (large)   | Capsule / Box         | Только если блокирует  |
| GEOLOGICAL (<=3m) | 2-3 Primitives        | Box/Sphere per cluster |
| GEOLOGICAL (>3m)  | MeshCollider (Convex) | На основе LOD2         |
| STRUCTURAL        | MeshCollider (Static) | isKinematic = true     |

[FORBID] MeshCollider на LOD0.
[FORBID] Dynamic Rigidbody для статичных пропов.
[FORBID] > 500 активных коллайдеров на экране.

---

## ТЕКСТУРЫ

### Спецификации
| Map           | Format | Max Size | sRGB | Note                        |
|---------------|--------|----------|------|-----------------------------|
| Albedo        | BC7    | 2048     | Yes  | Tiling 2-4x                 |
| Normal        | BC5    | 2048     | No   | Tangent Space, green flip   |
| Mask (ARM)    | BC7    | 2048     | No   | R=AO G=Rough B=Height/Metal |
| Detail Normal | BC5    | 1024     | No   | Micro-relief, shader blend  |

### CURRENT FLORA RUNTIME CONTRACT
[REQ] `WorldProceduralFlora` currently ships a category-owned texture contract, not generic ARM/detail-normal.
[REQ] Do not reinterpret imported flora channels unless the runtime shaders are changed in the same task.
[REQ] Current runtime ownership:
      - `_DetailMap` = linear grayscale micro-detail / breakup / caustic modulation
      - `_MaskMap` = flora-specific packed control map owned by the shader
      - kelp/coral channel semantics differ; shader code is source of truth
[REQ] Importer settings still stay strict:
      - `albedo`  = sRGB On, Default
      - `normal`  = sRGB Off, Normal Map
      - `mask`    = sRGB Off, Default
      - `detail`  = sRGB Off, Default
[WARN] If runtime is migrated to true ARM + detail-normal packing, update:
       shader sampling, material authoring, validator rules, and all imported family sets together.

[REQ] Все текстуры seamless (Wrap Mode = Repeat).
[REQ] Один атлас на biome/family. Один Material на GPU Instancer batch.
[REQ] Generate Mip Maps = On. Streaming = Off.
[FORBID] Non-tiling, unique per instance, uncompressed, > 2048px для scatter.

### Если текстур нет
[REQ] Не генерировать placeholder. Не писать validator
      для несуществующих текстур.
[REQ] Выдать Master Prompt для генерации и остановиться до импорта:

      "Seamless tiling PBR [texture_type] texture, [subject],
      [biome_context], top-down orthographic, uniform lighting,
      no shadows, no perspective distortion, photorealistic,
      4K, edge-perfect seamless tile, --tile --v 6 --ar 1:1"

[REQ] Biome context tokens:
  ORGANIC_shallow:  "sunlit underwater, turquoise, silicon-based coral"
  GEOLOGICAL_slope: "weathered rock, basalt layers, wet sheen"
  STRUCTURAL_ruin:  "NASA-punk metal, corrosion, salt staining, welded seams"
  ORGANIC_abyss:    "bioluminescent deep sea, dark basalt, pale translucent"

[REQ] После импорта: Wrap=Repeat, MipMaps=On, BC7/BC5,
      sRGB только Albedo, Read/Write=Off,
      Max 2048 (hero) / 1024 (scatter).
[FORBID] AI генерирует финальные .png напрямую.
         Только промпты — импорт вручную через Unity Texture Importer.

---

## SHADER ARCHITECTURE

[REQ] URP Shader Graph:
  - Master Node: PBR / AlphaTest / GPU Instancing ON
  - Triplanar UV: World Space, Normal Type
  - Texture Sampling: Albedo + Normal + Mask
  - Detail Map Blending: Normal + Height
  - Vertex Displacement: sin(Time.y * Freq + WorldPos.xz * Phase) * Amp
  - Depth Fog: Lerp(MatColor, FogColor, exp(-Depth * Coeff))
  - Quality Keywords: _QUALITY_MX350 / _QUALITY_HIGH

[REQ] _QUALITY_MX350 отключает Parallax, снижает Displacement Amp.
[REQ] Max 8 texture samples per pixel. GPU Instancing = ON.
[REQ] Cull Off (organic). Cull Back (hard surface). ZWrite On. Blend Off.

[FORBID] Transparent shaders для opaque geometry.
[FORBID] GrabPass. ComputeBuffer в renderer.
[FORBID] Dynamic branch if() в runtime.
[FORBID] ScreenPosition dependencies.
[FORBID] multi_compile > 4 keywords.

---

## LOD И ИНСТАНСИНГ

### Mantis LOD Workflow
1. Export LOD0 -> Import в Mantis
2. LOD1: Poly Reduction 40-50%, Preserve Silhouette = ON
3. LOD2: Poly Reduction 85-90%
4. LOD Group thresholds: 0.6 / 0.15 / 0.04 / 0
5. Cross Fade = ON (Dithered) для ближних дистанций

### GPU Instancer Pro
- Color Variation: Hue +-0.05, Sat 0.9-1.1, Val 0.85-1.05
- Scale/Rotation: Y 0-360 deg, X/Z tilt +-8 deg, Scale 0.7-1.3
- Frustum Culling: ON. Occlusion Culling: ON
- Buffer Size: Auto-grow, max 100k instances per prefab type

### Cluster Baking
[REQ] Rock/coral группы -> Mesh Baker -> Mantis Decimation ->
      LOD Group -> GPU Instancer.
      Source meshes удалить после bake.

### MapMagic Scatter
[REQ] Координаты через HectonRockOutput -> GPU Instancer API.
      Floor Offset Y: -0.2 to -0.8m. Yaw random 0-360 deg.
[REQ] Max 1200 instances per 1000m tile (density clamp).
[REQ] Минимум 2.5m clearance от player spawn coordinates.
[FORBID] Instantiate() в runtime. Только GPU Instancer / Pools.
[FORBID] Spawn внутри geometry. Unclamped scatter density.

---

## SEAMLESS STITCHING

[REQ] Chunk boundaries: Vertex Normal Blending + Height Offset <= 0.02m.
[REQ] Structural/bases: Base Ring vertices фиксированы к Terrain/Snap Grid.
      Modular snap grid = 0.5m.
[REQ] MapMagic terrain hole edges покрыты rock/debris scatter.

---

## PIPELINE ШАГИ

[REQ] Выполнять в порядке внутренне. Каждый шаг производит реальный
      файл или конфиг в проекте. Не останавливаться для нарратива
      каждого шага — только если заблокирован отсутствием ресурса.

### STEP 1 — BASE MESH
-> Генерация геометрии по категории
-> Проверка: Manifold? Normals consistent? Edge loops clean?
-> Output: .fbx/.obj (LOD0) в папке проекта

### STEP 2 — TEXTURE SET
-> Если текстуры есть: проверить seamless, normal green flip, Mask RGB
-> Если текстур нет: выдать Master Prompts -> STOP до импорта
-> Output: импортированные .png в Unity с правильными настройками

### STEP 3 — SHADER + MATERIAL
-> Собрать URP Shader Graph
-> Включить GPU Instancing, Triplanar, Depth Fog, Quality Keywords
-> Проверка: компилируется? < 8 samples? Zero CPU animation?
-> Output: .shadergraph + MAT_[Category]_[Name]

### STEP 4 — LOD + COLLIDER
-> Mantis: LOD1/LOD2. LOD Group thresholds.
-> Коллайдер по таблице категорий
-> Проверка: Crossfade? Silhouette preserved? Poly budget met?
-> Output: Prefab с LOD Group + Collider

### STEP 5 — INSTANCER + SCATTER
-> GPU Instancer Pro: Color, Scale, Rot, Culling, Buffer
-> MapMagic Scatter profile: Biome mask, Density, Floor Offset, Yaw
-> Проверка: Draw Calls <= 1 per type? VRAM <= 20MB per set?
-> Output: Prefab зарегистрирован в GPU Instancer.
           Scatter profile активен в MapMagic графе.

Validation checklist заполняется ТОЛЬКО после того как prefab существует.

---

## VALIDATION CHECKLIST

[REQ] Заполнить после завершения STEP 5.
[FORBID] Заполнять если prefab не создан.
[FORBID] Выдавать заполненный checklist как proof работы
         без существующего prefab.

[ ] Prefab существует в папке проекта
[ ] Poly count <= бюджету категории
[ ] Zero UV seams (Triplanar verified)
[ ] Shader компилируется, GPU Instancing ON, < 8 samples
[ ] LOD0->1->2 smooth, dithered, no pop-in
[ ] Draw calls <= 1 для 5k instances
[ ] VRAM <= 1.6GB total для всех procedural sets
[ ] Коллайдер соответствует таблице категорий
[ ] Animation 100% vertex-shader driven
[ ] GPU Instancer prototype зарегистрирован
[ ] MapMagic scatter profile активен

[REQ] Незаполненный пункт = блокер.
      Указать что именно не готово и почему.

---

## BLOCKED-BY FORMAT

Если шаг заблокирован — выдавать строго в этом формате.
Не писать validator/placeholder вместо этого.

  BLOCKED: [Название шага]
  Причина: [Что именно отсутствует]
  Нужные файлы:
    - Assets/Textures/[ExactName].png
    - Assets/Meshes/[ExactName].fbx
  Следующее действие: [Конкретное действие пользователя]
  Готово и ждёт разблокировки:
    - [Список готовых файлов/конфигов]

---

## АБСОЛЮТНЫЕ ЗАПРЕТЫ

[FORBID] Flat meshes без normals/displacement
[FORBID] Unique materials per instance
[FORBID] Animation через Update(), Animator, Bones, Coroutines
[FORBID] UV-dependent details для процедурной геометрии
[FORBID] MeshCollider на LOD0 / Dynamic Rigidbody для statics
[FORBID] Transparent shaders для opaque geometry
[FORBID] Угадывать параметры. Все значения из таблицы или формулы
[FORBID] Runtime texture generation / Graphics.CopyTexture для mass assets
[FORBID] Instantiate() в runtime для scatter объектов
[FORBID] New validator/report/status markdown без явного запроса
[FORBID] Editor script который только проверяет, не создаёт ассеты

---

## USAGE PROMPT

  Generate [Asset Category] [Asset Name] per PROCEDURAL_ASSET_PIPELINE.md.
  Target: MX350 2GB VRAM, URP Forward+, GPU Instancer compatible.
  Follow steps in order. Each step produces a real file or config.
  If resource missing -> STOP + blocked-by list.
  Do not write validators, reports or placeholders instead.
