# Hecton8 — Changelog (краткий)

## [Tools v1] — Tool System ENTERPRISE v1.0

### ToolHUDPanel.cs v2.0 ENTERPRISE (новый)
- **Durability bar:** визуальная шкала износа с цветовой индикацией (good/warning/critical/broken)
- **Upgrade slots display:** показывает установленные улучшения (до 3 слотов, иконки)
- **Real-time stats:** efficiency, speed, energy consumption (обновляются с учётом upgrades)
- **Tool name & tier:** название инструмента + tier badge (BASIC/ADVANCED/MASTER)
- **Warning indicators:** critical durability warning, broken tool overlay (pulse animation)
- **Smooth animations:** fade in/out при смене инструмента, pulse effects для критических состояний
- **Event integration:** подписка на ToolDurabilitySystem.OnDurabilityChanged/OnToolBroken/OnToolRepaired
- **Layout:** нижний левый угол (над life support panel), 280x160px
- **Zero GC:** pre-allocated string cache (StringBuilder), cached references, struct-based animation state
- **Diagnostics:** _debugPanelVisible, _debugPanelAlpha, _debugCurrentToolID, _debugDurability

### ToolMetadata.cs v1.0 ENTERPRISE (новый)
- **Identity:** toolID, tier (Basic/Advanced/Master), category (Utility/Construction/Combat/Survival/Science)
- **Durability:** maxDurability, durabilityDrainRate (Primary/Secondary), criticalThreshold
- **Stats:** efficiency, speed, energyConsumptionRate (с учётом upgrades)
- **Upgrades:** до 3 слотов, installedUpgrades[], compatibility check
- **Repair:** repairCostFull, repairResourceID
- **Localization:** nameLocKey, descriptionLocKey
- **API:** GetTotalEfficiency(), GetTotalSpeed(), GetTotalEnergyConsumption(), InstallUpgrade(), RemoveUpgrade()

### ToolUpgradeData.cs v1.0 ENTERPRISE (новый)
- **Identity:** upgradeID, nameLocKey, descriptionLocKey, icon
- **Requirements:** requiredTier, compatibleCategories[]
- **Stat Modifiers:** efficiencyBonus, speedBonus, energyConsumptionModifier
- **Special Effects:** durabilityDrainMultiplier, repairCostReduction
- **Crafting:** craftingCost, craftingResourceID
- **API:** IsCompatibleWith(ToolMetadata)

### ToolDurabilitySystem.cs v1.0 ENTERPRISE (новый)
- **Singleton:** Instance pattern, ISaveable integration
- **Runtime tracking:** Dictionary<toolID, durability>, Dictionary<toolID, broken>
- **Durability drain:** DrainDurability(toolID, amount, maxDurability) — вызывается из PlayerTool
- **Repair system:** RepairTool(), RepairToolFull(), resource-based cost
- **Break system:** BreakTool(), autoBreakOnZero flag
- **Events:** OnDurabilityChanged, OnToolBroken, OnToolRepaired
- **Save/Load:** SavePriority=20, сохраняет durability и broken maps
- **Zero GC:** pre-allocated dictionaries (capacity 32), cached references

### SaveData v2.0 ENTERPRISE
- **Tool persistence:** toolDurabilityMap (ES3SerializableDictionary<string, float>)
- **Broken tools:** toolBrokenMap (ES3SerializableDictionary<string, bool>)
- **Version:** CurrentVersion = 2 (инкрементирован для миграции)

**Как использовать:**
1. Создать ToolMetadata asset: Assets → Create → Hecton8 → Tools → Tool Metadata
2. Назначить на ItemData инструмента
3. Создать ToolUpgradeData assets для модулей улучшения
4. Добавить ToolDurabilitySystem на сцену (singleton)
5. PlayerTool автоматически интегрируется через DrainDurability()

---

## [Controls v8] — HUD Extensions ENTERPRISE v4.0

### HectonSuitHUDExtensions.cs v4.0 ENTERPRISE (новый)
- **FlashlightStatusIndicator:** иконка фонаря + heat bar + overheat warning
- **PDAStatusIndicator:** иконка PDA когда открыт + active state
- **NotificationSystem:** всплывающие уведомления (top-center, fade in/out)
  - Overheat, low battery, battery depleted notifications
  - Pre-allocated queue (max 5 entries), zero GC
  - Auto-fade: 0.3s fade-in, full opacity, 0.5s fade-out
- **EquipmentStatusPanel:** top-right панель с иконками инструментов
- **Event integration:** подписка на FlashlightEvents, PDAEvents
- **Zero GC:** pre-allocated notification queue, cached handlers, struct-based animation
- **Diagnostics:** _debugFlashlightOn, _debugFlashlightHeat, _debugPDAOpen, _debugNotificationCount

### HectonSurvivalSystem v5.0 ENTERPRISE
- **EnergyPercent property:** возвращает энергию в процентах (0-100) для UI
- **DrainEnergy(int) method:** публичный метод для расхода энергии (Flashlight, PDA, tools)
- Интеграция с PlayerFlashlight и PlayerPDA для battery drain

**Как подключить:**
- Добавить `HectonSuitHUDExtensions` на HUD Camera (рядом с `HectonSuitHUD`)
- Назначить `hudCamera`, `hudFont`, `flashlight` в инспекторе
- Работает автоматически с существующим HUD v3.0

---

## [Controls v7] — PDA / Flashlight ENTERPRISE v2.0

### PlayerPDA.cs v2.0 ENTERPRISE
- **События:** `PDAEvents` — глобальная шина (OnOpened, OnClosed, OnTabChanged, OnLowBatteryShutdown)
- **Аудио:** open/close/tab switch/low battery sounds через `SpatialAudioManager`
- **Анимация:** `CanvasGroup` fade (плавный alpha transition), auto-resolve если не назначен
- **Battery drain:** интеграция с `HectonSurvivalSystem`, настраиваемый `batteryDrainRate`
- **Low battery:** warning sound + автозакрытие при критическом уровне
- **Tab history:** стек из 8 записей, `Backspace` = назад, zero GC (pre-allocated)
- **Diagnostics:** `_debugIsOpen`, `_debugActiveTab`, `_debugOpenDuration`, `_debugBatteryDrainAccum`
- **Null-safety:** graceful degradation, auto-resolve `CanvasGroup` и `SurvivalSystem`

### PlayerFlashlight.cs v2.0 ENTERPRISE
- **События:** `FlashlightEvents` — глобальная шина (OnToggled, OnBatteryDepleted, OnOverheat, OnFlickerStart)
- **Аудио:** toggle on/off, low battery, overheat sounds через `SpatialAudioManager`
- **Battery drain:** интеграция с `HectonSurvivalSystem`, настраиваемый `batteryDrainRate`
- **Heat buildup:** накопление тепла → flickering → overheat shutdown + cooldown period
- **Flickering:** Perlin noise модуляция интенсивности при low battery ИЛИ high heat
- **VolumetricLightBeam:** опциональная интеграция через reflection (no hard dependency)
- **Diagnostics:** `_debugIsOn`, `_debugHeatLevel`, `_debugBatteryDrainAccum`, `_debugIsFlickering`
- **Zero GC:** pre-seeded Random, cached clips, struct math

**Как подключить:**
- `PlayerPDA`: добавить на Player root, назначить Canvas-панель в `pdaPanel`, tabs[] в инспекторе
- `PlayerFlashlight`: добавить на Player root, назначить `SpotLight` дочерний к камере в `flashlightLight`

---

### SuitData
- Добавлено `sprintMultiplier = 1.6` (настраивается в инспекторе)

### HectonPlayerMovement
- `_isSprinting` — читает `sprintKey` из `ControlScheme` (fallback `LeftShift`)
- `WalkPhysics` — `force *= sprintMult` только на земле
- `ClampVelocity` — `maxSpd *= sprintMultiplier` при спринте

---

### ControlScheme.cs
- Добавлено поле `deconstructModifier = KeyCode.R`

### LaserCutter
- Добавлено `[SerializeField] ControlScheme controlScheme`
- `deconstructModifier` читается из `controlScheme` если назначен

### PlayerToolManager
- Добавлено `[SerializeField] ControlScheme controlScheme`
- `ProcessSlotInput()` → `GetSlotKey(i)` — читает `toolSlot1-4` из `controlScheme` или fallback на `SlotKeys[]`

### ScannerTool, BuilderTool
- Своих хардкодных клавиш нет — подключение не требуется

---

### ControlScheme.cs (новый)
- `CreateAssetMenu` → `Hecton8/Control Scheme`
- Содержит: `interactKey`, все 5 swim keys, слоты 1–4, `inventoryKey`
- Задел: `flashlightKey (F)`, `mapKey (M)`, `sprintKey (Shift)` — не подключены

### HectonPlayerMovement
- Добавлено поле `[SerializeField] ControlScheme controlScheme`
- `SwimAscendHeld()` / `SwimDescendHeld()` — читают из `controlScheme` если назначен, иначе fallback на локальные поля

### PlayerInteraction
- Добавлено поле `[SerializeField] ControlScheme controlScheme`
- `ResolvedInteractKey` — свойство: `controlScheme?.interactKey ?? interactKey`
- `ActiveInteractKey`, `Tick`, `Awake`, `OnEnable` — используют `ResolvedInteractKey`

**Как использовать:** создать asset `ControlScheme_Default`, назначить в обоих компонентах на префабе игрока.

---

## [Controls v1] — Swim keys + Interact key refactor

### HectonPlayerMovement
- Вертикаль в воде вынесена в 5 `KeyCode`-полей в инспекторе
- Дефолты: вверх = `Space`, вниз = `LeftCtrl` + `C` + `Q` (текущая HECTON-8 схема)
- `swimAscendAlternate` = `None` по умолчанию (раньше был `E` — конфликтовал с Interact)
- Хелпер `KeyHeld(KeyCode)` — пропускает `KeyCode.None` без аллокаций

### PlayerInteraction
- Клавиша взаимодействия вынесена в `[SerializeField] KeyCode interactKey = E`
- Статическое свойство `ActiveInteractKey` — обновляется в `Awake` и `OnEnable`

### InteractionUI
- `ResolveInteractPrefix()` — в Play Mode берёт `ActiveInteractKey`, в Edit Mode — `inputPrefix`
- `ShowPrompt()` теперь использует `ResolveInteractPrefix()` (был баг: всегда брал `inputPrefix`)

---

## Схема клавиш (текущая)

| Действие              | Клавиши                        |
|-----------------------|--------------------------------|
| Ходьба / плавание     | WASD                           |
| Прыжок (суша)         | Space                          |
| Вверх в воде          | Space (+ доп. в ControlScheme) |
| Вниз в воде           | LeftCtrl, C, Q                 |
| Взаимодействие        | E (`interactKey`)              |
| Инструменты           | 1–4                            |
| Основной / альт.      | ЛКМ / ПКМ                      |
| Инвентарь             | Tab                            |
| Разбор лазером        | Удерживать R + ЛКМ             |
