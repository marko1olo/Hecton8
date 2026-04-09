# HECTON-8 — Lore Systems Integration Guide

## Что реализовано

### 1. AudioLog System (`Assets/_Project/Scripts/AudioLog/`)
**Аудиодневники колонии.**

- `AudioLogData` — ScriptableObject: данные одного дневника (клип, субтитры, автор, категория)
- `AudioLogSystem` — singleton: воспроизведение, архив, ISaveable
- `AudioLogPickup` — IInteractable: объект в мире, воспроизводит лог при взаимодействии
- `AudioLogEvents` — static event bus

**Как использовать:**
1. Create → Hecton8/Narrative/Audio Log Data → заполнить поля
2. Добавить `AudioLogPickup` на GameObject в сцене
3. Назначить `AudioLogData` в инспекторе
4. `AudioLogSystem` создаётся через `HectonLoreSystemsRoot`

---

### 2. Quest System (`Assets/_Project/Scripts/Quest/`)
**Нарративные квесты через события мира.**

- `QuestData` — ScriptableObject: квест с триггером и условием завершения
- `QuestManager` — singleton: слушает события, ISaveable
- `QuestEvents` — static event bus

**Типы триггеров:** OnItemCollected, OnDepthReached, OnBiomeEntered, OnDiscoveryMade, OnAudioLogFound, OnEclipseStart, OnSignalDetected, Manual

**Как использовать:**
1. Create → Hecton8/Quest/Quest Data → заполнить triggerType, triggerId
2. Добавить `QuestData` в массив `QuestManager.allQuests`
3. Квест активируется автоматически при событии

---

### 3. Atlas Signal System (`Assets/_Project/Scripts/AtlasSignal/`)
**Пульс сигнала Атлас-6 (ритм 11:23).**

- `AtlasSignalSystem` — singleton: пульс каждые 683с, сила по расстоянию
- `AtlasSignalDecoder` — 4-фазная расшифровка по силе сигнала
- `Atlas6DirectiveSystem` — статус игрока с точки зрения Атлас-6
- `AtlasSignalEvents` — static event bus
- `Atlas6Events` — static event bus

**Shader globals:** `_AtlasSignalStrength`, `_BiolumPulseTime`

**Как использовать:**
1. `AtlasSignalSystem` — назначить `atlasCorePosWorld` (позиция ядра на -5000м)
2. `AtlasSignalDecoder` — автоматически расшифровывает при приближении
3. `Atlas6DirectiveSystem` — отслеживает статус игрока

---

### 4. Suit Upgrade System (`Assets/_Project/Scripts/Gameplay/`)
**Апгрейды скафандра Tier 0-4.**

- `SuitUpgradeData` — ScriptableObject: дельты параметров, требования
- `SuitUpgradeManager` — singleton: применяет апгрейды, ISaveable
- `RuntimeSurvivalStats` — mutable wrapper над SurvivalStats

**Тиры из лора:**
- Tier 0: до -150м, O2 4 мин (стартовый)
- Tier 1: до -500м, O2 8 мин (первый крафт)
- Tier 2: до -1500м, O2 15 мин
- Tier 3: до -3500м, O2 25 мин
- Tier 4: до -5000м, O2 45 мин

**Как использовать:**
1. Create → Hecton8/Gameplay/Suit Upgrade Data → заполнить tier, deltaSafeDepth, deltaMaxOxygen
2. Добавить в массив `SuitUpgradeManager.allUpgrades`
3. `SuitUpgradeManager.InstallUpgrade(data)` — установить апгрейд

---

### 5. Depth Zone System (`Assets/_Project/Scripts/World/`)
**Вертикальная стратификация мира.**

- `DepthZoneProfile` — ScriptableObject: зона с глубиной, атмосферой, требованиями
- `DepthZoneDirector` — singleton: отслеживает зону игрока, hull warnings
- `DepthZoneEvents` — static event bus

**Зоны из лора:**
- THE SPINE: 0-100м (стартовая)
- THE DROWNED FACTORIES: 100-1500м
- THE DROP: 1000-5000м
- Подзоны: 0-150м, 150-500м, 500-1000м, 1000-1200м, 1200-2500м, 2500-4000м, 4000-5000м

**Как использовать:**
1. Create → Hecton8/World/Depth Zone Profile → заполнить minDepth, maxDepth, requiredHullTier
2. Добавить в массив `DepthZoneDirector.zones`

---

### 6. Eclipse Gameplay System (`Assets/_Project/Scripts/Gameplay/`)
**Геймплейные последствия Великого Затмения.**

- `EclipseGameplaySystem` — singleton: температура -8°C/мин, ночные хищники через 60с
- `EclipseGameplayEvents` — static event bus

**Shader globals:** `_EclipseBiolumMultiplier`

**Автоматически:** слушает `HectonCelestialEngine.OnEclipseStart/End`

---

### 7. Spectrum System (`Assets/_Project/Scripts/Visor/`)
**Режимы визора Hecton-OS.**

- `SpectrumSystem` — singleton: Normal/Thermal/Sonar/Echolocation
- `PDASpectrumTab` — UI вкладка в PDA (индекс 5)
- `SpectrumEvents` — static event bus

**Shader globals:** `_SpectrumMode`, `_SonarRadius`, `_SonarPulseTime`

**Как использовать:**
- `SpectrumSystem.Instance.SetMode(SpectrumMode.Thermal)` — переключить режим
- `SpectrumSystem.Instance.CycleMode()` — циклическое переключение

---

### 8. Biolum Controller (`Assets/_Project/Scripts/World/`)
**Глобальная биолюминесценция.**

- `HectonBiolumController` — singleton: реагирует на глубину, затмение, сигнал Атлас-6

**Shader globals:** `_BiolumIntensity`, `_BiolumPulseTime`

---

### 9. Narrative Systems (`Assets/_Project/Scripts/Narrative/`)
**Лорные данные.**

- `ColonistLoreRegistry` — SO: все лорные объекты колонии (Chen_M, капитан, биолог...)
- `FaunaLoreRegistry` — SO: все существа (11 типов из лора)
- `DeepReachCorporationData` — SO: корпорация, фракции, изотопы, приказы
- `CorporateOrderSystem` — singleton: противоречивые приказы с задержкой 8-12ч

**Как использовать:**
1. Create → Hecton8/Narrative/Colonist Lore Registry → уже предзаполнен
2. Create → Hecton8/Narrative/Fauna Lore Registry → уже предзаполнен
3. Create → Hecton8/Narrative/Deep Reach Corporation Data → уже предзаполнен
4. `CorporateOrderSystem` — назначить `corporationData` в инспекторе

---

### 10. Random Event System (`Assets/_Project/Scripts/Gameplay/`)
**Случайные события мира.**

- `RandomEventSystem` — singleton: 5 типов событий с условиями по глубине

**События:** BiolumStorm (>1000м), ThermalEruption (>3000м), FaunaMigration (любая), HectonOSGlitch (>500м), CaveCollapse (>200м)

**Shader globals:** `_BiolumStormActive`, `_HUDGlitchActive`

---

### 11. First Hour Director (`Assets/_Project/Scripts/Gameplay/`)
**Режиссура первого часа.**

- `FirstHourDirector` — singleton: 6 milestone, ISaveable

**Milestone:** Orientation (5мин), FirstAnxiety (15мин), FirstCraft (25-40мин), TheShadow (40мин), FirstModule (70мин), HumCloser (90мин)

---

### 12. Soundscape System (`Assets/_Project/Scripts/World/`)
**Звуковые тиры по глубине.**

- `SoundscapeSystem` — singleton: 7 тиров (Surface→Thermal)
- `SoundscapeEvents` — static event bus

**Shader globals:** `_SoundscapeDepthTier`

**Как использовать:** Подписаться на `SoundscapeEvents.OnTierChanged` в AudioManager

---

### 13. Ending System (`Assets/_Project/Scripts/Gameplay/`)
**Три концовки игры.**

- `EndingSystem` — singleton: условия активации, выбор концовки, ISaveable
- `EndingTerminalInteractable` — IInteractable: терминал у ядра Атлас-6
- `EndingEvents` — static event bus

**Концовки:** ShutDown (выключить), Leave (оставить), Amplify (усилить сигнал)

**Как использовать:**
1. Разместить `EndingTerminalInteractable` у ядра Атлас-6 на -5000м
2. `EndingSystem.Instance.ChooseEnding(EndingChoice.Amplify)` — из UI

---

### 14. PDA Data Log Tab (`Assets/_Project/Scripts/UI/`)
**Архив аудиодневников в PDA.**

- `PDADataLogTab` — вкладка 4 в PDA
- Автоматически добавляется через `PlayerPDA.AutoResolveTabs`

**Как использовать:**
1. Назначить `AudioLogData[]` в `PDADataLogTab.allLogs`
2. Вкладка отображает обнаруженные записи, позволяет переслушать

---

## Как добавить в сцену

### Шаг 1: Создать LoreSystems GameObject
```
Hierarchy → Create Empty → назвать "LoreSystems"
Добавить компонент: HectonLoreSystemsRoot
Нажать [Setup All Systems] в инспекторе
```

### Шаг 2: Создать ScriptableObject ассеты
```
Assets/_Project/Data/Lore/ → создать папку
Создать: ColonistLoreRegistry, FaunaLoreRegistry, DeepReachCorporationData
Создать: DepthZoneProfile × 7 (по зонам из лора)
Создать: SuitUpgradeData × 5 (Tier 0-4)
Создать: QuestData × N (квесты из лора)
Создать: AudioLogData × N (дневники колонии)
```

### Шаг 3: Назначить ссылки
```
AtlasSignalSystem → atlasCorePosWorld = (0, -5000, 0)
DepthZoneDirector → zones[] = все DepthZoneProfile
SuitUpgradeManager → baseStats, allUpgrades[]
QuestManager → allQuests[]
CorporateOrderSystem → corporationData
PDADataLogTab → allLogs[]
```

### Шаг 4: Разместить объекты в мире
```
AudioLogPickup × N → в модулях колонии
EndingTerminalInteractable → у ядра Атлас-6 (-5000м)
NarrativeDiscovery × N → лорные объекты (КПК, схемы, скафандры)
```

---

## SaveData версия: 16
Все системы сохраняют состояние через ISaveable.
