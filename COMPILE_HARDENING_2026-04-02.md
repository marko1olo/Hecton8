# Compile Hardening - 2026-04-02

## Что исправлено

### 1. `FlashlightTool` после перевода на query-cache держал битые ссылки на `hit`

Файл:
- `Assets/_Project/Scripts/FlashlightTool.cs`

Проблема:
- Логика уже использовала `QueryResult qResult` и `finalHit`.
- Ниже по методу остались старые обращения к локальной переменной `hit`.
- Это ломало сборку (`CS0103`) и делало ветку рекомендаций лампы неконсистентной.

Решение:
- Все поздние проверки дистанции переведены на `finalHit.distance`.

### 2. `ScanEvents` и scan-runtime цепляли не тот `float3`

Файлы:
- `Assets/_Project/Scripts/ScanEvents.cs`
- `Assets/_Project/Scripts/ScannerTool.cs`
- `Assets/_Project/Scripts/HectonScanMarkerSystem.cs`

Проблема:
- В `Hecton8.Gameplay` существовал внутренний helper-тип `float3` из `HectonHazardManager`.
- Публичный event bus `ScanEvents` использовал неqualified `float3`, из-за чего компилятор видел менее доступный тип в публичных сигнатурах (`CS0052`).

Решение:
- Контракт scan-системы переведён на явный `Unity.Mathematics.float3`.
- То же сделано в основных runtime-потребителях scan-событий.

### 3. Остальные gameplay-системы тоже утыкались в namespace-level `float3`

Файлы:
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`

Проблема:
- `PlayerInteraction` ловил неоднозначность между `Hecton8.Gameplay.float3` и `Unity.Mathematics.float3` (`CS0104`).
- `HectonPlayerMovement` создавал вектор течения через конструктор у неправильного типа (`CS1729`).

Решение:
- Проблемные участки переведены на явный `Unity.Mathematics.float3`.

### 4. Источник конфликта убран локально

Файл:
- `Assets/_Project/Scripts/Gameplay/HectonHazardManager.cs`

Проблема:
- Внутренний helper `float3` загрязнял всё пространство имён `Hecton8.Gameplay`.

Решение:
- Helper переименован в `HazardFloat3`.
- Локальная hazard-система сохранила поведение, но перестала ломать соседние gameplay-модули.

### 5. Поздний compile-blocker в `HectonSuitHUD` оказался дубликатом локального блока

Файл:
- `Assets/_Project/Scripts/HectonSuitHUD.cs`

Проблема:
- В локальном hazard-блоке HUD один и тот же набор полей был вставлен дважды.
- Это ломало новый compile-pass ошибками `CS0102` и мешало верификации уже исправленных систем.

Решение:
- Удалён только дублирующий блок полей без изменения остальной логики `HectonSuitHUD`.

## Что это значит простыми словами

- Сборка снова проходит без ошибок.
- Сканер, фонарь, взаимодействие и движение игрока больше не висят на хрупком конфликте одноимённых типов.
- Следующие правки в gameplay-слое будут намного реже ловить “странные” ошибки вокруг `float3`.

## Что проверено

- После серии правок Unity завершил compile-pass.
- В консоли больше нет `Error`.
- Остались только `Warning`, в основном:
  - third-party устаревшие API
  - debug-поля без чтения
  - editor-side технический шум

## Что осталось проблемой

- `Warning`-слой всё ещё шумный, особенно в third-party пакетах и части editor tooling.
- Это уже не compile-blocker, а отдельный hygiene-pass.
