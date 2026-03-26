# HUD V4 Tuning

## Latest Active Left-Block Knobs

The current left vitals block is no longer the failed slanted-bar version.
It is now a compact radial-gauge cluster on `HUD_V4_CanvasRoot`.

Object:
- `Suit_HUD_Canvas`

Component:
- `SuitHUDV4CanvasOverlay`

Relevant fields:
- `gaugeClusterOffset`
- `gaugeClusterSize`
- `gaugeColumnSpacing`
- `gaugeRingSize`
- `gaugeRingThickness`
- `gaugeIconSize`
- `gaugeValueOffsetY`
- `gaugeLabelOffsetY`

Symptom -> what to turn:
- gauges overlap each other:
  - increase `gaugeColumnSpacing`
  - if needed increase `gaugeClusterSize.x`
- gauges are too small:
  - increase `gaugeRingSize`
- rings are too thick / visually dirty:
  - decrease `gaugeRingThickness`
- number is off-center inside the ring:
  - tweak `gaugeValueOffsetY`
- label under ring collides or sits too low:
  - tweak `gaugeLabelOffsetY`
- whole left block sits too far into screen:
  - decrease `gaugeClusterOffset.x`
- whole left block sits too low/high:
  - tweak `gaugeClusterOffset.y`

Рабочий HUD: `--- UI ---/Suit_HUD_Canvas/HUD_V4_CanvasRoot`

Компонент:
- [SuitHUDV4CanvasOverlay.cs](C:/hades/Hecton8/Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs)

## Что крутить в инспекторе

Объект: `Suit_HUD_Canvas`

Основное:
- `overallScale`: общий масштаб всего HUD
- `chromeAlpha`: сила верхней/нижней/боковой вуали

Позиции блоков:
- `headerOffset`: верхний заголовок
- `telemetryOffset`: правый depth/temp/pressure блок
- `telemetrySize`: размер правого блока
- `gaugeClusterOffset`: левый vitals-блок
- `gaugeClusterSize`: размер левого vitals-блока
- `statusOffset`: нижняя центральная строка
- `reticleOffset`: центральный прицел

Новый bar-блок:
- `gaugeRowSpacing`: вертикальный шаг между `OXYGEN / HEALTH / ENERGY`
- `gaugeBarWidth`: длина slanted bar
- `gaugeBarHeight`: толщина bar
- `gaugeIconSize`: размер иконки слева
- `gaugeValueOffsetX`: насколько число вынесено вправо от bar
- `gaugeLabelOffsetX`: тонкая подстройка label/sub относительно bar

## Практические симптомы

Если bars наезжают друг на друга:
- увеличить `gaugeRowSpacing`
- при необходимости увеличить `gaugeClusterSize.y`

Если bars слишком длинные и лезут в центр экрана:
- уменьшить `gaugeBarWidth`
- сдвинуть `gaugeClusterOffset.x` ближе к краю

Если числа оторваны от bars:
- уменьшить `gaugeValueOffsetX`

Если иконки слишком крупные или давят на текст:
- уменьшить `gaugeIconSize`
- при необходимости подвинуть `gaugeLabelOffsetX`

Если левый блок слишком шумный:
- уменьшить `chromeAlpha`
- уменьшить `overallScale`

## Текущая логика метрик

Левый блок использует только реальные данные:
- `OXYGEN` -> `HectonSurvivalSystem.Oxygen`
- `HEALTH` -> `HectonSurvivalSystem.Integrity`
- `ENERGY` -> `HectonSurvivalSystem.Energy`

Не используется:
- `Food`
- `Water`

Причина:
- в текущем gameplay-коде этих метрик нет
- рисовать их сейчас было бы фейком
