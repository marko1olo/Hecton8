# Flow Field Visualizer — Визуализатор течений

## Обзор

**FlowFieldVisualizer** — это **enterprise-grade** инструмент для Unity Editor, который визуализирует векторное поле подводных течений в HECTON-8. Полезен для дизайнеров и разработчиков при настройке физики воды, балансе геймплея и отладке.

## Возможности

- **Grid-based sampling**: Эффективный сэмплинг течений в регулярной сетке
- **Множественные стили визуализации**: Arrows, Lines, Cones, Dots + Particle Effects
- **Цветовая кодировка**: Сила течения кодируется цветом (синий = слабое, красный = сильное) + HDR support
- **Фильтрация слабых течений**: Опциональная фильтрация для лучшей читаемости
- **Числовые лейблы**: Отображение силы течения в м/с
- **Множественные источники**: Визуализация глобального phantom течения + локальных CurrentVolume
- **Профили настроек**: Сохранение и переиспользуемые конфигурации через ScriptableObject
- **Автоматические обновления**: Реагирует на изменения настроек в HectonFluidEngine
- **LOD-friendly**: Только при выделении объекта (OnDrawGizmosSelected)
- **Enterprise features**: Async calculation, Burst sampling, Job System, error handling, performance monitoring

## Настройка визуализации

#### Grid Settings
- **Area Size**: Размер области визуализации (метры)
- **Grid Resolution**: Количество точек сэмплинга по X и Z
- **Sample Height**: Высота над водой для сэмплинга

#### Performance (Enterprise)
- **Max Grid Resolution**: Максимальное разрешение для предотвращения зависаний
- **Async Threshold**: Порог для асинхронного расчёта (1000+ точек)
- **Async Timeout**: Таймаут асинхронных операций (сек)
- **Use Burst Sampling**: Burst compilation для расчётов (быстрее)
- **Use Job System**: Параллельный сэмплинг через Job System

#### Visualization
- **Arrow Style**: Стиль стрелок (Arrows/Lines/Cones/Dots)
- **Show Force Labels**: Показывать числовые значения силы
- **Label Font Size**: Размер шрифта лейблов (8-24)
- **Cull Weak Flows**: Фильтровать слабые течения
- **Min Flow Strength**: Минимальная сила для отображения (м/с)

#### Advanced Visualization (Enterprise)
- **Use HDR Colors**: HDR цвета для лучшей видимости (красный x3 яркости)
- **Animate In Editor**: Плавная анимация в editor для preview
- **Animation Speed**: Скорость анимации (0.1-5x)
- **Use Particle Effects**: Particle systems для сильных течений
- **Particle Prefab**: Prefab для эффектов (автоматический пул)

#### Current Sources
- **Show Global Current**: Визуализировать phantom течение из HectonFluidEngine
- **Show Local Currents**: Визуализировать CurrentVolume объекты
- **Only Selected Volumes**: Ограничить только выбранными CurrentVolume

## Enterprise Features

### Performance Optimization
- **Async Calculation**: Большие grid'ы (>1000 точек) рассчитываются в фоне с progress bar
- **Burst Compilation**: SIMD-оптимизированные расчёты через Unity Burst
- **Job System**: Параллельный сэмплинг через Unity Jobs
- **Memory Pooling**: Object pooling для particle effects
- **Lazy Recalculation**: Пересчёт только при изменении настроек

### Advanced Rendering
- **HDR Colors**: Яркие цвета для лучшей видимости в сложных сценах
- **Particle Effects**: Динамические эффекты для сильных течений с автоматическим пулом
- **Animation Preview**: Time-based анимация в editor для тестирования
- **Multiple Styles**: 4 стиля визуализации + particle overlay

### Error Handling & Validation
- **Robust error handling**: Graceful degradation при ошибках сэмплинга
- **Configuration validation**: Автоматическая коррекция недопустимых значений
- **Dependency checking**: Проверка наличия HectonFluidEngine
- **Performance monitoring**: Защита от слишком больших расчётов

### Configuration Management
- **Profile system**: Переиспользуемые конфигурации с полным набором настроек
- **Version compatibility**: Поддержка обратной совместимости
- **Bidirectional sync**: Profile ↔ Visualizer синхронизация

## API Reference

### FlowFieldVisualizer

```csharp
// Создание и настройка
var visualizer = FlowFieldVisualizer.Instance;
visualizer.AreaSize = new Vector2(100f, 100f);
visualizer.UseHDRColors = true;
visualizer.UseParticleEffects = true;

// Enterprise настройки
visualizer.UseBurstSampling = true;
visualizer.UseJobSystem = true;
visualizer.AsyncThreshold = 2000;

// Принудительный пересчёт
visualizer.Recalculate();
```

### FlowFieldProfile

```csharp
// Создание профиля
var profile = ScriptableObject.CreateInstance<FlowFieldProfile>();

// Применение к визуализатору
profile.ApplyTo(visualizer);

// Захват текущих настроек
profile.CaptureFrom(visualizer);
```

## Тестирование

Проект включает comprehensive unit tests в `FlowFieldVisualizerTests.cs`:
- Валидация настроек и error handling
- Производительность и async calculation
- Интеграция с профилями и HECTON-8 системами
- Memory management и pooling

## Советы по использованию

1. **Для больших областей**: Включите async calculation и настройте threshold
2. **Для производительности**: Используйте Burst + Job System для больших grid'ов
3. **Для детальной настройки**: Включите HDR colors и particle effects
4. **Для анимации**: Используйте Animate In Editor для preview динамики
5. **Для отладки**: Комбинируйте multiple styles для разных аспектов

## Технические детали

- **Namespace**: `Hecton8.Physics`
- **Dependencies**: `HectonFluidEngine`, `CurrentManager`, `CurrentVolume`
- **Editor-only features**: `OnDrawGizmosSelected`, custom Inspector
- **Threading**: Main thread + async background calculation
- **Testing**: NUnit framework для enterprise-quality assurance
- **Performance**: O(N²) complexity с enterprise оптимизациями

## Известные ограничения

- Визуализация только в Editor (Gizmos)
- Particle effects требуют prefab с ParticleSystem
- Burst требует Unity 2019.3+ с Burst package
- Job System требует Unity 2019.3+ с Jobs package

## Roadmap (Enterprise)

- **GPU Acceleration**: Compute shaders для massive grid'ов
- **Real-time Runtime**: Runtime визуализация для игроков
- **Network Sync**: Multiplayer синхронизация настроек
- **Analytics Integration**: Telemetry и performance metrics
- **Custom Shaders**: Advanced rendering с transparency и glow
- **Interactive Editing**: Click-to-modify flow vectors
- **Export System**: CSV/JSON export для анализа и ML
- **VR Support**: VR-совместимая визуализация

---

*"Enterprise-grade visualization for AAA underwater physics"* 🚀
- **Dependencies**: `HectonFluidEngine`, `CurrentManager`, `CurrentVolume`
- **Editor-only features**: `OnDrawGizmosSelected`, custom Inspector
- **Threading**: Main thread only (Gizmos ограничения)

## Известные ограничения

- Визуализация только в Editor (Gizmos)
- Не показывает анимированные течения в реальном времени
- Ограниченная производительность при высоком Grid Resolution
- Требует активного HectonFluidEngine в сцене