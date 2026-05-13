# Hecton8 — Changelog (kratkiy)

## [Tools v1] — Tool System ENTERPRISE v1.0

## CURRENT STATUS OVERRIDE - 2026-05-13 DOC_AUDIT R5

This file is a historical short changelog, not current source authority.

- `SaveData.CurrentVersion` is `68`, not `2`.
- `toolDurabilityMap` and `toolBrokenMap` are plain `Dictionary<string, float>` / `Dictionary<string, bool>` fields in `Assets/_Project/Scripts/SaveData.cs`.
- Save serialization goes through `SaveBinaryPayloadCodec`; the current source scan found no ES3 dictionary wrapper type.
- `ToolDurabilitySystem` exists under `Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs` and mirrors durability into those save dictionaries.
- `ToolHUDPanel.cs` was not found under `Assets/_Project/Scripts` in the R5 path scan; treat the old HUD entry below as historical until a current UI owner is verified.
- Easy Save 3 is forbidden as a current backend. Its physical plugin folder still exists under `Assets/Plugins/Easy Save 3` and is asset contamination, not approved usage.

### ToolHUDPanel.cs v2.0 ENTERPRISE (novyy)
- **Durability bar:** vizualnaya shkala iznosa s tsvetovoy indikatsiey (good/warning/critical/broken)
- **Upgrade slots display:** pokazyvaet ustanovlennye uluchsheniya (do 3 slotov, ikonki)
- **Real-time stats:** efficiency, speed, energy consumption (obnovlyayutsya s uchetom upgrades)
- **Tool name & tier:** nazvanie instrumenta + tier badge (BASIC/ADVANCED/MASTER)
- **Warning indicators:** critical durability warning, broken tool overlay (pulse animation)
- **Smooth animations:** fade in/out pri smene instrumenta, pulse effects dlya kriticheskih sostoyaniy
- **Event integration:** podpiska na ToolDurabilitySystem.OnDurabilityChanged/OnToolBroken/OnToolRepaired
- **Layout:** nizhniy levyy ugol (nad life support panel), 280x160px
- **Zero GC:** pre-allocated string cache (StringBuilder), cached references, struct-based animation state
- **Diagnostics:** _debugPanelVisible, _debugPanelAlpha, _debugCurrentToolID, _debugDurability

### ToolMetadata.cs v1.0 ENTERPRISE (novyy)
- **Identity:** toolID, tier (Basic/Advanced/Master), category (Utility/Construction/Combat/Survival/Science)
- **Durability:** maxDurability, durabilityDrainRate (Primary/Secondary), criticalThreshold
- **Stats:** efficiency, speed, energyConsumptionRate (s uchetom upgrades)
- **Upgrades:** do 3 slotov, installedUpgrades[], compatibility check
- **Repair:** repairCostFull, repairResourceID
- **Localization:** nameLocKey, descriptionLocKey
- **API:** GetTotalEfficiency(), GetTotalSpeed(), GetTotalEnergyConsumption(), InstallUpgrade(), RemoveUpgrade()

### ToolUpgradeData.cs v1.0 ENTERPRISE (novyy)
- **Identity:** upgradeID, nameLocKey, descriptionLocKey, icon
- **Requirements:** requiredTier, compatibleCategories[]
- **Stat Modifiers:** efficiencyBonus, speedBonus, energyConsumptionModifier
- **Special Effects:** durabilityDrainMultiplier, repairCostReduction
- **Crafting:** craftingCost, craftingResourceID
- **API:** IsCompatibleWith(ToolMetadata)

### ToolDurabilitySystem.cs v1.0 ENTERPRISE (novyy)
- **Singleton:** Instance pattern, ISaveable integration
- **Runtime tracking:** Dictionary<toolID, durability>, Dictionary<toolID, broken>
- **Durability drain:** DrainDurability(toolID, amount, maxDurability) — vyzyvaetsya iz PlayerTool
- **Repair system:** RepairTool(), RepairToolFull(), resource-based cost
- **Break system:** BreakTool(), autoBreakOnZero flag
- **Events:** OnDurabilityChanged, OnToolBroken, OnToolRepaired
- **Save/Load:** SavePriority=20, sohranyaet durability i broken maps
- **Zero GC:** pre-allocated dictionaries (capacity 32), cached references

### SaveData current reality - DOC_AUDIT R5
- **Tool persistence:** `toolDurabilityMap` (`Dictionary<string, float>`)
- **Broken tools:** `toolBrokenMap` (`Dictionary<string, bool>`)
- **Version:** `CurrentVersion = 68`
- **Serialization:** `SaveBinaryPayloadCodec`, not ES3

**Kak ispolzovat:**
1. Sozdat ToolMetadata asset: Assets → Create → Hecton8 → Tools → Tool Metadata
2. Naznachit na ItemData instrumenta
3. Sozdat ToolUpgradeData assets dlya moduley uluchsheniya
4. Dobavit ToolDurabilitySystem na stsenu (singleton)
5. PlayerTool avtomaticheski integriruetsya cherez DrainDurability()

---

## [Controls v8] — HUD Extensions ENTERPRISE v4.0

### HectonSuitHUDExtensions.cs v4.0 ENTERPRISE (novyy)
- **FlashlightStatusIndicator:** ikonka fonarya + heat bar + overheat warning
- **PDAStatusIndicator:** ikonka PDA kogda otkryt + active state
- **NotificationSystem:** vsplyvayuschie uvedomleniya (top-center, fade in/out)
  - Overheat, low battery, battery depleted notifications
  - Pre-allocated queue (max 5 entries), zero GC
  - Auto-fade: 0.3s fade-in, full opacity, 0.5s fade-out
- **EquipmentStatusPanel:** top-right panel s ikonkami instrumentov
- **Event integration:** podpiska na FlashlightEvents, PDAEvents
- **Zero GC:** pre-allocated notification queue, cached handlers, struct-based animation
- **Diagnostics:** _debugFlashlightOn, _debugFlashlightHeat, _debugPDAOpen, _debugNotificationCount

### HectonSurvivalSystem v5.0 ENTERPRISE
- **EnergyPercent property:** vozvraschaet energiyu v protsentah (0-100) dlya UI
- **DrainEnergy(int) method:** publichnyy metod dlya rashoda energii (Flashlight, PDA, tools)
- Integratsiya s PlayerFlashlight i PlayerPDA dlya battery drain

**Kak podklyuchit:**
- Dobavit `HectonSuitHUDExtensions` na HUD Camera (ryadom s `HectonSuitHUD`)
- Naznachit `hudCamera`, `hudFont`, `flashlight` v inspektore
- Rabotaet avtomaticheski s suschestvuyuschim HUD v3.0

---

## [Controls v7] — PDA / Flashlight ENTERPRISE v2.0

### PlayerPDA.cs v2.0 ENTERPRISE
- **Sobytiya:** `PDAEvents` — globalnaya shina (OnOpened, OnClosed, OnTabChanged, OnLowBatteryShutdown)
- **Audio:** open/close/tab switch/low battery sounds cherez `SpatialAudioManager`
- **Animatsiya:** `CanvasGroup` fade (plavnyy alpha transition), auto-resolve esli ne naznachen
- **Battery drain:** integratsiya s `HectonSurvivalSystem`, nastraivaemyy `batteryDrainRate`
- **Low battery:** warning sound + avtozakrytie pri kriticheskom urovne
- **Tab history:** stek iz 8 zapisey, `Backspace` = nazad, zero GC (pre-allocated)
- **Diagnostics:** `_debugIsOpen`, `_debugActiveTab`, `_debugOpenDuration`, `_debugBatteryDrainAccum`
- **Null-safety:** graceful degradation, auto-resolve `CanvasGroup` i `SurvivalSystem`

### PlayerFlashlight.cs v2.0 ENTERPRISE
- **Sobytiya:** `FlashlightEvents` — globalnaya shina (OnToggled, OnBatteryDepleted, OnOverheat, OnFlickerStart)
- **Audio:** toggle on/off, low battery, overheat sounds cherez `SpatialAudioManager`
- **Battery drain:** integratsiya s `HectonSurvivalSystem`, nastraivaemyy `batteryDrainRate`
- **Heat buildup:** nakoplenie tepla → flickering → overheat shutdown + cooldown period
- **Flickering:** Perlin noise modulyatsiya intensivnosti pri low battery ILI high heat
- **Screen-space light shafts:** post-process fake driven by registered light sources (no third-party beam dependency)
- **Diagnostics:** `_debugIsOn`, `_debugHeatLevel`, `_debugBatteryDrainAccum`, `_debugIsFlickering`
- **Zero GC:** pre-seeded Random, cached clips, struct math

**Kak podklyuchit:**
- `PlayerPDA`: dobavit na Player root, naznachit Canvas-panel v `pdaPanel`, tabs[] v inspektore
- `PlayerFlashlight`: dobavit na Player root, naznachit `SpotLight` docherniy k kamere v `flashlightLight`

---

### SuitData
- Dobavleno `sprintMultiplier = 1.6` (nastraivaetsya v inspektore)

### HectonPlayerMovement
- `_isSprinting` — chitaet `sprintKey` iz `ControlScheme` (fallback `LeftShift`)
- `WalkPhysics` — `force *= sprintMult` tolko na zemle
- `ClampVelocity` — `maxSpd *= sprintMultiplier` pri sprinte

---

### ControlScheme.cs
- Dobavleno pole `deconstructModifier = KeyCode.R`

### LaserCutter
- Dobavleno `[SerializeField] ControlScheme controlScheme`
- `deconstructModifier` chitaetsya iz `controlScheme` esli naznachen

### PlayerToolManager
- Dobavleno `[SerializeField] ControlScheme controlScheme`
- `ProcessSlotInput()` → `GetSlotKey(i)` — chitaet `toolSlot1-4` iz `controlScheme` ili fallback na `SlotKeys[]`

### ScannerTool, BuilderTool
- Svoih hardkodnyh klavish net — podklyuchenie ne trebuetsya

---

### ControlScheme.cs (novyy)
- `CreateAssetMenu` → `Hecton8/Control Scheme`
- Soderzhit: `interactKey`, vse 5 swim keys, sloty 1–4, `inventoryKey`
- Zadel: `flashlightKey (F)`, `mapKey (M)`, `sprintKey (Shift)` — ne podklyucheny

### HectonPlayerMovement
- Dobavleno pole `[SerializeField] ControlScheme controlScheme`
- `SwimAscendHeld()` / `SwimDescendHeld()` — chitayut iz `controlScheme` esli naznachen, inache fallback na lokalnye polya

### PlayerInteraction
- Dobavleno pole `[SerializeField] ControlScheme controlScheme`
- `ResolvedInteractKey` — svoystvo: `controlScheme?.interactKey ?? interactKey`
- `ActiveInteractKey`, `Tick`, `Awake`, `OnEnable` — ispolzuyut `ResolvedInteractKey`

**Kak ispolzovat:** sozdat asset `ControlScheme_Default`, naznachit v oboih komponentah na prefabe igroka.

---

## [Controls v1] — Swim keys + Interact key refactor

### HectonPlayerMovement
- Vertikal v vode vynesena v 5 `KeyCode`-poley v inspektore
- Defolty: vverh = `Space`, vniz = `LeftCtrl` + `C` + `Q` (tekuschaya HECTON-8 shema)
- `swimAscendAlternate` = `None` po umolchaniyu (ranshe byl `E` — konfliktoval s Interact)
- Helper `KeyHeld(KeyCode)` — propuskaet `KeyCode.None` bez allokatsiy

### PlayerInteraction
- Klavisha vzaimodeystviya vynesena v `[SerializeField] KeyCode interactKey = E`
- Staticheskoe svoystvo `ActiveInteractKey` — obnovlyaetsya v `Awake` i `OnEnable`

### InteractionUI
- `ResolveInteractPrefix()` — v Play Mode beret `ActiveInteractKey`, v Edit Mode — `inputPrefix`
- `ShowPrompt()` teper ispolzuet `ResolveInteractPrefix()` (byl bag: vsegda bral `inputPrefix`)

---

## Shema klavish (tekuschaya)

| Deystvie              | Klavishi                        |
|-----------------------|--------------------------------|
| Hodba / plavanie     | WASD                           |
| Pryzhok (susha)         | Space                          |
| Vverh v vode          | Space (+ dop. v ControlScheme) |
| Vniz v vode           | LeftCtrl, C, Q                 |
| Vzaimodeystvie        | E (`interactKey`)              |
| Instrumenty           | 1–4                            |
| Osnovnoy / alt.      | LKM / PKM                      |
| Inventar             | Tab                            |
| Razbor lazerom        | Uderzhivat R + LKM             |
