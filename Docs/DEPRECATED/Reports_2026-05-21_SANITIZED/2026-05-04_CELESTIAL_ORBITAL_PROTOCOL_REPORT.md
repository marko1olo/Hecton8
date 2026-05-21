<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-04 Celestial Orbital Protocol Report
Date: 2026-05-07

Status: PENDING VERIFICATION

Reason: compile and Unity MCP console gates are clean. PlayMode smoke execution and profiler GC capture were not run in this pass.

## Mandates Applied

- ARCH_Global_Registry_ServiceLocator_DI_Init
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows
- CORE_Weather_Abyssal_FlowField_Currents
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC

## Recon Evidence

- Read: `Docs/Reports/CELESTIAL_MECHANICS_SPECIFICATION.md`
- Audited: `EclipseGameplaySystem.cs`, `HectonCelestialEngine.cs`, `EcosystemDirector.cs`
- Additional dependency audit: `ObserverRelativeCelestialBody.cs`, `HectonBiolumController.cs`, `HectonCrestOceanDepthCacheBootstrap.cs`, `SpatialAudioManager.cs`, `Hecton_AlienSky_Master.shader`, `HectonAtmosphereManager.cs`

## Protocol Closure

1. Multi-body eclipse detection
   - `HectonCelestialEngine` evaluates cached `ObserverRelativeCelestialBody` moons against the sun direction.
   - Eclipse state is driven by angular separation and angular radius overlap, with raw `PenumbraFactor` and smoothed `_EclipseOcclusion` global output.

2. Predator rising delay
   - `EclipseGameplaySystem` waits `predatorRiseDelay`, raises `NightPredatorsRising`, and calls `GlobalRegistry.EcosystemDirector.ApplyEclipsePredatorShallowMigration`.
   - `EcosystemDirector` applies Tier 0 targeting and exposes eclipse light suppression so active deep predators can enter shallow water without the light starvation constraint blocking them.

3. Tidal waterlevel modulation
   - `HectonCrestOceanDepthCacheBootstrap.ResolveWaterLevel()` now adds the Giant's Wake offset:
     `offsetY = 4m * clamp(dot(normalize(aegirDirection), Vector3.up), -1, 1)`.
   - Scope is the ocean depth cache water-level input. It does not overwrite the authored Crest sea level.

4. Biolum multiplier broadcast
   - `EclipseGameplayEvents` broadcasts `RaiseBiolumMultiplierChanged`.
   - `EclipseGameplaySystem` writes `_EclipseBiolumMultiplier`.
   - `HectonBiolumController` interpolates shader intensity and local proxy light intensity using bounded movement to avoid light pops.

5. Penumbra angular overlap
   - `HectonCelestialEngine` computes fractional solar-disc occlusion rather than binary eclipse state.
   - `_EclipseOcclusion` is lerped from that factor, preserving soft penumbra transitions.

6. Aegir fixed-direction lock
   - `GasGiant_Aegir` is forced through `ObserverRelativeCelestialBody.EnforceFixedDirectionLock`.
   - Visual sky direction stays fixed relative to observer AUP sky space.
   - Phase still changes from `dot(toSun, aegirToPlayer)`.

7. Procedural star grid hash
   - `_StarTex` sampling was stripped from `Hecton_AlienSky_Master.shader`.
   - Stars are generated from hashed grid cells and `_StarSeed`.
   - `HectonCelestialEngine` resolves `_StarSeed` from `HectonWorldGenerator` noise-layer seeds.

8. AtmosphereDirector skybox facade
   - Direct `RenderSettings.skybox =` scan returns one write only: `AtmosphereDirector.SetSkybox()` in `HectonAtmosphereManager.cs`.
   - No other first-party script writes the skybox directly.

9. Eclipse acoustic pitch shift
   - `EclipseGameplaySystem` maps total eclipse occlusion to `totalEclipseAcousticPitchShiftCents`, default `-150`.
   - `SpatialAudioManager.SetEclipseAcousticPitchShiftCents` applies the ratio only to ambient bed-bus world sources:
     `pitchRatio = 2^(cents / 1200)`.
   - At `-150 cents`, ratio is approximately `0.917004`.

## Smoke Tester

- Added: `Assets/_Project/Scripts/Dev/CelestialSyncSmokeTester.cs`
- Coverage:
  - Required runtime references resolve.
  - Aegir direction is finite and normalized.
  - Aegir fixed-direction lock is active.
  - Penumbra full, partial, and separated cases evaluate correctly.
  - Star seed is resolved.
  - AtmosphereDirector facade state is readable.
  - Spatial audio eclipse pitch ratio matches `2^(-150/1200)`.

## Penumbra Angular Overlap Math

Let:

- `rS` = sun angular radius in degrees.
- `rO` = occluder angular radius in degrees.
- `d` = angular separation between sun direction and occluder direction in degrees.

Cases:

- `d >= rS + rO`: no overlap, `PenumbraFactor = 0`.
- `d <= abs(rO - rS)` and `rO >= rS`: full solar-disc cover, `PenumbraFactor = 1`.
- `d <= abs(rO - rS)` and `rO < rS`: occluder is inside the solar disc, `PenumbraFactor = rO^2 / rS^2`.
- Partial overlap:

```text
A =
  rS^2 * acos((d^2 + rS^2 - rO^2) / (2 * d * rS))
+ rO^2 * acos((d^2 + rO^2 - rS^2) / (2 * d * rO))
- 0.5 * sqrt((-d + rS + rO) * (d + rS - rO) * (d - rS + rO) * (d + rS + rO))

PenumbraFactor = saturate(A / (pi * rS^2))
```

This returns the fraction of the sun disc area blocked by the occluding body.

## Verification

- Compile command:
  `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`
- Compile result:
  `Build succeeded. 35 Warning(s). 0 Error(s). Time 00:01:31.96`
- Unity MCP console command:
  `read_console` with `types=["error"]`, `count="50"`, `format="json"`, `include_stacktrace=false`
- Unity MCP console result:
  `Retrieved 0 log entries.`
- Direct `_StarTex` scan:
  no matches in `HectonCelestialEngine.cs` or `Hecton_AlienSky_Master.shader`
- Direct skybox assignment scan:
  only `HectonAtmosphereManager.cs:80`, inside `AtmosphereDirector.SetSkybox()`

## Diff Artifact

- Complete relevant Git diff saved to:
  `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_DIFF.patch`

## Residual Limits

- `CelestialSyncSmokeTester` was written but not executed in PlayMode.
- Runtime predator migration, tidal cache motion, and audio pitch behavior were not observed in-scene.
- Profiler GC capture was not executed.
- The repository had extensive dirty state before this pass. Diff artifacts are scoped to relevant touched files but may include pre-existing hunks inside those same files.
