# SUIT HUD SCREEN COMPOSITOR HARDENING — 2026-04-02

## Что было не так

`Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs` в play mode продолжал
крутиться каждый кадр, даже когда:

- все ссылки уже найдены
- overlay уже создан
- текстура уже привязана
- делать больше ничего не нужно

Проще говоря: этот слой HUD продолжал “проверять сам себя” без пользы.

## Что сделано

- добавлен `pending refresh` флаг
- runtime tick теперь сам отключается, когда compositor уже стабильно настроен
- если texture/alpha меняются через API, compositor снова просыпается
- добавлены методы:
  - `SetSharedProjectionTexture`
  - `SetOverlayAlpha`
- повторный поиск overlay-объекта по имени теперь не делается лишний раз, если кэш уже валиден

## Что это даёт

- меньше пустой работы каждый кадр
- compositor теперь работает как “настроился и спит”, а не как вечный polling-цикл
- меньше шансов получить лишнюю нагрузку от visor/HUD слоя

## Что проверено

- Unity compile после правки без first-party `Error`
- короткий `play -> stop` smoke после правки завершился с пустой консолью
- в консоли остались только прежние third-party warnings
