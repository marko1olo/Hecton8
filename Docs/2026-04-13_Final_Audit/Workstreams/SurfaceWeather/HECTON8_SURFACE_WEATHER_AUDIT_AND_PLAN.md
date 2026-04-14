# HECTON-8 Surface Weather Audit And Plan

Status: PENDING VERIFICATION
Date: 2026-04-13
Scope: analysis and implementation plan only. No runtime weather code was added in this pass.

## 1. Mission

Design a production-grade above-water weather system for HECTON-8 that:

- treats the surface as a first-class gameplay and visual domain;
- supports calm sea, cloud variation, wind, rain, storms, lightning, day/night response, and sky beauty;
- affects ocean presentation, sky, lighting, audio, VFX, and surface ambience together;
- remains within MX350-class performance limits;
- does not pollute underwater systems with unrelated surface logic;
- preserves zero-GC hot-path rules.

This document is evidence-based. It describes what already exists, what is missing, where ownership is fragmented, and how the final system should be built.

## 2. Audit Result In One Sentence

There is no unified surface weather system in the project. There are only partial low-level building blocks spread across atmosphere, celestial visuals, underwater visuals, audio snapshots, depth soundscape, and Crest ocean runtime.

## 3. Evidence Base

The following systems were inspected directly:

- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- `Assets/_Project/Scripts/AtmosphereProfile.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/SurfaceStateUtility.cs`
- `Assets/_Project/Scripts/World/SoundscapeSystem.cs`
- `Assets/_Project/Scripts/AcousticZoneController.cs`
- `Assets/_Project/Scripts/AmbientWaterMotionManager.cs`
- `Assets/_Project/Scripts/AmbientWaterMotionProfile.cs`
- `Assets/_Project/Scripts/AmbientWaterMotion.cs`
- `Assets/_Project/Scripts/HectonOceanPalette.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
- `Assets/_Project/Scripts/World/DepthZoneProfile.cs`
- `Assets/_Project/Scripts/CurrentManager.cs`
- `Assets/_Project/Scripts/CurrentVolume.cs`
- `Assets/_Project/Scripts/MapMagicBridge.cs`
- `Assets/_Project/Scripts/EnvironmentState.cs`
- `Assets/_Project/Scripts/HectonBiomeProfile.cs`
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
- `Assets/_Project/Art/Shaders/SkyboxBlend.shader`
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` via Unity MCP scene/component inspection

Project documentation inspected:

- `AGENTS.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`

Unity scene inspection performed in live editor session:

- active scene: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- active sky/celestial system present
- Crest ocean present
- no dedicated weather director found in scene hierarchy

## 4. What Exists Right Now

### 4.1 HectonAtmosphereManager

Observed role:

- owns high-level environment states;
- handles day/night progression;
- handles eclipse state timing;
- switches between basic atmosphere profiles;
- reacts to underwater/surface transition and biome changes.

Observed states:

- `SURFACE_DAY`
- `SURFACE_NIGHT`
- `UNDERWATER`
- `ECLIPSE`

Observed limitation:

- no weather state model;
- no rain, storm, wind intensity, cloud cover ownership;
- no ocean-state orchestration;
- no surface event cadence beyond simple state switching.

Conclusion:

- useful as an upstream environment/time authority;
- insufficient as the full owner of release-grade surface weather without major scope expansion.

### 4.2 HectonCelestialEngine

Observed role:

- drives sky shader time and celestial presentation;
- controls day/night blend and star intensity;
- handles Aegir visuals and eclipse dimming;
- includes deep-water texture residency reduction;
- writes `_GameTime` and `_WindDirection` shader data;
- already has cloud-speed and storm-emission related material controls.

Observed limitation:

- weather concepts exist only as rendering knobs, not as gameplay-level weather logic;
- no formal storm lifecycle;
- no rain, lightning scheduler, cloud-front transitions, or weather severity ownership;
- currently a consumer endpoint, not a full orchestrator.

Conclusion:

- strong rendering sink for a future weather system;
- should receive weather outputs, not become the master weather brain.

### 4.3 HectonUnderwaterVisuals

Observed role:

- owns underwater fog, extinction, depth color, ambient attenuation, camera/background handling;
- applies deep celestial culling;
- restores limited surface defaults when player returns above water.

Observed limitation:

- surface handling is fallback-level only;
- above-water visuals are treated as reset values, not as a rich weather domain;
- mixing underwater ownership with surface defaults is an architecture smell.

Conclusion:

- underwater ownership should stay underwater;
- surface weather should not be implemented by stuffing more logic into this class.

### 4.4 AcousticZoneController

Observed role:

- switches audio mixer snapshots between `Surface`, `Underwater`, and `Interior`;
- already understands the surface as an acoustic domain.

Observed limitation:

- surface audio is one broad state only;
- no calm/rain/storm/heavy-storm differentiation;
- no lightning/thunder timing layer.

Conclusion:

- good integration point for weather-aware surface audio tiers.

### 4.5 SoundscapeSystem

Observed role:

- publishes depth-based soundscape tiers to shaders and possibly other consumers.

Observed limitation:

- surface is only a depth tier, not a weather system;
- no atmospheric modulation.

Conclusion:

- not a weather owner;
- may remain a separate depth context provider.

### 4.6 MapMagicBridge

Observed role:

- owns water surface level and terrain/biome queries;
- provides a reliable source for waterline and biome lookup.

Observed limitation:

- no weather authority;
- no direct coupling to sky/ocean states.

Conclusion:

- should remain source-of-truth for water level and biome context;
- useful for deciding whether surface weather simulation should be active.

### 4.7 Crest Ocean Runtime

Observed role:

- ocean renderer exists in scene;
- `ShapeFFT` exists;
- sea level is present and readable.

Observed live configuration snapshot:

- `CreateSeaFloorDepthData = true`
- `CreateFoamSim = false`
- `CreateDynamicWaveSim = false`
- `CreateFlowSim = false`
- `Scale = 8`

Observed limitation:

- current setup is lean and likely optimized for baseline cost;
- no weather coupling layer exists;
- naive storm implementation could enable expensive simulations and break frame budget.

Conclusion:

- ocean response must be designed around measured budget, not feature wish-list.

### 4.8 Sky Shader / Materials

Observed role:

- `Hecton_AlienSky_Master.shader` already exposes cloud density, softness, tiling, cloud colors, day/sunset/night cloud color logic, wind direction, and time scroll;
- `Mat_HectonSky.mat` already contains cloud-related tuning;
- art support for a strong sky presentation already exists.

Observed limitation:

- no authoring layer that maps weather states to these material parameters;
- no cloud-front progression model;
- no surface weather preset bank.

Conclusion:

- art substrate exists;
- system-level orchestration is missing.

## 5. What Does Not Exist

No evidence was found for:

- dedicated runtime surface weather owner;
- weather state machine over water;
- precipitation controller;
- lightning / thunder scheduling system;
- sea-state controller linked to weather severity;
- weather preset library;
- weather transition blending rules;
- storm gameplay cadence;
- surface-only culling coordinator for weather-dependent visuals;
- metrics-driven weather scalability model.

Search across scripts/docs/art found no dedicated first-party runtime `Weather*` system responsible for the surface experience.

## 6. Current Architecture Problem

The surface domain is currently fragmented:

- atmosphere manager decides broad state;
- celestial engine drives sky visuals;
- underwater visuals restore surface defaults;
- acoustic controller handles only broad surface audio;
- Crest handles ocean presentation independently;
- soundscape uses depth tiers;
- no single system owns the combined surface result.

This is the main technical problem.

If new weather logic is injected directly into all existing classes ad hoc, the likely result is:

- duplicated state;
- conflicting writes to sky/light/ocean/audio;
- difficult debugging;
- poor regression control;
- expensive per-frame cross-system polling;
- impossible release balancing.

## 7. Required Ownership Model

### Decision

Create a dedicated runtime owner for above-water weather.

Recommended owner:

- `HectonSurfaceWeatherDirector`

Recommended namespace:

- `Hecton8.Atmosphere`

Recommended responsibility:

- be the single source of truth for current and target surface weather;
- own state transitions, timing, severity, cloud cover, wind strength, precipitation, thunder cadence, and sea-state outputs;
- drive existing consumer systems through explicit integration points;
- remain inactive or reduced when the player is underwater for extended periods.

### Explicit Non-Ownership

The following systems should not become the primary weather owner:

- `HectonUnderwaterVisuals`
- `HectonCelestialEngine`
- `AcousticZoneController`
- Crest components directly

They should be consumers or adapters.

## 8. Target Weather Pillars

The release system must balance four pillars:

- beauty;
- realism;
- readability;
- optimization.

Beauty means:

- rich sky silhouette;
- layered cloud coverage;
- elegant calm states;
- dramatic but controlled storms;
- strong dawn, day, dusk, and night variation.

Realism means:

- weather affects ocean mood and surface visibility;
- wind changes are reflected in sea surface and audio;
- rain and storm light levels feel coherent;
- thunder has time offset and spatial cadence, not random spam.

Readability means:

- player can immediately read calm vs unstable vs storm danger;
- surface exposure conditions are legible without UI dependency;
- transitions are gradual and cinematic, not binary pops.

Optimization means:

- no expensive weather simulation while player is deep underwater for long periods;
- no full-feature storm stack running off-camera if the surface is culled;
- every effect tier has a low-cost fallback;
- GPU and VRAM budgets stay within project limits.

## 9. Proposed Runtime Model

### 9.1 Surface Presence Gate

The weather system should support three execution modes:

1. `SurfaceActive`
2. `SurfaceDormant`
3. `SurfaceSuppressed`

Meaning:

- `SurfaceActive`: player is above water or near enough to need full sky/ocean/weather presentation.
- `SurfaceDormant`: player just went underwater; weather state still advances at low cadence for continuity.
- `SurfaceSuppressed`: player is deep underwater long enough that above-surface rendering and rich weather simulation can be reduced to minimal bookkeeping.

This aligns with the user goal: if objects above surface are culled while player is underwater, the weather system must also reduce its cost.

### 9.2 Weather State Stack

Do not use a single enum only. Use a layered model:

- `TimeOfDayState`
- `CloudCoverageState`
- `WindState`
- `PrecipitationState`
- `StormState`
- `SeaState`

Recommended coarse weather presets:

- `ClearCalm`
- `ClearBreeze`
- `OvercastCalm`
- `OvercastWindy`
- `LightRain`
- `HeavyRain`
- `ElectricalStorm`
- `PostStormClearing`

Recommended output channels:

- sky material parameters;
- sun intensity/color modifiers;
- fog modifiers;
- ambient modifiers;
- ocean wave/wind multipliers;
- rain VFX activation tier;
- lightning scheduler;
- thunder audio cadence;
- optional gameplay hooks.

### 9.3 Authoring Data

Recommended ScriptableObjects:

- `SurfaceWeatherProfile`
- `SurfaceWeatherTransitionProfile`
- `SurfaceSeaStateProfile`
- `SurfaceLightningProfile`
- `SurfaceWeatherRuntimeConfig`

Profile content should include:

- cloud density/softness/scroll multipliers;
- horizon haze and sky exposure modifiers;
- wind direction bias and gust behavior;
- rain emission rates and visibility rules;
- thunder delay ranges;
- ocean response multipliers;
- ambient/audio sends;
- day/night override weights;
- transition duration ranges;
- low-tier fallback behavior.

### 9.4 Runtime State Separation

Separate:

- authored weather presets;
- live mutable weather runtime state;
- visual sinks;
- audio sinks;
- ocean sinks.

Do not mutate shared ScriptableObjects at runtime.

## 10. Integration Plan By Existing System

### 10.1 HectonAtmosphereManager

Keep as:

- day/night and high-level environment authority;
- eclipse source;
- underwater/surface truth contributor.

Add later:

- clean integration API so weather director can read time-of-day and environment context;
- optional event when surface-active state changes.

Do not:

- turn it into a monolithic weather manager unless approved after deeper refactor review.

### 10.2 HectonCelestialEngine

Use as sky rendering sink.

Weather director should drive:

- cloud density and softness;
- wind direction and speed scalars;
- star visibility suppression during overcast;
- sky exposure and tint modifiers;
- storm darkening;
- lightning flash requests through controlled interface.

This file already has the material plumbing. It lacks the weather brain.

### 10.3 HectonUnderwaterVisuals

Keep underwater-only.

Refactor target for later phase:

- remove non-essential surface weather responsibility from this class;
- leave only underwater visuals and surface reset fallback where strictly necessary.

### 10.4 AcousticZoneController

Extend surface branch to support weather layers:

- calm surface;
- windy surface;
- rain surface;
- storm surface.

Thunder should not be encoded only as one static snapshot. It needs timed one-shots on top of the base surface mix.

### 10.5 SoundscapeSystem

Leave as depth context system unless a later need appears.

Possible optional use:

- expose a weather intensity scalar to shaders if the project needs surface ambience modulation.

### 10.6 Crest Ocean

Weather should influence ocean through a constrained adapter layer, not by hardcoding directly in many classes.

Potential controls:

- global wind speed multiplier;
- wave amplitude multiplier;
- chop/foam response only if budget allows;
- calm state smoothing;
- storm roughness escalation.

Critical warning:

- current live scene disables several expensive Crest sims.
- enabling everything for storms without measurement is a regression risk.

### 10.7 Surface Culling

Above-water weather must obey the same high-level visibility logic as surface rendering.

If the player is deep underwater for sustained time:

- disable rain VFX;
- disable lightning flash VFX unless globally visible;
- reduce sky update cadence;
- reduce audio/weather scheduler cadence;
- keep only low-cost state progression data.

## 11. Optimization Rules For This Feature

The final implementation must follow these constraints:

- no `Update()` in gameplay weather code; use `ITickable` and `ISlowTickable`;
- zero GC in hot path;
- no per-frame string ops;
- no dynamic allocations during weather evaluation;
- no material instance leaks;
- no per-frame `GetComponent`;
- no coroutine-driven precipitation state machines in gameplay code;
- no uncontrolled VFX emission while surface is suppressed;
- no always-on expensive Crest storm features without measured proof.

Recommended cadence split:

- `ITickable`: short deterministic blending only;
- `ISlowTickable`: weather schedule, forecast roll, thunder planning, dormant-mode updates.

Recommended runtime caches:

- precomputed shader property IDs;
- cached component refs;
- preallocated lightning/rain buffers if needed;
- prebuilt preset lookup arrays.

## 12. Visual Design Direction For Surface Weather

The surface should not look like generic Earth weather.

Target look:

- NASA-punk clarity and instrument-readability;
- alien beauty in sky gradient and cloud coloration;
- ocean mood tied to scientific harshness, not fantasy excess;
- storms should feel rare, heavy, electrical, industrial, and dangerous;
- calm should feel premium and cinematic, not empty.

Key visual modes:

- crystal calm sunrise with soft cloud filaments;
- bright harsh day with controlled glare and restrained haze;
- overcast metallic sky with broad cloud mass;
- storm build-up with directional wind, lower sky ceiling, darkened horizon;
- night surface with sparse celestial visibility under different cloud loads;
- post-storm clearing with residual wave energy and broken clouds.

## 13. Audio Direction For Surface Weather

Required layers:

- base ocean surface ambience;
- wind intensity loop families;
- rain intensity loop families;
- thunder distant / mid / near one-shots;
- optional hull/suit surface response if design wants it later.

Rules:

- thunder timing must respect lightning delay;
- storm intensity must modulate wind and rain together;
- calm state must preserve negative space and avoid constant noise wash.

## 14. Proposed Implementation Phases

### Phase 0. Approval Gate

- approve ownership model;
- approve weather scope for release;
- approve whether storms affect gameplay or visuals/audio only.

### Phase 1. Foundation

- create data model and profile assets;
- create `HectonSurfaceWeatherDirector`;
- integrate with surface presence gate;
- wire time-of-day input from existing atmosphere/celestial systems.

### Phase 2. Sky And Lighting

- bind weather outputs into sky shader/material parameters;
- bind sun/ambient/fog modifiers;
- support clean transitions between calm, overcast, rain, and storm.

### Phase 3. Ocean Response

- build Crest adapter;
- expose sea-state response tiers;
- measure cost on MX350 target tier;
- keep expensive sims disabled unless proven safe.

### Phase 4. Audio And Thunder

- extend surface acoustic state;
- add thunder/lightning scheduler;
- synchronize flash and delayed thunder.

### Phase 5. Surface Suppression / Dormancy

- integrate long-underwater suppression behavior;
- reduce surface weather update cost when above-water rendering is not needed;
- verify continuity when resurfacing.

### Phase 6. Polish And Verification

- tune weather libraries;
- verify transitions;
- run regression checks for CPU, GC, memory, and cadence;
- test edge cases.

## 15. Risks

### Architecture Risk

If weather ownership is split again across atmosphere, celestial, underwater visuals, and Crest, maintenance cost will become unstable.

### Performance Risk

Storm visuals can easily blow GPU budget if implemented through:

- expensive rain overdraw;
- always-on lightning post chains;
- expensive foam/dynamic wave sims;
- frequent sky material writes across multiple renderers;
- uncontrolled particle systems.

### Correctness Risk

Without a dormancy model, resurfacing after long underwater time can cause:

- visual pops;
- wrong cloud state;
- audio mismatch;
- ocean mismatch.

### Regression Risk

`HectonUnderwaterVisuals` and `HectonCelestialEngine` already manage overlapping presentation concerns. Any new weather integration can create write conflicts if ownership boundaries are not explicit.

## 16. Open Technical Questions Before Implementation

These require explicit approval or follow-up inspection before coding the release system:

1. Is weather visual/audio-only, or does it affect gameplay systems later?
2. How often should storms occur in final pacing?
3. Should lightning be purely cinematic or capable of interacting with world logic later?
4. Do we want biome influence on surface weather styling, or only one global surface weather model?
5. Should the surface weather continue simulating globally while underwater, or only maintain compressed continuity state?
6. Is there any planned menu/debug tooling requirement for forcing weather presets?

## 17. Concrete Recommendation

Proceed with a dedicated surface-weather workstream, not a patchwork extension of current classes.

Recommended final structure:

- `HectonSurfaceWeatherDirector` as master runtime owner
- weather preset ScriptableObjects for authoring
- adapters into sky, lighting, ocean, audio, and VFX
- surface presence gate for aggressive optimization when underwater
- measured Crest response, not speculative full-feature storms

## 18. Verification Plan For Future Implementation

When coding starts, the following must be measured:

- BEFORE and AFTER frame time at surface calm state;
- BEFORE and AFTER frame time in heavy storm state;
- BEFORE and AFTER GC per frame;
- VRAM delta for weather-enabled scenes;
- ocean cost delta with weather coupling on;
- resurfacing continuity after spending long time underwater;
- dormant/suppressed mode recovery correctness.

Mandatory edge cases:

- surface to underwater rapid transitions;
- underwater for extended period, then resurfacing into changed weather;
- entering interior from stormy surface;
- save/load during different surface weather states if weather becomes persistent;
- eclipse coinciding with storm or overcast state;
- null or missing optional weather assets.

## 19. Immediate Next Step

Do not implement runtime weather yet.

First approval should confirm:

- ownership model;
- surface dormancy strategy while underwater;
- release weather scope;
- whether ocean response is visual-only or physical/gameplay-relevant.

After that, implementation can begin in controlled phases.

