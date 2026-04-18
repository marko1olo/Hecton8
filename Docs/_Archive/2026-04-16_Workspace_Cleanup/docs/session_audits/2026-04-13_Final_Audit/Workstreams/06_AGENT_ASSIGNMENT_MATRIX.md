**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Agent Assignment Matrix

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

Это не общий план. Это прямой лист раздачи задач агентам.

## Волна 1

### Agent 1 — Main Menu / Save-Load Flow

Owner files:

- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`

Задача:

- Добить main menu flow.
- Убрать пустые и тупиковые panel states.
- Привести `new game / load game / back / cancel` к одному понятному сценарию.
- Проверить default selection и возвраты.

Не трогать:

- `PauseMenuController.cs`
- input rebinding
- narrative systems
- save backend contract в `SaveManager`

Результат:

- Main menu перестаёт быть полузаглушкой.
- Save/load path выглядит как production shell.

Критерий готовности:

- Нет тупиковых состояний.
- Все back-paths закрыты.
- Пользователь может стабильно пройти menu -> load/new game -> world.

### Agent 2 — Pause Shell

Owner files:

- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/PauseMenuHost.cs`

Задача:

- Довести pause menu.
- Проверить секции `Main / Saves / Help / Settings`.
- Исправить selection defaults и возвраты.
- Проверить pause resume path и переход назад в main menu.

Не трогать:

- `MainMenuController.cs`
- quest / lore systems
- world bootstrap

Результат:

- Pause перестаёт быть хрупким shell-слоем.

Критерий готовности:

- Нет разваленных переходов.
- Нет пустых section routes.
- Проверен сценарий pause -> settings/save -> resume.

### Agent 3 — Pause Rebinding UI

Owner files:

- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`

Задача:

- Довести rebinding UI в pause.
- Проверить reset/apply/save/cancel.
- Проверить поведение при missing binding rows.
- Привести статусы и текст к внятному виду.

Не трогать:

- `PDAControlsRebindUI.cs`
- `MainMenuController.cs`
- general options persistence owner

Результат:

- Rebinding в pause работает как отдельный законченный слой.

Критерий готовности:

- Rows корректно строятся.
- Overrides сохраняются.
- Ошибки и пустые bindings не ломают UI.

### Agent 4 — PDA Rebinding UI

Owner files:

- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`

Задача:

- Довести rebinding UI в PDA.
- Проверить tab switching, row resolution, reset/save flow.
- Проверить consistency с `RebindingManager`.

Не трогать:

- `PauseControlsPanel.cs`
- `MainMenuController.cs`
- lore / quest files

Результат:

- PDA controls panel не выглядит недоделанным дубликатом.

Критерий готовности:

- PDA rebinding path стабилен.
- Overrides читаются и сохраняются без рассинхрона.

### Agent 5 — Options Persistence Owner

Owner files:

- новый owner под user options
- минимальные точки входа в menu/pause UI
- `Assets/_Project/Scripts/Input/RebindingManager.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`

Задача:

- Создать единый persistence слой для не-input настроек.
- Зафиксировать contract хранения опций.
- Подключить menu/pause к этому owner'у без расползания логики.

Не трогать:

- main menu layout
- pause shell layout
- world systems

Результат:

- В проекте появляется единый владелец пользовательских настроек.

Критерий готовности:

- Настройки сохраняются между сессиями.
- Есть явный owner вместо разрозненных `PlayerPrefs` островков.

## Волна 2

### Agent 6 — Narrative Spine

Owner files:

- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/NarrativeEvents.cs`
- `Assets/_Project/Data/Lore/Registries`
- `Assets/_Project/Data/Lore/DepthZones`

Задача:

- Собрать narrative spine первого часа.
- Заполнить discovery layer.
- Привязать depth beats к registries и событиям.

Не трогать:

- quest assets
- audio logs
- suit upgrades
- menu/pause

Результат:

- Появляется осмысленный narrative backbone вместо абстрактного лора.

Критерий готовности:

- Есть минимум один связный narrative route.
- Discovery IDs и progression links не пустые и не висят в воздухе.

### Agent 7 — Quest Content

Owner files:

- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestData.cs`
- `Assets/_Project/Scripts/Quest/QuestEvents.cs`
- `Assets/_Project/Data/Lore/Quests`

Задача:

- Создать реальные quest assets.
- Определить trigger points.
- Проверить активацию от существующих событий.

Не трогать:

- audio logs
- suit upgrades
- world cleanup

Результат:

- Quest system выходит из состояния пустой инфраструктуры.

Критерий готовности:

- `Data/Lore/Quests` больше не пустой.
- Есть хотя бы один рабочий квестовый маршрут.

### Agent 8 — Audio Logs

Owner files:

- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogData.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
- `Assets/_Project/Data/Lore/AudioLogs`

Задача:

- Создать audio log assets.
- Привязать pickup flow.
- Проверить discovery и PDA presentation.

Не трогать:

- quest logic
- suit upgrades
- menu/pause

Результат:

- Audio log system начинает существовать как контент, а не только как код.

Критерий готовности:

- `Data/Lore/AudioLogs` не пустой.
- Игрок может подобрать лог и увидеть/проиграть его через PDA.

### Agent 9 — Suit Progression

Owner files:

- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs`
- `Assets/_Project/Scripts/Gameplay/SuitHUDProfile.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Data/Lore/SuitUpgrades`

Задача:

- Создать data-driven suit upgrades.
- Привязать unlock conditions.
- Проверить отражение состояния в HUD.

Не трогать:

- quests
- audio logs
- pause/menu

Результат:

- У прогрессии появляется осязаемый слой улучшений.

Критерий готовности:

- `Data/Lore/SuitUpgrades` не пустой.
- Upgrade path реально влияет на состояние игрока и HUD.

### Agent 10 — Lore Bootstrap Integration

Owner files:

- `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`
- `Assets/_Project/Scripts/Editor/HectonLoreSceneSetupEditor.cs`
- `Assets/_Project/Scripts/Editor/HectonLoreSystemsRootEditor.cs`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Задача:

- Гарантировать наличие live `LoreSystems` root в production world path.
- Проверить, что lore systems реально поднимаются в сцене.
- Не допустить состояния "код есть, в live-мире не живёт".

Не трогать:

- content authoring
- shell/menu
- world density

Результат:

- Narrative stack перестаёт быть призраком в коде.

Критерий готовности:

- В `02_HECTON_WORLD` подтверждён live root.
- Системы реально инстанцируются в production path.

## Волна 3

### Agent 11 — Production World Cleanup

Owner files:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Scripts/SceneBootstrap.cs`
- world bootstrap owners

Задача:

- Зачистить production path от `temp / trial / staging / smoke`.
- Отделить debug-only route от shipping route.
- Зафиксировать truth hierarchy.

Не трогать:

- quests/audio logs
- main menu/pause
- save backend

Результат:

- Production world перестаёт быть активной мастерской.

Критерий готовности:

- В live route нет временного мусора.
- Debug path и shipping path отделены.

### Agent 12 — World Density / Biomes

Owner files:

- `Assets/_Project/Scripts/World/WorldContentDirector.cs`
- `Assets/_Project/Scripts/World/WorldPopulationDirector.cs`
- `Assets/_Project/Scripts/World/BiomeMatrixDirector.cs`

Задача:

- Усилить world density.
- Добить биомную дифференциацию.
- Добавить смысл между hero-точками.

Не трогать:

- shell/UI
- lore bootstrap
- save backend

Результат:

- Мир перестаёт держаться только на backbone и procedural mass.

Критерий готовности:

- Есть читаемые различия по биомам и слоям мира.
- Между крупными точками появились meaningful fillers.

### Agent 13 — Caves / Geology Gameplay

Owner files:

- `Assets/_Project/Scripts/World/WorldCaveDirector.cs`
- geology integration owners

Задача:

- Довести caves до уровня маршрутов, а не только генерации.
- Проверить rewards, landmarks, shortcuts, fear/visibility curve.

Не трогать:

- menu/pause
- quests
- general perf pass

Результат:

- Пещеры становятся игровым контентом, а не просто геометрией.

Критерий готовности:

- Есть хотя бы один полноценный cave route с payoff.

### Agent 14 — Base Loop / Return Value

Owner files:

- support/crafting/building/power/inventory owners
- survival path owners

Задача:

- Зафиксировать, зачем игрок возвращается.
- Склеить crafting, storage, power, oxygen, upgrade loop.
- Проверить continuity после save/load.

Не трогать:

- shell/UI
- narrative content
- world cleanup

Результат:

- База и support systems становятся опорой цикла, а не декорацией.

Критерий готовности:

- Есть рабочая петля `explore -> gather -> return -> recover/craft/upgrade -> go deeper`.

## Волна 4

### Agent 15 — Perf / Memory Truth

Owner files:

- perf-sensitive world owners
- profiling routines
- relevant docs/ledgers

Задача:

- Собрать baseline по CPU, GC, VRAM, RT, batches, SetPass.
- Проверить streaming hitch и scatter cost.

Не трогать:

- feature scope
- narrative authoring

Результат:

- У команды появляются реальные цифры, а не ощущения.

Критерий готовности:

- Есть baseline measurements и список red zones.

### Agent 16 — Critical Flow Tests / Build Discipline

Owner files:

- `Assets/_Project/Tests`
- critical path owners для shell/save/pause/core loop
- build issue docs

Задача:

- Поднять минимальный smoke/test слой по critical path.
- Зафиксировать build cadence и issue discipline.

Не трогать:

- world content authoring
- narrative content production

Результат:

- Регрессии начинают ловиться раньше.

Критерий готовности:

- Есть smoke checklist.
- Есть coverage на main menu, pause, save/load и один core progression path.

## Жёсткие правила выдачи

- Не давать двум агентам один и тот же owner file.
- Не совмещать scene integration и content authoring в одном агенте, если можно разделить.
- Не пускать агентов одновременно в `02_HECTON_WORLD.unity`, если задачи не разделены по ownership.
- Сначала закрывать пустоты и integration gaps, потом polishing.
- Любую задачу без live proof считать `PENDING VERIFICATION`.
