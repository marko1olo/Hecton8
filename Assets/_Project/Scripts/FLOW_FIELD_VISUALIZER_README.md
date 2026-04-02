# Flow Field Visualizer

## Назначение

`FlowFieldVisualizer` — редакторский gizmo-инструмент для просмотра поля течений в сцене.
Он рисует выборку по сетке поверх:

- глобального течения из `HectonFluidEngine` / `CurrentManager`
- локальных authored-объёмов `CurrentVolume`

Инструмент нужен для настройки воды, проверки локальных current volumes и быстрой
диагностики направления/силы потока прямо в Scene View.

## Что умеет

- выборка по прямоугольной сетке `AreaSize` x `GridResolution`
- стили отрисовки `Arrows`, `Lines`, `Cones`, `Dots`
- цветовая кодировка силы потока
- фильтрация слабых значений через `CullWeakFlows` + `MinFlowStrength`
- подписи силы в м/с через `ShowForceLabels`
- опциональный async/job-пересчёт для больших сеток
- профили настроек через `FlowFieldProfile`

## Как работает

1. Компонент висит в сцене и рисует gizmos только в `OnDrawGizmosSelected`.
2. При изменении настроек визуализатор помечает кэш как dirty.
3. При следующем draw он пересчитывает grid-позиции и flow vectors.
4. Для крупных сеток может запускать job и завершать её через editor update.
5. Job/Burst-путь уважает флаги источников:
   - при `ShowGlobalCurrent = false` глобальный phantom current не подмешивается;
   - при отсутствии `HectonFluidEngine` локальные `CurrentVolume` всё равно могут считаться через job-путь.

## Ключевые настройки

- `AreaSize`: размер области выборки в метрах.
- `GridResolution`: плотность выборки по X/Z.
- `SampleHeight`: Y-offset относительно объекта визуализатора.
- `MaxGridResolution`: жёсткий clamp против слишком тяжёлых сеток.
- `AsyncThreshold`: с какого числа точек имеет смысл job-путь.
- `AsyncTimeout`: после какого времени job принудительно завершается на main thread.
- `ShowGlobalCurrent`: учитывать глобальное phantom-течение.
- `ShowLocalCurrents`: учитывать `CurrentVolume`.
- `OnlySelectedVolumes`: ограничить расчёт списком `SelectedVolumes`.

## Профили

`FlowFieldProfile` хранит сериализуемый набор параметров визуализатора.

Типовой сценарий:

```csharp
FlowFieldProfile profile = ScriptableObject.CreateInstance<FlowFieldProfile>();
profile.CaptureFrom(visualizer);
profile.ApplyTo(visualizer);
```

Через editor menu можно создать asset профиля:

- `Hecton/Tools/Create Flow Field Profile`

Меню использует уникальный asset path и не перезаписывает существующий профиль.

## Ограничения и замечания

- Инструмент редакторский; он не предназначен для runtime HUD/FX.
- `UseParticleEffects` годится только для визуального preview в редакторе и может
  быстро заспамить сцену при плотной сетке.
- Если `HectonFluidEngine` отсутствует, визуализатор всё равно продолжит работать
  по локальным `CurrentVolume`.
- Preview-particles считаются временными editor-ресурсами и полностью очищаются
  при отключении компонента, чтобы не оставлять hidden objects в сцене.
- Высокие разрешения сетки всё равно дорогие: job-путь убирает фриз, но не делает
  расчёт бесплатным.

## Связанные файлы

- `Assets/_Project/Scripts/FlowFieldVisualizer.cs`
- `Assets/_Project/Scripts/FlowFieldProfile.cs`
- `Assets/_Project/Scripts/CurrentVolume.cs`
- `Assets/_Project/Scripts/Editor/FlowFieldVisualizerEditor.cs`
- `Assets/_Project/Scripts/Editor/FlowFieldVisualizerTests.cs`
