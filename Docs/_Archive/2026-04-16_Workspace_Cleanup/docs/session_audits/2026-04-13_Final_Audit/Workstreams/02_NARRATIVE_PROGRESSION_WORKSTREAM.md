Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# HECTON-8 — Narrative / Progression Workstream

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Что закрывает этот фронт

- Quest content
- Audio logs
- Suit upgrades
- Narrative discovery
- First-hour progression
- Live lore system integration

## Почему это критично

Сейчас здесь главный разрыв между "много кода" и "есть игра".  
Code owners существуют. Production content по ключевым data roots почти пустой.

## Owner files

- `Assets/_Project/Scripts/Bootstrap/HectonLoreSystemsRoot.cs`
- `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
- `Assets/_Project/Scripts/NarrativeDiscovery.cs`
- `Assets/_Project/Scripts/NarrativeEvents.cs`
- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestData.cs`
- `Assets/_Project/Scripts/Quest/QuestEvents.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogData.cs`
- `Assets/_Project/Scripts/AudioLog/AudioLogPickup.cs`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeData.cs`
- `Assets/_Project/Scripts/Gameplay/SuitHUDProfile.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs`
- `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs`
- `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Gameplay/EndingSystem.cs`

## Data roots

- `Assets/_Project/Data/Lore/Registries`
- `Assets/_Project/Data/Lore/DepthZones`
- `Assets/_Project/Data/Lore/Quests`
- `Assets/_Project/Data/Lore/AudioLogs`
- `Assets/_Project/Data/Lore/SuitUpgrades`

Факт:

- `Quests` пусто.
- `AudioLogs` пусто.
- `SuitUpgrades` пусто.

## Основные задачи

### Front A. Narrative data authoring

- Заполнить discovery IDs и narrative links.
- Привязать registries к реальным depth beats.
- Зафиксировать story spine первого часа.

### Front B. Quest system fill-in

- Создать реальные quest assets.
- Определить trigger sources.
- Проверить активацию квестов от существующих world/narrative events.

### Front C. Audio log fill-in

- Создать audio log assets.
- Привязать pickup flow.
- Проверить discovery и PDA display.

### Front D. Suit progression

- Создать assets улучшений.
- Привязать unlock conditions.
- Проверить визуальную подачу через HUD.

### Front E. Scene/bootstrap integration

- Проверить живое наличие `LoreSystems` в `02_HECTON_WORLD`.
- Гарантировать, что корневой lore owner реально поднимается в production path.
- Проверить, что системы не существуют только на бумаге.

## Do-Not-Touch Scope

- Не лезть в menu/pause UI.
- Не трогать save/load shell.
- Не переписывать world streaming/scatter.
- Не смешивать content authoring с performance work.

## Как дробить по агентам

Агент 1:
- `HectonNarrativeDirector.cs`
- `NarrativeDiscovery.cs`
- `NarrativeEvents.cs`
- `Registries`, `DepthZones`
- Задача: narrative spine и discovery layer.

Агент 2:
- `QuestManager.cs`
- `QuestData.cs`
- `QuestEvents.cs`
- `Data/Lore/Quests`
- Задача: quest content и activation.

Агент 3:
- `AudioLogSystem.cs`
- `AudioLogData.cs`
- `AudioLogPickup.cs`
- `PDADataLogTab.cs`
- `Data/Lore/AudioLogs`
- Задача: audio logs и PDA flow.

Агент 4:
- `SuitUpgradeManager.cs`
- `SuitUpgradeData.cs`
- `SuitHUDProfile.cs`
- `SuitHUDPresentationController.cs`
- `Data/Lore/SuitUpgrades`
- Задача: suit progression.

Агент 5:
- `HectonLoreSystemsRoot.cs`
- scene wiring / validation tooling
- Задача: live bootstrap integration.

## Expected Result

- Narrative/progression перестаёт быть пустым каркасом.
- Первый час игры получает реальный content spine.
- Quest/log/upgrade блоки существуют не только в коде.

## Exit Criteria

- Data roots больше не пустые.
- В production world path реально живут lore systems.
- Игрок может пройти хотя бы один связный narrative/progression маршрут.
