# Procedural World Fill Enterprise Plan

## Цель
- Построить не “ещё один scatter”, а масштабируемую систему наполнения подводного мира.
- MapMagic отвечает за поля и маски.
- Наш runtime/authoring слой отвечает за осмысленные семейства контента, pockets, landmarks, ruins, cave entries и creature spawn anchors.

## Главный принцип
- Не спавнить конкретный prefab напрямую как часть дизайна мира.
- Спавнить через цепочку:
- `Biome / heatmap / zone -> family rule -> family profile -> variant prefab`

Это нужно, чтобы потом:
- у одной категории было много разных видов
- не переписывать код при добавлении новых ассетов
- иметь proxy-слой сейчас и финальные модели потом

## Что делает MapMagic
- height / depth / slope
- biome masks
- density / heatmap fields
- terrain distortion
- raw nature scatter там, где он массовый и тупой

## Что делают наши скрипты
- выбор осмысленного семейства контента
- pockets риска/награды
- landmarks
- abandoned modules / ruins
- cave entry markers
- creature spawn anchors
- service / power traces

## Первая волна family-групп

### Rocks
- `Rock Small Floor`
- `Rock Cluster Medium`
- `Rock Arch Large`

### Flora
- `Kelp Tall`
- `Kelp Patch Dense`
- `Plant Giant`

### Coral / bio
- `Coral Low`
- `Coral Branching`
- `Egg Cluster`

### Debris / salvage
- `Debris Scatter`
- `Debris Field`

### Ruins / structures
- `Ruin Module Single`
- `Ruin Cluster Medium`
- `Ruin Megastructure`

### Cave readability
- `Cave Entrance Marker`

### Landmarks
- `Landmark Spire`

### Fauna anchors
- `Creature Spawn Passive`
- `Creature Spawn Predator`

### Gameplay pockets
- `Pocket Resource`
- `Pocket Hazard`
- `Pocket Safe`
- `Route Power`
- `Service Scar`

## Почему это enterprise, а не времянка
- Каждая family живёт как `WorldPrefabFamilyProfile`
- Есть отдельные `WorldProceduralPlacementRule`
- У family уже есть:
- domain
- placement mode
- fidelity
- budget class
- spacing
- cluster sizes
- heatmap channel
- proxy color
- variants

Это позволяет:
- сначала работать proxy-кубами и простыми prefab’ами
- потом подменять их финальными моделями без смены архитектуры

## Что создаётся в коде прямо сейчас
- Расширен [WorldPrefabFamilyProfile.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs)
- Добавлен [WorldProceduralPlacementRule.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldProceduralPlacementRule.cs)
- Добавлен authoring tool [WorldProceduralProxyAuthoring.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/WorldProceduralProxyAuthoring.cs)
- Добавлен proxy scene builder [WorldProceduralProxySceneBuilder.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs)
- Добавлен instance metadata component [WorldProceduralProxyInstance.cs](C:/hades/Hecton8/Assets/_Project/Scripts/WorldProceduralProxyInstance.cs)

## Что делает authoring tool
Меню:
- `Hecton/Authoring/Build Procedural Fill Foundations`

Он создаёт:
- family assets в `Assets/_Project/Data/World/ProceduralFamilies`
- placement rule assets в `Assets/_Project/Data/World/ProceduralPlacementRules`

Это не финальный scatter runtime.
Это первый production-фундамент для:
- MapMagic integration
- proxy placement
- будущих variant prefabs

## Что уже умеет foundation
- Создавать family assets
- Создавать placement rule assets
- Создавать proxy materials и proxy prefabs под family
- Строить proxy scene root из существующих `WorldContentSocket`
- Помечать каждую proxy-instance метаданными:
- family
- rule
- zone
- socket
- fidelity
- variant

## Что уже поднято в runtime
- Добавлен `WorldProceduralFillDirector` в `Assets/_Project/Scripts/WorldProceduralFillDirector.cs`
- Он живет в `[MANAGERS]` рядом с `WorldZoneDirector`, `WorldContentDirector`, `WorldPopulationDirector` и `BiomeMatrixDirector`
- Он в рантайме решает цепочку:
- `zone + biome + socket -> procedural rule -> family -> variant`
- Он кладет resolved procedural diagnostics прямо в `WorldContentSocket`
- `WorldContentDirector` теперь показывает nearest procedural read:
- rule
- family
- variant
- source
- heatmap
- intent
- reason

## Что уже проверено в Unity
- `Hecton/Authoring/Build Procedural Fill Foundations` проходит без ошибок в консоли
- `Hecton/Authoring/Rebuild World Runtime Stack` проходит без ошибок в консоли
- `Hecton/Authoring/Rebuild Procedural Proxy Scene` проходит без ошибок в консоли
- `Hecton/Validation/Validate MapMagic World Stack` проходит без ошибок и warning в консоли
- В сцене реально существует `__PROCEDURAL_PROXY_WORLD`
- В сцене реально существует `[MANAGERS]` с `WorldProceduralFillDirector`
- Найдено `157` scene objects с `WorldProceduralProxyInstance`
- Сцена сохранена: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

## Что уже поднято следующим шагом
- Добавлен `WorldProceduralFieldSampler` в `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`
- Добавлен `WorldProceduralScatterDirector` в `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- Добавлен editor menu builder `Hecton/Authoring/Rebuild Procedural Scatter Preview`
- Новый scatter-слой больше не зависит от `WorldContentSocket` как единственного источника мира
- Теперь chain такой:
- `field sample (height/depth/slope/zone/biome) -> procedural rule -> family -> variant -> proxy instance`

## Что это значит простыми словами
- Раньше прокси-мир строился только вокруг заранее размеченных точек
- Теперь появился второй слой, который сам смотрит на местность вокруг игрока
- И уже от этого может ставить:
- кораллы
- келп
- камни
- руины
- опасные pockets
- и другие family-группы

## Честный статус scatter preview
- В этой сцене `MapMagicBridge` не всегда отдаёт живую высоту terrain в editor-preview режиме
- Поэтому в `WorldProceduralFieldSampler` добавлен честный fallback:
- сначала попытка взять высоту из MapMagic
- потом лучом по коллайдерам сцены
- и только потом синтетическое preview-дно под водой
- Это не финальный runtime-режим мира
- Это авторский режим превью, чтобы система уже была полезной до полного wiring MapMagic-полей

## Что уже проверено в Unity по scatter
- `Hecton/Authoring/Rebuild Procedural Scatter Preview` проходит без ошибок в консоли
- В сцене реально существует `__PROCEDURAL_SCATTER_WORLD`
- После rebuild preview в сцене реально появились новые `WorldProceduralProxyInstance`
- Один из проверенных примеров:
- family = `family.coral.branching`
- rule = `rule.coral.reef`
- source = `FieldScatter`
- heatmap = `coral_density`

## Правильный порядок внедрения

### Stage 1
- rocks
- kelp
- coral
- debris
- cave markers
- creature spawn anchors
- resource / hazard / safe pockets

### Stage 2
- ruins
- service scars
- power routes
- medium landmarks

### Stage 3
- huge abandoned modules
- megastructures
- deeper biome-specific silhouettes

## Что не делаем
- не кодим placement отдельно под каждый prefab
- не заставляем MapMagic решать весь gameplay-дизайн
- не тащим мир в рельсовые маршруты
- не делаем giant content pass без базового природного слоя

## Следующий продуктовый шаг
- Поднять first usable proxy pass:
- family assets
- placement rules
- proxy prefabs
- и потом уже связать это с biome/zone/heatmap логикой

## Status Update 2026-03-31
- Stage 1 procedural fill is now running as a layered proxy world, not a flat single-pool scatter.
- `WorldPrefabFamilyProfile` has `ScatterLayer`:
- `Ground`
- `Cluster`
- `Structure`
- `Spawn`
- `WorldProceduralScatterDirector` now applies separate budgets by layer instead of one shared top-2 fight.
- Stage 1 catalog now covers all current procedural families with working rules.
- `WorldProceduralProxyAuthoring` now creates multiple proxy variants per family instead of one identical block.
- `WorldProceduralFieldSampler` now reports the seafloor source explicitly:
- `MapMagicHeight`
- `SceneRaycast`
- `FallbackSynthetic`

### Verified In Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed with no errors/warnings in console
- Scene root `__PROCEDURAL_SCATTER_WORLD` exists
- `WorldProceduralProxyInstance` total in scene: `358`
- Active field-driven scatter instances under `__PROCEDURAL_SCATTER_WORLD`: `201`

### Layer Result In Current Preview
- `Ground`: `139`
- `Cluster`: `21`
- `Structure`: `6`
- `Spawn`: `35`

### Honest Meaning
- The proxy world is no longer just "floor dressing".
- It now visibly contains:
- natural floor/background

## Status Update 2026-03-31 J

### Что переведено в data-driven слой
- Добавлен `WorldProceduralPatternProfile`:
- каждый из `9` типов воды теперь живёт как отдельный asset с бюджетами, квотами и целями по слоям
- Добавлен `WorldProceduralPatternCatalog`:
- scatter теперь берёт balance-правду из каталога, а не из большого hardcoded switch
- `WorldProceduralProxyAuthoring` теперь создаёт и обновляет:
- `9` pattern profiles
- общий pattern catalog
- `WorldRuntimeBootstrapAuthoring` автоматически подцепляет catalog в runtime stack
- `MapMagicWorldValidator` теперь валидирует:
- есть ли catalog
- задан ли fallback profile
- все ли `9` water patterns реально покрыты asset-профилями

### Что это даёт простыми словами
- тип воды теперь настраивается как контент
- можно крутить бюджеты, mix и spawn не переписывая scatter-логику
- отчёт по всем водам стал главным источником правды
- это нормальная база под `108` биомов, а не ещё одна временная настройка в коде

### Проверено в Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Generate Procedural Water Pattern Report`: passed
- `Validate MapMagic World Stack`: passed
- console: `0 errors / 0 warnings`
- forced override после проверки выключен
- рабочая сцена снова в normal mode:
- `Sediment Drift + Synthetic:Resources + SedimentResources`

### Итог по 9 водам
- все `9` water patterns сейчас дают `PASS` в `PROCEDURAL_WATER_PATTERN_REPORT.md`
- мягкие воды больше не проседают:
- `FertileShallows`: `ground 107 | cluster 12 | structure 4 | spawn 8`
- `ReefNavigation`: `ground 170 | cluster 8 | structure 6 | spawn 6`
- reference-вода держится стабильно:
- `SedimentResources`: `ground 40 | cluster 18 | structure 9 | spawn 8`

### Честный смысл
- верхний sandbox-layer по воде уже не хрупкий прототип
- это рабочая enterprise-схема:
- `9` читаемых характеров воды
- data-driven профили
- единый отчёт
- валидируемый runtime wiring
- следующая большая работа уже не в самих water-pattern profiles
- а в более глубокой привязке этих `9` характеров к `108` биомам и к реальным content families мира
- clusters and pockets
- large structures
- creature spawn anchors
- This is still proxy preview, not final art pass.
- MapMagic is still unavailable in this scene preview, so the current result is driven by the fallback field mode on purpose.

## Status Update 2026-03-31 B
- Stage 1 rules now use real biome-family and zone-kind preferences instead of leaving those filters mostly empty.
- `WorldProceduralFieldSampler` now has synthetic fallback biome selection when MapMagic and live zones are unavailable.
- The same sampler now also derives a synthetic zone hint:
- `Resources`
- `Navigation`
- `Fabrication`
- `Service`
- `Power`
- `Combat`
- `Progression`
- This keeps biome/zone-aware rules usable in editor preview instead of making them all collapse to `None`.

### Verified In Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed with no errors/warnings in console
- Scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `WorldProceduralFieldSampler` fallback families are assigned
- sampler debug fallback biome is no longer `None`
- current sampler debug biome: `Sediment Drift`
- `WorldProceduralProxyInstance` total in scene: `295`
- active field-driven scatter instances under `__PROCEDURAL_SCATTER_WORLD`: `138`

### Current Preview Reading
- `Ground`: `68`
- `Cluster`: `21`
- `Structure`: `46`
- `Spawn`: `3`
- Honest meaning:
- the preview became more selective and less evenly smeared
- structure/service/ruin families now have a stronger voice in fallback preview
- this is a better step toward biome-specific world character, even though real MapMagic biome fields are still offline in this scene

## Status Update 2026-03-31 C
- `WorldPrefabFamilyProfile` now stores soft biome/zone affinity, so families can be gently attracted to the right water instead of only being hard-filtered by rules.
- Those affinities are not hand-copied in two places:
- `WorldProceduralProxyAuthoring` now derives family affinity automatically from Stage 1 rule definitions during foundations rebuild.
- `WorldProceduralScatterDirector` now adds a soft bonus/penalty from family affinity to candidate score.
- This means the same fallback preview space can now feel more like:
- `resource/sediment`
- `reef/navigation`
- `rift/power/combat`
- even before live MapMagic biome fields are online in-scene.
- Diagnostics also got cleaned up:
- synthetic zones now show up honestly
- rescue-placed `Structure` and `Spawn` layers now report their top families instead of `None`

### Verified In Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed with no errors/warnings in console
- Scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- total `WorldProceduralProxyInstance` in scene: `385`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `228`
- sampler fallback biome: `Sediment Drift`
- sampler fallback zone: `Synthetic:Resources`

### Current Preview Reading
- `Ground`: `73`
- `Cluster`: `97`
- `Structure`: `47`
- `Spawn`: `11`
- top overall family: `Pocket Safe`
- top ground family: `Kelp Tall`
- top cluster family: `Pocket Safe`
- top structure family: `Plant Giant`
- top spawn family: `Creature Spawn Passive`

### Honest Meaning
- the proxy world now has a much stronger local character instead of just "generic valid scatter"

## Status Update 2026-03-31 - Biome Pattern Ground Fix
- Added area-wide scatter diagnostics in `WorldProceduralScatterDirector`, not just last-sample diagnostics.
- The director now shows:
- dominant sampled biome family across the full evaluated area
- dominant sampled pattern across the full evaluated area
- dominant sampled zone across the full evaluated area
- dominant biome family per layer, so we can see whether ground/cluster/structure are actually coming from the intended water
- `WorldProceduralFieldSampler` fallback biome resolution was tightened to create more coherent sediment/service/rift regions across neighboring cells instead of overly mixed shallow fallback water.
- `SedimentResources` fallback preview was strengthened for ground:
- stronger rock heat scaling
- stronger rock fallback rescue
- much harsher coral suppression in sediment water
- new ground-rescue pass for fallback preview, so seabed language does not disappear behind spawn gating

### Verified In Unity
- Console: `0 errors / 0 warnings`
- Scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `MapMagicBridge.IsAvailable = false`, so this result is still honest fallback-preview behavior
- current sampled dominant biome family: `Sediment Drift` (`156` sampled cells)
- current sampled dominant pattern: `SedimentResources` (`152` sampled cells)
- current sampled dominant zone: `Synthetic:Resources` (`152` sampled cells)
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `246`
- layer counts:
- `Ground = 44`
- `Cluster = 152`
- `Structure = 21`
- `Spawn = 29`

### Honest Result
- The main product bug from the previous pass is fixed:
- fallback resource water no longer reads as coral-first ground
- current ground top family: `Rock Small Floor`
- current ground dominant family: `Rock Small Floor`
- current ground dominant biome family: `Sediment Drift`
- cluster/service/resource behavior still remains strong:
- top family: `Pocket Resource`
- dominant sampled biome still remains `Sediment Drift`
- shallow fertile fallback water is clearly pulling shelter/kelp/passive-life logic
- structures and spawns are no longer hidden behind broken diagnostics
- this is still proxy reality, not final art reality

## Status Update 2026-03-31 D
- Added an explicit procedural water-pattern layer:
- `FertileShallows`
- `ReefNavigation`
- `SedimentResources`
- `IndustrialService`
- `RiftHazard`
- `AbyssSparse`
- `LandmarkCorridor`
- `WorldProceduralFieldSampler` now resolves a concrete pattern for every sampled cell, even in fallback preview.
- `WorldProceduralScatterDirector` now uses that pattern in both score and per-layer local budgets.
- `WorldProceduralScatterDirector` also now adds a small domain-context bonus:
- sediment water gently favors rocks / safe-resource pockets
- industrial water favors debris / service / ruins
- reef water favors kelp / coral / growth
- hazard water favors hazard pockets / predator anchors
- `WorldProceduralProxyAuthoring` now writes primary/secondary pattern intent and pattern affinity weight into Stage 1 family assets.
- During verification I found a real product issue:
- `Sediment Drift + Synthetic:Resources` water was resolving too often to `FertileShallows`.
- After that fix, cluster placements briefly collapsed to zero.
- I corrected that by adding a preview rescue path for cluster-layer placements so resource/shelter water keeps visible pockets instead of becoming only floor + large structures.

### Verified In Unity
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed with no errors/warnings in console
- current sampler biome: `Sediment Drift`
- current sampler zone: `Synthetic:Resources`
- current sampler pattern: `SedimentResources`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `133`
- total `WorldProceduralProxyInstance` in scene: `290`
- current layer counts:
- `Ground = 70`
- `Cluster = 6`
- `Structure = 47`
- `Spawn = 10`
- current top families:
- `Ground = Coral Low`
- `Cluster = Pocket Safe`
- `Structure = Plant Giant`
- `Spawn = Creature Spawn Passive`

### Honest Meaning
- the water is now reading closer to "resource-rich sediment water" instead of accidentally behaving like fertile reef water
- the world became more selective and less overfilled than the previous fertile fallback pass
- cluster/pocket layer is no longer lost when the pattern shifts toward sediment/resource logic
- this is a better base for the next step: making different water types feel intentionally different instead of merely valid

## Status Update 2026-03-31 E
- Added a second ecology layer on top of water-pattern logic:
- pattern now influences raw heat strength, not just score bonuses
- depth now also influences domain viability
- this means shallow plant/coral/kelp families no longer behave the same way at `30m` and `140m`
- the scatter system now reshapes local ecology using:
- water type
- depth
- domain

### Verified In Unity
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed with no errors/warnings in console
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- current sampler biome: `Sediment Drift`
- current sampler zone: `Synthetic:Resources`
- current sampler pattern: `SedimentResources`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `128`
- current layer counts:
- `Ground = 24`
- `Cluster = 49`
- `Structure = 46`
- `Spawn = 9`
- current top families:
- `Ground = Coral Low`
- `Cluster = Pocket Safe`
- `Structure = Plant Giant`
- `Spawn = Creature Spawn Passive`

### Honest Meaning
- this pass made the world more selective and more depth-aware
- the same sediment/resource water no longer floods with shallow-style ground clutter
- cluster layer remains healthy instead of disappearing
- but one honest issue remains:
- in this specific fallback sample, ground still leans too coral-heavy for the intended sediment reading
- next strong fix should likely move from score-only tuning to better pattern-shaped fallback heat fields, not endless micro-tweaking of one bonus

## Status Update 2026-03-31 F

### What Was Actually Fixed
- Added area-wide diagnostics in `WorldProceduralScatterDirector`, not just one-point debug:
- dominant sampled biome
- dominant sampled pattern
- dominant sampled zone
- dominant accepted biome per scatter layer
- Improved fallback biome coherence in `WorldProceduralFieldSampler` so sediment/resource water stays spatially sediment-first more often.
- Corrected the next real product bug after the ground fix:
- in `Sediment Drift + Synthetic:Resources` water, large structures were still behaving too reef-like
- `Plant Giant / Fossil Reef` was dominating the structure layer
- Fixed that by changing three things together:
- stronger sediment-specific structure heat shaping
- stronger sediment pattern bonuses for rock-arch / cave / ruin / service structure domains
- wider Stage 1 rule coverage for sediment water on arches / ruins / caves / service-power fragments / spires

### Verified In Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Rebuild Procedural Scatter Preview`: passed
- `Validate MapMagic World Stack`: passed
- console: `0 errors / 0 warnings`
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `MapMagicBridge.IsAvailable = false`, so this is still fallback-driven preview, not live MapMagic biome field output
- dominant sampled biome: `Sediment Drift (156)`
- dominant sampled pattern: `SedimentResources (152)`
- dominant sampled zone: `Synthetic:Resources (152)`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `236`
- current layer counts:
- `Ground = 44`
- `Cluster = 152`
- `Structure = 4`
- `Spawn = 36`
- current top families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- dominant accepted biome by layer:
- `Ground = Sediment Drift`
- `Cluster = Sediment Drift`
- `Structure = Sediment Drift`
- `Spawn = Sediment Drift`

### Honest Meaning
- the same fallback sediment/resource water now reads much closer to a rocky sediment field with reward pockets and route memory
- the ground layer is no longer coral-dominated
- the structure layer is no longer giant-plant-dominated
- the remaining honest issue is the new balance extreme:
- structure is now correct in character, but too thin in count (`4`)
- next strong step should be widening structure variety and count in sediment/resource water without falling back into reef-like giant-plant spam

## Status Update 2026-03-31 G

### Follow-up Pass
- Added minimum rescue counts for:
- `Structure`
- `Spawn`
- Increased local structure budget for `SedimentResources`
- Tightened sediment fallback rescue against structure-scale flora, trying to stop giant-plant takeover without killing the whole layer

### Verified In Unity
- console: `0 errors / 0 warnings`
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- dominant sampled biome: `Sediment Drift (156)`
- dominant sampled pattern: `SedimentResources (152)`
- dominant sampled zone: `Synthetic:Resources (152)`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `212`
- current layer counts:
- `Ground = 44`
- `Cluster = 152`
- `Structure = 10`
- `Spawn = 6`
- current top families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- current dominant accepted families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Plant Giant`
- `Spawn = Creature Spawn Passive`

### Honest Meaning
- this pass successfully recovered structure count and spawn count
- but the structure layer is still contested:
- top structure family is now correct (`Rock Arch Large`)
- dominant structure family is still wrong (`Plant Giant`)
- so the remaining blocker is no longer generic scatter density
- it is specifically fallback structure ecology around mixed sediment / reef sub-cells

## Status Update 2026-03-31 H

### Balanced Sandbox Accents
- added explicit `structureAccentRole` usage to Stage 1 family authoring
- added sediment-specific structure quotas and caps inside `WorldProceduralScatterDirector`
- sediment structure now targets:
- `NaturalLandmark = 4-5`
- `TechFragment = 3-4`
- `CaveRead = 1-2`
- `BiologicalSilhouette = 1-2`
- added sediment-specific fauna tuning fields:
- `sedimentSpawnTargetMin/Max`
- `sedimentPassiveSpawnMin`
- `sedimentPredatorSpawnMax`
- cave-read fallback was strengthened so sediment/resource water can hold a visible “way inward” accent without turning into a level corridor

### Verified In Unity
- console: `0 errors / 0 warnings`
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- sampled area remains: `Sediment Drift + Synthetic:Resources + SedimentResources`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `214`
- current layer counts:
- `Ground = 44`
- `Cluster = 152`
- `Structure = 10`
- `Spawn = 8`
- current top families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- current dominant accepted families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- structure accent mix:
- `NaturalLandmark = 4`
- `TechFragment = 3`
- `CaveRead = 1`
- `BiologicalSilhouette = 2`
- spawn split:
- `Passive = 8`
- `Predator = 0`

### Honest Meaning
- sediment/resource water now reads as a balanced sandbox pocket instead of a pseudo-level
- nature still leads the scene through rocks and pockets
- tech traces are visible but not dominant
- giant plants remain present as accents, not as the owner of the whole structure layer
- fauna is now tunable and currently biased toward ambient passive life, not combat pressure

## Status Update 2026-03-31 I

### Pattern Layer Expanded
- expanded the top-layer procedural water character set from `7` to `9` patterns
- added:
- `BrineToxic`
- `VolcanicPressure`
- this does **not** replace the `108` biome matrix
- it sits above biome families as a bigger readable water character layer:
- `9` water patterns = large sandbox feel
- `108` biomes = concrete material, flora, resource, and visual variety inside those feelings

### Verified In Unity
- console: `0 errors / 0 warnings`
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- current sampled area remains:
- `Sediment Drift + Synthetic:Resources + SedimentResources`
- current sampler source:
- `Height = FallbackSynthetic`
- `Heatmap = service_density`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `212`
- current layer counts:
- `Ground = 44`
- `Cluster = 152`
- `Structure = 8`
- `Spawn = 8`
- current top families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- current dominant accepted families:
- `Ground = Rock Small Floor`
- `Cluster = Pocket Resource`
- `Structure = Rock Arch Large`
- `Spawn = Creature Spawn Passive`
- current structure accent mix:
- `NaturalLandmark = 4`
- `TechFragment = 0`
- `CaveRead = 2`
- `BiologicalSilhouette = 2`
- current fauna split:
- `Passive = 8`
- `Predator = 0`

### Honest Meaning
- the move to `9` big water patterns compiled and rebuilt cleanly
- the already-good sediment/resource sandbox area did **not** collapse after this expansion
- the current scene is still running on smart fallback preview because `MapMagicBridge.IsAvailable = false`
- so this is a verified proxy-world step, not yet the final live MapMagic field pass

## Status Update 2026-03-31 J

### Designer Preview Control
- added a safe preview-only pattern override to `WorldProceduralFieldSampler`
- by default it stays off
- when enabled, it can force a chosen water character only on fallback preview cells
- this lets world design inspect the new water characters even when live MapMagic biome fields are unavailable

### Verified In Unity
- console: `0 errors / 0 warnings`
- forced `BrineToxic` preview:
- `Ground = 10`
- `Cluster = 4`
- `Structure = 9`
- `Spawn = 4`
- dominant structure family: `Service Scar`
- dominant structure accent role: `TechFragment`
- forced `VolcanicPressure` preview:
- `Ground = 14`
- `Cluster = 4`
- `Structure = 9`
- `Spawn = 5`
- dominant structure family: `Cave Entrance Marker`
- dominant structure accent role: `CaveRead`
- after returning override to normal:
- sampled area again resolves as `Sediment Drift + Synthetic:Resources + SedimentResources`
- active scatter children under `__PROCEDURAL_SCATTER_WORLD`: `212`

### Honest Meaning
- `BrineToxic` now clearly reads as a dirtier and more service-scarred water character
- `VolcanicPressure` now clearly reads as a harder, cave-led, pressure-heavy water character
- so the new water types are no longer just code branches
- they are inspectable design states inside the current scene

## Status Update 2026-03-31 K

### Balanced Pattern Pass
- restored `SedimentResources` after the new pattern work so it again holds:
- rock-led floor
- resource-pocket cluster identity
- readable tech traces
- ambient passive life
- tuned `BrineToxic` as a deliberately sparser but still balanced toxic/service water:
- lower overall structure count than sediment
- visible tech fragments
- one natural landmark
- one cave-read
- tuned `VolcanicPressure` as a harder cave-and-landmark water:
- strong cave-read dominance
- supporting natural landmarks
- some tech residue, but not service-heavy

### Verified In Unity
- console: `0 errors / 0 warnings`
- scene saved: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- normal restored scene:
- `Sediment Drift + Synthetic:Resources + SedimentResources`
- `Ground = 44`
- `Cluster = 152`
- `Structure = 10`
- `Spawn = 8`
- sediment structure accents:
- `NaturalLandmark = 4`
- `TechFragment = 3`
- `CaveRead = 1`
- `BiologicalSilhouette = 2`
- forced `BrineToxic` preview:
- `Ground = 10`
- `Cluster = 4`
- `Structure = 7`
- `Spawn = 4`
- brine structure accents:
- `NaturalLandmark = 1`
- `TechFragment = 5`
- `CaveRead = 1`
- `BiologicalSilhouette = 0`
- forced `VolcanicPressure` preview:
- `Ground = 14`
- `Cluster = 4`
- `Structure = 9`
- `Spawn = 5`
- volcanic structure accents:
- `NaturalLandmark = 3`
- `TechFragment = 2`
- `CaveRead = 4`
- `BiologicalSilhouette = 0`

### Honest Meaning
- the system is no longer just “9 labels in code”
- at least three big water characters are now visibly distinct and sandbox-readable in the same scene
- the balance is intentionally not symmetrical:
- sediment/resource water is richer and more lived-in
- brine water is sparser and harsher
- volcanic water is denser in cave/landmark pressure

## Status Update 2026-03-31 L

### Water Pattern Balance Report
- added editor diagnostic menu:
- `Hecton/Validation/Generate Procedural Water Pattern Report`
- report output:
- `PROCEDURAL_WATER_PATTERN_REPORT.md`
- the report now captures all `9` water patterns from the same scene through forced fallback-compatible preview override

### What This Pass Actually Fixed
- `IndustrialService` is no longer dead water:
- it now keeps tech fragments and debris identity while also resolving passive ambient fauna
- `RiftHazard` now tests honestly as rift/combat water instead of sediment water with a borrowed label:
- fallback override now injects matching biome family and zone context
- `VolcanicPressure` now resolves as a real volcanic pressure water:
- hazard pockets appear
- cave reads appear
- passive and predator spawn anchors both resolve
- `FertileShallows` and `ReefNavigation` now have more honest navigation/sandbox accents:
- fertile water now gets at least a little resource-pocket variance
- reef water now resolves landmark and cave structure accents instead of only giant plants

### Verified Snapshot From Report
- `FertileShallows`
- total `128`
- ground `107`
- cluster `10`
- structure `3`
- spawn `8`
- cluster mix now includes resource + shelter instead of pure shelter spam
- `ReefNavigation`
- total `204`
- ground `184`
- cluster `8`
- structure `6`
- spawn `6`
- dominant structure shifted to `Landmark Spire`
- `SedimentResources`
- total `76`
- ground `44`
- cluster `15`
- structure `9`
- spawn `8`
- still the strongest resource-pocket sandbox water
- `IndustrialService`
- total `31`
- ground `12`
- cluster `8`
- structure `7`
- spawn `4`
- dominant structure `Route Power`
- dominant cluster `Debris Field`
- passive fauna present
- `BrineToxic`
- total `23`
- ground `10`
- cluster `4`
- structure `5`
- spawn `4`
- reads as sparse toxic service water with mixed passive/predator life
- `VolcanicPressure`
- total `32`
- ground `14`
- cluster `4`
- structure `9`
- spawn `5`
- dominant structure `Cave Entrance Marker`
- mix now includes both passive and predator spawns
- `RiftHazard`
- total `27`
- ground `10`
- cluster `4`
- structure `8`
- spawn `5`
- dominant cluster `Pocket Hazard`
- dominant structure `Cave Entrance Marker`
- predator-led spawn mix is working
- `AbyssSparse`
- total `14`
- ground `6`
- cluster `2`
- structure `3`
- spawn `3`
- still intentionally sparse
- `LandmarkCorridor`
- total `26`
- ground `10`
- cluster `3`
- structure `10`
- spawn `3`
- reads as landmark/cave guidance water rather than loot-heavy water

### Honest Meaning
- the project now has a reusable design report for all major water characters
- all `9` water patterns are no longer just labels in code
- they can be compared in one scene with stable fallback logic
- the remaining work is now product tuning, not blind systems guesswork

## Status Update 2026-03-31 — Water Patterns x Biome Context
- The top sandbox character layer now runs through two data-driven asset layers:
- `WorldProceduralPatternProfile` / `WorldProceduralPatternCatalog`
- `WorldProceduralBiomeFamilyContextProfile` / `WorldProceduralBiomeFamilyContextCatalog`
- Foundations authoring now creates and updates:
- `9` water pattern profiles
- biome-family context profiles for the current family set
- both runtime catalogs used by scatter
- `WorldProceduralScatterDirector` now combines:
- water-pattern profile
- biome-family context profile
- field sample + rule/family scoring
- Validator now checks:
- pattern catalog assignment
- biome context catalog assignment
- biome-family coverage in the context catalog
- `WorldProceduralPatternBalanceReport` now shows:
- pattern label
- biome context label
- whether fallback pattern/context profile was used

### Verified In Unity
- `Build Procedural Fill Foundations`: completed successfully in editor workflow
- `Rebuild World Runtime Stack`: passed
- `Generate Procedural Water Pattern Report`: passed
- `Validate MapMagic World Stack`: passed
- Console: `0 errors / 0 warnings`
- Scene saved cleanly: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Normal mode restored after report generation:
- `forcePatternPreviewOverride = false`
- `_debugPatternOverride = None`
- current preview water = `Sediment Drift + Synthetic:Resources + SedimentResources`
- current scatter director diagnostics:
- resolved pattern profile = `Sediment Resources`
- fallback pattern profile used = `false`
- resolved biome context profile = `Sediment Drift Context`
- fallback biome context profile used = `false`

### Final Result Right Now
- `PROCEDURAL_WATER_PATTERN_REPORT.md` is the source of truth for water balance
- all `9 / 9` sandbox waters are currently `PASS`
- this is now a scalable character layer:
- `9` readable water patterns on top of biome-family context
- ready to be pushed deeper into broader biome/content world work instead of staying a flat proxy tweak system

### Honest Note
- `Build Procedural Fill Foundations` may sometimes time out through MCP transport even when the editor finishes the work
- in those cases the real proof is:
- updated assets on disk
- regenerated report
- clean Unity console

## Status Update 2026-03-31 — Matrix Biome Bridge
- The procedural fill stack now uses not only:
- water pattern profiles
- biome family context profiles
- but also the current matrix-biome profile values where available
- `WorldProceduralFieldSampler` now carries matrix-biome context into field samples
- and shapes heatmaps with matrix-bias signals such as:
- `rewardPull`
- `salvageBias`
- `landmarkStrength`
- `survivalPressure`
- `WorldProceduralScatterDirector` now adds a matrix-biome score bonus on top of:
- pattern
- biome-family context
- rule/family scoring
- `PROCEDURAL_WATER_PATTERN_REPORT.md` now shows the representative matrix-biome used for each water pattern
- field-scatter proxy metadata now exposes:
- `sourceBiomeMatrix`
- `sourceBiomeFamily`
- `sourceWaterPattern`
- `sourceBiomeContext`

### Verified Result
- Example live field-scatter instance now reads with explicit source metadata instead of `None`
- example:
- `sourceBiomeMatrix = Soft Domes`
- `sourceBiomeFamily = Sediment Drift`
- `sourceWaterPattern = SedimentResources`
- `sourceBiomeContext = Sediment Drift Context`
- This turns field-scatter from a black box into an inspectable world-design tool.

## Status Update 2026-03-31 — Matrix Biome Quota Shaping
- Procedural fill now uses the dominant sampled matrix-biome not only for score shaping, but also for quota shaping.
- This means the representative matrix-biome around the player now influences:
- cluster minimums and target ranges
- structure minimums and target ranges
- spawn minimums and target ranges
- structure accent quotas
- cluster accent minimums and max ratios
- passive/predator spawn minimums and maximums

### What This Means
- Two places inside the same top-level water pattern can now diverge in a more honest way:
- one `SedimentResources` biome can pull harder toward resource pockets and passive life
- another `SedimentResources` biome can pull harder toward ruins, cave reads, salvage, or stronger pressure
- This is the first real bridge from the `9` water-character layer down toward the `108` biome-matrix layer.

### Diagnostics Upgrade
- `WorldProceduralScatterDirector` now tracks:
- dominant sampled matrix-biome
- quota-driving matrix-biome
- target ranges after matrix-biome shaping
- `PROCEDURAL_WATER_PATTERN_REPORT.md` now shows:
- `Matrix Biome`
- `Sample Dominant Matrix Biome`
- This removes the old confusion where the report only reflected the sampler's last cell instead of the biome that actually drove the area's quotas.

### Verified In Unity
- `Generate Procedural Water Pattern Report`: passed after the quota-shaping pass
- `Validate MapMagic World Stack`: passed
- Console: `0 errors / 0 warnings`
- Scene saved cleanly: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `PROCEDURAL_WATER_PATTERN_REPORT.md`: `9 / 9 PASS`

### Current Reference Preview
- normal-mode preview remains:
- `Sediment Drift + Synthetic:Resources + SedimentResources`
- current scatter diagnostics:
- quota-driving matrix-biome = `Soft Domes`
- dominant sampled matrix-biome = `Soft Domes`
- targets after shaping = `ground 40-50 | cluster 15-19 | structure 9-11 | spawn 8-10`
- current live counts = `ground 42 | cluster 19 | structure 9 | spawn 8`

### Honest Result
- the water layer is no longer only:
- `pattern profile + biome family context + matrix score bonus`
- it is now:
- `pattern profile + biome family context + matrix score bonus + matrix quota shaping`
- This is a materially stronger foundation for pushing procedural fill deeper into real biome-specific content memory instead of staying a flat proxy scatter system.

## 2026-03-31 Matrix Biome Memory Layer

### What was added
- `HectonBiomeMatrixProfile` now stores simple procedural memory fields:
- `primaryClusterFocus`
- `secondaryClusterFocus`
- `primaryStructureFocus`
- `secondaryStructureFocus`
- `faunaMood`
- `BiomeMatrixBootstrapAuthoring` now auto-fills those fields from existing biome data instead of requiring manual authoring for all 108 entries.
- `WorldProceduralScatterDirector` now uses those biome-memory fields to bias:
- cluster choices
- structure choices
- passive/predator spawn pressure
- `WorldProceduralFieldSampler` can now force a concrete matrix-biome in preview for validation.
- added report:
- `PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md`

### What it means in plain language
- the world no longer only knows:
- "this is resource water" or "this is hazard water"
- it now also knows:
- "inside this water, this exact biome is known for nests"
- "this one is known for salvage"
- "this one is known for cave reads"
- "this one is known for calmer or harsher fauna"

### Verified state
- Unity MCP validation stayed clean:
- console `0 errors / 0 warnings`
- `PROCEDURAL_WATER_PATTERN_REPORT.md` remains `9 / 9 PASS`
- the new matrix-biome memory report is generated successfully
- representative biomes inside the same water now differ more clearly by:
- cluster focus
- structure focus
- spawn mood
- dominant content families

### Honest remaining limitation
- this is still proxy-world logic, not final art placement
- some waters still show stronger differences in reports than in raw visual silhouette because the proxy family set is still limited
- next meaningful step is not more water tuning, but stronger binding between matrix biomes and concrete content family mixtures

## 2026-03-31 Matrix Biome Preferred Content Categories

### What was added
- `HectonBiomeMatrixProfile` now stores direct preferred content category lists:
- `preferredGroundFamilies`
- `preferredClusterFamilies`
- `preferredStructureFamilies`
- `preferredSpawnFamilies`
- `BiomeMatrixBootstrapAuthoring` now auto-fills those lists for all `108` matrix biomes from:
- biome family defaults
- reward / salvage / landmark / survival values
- text tokens such as `cave`, `reef`, `module`, `station`, `crystal`
- `WorldProceduralScatterDirector` now listens to those lists as the fourth influence layer:
- water pattern
- biome family ecology
- matrix memory
- direct biome content preferences
- added report:
- `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md`

### What it means in plain language
- the world no longer only knows:
- "this biome is resource-leaning"
- it now also knows:
- "this biome prefers pocket resources and ruin modules"
- "this biome prefers cave entrances and landmarks"
- "this biome prefers debris fields and predator anchors"
- this makes two biomes inside the same top-level water diverge more honestly by actual object mix, not only by soft score shaping

### Verified in Unity
- `Rebuild 108 Biome Matrix`: passed
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Generate Procedural Water Pattern Report`: passed
- `Generate Procedural Matrix Biome Memory Report`: passed
- `Generate Procedural Matrix Biome Content Report`: passed
- `Validate 108 Biome Matrix`: passed
- `Validate MapMagic World Stack`: passed
- Console: `0 errors / 0 warnings`
- Scene saved cleanly: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Water report remains `9 / 9 PASS`

### Honest result
- this is the first version where matrix biomes carry direct object-category intent
- the new content report now shows real preferred category differences inside the same water
- example:
- `SedimentResources` now diverges between:
- `Soft Domes`
- `The Silt Shadows`
- `Block-City`
- by preferred categories and by dominant cluster / structure outcomes
## 2026-03-31 Preferred Category Pressure In Scene

### What Was Actually Improved
- Preferred biome categories now push harder into the real proxy scene, not only into reports.
- `WorldProceduralScatterDirector` now does three extra passes:
- preferred cluster categories get a direct top-up pass
- preferred structure categories get a direct top-up pass
- preferred spawn categories get a direct top-up pass
- Preferred categories also get extra preview rescue:
- lower heat threshold in fallback preview
- higher density in fallback preview

### Concrete Fix
- A real fauna bug was found and fixed.
- `Metallic Hadal` was missing from the passive fauna rule list.
- Because of that, `The Black Spine` under `IndustrialService` could collapse to `spawn = 0`.
- `WorldProceduralProxyAuthoring` was updated so passive fauna anchors now allow `biome.family.metallic_hadal`.
- After rebuild, `The Black Spine` no longer drops to zero fauna in the biome content report.

### Verified In Unity
- `Build Procedural Fill Foundations`: passed
- `Rebuild World Runtime Stack`: passed
- `Generate Procedural Water Pattern Report`: passed
- `Generate Procedural Matrix Biome Content Report`: passed
- `Validate MapMagic World Stack`: passed
- console: `0 errors / 0 warnings`
- `PROCEDURAL_WATER_PATTERN_REPORT.md` still stays at `9 / 9 PASS`

### Honest Result
- Fertile, reef, sediment, brine, volcanic, rift and abyss waters now show clearer biome-to-biome differences in the content report.
- The concrete fauna failure for `IndustrialService -> The Black Spine` is fixed.
- One honest weak spot remains:
- `IndustrialService` representative biomes still lean too similarly into `Service Scar / Debris Field / Passive Spawn`
- so service-water biomes are healthier now, but still not distinctive enough yet.

## 2026-03-31 Service Water Divergence Pass

### What Was Changed
- Service-water biomes now push their preferred large-object identity earlier in the rescue pipeline instead of trying to squeeze in after generic pattern fill is already done.
- Exact preferred structure categories in `IndustrialService` and `BrineToxic` also get extra score pressure.
- In `IndustrialService` the primary large-object role of the biome now gets more room:
- more room for natural landmark when the biome wants arches/spires
- more room for cave-read when the biome wants entrances/depth reads
- less room for generic tech-fragment takeover in those cases

### Honest Effect
- `The Black Spine` inside `IndustrialService` now stops reading like just another service-scar field:
- dominant large object became `Cave Entrance Marker`
- structure mix moved toward `natural 1 | tech 4 | cave 5`
- `BrineToxic` also reads cleaner now:
- `Hydrothermal Spires` top large object is now `Landmark Spire`
- `Tectonic Shards` top/dominant large object is now `Cave Entrance Marker`
- `9 / 9` water reports remain `PASS`
- console remains `0 errors / 0 warnings`

### Honest Remaining Weak Spot
- `IndustrialService` is better, but `The Fluid Seam` and `Hydrothermal Spires` are still too close.
- They now carry more natural landmark weight, but both still often surface `Service Scar` as the top large object.
- Next real move there should be:
- split service-water biomes harder by cluster identity and route/power vs brine/vent identity
- not another generic world-wide retune

## 2026-03-31 Biome-Specific Divergence Pass N

### What Was Changed
- Narrowed the heavy-service overrides so they target the actual special biomes instead of whole biome families.
- The intent is now:
- `The Fluid Seam` = route/power traces, ridge reading, exposed shelf runs
- `Hydrothermal Spires` = hot vents, hazard bowls, bright spire silhouettes
- `The Black Spine` = vertical fissures, deep metal mass, cave-read pressure
- Started the same kind of place-specific split for softer waters:
- `Archipelago Needles` = safe resets and bright navigational spires
- `Mesa Plateaus` = bowls, resets, and shallow loop memory
- `White Alabaster Pools` = mineral extraction and reflective resource basins
- `Fossil Gallows` = nest-heavy fossil reef with riskier reef corridors

### Honest Status
- The biome profile assets on disk now carry these new preferred object-category orders.
- The actual Unity editor stayed alive, and `BiomeMatrixBootstrap` still rebuilt successfully according to `Editor.log`.
- But the Unity MCP bridge orphaned the session, and after that the editor stopped picking up newly added external helper files cleanly.
- So the latest service-water narrowing and soft-water split are in code and in the biome authoring logic, but the fresh report pass for those last changes is still waiting on a clean editor reconnect / refresh.

### Why This Still Matters
- This is not abstract tuning.
- It is the step that makes places inside the same water type differ by what the player actually sees:
- safer pockets vs resource basins
- route-power scars vs hydrothermal danger bowls
- vertical cave reads vs bright surface spires

## 2026-03-31 Save Integration Reality Check

### What Was Confirmed
- Several items from the blocker list are already present in the project and should no longer be treated as missing:
- `00_BOOTSTRAP.unity` exists
- `01_MAIN_MENU.unity` exists
- sandbox scenes exist
- `ConstructionManager` already implements save/load
- `WorldStateManager` already saves destroyed resource nodes
- `BeaconNetworkSystem` already implements save/load
- pause menu already has a real manual save path through `SaveManager.SaveGameAsync`

### What Was Actually Missing
- The real save-system gap was version migration.
- `SaveManager` only warned on version mismatch and then tried to load raw data as-is.
- That meant older or partially empty saves had no central repair step.

### What Was Added
- New file:
- `Assets/_Project/Scripts/SaveDataMigration.cs`
- It now repairs and upgrades old saves before the data is applied to gameplay systems:
- restores missing arrays and capacities
- clamps invalid counters
- creates missing tool-state dictionaries
- restores legacy module integrity for old base-module saves
- repairs beacon ids, labels, ranges, and sequence values
- upgrades the save version to the current format
- `SaveManager.LoadGameAsync` now runs this migration step and logs what was repaired.

### Honest Status
- This is a real product fix, not report polishing.
- It directly reduces the chance that older saves partially load into broken world state.
- Unity MCP session was down again during the final verification pass, so the new migration path is code-reviewed and wired, but not yet revalidated through a fresh live editor compile round.

## 2026-03-31 Biome Content Pass C

### What Was Changed
- Increased the weight of each biome's exact preferred object categories inside `WorldProceduralScatterDirector`.
- This especially affects:
- soft waters (`FertileShallows`, `ReefNavigation`)
- service-heavy waters (`IndustrialService`, `BrineToxic`)
- reference resource water (`SedimentResources`)
- Lowered preview thresholds and raised preview density for the first preferred categories so the scene can show the biome's signature earlier.
- Added hard place-identity overrides in `BiomeMatrixBootstrapAuthoring` for:
- `The Fluid Seam`
- `Hydrothermal Spires`
- `The Black Spine`
- `Archipelago Needles`
- `Mesa Plateaus`
- `White Alabaster Pools`
- `Fossil Gallows`
- `White Alabaster Pools` was shifted away from "yet another spire place" toward a stronger mineral / biological silhouette signature.

### Honest Status
- The code now gives specific places a stronger chance to show their own object mix instead of collapsing back into generic water noise.
- This is the right gameplay direction: places inside one water type should differ by what the player actually sees.
- Live Unity rebuild / report verification is still blocked by the missing active MCP editor session, so this pass is implemented but still waiting for a fresh in-editor proof round.
