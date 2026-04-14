# HECTON-8 — World Content / Runtime Workstream

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Что закрывает этот фронт

- Production world scene truth
- Cleanup временных сущностей
- World density
- Caves / ruins / ecology
- Runtime world ownership

## Почему это критично

Сейчас production-сцена несёт следы активной мастерской: temp, trial, staging, smoke.  
Пока это не зачищено, любая оценка готовности мира загрязнена.

## Live facts from current world scene

- Есть `Fabrication_Trial`.
- Есть `Tool_Staging`.
- Есть `__TEMP_DENSE_KELP_PREVIEW`.
- Есть `__PROCEDURAL_PROXY_WORLD`.
- Есть `__PROCEDURAL_SCATTER_WORLD`.
- На Player видны smoke-test компоненты.

## Owner files and systems

- `Assets/_Project/Scripts/SceneBootstrap.cs`
- `Assets/_Project/Scripts/World/WorldStreamingDirector.cs`
- `Assets/_Project/Scripts/World/WorldSliceDirector.cs`
- `Assets/_Project/Scripts/World/WorldInterestDirector.cs`
- `Assets/_Project/Scripts/World/WorldZoneDirector.cs`
- `Assets/_Project/Scripts/World/WorldContentDirector.cs`
- `Assets/_Project/Scripts/World/WorldPopulationDirector.cs`
- `Assets/_Project/Scripts/World/BiomeMatrixDirector.cs`
- `Assets/_Project/Scripts/World/WorldProceduralFillDirector.cs`
- `Assets/_Project/Scripts/World/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/World/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/World/WorldCaveDirector.cs`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

## Основные задачи

### Front A. Production scene cleanup

- Отделить debug/trial/staging от shipping path.
- Убрать мусор из live scene или сделать его debug-only.
- Зафиксировать truth hierarchy.

### Front B. World truth matrix

- Отметить для каждого крупного subsystem:
  - code exists
  - scene-wired
  - content-backed
  - runtime-verified
- Не путать наличие manager'а с готовностью мира.

### Front C. World density

- Surface ecology.
- Mid-depth identity.
- Deep-zone identity.
- Ruins / colony remnants / industrial remains.
- Small set pieces между hero-точками.

### Front D. Caves / geology gameplay

- Не только генерация, но и маршрут.
- Reward placement.
- Landmark readability.
- Shortcut logic.
- Visibility / pressure / fear curve.

### Front E. Procedural pipeline sanity

- Проверить, где procedural stack помогает миру, а где просто наращивает массу.
- Зафиксировать семейства, где нужны authored finals, а где достаточно runtime variation.

## Do-Not-Touch Scope

- Не трогать shell/menu/pause.
- Не трогать quest/audio log data.
- Не править save/load backend.
- Не устраивать большой архитектурный рефактор world stack без отдельного решения.

## Как дробить по агентам

Агент 1:
- `02_HECTON_WORLD.unity`
- `SceneBootstrap.cs`
- world bootstrap owners
- Задача: cleanup production path и truth hierarchy.

Агент 2:
- `WorldContentDirector.cs`
- `WorldPopulationDirector.cs`
- `BiomeMatrixDirector.cs`
- Задача: world density и биомное наполнение.

Агент 3:
- `WorldCaveDirector.cs`
- geology integration owners
- Задача: caves/geology payoff.

Агент 4:
- procedural fill/scatter owners
- Задача: sanity-check procedural contribution и content ownership.

## Expected Result

- Production world перестаёт выглядеть как мастерская.
- Мир становится чище и плотнее.
- Появляется реальное разделение между debug path и shipping path.

## Exit Criteria

- Нет temp/trial/staging мусора в live route.
- По крупным world-системам есть truth matrix.
- Есть подтверждённые маршруты caves/ruins/ecology, а не только слой генерации.
