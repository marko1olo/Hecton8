# PDA UI Tick Hardening — 2026-04-02

## Контекст

В first-party PDA UI оставался лишний native `Update()` polling в нескольких
runtime-скриптах:

- `Assets/_Project/Scripts/UI/PDABarterTab.cs`
- `Assets/_Project/Scripts/UI/PDAConstructionTab.cs`
- `Assets/_Project/Scripts/UI/PDAShellChrome.cs`

Эти компоненты проверяли состояние каждый кадр даже тогда, когда PDA была
закрыта или соответствующая вкладка не была активной.

## Что изменено

### 1. `PDABarterTab`

- Убран `Update()`.
- Класс переведён на `ITickable`.
- Добавлена динамическая регистрация в `GameTickManager` только пока активна
  barter-вкладка.
- `ExchangeStateChanged` больше не форсит refresh для закрытой вкладки.
- На `PDAEvents.OnClosed` и на переключении на другую вкладку компонент
  гарантированно снимается с тика.

### 2. `PDAConstructionTab`

- Убран `Update()`.
- Класс переведён на `ITickable`.
- Добавлена динамическая регистрация только для активной construction-вкладки.
- `InventoryChanged` больше не перерисовывает закрытую вкладку.
- На `PDAEvents.OnClosed` и на уходе с construction-вкладки ticking отключается.

### 3. `PDAShellChrome`

- Убран `Update()`.
- Класс переведён на `ITickable`.
- Chrome теперь тикает только пока PDA открыта.
- Инвентарь и tool-события больше не вызывают обновление в закрытом состоянии.

## Что это даёт

- Закрытые PDA-вкладки больше не живут в per-frame polling path.
- Снижается количество лишних `Time.unscaledTime` gate-проверок в UI-runtime.
- Архитектура приведена ближе к проектному правилу `GameTickManager` вместо
  native `Update()` для gameplay/runtime логики.

## Ограничения проверки

Во время этого прохода Unity Editor не был подключён к MCP-сессии
(`no_unity_session`), поэтому:

- editor-side compile check не был подтверждён через Unity tools;
- perf-validator меню не был прогнан из редактора;
- правки прошли только статический self-review и diff-аудит.

Следующий шаг при доступном Unity Editor:

1. дождаться `ready_for_tools=true`;
2. прогнать compile/console check;
3. выполнить `Hecton/Validation/Validate Performance Hot Paths`;
4. продолжить perf-pass по `BuilderStatusOverlay` и следующему UI/runtime слою.
