# R97 ADDENDUM — стриминг/пейджер/сэмплер: верификация + внедрение

Статус: TASK / STATIC_DOC. Дополнение к `R96_LOCAL_AGENT_TASK.md` — выполнять ПОСЛЕ его этапов 0-1 (или параллельно, если R96 закрыт). Авторитеты и process-гейты те же. Все findings ниже подтверждены цитатами в статическом аудите 2026-07-26; правки R97 уже в репозитории (помечены `R97` в комментариях).

## Уже внедрено удалённо (проверить компиляцией + прогоном)

- `WorldProceduralFieldSampler.cs`: подписка на `MapMagicTerrainTileEvents` → сброс кэша высот при apply/move тайла (раньше геймплейная глубина жила на fallback-высотах после стрима реального террейна — НАВСЕГДА); isfinite-гвард MapMagic-лейна; `ChooseFamily` без params-аллокации; force-complete перед снятием пинов в `MarkScatterSamplingJobCompleted`; детерминированный Y-независимый synthetic fallback.
- `TerrainChunkPagerTypes.cs`: `CommitByteBudgetPerFrame >= ChunkByteCapacity` (анти-livelock); job-side `FindSlotByHash` учитывает `MissingFile` (стоп ежекадровому busywork по павшим секторам).
- `TerrainChunkPagerRuntime.cs`: first-commit-always в `VisualSyncTick`; retry/backoff-лейн для транзиентных IO-ошибок (~4 с, затем слот освобождается — сектор больше не отравляется на всю сессию); сохранение VisualSync-лейна при deferred shutdown (утечка воркера + слабов вылечена).

Верификация: компиляция (этап 0 R96); Play Mode со стримингом — счётчики пейджера (`TryReadCounters`) не растут по IoError бесконечно; симуляция ошибки (залочить один `sector_*.h8bin` вручную) → сектор восстанавливается после снятия лока ≤10 с.

## КРИТИЧЕСКОЕ РЕШЕНИЕ 1: `WorldChunkPhysicsBakedSignal` НЕ СУЩЕСТВУЕТ

Контракт AGENTS.md (Kinematic Arrest Gate): спавнер держит игрока подвешенным, пока `WorldStreamingDirector` не бродкастнет `WorldChunkPhysicsBakedSignal` для AUP-чанка спавна. Факт: идентификатор существует ТОЛЬКО в AGENTS.md — ни паблишера, ни сигнала, ни консюмера в исходниках. Либо гейта нет (игрок может провалиться сквозь асинхронный террейн — ровно тот баг, ради которого контракт писан), либо спавнер ждёт вечно.

1. Установи, что реально делает `HectonPlayerSpawner` (52КБ) при спавне: чего он ждёт? Есть ли `IsSuspended`-механика?
2. Внедри сигнал: unmanaged `WorldChunkPhysicsBakedSignal { long ChunkX, ChunkZ; uint Generation; uint Flags; }` на `SignalBus<T>`; паблиш после ДОКАЗАННОЙ синхронизации коллайдера чанка (после `SyncHeightmap` + `TerrainCollider` активен — состояние ловится из `TerrainChunkGeneratedSignal`-цепочки MapMagicBridge либо polling-стейт-машиной по протоколу MapMagic из AGENTS.md); обязательно failure-вариант флагом на error-путях — гейт всегда разрешается, таймауты по времени запрещены.
3. Свяжи спавнер с сигналом по контракту.
Критерий: спавн над несгенерированным чанком → игрок подвешен → сигнал → приземление; лог-капча последовательности; спавн при павшей генерации → failure-флаг → аварийный маршрут спавнера, не вечный подвес.

## КРИТИЧЕСКОЕ РЕШЕНИЕ 2: пейджер — тупиковый конвейер

`TerrainChunkPagerRuntime` коммитит чанки в Active-слаб и НИКОГО не уведомляет; ни один класс не читает его буферы/статики. Параллельно живёт второй путь (MapMagicBridge → `TerrainChunkGeneratedSignal` → SeamApplier). Реши судьбу: (a) пейджер — будущий владелец дисковых чанков → добавь per-commit сигнал (слот, сектор, generation) и подключи консюмера террейна; (b) пейджер преждевременный → выключи его тики до готовности консюмера (не жги CPU/RAM: слабы ×2 по `DefaultMaxChunkSlots`×`ChunkByteCapacity`). Задокументируй выбор в terrain.md/streaming.md.

## HIGH (внедрять после решений)

1. **Worker write-after-free при rebind-таймауте** (`TerrainChunkPagerRuntime` ~1648): воркер пишет через сырой указатель vault-слаба сквозь блокирующее IO; при неудачном join (2 с) и подмене vault старые буферы могут быть освобождены под указателем. Дизайн: воркер пишет в собственный Persistent-скретч (не vault), main-thread memcpy в слаб при drain результатов; либо vault-side pin/refcount на запрос.
2. **Идентичность чанк-файлов** (`TerrainChunkFileHeaderDTO`): добавить `WorldSeed`(ulong), `ContentVersion`(uint), эхо `SectorX/SectorZ`; reject при несовпадении с fault-флагом; `FileVersion` 1→2 (кэш регенерируется). Синхронно обнови писателя файлов (найди его; если это внешний бейкер/питон — и его).
3. **Read-purity сэмплера**: `TrySampleSeafloor`/`TryResolveSeafloorSource`/`TrySampleBiomePhysicsInfluence` при `!_samplingFramePrepared` вызывают `BeginScatterSamplingFrame` → force-complete job + заполнение 512×512 noise-таблицы (262k×4 snoise) на main thread + рост vault-буферов ИЗ READ-АКЦЕССОРА (физика может дёрнуть). Дизайн: reads fail-soft (false/last-good) при неподготовленном кадре; `BeginScatterSamplingFrame` зовёт только tick-владелец. Проверь всех вызывающих на устойчивость к false.
4. **Bake-generation штамп** для `CellOutputData` индексов (сэмплер ~2776): outputs, произведённые до ребейка списков, после него молча резолвятся в чужой профиль/зону. Штамп поколения при schedule, reject при несовпадении.

## MEDIUM

- Bridge: double-лейны AUP (`TryGetHeightAUP(in double3)`) вместо усечения в float (~1068-1105 + `ToVector3`); квантование высот 1.6 см на 131 км, 12.5 см на 1048 км.
- Bridge: `TryGetTerrainArtifactIdentity` хардкодит `DefaultAuthoringSeed`, сэмплер использует сериализованный `macroGeologyAuthoringSeed` — сейв-идентичность может описывать не тот террейн. Единый источник сида.
- Bridge: `QuantizedHeightmapPayload` — голый `NativeArray<ushort>` без generation-handle; свап посреди чтения не детектируем. Провести через `VaultGenerationHandle`.
- Единый резолвер `double3 AUP → чанк`: сейчас три системы координат (WorldChunkCoordinate float/`Mathf.FloorToInt`, пейджер double/long, `TryResolveSafeSpawnChunkCoordinate`) — на >16 км float мис-флурит границы; гейт/спавнер/пейджер могут разойтись в определении чанка спавна. Обобщить `ResolveSectorCoord`.
- `Terrain.SetNeighbors` отсутствует во всём проекте — проверь, линкует ли MapMagic соседей рантайм-тайлов сам; иначе LOD-стыки хайтмап не синхронизированы (шов-закон). Fix при подтверждении.
- Пейджер: ungated main-thread чтения metadata при летящих jobs (`OnDrawGizmos`, `TryGetDebugCell`, `TryReadCounters`, `TryReadTuning`) — early-out при `_pendingResidency|_pendingEviction`.
- `HectonVoxelStreamingBridge`: fire-and-forget `SpawnCaveAsync` глотает не-cancellation исключения без black-box; fade-путь мутирует shared-материалы `SetFloat` вместо MPB (нужна либо MPB-миграция, либо явное mandate-исключение).

## Перенос (аудит не завершён удалённо — честный остаток)

`HydraulicErosionJob.cs` (67КБ) и `HectonSandboxAbyssalShelfJobs.cs` (58КБ) прочитаны НЕ были (бюджет сессии). Прогони по чек-листу terrain.md: запрет `math.abs`-зеркала координат, дендритный дренаж на 1-км slope-карте, грит 1.5-3.5 м через HardRock/Scree маски, Burst-атрибуты по библии, детерминизм. Плюс `HectonAnomalyEngine/SdfJobs` — полные файлы.

## Отчёт

Формат R96: файл/строки, команды+exit codes, капчи, профайлер, статус VERIFIED/PENDING/BLOCKED. Без оптимизма.
