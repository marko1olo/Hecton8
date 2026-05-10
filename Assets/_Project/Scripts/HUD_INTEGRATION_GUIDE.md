# HUD Integration Guide — v4.0 ENTERPRISE

## Obzor

HUD sistema sostoit iz dvuh komponentov:
- **HectonSuitHUD** (v3.0) — bazovyy HUD s life support, environment, module status
- **HectonSuitHUDExtensions** (v4.0 ENTERPRISE) — rasshireniya: flashlight, PDA, notifications

## Bystraya ustanovka

### 1. HUD Camera Setup

Na GameObject s HUD Camera dolzhny byt oba komponenta:

```
HUD Camera (GameObject)
├── Camera
├── HectonSuitHUD (v3.0)
└── HectonSuitHUDExtensions (v4.0) ← NOVYY
```

### 2. HectonSuitHUDExtensions — Inspector Setup

**References:**
- `hudCamera` → naznachit Camera komponent
- `hudFont` → naznachit TMP_FontAsset (tot zhe chto v HectonSuitHUD)
- `flashlight` → naznachit PlayerFlashlight na Player root (ili ostavit null dlya auto-resolve)

**Colors:** (optsionalno, est defolty)
- `normalColor` — osnovnoy tsvet HUD (cyan)
- `warningColor` — preduprezhdeniya (yellow)
- `criticalColor` — kriticheskie sobytiya (orange/red)
- `flashlightOnColor` — tsvet ikonki fonarya kogda vklyuchen
- `pdaActiveColor` — tsvet ikonki PDA kogda otkryt

**Layout:** (optsionalno)
- `lineThickness`, `fontSize`, etc. — nastroyki otrisovki

**Notifications:**
- `notificationDuration` — dlitelnost uvedomleniya (default: 3s)
- `notificationFadeSpeed` — skorost fade in/out (default: 4)

### 3. HectonSurvivalSystem — Upgrade to v5.0

Ubedites chto `HectonSurvivalSystem` na Player root imeet:
- `EnergyPercent` property (dobavleno v v5.0)
- `DrainEnergy(int)` method (dobavleno v v5.0)

Eti metody ispolzuyutsya PlayerFlashlight i PlayerPDA dlya battery drain.

## Funktsionalnost

### Flashlight Status Indicator

**Raspolozhenie:** Equipment Panel (top-right)

**Otobrazhaet:**
- Ikonka: ◉ (on) / ○ (off)
- Heat bar (kogda vklyuchen i heat > 0)
- Overheat warning (krasnyy tekst)
- Flickering animation (pri low battery ili high heat)

**Sobytiya:**
- `IFlashlightEventListener.OnFlashlightEvent(FlashlightEventType.Toggled)` → obnovlyaet ikonku
- `IFlashlightEventListener.OnFlashlightEvent(FlashlightEventType.Overheat)` → pokazyvaet notification + warning
- `IFlashlightEventListener.OnFlashlightEvent(FlashlightEventType.FlickerStart)` → animatsiya mertsaniya
- `IFlashlightEventListener.OnFlashlightEvent(FlashlightEventType.BatteryDepleted)` → notification

### PDA Status Indicator

**Raspolozhenie:** Equipment Panel (top-right, pod flashlight)

**Otobrazhaet:**
- Ikonka: ▣ (active) / □ (inactive)
- Label: "PDA [ACTIVE]" / "PDA"

**Sobytiya:**
- `IPDAEventListener.OnPDAEvent(PDAEventType.Opened)` → aktiviruet indikator
- `IPDAEventListener.OnPDAEvent(PDAEventType.Closed)` → deaktiviruet indikator
- `IPDAEventListener.OnPDAEvent(PDAEventType.LowBatteryShutdown)` → pokazyvaet notification

### Notification System

**Raspolozhenie:** Top-center (pod vremenem)

**Tipy uvedomleniy:**
- `FLASHLIGHT OVERHEAT` (critical, red)
- `FLASHLIGHT LOW BATTERY` (warning, yellow)
- `PDA LOW BATTERY` (warning, yellow)
- `BATTERY DEPLETED` (critical, red)

**Povedenie:**
- Fade in: 0.3s
- Full opacity: duration - 0.8s
- Fade out: 0.5s
- Max 5 notifications odnovremenno
- Duplicate notifications obnovlyayut duration (ne sozdayut novye)

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
- `_debugFlashlightOn` — tekuschee sostoyanie fonarya
- `_debugFlashlightHeat` — uroven nagreva (0-1)
- `_debugPDAOpen` — PDA otkryt/zakryt
- `_debugNotificationCount` — kolichestvo aktivnyh uvedomleniy

## Troubleshooting

**Problema:** Notifications ne poyavlyayutsya
- Proverte chto `hudCamera` naznachen
- Proverte chto `hudFont` naznachen
- Proverte chto flashlight listener zaregistrirovan cherez `FlashlightEvents.Register`, a PDAEvents vyzyvayutsya

**Problema:** Flashlight indicator ne obnovlyaetsya
- Proverte chto `flashlight` naznachen (ili auto-resolve rabotaet)
- Proverte chto PlayerFlashlight.IsOn property dostupen
- Proverte chto `FlashlightEventType.Toggled` dohodit do `IFlashlightEventListener.OnFlashlightEvent`

**Problema:** Battery drain ne rabotaet
- Proverte chto HectonSurvivalSystem.DrainEnergy(int) metod suschestvuet
- Proverte chto survivalSystem naznachen v PlayerFlashlight/PlayerPDA
- Proverte chto enableBatteryDrain = true v inspektore

## Performance

**Draw calls:** +1 draw call (Equipment Panel + Notifications)
**CPU:** ~0.1ms per frame (immediate mode rendering)
**Memory:** ~2KB (pre-allocated notification queue)
**GC:** 0 allocations per frame

## Sovmestimost

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
