# CELESTIAL MECHANICS SPECIFICATION

STATUS: PENDING VERIFICATION

Reason: this specification has static ledger, compile-contract, shader-import, and event-bus evidence, but PlayMode behavior, profiler allocation trace, and long-duration orbital drift are not proven. May 4 documentation sweep evidence supersedes the older warning-free compile line for current global build truth.

## Mandates Applied

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `CORE_Weather_Abyssal_FlowField_Currents.txt`
- `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Evidence

- Project authority: `AGENTS.md`, `Docs/DOC_GOVERNANCE.md`.
- Unity version observed through MCP: `6000.4.1f1`.
- World scene readback source: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, captured through Unity MCP before editor reload returned to `00_BOOTSTRAP`.
- C# verification from the original spec pass: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` completed with `0 Warning(s), 0 Error(s)`.
- Current May 4 documentation-sweep compile truth: `Hecton8.Core.csproj` completed with `0 Warning(s), 0 Error(s)`.
- Unity import verification: console cleared, Unity refresh completed, post-refresh console returned `0 log entries`.
- `RenderSettings.skybox` scan result: only `HectonAtmosphereManager.AtmosphereDirector` owns the direct read/write.

## Orbital Ledger

| Body | Scene mode | Period | Angular size | Current role |
| --- | --- | ---: | ---: | --- |
| Sun | `HectonAtmosphereManager` daily rotation, fallback `HectonCelestialEngine.orbitalPeriod` | 3600 s / 1.0 h | Directional light, no body angular diameter in ledger | Light-bearing |
| GasGiant_Aegir | `FixedDirection`; observer-relative fixed sky position | `orbitalPeriodSeconds=1200 s` present but unused in FixedDirection | 32.25 deg | Visual, eclipse occluder, reflected-light source through runtime planet-shine directional light |
| Moon_Pelagia | `OrbitAroundParent` apparent sky-sphere orbit | 34200 s / 9.5 h | 1.00 deg | Visual-only, eclipse occluder after patch |
| Moon_Varda | `OrbitAroundParent` apparent sky-sphere orbit | 52200 s / 14.5 h | 1.35 deg | Visual-only, eclipse occluder after patch |
| Moon_Ione | `OrbitAroundParent` apparent sky-sphere orbit | 75600 s / 21.0 h | 2.40 deg | Visual-only, eclipse occluder after patch |
| Moon_Khepri | `OrbitAroundParent` apparent sky-sphere orbit | 111600 s / 31.0 h | 1.20 deg | Visual-only, eclipse occluder after patch |
| Moon_Thalos | `OrbitAroundParent` apparent sky-sphere orbit | 151200 s / 42.0 h | 1.80 deg | Visual-only, eclipse occluder after patch |
| Moon_Nammu | `OrbitAroundParent` apparent sky-sphere orbit | 205200 s / 57.0 h | 1.75 deg | Visual-only, eclipse occluder after patch |

## Orbital Period Math

The implemented moon orbits are apparent angular sky orbits, not Keplerian distance orbits. Runtime equation in `ObserverRelativeCelestialBody`:

```text
orbitAngleDeg = orbitPhaseOffsetDeg + (timeSeconds / orbitalPeriodSeconds) * 360
```

Angular rates:

| Body | Period | deg/s | deg/hour |
| --- | ---: | ---: | ---: |
| Sun cycle | 3600 s | 0.100000 | 360.000 |
| Moon_Pelagia | 34200 s | 0.010526 | 37.895 |
| Moon_Varda | 52200 s | 0.006897 | 24.828 |
| Moon_Ione | 75600 s | 0.004762 | 17.143 |
| Moon_Khepri | 111600 s | 0.003226 | 11.613 |
| Moon_Thalos | 151200 s | 0.002381 | 8.571 |
| Moon_Nammu | 205200 s | 0.001754 | 6.316 |

## Orbital Model Finding

No Keplerian solver was found. No semimajor axis, eccentric anomaly, mean anomaly, gravitational parameter, or true anomaly path was found in the celestial controller path.

Observed model:

- Sun: `HectonAtmosphereManager.RotateSun()` computes normalized cycle time, then applies `RotateX(daily)`, `RotateZ(inclination)`, `RotateY(azimuth)`. `HectonCelestialEngine` uses `HectonAtmosphereManager.SunAngle` when atmosphere is assigned; fallback is uniform circular rotation using `360 / orbitalPeriod`.
- Aegir: `ObserverRelativeCelestialBody.FixedDirection` keeps the parent gas giant fixed in observer-relative sky space.
- Other moons: `ObserverRelativeCelestialBody.OrbitAroundParent` builds a tangent/bitangent basis around the parent direction and offsets each body by an apparent angular radius. This is a visual sky-sphere orbit, not physical orbital mechanics.

## Tidal Lock Validation

The current implementation validates as a visual tidal lock, not a physical moon-frame simulation.

- Aegir remains constant in the sky because its observer-relative body is `FixedDirection`.
- Aegir phase changes because `HectonCelestialEngine.UpdateAegirMaterial()` computes:

```text
planetPhase = dot(toSun, aegirToPlayer)
```

- Planet-shine intensity follows:

```text
phaseFactor = saturate((rawPhase - newMoonThreshold) / (1 - newMoonThreshold))
phaseFactor = phaseFactor * phaseFactor
intensity = phaseFactor * (1 - eclipseBacklightFactor) * planetShineMaxIntensity
```

Boundary: this does not prove an AUP-consistent moon surface rotating in a parent-planet inertial frame. It proves the runtime sky presentation keeps Aegir fixed relative to the observer while sun lighting changes phase.

## Eclipse Signal Bus

Existing bus was present and has been extended.

Signal path:

```text
HectonCelestialEngine.DetectEclipse()
  -> CelestialEvents.RaiseEclipseStarted/RaiseEclipseEnded()
  -> SystemDispatcher late-frame CelestialEvents lane
  -> EclipseGameplaySystem.OnCelestialEclipseStarted/Ended()
  -> EclipseGameplayEvents.RaisePhaseChanged(...)
  -> SystemDispatcher late-frame EclipseGameplayEvents lane
```

Implemented extension:

- Previous eclipse detection evaluated Aegir only.
- Current detection evaluates Aegir plus cached `ObserverRelativeCelestialBody` moons.
- Each occluder uses angular separation from the sun against body angular radius plus hysteresis.
- No LINQ, string construction, class allocation, array allocation, or dynamic collection growth was added to Tick.

Abyss response already wired:

- Eclipse start sets `_EclipseBiolumMultiplier`.
- Eclipse phase and temperature delta flow through `EclipseGameplayEvents`.
- Predator-rise signal remains delayed by `predatorRiseDelay`.

## Global Uniforms

Requested names were not present:

- `_GlobalLightDirection`: not found.
- `_GiantAtmosphereColor`: not found.

Actual first-party uniforms observed and/or patched:

- `_SunDirection`
- `_AegirDirection`
- `_SkyColorZenith`
- `_SkyColorHorizon`
- `_SkyColorNadir`
- `_NightBlend`
- `_EclipseOcclusion`
- `_AtmosphereTransmittanceWeight`
- `_AtmosphereInscatterWeight`
- `_CelestialAtmosphereLUT`
- `_StarSeed`
- `_EclipseBiolumMultiplier`

## RenderSettings Ownership

Direct `RenderSettings.skybox` use has been centralized behind `AtmosphereDirector`.

Touched runtime paths:

- `HectonAtmosphereManager.cs`: added `AtmosphereDirector`.
- `HectonCelestialEngine.cs`: skybox writes now call `AtmosphereDirector.SetSkybox`.
- `HectonUnderwaterVisuals.cs`: runtime skybox handoff uses `AtmosphereDirector`.
- `RenderSettingsLifecycleGuard.cs`: snapshot/restore uses `AtmosphereDirector`.
- `SystemDispatcher.cs`: render-settings restoration uses `AtmosphereDirector`.

## Tidal Interaction

Before this pass, no hard gameplay link was found between moon phases and `HectonFluidEngine` water level/current intensity. The existing abyssal flow field consumed weather current and authored current volumes, but not parent-planet position.

Implemented `Giant's Wake`:

```text
horizontal = normalize(float3(aegirDirection.x, 0, aegirDirection.z))
wakeDirection = normalize(float3(horizontal.x, verticalBias, horizontal.z))
wakeBase = wakeDirection * giantWakeCurrentStrength
depthFade = saturate((waterLevel - sampleY - giantWakeDepthFadeStart) / giantWakeDepthFadeRange)
wakeCurrent = wakeBase * depthFade
```

Applied to:

- CPU buoyancy job sampled current.
- Mod/API flow sampling path.
- GPU abyssal flow upload through `_AbyssalFlowWeatherCurrent`.

Default authored values:

- `enableGiantWakeCurrent = true`
- `giantWakeCurrentStrength = 0.18`
- `giantWakeVerticalBias = -0.04`
- `giantWakeDepthFadeStart = 120`
- `giantWakeDepthFadeRange = 480`

## Atmospheric Refraction Bridge

Proposed formula for depth-sensitive Aegir reflected light:

```text
depthMeters = max(0, waterLevel - playerY)
sigmaRgbPerMeter = float3(0.0035, 0.0012, 0.00055)
waterTransmittance = exp(-depthMeters * sigmaRgbPerMeter)
giantDepthColor = giantSurfaceColor * waterTransmittance
biolumeTerm = abyssBiolumeColor * eclipseBiolumeMultiplier * biolumeMask
finalGiantAbyssLight = giantDepthColor * planetShineIntensity + biolumeTerm
```

Rationale: red attenuates fastest, blue persists longest. This formula should be driven by the same depth scalar already used by underwater visuals and by `_EclipseBiolumMultiplier` from the eclipse bus. This bridge is specified, not fully implemented in this pass.

## Star-Map Determinism

Previous path sampled `_StarTex`, which could be arbitrary imported texture data.

Implemented path:

- `HectonCelestialEngine` folds live `HectonWorldGenerator` noise seeds into `_StarSeed`.
- Fallback seed: `99173`.
- Fold equation:

```text
seed = (seed * 397) ^ layer.seed
starSeedFloat = seed & 0x00FFFFFF
```

- `Hecton_AlienSky_Master.shader` uses `_StarSeed` in the star hash.
- The star field is generated on a deterministic grid:

```text
starGrid = starUV * 128
starCell = floor(starGrid)
density = hash(starCell + seedOffset)
star exists when density >= 0.985
star core = smoothstep(0.055, 0, distanceToSeededCellPoint)
```

`_StarTex` remains declared for material compatibility but is no longer sampled by the star layer.

## Surgery Log

- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - Added multi-body eclipse detection.
  - Added `TryGetAegirSkyDirection` for downstream current systems.
  - Added world-seed-derived `_StarSeed` publish path.
  - Routed skybox ownership through `AtmosphereDirector`.
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
  - Added `Giant's Wake` current component.
  - Routed wake through CPU buoyancy, mod sampling, and GPU abyssal flow current upload.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  - Added `AtmosphereDirector` as single `RenderSettings.skybox` facade.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
  - Replaced direct skybox assignment with `AtmosphereDirector`.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - Replaced render-settings skybox restore with `AtmosphereDirector`.
- `Assets/_Project/Scripts/RenderSettingsLifecycleGuard.cs`
  - Replaced render-settings skybox snapshot/restore with `AtmosphereDirector`.
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
  - Added deterministic procedural star grid seeded by `_StarSeed`.

## Hot Path Impact

- Tick path: no new heap allocations, no LINQ, no strings, no per-frame array/list creation.
- Eclipse detection iterates the existing cached observer-body list.
- Giant wake uses scalar/vector math and existing job payload fields.
- Shader star path removes a texture sample and adds deterministic hash ALU.

## Failure Modes And Boundaries

- Not a Keplerian simulator. The current model is visually plausible, deterministic, and scene-authored.
- Aegir tidal lock is a presentation lock, not a full physical AUP moon-rotation proof.
- Eclipse bus is wired and compile/import clean; PlayMode signal timing was not captured in this pass.
- `Giant's Wake` is subtle by default and depends on `HectonCelestialEngine.ActiveRuntimeInstance`.
- If `HectonWorldGenerator.ActiveRuntimeInstance` is absent, star generation uses fallback seed `99173`.
- No profiler or GC allocation capture was run. Zero-GC compliance is based on code inspection plus compile/import verification.
