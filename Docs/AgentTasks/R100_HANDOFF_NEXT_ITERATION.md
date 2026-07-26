# R100 — Handoff Task Brief for Agent Iteration (Cline / Claude / Gemini)

Status: `TASK_BRIEF` | Date: 2026-07-26 | Target: `C:\hades\Hecton8` (Branch: `main`)

---

## 0. ОТВЕТ НА ВОПРОС: Вписались ли предыдущие изменения?
**НЕТ.** Из-за ошибки поиска строки в `replace_in_file` и последующего блокера `400 content-blocked`, правки в `HectonVoxelEngine.cs` **НЕ записались на диск**. Дерево проекта полностью чистое (`git status` clean, commit `76d4e2ff1`).

---

## 1. ПРАВИЛА ИЗБЕЖАНИЯ СБОЕВ ДЛЯ СЛЕДУЮЩЕЙ ИТЕРАЦИИ (Анти-Обсёр Инструкция)

1. **Правило хирургических правок в `HectonVoxelEngine.cs` (15 353 строки, 671 КБ)**:
   - **ЗАПРЕЩЕНО** объединенять неcмежные функции (например, строки 2948 и 4583) в один дифф-блок `replace_in_file`.
   - Заменять строго по **ОДНОЙ структуре/функции за раз**.
   - Соблюдать Windows `CRLF` переносы строк при поиске точного фрагмента.

2. **Правило терминала Windows PowerShell**:
   - **ЗАПРЕЩЕНО** использовать оператор `&&` в PowerShell командной строке (`cd Hecton8 && ...` выбивает синтаксическую ошибку).
   - Используй точка с запятой `;` или раздельные команды.

3. **Защита от 400 content-blocked**:
   - Не передавать в промпт огромные дампы системных логов с ошибками и матерными словами.
   - При появлении `content-blocked` — нажмите **Start New Task** и подайте этот документ с чистого листа.

---

## 2. ИСПОЛНИТЕЛЬНОЕ ЗАДАНИЕ (План работ R100)

### СТАДИЯ 1 — Хирургические P0 фиксы в `HectonVoxelEngine.cs`

#### Задача 1.1: Фикс 8e (Молчаливый выброс сталактитов/столбов из-за 8 углов)
* **Файл**: `Assets/_Project/Scripts/HectonVoxelEngine.cs` (строки ~2948-2971, структура `VoxelChunkBoundsContentJob`).
* **Дефект**: Проверка `allCornersVoid` сэмплила только 8 угловых вокселей 128³ тома. Чанки с водой по углам, но со сталактитами/скалой внутри, молча выбрасывались (`hasContent[0] = 0`).
* **Точная правка**: Заменить метод `Execute()` структуры `VoxelChunkBoundsContentJob` на:
```csharp
    public void Execute()
    {
        if (!hasContent.IsCreated || hasContent.Length <= 0)
            return;

        hasContent[0] = 0;
        if (!density.IsCreated || ptsX <= 0 || ptsY <= 0 || ptsZ <= 0 || !HasCompleteDensityField())
            return;

        int total = ptsX * ptsY * ptsZ;
        for (int i = 0; i < total; i++)
        {
            float value = density[i];
            if (math.isfinite(value) && value >= 0f)
            {
                hasContent[0] = 1;
                return;
            }
        }
    }
```

#### Задача 1.2: Фикс 8b (Исправление шипов геометрии при выбитом индексе)
* **Файл**: `Assets/_Project/Scripts/HectonVoxelEngine.cs` (строки ~4583-4595, структура `VoxelSanitizeTriangleIndexJob`).
* **Дефект**: Переписывание одиночного битого индекса в `0` приводило к длинному тонкому полигональному шипу от поверхности к вершине 0.
* **Точная правка**: Заменить метод `Execute(int triangleIdx)` структуры `VoxelSanitizeTriangleIndexJob` на работу тройками вершин:
```csharp
    public void Execute(int triangleIdx)
    {
        if (!triangleIndices.IsCreated || triangleIdx < 0)
            return;

        int triangleCount = indexCount / 3;
        if (triangleIdx >= triangleCount)
            return;

        int baseIndex = triangleIdx * 3;
        if (baseIndex + 2 >= triangleIndices.Length)
            return;

        int i0 = triangleIndices[baseIndex];
        int i1 = triangleIndices[baseIndex + 1];
        int i2 = triangleIndices[baseIndex + 2];
        bool valid =
            (uint)i0 < (uint)vertexCount &&
            (uint)i1 < (uint)vertexCount &&
            (uint)i2 < (uint)vertexCount;
        if (valid)
            return;

        triangleIndices[baseIndex] = 0;
        triangleIndices[baseIndex + 1] = 0;
        triangleIndices[baseIndex + 2] = 0;
        if (densityFaultFlags.IsCreated && (uint)VoxelDensityPipelineFaultSlots.WeldOutput < (uint)densityFaultFlags.Length)
            densityFaultFlags[VoxelDensityPipelineFaultSlots.WeldOutput] = 1;
    }
```

---

### СТАДИЯ 2 — Компиляция и Прогоны (Доказательный запуск)

1. **Компиляционный тест**:
   ```powershell
   Remove-Item -Path "Logs\*.log" -Force -ErrorAction SilentlyContinue
   & "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\hades\Hecton8" -executeMethod Hecton8.Editor.BootstrapArchitectureValidator.ValidateBootstrapArchitecture -logFile "C:\hades\Hecton8\Logs\R100_compile.log"
   ```
   Убедиться, что `error CS` = 0 и timestamp `Hecton8.Core.dll` обновился.

2. **X-Ray 1 км Slope**:
   - Очистить старые PNG: `Remove-Item -Path "Docs\GeneratedAssets\Terrain\*.png" -Force -ErrorAction SilentlyContinue`.
   - Запустить генерацию X-Ray склейки 1 км slope и убедиться в отсутствии рамок чанков.

3. **Цементирование**:
   - Выполнить `git add` изменённых файлов и сделать `git commit -m "fix(voxel/R100): P0 geometry fixes for 8e stalactites and 8b triangle sanitization"`.

---
`Authority: AGENTS.md; GEMINI.md; HANDOFF_CLAUDE_CODE.md; R100_HANDOFF_NEXT_ITERATION.md`
