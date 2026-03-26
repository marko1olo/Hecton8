# Scene Sky Notes

## Latest

- `Sky_System` was still drifting to the wrong editor camera target.
- `SkySystemFollowCamera.cs` now uses this priority:
  - `runtimeCamera`
  - `Camera.main`
  - `SceneView.lastActiveSceneView.camera`
  - any enabled active camera fallback
- Added explicit editor tick through `EditorApplication.update`.
- Verified after compile through MCP:
  - `Sky_System.position == Main Camera.position`
- Result:
  - one direct cause of black `Game` sky in edit mode is removed
  - remaining issue is specifically the custom cloud/sky layer in `Scene View`, not the dome position itself

Проблема:
- В `Game` кастомное небо и облака видны
- В `Scene View` долгое время были видны только солнце/газовый гигант или серо-чёрный фон

## Что уже выяснено

- `Sky_System/Sphere` существует и активна
- `RenderSettings.skybox` указывает на проектный `Mat_HectonSky`
- [HectonUnderwaterVisuals.cs](C:/hades/Hecton8/Assets/_Project/Scripts/HectonUnderwaterVisuals.cs) в editor режиме подписывается на `EditorApplication.update`
- В editor режиме этот скрипт раньше использовал `SceneView.lastActiveSceneView.camera` как `playerCamera`
- Из-за этого `Scene View` почти всегда считался под водой, потому что камера редактора была ниже `waterLevel = 4900`

## Что уже исправлено

- В `ResolveEditorCamera()` теперь:
  - для расчёта состояния среды сначала берётся `Camera.main`
  - `SceneView` остаётся только render-target камерой редактора
- Добавлен [SceneViewSkyboxEnforcer.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs)
  - включает `showSkybox`
  - включает `showClouds`
  - включает `showImageEffects`
  - включает `showFog`
  - включает `sceneLighting`
  - ставит `SceneView.camera.clearFlags = Skybox`

## Остаточный дефект

- После этих правок `Scene View` уже не в полностью сломанном состоянии
- Но облака/кастомный sky всё ещё не совпадают с `Game`
- Значит остаток бага находится не в полном отсутствии skybox, а в editor-preview состоянии sky pipeline

## Следующие точки проверки

- live-поля `HectonUnderwaterVisuals` в editor:
  - `CurrentDepth`
  - `IsUnderwater`
  - `playerCamera`
  - `mainCamera`
- live-поля `HectonAtmosphereManager`:
  - `TimeOfDay`
  - `CurrentSkyExposure`
  - `CurrentSunIntensity`
- live material values `Mat_HectonSky`:
  - `_NightBlend`
  - `_EclipseOcclusion`
  - `_GameTime`
  - cloud colors / haze / star intensity
