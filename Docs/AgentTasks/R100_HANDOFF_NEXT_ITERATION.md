# R100 — Handoff Task Brief for Agent Iteration (Cline / Claude / Gemini)

Status: `TASK_BRIEF` | Date: 2026-07-27 | Target: `C:\hades\Hecton8` (Branch: `main`)

---

## 0. ТЕКУЩИЙ СТАТУС ВЫПОЛНЕНИЯ (PROVED IN GIT)

- ✅ **Задача 1.1 (Фикс 8e — сталактиты/столбы)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit edb91b21a`).
- ✅ **Задача 1.2 (Фикс 8b — шипы геометрии треугольников)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit 636c10cdd`), PUSHED TO REMOTES.

---

## 1. ПРАВИЛА ДЛЯ СЛЕДУЮЩЕЙ ИТЕРАЦИИ

1. **Правило хирургических правок**:
   - Работай в подпапке `Hecton8`.
   - Вноси изменения строго по ОДНОЙ функции/файлу за раз с git commit после каждого шага.

2. **Правило терминала Windows PowerShell**:
   - Используй раздельные вызовы или `git -C c:\hades\Hecton8 <command>`.

---

## 2. ИСПОЛНИТЕЛЬНОЕ ЗАДАНИЕ (Следующие P0 задачи)

### Задача 1.3: P0 — Устранение managed array / throw ошибки Burst в `VoxelMCExtractJob`
* **Файл**: `Hecton8/Assets/_Project/Scripts/HectonVoxelEngine.cs` (или `MarchingCubesLookupTable.cs`)
* **Описание**: `MarchingCubesLookupTable.Calculate` индексирует управляемый `static readonly int[] EdgeTable` и бросает `throw new ArgumentException(...)`, что не поддерживается Burst compiler. В `VoxelMCExtractJob` поле `edgeTable` уже прокинуто как `NativeArray`.
* **Действие**: Заменить вызов `MarchingCubesLookupTable.Calculate(...)` на точечное чтение `edgeTable[cubeIndex]`.

### Задача 1.4: P0 — Фикс 4x перерасхода VRAM сплат-текстур террейна (2048 -> 1024)
* **Файл**: `Hecton8/Assets/_Project/Scripts/Editor/Terrain/HectonTerrainTextureArrayBuilder.cs`
* **Описание**: Поле `_resolution` равно 2048, что раздувает VRAM до 128 МиБ вместо 32 МиБ бюджетных.
* **Действие**: Установить `_resolution = 1024` и добавить hard-assert на ширину 1024.

---

## 3. ПРОВЕРКА И ПРИЁМКА

После каждой задачи выполнять:
```shell
git -C c:\hades\Hecton8 add -A
git -C c:\hades\Hecton8 commit -m "fix(R100): <описание задачи>"
```
