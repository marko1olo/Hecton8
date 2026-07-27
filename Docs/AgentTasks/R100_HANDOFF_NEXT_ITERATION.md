# R100 — Handoff Task Brief for Agent Iteration (Cline / Claude / Gemini)

Status: `ALL_R100_TASKS_COMPLETE` | Date: 2026-07-27 | Target: `C:\hades\Hecton8` (Branch: `main`)

---

## 0. ТЕКУЩИЙ СТАТУС ВЫПОЛНЕНИЯ (PROVED IN GIT)

- ✅ **Задача 1.1 (Фикс 8e — сталактиты/столбы)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit edb91b21a`).
- ✅ **Задача 1.2 (Фикс 8b — шипы геометрии треугольников)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit 636c10cdd`), PUSHED TO REMOTES.
- ✅ **Задача 1.3 (Burst managed array — VoxelMCExtractJob)**: ВЫПОЛНЕНА ранее (commit `b126deeba`).
- ✅ **Задача 1.4 (Texture VRAM 2048→1024)**: ВЫПОЛНЕНА ранее (`commit 62ce6613b`).
- ✅ **Задача 1.5 (P0 — удалить дубликаты бейкеров)**: ВЫПОЛНЕНА (`commit fdf199453`). Удалены `BakeTerrainArrays.cs`, `RebuildTexArrays.cs`, `TextureBakerRun.cs` + `.meta`.
- ✅ **Задача 1.6 (P1 — CPU-куллинг растительности)**: ПОДТВЕРЖДЕНА РАНЕЕ (`commit e1240c476`).
- ✅ **Задача 1.7 (Фикс 7a — cliff overhang noise gate)**: ВЫПОЛНЕНА в R99 (`commit a2931077f`). `ApplyVoxelCliffOverhangNoise` применяется безусловно, `if (quality > 0.05f)` гейт удалён.
- ✅ **Задача 1.8 (Фикс 8a — weld scratch buffer contamination)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit 0be82120a`). Добавлен `weldOutputFault` gating: при переполнении велда пайплайн возвращает `false` до того, как sanitize/render/collider читают устаревший хвост `TriangleIndices`.

---

## 1. ПРАВИЛА ДЛЯ СЛЕДУЮЩЕЙ ИТЕРАЦИИ

1. **Правило хирургических правок**:
   - Работай в подпапке `Hecton8`.
   - Вноси изменения строго по ОДНОЙ функции/файлу за раз с git commit после каждого шага.

2. **Правило терминала Windows PowerShell**:
   - Используй раздельные вызовы или `git -C c:\hades\Hecton8 <command>`.

---

- ✅ **Задача 1.9 (P0 — Коллизия пещер)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА ранее (`commit 33e7c47c1`). Код уже есть в ветке `main`. Агент только выполнил аудит и подтвердил наличие фикса.

---

- ✅ **Задача 1.10 (P1 — Раздел 9a: float precision terrain)**: ВЫПОЛНЕНА И ЗАКОММИЧЕНА (`commit 93f0ea1df`).

---

## 2. СЛЕДУЮЩИЕ ПРИОРИТЕТЫ (из HANDOFF_CLAUDE_CODE.md)

Все R100 задачи, P0 (коллизия пещер) и первый P1 (float precision) выполнены. Следующие открытые дефекты из `HANDOFF_CLAUDE_CODE.md`:

### P1 — Раздел 7b: Two meshes (visual LOD + canonical collider)
* Surface Nets stride/decimationBias влияют на физический меш → коллизия зависит от качества.
* **Фикс:** канонический физический меш должен строиться с максимальной детализацией (`stride=1`, `decimationBias=0`), а визуальный — с учётом настроек качества.

---

## 3. ПРОВЕРКА И ПРИЁМКА

После каждой задачи выполнять:
```shell
git -C c:\hades\Hecton8 add Assets/<changed_file>
git -C c:\hades\Hecton8 commit -m "fix(R100): <описание задачи>"
```
