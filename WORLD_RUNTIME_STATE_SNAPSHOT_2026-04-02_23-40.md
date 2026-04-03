# WORLD_RUNTIME_STATE_SNAPSHOT_2026-04-02_23-40

## Что подтверждено в Unity

Это snapshot текущего честного baseline после возврата scatter к безопасной
логике.

Подтверждено:

- cave/geology path снова жив
- пользователь снова видит cave placeholders
- пользователь снова видит `Low Oxygen`, то есть часть ранее пропавшего world path
  вернулась не только по цифрам, но и по реальному игровому восприятию
- новые first-party compile errors после последних правок отсутствуют

## Опорный runtime trace

- [Hecton8_runtime_2026-04-02_23-40-05.log](C:/Users/danat/AppData/LocalLow/Danat%20Games/Submerge/Diagnostics/Hecton8_runtime_2026-04-02_23-40-05.log)

## Главные подтверждённые факты

- startup scatter:
  - `rebuild=535.88ms`
  - `sample=354.17ms`
  - `reconcile=137.50ms`
  - `created=153`
- startup slow tick:
  - `WorldProceduralScatterDirector=558.21ms`
  - `WorldGenerativeGeologySeamExecutionDirector=70.37ms`
- startup runtime:
  - `gc=14399323B`
  - `geoBindings=7`
  - `geoRoots=7`
  - `geoRenderers=117`
  - `geoVoxels=0`
- обычный rebuild после движения:
  - `rebuild=126.84ms`
  - `sample=116.51ms`
  - `reconcile=7.05ms`
  - `created=14`
  - `reused=138`
- повторяющийся GC spike всё ещё есть:
  - `window=6 gc=5115197B`
- pool miss всё ещё есть:
  - `PFB_family_coral_branching_Placeholder`

## Честный вывод по состоянию

- baseline мира восстановлен
- caves/geology больше не считаются "пропавшими"
- производительность всё ещё далека от production-ready
- главный подтверждённый CPU-виновник сейчас:
  - scatter sampling
- главный подтверждённый runtime-churn риск сейчас:
  - recurring GC
  - pool expansion на горячих семействах

## Что нельзя забывать

- Любая следующая оптимизация должна сравниваться именно с этим snapshot.
- Любое "ускорение", при котором caves/geology снова уйдут в ноль, считается
  регрессом вне зависимости от красивых миллисекунд.

## Обновление 2026-04-03

- После восстановления baseline был проверен отдельный эксперимент:
  динамическое сжатие `radiusCells` через streaming budget path.
- Эксперимент оказался неудачным и удалён из кода.
- Причина удаления:
  - во время движения он сам вызывал `dirty:scatter-radius-scale`
  - это создавало дополнительные runtime rebuild прямо в живом плавании
  - в результате подфризы субъективно усилились, хотя caves/geology оставались живы
- Подтверждённый trace неудачного эксперимента:
  - [Hecton8_runtime_2026-04-02_23-48-50.log](C:/Users/danat/AppData/LocalLow/Danat%20Games/Submerge/Diagnostics/Hecton8_runtime_2026-04-02_23-48-50.log)
- Подтверждённые факты по нему:
  - `cells=169`, то есть окно действительно сжалось
  - но появились rebuild с причиной `dirty:scatter-radius-scale`
  - были spikes:
    - `rebuild=112.51ms`
    - `sample=83.44ms`
    - `reconcile=27.30ms`
  - были крупные runtime окна:
    - `window=17 frame=439.30ms`
    - `window=23 gc=4010685B`
  - memory footprint во время прогона доходил примерно до `4423.8MB`
- Инженерный вывод:
  - менять scatter radius на лету через runtime budget switching нельзя
  - безопасные оптимизации дальше должны уменьшать CPU-стоимость sampling/reconcile
    без дополнительной инвалидации scatter окна во время движения

## Обновление 2026-04-03 — честный GC trace

- Был найден отдельный источник ложных GC spikes:
  `RuntimePerformanceProfiler` автоматически запускал renderer ownership audit на каждом GC spike,
  а audit внутри себя делал `FindObjectsByType<Renderer>()`.
- Это означало, что часть повторяющихся `~5 MB` окон загрязнялась самим диагностическим кодом.
- После правки profiler:
  - автоматический audit по GC spike по умолчанию выключен
  - добавлен cooldown на повторный ownership audit
- Подтверждённый trace после правки:
  - [Hecton8_runtime_2026-04-03_00-20-30.log](C:/Users/danat/AppData/LocalLow/Danat%20Games/Submerge/Diagnostics/Hecton8_runtime_2026-04-03_00-20-30.log)
- Подтверждённые факты по нему:
  - startup всё ещё тяжёлый:
    - `rebuild=204.51ms`
    - `sample=113.92ms`
    - `window=1 gc=7739502B`
  - после старта повторяющийся `~5 MB` мусор почти ушёл:
    - `window=2 gc=5099924B`
    - `window=3 gc=111986B`
    - `window=4 gc=103985B`
    - `window=5 gc=104929B`
    - `window=6 gc=627116B`
- Честный вывод:
  - большой кусок старых регулярных `~5 MB` spikes был не игровым, а диагностическим
  - реальная оставшаяся проблема GC теперь в основном концентрируется в startup / раннем scatter burst
  - steady-state runtime стал заметно чище по мусору
## Обновление 2026-04-03 — startup scatter prime

- В `SceneBootstrap` добавлен безопасный pre-activation pass для `WorldProceduralScatterDirector`.
- Смысл правки:
  - не менять правила мира
  - не резать caves/geology
  - не крутить runtime radius
  - просто увести первый тяжёлый scatter burst под загрузочный экран до `ActivatePlayer()`
- Реализация:
  - bootstrap теперь может сделать до `2` scatter-pass'ов до выдачи управления игроку
  - scatter получил отдельный bootstrap-only bypass для defer-логики, который не меняет обычный runtime path
  - если после первого pass ещё есть `_hasPendingStartupPlacements`, bootstrap даёт второй pass и только потом активирует игрока
- Что реально проверено:
  - first-party compile errors после правки нет
  - короткий startup playmode smoke run после правки не дал новых first-party runtime warnings
- Честное ограничение:
  - без полноценного живого маршрута игрока этот шаг пока подтверждён как безопасный по коду и по compile/smoke run
  - его реальный эффект по ранним фризам и `window=1/window=2` нужно подтверждать следующим честным runtime trace с плаванием
## Обновление 2026-04-03 — field sampler diagnostics

- В `WorldProceduralFieldSampler` найден ещё один реальный hot-path overhead.
- До правки:
  - `TrySampleSeafloor()` и `EvaluateHeatmap()` обновляли inspector diagnostics на каждом вызове
  - это происходило внутри цикла `клетка × правило`
  - то есть sampler сам тратил CPU на debug-state даже в обычном runtime
- Исправление:
  - live diagnostics sampler-а теперь выключены по умолчанию в playmode
  - они остаются доступными как opt-in для точечной отладки
- Честный вывод:
  - это безопасная production-oriented правка
  - она не меняет правила генерации мира
  - она режет чисто служебную работу, которая не должна была жить в hot path
