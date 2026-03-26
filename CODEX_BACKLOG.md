# Codex Backlog

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
