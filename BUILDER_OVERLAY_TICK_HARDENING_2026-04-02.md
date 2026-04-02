# Builder Overlay Tick Hardening — 2026-04-02

## Контекст

`Assets/_Project/Scripts/UI/BuilderStatusOverlay.cs` держал native `Update()` и
регулярно опрашивал состояние даже тогда, когда строительный инструмент не был
активен.

Для проекта это плохой runtime-pattern:

- overlay не нужен большую часть времени;
- состояние видимости завязано на Builder Tool;
- hidden UI не должен жить в per-frame polling без причины.

## Что изменено

- `BuilderStatusOverlay` переведён с native `Update()` на `ITickable`.
- Добавлен `PlayerToolManager` reference path для отслеживания активного слота.
- Добавлена динамическая регистрация в `GameTickManager`:
  - overlay тикает только пока Builder активен;
  - overlay временно остаётся в тике, если runtime references ещё не дорезолвились.
- Добавлены подписки на:
  - `PlayerInventory.InventoryChanged`
  - `PlayerToolManager.ActiveSlotChanged`
  - `PlayerToolManager.ToolAssignmentsChanged`
- Добавлен безопасный refresh subscription path на случай, если ссылки
  дорезолвились уже после `OnEnable`.

## Что это даёт

- Скрытый builder overlay больше не участвует в постоянном per-frame polling.
- Возврат Builder Tool снова поднимает overlay в активный tick-path без ручного
  вмешательства.
- Изменения инвентаря и tool-slot состояния теперь быстрее будят overlay по
  событию, а не только через периодический polling.

## Что ещё не закрыто

- Пока Builder активен, overlay всё ещё опрашивает placement/snap/readiness по
  таймеру `refreshInterval`, потому что сам `PlayerBuilder` пока не публикует
  dedicated state-change events для этих состояний.
- Это уже лучше прежнего состояния, но финальный потолок здесь — отдельная
  event-driven шина от builder/runtime preview path.

## Ограничения проверки

Во время этого прохода Unity Editor не был подключён к MCP, поэтому:

- live compile check в Unity не подтверждён;
- console check не подтверждён;
- изменения прошли только статический code-review и diff-аудит.

Следующий логичный шаг:

1. live compile/console check в Unity;
2. продолжить UI/runtime perf-pass по `PDADataLogTab` и `SuitHUDV4CanvasOverlay`;
3. позже вынести builder state в события, чтобы снять даже active-state polling.
