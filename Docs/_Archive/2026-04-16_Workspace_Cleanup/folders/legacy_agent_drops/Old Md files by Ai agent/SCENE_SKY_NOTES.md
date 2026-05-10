Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

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

Problema:
- V `Game` kastomnoe nebo i oblaka vidny
- V `Scene View` dolgoe vremya byli vidny tolko solntse/gazovyy gigant ili sero-chernyy fon

## Chto uzhe vyyasneno

- `Sky_System/Sphere` suschestvuet i aktivna
- `RenderSettings.skybox` ukazyvaet na proektnyy `Mat_HectonSky`
- [HectonUnderwaterVisuals.cs](C:/hades/Hecton8/Assets/_Project/Scripts/HectonUnderwaterVisuals.cs) v editor rezhime podpisyvaetsya na `EditorApplication.update`
- V editor rezhime etot skript ranshe ispolzoval `SceneView.lastActiveSceneView.camera` kak `playerCamera`
- Iz-za etogo `Scene View` pochti vsegda schitalsya pod vodoy, potomu chto kamera redaktora byla nizhe `waterLevel = 4900`

## Chto uzhe ispravleno

- V `ResolveEditorCamera()` teper:
  - dlya rascheta sostoyaniya sredy snachala beretsya `Camera.main`
  - `SceneView` ostaetsya tolko render-target kameroy redaktora
- Dobavlen [SceneViewSkyboxEnforcer.cs](C:/hades/Hecton8/Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs)
  - vklyuchaet `showSkybox`
  - vklyuchaet `showClouds`
  - vklyuchaet `showImageEffects`
  - vklyuchaet `showFog`
  - vklyuchaet `sceneLighting`
  - stavit `SceneView.camera.clearFlags = Skybox`

## Ostatochnyy defekt

- Posle etih pravok `Scene View` uzhe ne v polnostyu slomannom sostoyanii
- No oblaka/kastomnyy sky vse esche ne sovpadayut s `Game`
- Znachit ostatok baga nahoditsya ne v polnom otsutstvii skybox, a v editor-preview sostoyanii sky pipeline

## Sleduyuschie tochki proverki

- live-polya `HectonUnderwaterVisuals` v editor:
  - `CurrentDepth`
  - `IsUnderwater`
  - `playerCamera`
  - `mainCamera`
- live-polya `HectonAtmosphereManager`:
  - `TimeOfDay`
  - `CurrentSkyExposure`
  - `CurrentSunIntensity`
- live material values `Mat_HectonSky`:
  - `_NightBlend`
  - `_EclipseOcclusion`
  - `_GameTime`
  - cloud colors / haze / star intensity

## Current Visual Baseline Snapshot

Purpose:
- preserve the current look before future A/B preset work
- future preset system should treat this as `baseline_00`

Source of truth:
- Skybox material: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Gas giant material: `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- Active gas giant object: `--- WORLD ---/GasGiant_Aegir`
- Active ocean object: `--- WORLD ---/Ocean_Crest`
- Active ocean material from live `OceanRenderer`: `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`

Live environment snapshot:
- `RenderSettings.skybox = Mat_HectonSky`
- Fog:
  - enabled = `true`
  - mode = `ExponentialSquared`
  - color = `(0.120882213, 0.3810319, 0.641181648, 1)`
  - density = `0.0007995186`
- Ambient:
  - mode = `Flat`
  - ambientLight = `(0.02, 0.04, 0.06, 1)`
  - equatorColor = `(0.114, 0.125, 0.133, 1)`
  - groundColor = `(0.047, 0.043, 0.035, 1)`

`Mat_HectonSky` key values:
- `_SkyColorZenith = (0.1, 0.16, 0.5, 1)`
- `_SkyColorHorizon = (0.4248, 0.38736, 0.61200005, 1)`
- `_SkyColorNadir = (0.0152634354, 0.0254182927, 0.0660377145, 1)`
- `_HazeColor = (0.3, 0.35, 0.43, 1)`
- `_HazeIntensity = 0.82`
- `_CloudColorLit = (0.8, 0.8346785, 0.9019608, 1)`
- `_CloudColorShadow = (0.32, 0.36, 0.48, 1)`
- `_NightBlend = 0`
- `_StarIntensity = 0`

`Mat_GasGiant` key values:
- `_SkyColorZenith = (0.05, 0.08, 0.25, 1)`
- `_SkyColorHorizon = (0.56, 0.52, 0.7, 1)`
- Upper haze:
  - `_DistanceUpperHazeStart = 0.24`
  - `_DistanceUpperHazePeak = 0.38`
  - `_DistanceUpperHazeEnd = 0.98`
  - `_DistanceUpperHazeBlend = 0.82`
  - `_DistanceUpperHazeVeilBoost = 0.58`
  - `_DistanceUpperHazeWhiten = 0.88`
  - `_DistanceUpperHazeDarken = 0`
  - `_DistanceUpperHazeDesaturate = 0`
  - `_DistanceUpperHazeDetailFade = 0`
- Medium haze:
  - `_DistanceMediumHazeStart = 0.08`
  - `_DistanceMediumHazePeak = 0.18`
  - `_DistanceMediumHazeEnd = 0.52`
  - `_DistanceMediumHazeBlend = 0.54`
  - `_DistanceMediumHazeVeilBoost = 0.34`
  - `_DistanceMediumHazeWhiten = 0.7`

Ocean / water baseline:
- Active ocean renderer: Crest `OceanRenderer` on `Ocean_Crest`
- Material path: `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
- Sea level = `4900`
- Gravity = `8.8`
- Scale = `8`
- `UnderwaterDepthFogDensity = (0.0099700689, 0.0099700689, 0.0099700689)`

Preset-system requirement note:
- future presets must switch one coherent scene look, not isolated single-material tweaks
- minimum preset scope:
  - skybox colors / haze / cloud tint
  - fog and ambient colors
  - gas giant palette + haze fields
  - water baseline material / ocean fog-related look values
