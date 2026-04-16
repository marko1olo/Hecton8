# HECTON-8 Celestial Mechanics Audit And Execution Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-16`

## 1. Scope

Objective:
- audit the current sun / sky / gas giant / moon stack in `02_HECTON_WORLD`
- identify what is architecturally correct, what is visually false, and what still needs work
- improve the current system without replacing the atmosphere, weather, or eclipse owners

Evidence base:
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs`
- `Assets/_Project/Scripts/GasGiantRotationDriver.cs`
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
- `Assets/_Project/Art/Shaders/SG_GasGiant_Master.shader`
- `Assets/_Project/Art/Shaders/Hecton_CelestialMoon.shader`
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Ione.mat`
- `Assets/_Project/Art/Materials/Celestial/MAT_CelestialMoon_Thalos.mat`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Unity MCP scene inspection and screenshots on `2026-04-16`

## 2. Current Runtime Truth

### 2.1 Camera architecture

Current sky rendering is not owned by the gameplay camera directly.

Observed in scene:
- `SpaceCamera` is the base camera for celestial rendering
- `SpaceCamera` culls only the `Celestial` layer
- `Main Camera` is stacked on top of `SpaceCamera`
- debug captures from `Main Camera` alone are misleading for sky verification

Implication:
- celestial work must remain compatible with the `SpaceCamera -> Main Camera` stack
- visual verdicts must be taken from `SpaceCamera`, not from overlay-only captures

### 2.2 Sun ownership split

Current sun state is intentionally split across three systems:

1. `HectonAtmosphereManager`
- owns cycle time
- rotates the directional light
- computes horizon fade
- exposes shared elapsed cycle time

2. `HectonUnderwaterVisuals`
- attenuates light and fog for depth states

3. `HectonCelestialEngine`
- computes gas giant relation, eclipse state, night blend, stars, and sky shader data
- modulates final celestial visibility against the already-owned light chain

This split is valid. It should not be replaced casually.

### 2.3 Sun visual model

Current implementation combines:
- directional light rotation from `HectonAtmosphereManager`
- a legacy mesh-based `Sun_Body` visual placed observer-relative
- shader sun disc and atmospheric scatter in `Hecton_AlienSky_Master`

Result:
- translation parallax is suppressed for the sun
- sunrise / sunset direction changes are smooth and cheap
- sun appearance is still art-driven rather than full physical sky scattering

### 2.4 Gas giant model

Current implementation for `GasGiant_Aegir`:
- observer-relative body driven by `ObserverRelativeCelestialBody`
- fixed sky direction instead of free world-space placement
- authorable apparent angular diameter instead of inferred fake distance
- custom shader `HECTON/Celestial/SG_GasGiant_Master`
- runtime phase, backlight, eclipse rim, and sky-color injection via MPB
- slow UV rotation via `GasGiantRotationDriver` and `HectonCelestialEngine`

Observed current scene values:
- `CurrentDirection = (0.9255, 0.1201, 0.3592)`
- `AngularDiameterDegrees = 21.5`
- `anchorDistance = 21000`
- `timeSourceMode = AtmosphereCycle`

Result:
- translation parallax is suppressed for Aegir
- Aegir remains horizon-anchored as a sky body instead of drifting like a nearby mesh
- the remaining problem is presentation tuning and system coherence, not raw placement

### 2.5 Secondary body model

Current implementation for visible moons:
- six observer-relative secondary bodies now exist around `Aegir`
- current visible family: `Moon_Pelagia`, `Moon_Ione`, `Moon_Varda`, `Moon_Khepri`, `Moon_Thalos`, `Moon_Nammu`
- both use the shared atmosphere-owned time source
- both use `HECTON/Celestial/Hecton_CelestialMoon`
- both receive global sun / sky / Aegir context from `HectonCelestialEngine`

Observed current scene values:
- `Moon_Pelagia`: `AngularDiameterDegrees = 0.34`, `apparentOrbitRadiusDegrees = 7.1`
- `Moon_Varda`: `AngularDiameterDegrees = 0.56`, `apparentOrbitRadiusDegrees = 10.2`
- `Moon_Ione`: `AngularDiameterDegrees = 1.18`, `apparentOrbitRadiusDegrees = 14.6`
- `Moon_Khepri`: `AngularDiameterDegrees = 0.48`, `apparentOrbitRadiusDegrees = 19.7`
- `Moon_Thalos`: `AngularDiameterDegrees = 0.74`, `apparentOrbitRadiusDegrees = 24.9`
- `Moon_Nammu`: `AngularDiameterDegrees = 0.86`, `apparentOrbitRadiusDegrees = 31.4`

Current authoring fix applied in this pass:
- `Moon_Pelagia orbitalPeriodSeconds = 34200`
- `Moon_Varda orbitalPeriodSeconds = 52200`
- `Moon_Ione orbitalPeriodSeconds = 75600`
- `Moon_Khepri orbitalPeriodSeconds = 111600`
- `Moon_Thalos orbitalPeriodSeconds = 151200`
- `Moon_Nammu orbitalPeriodSeconds = 205200`
- each visible moon now matches axial rotation period to orbital period so the family reads closer to tidally locked bodies

Intent:
- visible drift should stay subtle over a one-hour play session
- moons should read closer to tidally locked bodies than to fast UI ornaments

### 2.6 Sky dome model

Current sky dome:
- `Sky_System` follows the camera position
- child sphere scale = `25000`
- material = `Mat_HectonSky`
- shader already supports sun disc, scatter, stars, eclipse darkening, Aegir halo, and cloud transmittance

This is strong infrastructure. The current weakness is not the dome architecture. The weakness is visual tuning and shared celestial framing.

## 3. What Works

- observer-relative sun visual avoids close-object parallax
- Aegir is no longer a raw world-space wall
- eclipse detection is angular, not trigger-based
- `SurfaceWeatherDirector` already modulates cloud density, sun disc, scatter, and luminance
- celestial rendering remains isolated on `SpaceCamera`
- sky, giant, and moon materials already receive shared color context
- shared atmosphere-owned cycle time removed the old split between sun time and moon time

## 4. Main Failures

### 4.1 The old distance-model failure is gone, but stale docs were lying

The original failure state for Aegir was already removed from scene authoring.

Remaining failure:
- old audit text still described the pre-fix world-space giant
- any future art or lore decisions built on the old `49.5 degree` baseline would be wrong

### 4.2 Angular size must stay authored against the target fantasy

Current angular diameter is `21.5 degrees`.

That is intentionally huge. It is not physically neutral. It is a chosen fantasy target.

Risk:
- too low and Aegir loses the oppressive identity the project wants
- too high and it becomes surreal nearby-wall noise again

Current safe working band:
- `21` to `24` apparent degrees
- center kept low enough that the lower hemisphere is partially buried by the ocean horizon

### 4.3 Orbital mechanics are still mostly implied, not fully simulated

Current runtime model gives:
- day-night solar motion
- eclipse alignment
- gas giant phase / backlight
- cheap apparent moon drift around the parent giant

Current runtime model still does not give:
- true n-body ephemeris
- generalized multi-body occlusion
- fully physical synodic phase solution for every secondary body
- lore-grade astronomy and runtime-grade motion from one shared dataset

### 4.4 Remaining ugliness is timing and presentation

Edge cases that can still look wrong:
- moon drift becomes too obvious inside a one-hour play session
- moon phase and giant phase disagree with sun motion if time ownership splits again
- Aegir reads as a clean matte disc instead of a body seen through atmospheric depth
- sun disc vanishes into haze while the surrounding sky still over-brightens
- cloud cover can wash the frame so much that the celestial hierarchy loses legibility
- edit-mode screenshots can under-report celestial shading because part of the sky / giant read is runtime-driven through `HectonCelestialEngine`

## 5. Constraints For Fix

Non-negotiable:
- do not rewrite the whole atmosphere system
- do not replace `HectonAtmosphereManager` as sun owner
- do not replace `HectonCelestialEngine` as eclipse / weather bridge
- do not add extra cameras or expensive render textures
- do not add per-frame allocations

## 6. Recommended Architecture

### 6.1 Keep current sun stack

Keep:
- `HectonAtmosphereManager` as cycle and directional-light owner
- `HectonUnderwaterVisuals` as depth attenuation owner
- `HectonCelestialEngine` as eclipse / sky / phase bridge

### 6.2 Keep observer-relative placement

All astronomical-feeling bodies should stay observer-relative:
- stable apparent size
- stable horizon anchoring
- no fake travel parallax
- cheap rendering

### 6.3 Keep time ownership unified

Visible bodies must not invent their own time.

Rule:
- `HectonAtmosphereManager` owns cycle time
- observer-relative bodies consume that time
- celestial shading consumes the same resolved directions

### 6.4 Separate lore astronomy from runtime motion compression

Needed for this project:
- lore numbers can stay physically motivated
- runtime visible drift can be compressed or damped for readability
- docs must state both values explicitly to avoid design confusion later

## 7. Executed On 2026-04-16

Implemented:
- new `ObserverRelativeCelestialBody` runtime driver in `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs`
- converted `GasGiant_Aegir` to observer-relative placement with authorable angular diameter
- added `Moon_Ione` and `Moon_Thalos` as secondary observer-relative bodies
- updated `HectonCelestialEngine` so eclipse, halo, sky-direction, and phase logic can consume observer-relative data
- corrected `ObserverRelativeCelestialBody` to use shared atmosphere-owned cycle time instead of raw `Time.time`
- corrected `ObserverRelativeCelestialBody` to tick via `ITickable` instead of `LateUpdate`
- added optional observer-height horizon compensation in `ObserverRelativeCelestialBody` so Aegir can remain visually buried against the sea horizon when the player gains altitude
- exposed monotonic elapsed cycle time from `HectonAtmosphereManager`
- published global celestial shader data for moon materials
- moved moons onto a dedicated far-body shader instead of stock URP Lit
- expanded the visible moon family from 2 bodies to 6 bodies
- authored distinct material variants for `Pelagia`, `Varda`, `Khepri`, and `Nammu`
- slowed the visible family cadence and matched axial rotation period to orbital period for all visible moons
- retuned `Mat_GasGiant.mat` toward stronger rim veil and lower white wash so Aegir separates from the local sky more convincingly
- retuned `Mat_GasGiant.mat` and `SG_GasGiant_Master.shader` again to preserve center detail and cloud-band readability instead of washing the entire disc into horizon haze
- retuned sun presentation in `Mat_HectonSky.mat` toward a softer disc and broader glow
- replaced the old `Sun.shader` output with a softer disc/core/corona model and authored explicit `Mat_Sun` values so the geometry sun is no longer just a hard white additive sphere
- aligned `Mat_HectonSky.mat` and `Mat_HectonSky_CloudOverlay.mat` toward a smaller, softer sky-disc with lower raw HDR disc energy and broader warm scattering
- disabled `Sun_Body` as an above-water runtime owner when `HectonAtmosphereManager` is present so the sky-disc is now the sole surface sun path
- added a mid-disc belt recovery step to `SG_GasGiant_Master.shader` so Aegir can keep atmospheric edges while recovering cloud-band readability inside the disc
- adjusted `Hecton_CelestialMoon.shader` so daytime crescents keep faint sky-lit shadow-side visibility instead of collapsing into black cutout discs
- fixed `Game Preview` celestial visibility by ensuring the active `Main Camera` scene instance includes the `Celestial` layer when camera stacking is unavailable
- increased `GasGiant_Aegir` runtime apparent angular diameter to `32.25 degrees`
- expanded visible moon orbital bands outward after the Aegir up-scale so inner moons still transit the giant while outer moons can read as separate bodies beyond the limb
- added daylight-presence controls to `Hecton_CelestialMoon.shader` (`_DayDiskLift`, `_DayShadowSkyLift`, `_HazeLitPreserve`) and authored them per-moon
- reverted the last over-detailed gas giant pass after user feedback and restored the more distant giant read before continuing moon expansion

## 8. Remaining Risks

`PENDING VERIFICATION` until Unity confirms:
- clean domain reload after script and material changes
- no missing serialized references after scene reopen
- no visual regression in `SpaceCamera`
- no eclipse regression from the observer-relative angular source
- no accidental play-mode-only loss of the new moon cadence values

Known risks:
- moon transforms are still mostly visual and not yet a full multi-body ephemeris
- eclipse / occlusion is still primarily Aegir-centric and not generalized to every secondary body
- moon horizon haze still needs live gameplay-camera tuning
- the editor currently shows play-mode drift during tool-driven inspection, so scene save discipline is mandatory after every celestial authoring pass

## 9. Verification Checklist

Required after each pass:
- Unity compile clean
- no new console errors
- `SpaceCamera` screenshots at dawn, noon, dusk, and partial eclipse cases
- confirm Aegir no longer exhibits travel parallax
- confirm moon drift stays subtle over a one-hour session
- confirm sun / underwater / weather stack still writes sane values
