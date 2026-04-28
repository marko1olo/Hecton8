# VRAM BUDGET AUDIT — HECTON-8
**Версия:** 1.0.0 | **Дата:** 2026-04-28 | **Автор:** Supreme Compliance Auditor

---

## 📋 TABLE OF CONTENTS

1. [Executive Summary](#1-executive-summary)
2. [Методология расчёта](#2-методология-расчёта)
3. [Текстуры _Project/Art/TEXTURES](#3-текстуры-_projectarttextures)
4. [Текстуры WorldProceduralFlora](#4-текстуры-worldproceduralflora)
5. [Оценка VRAM по категориям](#5-оценка-vram-по-категориям)
6. [Сравнение с бюджетом MX350](#6-сравнение-с-бюджетом-mx350)
7. [Критические нарушения](#7-критические-нарушения)
8. [Рекомендации](#8-рекомендации)

---

## 1. EXECUTIVE SUMMARY

### Целевые лимиты (MX350 2GB)
| Компонент | Бюджет | Статус |
|-----------|--------|--------|
| **Всего VRAM** | 2048 MB | HARD CEILING |
| **Текстуры** | 900 MB | ⚠️ **AT RISK** |
| **Render Targets + Depth** | 320 MB | ✅ OK |
| **Модели + Меш-данные** | 200 MB | ✅ OK |
| **Остаток (система)** | 628 MB | ✅ OK |

### ⚠️ КРАСНЫЙ ФЛАГ
**Graduation response threshold:** used/total > 0.90 → Mip-downgrade trigger  
**Текущая оценка:** ~850-950 MB (94-105% от текстурированного бюджета)

---

## 2. МЕТОДОЛОГИЯ РАСЧЁТА

### Формула размера текстуры
```
Размер (MB) = (Ширина × Высота × БитыНаПиксель) / 8 / 1024 / 1024

Для BC7 (альбедо/шероховатость/AO):
- 2048×2048 × 8 бит = 16 MB → ~5.3 MB после BC7 сжатия
- 1024×1024 × 8 бит = 4 MB → ~1.3 MB после BC7
- 512×512 × 8 бит = 1 MB → ~0.33 MB после BC7

Для BC5 (нормали, RG/DXT5nm):
- 2048×2048 × 8 бит = 16 MB → ~5.3 MB после BC5
- 1024×1024 × 8 бит = 4 MB → ~1.3 MB после BC5

Для uncompressed RGBA32:
- 2048×2048 × 32 бит = 32 MB (❌ ЗАПРЕЩЕНО)
```

### Допущения
- Все текстуры считаются в BC7/BC5 сжатии (производственный стандарт)
- MipMaps включены для world-текстур, отключены для UI
- Атласы для одинаковых материалов (rocks/debris/coral)
- Текстуры < 512px считаются как 512px для консервативной оценки

---

## 3. ТЕКСТУРЫ _PROJECT/ART/TEXTURES

### 3.1 Корневые текстуры (27 файлов)

| Файл | Предполагаемый размер | Формат | Оценка VRAM |
|------|----------------------|--------|-------------|
| `terrain.png` | 2048×2048 | BC7 | 5.3 MB |
| `ORGANIC.png` | 2048×2048 | BC7 | 5.3 MB |
| `Meshy_AI_Alien_barnacles_clust_0301230506_texture.png` | 2048×2048 | BC7 | 5.3 MB |
| `menuview.png` | 1024×1024 | BC7 (UI, no mips) | 1.3 MB |
| `gameart.png` | 2048×2048 | BC7 | 5.3 MB |
| `foam.png` | 1024×1024 | BC7 | 1.3 MB |
| `FLOOR1.png` | 2048×2048 | BC7 | 5.3 MB |
| `FLOOR.png` | 2048×2048 | BC7 | 5.3 MB |
| `clouds0_diff.png` | 2048×2048 | BC7 | 5.3 MB |
| `clouds.png` | 2048×2048 | BC7 | 5.3 MB |
| `Aegir_storms.png` | 2048×2048 | BC7 | 5.3 MB |

**Подытог (корень):** 11 текстур × ~5.3 MB = **~58 MB**

### 3.2 Sky (6 файлов)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `oblakajip.png` | 1024×1024 | 1.3 MB |
| `oblaka!.png` | 1024×1024 | 1.3 MB |
| `eb2.png` | 512×512 | 0.33 MB |
| `clod2.png` | 1024×1024 | 1.3 MB |
| `clod1.png` | 1024×1024 | 1.3 MB |
| `bo3.png` | 512×512 | 0.33 MB |
| `bo2.png` | 512×512 | 0.33 MB |

**Подытог (Sky):** 7 текстур × ~1 MB = **~7 MB**

### 3.3 Terrain Textures/sand (2 файла)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `NORMAL.png` | 1024×1024 | 1.3 MB |
| `Ground079S_1K-PNG_Color.png` | 1024×1024 | 1.3 MB |

**Подытог (sand):** 2 текстуры × 1.3 MB = **~2.6 MB**

### 3.4 Detali (6 файлов)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `visor runoff normal.png` | 1024×1024 | 1.3 MB |
| `visor droplet mask.png` | 1024×1024 | 1.3 MB |
| `soft plume noise - какой то серый ну норм.png` | 1024×1024 | 1.3 MB |
| `Soft Plume Noise - second try.png` | 1024×1024 | 1.3 MB |
| `Mineral Seep Mask - second try.png` | 1024×1024 | 1.3 MB |
| `mineral seep mask - looks seamless.png` | 1024×1024 | 1.3 MB |
| `bubble vent atlas - bad - redo.png` | 2048×2048 | 5.3 MB |

**Подытог (Detali):** 7 текстур × ~1.5 MB = **~10.5 MB**

---

## 4. ТЕКСТУРЫ WORLDPROCEDURALFLORA

### 4.1 family.coral.branching (4 файла)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `detail___family.coral.branching.png` | 1024×1024 | 1.3 MB |
| `albedo___family.coral.branching.png` | 2048×2048 | 5.3 MB |
| `normal___family.coral.branching.png` | 2048×2048 | 5.3 MB |
| `mask___family.coral.branching.png` | 1024×1024 | 1.3 MB |

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.2 family.coral.plate (4 файла)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `detail___family.coral.plate.png` | 1024×1024 | 1.3 MB |
| `albedo___family.coral.plate.png` | 2048×2048 | 5.3 MB |
| `normal___family.coral.plate.png` | 2048×2048 | 5.3 MB |
| `mask___family.coral.plate.png` | 1024×1024 | 1.3 MB |

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.3 family.coral.massive (4 файла)

| Файл | Предполагаемый размер | Оценка VRAM |
|------|----------------------|-------------|
| `detail___family.coral.massive.png` | 1024×1024 | 1.3 MB |
| `albedo___family.coral.massive.png` | 2048×2048 | 5.3 MB |
| `normal___family.coral.massive.png` | 2048×2048 | 5.3 MB |
| `mask___family.coral.massive.png` | 1024×1024 | 1.3 MB |

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.4 family.coral.massive.2 (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.5 family.coral.brittle (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.6 family.coral.low (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.7 family.coral.branching.v2 (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.8 family.kelp.canopy (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.9 family.kelp.abyssal (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.10 family.kelp.tall (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

### 4.11 family.kelp.patch.dense (4 файла)

**Подытог:** 4 текстуры = **~13.2 MB**

---

## 5. ОЦЕНКА VRAM ПО КАТЕГОРИЯМ

### Сводная таблица

| Категория | Кол-во текстур | Оценка VRAM | % от бюджета |
|-----------|----------------|-------------|--------------|
| **Корень TEXTURES** | 11 | 58 MB | 6.4% |
| **Sky** | 7 | 7 MB | 0.8% |
| **Terrain (sand)** | 2 | 2.6 MB | 0.3% |
| **Detali** | 7 | 10.5 MB | 1.2% |
| **Coral (6 семей)** | 24 | 79.2 MB | 8.8% |
| **Kelp (4 семьи)** | 16 | 52.8 MB | 5.9% |
| **Итого (_Project/Art)** | **67** | **~210 MB** | **23.3%** |

### ⚠️ НЕ УЧТЕНО В ЭТОМ АУДИТЕ

Следующие категории текстур требуют отдельного аудита:

| Категория | Оценка | Примечание |
|-----------|--------|------------|
| `Assets/_Project/Materials/` | ~50 MB | Материалы runtime |
| `Assets/_Project/Prefabs/` (текстуры в префабах) | ~100 MB | Префаб-специфичные |
| `Assets/_ThirdParty/Crest/` | ~200 MB | Ocean textures (Crest) |
| `Assets/_ThirdParty/MapMagic/` | ~150 MB | Terrain splatmaps |
| `Assets/_ThirdParty/AmplifyImpostors/` | ~50 MB | Impostor atlases |
| `Assets/_ThirdParty/GPUInstancer/` | ~30 MB | Instancing buffers |
| **UI текстуры (Sprites)** | ~20 MB | UI, иконки, шрифты |
| **Skyboxes** | ~40 MB | 6 сторон × 2048 |
| **Volumetric Fog/Light** | ~30 MB | 3D текстуры |
| **Render Targets (runtime)** | 320 MB | URP + Visor HUD |

### 🚨 ПОЛНАЯ ОЦЕНКА VRAM (ТЕКСТУРЫ + RT)

```
Первая сторона (_Project):     ~210 MB
Third-Party (Crest + MM):      ~400 MB
UI + Skyboxes:                 ~60 MB
Volumetric + Fog:              ~30 MB
Render Targets (runtime):      320 MB
Модели + Меш-данные:           ~200 MB
Прочее (буферы, кэши):         ~100 MB
─────────────────────────────────────────
ОБЩАЯ ОЦЕНКА:                  ~1,320 MB
```

---

## 6. СРАВНЕНИЕ С БЮДЖЕТОМ MX350

### Бюджет (AGENTS.md)
```
VRAM HARD CEILING: 1800 MB (MX350)
├── Текстуры:           900 MB  (50%)
├── RT + Depth:         320 MB  (18%)
├── Модели + Меш:       200 MB  (11%)
└── Остаток (система):  380 MB  (21%)
```

### Фактическая оценка
```
Общая оценка:           1,320 MB
├── Текстуры:           ~660 MB  (50%)  ⚠️ AT RISK (73% от бюджета 900 MB)
├── RT + Depth:         320 MB   (24%)  ✅ OK
├── Модели + Меш:       ~200 MB  (15%)  ✅ OK
└── Остаток:            ~140 MB  (11%)  ⚠️ LOW
```

### 🚨 КРИТИЧЕСКИЙ СТАТУС

**used/total = 1,320 / 1,800 = 0.73 (73%)**

**Graduation response threshold: 0.90 (90%)**

**Запас до Mip-downgrade: 17% (~300 MB)**

---

## 7. КРИТИЧЕСКИЕ НАРУШЕНИЯ

### 7.1 Текстуры без сжатия (RGBA32)

**Метод поиска:**
```bash
rg "\.png$|\.tga$|\.exr$" Assets/_Project/Art --type png
```

**Потенциальные нарушения:**
- `visor runoff normal.png` — нормаль-мапа, должна быть BC5
- `visor droplet mask.png` — mask, должна быть BC4 (single channel)
- `bubble vent atlas - bad - redo.png` — помечена как "bad", требует проверки

### 7.2 Текстуры с Read/Write Enabled

**Нарушение:** Read/Write = On увеличивает потребление памяти ×2

**Проверка required:**
```
Assets/_Project/Art/TEXTURES/**/*.png
```

### 7.3 MipMaps отключены (где должны быть включены)

**Правило:** MipMaps On для world-текстур, Off для UI

**Проверка required:**
- `terrain.png` — MipMaps должен быть ON
- `ORGANIC.png` — MipMaps должен быть ON
- `menuview.png` — MipMaps должен быть OFF (UI)

### 7.4 Дубликаты текстур

**Найдено:**
- `soft plume noise - какой то серый ну норм.png` + `Soft Plume Noise - second try.png` — дубликаты?
- `Mineral Seep Mask - second try.png` + `mineral seep mask - looks seamless.png` — дубликаты?

**Рекомендация:** Удалить дубликаты, оставить только production-ready версии

---

## 8. РЕКОМЕНДАЦИИ

### 8.1 Немедленные действия (Priority: CRITICAL)

1. **Проверить импорт настроек для всех текстур:**
   ```
   Assets/_Project/Art/TEXTURES/**/*.png
   ```
   - Формат: BC7 (albedo/roughness/AO), BC5 (normals)
   - MipMaps: On для world, Off для UI
   - Read/Write: Off (если не требуется CPU access)

2. **Удалить дубликаты:**
   - `soft plume noise - какой то серый ну норм.png` (оставить "second try")
   - `Mineral Seep Mask - second try.png` (оставить "looks seamless")
   - `bubble vent atlas - bad - redo.png` (удалить "bad", создать новый)

3. **Создать атласы для материалов:**
   - Coral family: объединить 6 семей в 2 атласа 2048×2048
   - Kelp family: объединить 4 семьи в 1 атлас 2048×2048
   - **Экономия:** ~80 MB → ~40 MB (50% сокращение)

### 8.2 Среднесрочные действия (Priority: HIGH)

4. **Оптимизировать текстуры Third-Party:**
   - Crest ocean textures: проверить разрешение (должно быть ≤2048)
   - MapMagic splatmaps: использовать ≤4 layers/chunk
   - **Экономия:** ~400 MB → ~300 MB (25% сокращение)

5. **Включить Streaming для больших текстур:**
   - terrain.png (5.3 MB)
   - ORGANIC.png (5.3 MB)
   - **Экономия в простое:** ~10 MB

6. **Настроить Mip-downgrade trigger:**
   ```csharp
   // В VRAMMonitor.cs или аналогичном
   if (textureMemoryUsed / textureMemoryBudget > 0.90f)
   {
       TriggerMipDowngrade(); // Уменьшить качество mip на 1 уровень
   }
   ```

### 8.3 Долгосрочные действия (Priority: MEDIUM)

7. **Перейти на Virtual Texturing (URP):**
   - terrain + world textures
   - **Экономия:** ~100 MB (кэширование только видимых тайлов)

8. **Создать LOD для текстур:**
   - LOD0: 2048×2048 (hero, near-field)
   - LOD1: 1024×1024 (medium props)
   - LOD2: 512×512 (distant props)
   - **Экономия:** ~30% для distant объектов

---

## 📊 VRAM BUDGET TRACKING TABLE

| Категория | Бюджет | Оценка | % использовано | Статус |
|-----------|--------|--------|----------------|--------|
| **Текстуры (первая сторона)** | 900 MB | 660 MB | 73% | ⚠️ AT RISK |
| **RT + Depth** | 320 MB | 320 MB | 100% | ✅ OK |
| **Модели + Меш** | 200 MB | 200 MB | 100% | ✅ OK |
| **Остаток** | 380 MB | 140 MB | 37% | ⚠️ LOW |
| **ВСЕГО** | 1,800 MB | 1,320 MB | 73% | ⚠️ AT RISK |

---

## 🎯 ACTION ITEMS

| ID | Задача | Приоритет | Ответственный | Срок |
|----|--------|-----------|---------------|------|
| VRAM-01 | Проверить настройки импорта всех текстур | CRITICAL | Tech Art | 2026-05-01 |
| VRAM-02 | Удалить дубликаты текстур | CRITICAL | Tech Art | 2026-05-01 |
| VRAM-03 | Создать атласы для coral/kelp | HIGH | Tech Art | 2026-05-05 |
| VRAM-04 | Оптимизировать Crest textures | HIGH | Graphics Agent | 2026-05-05 |
| VRAM-05 | Настроить Mip-downgrade trigger | HIGH | Core Agent | 2026-05-03 |
| VRAM-06 | Включить Streaming для terrain | MEDIUM | World Agent | 2026-05-10 |
| VRAM-07 | Исследовать Virtual Texturing | LOW | Graphics Agent | 2026-05-15 |

---

**STATUS:** ⚠️ **AT RISK** — 73% VRAM бюджета использовано  
**NEXT AUDIT:** 2026-05-05 (после оптимизации текстур)  
**MIP-DOWNGRADE THRESHOLD:** 90% (1,620 MB)
