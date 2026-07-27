Проект: C:\hades\Hecton8 (Unity 6000.5.0f1 URP, branch main)
ПРАВИЛА: хирургические правки, одна логическая часть за раз, git commit после каждого шага, только git -C c:\hades\Hecton8 <команда>, только per-file git add.

---

# P1 БАГ (Раздел 7b): Two meshes (visual LOD + canonical collider)

## СУТЬ ПРОБЛЕМЫ
Игрок проваливается сквозь коллизию пещер на низких настройках графики. 
Причина: `Surface Net Extraction` использует глобальные настройки качества (`stride` 1..4 и `decimationBias`), которые прореживают треугольники. Поскольку генерируется **только один набор треугольников** для чанка, этот же прореженный (LOD) меш уходит в физический движок (BakeMesh).
Нам нужно честное разделение: 
- **Визуальный меш:** зависит от настроек графики (`stride`, `decimationBias`).
- **Канонический физический меш:** ВСЕГДА `stride=1` и `decimationBias=0`.

## СОСТОЯНИЕ РАБОЧЕЙ ДИРЕКТОРИИ (ВАЖНО!)
Предыдущий агент оставил незакоммиченные изменения в `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` и `VoxelSurfaceNetsContracts.cs` (сделай `git diff HEAD`, чтобы увидеть).
Он попытался добавить флаг `CanonicalCollider` в `SurfaceNetExtractionJob` и поля `SamplingStride=1`, `DecimationBias=0` в DTO `VoxelSurfacePhysicsBakeRequestDTO`.
**ЭТОГО НЕДОСТАТОЧНО И ЭТО СЛОМАЕТ ПАМЯТЬ!** 

## ПОЧЕМУ ТЕКУЩИЙ КОД ОПАСЕН (АРХИТЕКТУРНЫЙ БЛОКЕР)
В `VoxelSurfaceNetsVault.cs` структура `VoxelSurfaceNetsVaultBuffers` содержит **только один набор** выходных буферов для извлечения:
- `NativeArray<VoxelVertexDTO> Vertices`
- `NativeArray<uint> Indices`

Если ты просто пробросишь флаг и запустишь `SurfaceNetExtractionJob` дважды (один раз для визуала, один раз для физики), **канонический проход тупо перезапишет визуальный меш в `Vertices` и `Indices`**. Из-за этого мы получим race conditions и сломанный рендер.

Кроме того, диспетчеризация (пайплайн) в `HectonVoxelEngine.cs` (например, `ApplyChunkedColliderMeshesAsync`) читает `TriangleIndices`, которые копируются или ссылаются на выход из Vault.

## ЧТО ТЕБЕ НУЖНО СДЕЛАТЬ
Тебе нужно провести глубокий аудит того, как треугольники перетекают из `VoxelSurfaceNetsVault` в `HectonVoxelEngine` и физику, и спроектировать параллельный не-LOD буфер.

**Шаг 1. Анализ текущих изменений**
Изучи `git diff HEAD`. Оцени, стоит ли развивать добавленный флаг `CanonicalCollider`, или лучше сделать Revert и написать чисто.

**Шаг 2. Анализ VoxelSurfaceNetsVault.cs**
Проверь структуру `VoxelSurfaceNetsVaultBuffers` (строки ~30-110). 
Нужно ли добавить туда `ColliderVertices` и `ColliderIndices`? Если да, тебе придется обновить:
- `IsCreated()`
- Инициализацию хэндлов (в методах вроде `EnsureGenerationHandle`)
- Очистку (в `TryClearBuffer`)
Либо, возможно, физический коллайдер генерируется не через этот Vault, а как-то иначе? (В `HectonVoxelEngine.cs:14436` есть `data.ScratchLease.ColliderChunkTriangleIndices`. Изучи, кто туда пишет).

**Шаг 3. Рефакторинг SurfaceNetExtractionJob**
Вместо того, чтобы жестко писать в `Vertices` и `Indices`, джоб должен принимать целевые буферы. Настрой пайплайн так, чтобы:
- Запуск визуального прохода писал в `Vertices/Indices` (используя `stride`/`decimation` из качества).
- Запуск физического прохода писал в параллельный буфер `ColliderVertices/ColliderIndices` (используя `stride=1`).
*Заметка: физике не нужны нормали/UV/AO. Возможно, физический джоб можно облегчить, но начни с надежного проброса буферов.*

**Шаг 4. Адаптация физического запроса**
В `VoxelSurfacePhysicsBakeRequestJob` (строка ~794 в `VoxelSurfaceNetsJobs.cs`) создается DTO запроса. Убедись, что система, которая ЧИТАЕТ этот DTO (вероятно, `HectonVoxelEngine.cs`), знает, что забирать треугольники нужно из `ColliderIndices`, а не из общих `Indices`.

**Шаг 5. Коммиты и проверка**
Действуй ОЧЕНЬ аккуратно. Это ядро воксельного движка. Делай `git commit` после каждой логической правки (Vault, Jobs, Engine). В конце сделай коммит с сообщением `fix(voxel/7b): separate visual LOD from canonical collider mesh`.

**ВНИМАНИЕ: Не угадывай!** Открой нужные файлы (`VoxelSurfaceNetsVault.cs`, `HectonVoxelEngine.cs`), прочитай методы диспетчеризации. Это HECTON-8, здесь требуется инженерный подход, а не слепой копипаст.
