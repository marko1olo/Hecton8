<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-04 Celestial Environment Orbital Sync Report
Date: 2026-05-07

Status: PENDING VERIFICATION

Reason: compile and Unity MCP console gates were reported for that pass, but PlayMode behavior, profiler GC capture, and in-scene predator/tide/biolum observation were not executed. Later May 4 documentation sweep evidence supersedes this report as global project truth; this file remains task evidence only.

## Mandates Applied

- ARCH_Global_Registry_ServiceLocator_DI_Init
- AI_Director_Encounter_Manager
- CORE_Weather_Abyssal_FlowField_Currents
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Zero_GC_Policy_AllocFree_Mandate

## Recon Evidence

- Read: `Docs/Reports/CELESTIAL_MECHANICS_SPECIFICATION.md`
- Audited: `EclipseGameplaySystem.cs`, `HectonCelestialEngine.cs`, `EcosystemDirector.cs`
- Additional dependency audit: `HectonBiolumController.cs`, `HectonCrestOceanDepthCacheBootstrap.cs`, `FaunaBrain.cs`, `GlobalRegistryContracts.cs`

## Surgery Log

### Eclipse Predator Rising

- `EclipseGameplaySystem` now routes post-delay eclipse predator migration through `GlobalRegistry.EcosystemDirector`.
- `EcosystemDirector` exposes `ApplyEclipsePredatorShallowMigration(float intensity01, float holdSeconds)` and `ResolveEclipsePredatorLightSuppression01(Vector3 worldPosition)`.
- During an active eclipse window, Tier 0 depth is treated as 0-40 meters below the water surface.
- Deep predator migration targets a player-centered Tier 0 attractor at water surface minus `eclipsePredatorTier0TargetDepthMeters`.
- Hibernated apex fauna are migrated toward that attractor through `PersistentWorldRegistry.MigrateApexFaunaHibernationStatesToward`.
- Active predator light exposure is suppressed in `FaunaBrain`, so the shallow migration is not blocked by residual light starvation behavior.

### Tidal Height Cache Modulation

- `HectonCrestOceanDepthCacheBootstrap.ResolveWaterLevel()` now adds a tidal offset derived from `HectonCelestialEngine.TryGetAegirSkyDirection`.
- Math: `waterLevelY = baseWaterLevelY + tidalHeightCacheAmplitudeMeters * clamp(dot(normalize(aegirDirection), Vector3.up), -1, 1)`.
- Default amplitude is 4 meters.
- Scope is the depth cache water-level input. Crest global sea level is not overwritten.

### Bioluminescence Multiplier Broadcast

- `EclipseGameplayEvents` now includes `RaiseBiolumMultiplierChanged(float multiplier)`.
- `EclipseGameplaySystem` publishes `_EclipseBiolumMultiplier` from the stronger of `PenumbraFactor` and `SunOcclusionFactor`.
- `HectonBiolumController` listens to the event and interpolates shader intensity plus local proxy lights with `Mathf.MoveTowards`, preventing instantaneous light pops.

### Gas Giant Atmospheric Shadowing

- `HectonCelestialEngine` now computes a raw `PenumbraFactor` and publishes `_PenumbraFactor`.
- `_EclipseOcclusion` remains smoothed and now targets the raw penumbra value instead of a binary on/off eclipse state.

## Penumbra Angular Overlap Math

Let:

- `rS` = sun angular radius in degrees.
- `rO` = occluder angular radius in degrees.
- `d` = angular separation between sun direction and occluder direction in degrees.

Cases:

- `d >= rS + rO`: no overlap, `PenumbraFactor = 0`.
- `d <= abs(rO - rS)` and `rO >= rS`: full solar disc cover, `PenumbraFactor = 1`.
- `d <= abs(rO - rS)` and `rO < rS`: occluder is inside the solar disc, `PenumbraFactor = rO^2 / rS^2`.
- Partial overlap:

```text
A =
  rS^2 * acos((d^2 + rS^2 - rO^2) / (2 * d * rS))
+ rO^2 * acos((d^2 + rO^2 - rS^2) / (2 * d * rO))
- 0.5 * sqrt((-d + rS + rO) * (d + rS - rO) * (d - rS + rO) * (d + rS + rO))

PenumbraFactor = saturate(A / (pi * rS^2))
```

This reports the fraction of the solar disc area blocked by the occluder.

## Verification

- Compile: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`
- Compile result: `Build succeeded. 0 Warning(s), 0 Error(s), Time 00:00:07.18`
- Unity MCP console command: `read_console` with `types=["error"]`, `count="50"`, `format="json"`
- Unity MCP console result: `Retrieved 0 log entries.`

## Diff Artifact

- Complete relevant Git diff saved to: `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_DIFF.patch`

## Residual Limits

- PlayMode behavioral proof was not executed.
- Profiler GC capture was not executed.
- The repository had extensive pre-existing dirty state before this task. The saved diff is complete for the touched files and may include pre-existing hunks in those same files.
