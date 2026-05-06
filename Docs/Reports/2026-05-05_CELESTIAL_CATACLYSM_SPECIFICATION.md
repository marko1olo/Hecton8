# HECTON-8 Celestial Cataclysm Specification

Date: 2026-05-07
Status: PENDING VERIFICATION

## Mandates Followed

- `.agents-skills/VOX_Voxel_World_Logic_Carving_Persistence.txt`
- `.agents-skills/PHYS_Fluid_Incursion_Interior.txt`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`
- `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

## Implemented Runtime Links

- `CelestialCataclysmSystem` now owns cross-domain cataclysm consequences: meteor impact craters, titanium scrap drops, solar EMP broadcast, physical tide modulation, lunar resonance relay, and temporary meteor fog-shadow globals.
- `RandomEventSystem` exposes `SolarFlare` as a first-party random event with 30 second duration and intensity.
- `BaseModule` listens for `ElectromagneticPulseEvent` and forces operational power to zero while the EMP blackout timer is active.
- `HectonCelestialEngine` detects lunar resonance when two non-Aegir moons align under the configured angular threshold and publishes `_AtmosphereDensity`.
- `EclipseGameplaySystem`, `HectonBiolumController`, and `FloraRegrowthDirector` consume the lunar resonance multiplier for brighter biolum and faster flora maturation.
- `HectonFluidEngine` applies tidal shear torque where Giant Wake current opposes or crosses standard abyssal current.
- `Hecton_AlienSky_Master.shader` adds atmosphere-density twinkle; horizon stars twinkle faster than zenith stars.
- `HectonAtmosphereManager` applies Aegir ring shadow attenuation to the final giant abyss light.
- `Hecton_VolumetricLight.compute` consumes temporary meteor shadow positions for fog shadowing.
- `CelestialTimeLapseDebugger` provides a developer-only 1000x time accelerator while clamping physics deltas.
- `CelestialCataclysmSmokeTester` validates the random-event enum, EMP listener contract, voxel crater entry point, shader globals, volumetric compute asset, and titanium scrap prefab contract.

## Physical Voxel Tides

The tide now drives `HectonFluidEngine.WaterLevel` from Aegir's wake:

```
tidalHeightMeters = 4.0 * dot(normalize(aegirDirection), Vector3.up)
waterLevelY = baseWaterLevelY + tidalHeightMeters
```

This affects systems that read the authoritative water level, including buoyancy and depth-cache consumers already linked to the fluid engine.

Schema limitation: the current voxel cell schema exposes SDF density, material ID, and flags. There is no authoritative persisted water-occupancy channel with consumers that interpret "SDF Water cells" separately from void cells. Therefore, this pass does not fake an Air-to-Water SDF swap. The production path is to add a bounded water occupancy/delta channel and consumers for cave flooding in a separate voxel-fluid schema change.

## Meteor SDF Impact Surgery Log

Event path:

```
RandomEventSystem.MeteorShower
-> RandomEventEvents
-> CelestialCataclysmSystem.OnRandomEventStarted
-> ExecuteMeteorSdfImpact(intensity)
-> HectonVoxelVolume.TryApplyExtraterrestrialImpactCrater(impactPosition, 50m)
-> HectonVoxelVolume.CarveCrater
-> VoxelDeltaProcessor.ApplyImmediateCrater / ApplyImmediateAbsoluteCrater
-> VoxelDeltaProcessor.TrySchedulePendingCarve
-> [BurstCompile] CarveSdfJob : IJobParallelFor
-> VoxelDeltaProcessor.TryCommitScheduledCarve
```

Burst carve math:

```
candidateBounds = impactCenter +/- craterRadius +/- blendRadius
absoluteCell = minCell + int3(localX, localY, localZ)
cellCenter = (absoluteCell + 0.5) * voxelSize
signedDistance = distance(cellCenter, impactCenter) - craterRadius

if signedDistance < blendRadius:
    density = clamp(signedDistance, -8, 8)
    write { absoluteCell, density(fp16), materialId, flags }
```

Commit path:

```
chunkCoord = floorDiv(absoluteCell, 32)
localIndex = absoluteCell -> chunk-local flat index
SetCell(...)
VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch(...)
EnqueueVolumeRebuild(volume)
```

Scrap spawn:

```
count = 100
item = Data_TitaniumScrap
position = impactPoint + seeded radial scatter
PersistentWorldRegistry.TryRegisterDroppedItem(...)
```

## Penumbra / Resonance Math

Penumbra overlap remains the angular occultation basis:

```
d = angular separation between blocker center and sun center
r0 = sun angular radius
r1 = blocker angular radius

no overlap:       d >= r0 + r1
full coverage:    d <= abs(r1 - r0) and r1 >= r0
partial overlap:  normalized disc-overlap area / sun disc area
```

Lunar resonance:

```
alignmentDegrees = angle(moonA.direction, moonB.direction)
active = alignmentDegrees < 5
biolumMultiplier *= 3
floraGrowthMultiplier = 3
```

Ring shadow approximation:

```
sunAegirAlignment = saturate(dot(normalize(-sunDirection), normalize(aegirDirection)))
planeBand = 1 - smoothstep(ringPlaneWidth, ringPlaneWidth + softness, abs(dot(up, aegirDirection)))
shadow = saturate(sunAegirAlignment * planeBand) * ringShadowStrength
FinalGiantAbyssLight *= 1 - shadow
```

## Regression Model

CPU: cataclysm coordinator runs on `ISlowTickable`; meteor SDF mutation schedules the existing Burst carve job and commits through the existing delta processor. Current shear math is inside the existing buoyancy job path.

GC: no coroutine was added; no LINQ in hot paths; fog shadow data uses a fixed `Vector4[4]` array owned by `CelestialCataclysmSystem`. Measured GC proof is absent because MCP/Profiler was unavailable.

Memory: one bounded meteor fog shadow array and two dev-only smoke/debug scripts were added. No unbounded runtime cache was introduced.

Cadence: random events are slow-tick driven; EMP blackout uses module slow tick; time-lapse debugger registers as slow tick only in play mode.

Correctness risks:

- Cave flood/drain is water-level physical, not persisted SDF water occupancy. This is a deliberate schema limitation, not a completed Air-to-Water voxel swap.
- Meteor impact runtime behavior still needs PlayMode validation with an active voxel volume and `PersistentWorldRegistry`.
- Fog shadow visual correctness needs frame/debugger validation in scene lighting.

## Verification Evidence

- `Hecton8.Core` direct Roslyn compile: exit code 0, no SARIF error entries in `.codex-artifacts/csc-core-cataclysm-postcleanup-2026-05-05.json`.
- `Hecton8.Editor` direct Roslyn compile: exit code 0, no SARIF error entries in `.codex-artifacts/csc-editor-cataclysm-postcleanup-2026-05-05.json`.
- Unity batch automation compile/smoke: `C:\hades\unity-batch-automation-smoke-editor-after-route-capacity.log` reports `Tundra build success`, `ExtractorStorageRouteSmoke pass=True`, and `Application will terminate with return code 0`.
- Unity batch thermal smoke: `CodexArtifacts/unity-thermal-survival-smoke-2026-05-05-run4.log` reports `Tundra build success`, `ThermalSurvivalSmoke PASS`, and `Application will terminate with return code 0`.
- Complete scoped patch artifact: `.codex-artifacts/hecton8_celestial_cataclysm_2026-05-05.diff`.
- MCP console evidence: unavailable. `mcpforunity://instances` previously reported no connected Unity instance, so no MCP console zero-error claim is made.
