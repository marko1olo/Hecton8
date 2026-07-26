# R96 LOCAL AGENT TASK — верификация R95/R96 + внедрение (терраформинг, воксельные пещеры, шейдеры)

Статус: TASK / STATIC_DOC. Исполнитель: локальный агент (Opus) на рабочей станции с Unity 6000.4.
Авторитет: `CLAUDE.md` → `AGENTS.md` → `COMMON_SENSE.md` → `terrain.md`, `voxels.md`, `shaders.md`. Этот файл — задание, не авторитет; при конфликте побеждает корневой закон. Обязателен process preflight перед каждым тяжёлым действием (CPU<50%, нет активных dotnet/csc/Unity-компиляций). Перед каждым прогоном с капчами — Atomic File Delete (`Remove-Item` всех старых `.png`/`.log` в выходных папках).

## Контекст: что уже сделано удалённо (R95/R96, записано в репозиторий)

Правленые файлы (все изменения помечены комментариями `R95`/`R96` — прочитай diff перед работой):

- `Assets/_Project/Scripts/HectonVoxelEngine.cs` — удалён kill-switch кадра 310; MC-lerp кламп 0.001..0.999.
- `Assets/_Project/Scripts/HectonVoxelVolume.cs` — collision gate `== Complete`; containment-отбраковка сэмплов published SDF вне AABB тома (3 сайта: density, audio material, gradient).
- `Assets/_Project/Scripts/WorldCaveDirector.cs` — ComposeCaveKey непересекающиеся 21-бит поля; сид из полного 64-бит ключа; 2D cell-hash сида кандидатов.
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs` — пруннинг `_bindingLastSeenTimes`; кэш `Comparison<T>`.
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs` — кэш `Comparison<T>`.
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs` — runtime-гвард мёртвого vault-копирования хайтмап (drain сохранён).
- `Assets/_Project/Scripts/CaveGraphGenerator.cs` — minEntrances пол 0.
- `Assets/_Project/Scripts/SurfaceTrenchGraphGenerator.cs` — normalizesafe.
- `Assets/_Project/Scripts/World/WorldProceduralCaveSdfJobs.cs` — R95 периодизация: всё поле пещер точно 6627-периодично (квантованные частоты + периодическая градиентная решётка); кламп страт ниже порога инверсии; full-3D контракт масок.
- `Assets/_Project/Shaders/HectonTerrain.shader` — удалён битый GBuffer pass; debug-кейворды → shader_feature.
- `Assets/_Project/Shaders/HectonTerrainLitPasses.hlsl` — восстановлен `-IN.tangent` (2 сайта).
- `Assets/_Project/Shaders/HectonTerrainSampling.hlsl` — NaN-щит при нулевых сплат-весах; `_TotalUniverseOffset` AUP-якорение всех паттерн-полей; удалён мёртвый coarseScaleB.

## ЭТАП 0 — Компиляция (блокер для всего остального)

1. Preflight процессов, затем: `Unity.exe -batchmode -quit -executeMethod Hecton8.Editor.BootstrapArchitectureValidator.ValidateBootstrapArchitecture`.
2. В логе: ноль CSxxxx; проверь, что Bee реально пересобрал целевые DLL (строки `Csc Library/Bee/artifacts/.../Hecton8.Core.dll` и связанных asmdef), а не cache hit. При cache hit — touch asmdef или удалить `Library/Bee/artifacts`, повторить.
3. Любая ошибка компиляции в правленых файлах — чинить по месту (правки узкие, вероятные конфликты только с локальными изменениями после 2026-07-26 13:00 UTC).

## ЭТАП 1 — Верификация R95/R96 (капчи обязательны)

1. **Kill-switch**: Play Mode в мире с воксельными томами ≥ 600 кадров. Критерий: сессия не завершается сама; в `Docs/AgentLogs/` не появляются `Dump_*.bin` без реальных fault-флагов.
2. **Тангенс/NaN террейна**: сцена по Clean Room Protocol (`terrain.md`). (a) `_DEBUG_NORMALS` капча — бамп-отклик не зеркален по X (сравнить свет с востока и запада на одном хребте); (b) временно очистить control-карту на одном тайле — нет чёрных/NaN пикселей и «светлячков» TAA (должен рендериться плоский ShellSand-fallback).
3. **X-Ray Matrix** (`terrain.md`): 9-чанковые склейки height+slope на 10 км / 1 км / 100 м. Критерии из библии (нет прямых линий на границах чанков; дендритные овраги; грит на скалах).
4. **Wrap-плоскость пещер**: в песочнице заспавнить кейв-том, пересекающий X=6627.0 (или Y=0 в прибрежной скале), карвнуть канонической джобой `ProceduralCaveSdfCarveJob` (см. ЭТАП 2C — сначала она должна стать живой) — стена без шва-плоскости. Сравнительная капча до/после.
5. **AUP-якорение террейна**: телепорт/заплыв, форсирующий ≥2 origin-сдвига; видеокапча поверхности — паттерн (пятна тона, warp, рябь) не «прыгает» в момент сдвига. Проверить, что `HectonShaderGlobalDataVaultBridge.PublishAupShaderGlobals` публикует `_TotalUniverseOffset` (float3-совместимый) ДО первого кадра террейна; если тип Vector4 — совместимо.
6. **SDF containment**: две активные пещеры; сонар-пинг и `TryReadRuntimeSdfDensity` из точки вне обоих томов — false (раньше возвращал плотность границы последнего опубликованного). Burrow-AI не ломается (fallback-путь).
7. **GC/профайлер**: 60-сек Play Mode капча — карвинг/rebuild в бюджетах voxels.md, 0 B/frame GC в тик-путях затронутых систем.

## ЭТАП 2 — Внедрение (готовые дизайны)

### A. Визуальная правда пещер: вершинные цвета вместо debug-палитры [ПРИОРИТЕТ 1 — player-visible]

Сейчас `VoxelColorJob.Execute` (`HectonVoxelEngine.cs` ~4076) пишет `normal.y > 0.6 ? (255,0,0,ao) : (0,255,0,ao)` — бинарная debug-палитра. Вся машинерия уже в джобе, но не вызвана: `TryResolveCaveMouthTerrainColor` (сплат-цвет устья с затемнением, готов), `IsModifiedSdfCell` (tool-scar detection, готов), входы `gridBiome`/`biomeValues`/`curvatureValues`. То же в `VoxelPackSurfaceVertexJob` (~4315).

Шаги:
1. Открой материал, назначенный в `HectonVoxelEngine.voxelMaterial` (инспектор сцены/префаба) — установи шейдер и СЕМАНТИКУ вершинного COLOR-канала (Frame Debugger). Если шейдер читает R/G как маски пол/стена — правь шейдерный контракт синхронно, не вслепую.
2. Замени палитру на материальную: базовый цвет из biome/материал-класса (см. `HectonMaterialPalette` в `HectonTerrainSampling.hlsl` как референс тонов), затемнение по curvature (щели темнее), `IsModifiedSdfCell` → скол/срез оттенок (свежий срез светлее + маска в A?), поверх — `TryResolveCaveMouthTerrainColor` lerp (шов устья к сплату террейна). AO остаётся в A, если шейдер так читает.
3. Критерии: Compact+High капчи пещеры изнутри и устья снаружи; нет красно-зелёного; шов устья читается блендом, не линией. Тесты бюджета: job не медленнее +10%.

### B. Per-volume SDF slots (полный фикс single-slot vault)

Containment-отбраковка (уже внедрена) убрала коррупцию, но чтение работает только в томе, опубликованном последним. Дизайн:
- Пул из 4 слотов payload в vault: `VoxelSdfTexture3D[slot]`, `VoxelSdfAudioMaterialIds[slot]`, дескриптор на слот (сейчас `descriptors[0]` — расширить до 4). 129³ байт ≈ 2.1 МБ × 2 массива × 4 слота ≈ 17 МБ RAM — в бюджете.
- Том при publish получает слот из пула (эвикция самого дальнего от игрока published-тома; гистерезис ≥3 сек — закон GlobalQualityWeight/hysteresis).
- Читатели: `TryAcquireClosestPublishedSonarSdfPayloadReadLease` уже итерирует тома — переключить на per-slot дескрипторы; `TryReadRuntimeSdfDensity` выбирает том, СОДЕРЖАЩИЙ точку (containment уже есть).
- Encode-инвалидация становится per-slot — публикация одного тома больше не глушит чтения остальных.
- Критерии: 2+ пещеры, сонар корректен в обеих; NativeMemory капча ≤ +20 МБ; нет лишних encode-джобов.

### C. Унификация поля пещер + периодизация живой копии [решение по сейвам]

`VoxelDensityJob.EvaluateGyroidCellularCaveSdf` (`HectonVoxelEngine.cs` ~2409) — живая копия поля с дрейфом констант против канонической джобы и `FloatMode.Fast`. Канон (`WorldProceduralCaveSdfJobs.cs`) после R95 периодичен и монотонен по стратам; живая копия — нет (швы на плоскостях k·6627 м, включая Y=0).
- Перенеси R95-математику (QuantizeCellsPerPeriod / PeriodicGradientNoise / WrapCell3 / кламп страт) в живую копию, выровняй константы под канон ЛИБО осознанно задокументируй расхождение в voxels.md.
- Это меняет базовое поле дельт → **bump `WorldMacroGeologyFields.ArtifactVersion` 11→12** и проверь маршрут `SaveDataMigration`/валидации identity (сейвы со старым version должны либо мигрировать, либо честно отклоняться — НЕ тихо грузиться на новом поле).
- `FloatMode.Fast` → `Deterministic` на `VoxelDensityJob` (закон voxels.md для владельца карва); профилируй дельту стоимости (ожидаемо <15% на поле).
- Критерии: капча стены на wrap-плоскости без шва; delta-replay сейва на новом поле; профайлер до/после.

### D. Вход пещеры на склоне

`CaveGraphGenerator.GenerateEntrances` ставит Y устья по высоте центра тома при XZ-смещении до 0.3×радиуса комнаты → на склоне 30° устье в скале/в воде. Дизайн: добавить в `CaveGenerationParams` (или соседний контракт) уже семплированную сетку высот тома (она есть у движка в `terrainHeights` до вызова графа) либо колбэк `float SampleHeight(float2 xz)`; в `GenerateEntrances` брать высоту в фактическом XZ устья. Критерий: капчи устьев на склонах ≥25° — воротник касается поверхности.

### E. Решение по коллизии вокселей [отчёт-решение, потом внедрение]

Факты: `Physics.BakeMesh` в проекте не существует; меш-коллайдеры томов отключены навсегда; прокси — придонные box-слабы на слое, игнорируемом игроком; `BakeState` рапортуется `Complete` без bake; мёртвая классификация треугольников жжёт CPU на каждый билд чанка (`ApplyChunkedColliderMeshesAsync` ~14150-14371).
1. Установи ФАКТИЧЕСКИЙ маршрут коллизии игрока с пещерой: чем KCC (`HectonPlayerMovement`) останавливается о стену пещеры? (SDF read model? Каст по каким слоям? Ничем — проходит сквозь?) Play Mode проверка в пещере + чтение KCC-кода.
2. Если SDF-маршрут реален — почини его питание (см. B), удали мёртвую классификацию, переименуй/почини семантику BakeState (не врать `Complete`).
3. Если коллизии нет — восстанови staged `Physics.BakeMesh`: у `HectonVoxelVolume` есть пул `VoxelPhysicsBakeMesh` и стейт-машина; верни меш-путь (job-friendly `Physics.BakeMesh(meshID, convex:false)` в фоне, апдейт `BakeState` по завершении, гейт `== Complete` уже исправлен).
4. Отчёт: выбранный маршрут, профайлер бюджет, капча стояния на полу пещеры + упора в стену.

### F. Runtime seam-projection эксперимент

`WorldGenerativeGeologyTerrainSeamApplier` глушит гибридную проекцию в Play Mode (`if (Application.isPlaying) return;` в двух местах, без комментария-обоснования). В копии сцены сними гварды, профилируй `SetHeightsDelayLOD/SyncHeightmap` на чанк при активных вокс-блендах. Если бюджет проходит (<1.5 мс пик, амортизировано) — включай в прод с гвардом бюджета; если нет — оставь и задокументируй в voxels.md, что runtime-шов покрывается только collar/skirt маршрутом (и сними мой R96-гвард копирования как ненужный вместе с мёртвым кодом).

## Прочее из аудита (по остатку бюджета)

- Неевклидова метрика carve-дельт (`VoxelDeltaProcessor.cs` ~7718 `AxisWeightedLengthApprox`) → `math.length` (сфера вместо октаэдра, ошибка до 1 м).
- Камера/качество мутируют overhang-SDF до экстракции (`HectonVoxelEngine.cs` ~10590) → сид вместо камеры, качество только на визуальные пассы.
- `EvaluateSinglePass` зонд 120 м vs 12 м в `EvaluateDifferentials` — унифицировать или задокументировать.
- Биплан: знак нормали на −X/−Z гранях (зеркальные детали) — фикс со скриншот-проверкой.
- Мёртвый `edgeTable` в `VoxelMCCountJob`; `CaveGraphGenerator.Validate` без `#if` — стрипнуть.

## Отчёт

По каждому пункту: файл/строки, что изменено, команда+exit code+ошибки/варнинги, пути капч, профайлер-числа, статус (VERIFIED с артефактом | PENDING | BLOCKED с точной причиной). Без оптимизма, без «should work». Authority receipt обязателен.
