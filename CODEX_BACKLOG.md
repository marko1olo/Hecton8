# Codex Backlog

## 2026-03-27 - Pause menu migration, font cleanup, and scene audio bootstrap

- PDA no longer carries `Controls` as a live tab in the active user flow.
  - Changed in:
    - `Assets/_Project/Scripts/PlayerPDA.cs`
    - `Assets/_Project/Scripts/PDAInventoryTab.cs`
    - `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
    - `Assets/_Project/Scripts/UI/PDAShellChrome.cs`
  - What changed:
    - active PDA contract is now:
      - `0 = Inventory`
      - `1 = Loadout`
      - `2 = Data Log`
    - `Tab_Controls` is no longer part of `PlayerPDA.tabs[]`
    - top tab labels were reduced accordingly
  - Why:
    - user requested moving controls/rebinding out of PDA into the standard `Esc` settings flow
  - Result:
    - PDA surface is simpler and closer to a real in-game field device instead of a settings dump

- Added a real `Esc` pause/settings shell.
  - New files:
    - `Assets/_Project/Scripts/UI/PauseMenuController.cs`
    - `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`
    - `Assets/_Project/Scripts/UI/PauseMenuHost.cs`
  - Scene wiring:
    - `PauseMenuHost` attached to `--- UI ---/Suit_HUD_Canvas`
    - host creates `PauseMenu_Root` at runtime under the existing HUD canvas
  - What it provides:
    - `Resume Expedition`
    - `Save Station`
    - `Field Guide`
    - `Settings`
    - `Exit To Main Menu`
    - `Quit Application`
    - runtime rebinding UI lives inside `Settings`, not inside PDA
  - Important implementation detail:
    - pause menu root stays active and uses `CanvasGroup` for visibility, so `ITickable` registration is not lost after closing

- Fixed PDA/UI audio null-spam without faking a backend.
  - Changed in:
    - `Assets/_Project/Scripts/SpatialAudioManager.cs`
    - `Assets/_Project/Scripts/PlayerPDA.cs`
  - What changed:
    - added `SpatialAudioManager.TryGetInstance(out ...)`
    - `PlayerPDA.PlaySound(...)` now probes silently instead of touching the noisy `Instance` getter
  - Why:
    - user hit repeated `[SpatialAudioManager] Instance is null` errors when opening/closing PDA
  - Result:
    - PDA sound calls no longer spam the console when the manager is absent

- Added a real `SpatialAudioManager` scene bootstrap.
  - Scene change:
    - created root scene object `SpatialAudioManager_Root`
    - attached `Hecton8.Audio.SpatialAudioManager`
    - saved `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - Why:
    - optional silent probing is good, but the correct production fix is to actually have the scene audio manager present
  - Result:
    - PDA / pause-menu UI audio path now has a real manager available
  - Bad attempt noted:
    - first manager instance was created as a child under `--- UI ---`
    - this triggered `DontDestroyOnLoad only works for root GameObjects`
    - rolled forward by deleting that child instance and creating a root object instead

- Removed pause/settings TMP glyph warnings caused by numeric-only font assignment.
  - Changed in:
    - `Assets/_Project/Scripts/UI/PauseMenuController.cs`
    - `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`
  - What changed:
    - both scripts now sanitize assigned fonts through a readable-font resolver
    - numeric-only fonts like `цифры SDF` are rejected for text labels/binding text
  - Why:
    - runtime warnings were emitted for Cyrillic/letter glyphs missing from the numeric font
  - Result:
    - glyph warnings from pause controls panel disappeared in play-mode smoke checks

- Updated runtime input asset to support closing PDA from `Tab` while UI map is active.
  - Changed in:
    - `Assets/Resources/HectonRuntimeInputActions.inputactions`
  - What changed:
    - UI `Cancel` now includes `<Keyboard>/tab` in addition to escape/right mouse/gamepad cancel paths
  - Why:
    - user explicitly reported that inventory/PDA was not closing via `Tab`
  - Result:
    - input asset side is now aligned with the intended `Tab` close behavior
  - Validation status:
    - asset change is present
    - not yet manually verified by user in real input session

- Console state after this pass:
  - Confirmed gone:
    - `[SpatialAudioManager] Instance is null`
    - TMP glyph warnings from `Binding_Interact`, `Binding_Flashlight`, etc.
    - `DontDestroyOnLoad only works for root GameObjects`
  - New residual noise seen in play mode:
    - `Resource ID out of range in SetResource: ...`
  - Current status of that noise:
    - source not yet tied to PDA/audio changes
    - treat as separate rendering/runtime issue until proven otherwise

- Honest validation done:
  - compile clean after the pause/audio/font pass
  - post-play console rechecked
  - runtime `PauseMenu_Root` was confirmed to exist
  - scene was re-saved after audio-manager bootstrap
  - direct manual `Esc` interaction was not simulated through MCP; input-side correctness is inferred from code + input asset, not yet manually asserted

## 2026-03-27 - First runtime smoke-pass after tool provisioning

- Ran a real play-mode smoke-pass through Unity MCP after:
  - `ToolLoadoutProvisioner`
  - `ToolStagingSpawner`
  - world-prefab binding
  - flashlight runtime binding
- Verified live `Player` state in play mode:
  - `ToolLoadoutProvisioner` resolved its refs and default assets correctly
  - `PlayerInventory` was populated at runtime
  - live inventory snapshot:
    - `OccupiedCells = 30`
    - `FreeCells = 18`
    - `Weight = 21.8`
  - `PlayerToolManager` retained the intended core quick-slot assignments:
    - Scanner
    - Repair
    - Builder
    - Laser Cutter
  - `PlayerFlashlight` resolved `DiveLamp_Light` and `HectonSurvivalSystem`
  - no new gameplay/runtime errors were emitted during the smoke-pass
- Residual console noise during inspection was only MCP serializer `TransformHandle` warnings.
  - These are tooling-side and were already known, not gameplay regressions.
- Important limitation of this smoke-pass:
  - it confirms provisioning and live component wiring
  - it does not yet confirm per-tool interaction behavior under manual input

## 2026-03-27 - Tool provisioning API and runtime loadout bootstrap

- Added official provisioning entrypoint in `Assets/_Project/Scripts/PlayerInventory.cs`:
  - `TryAddItem(ItemData item, int quantity = 1)`
  - Why: tool/inventory integration can now seed items through one safe public API instead of faking world pickups or duplicating placement logic.
  - Result: inventory provisioning, debug seeding, and future fabricator rewards can all go through the same stacking/weight/event path.
- Refactored `HandleItemCollected(...)` in `PlayerInventory` to use that new API.
  - Why: one source of truth for placement/stacking/full-inventory handling.
- Added assignment-change event and public slot assignment API in `Assets/_Project/Scripts/PlayerToolManager.cs`:
  - `event Action ToolAssignmentsChanged`
  - `SetAssignedToolPrefab(int slotIndex, GameObject prefab, bool holsterIfCurrentInvalid = true)`
  - Why: quickbar/PDA/tool provisioning can now react to live slot remaps without inspector-only workflows.
- Updated UI listeners:
  - `Assets/_Project/Scripts/HUDQuickBar.cs`
  - `Assets/_Project/Scripts/PDAInventoryTab.cs`
  - Both now refresh not only on active-slot changes but also on loadout assignment changes.
- Added `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`.
  - What: dev/runtime helper that can:
    - provision the full 12-tool kit into `PlayerInventory`
    - assign the default core 4-slot loadout into `PlayerToolManager`
  - Why: removes manual setup debt from every test pass and gives a deterministic bootstrap path for the full tool system.
  - Important:
    - it auto-resolves scene refs
    - in editor it auto-resolves the default tool assets/prefabs from known project paths
    - it is safe/dev-oriented and does not replace the real gameplay acquisition loop
- Added `ToolLoadoutProvisioner` to `Player` in `02_HECTON_WORLD` and enabled:
  - `provisionInventoryOnStart = true`
  - `assignCoreLoadoutOnStart = true`
  - `holsterBeforeAssigning = true`
- Result of this pass:
  - next play session should bootstrap a full tool inventory plus stable core quick slots automatically
  - HUD/PDA quick-slot UI now has the event surface needed to stay in sync with runtime loadout changes

## 2026-03-27 - Tool world integration and flashlight runtime binding

- Completed world-item loop for the full 12-tool set.
  - Created world pickup prefabs under:
    - `Assets/_Project/Prefabs/Items/Tools`
  - Bound every tool `ItemData.worldPrefab` under:
    - `Assets/_Project/Data/Items/Tools`
  - Result: inventory `DROP` / world pickup path is no longer blocked by null `worldPrefab` on tool items.
- Completed runtime flashlight scene binding without requiring manual inspector setup.
  - Added `DiveLamp_Light` under `--- GAMEPLAY ---/Player/Main Camera`
  - Added `PlayerFlashlight` to `--- GAMEPLAY ---/Player`
  - Extended `Assets/_Project/Scripts/PlayerFlashlight.cs` to auto-resolve:
    - `flashlightLight`
    - `survivalSystem`
  - Also added editor/runtime light normalization so the dive lamp can self-configure:
    - local position
    - spot settings
    - enabled/intensity preview state
  - Result: `FlashlightTool` is now backed by a real runtime flashlight path instead of a dead adapter.
- Verified after Unity refresh:
  - compile clean
  - console clean for these changes
  - live `PlayerFlashlight` resolves both `flashlightLight` and `survivalSystem`
  - live `DiveLamp_Light` exists under the main camera and is driven by the flashlight system
- Remaining integration gap at this point:
  - no play-mode validation yet
  - `PlayerToolManager` still intentionally holds only the original 4 core slots
  - advanced tools exist as data + held prefabs + world prefabs, but are not all mounted into quick slots simultaneously

## 2026-03-27 - Tool staging rack in scene

- Added `Assets/_Project/Scripts/ToolStagingSpawner.cs`.
  - What: editor-side authoring helper that rebuilds a clean tool rack from all world tool prefabs.
  - Why: gives a deterministic scene-level validation surface for the full 12-tool set without touching the player's active 4-slot loadout.
  - How:
    - has a static list of all `Assets/_Project/Prefabs/Items/Tools/Item_Tool_*_World.prefab`
    - instantiates them in a simple grid under one parent
    - exposes a menu item: `Hecton8/Dev/Rebuild Tool Staging`
- Added `ToolStagingSpawner` to `--- WORLD ---/Tool_Staging` in `02_HECTON_WORLD`.
- Rebuilt the rack through the editor menu and saved the scene.
- Result:
  - `--- WORLD ---/Tool_Staging` now contains all 12 world-tool pickup objects
  - the staging rack is isolated from the player tool slots and safe to keep in-scene for future debugging

## 2026-03-27 - Remaining tool rollout

- Added shared gameplay helper:
  - `Assets/_Project/Scripts/ToolHitUtility.cs`
  - centralizes common hit logic for new tools:
    - `ICuttable`
    - `HectonBaseAI`
    - `HectonSurvivalSystem`
    - world item pickup via `HectonItem`
- Added first-pass runtime scripts for the remaining non-core tools:
  - `KnifeTool.cs`
  - `SalvageSamplerTool.cs`
  - `PropulsionTool.cs`
  - `BeaconDeployerTool.cs`
  - `EnvironmentalAnalyzerTool.cs`
  - `StunPistolTool.cs`
  - `HarpoonLauncherTool.cs`
- All seven new scripts compile cleanly in Unity after namespace cleanup:
  - explicit `UnityEngine.Physics` was required because project namespaces shadowed `Physics`
  - AI references required `Hecton8.AI`
  - `ResourceNode` references required `Hecton8.Scavenging`
- Behavior level of this pass:
  - `KnifeTool` = spherecast melee
  - `SalvageSamplerTool` = short-range sampling damage, secondary collect on `HectonItem`
  - `PropulsionTool` = push/pull force on rigidbodies under a mass cap
  - `BeaconDeployerTool` = deploy runtime beacon markers; pooled prefab path supported if later assigned
  - `EnvironmentalAnalyzerTool` = target/suit diagnostics via `HUDNotification` or fallback `Debug.Log`
  - `StunPistolTool` = damage/impulse plus temporary disable of `HectonBaseAI`
  - `HarpoonLauncherTool` = ranged damage and secondary reel impulse
- Created placeholder materials for those seven tools under:
  - `Assets/_Project/Art/Materials/Tools`
- Created held prefab scaffolds for those seven tools under:
  - `Assets/_Project/Prefabs/Tools/Held`
- Bound each new prefab to its corresponding `ItemData` and `ToolMetadata` in prefab YAML.
- Cleaned temporary `_TMP` authoring objects from `02_HECTON_WORLD` after prefab generation.
- Unity console was rechecked after script and prefab import:
  - no new compile errors
  - no new warnings from these tool additions

## 2026-03-26 PDA / Inventory / Hotbar

- New task switched from sky/HUD debugging to PDA / inventory / quick-access system.
- Verified existing reusable backbone before coding:
  - `PlayerInventory` is already the inventory authority and save source.
  - `InventoryGrid` is already the tetris placement core.
  - `PlayerPDA` is already the PDA shell with tabs/fade/input map switching.
  - `PlayerToolManager` is already the 4-slot equip authority.
  - `PDAControlsRebindUI` already fits a controls tab.
- Verified current scene state:
  - `PlayerInventory` and `PlayerToolManager` are attached to `--- GAMEPLAY ---/Player`.
  - `PlayerPDA` is not attached in the live scene.
  - `Suit_HUD_Canvas` is the active UI canvas.
  - `Suit_HUD_ProjectionSource`, `HUD_Render_Camera`, and `Suit_Visor` are inactive and remain out of scope.
- Verified current architectural limitation:
  - `InventoryGrid` stores `ItemData` per cell only.
  - There is no proper item-instance / stack-splitting model yet.
  - First pass must therefore build UI/shell integration on top of the existing grid, not invent a second backend.
- Added design contract:
  - `PDA_INVENTORY_PLAN.md`
  - defines authorities, limits, implementation order, and first-pass scope
  - tabs for first pass: `Inventory`, `Equipment`, `Controls`
  - `OnInventory` should open PDA directly to the inventory tab
  - HUD hotbar should reflect the existing 4 tool slots


## 2026-03-26 Latest

- `SkySystemFollowCamera.cs`
  - fixed editor follow target priority: `runtimeCamera -> Camera.main -> SceneView.camera -> active enabled camera`
  - added explicit `EditorApplication.update` tick because `ExecuteAlways/LateUpdate` was not keeping `Sky_System` synced in edit mode
  - verified through MCP after compile: `Sky_System.position` now matches `Main Camera.position`
  - impact: removed one direct cause of black sky in `Game` while editing
- `SuitHUDV4CanvasOverlay.cs`
  - removed failed slanted bar vitals pass
  - rebuilt left vitals as compact radial gauges around the numeric value
  - `LayoutRevision = 12`
  - active knobs now are:
    - `gaugeColumnSpacing`
    - `gaugeRingSize`
    - `gaugeRingThickness`
    - `gaugeIconSize`
    - `gaugeValueOffsetY`
    - `gaugeLabelOffsetY`
  - goal: stop label overlap, stop huge empty gap to the right, restore a cleaner bottom-left module
- Remaining sky issue is narrowed further:
  - sun and gas giant render in `Scene View`
  - cloud/custom sky layer is still not matching `Game`
  - atmosphere and underwater state are no longer the primary cause
  - remaining fault is inside editor sky presentation / custom sky shader path

## 2026-03-26

- `SuitHUDV4CanvasOverlay` получил второй pass по левому bar-блоку после неудачного первого варианта:
  - первый bar-layout оказался визуально перегруженным и слабым
  - второй pass убирает `Sub` из видимого интерфейса, сокращает label/value, делает bars длиннее и чище
  - цель: уйти от дешёвого “табличного” вида к более собранному tech-strip
- По `Scene View` sky/clouds:
  - confirmed через MCP: после фикса editor не считается под водой (`CurrentDepth = 0`, `IsUnderwater = false`)
  - значит остаточная проблема облаков уже не в underwater-state
  - оставшийся дефект находится в editor-представлении sky pipeline / material presentation, а не в глубине/воде

- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` переведён с круговых gauge-ring на горизонтальные slanted bars в `HUD_V4_CanvasRoot/GaugeClusterRoot`:
  - `LayoutRevision` поднят до `9`
  - левый блок теперь строится как вертикальный stack из `Gauge_O2`, `Gauge_HLT`, `Gauge_PWR`
  - каждый gauge состоит из `Icon`, `BarBack`, `BarFill`, `BarFrame`, `Label`, `Value`, `Sub`
  - реальные live-метрики для bar-блока: `oxygen`, `integrity`, `energy`
  - `food/water` сознательно не добавлялись, потому что в `HectonSurvivalSystem` этих данных нет
- Добавлен editor-fix для `Scene View`:
  - `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs`
  - насильно включает `showSkybox`, `showClouds`, `showImageEffects`, `showFog`, `sceneLighting`, `CameraClearFlags.Skybox`
  - цель: убрать зависимость scene-view от случайно выключенного skybox/fx режима редактора
- Проверка через MCP после этих правок:
  - консоль без новых ошибок
  - `HUD_V4_CanvasRoot/GaugeClusterRoot` реально пересобран в bar-иерархию
  - `Scene View` больше не даёт чистый оранжевый контур по краю; виден газовый гигант, но sky still not final — остаточная проблема ещё есть

- Плоский HUD в `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` переведён на более честную семантику без фейковых survival-метрик:
  - `DEPTH` теперь показывается отрицательным (`-50 m`)
  - третий gauge больше не `HLT/HULL`, а `SAFE / DEPTH LIMIT`
  - статусная строка больше не использует `HULL INTEGRITY` как основной текст для обычного костюма
- Проверка по коду показала: в `HectonSurvivalSystem` нет реальных `food/water/hunger/thirst`; есть только `oxygen/energy/integrity/depth/pressure`. Без новой механики вода/еда в HUD были бы фейком.
- Плоский HUD изолирован по шрифтам:
  - добавлены `labelFont` и `numericFont` в `SuitHUDV4CanvasOverlay`
  - live `Suit_HUD_Canvas.labelFont` переключён на `Assets/_Project/Art/Materials/Fonts/текст SDF.asset`
  - `numericFont` оставлен на `Assets/_Project/Art/Materials/Fonts/цифры SDF.asset`
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` обновлён под те же font references, чтобы изоляция не жила только в сцене.

Не стирать старые записи. Новые записи добавлять в начало файла.

Правила ведения:
- Писать коротко и по фактам.
- Для каждого изменения фиксировать: что менялось, где менялось, зачем менялось, к чему привело.
- Если правка оказалась плохой, не удалять запись, а помечать как неудачную и писать откат.
- Если состояние сцены или live-параметры важны, фиксировать их явно.
- Если есть гипотеза, помечать её как гипотезу, а не как факт.

## 2026-03-27 - Flashlight tool adapter

- Добавлен `Assets/_Project/Scripts/FlashlightTool.cs`.
  - Что: новый `PlayerTool`-наследник для фонаря.
  - Зачем: аккуратно ввести `Flashlight` в общий tool / prefab / quickbar pipeline, не создавая вторую систему света.
  - Как: `FlashlightTool` не рендерит свой отдельный свет, а оборачивает уже существующий `PlayerFlashlight`.
  - Поведение первого прохода:
    - primary = toggle текущего `PlayerFlashlight`
    - secondary = status/info через `HUDNotification`
    - при unequip может выключить свет только если до equip фонарь был выключен
- Создан placeholder-material:
  - `Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat`
- Создан held prefab scaffold:
  - `Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab`
  - В prefab вручную зафиксированы:
    - `_toolData -> Item_Tool_Flashlight`
    - `_toolMetadata -> ToolMetadata_Flashlight`
    - root transform обнулён
    - visual child сдвинут/масштабирован как placeholder-корпус
- В `Tool_Flashlight_Held.prefab` отключены `enableDurabilityDrain` и `enableEnergyConsumption` у базового `PlayerTool`-слоя.
  - Причина: энергия/состояние фонаря уже обслуживаются существующим `PlayerFlashlight`, не нужно дублировать drain в двух системах.
- Временные `_TMP` tool objects удалены из live scene:
  - `Tool_Flashlight_Held_TMP`
  - `ToolPrefab_Scanner_TMP`
  - `ToolPrefab_Repair_TMP`
  - `ToolPrefab_Builder_TMP`
  - `ToolPrefab_LaserCutter_TMP`
- Сцена `Assets/_Project/Scenes/02_HECTON_WORLD.unity` сохранена после cleanup.
- Проверка:
  - Unity compile clean
  - console clean (0 warnings/errors по новым правкам)
  - активные 4 слота игрока не менялись, чтобы не ломать текущий тестовый набор

## 2026-03-26

- Gauge ring в `SuitHUDV4CanvasOverlay` переписан второй раз:
  - убрана квадратная `Image`-заглушка
  - теперь используется runtime-generated ring sprite + `Image.Type.Filled` с `Radial360`
  - цель: Subnautica-like круговой индикатор вокруг числа
- `LayoutRevision` в `SuitHUDV4CanvasOverlay` поднят до `7`, чтобы gauge hierarchy пересобралась.
- RenderSettings.skybox переставлен с ошибочного `Mat_Skybox_Final` на проектный `Mat_HectonSky`.
- Проверка показала: `Sky_System/Sphere` не исчезала. Она активна, `MeshRenderer.enabled = true`, material = `Mat_HectonSky`.
- Вывод по небу: проблема не в отсутствии `Sky_System`, а в том, как `Scene View` показывает купол/небесную систему изнутри и как celestial state затемняет сцену.

- Задача переключена обратно на плоский HUD. Объёмная ветка отключена:
  - `HUD_Render_Camera` inactive
  - `SuitHUDPresentationController` disabled
  - `VisorHUDController` disabled
  - `Suit_Visor.MeshRenderer` disabled
  - `Suit_HUD_ProjectionSource` inactive
- Возвращён проектный sky material в RenderSettings:
  - `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - Ранее по ошибке был подставлен `Mat_Skybox_Final`, это было неверно.
- Проверено через MCP:
  - `Sky_System/Sphere` существует
  - `MeshRenderer.enabled = true`
  - `scale = 25000`
  - material = `Mat_HectonSky`
- По небу/солнцу зафиксирован live-state:
  - `HectonCelestialEngine.IsEclipseActive = true`
  - `Directional Light.intensity = 0`
  - `Mat_HectonSky` имеет `_NightBlend = 1.0`, `_EclipseOcclusion = 1.0`
  - Проблема с тёмной сценой связана не с time-of-day, а с eclipse-state.
- Gauge ring в `SuitHUDV4CanvasOverlay` сначала был переведён с отсутствующего glyph на текст `"O"`. Это было технической заглушкой и визуально плохим решением.
- Затем gauge ring был переделан в `Image`-рамку и `Image`-fill прямоугольного типа. Это тоже оказалось неправильным визуальным направлением.
- Следующий шаг по HUD: сделать gauge как настоящий круговой индикатор с radial fill, без текстовых символов и без квадратной имитации.
## 2026-03-26 - PDA / Inventory handoff

- Stabilized project away from the abandoned volumetric HUD branch.
- User explicitly requested that large/complex feature work can be handed off as a master prompt for Claude.
- Added `CLAUDE_MASTER_PROMPT_PDA.md` with the full implementation brief for PDA / inventory / quickbar / controls integration.
- Added `MCP_CONSOLE_NOTES.md` documenting that several current console messages are MCP serializer/tooling issues, not core gameplay regressions.
- Current direction:
  - keep flat HUD path
  - use existing `PlayerInventory`, `PlayerPDA`, `PlayerToolManager`, `PDAControlsRebindUI`
  - build PDA / inventory / hotbar on top of those systems
  - do not revive volumetric visor HUD for now
## 2026-03-26 — Tool data rollout

- Created 12 `ItemData` assets under `Assets/_Project/Data/Items/Tools`
- Created 12 `ToolMetadata` assets under `Assets/_Project/Data/Tools`
- Expanded `ItemCatalog.asset` with the new tool item assets
- Created held prefab scaffolds:
  - `Tool_Scanner_Held`
  - `Tool_Repair_Held`
  - `Tool_Builder_Held`
  - `Tool_LaserCutter_Held`
- Bound those four prefabs into `PlayerToolManager.toolPrefabs[0..3]`
- Created `TOOL_MATRIX.md` as the tool registry / rollout source of truth

Notes:
- Flashlight remains an existing `PlayerFlashlight` path, not yet a `PlayerTool` prefab
- The four held prefabs are logic scaffolds only and still need visuals/audio tuning
- The remaining eight tools currently exist only as data assets until gameplay scripts are added

## 2026-03-27 — PDA tabs completion pass

- `PDAControlsRebindUI` upgraded from a reference-only shell into a self-building runtime tab.
  - If the tab has no preauthored rows, it now creates the whole controls list, selection markers, binding boxes, and status line itself.
  - Existing event-driven rebinding flow through `InputManager` / `RebindingManager` remains intact.
- Added `Assets/_Project/Scripts/UI/PDADataLogTab.cs`.
  - This is now the third PDA tab (`Data Log`) instead of a dead placeholder.
  - It shows live suit telemetry, cargo summary, manifest preview, and current quick-slot loadout.
- `Tab_Reserved` in `02_HECTON_WORLD` was cleaned from the old non-UI TMP placeholder and is now intended to host `PDADataLogTab`.
- `PlayerPDA` comments/tooltips were aligned to the real contract:
  - `0 = Inventory`
  - `1 = Controls`
  - `2 = Data Log`

## 2026-03-27 - PDA inventory usability pass

- `PDAInventoryTab` now has category filters:
  - `ALL`
  - `TOOLS`
  - `CONS`
  - `MATS`
  - `PARTS`
- Filtering is UI-side only and does not mutate `PlayerInventory` or `InventoryGrid`.
- Added a `CargoDigest` block under the grid:
  - anchor count
  - unit count
  - free cells
  - per-category breakdown
- Inventory footer now reports both cargo mass and used cells.
- Item details now show stack state, total stack mass, and whether the item is consumable/field-use only.
- Compile was rechecked after this pass; only legacy/third-party warnings remain in console.

## 2026-03-27 - PDA loadout assignment pass

- `PlayerToolManager` now owns a serialized `knownToolPrefabs` registry for the full held-tool set.
- Added `GetKnownToolPrefabForItem(ItemData)` so PDA/inventory UI can resolve a runtime-held prefab from an inventory item without hardcoded scene hacks.
- `PDAInventoryTab` details panel now includes `SET SLOT 1-4` loadout buttons for tool/equipment items.
- Selected tools can now be assigned directly from inventory into quick slots through `PlayerToolManager.SetAssignedToolPrefab(...)`.
- Loadout assignment feeds back into HUD via existing tool-assignment events instead of introducing a second loadout backend.

## 2026-03-27 - PDA loadout tab pass

- Added `PDALoadoutTab` as a dedicated PDA screen for quick-slot readiness.
- Expanded PDA tab contract to `Inventory / Loadout / Controls / Data Log`.
- `PDAInventoryTab` top bar now exposes all four tabs instead of the old three-label shell.
- Loadout cards now read real assignment, cargo availability, durability, and energy profile from the existing tool systems.

## 2026-03-27 - PDA loadout interaction pass

- Upgraded `PDALoadoutTab` from read-only summary into a working management screen.
- Each loadout card now exposes slot actions:
  - activate slot
  - holster current slot
  - clear slot assignment
- Added HUD feedback for loadout actions and invalid states through `HUDNotification`.
- Kept all actions routed through existing `PlayerToolManager` APIs instead of adding parallel state.

## 2026-03-27 - PDA inventory decision-support pass

- Expanded `PDAInventoryTab` details panel with richer decision-support fields:
  - effect profile
  - live status
  - recommended next action
- Consumables now expose actual suit restore profile directly in the details view.
- Tool/equipment items now expose loadout relevance and registry/assignment state directly in inventory.
- Details panel now refreshes immediately after loadout assignment so the user sees the new state without tab churn.

## 2026-03-27 - PDA inventory contextual-action pass

- Upgraded the former `USE` button in `PDAInventoryTab` into a contextual primary-action control.
- Consumables still execute `UseSelectedItem()`, but assignable tools now expose direct actions:
  - `ARM Sx`
  - `ACTIVATE Sx`
  - `HOLSTER`
  - `RE-ARM Sx`
  - `NO PREFAB`
- Primary action now routes through the real tool/loadout backend instead of forcing the user to leave Inventory just to arm or activate a selected tool.
- Rechecked both compile and a short play-mode smoke pass after the change; no new red errors were emitted.

## 2026-03-27 - PDA directives pass

- `PDALoadoutTab` now emits a live directive line instead of a static hint:
  - no kit assigned
  - broken tools present
  - cargo/loadout mismatch
  - under-armed expedition state
  - ready-to-deploy state
- `PDADataLogTab` now includes a dedicated `OPERATIONS DIRECTIVE` block driven by real suit/cargo state:
  - low integrity
  - low oxygen
  - low energy
  - elevated pressure
  - heavy cargo load
  - stable expedition profile
- `PDADataLogTab` footer hint is now dynamic and reflects current quick-slot readiness instead of staying static.
- Compile was rechecked and a post-play console sweep stayed clean after the pass.

## 2026-03-27 - PDA severity-visual pass

- `PDALoadoutTab` now gives each slot card stronger visual hierarchy:
  - left accent bars
  - status-chip backplates
  - state-tinted severity colors for `READY`, `MISSING`, `BROKEN`, and `UNASSIGNED`
- `PDADataLogTab` now renders the operations directive inside a dedicated severity panel instead of plain text.
- Directive visuals now shift between stable, warning, and critical states based on live suit/cargo conditions.
- `PDADataLogTab` footer hint now also changes color based on actual loadout readiness state.
- Rechecked compile and post-play console after the visual pass; no new red errors were emitted.

## 2026-03-27 - PDA controls visual-language pass

- `PDAControlsRebindUI` now uses the same stronger visual language as the other PDA tabs:
  - selected-row background emphasis
  - accent bars
  - stronger binding-box highlight on the focused row
- Controls status line now has explicit visual states for:
  - neutral browse state
  - active rebinding state
  - successful completion state
- This keeps the Controls tab from feeling like a legacy/debug screen next to Inventory, Loadout, and Data Log.
- Compile was rechecked and a post-play console sweep stayed clean after the pass.

## 2026-03-27 - PDA shell chrome pass

- Added a new runtime shell component: `PDAShellChrome`.
- Attached it to `PDA_Panel` so the whole PDA now has a shared top/bottom chrome layer independent of individual tabs.
- Shell chrome now shows:
  - fixed system title
  - current active tab
  - cargo cells / cargo mass / ready tools
  - oxygen / power / PDA online state
- Shell header/footer severity now shifts between stable, warning, and critical states using live suit/cargo/loadout conditions.
- Added corner brackets and shell rules so the PDA reads as one coherent premium panel instead of four separate screens.
- Rechecked compile and post-play console after live attachment to `PDA_Panel`; no new red errors were emitted.

## 2026-03-27 - PDA inventory section-rhythm pass

- `PDAInventoryTab` now has clearer flagship-tab sectioning instead of a flat grid/details layout:
  - `CARGO GRID`
  - `ITEM ANALYSIS`
  - `QUICK ACCESS MATRIX`
  - `CARGO DIGEST`
- Grid and details panels were shifted to leave explicit section-label space, improving top-of-screen breathing room.
- Added an additional lower rule and extended vertical separator so the inventory screen reads with stronger structural rhythm.
- Sort control was moved into cleaner alignment with the grid header band instead of floating deeper in the panel.
- Rechecked compile and post-play console after the layout pass; no new red errors were emitted.

## 2026-03-27 - PDA inventory detail-card pass

- `PDAInventoryTab` selected-item presentation was upgraded into a stronger command-card treatment:
  - dedicated icon-box backplate
  - title band
  - status chip panel
  - action recommendation panel
- Detail-card chrome now changes tint by item category and item state instead of using a flat single-color block.
- During this pass a real runtime regression was caught:
  - duplicate `Image` usage on the same detail icon container caused a `NullReferenceException`
  - fixed by splitting the icon background and icon visual into separate UI objects
- Rechecked compile and post-play console after the fix; the pass now closes clean with no red errors.
