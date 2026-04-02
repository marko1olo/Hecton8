# HUD Runtime Tick Hardening — 2026-04-02

## Контекст

Следующий слой после PDA/UI polling оказался в HUD/visor runtime:

- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs`

Оба класса реально требуют частого обновления в игре, но не обязаны делать это
через native `Update()` в play mode.

При этом оба файла работают с `[ExecuteAlways]`, поэтому editor preview нельзя
ломать грубой полной заменой `Update()`.

## Что изменено

### 1. `SuitHUDV4CanvasOverlay`

- Класс переведён на `ITickable` для play mode.
- Runtime refresh теперь идёт через `GameTickManager`.
- `Update()` оставлен только как:
  - editor preview path;
  - безопасный bootstrap для поздней регистрации, если `GameTickManager`
    ещё не поднялся в момент `OnEnable`.
- Добавлен safe unregister на `OnDisable`.

### 2. `SuitHUDScreenCompositor`

- Класс переведён на `ITickable` для play mode.
- `Update()` больше не делает play-mode refresh напрямую.
- Editor preview path сохранён.
- Добавлен safe unregister на `OnDisable`.

## Что это даёт

- HUD и compositor больше не сидят в постоянном native `Update()` в рантайме.
- Play-mode HUD pipeline стал ближе к общей архитектуре проекта:
  `GameTickManager` вместо разрозненных per-frame MonoBehaviour updates.
- Editor authoring и preview path при этом не ломаются.

## Что ещё не закрыто

- Это не снимает сам факт per-frame обновления HUD в игре, потому что:
  - глубина,
  - курс,
  - живые gauge-показатели,
  - compositing
  действительно должны обновляться часто.
- Но это снимает лишний native-update overhead и выравнивает runtime path.
- Следующий visor-tail теперь уже не здесь, а в:
  - `SuitHUDPresentationController`
  - `VisorHUDController`

## Ограничения проверки

Во время этого прохода Unity Editor не был подключён к MCP, поэтому:

- live compile check не подтверждён;
- console check не подтверждён;
- правки прошли статический self-review и diff-аудит.
