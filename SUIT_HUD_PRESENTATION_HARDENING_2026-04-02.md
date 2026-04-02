# SUIT HUD PRESENTATION HARDENING — 2026-04-02

## Что изменено

- `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`
  - runtime `LateUpdate()` убран из play-mode path
  - контроллер переведён на `ITickable` через `GameTickManager`
  - `Update()` оставлен только для editor preview / edit-time применения
  - tick теперь активен только пока:
    - есть pending apply
    - или ещё не дорезолвлены зависимости
  - добавлены явные runtime-setter'ы:
    - `SetPresentationMode`
    - `SetSharedProjectionTexture`
    - `SetFallbackProfile`
  - убраны лишние scene lookups:
    - scan `SuitHUDV4CanvasOverlay` теперь делается только когда реально нужны overlay refs
    - кэшируются `Suit_HUD_Canvas` и дочерний `HUD_RT_Compositor`

## Что это значит простыми словами

Раньше presentation controller жил в `LateUpdate()` постоянно, даже когда:

- режим уже применён
- ссылки уже найдены
- менять больше нечего

Теперь play-mode path ведёт себя как нормальный runtime coordinator:

- просыпается, когда надо что-то применить
- засыпает, когда состояние стабильно
- edit-mode preview при этом не потерян

## Что это даёт

- меньше лишнего per-frame polling в visor/HUD orchestration
- меньше scene search шума
- чище контракт для runtime-смены режима презентации
- меньше риска, что presentation-layer будет жечь кадры просто потому что компонент существует

## Что проверено

- Unity refresh/compile после правки завершился без `Error`
- после compile в консоли остались только third-party warnings
- короткий `play -> stop` smoke после правки завершился с пустой консолью

## Замечание по MCP

Во время самого play MCP-пинг по-прежнему может кратковременно “усыпать” `editor_state` / `read_console`.

Но после возврата редактора в idle:

- сессия восстановилась
- `editor/state` стал `ready_for_tools = true`
- консоль осталась пустой

То есть для этого прохода важен именно итог:

- compile чистый
- play-stop чистый
