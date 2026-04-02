# SUIT HUD EXTENSIONS RUNTIME HARDENING — 2026-04-02

## Что изменено

- `Assets/_Project/Scripts/HectonSuitHUDExtensions.cs`
  - runtime `LateUpdate()` заменён на `ITickable`
  - `Update()` оставлен только для edit-mode preview / editor refresh path
  - `StartCoroutine(ClearOverheatFlag)` удалён
  - `StartCoroutine(ClearFlickerFlag)` удалён
  - временные флаги фонаря переведены на timer-based state:
    - `_overheatFlagTimer`
    - `_flickerFlagTimer`
  - добавлена явная регистрация/отписка в `GameTickManager`

## Что это значит простыми словами

Legacy HUD extension больше не создаёт coroutine/`WaitForSeconds` мусор ради двух коротких reset-таймеров.

Теперь этот слой:

- обновляет heat / notifications / diagnostic state через проектный tick
- держит overheat/flicker windows обычными float-таймерами
- остаётся визуально эквивалентным старому поведению

## Что это даёт

- меньше GC-шума в HUD runtime
- более предсказуемый lifecycle при enable/disable
- меньше расхождения между legacy HUD и уже переведёнными modern HUD слоями

## Что проверено

- Unity compile после правки без `Error`
- в консоли остались только third-party warnings
- короткий `play -> stop` smoke после правки завершился с пустой консолью
