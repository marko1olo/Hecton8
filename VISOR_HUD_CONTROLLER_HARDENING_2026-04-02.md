# VISOR HUD CONTROLLER HARDENING — 2026-04-02

## Что изменено

- `Assets/_Project/Scripts/Visor/VisorHUDController.cs`
  - runtime refresh больше не живёт на native `Update()`
  - play-mode path переведён на `ITickable` через `GameTickManager`
  - `Update()` оставлен только для edit-mode preview и ленивой bootstrap-регистрации
  - glitch state machine больше не управляет регистрацией тика отдельно от основного runtime HUD path
  - обновление material property block вынесено в явный `ApplyMaterialProperties()`
  - освобождение собственного runtime `RenderTexture` теперь разделяет play/edit path:
    - `Destroy()` в play mode
    - `DestroyImmediate()` в edit mode

## Что это значит простыми словами

Раньше `VisorHUDController` был в промежуточном состоянии:

- `ITickable` уже использовался
- но только для glitch-таймера
- основной runtime HUD refresh всё ещё жил в `Update()`

Теперь контракт стал цельным:

- runtime visor HUD обновляется через проектную tick-систему
- edit-mode preview не потерян
- glitch остаётся zero-GC и просто модулирует уже существующий runtime path

## Зачем это полезно

- меньше дублирования между `Update()` и `Tick()`
- меньше риска, что runtime и glitch начнут жить в разных timing-path
- чище lifecycle при `OnEnable` / `OnDisable`
- безопаснее освобождение runtime RT

## Что подтверждено

- кодовый diff проверен вручную
- внешний API класса сохранён:
  - `SetHUDIntensity`
  - `SetProjectionMode`
  - `SetSharedRenderTexture`
  - `GlitchPulse`

## Что подтверждено позже

Позже после возврата Unity session этот проход был доснят живой проверкой:

- Unity снова подключилась к MCP
- refresh/compile прошёл без `Error`
- последующий короткий `play -> stop` smoke завершился с пустой консолью

Итог:

- этот visor pass теперь считается live-подтверждённым
