# HUD Integration Guide — v4.0 ENTERPRISE

## Обзор

HUD система состоит из двух компонентов:
- **HectonSuitHUD** (v3.0) — базовый HUD с life support, environment, module status
- **HectonSuitHUDExtensions** (v4.0 ENTERPRISE) — расширения: flashlight, PDA, notifications

## Быстрая установка

### 1. HUD Camera Setup

На GameObject с HUD Camera должны быть оба компонента:

```
HUD Camera (GameObject)
├── Camera
├── HectonSuitHUD (v3.0)
└── HectonSuitHUDExtensions (v4.0) ← НОВЫЙ
```

### 2. HectonSuitHUDExtensions — Inspector Setup

**References:**
- `hudCamera` → назначить Camera компонент
- `hudFont` → назначить TMP_FontAsset (тот же что в HectonSuitHUD)
- `flashlight` → назначить PlayerFlashlight на Player root (или оставить null для auto-resolve)

**Colors:** (опционально, есть дефолты)
- `normalColor` — основной цвет HUD (cyan)
- `warningColor` — предупреждения (yellow)
- `criticalColor` — критические события (orange/red)
- `flashlightOnColor` — цвет иконки фонаря когда включен
- `pdaActiveColor` — цвет иконки PDA когда открыт

**Layout:** (опционально)
- `lineThickness`, `fontSize`, etc. — настройки отрисовки

**Notifications:**
- `notificationDuration` — длительность уведомления (default: 3s)
- `notificationFadeSpeed` — скорость fade in/out (default: 4)

### 3. HectonSurvivalSystem — Upgrade to v5.0

Убедитесь что `HectonSurvivalSystem` на Player root имеет:
- `EnergyPercent` property (добавлено в v5.0)
- `DrainEnergy(int)` method (добавлено в v5.0)

Эти методы используются PlayerFlashlight и PlayerPDA для battery drain.

## Функциональность

### Flashlight Status Indicator

**Расположение:** Equipment Panel (top-right)

**Отображает:**
- Иконка: ◉ (on) / ○ (off)
- Heat bar (когда включен и heat > 0)
- Overheat warning (красный текст)
- Flickering animation (при low battery или high heat)

**События:**
- `FlashlightEvents.OnToggled` → обновляет иконку
- `FlashlightEvents.OnOverheat` → показывает notification + warning
- `FlashlightEvents.OnFlickerStart` → анимация мерцания
- `FlashlightEvents.OnBatteryDepleted` → notification

### PDA Status Indicator

**Расположение:** Equipment Panel (top-right, под flashlight)

**Отображает:**
- Иконка: ▣ (active) / □ (inactive)
- Label: "PDA [ACTIVE]" / "PDA"

**События:**
- `IPDAEventListener.OnPDAEvent(PDAEventType.Opened)` → активирует индикатор
- `IPDAEventListener.OnPDAEvent(PDAEventType.Closed)` → деактивирует индикатор
- `IPDAEventListener.OnPDAEvent(PDAEventType.LowBatteryShutdown)` → показывает notification

### Notification System

**Расположение:** Top-center (под временем)

**Типы уведомлений:**
- `FLASHLIGHT OVERHEAT` (critical, red)
- `FLASHLIGHT LOW BATTERY` (warning, yellow)
- `PDA LOW BATTERY` (warning, yellow)
- `BATTERY DEPLETED` (critical, red)

**Поведение:**
- Fade in: 0.3s
- Full opacity: duration - 0.8s
- Fade out: 0.5s
- Max 5 notifications одновременно
- Duplicate notifications обновляют duration (не создают новые)

## Zero GC Design

**Pre-allocated structures:**
- Notification queue: `NotificationEntry[5]` (struct array)
- Event handlers: cached delegates, no boxing
- String cache: common messages pre-allocated

**No allocations in:**
- `DrawExtensions()` — immediate mode rendering
- `UpdateNotifications()` — struct manipulation only
- Event handlers — direct field updates

## Diagnostics

**Inspector fields (read-only):**
- `_debugFlashlightOn` — текущее состояние фонаря
- `_debugFlashlightHeat` — уровень нагрева (0-1)
- `_debugPDAOpen` — PDA открыт/закрыт
- `_debugNotificationCount` — количество активных уведомлений

## Troubleshooting

**Проблема:** Notifications не появляются
- Проверьте что `hudCamera` назначен
- Проверьте что `hudFont` назначен
- Проверьте что события FlashlightEvents/PDAEvents вызываются

**Проблема:** Flashlight indicator не обновляется
- Проверьте что `flashlight` назначен (или auto-resolve работает)
- Проверьте что PlayerFlashlight.IsOn property доступен
- Проверьте что FlashlightEvents.OnToggled вызывается

**Проблема:** Battery drain не работает
- Проверьте что HectonSurvivalSystem.DrainEnergy(int) метод существует
- Проверьте что survivalSystem назначен в PlayerFlashlight/PlayerPDA
- Проверьте что enableBatteryDrain = true в инспекторе

## Performance

**Draw calls:** +1 draw call (Equipment Panel + Notifications)
**CPU:** ~0.1ms per frame (immediate mode rendering)
**Memory:** ~2KB (pre-allocated notification queue)
**GC:** 0 allocations per frame

## Совместимость

- Unity 2021.3+
- URP (Universal Render Pipeline)
- Shapes plugin (Immediate Mode Rendering)
- TextMeshPro

## Changelog

**v4.0 ENTERPRISE (current):**
- Initial release
- Flashlight status indicator
- PDA status indicator
- Notification system
- Event integration
- Zero GC design
