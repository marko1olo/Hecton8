# HECTON-8 — Shell / UI Workstream

Дата: 2026-04-13  
Статус: PENDING VERIFICATION

## Что закрывает этот фронт

- Main Menu
- Pause Menu
- Settings shell
- Input rebinding UI
- Option persistence
- Save/load UX

## Почему это один из главных фронтов

Сейчас shell существует, но выглядит как production foundation, а не как законченный пользовательский слой.  
Отдельного owner'а для общего persistence настроек не видно. Есть только фрагменты.

## Owner files

- `Assets/_Project/Scripts/MainMenuController.cs`
- `Assets/_Project/Scripts/SaveSlotUI.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/PauseMenuHost.cs`
- `Assets/_Project/Scripts/UI/PauseControlsPanel.cs`
- `Assets/_Project/Scripts/UI/PDAControlsRebindUI.cs`
- `Assets/_Project/Scripts/Input/RebindingManager.cs`
- `Assets/_Project/Scripts/Input/InputManager.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`

## Основные задачи

### Front A. Main menu flow

- Добить `MainMenuController`.
- Убрать пустые или тупиковые состояния.
- Довести load/new game flow до одного понятного сценария.
- Проверить возвраты, cancel-paths, фокус и default selection.

### Front B. Pause shell

- Довести `PauseMenuController` и `PauseMenuHost`.
- Проверить секции `Main / Saves / Help / Settings`.
- Исправить default focus и возврат из секций.
- Проверить path `pause -> save/load -> return`.

### Front C. Rebinding UX

- Добить `PauseControlsPanel`.
- Добить `PDAControlsRebindUI`.
- Проверить reset/save/apply/cancel.
- Проверить, что строки rebinding не разваливаются при пустых или missing bindings.

### Front D. Options persistence

- Вынести отдельного owner'а для настроек, если его реально нет.
- Сохранение не только input overrides, но и user options.
- Зафиксировать contract: какие настройки живут, где хранятся, кто их читает.

### Front E. Save/load user trust

- Проверить сообщения об ошибках.
- Проверить поведение при битом сейве или пустом слоте.
- Проверить согласованность с `SaveManager`.

## Do-Not-Touch Scope

- Не лезть в narrative systems.
- Не лезть в world bootstrap.
- Не править progression data.
- Не менять save backend contract без отдельного анализа зависимостей.

## Как дробить по агентам

Агент 1:
- `MainMenuController.cs`
- `SaveSlotUI.cs`
- Задача: menu flow и save/load UX.

Агент 2:
- `PauseMenuController.cs`
- `PauseMenuHost.cs`
- Задача: pause shell и section flow.

Агент 3:
- `PauseControlsPanel.cs`
- Задача: rebinding UI в pause.

Агент 4:
- `PDAControlsRebindUI.cs`
- Задача: rebinding UI в PDA.

Агент 5:
- новый owner под option persistence
- минимальные точки входа в existing UI
- Задача: общий persistence слой настроек.

## Expected Result

- Main menu не ведёт в тупики.
- Pause стабилен.
- Rebinding не выглядит как полузаглушка.
- Настройки реально сохраняются.
- Пользовательский shell перестаёт быть weak point.

## Exit Criteria

- Нет пустых panel states.
- Все back/cancel paths закрыты.
- Input overrides сохраняются и грузятся.
- Есть единый owner настроек.
- Проверен базовый сценарий: main menu -> world -> pause -> settings -> save/load -> return.
